// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
namespace Sanctuary.Integration;

public static class MirroredWorldChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(MirroredWorldChecks));
    private sealed record Row(uint Owner,uint Weapon,uint Boots,uint Arrow,uint Bolt,uint Target,Point3D Origin,int Mana,int Life,int Durability,int Quantity,WeaponFlight[] Flights);
    private sealed record Saved(int Generation,Row[] Rows);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"mirrored-world-fixtures.json");
    private sealed class Guaranteed:Random{public override int Next(int n)=>0;}
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=p=>p.Name!="MirroredWorldFixture"&&hp?.Invoke(p)!=false;
        ClassGearHooks.CanRegenerateMana=p=>p.Name!="MirroredWorldFixture"&&mp?.Invoke(p)!=false;
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"mirrored-world-reload-check":"mirrored-world-check";
            ConsoleInputHandler.RegisterCommand(name,"Prepared complete Mirrored melee/bow/crossbow/throw action and original saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value&&f.Mirrored!=null).OrderBy(f=>f.Id).ToArray();
    private static GoatmanNpcContactChecks.Recipient Target(PlayerMobile p,int x)
    {
        var t=new GoatmanNpcContactChecks.Recipient{Name="MirroredWorldTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(50000);t.Hits=t.HitsMax;
        t.MoveToWorld(new(p.X+x,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();
        foreach(var r in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy}){t.SetResistance(r,0);}t.SetResistance(ResistanceType.Poison,100);return t;
    }
    private static PlayerMobile Fresh(int kind)
    {
        var p=CleaveActionChecks.Fresh(kind%2!=0);p.Name="MirroredWorldFixture";
        while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"World strength");}
        while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"World dexterity");}
        ClassSkillChecks.Train(p,"warlock.mirroredblades",7);p.AddToBackpack(CombatTiming.Equipped(p));
        BaseWeapon weapon;
        if(kind is 0 or 3){string key=kind==0?"Ripsaw":"Gimmershred";var d=NamedGear.Catalog.Items.Single(x=>x.SourceKey==key);Require(NamedGear.Available(d.Id),"Actual world weapon unavailable "+key);weapon=(BaseWeapon)NamedGear.Create(d.Id,99,new Random(173),true);}
        else{weapon=kind==1?new Bow():new Crossbow();ClassGear.Profiles.Convert(weapon,99,GearRarity.Normal,new Random(173));}
        p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"World original weapon");
        var boots=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Goblin Toe").Id,99,new Random(173),true);p.AddToBackpack(boots);Require(p.EquipItem(boots),"Actual crushing boots");
        p.AddToBackpack(new Arrow(30));p.AddToBackpack(new Bolt(40));ClassGear.Refresh(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);
        FixtureSpace.Corridor(p,6600+kind*40,24,3400);Ready(p);return p;
    }
    private static MirroredBladesAction.Pending Cast(PlayerMobile p,BaseCreature target,int? mana=null)
    {
        Ready(p);NecromancerEffects.SetMana256(p,mana??p.ManaMax*256);int before=NecromancerEffects.ManaBalance256(p);
        var a=MirroredBladesAction.Begin(p,target?.Serial??Serial.Zero,target?.Location??new(p.X+12,p.Y,p.Z),Core.TickCount);Require(a!=null,"Prepared SQ25 start");
        Require(!MirroredBladesAction.Resolve(p,a.EffectAt-1)||!a.Released,"Early action release");
        Require(NecromancerEffects.ManaBalance256(p)==before,"Windup paid");
        int? firstLandedMana=null;
        void Paid(WeaponLandedContact e){if(e.Source==p&&firstLandedMana==null){firstLandedMana=NecromancerEffects.ManaBalance256(p);}}
        WeaponLandedEvents.BeforeDamage+=Paid;
        try{Require(MirroredBladesAction.Resolve(p,a.EffectAt)&&a.Released&&a.Fault==null,"Release "+a.Fault);}
        finally{WeaponLandedEvents.BeforeDamage-=Paid;}
        // Observe the debit before ordinary Ripsaw mana leech recovers it.
        Require(before-(firstLandedMana??NecromancerEffects.ManaBalance256(p))==a.ManaPaid256,"Fractional payment accounting before leech");
        int attempts=a.Attempts,after=NecromancerEffects.ManaBalance256(p);MirroredBladesAction.Resolve(p,a.EffectAt);
        Require(a.Attempts==attempts&&NecromancerEffects.ManaBalance256(p)==after,"Replayed release/payment");return a;
    }
    private static void Contacts(PlayerMobile p,int kind,BaseCreature target)
    {
        int seen=0,firstLife=SkillHealth.Balance256(target),previous=firstLife;
        void Landed(WeaponLandedContact e)
        {
            if(e.Source!=p||e.Target!=target){return;}int life=SkillHealth.Balance256(target);
            Require(e.Ranged==(kind!=0),"Wrong weapon event family");
            if(seen==0){int div=kind==0?4:8;Require(life==firstLife-firstLife/div,"Primary ordinary Crushing Blow");}
            else{Require(life<(long)previous*97/100,"Duplicate did not dispatch its own Crushing Blow");}
            previous=life;seen++;
        }
        WeaponLandedEvents.BeforeDamage+=Landed;
        try
        {
            int quantity=NamedAmmunition.Remaining(CombatTiming.Equipped(p));int arrows=BowAmmunition.Stack(p,CombatTiming.Equipped(p))?.Amount??0;
            var a=Cast(p,target);Require(!a.UsedBasic&&a.Attempts==3&&a.ManaPaid256==SkillMechanics.Values(p,CombatAbility.MirroredBlades).Mana256,"Paid count/rank/cost");
            Require(a.Timing==MirroredBladesAction.Timing(p,MirroredBladesPackets.Capture(p).Skill),"Owned SQ25 attack-speed clock");
            if(kind!=0)
            {
                Require(a.Flights.Length==3&&Own(p).All(f=>f.Mirrored.Ordinal is >=0 and <=2&&f.NextDelay==0),"Independent clone missiles");
                var next=Own(p).Min(f=>f.NextUtc);WeaponMissiles.Tick(next.AddMilliseconds(400),onlyOwner:p.Serial.Value);
                Require(Own(p).Length==0,"Nonpiercing missiles failed to finish");
                if(kind==3){Require(NamedAmmunition.Remaining(CombatTiming.Equipped(p))==quantity-1,"Throw quantity charged per mirror");}
                else{Require(BowAmmunition.Stack(p,CombatTiming.Equipped(p)).Amount==arrows-1,"Quiver charged per mirror");}
            }
            Require(seen==3&&SkillHealth.Balance256(target)<firstLife,"Independent landed/CB contacts");
            if(kind==0){Require(TimedEffects.Snapshot().Any(x=>x.Owner==target.Serial.Value&&x.BleedMs>0),"Actual Ripsaw Open Wounds not dispatched");}
        }
        finally{WeaponLandedEvents.BeforeDamage-=Landed;WeaponMissiles.CancelMirrored(p);TimedEffects.Clear(target);Ready(p);}
    }
    private static void Controls(PlayerMobile p,int kind)
    {
        var target=Target(p,kind==0?1:4);try
        {
            Contacts(p,kind,target);var basic=Cast(p,target,0);Require(basic.UsedBasic&&basic.Attempts==1&&basic.ManaPaid256==0,"Insufficient mana did not give one basic attack");
            WeaponMissiles.CancelMirrored(p);Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);
            int mana=NecromancerEffects.ManaBalance256(p);var a=MirroredBladesAction.Begin(p,target.Serial,target.Location,Core.TickCount);Require(a!=null,"Cancellation start");ClassCombat.Stop(p);
            Require(!MirroredBladesAction.Resolve(p,a.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana,"Cancellation paid");
            Ready(p);a=MirroredBladesAction.Begin(p,target.Serial,target.Location,Core.TickCount);var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);
            Require(!MirroredBladesAction.Resolve(p,a.EffectAt)&&Own(p).Length==0,"Moved windup emitted");p.MoveToWorld(origin,p.Map);Ready(p);
            if(kind!=0)
            {
                Cast(p,null);var next=Own(p).Min(f=>f.NextUtc);var destination=new Point3D(p.X+16,p.Y,p.Z);
                var town=new GuardedRegion("MirroredTown"+p.Serial.Value,p.Map,1000,new Rectangle2D(destination.X-2,destination.Y-2,5,5));town.Register();
                try{p.MoveToWorld(destination,p.Map);mana=NecromancerEffects.ManaBalance256(p);WeaponMissiles.Tick(next,onlyOwner:p.Serial.Value);Require(Own(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Safe-area flight retirement refunded/reset");}
                finally{p.MoveToWorld(origin,p.Map);town.Unregister();Ready(p);}
            }
            Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);mana=NecromancerEffects.ManaBalance256(p);
            a=MirroredBladesAction.Begin(p,target.Serial,target.Location,Core.TickCount);var equipped=CombatTiming.Equipped(p);p.AddToBackpack(equipped);
            Require(!MirroredBladesAction.Resolve(p,a.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana&&Own(p).Length==0,"Weapon change released/paid");
            Require(p.EquipItem(equipped),"Restore original world weapon");Ready(p);
            if(kind is 1 or 2)
            {
                var ammunition=BowAmmunition.Stack(p,equipped);var bag=new Bag();bag.DropItem(ammunition);
                try
                {
                    a=MirroredBladesAction.Begin(p,target.Serial,target.Location,Core.TickCount);Require(a!=null,"No-ammo prepared windup");
                    Require(!MirroredBladesAction.Resolve(p,a.EffectAt)&&a.Released&&a.ManaPaid256==0&&Own(p).Length==0
                        &&NecromancerEffects.ManaBalance256(p)==mana,"No-ammo release paid/allocated");
                }
                finally{p.AddToBackpack(ammunition);bag.Delete();Ready(p);}
            }
            if(kind==0)
            {
                SkillHealth.SetBalance256(target,1);a=Cast(p,target);
                Require(!target.Alive&&a.Attempts==1,"Melee copies continued after initial target died");
                var empty=Cast(p,null);Require(empty.Attempts==0&&empty.ManaPaid256>0,"Empty paid melee release policy changed");
            }
            if(kind==1)
            {
                target.Delete();var choice=SourceBlocking.ChoiceRoll;var block=SourceBlocking.BlockRoll;int rolls=0;
                SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>{rolls++;return 0;};
                var shield=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};shield.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);shield.AIObject?.AITimer.Stop();
                try
                {
                    Require(SourceBlocking.TryRate(shield,out int chance,out _)&&chance==10,"Actual original skeleton shield");int hp=SkillHealth.Balance256(shield);
                    a=Cast(p,shield);var next=Own(p).Min(f=>f.NextUtc);WeaponMissiles.Tick(next.AddMilliseconds(400),onlyOwner:p.Serial.Value);
                    Require(rolls==3&&Own(p).Length==0&&SkillHealth.Balance256(shield)==hp,"Independent copies bypassed actual shield block");
                }
                finally{SourceBlocking.ChoiceRoll=choice;SourceBlocking.BlockRoll=block;shield.Delete();Ready(p);}
            }
            if(kind==3)
            {
                // Each independent lifecycle control starts a real released set.
                Cast(p,null);var next=Own(p).Min(f=>f.NextUtc);int hp=SkillHealth.Balance256(target);
                WeaponMissiles.Tick(next.AddMilliseconds(2000),onlyOwner:p.Serial.Value);
                Require(Own(p).Length==0&&SkillHealth.Balance256(target)==hp,"Overdue expiry replayed damage");
                Cast(p,null);next=Own(p).Min(f=>f.NextUtc);p.MoveToWorld(origin,Map.Felucca);
                WeaponMissiles.Tick(next,onlyOwner:p.Serial.Value);Require(Own(p).Length==0,"Facet travel retained flight");p.MoveToWorld(origin,Map.Trammel);Ready(p);
                Cast(p,null);next=Own(p).Min(f=>f.NextUtc);var net=p.NetState;net.Mobile=null;p.NetState=null;
                WeaponMissiles.Tick(next,onlyOwner:p.Serial.Value);Require(Own(p).Length==0,"Offline flight kept fighting");net.Mobile=p;p.NetState=net;
                FixtureSpace.Place(p,origin,Map.Trammel);Ready(p);
                Cast(p,null);p.AddToBackpack(equipped);var boots=p.FindItemOnLayer(Layer.Shoes);p.AddToBackpack(boots);Ready(p);p.Combatant=null;
                // Meet the actual history service's equipment/combat preconditions.
                foreach(var info in p.Aggressed.ToArray()){p.RemoveAggressed(info.Defender);info.Defender.RemoveAggressor(p);}
                foreach(var info in p.Aggressors.ToArray()){p.RemoveAggressor(info.Attacker);info.Attacker.RemoveAggressed(p);}
                Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Sorceress)&&Own(p).Length==0,"Class history retained released missiles");
                Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Warlock)&&p.EquipItem(equipped),"Restore original Warlock history/weapon");
                Require(p.EquipItem(boots),"Restore original boots after history");Ready(p);Cast(p,null);var arrow=p.Backpack.FindItemByType<Arrow>();var bolt=p.Backpack.FindItemByType<Bolt>();
                p.Kill();Require(!p.Alive&&Own(p).Length==0,"Actual player death retained released missiles");p.Resurrect();
                foreach(var item in new Item[]{equipped,boots,arrow,bolt}){p.AddToBackpack(item);}
                Require(p.EquipItem(equipped)&&p.EquipItem(boots),"Restore original equipment after fixture death");Ready(p);
                Log.Information("Mirrored world:actual expired/facet/offline/class-history/death controls retire released throws without replay;original equipment restored");
            }
            Require(!WarlockCombat.Implemented(CombatAbility.MirroredBlades)&&!SkillBindings.Instance.Assign(p,true,CombatAbility.MirroredBlades),"Prepared world admitted learned route");
        }
        finally{WeaponMissiles.CancelMirrored(p);ClassCombat.Stop(p);target.Delete();}
    }
    private static void Scene(NetState network)
    {
        var hit=MirroredBladesContacts.HitRoll;var random=MirroredBladesContacts.Effects;var seed=MirroredBladesAction.SeedLow;
        MirroredBladesContacts.HitRoll=()=>0;MirroredBladesContacts.Effects=new Guaranteed();MirroredBladesAction.SeedLow=()=>71;
        var rows=new List<Row>();
        try
        {
            for(int kind=0;kind<4;kind++)
            {
                var p=Fresh(kind);var account=FixtureNetwork.Authenticate(network,p,"mirrored-world-");bool kept=false;
                try
                {
                    Controls(p,kind);BaseCreature target=null;
                    if(kind!=0)
                    {
                        target=Target(p,4);Cast(p,null);var original=WeaponMissiles.Snapshot();var ids=Own(p).Select(f=>f.Id).ToHashSet();
                        // Explicit future inspection dates isolate persistence from the real two-second flight lifetime.
                        WeaponMissiles.Restore(original with{Flights=original.Flights.Select(f=>ids.Contains(f.Id)?f with{NextUtc=f.NextUtc.Add(FixtureSpace.Retention)}:f).ToArray()});
                    }
                    SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);NecromancerEffects.SetMana256(p,p.ManaMax*256-17);
                    var weapon=CombatTiming.Equipped(p);var boots=p.FindItemOnLayer(Layer.Shoes);var arrow=p.Backpack.FindItemByType<Arrow>();var bolt=p.Backpack.FindItemByType<Bolt>();
                    rows.Add(new(p.Serial.Value,weapon.Serial.Value,boots.Serial.Value,arrow.Serial.Value,bolt.Serial.Value,target?.Serial.Value??0,p.Location,
                        NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),weapon.HitPoints,NamedAmmunition.Remaining(weapon),Own(p)));kept=true;
                }
                finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){WeaponMissiles.CancelMirrored(p);p.Delete();}}
            }
            var full=WeaponMissiles.Snapshot();var first=full.Flights.First(f=>f.Mirrored!=null);bool refused=false;
            try{WeaponMissiles.Restore(full with{Flights=full.Flights.Select(f=>f.Id==first.Id?f with{Mirrored=f.Mirrored with{Ordinal=99}}:f).ToArray()});}
            catch(InvalidDataException){refused=true;}
            Require(refused&&JsonSerializer.Serialize(WeaponMissiles.Snapshot())==JsonSerializer.Serialize(full),"Invalid new metadata partly replaced existing flights");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,rows.ToArray())));
            Log.Information("Mirrored world:all four original weapon kinds pass SQ25/one payment/independent primary-copy contacts and Crushing Blow,melee Open Wounds,one quiver-or-throw debit,basic fallback,cancel/move/safe-area retirement;learned route gated");
        }
        finally{MirroredBladesContacts.HitRoll=hit;MirroredBladesContacts.Effects=random;MirroredBladesAction.SeedLow=seed;}
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var hit=MirroredBladesContacts.HitRoll;MirroredBladesContacts.HitRoll=()=>0;
        try
        {
            foreach(var row in saved.Rows)
            {
                var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var account=FixtureNetwork.Authenticate(network,p,"mirrored-world-reload-");
                try
                {
                    var w=(BaseWeapon)World.FindItem((Serial)row.Weapon);
                    Require(w.Parent==p&&w.HitPoints==row.Durability&&NamedAmmunition.Remaining(w)==row.Quantity&&World.FindItem((Serial)row.Boots)?.Parent==p
                        &&World.FindItem((Serial)row.Arrow)?.Parent==p.Backpack&&World.FindItem((Serial)row.Bolt)?.Parent==p.Backpack
                        &&SkillHealth.Balance256(p)==row.Life&&NecromancerEffects.ManaBalance256(p)==row.Mana&&ClassCombat.For(p).Mirrored==null,"Saved original owner/gear/resources or pending replay");
                    Require(JsonSerializer.Serialize(Own(p))==JsonSerializer.Serialize(row.Flights),"Saved original projectile packet/ordinal/critical/seed/clock changed");
                    if(row.Flights.Length>0)
                    {
                        FixtureSpace.Place(p,row.Origin,Map.Trammel);var target=(BaseCreature)World.FindMobile((Serial)row.Target);target.AIObject?.AITimer.Stop();int hp=SkillHealth.Balance256(target);
                        var live=WeaponMissiles.Snapshot();var next=row.Flights.Min(f=>f.NextUtc);
                        WeaponMissiles.Tick(next.AddTicks(-1),onlyOwner:p.Serial.Value);Require(SkillHealth.Balance256(target)==hp,"Early post-load projectile replay");
                        WeaponMissiles.Tick(next.AddMilliseconds(400),onlyOwner:p.Serial.Value);Require(Own(p).Length==0&&SkillHealth.Balance256(target)<hp,"Saved projectiles failed actual continuation");
                        WeaponMissiles.Restore(live);SkillHealth.SetBalance256(target,hp);TimedEffects.Clear(target);
                    }
                }
                finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
            }
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Log.Information("Mirrored world:original four-kind owners/weapons/boots/ammo/quantity/fractional resources and nine primary-copy missiles/metadata/seeds/clocks retain and resume through actual reload;inspection dates only");
        }
        finally{MirroredBladesContacts.HitRoll=hit;}
    }
}
