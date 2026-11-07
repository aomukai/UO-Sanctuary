// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Spells;
using Server.Spells.Fourth;
namespace Sanctuary.Integration;

public static class DragonFlightChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DragonFlightChecks));
    private const CombatAbility Ability=CombatAbility.DragonFlight;
    private sealed record Saved(uint Owner,uint Target,uint Boots,Point3D Position,int Mana,int Life,int TargetLife,MartialCharge Charge,uint Unpaid,uint UnpaidTarget,int UnpaidMana,uint Paid,uint PaidTarget,Point3D PaidPosition,int PaidMana,int PendingHealth);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"dragon-flight-fixtures.json");
    private sealed class Fixed:Random{public override int Next(int max)=>Math.Max(0,max-1);}
    private sealed class Control:IDisposable
    {
        private readonly Func<double> Hit=DragonFlights.HitRoll,Element=DragonFlights.ElementRoll;
        private readonly Func<int,int> Physical=DragonFlights.PhysicalRoll,Poison=DragonFlights.PoisonRoll,Block=SourceBlocking.BlockRoll,Choice=SourceBlocking.ChoiceRoll;
        private readonly Random Effects=DragonFlights.Effects;
        internal Control(){DragonFlights.HitRoll=DragonFlights.ElementRoll=()=>0;DragonFlights.PhysicalRoll=DragonFlights.PoisonRoll=_=>0;DragonFlights.Effects=new Fixed();SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>99;}
        public void Dispose(){DragonFlights.HitRoll=Hit;DragonFlights.ElementRoll=Element;DragonFlights.PhysicalRoll=Physical;DragonFlights.PoisonRoll=Poison;DragonFlights.Effects=Effects;SourceBlocking.ChoiceRoll=Choice;SourceBlocking.BlockRoll=Block;}
    }
    private static bool Creating;
    private static void Rates()
    {
        var mana=Mobile.ManaRegenRateHandler;var life=Mobile.HitsRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>Creating||p.Name?.StartsWith("DragonFlightFixture",StringComparison.Ordinal)==true?FixtureSpace.Retention:mana?.Invoke(p)??Mobile.DefaultManaRate;
        Mobile.HitsRegenRateHandler=p=>Creating||p.Name?.StartsWith("DragonFlightFixture",StringComparison.Ordinal)==true?FixtureSpace.Retention:life?.Invoke(p)??Mobile.DefaultHitsRate;
    }
    public static void Configure()
    {
        Rates();Register("dragon-flight-check",Save);Register("dragon-flight-reload-check",Reload);Register("dragon-flight-mechanics-check",Mechanics);
        foreach(string mode in new[]{"left","right","quick"}){string label="dragon-flight-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        Rates();if(!File.Exists(Manifest)){return;}var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        foreach(uint id in new[]{s.Target,s.UnpaidTarget,s.PaidTarget}){if(World.FindMobile((Serial)id) is BaseCreature t){t.Frozen=t.CantWalk=true;t.AIObject?.AITimer.Stop();}}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Approved two-stage Flight landing/payment/kick/input/lifecycle",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh(int column=6910,int row=3500)
    {
        Creating=true;try
        {
            var p=BladeBodyChecks.Fresh(column,40,row);p.Name="DragonFlightFixture";while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}while(p.RawInt<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Energy),"Energy");}
            var weapon=new Dagger();ClassGear.Profiles.Convert(weapon,99,GearRarity.Normal,new Random(173));p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Actual primary dagger");ClassGear.Refresh(p);NecromancerEffects.SetMana256(p,200*256+17);return p;
        }
        finally{Creating=false;}
    }
    private static BaseCreature Target(PlayerMobile p,int dx=12)
    {
        var t=new CobraPassiveTarget{Name="DragonFlightFixtureTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;
        foreach(var r in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(r,0);}return t;
    }
    private static Item Boots(PlayerMobile p)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Gorerider").Id,99,new Fixed(),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Original Gorerider");ClassGear.Refresh(p);return item;}
    private static void Hold(PlayerMobile p,int count=3)
    {
        var v=SkillMechanics.Values(p,CombatAbility.TigerStrike);var charge=new MartialCharge(p.Serial.Value,CombatAbility.TigerStrike,count,v.Rank,count*(int)v.Eval("aurastatcalc2"),TimedEffects.Now.Add(FixtureSpace.Retention));
        AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=p.Serial.Value||x.Ability!=CombatAbility.TigerStrike).Append(charge).ToArray());
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static DragonFlights.Flight Begin(PlayerMobile p,BaseCreature t)
    {Ready(p);Require(ClassCombat.Perform(p,Ability,t,t.Location,Core.TickCount),"Learned Flight begin");return ClassCombat.For(p).Flight;}
    private static void Warp(PlayerMobile p)
    {var f=ClassCombat.For(p).Flight;Require(!ClassCombat.ResolveAction(p,f.TeleportAt-1)&&ClassCombat.ResolveAction(p,f.TeleportAt)&&ClassCombat.For(p).Flight is{Landed:true,Kicked:false},"Own teleport lost its pending kick or wrong event");}
    private static bool Kick(PlayerMobile p)=>ClassCombat.ResolveAction(p,ClassCombat.For(p).Flight.KickAt);
    private static void End(PlayerMobile p)
    {if(ClassCombat.For(p).Flight is{} f){Require(ClassCombat.ResolveAction(p,f.Until)&&ClassCombat.For(p).Flight==null,"Full sequence completion");}}
    private sealed class BlockRegion(Map map,Point3D p):BaseRegion("Flight travel fixture",map,250,new Rectangle3D(p.X-4,p.Y-4,-128,8,8,256))
    {public override bool CheckTravel(Mobile owner,Point3D point,TravelCheckType type,out TextDefinition message){message=null;return false;}}
    private sealed class PaymentField:FireFieldItem
    {
        internal PaymentField(Point3D point,Mobile caster,Map map):base(0x398C,point,caster,map,TimeSpan.FromMinutes(1),0){}
        public PaymentField(Serial serial):base(serial){}
        internal int EntryMana=-1;
        public override bool OnMoveOver(Mobile m){if(m is PlayerMobile p){EntryMana=NecromancerEffects.ManaBalance256(p);}return base.OnMoveOver(m);}
    }
    private static void Arrival()
    {
        var p=Fresh(6970);var t=Target(p);var caster=new Ogre{Frozen=true,CantWalk=true,Kills=5};caster.MoveToWorld(new(p.X-2,p.Y,p.Z),p.Map);caster.AIObject?.AITimer.Stop();PaymentField field=null;
        try
        {
            var f=Begin(p,t);Require(DragonFlightLanding.Destination(p,t,out var point),"Arrival destination");field=new(point,caster,p.Map){Visible=true};int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t);SkillHealth.SetBalance256(p,256);
            Require(ClassCombat.ResolveAction(p,f.TeleportAt)&&field.EntryMana==mana-3840&&!p.Alive&&ClassCombat.For(p).Flight==null&&SkillHealth.Balance256(t)==hp,"Arrival hazard did not see paid commit or resurrected pending kick");
        }
        finally{field?.Delete();caster.Delete();t.Delete();p.Corpse?.Delete();p.Delete();}
    }
    private static void Mechanics()
    {
        using var control=new Control();var p=Fresh();var t=Target(p);var initial=p.Location;
        try
        {
            ClassSkillChecks.Train(p,"assassin.dragonflight",20);Require(AssassinCombat.Implemented(Ability)&&ClassCombat.Available(p,Ability)&&SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Learned bindings");
            Hold(p);var original=AssassinCharges.State(p);int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t);var f=Begin(p,t);
            Require(NecromancerEffects.ManaBalance256(p)==mana&&ClassCombat.For(p).Timing==new ActionTiming(23,9)&&f.KickAt-f.TeleportAt==200&&f.Until-f.TeleportAt==560,"Fixed two-event clock or upfront debit");
            t.MoveToWorld(new(initial.X+24,initial.Y,initial.Z),t.Map);Warp(p);Require(p.Location!=initial&&NecromancerEffects.ManaBalance256(p)==mana-3840&&SkillHealth.Balance256(t)==hp&&AssassinCharges.State(p)==original,"Landing repeated start range,paid twice or kicked early");
            var kick=DragonTails.PrepareKick(p,t,f.KickAt,Ability);int physical=NamedElements.To256(kick.Kick.Impact.Parts.Physical);
            Require(kick.Contact.HitFlags==3&&kick.Kick.Input.SkillDamagePercent==100+35*19+AssassinCharges.TigerPercent(original,SkillMechanics.Rank(p,CombatAbility.TigerStrike)),"Flight skill ED/body/boot packet or kick flags");
            Require(!ClassCombat.ResolveAction(p,f.KickAt-1)&&Kick(p)&&!Kick(p)&&hp-SkillHealth.Balance256(t)==physical&&AssassinCharges.State(p)?.Count==2&&NecromancerEffects.ManaBalance256(p)==mana-3840,"Exactly one paid kick/charge use");End(p);
            AssassinCharges.Clear(p);DragonFlights.HitRoll=()=>1;mana=NecromancerEffects.ManaBalance256(p);hp=SkillHealth.Balance256(t);f=Begin(p,t);Warp(p);kick=DragonTails.PrepareKick(p,t,f.KickAt,Ability);Require(kick.Contact.HitFlags==0&&(kick.Flags&1)==0,"Miss copied kick hit flags");Kick(p);End(p);Require(SkillHealth.Balance256(t)==hp&&NecromancerEffects.ManaBalance256(p)==mana-3840,"Miss refund or damage");
            Hold(p);Begin(p,t);Warp(p);Kick(p);End(p);Require(AssassinCharges.State(p)?.Count==2,"Charged accuracy-only bypass");
            var shield=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};shield.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);shield.AIObject?.AITimer.Stop();shield.SetHits(50000);shield.Hits=shield.HitsMax;
            try{SourceBlocking.BlockRoll=_=>0;original=AssassinCharges.State(p);hp=SkillHealth.Balance256(shield);mana=NecromancerEffects.ManaBalance256(p);Begin(p,shield);Warp(p);Kick(p);End(p);Require(AssassinCharges.State(p)==original&&SkillHealth.Balance256(shield)==hp&&NecromancerEffects.ManaBalance256(p)==mana-3840,"Charged shield block consumed/refunded/damaged");}finally{shield.Delete();SourceBlocking.BlockRoll=_=>99;}
            DragonFlights.HitRoll=()=>0;Hold(p);t.SetResistance(ResistanceType.Physical,100);hp=SkillHealth.Balance256(t);Begin(p,t);Warp(p);Kick(p);End(p);Require(SkillHealth.Balance256(t)==hp&&AssassinCharges.State(p)?.Count==2,"Absorbed contact did not consume charge");t.SetResistance(ResistanceType.Physical,0);
            foreach(bool landed in new[]{false,true})foreach(string cause in new[]{"stop","step-return","weapon","boots","recovery","frozen","facet"})
            {
                Hold(p);f=Begin(p,t);if(landed){Warp(p);}original=AssassinCharges.State(p);mana=NecromancerEffects.ManaBalance256(p);hp=SkillHealth.Balance256(t);var origin=p.Location;var map=p.Map;Item item=null;
                switch(cause)
                {
                    case "stop":ClassCombat.Stop(p);break;case "step-return":p.MoveToWorld(new(p.X-1,p.Y,p.Z),p.Map);p.MoveToWorld(origin,map);break;
                    case "weapon":item=CombatTiming.Equipped(p);p.AddToBackpack(item);ClassGear.Refresh(p);break;case "boots":item=Boots(p);break;
                    case "recovery":Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount),"Recovery event");break;case "frozen":p.Frozen=true;break;case "facet":p.MoveToWorld(p.Location,Map.Felucca);break;
                }
                Require(!ClassCombat.ResolveAction(p,landed?f.KickAt:f.TeleportAt)&&ClassCombat.For(p).Flight==null&&SkillHealth.Balance256(t)==hp&&NecromancerEffects.ManaBalance256(p)==mana&&AssassinCharges.State(p)==original,"Pre/post-landing interruption replay/refund: "+cause);
                p.Frozen=false;p.MoveToWorld(origin,map);if(cause=="weapon"){Require(p.EquipItem(item),"Restore dagger");ClassGear.Refresh(p);}if(cause=="boots"){p.AddToBackpack(item);ClassGear.Refresh(p);}
            }
            Hold(p);f=Begin(p,t);var block=new BlockRegion(p.Map,p.Location);block.Register();original=AssassinCharges.State(p);mana=NecromancerEffects.ManaBalance256(p);hp=SkillHealth.Balance256(t);var before=p.Location;
            try{Require(!ClassCombat.ResolveAction(p,f.TeleportAt)&&p.Location==before&&ClassCombat.For(p).Flight==null&&NecromancerEffects.ManaBalance256(p)==mana&&AssassinCharges.State(p)==original&&SkillHealth.Balance256(t)==hp,"Failed travel spent mana or kicked");}finally{block.Unregister();}
            f=Begin(p,t);NecromancerEffects.SetMana256(p,3839);Require(!ClassCombat.ResolveAction(p,f.TeleportAt)&&p.Location==before&&ClassCombat.For(p).Flight==null&&NecromancerEffects.ManaBalance256(p)==3839&&AssassinCharges.State(p)==original,"Late mana shortfall moved/paid/consumed");NecromancerEffects.SetMana256(p,200*256+17);
            f=Begin(p,t);Warp(p);mana=NecromancerEffects.ManaBalance256(p);hp=SkillHealth.Balance256(t);var old=t.Location;t.MoveToWorld(new(t.X+10,t.Y,t.Z),t.Map);Require(!ClassCombat.ResolveAction(p,f.KickAt)&&ClassCombat.For(p).Flight==null&&NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(t)==hp&&AssassinCharges.State(p)==original,"Post-landing lost target refunded or consumed");t.MoveToWorld(old,t.Map);
            AssassinCharges.Clear(p);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);NecromancerEffects.SetMana256(p,3839);Ready(p);Require(ClassCombat.Perform(p,Ability,t,t.Location,Core.TickCount)&&ClassCombat.For(p).PendingAbility==CombatAbility.BasicAttack&&ClassCombat.For(p).Flight==null,"Underfunded ordinary fallback");ClassCombat.Stop(p);
            NecromancerEffects.SetMana256(p,3840);f=Begin(p,t);Require(ClassCombat.ResolveAction(p,f.Until)&&ClassCombat.For(p).Flight==null&&NecromancerEffects.ManaBalance256(p)==0,"Ordered late-tick teleport/kick/end catch-up or exact threshold");
            Log.Information("Flight action:fixed9/14/23,current-target landing,commit15mana,kick ED/flags/charges,miss/block/absorb,14pre/post interruptions,failed travel/late mana,target loss,basic fallback and ordered late-tick catch-up;modern timing/payment/charge,full item/PvP/client and native geometry limits remain");
        }
        finally{t.Corpse?.Delete();t.Delete();p.Corpse?.Delete();p.Delete();}
        Arrival();
    }
    private static void Save()
    {
        using var control=new Control();var p=Fresh(6930);ClassSkillChecks.Train(p,"assassin.dragonflight",20);var boots=Boots(p);var t=Target(p);Require(SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Saved bindings");Hold(p);Begin(p,t);Warp(p);Kick(p);End(p);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-257);
        var unpaid=Fresh(6930,3600);var u=Target(unpaid);Begin(unpaid,u);var us=ClassCombat.For(unpaid);long deadline=Core.TickCount+FixtureSpace.RetentionMilliseconds;us.Flight=us.Flight with{TeleportAt=deadline,KickAt=deadline+200,Until=deadline+560};us.EffectAt=deadline;us.NextAction=deadline+560;
        var paid=Fresh(6930,3700);var v=Target(paid);Begin(paid,v);Warp(paid);var ps=ClassCombat.For(paid);ps.Flight=ps.Flight with{KickAt=deadline,Until=deadline+360};ps.EffectAt=deadline;ps.NextAction=deadline+360;
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,boots.Serial.Value,p.Location,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),SkillHealth.Balance256(t),AssassinCharges.State(p),unpaid.Serial.Value,u.Serial.Value,NecromancerEffects.ManaBalance256(unpaid),paid.Serial.Value,v.Serial.Value,paid.Location,NecromancerEffects.ManaBalance256(paid),SkillHealth.Balance256(v))));
    }
    private static void Reload()
    {
        using var control=new Control();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var unpaid=(PlayerMobile)World.FindMobile((Serial)s.Unpaid);var paid=(PlayerMobile)World.FindMobile((Serial)s.Paid);
        Require(World.FindItem((Serial)s.Boots)?.Parent==p&&SkillMechanics.Rank(p,Ability)==20&&p.Location==s.Position&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(t)==s.TargetLife&&AssassinCharges.State(p)==s.Charge,"Original moved owner/equipment/rank/wounds/mana/charge changed");
        Require(ClassCombat.For(unpaid).Flight==null&&ClassCombat.For(paid).Flight==null&&ClassCombat.For(unpaid).PendingAbility==CombatAbility.None&&ClassCombat.For(paid).PendingAbility==CombatAbility.None&&NecromancerEffects.ManaBalance256(unpaid)==s.UnpaidMana&&NecromancerEffects.ManaBalance256(paid)==s.PaidMana&&paid.Location==s.PaidPosition&&SkillHealth.Balance256(World.FindMobile((Serial)s.UnpaidTarget))==s.PendingHealth&&SkillHealth.Balance256(World.FindMobile((Serial)s.PaidTarget))==s.PendingHealth,"Unpaid/paid stage replayed,refunded or damaged after reload");
        Require(SkillBindings.Instance.Get(p)==(Ability,Ability)&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Assassin,7)==Ability,"Saved bindings changed");p.MoveToWorld(s.Position,Map.Trammel);Begin(p,t);Warp(p);Kick(p);End(p);Require(AssassinCharges.State(p)?.Count==1,"Saved charge failed Flight");AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=p.Serial.Value).Append(s.Charge).ToArray());NecromancerEffects.SetMana256(p,s.Mana);SkillHealth.SetBalance256(p,s.Life);SkillHealth.SetBalance256(t,s.TargetLife);p.MoveToWorld(s.Position,Map.Trammel);p.Internalize();
    }
    private static void Input(NetState network,string label)
    {
        var control=new Control();var p=Fresh(6950);var t=Target(p);Hold(p);Require(SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Natural bindings");FixtureNetwork.Authenticate(network,p,"dragon-flight-input-");Ready(p);
        int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t);var origin=p.Location;bool windup=false,landed=false;long deadline=Core.TickCount+5000;
        void Observe()
        {
            try
            {
                var s=ClassCombat.For(p);if(!windup&&s.PendingAbility==Ability){Require(s.Flight is{Landed:false}&&NecromancerEffects.ManaBalance256(p)==mana&&s.Timing==new ActionTiming(23,9),"Input upfront debit or wrong clock");windup=true;Log.Information("{Label}: windup",label);}
                if(p.Location!=origin){Require(NecromancerEffects.ManaBalance256(p)==mana-3840,"Natural landing payment");landed=true;}
                if(s.Flight!=null||SkillHealth.Balance256(t)==hp){Require(Core.TickCount<deadline,"Natural Flight timeout");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(windup&&landed&&NecromancerEffects.ManaBalance256(p)==mana-3840&&AssassinCharges.State(p)?.Count==2,"Natural two stages/debit/charge delivery");control.Dispose();t.Delete();p.Delete();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){control.Dispose();t.Delete();p.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Assassin,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
