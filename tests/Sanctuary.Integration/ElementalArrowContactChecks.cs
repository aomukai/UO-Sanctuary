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
namespace Sanctuary.Integration;

public static class ElementalArrowContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ElementalArrowContactChecks));
    private static readonly CombatAbility[] Abilities=[CombatAbility.FireArrow,CombatAbility.ColdArrow];
    private sealed record Saved(uint Owner,uint Target,uint Bow,uint Gloves,uint Helm,uint Ammo,Point3D Point,string Gear,string Profile,WeaponFlight Flight,int Life,int TargetLife,int Mana,int Quantity,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"elemental-arrow-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static WeaponFlight[] Flights(PlayerMobile p)=>MagicArrowContactChecks.Flights(p);
    private static PlayerProcSave Own(PlayerMobile p,Item bow)=>MagicArrowContactChecks.Own(p,bow);
    private static ResistanceType Element(CombatAbility a)=>a==CombatAbility.FireArrow?ResistanceType.Fire:ResistanceType.Cold;
    private static Item Ammo(PlayerMobile p)=>BowAmmunition.Stack(p,CombatTiming.Equipped(p));
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"elemental-arrow-contact-reload-check":"elemental-arrow-contact-check";ConsoleInputHandler.RegisterCommand(name,"Fire/Cold Arrow ordered conversion,source poison,procs,cold and saved originals",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(var ability in Abilities)foreach(string mode in new[]{"right","quick"})
        {var a=ability;string label="elemental-arrow-"+(a==CombatAbility.FireArrow?"fire":"cold")+"-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated elemental arrow conversion and item hit proc",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label,a));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(int row,CombatAbility ability,bool crossbow,out BaseWeapon bow,out Item gloves,out Item helm)
    {var p=MagicArrowContactChecks.Owner(row,crossbow,out bow,out gloves,out helm);ClassSkillChecks.Train(p,SkillMechanics.For(ability).TalentId);p.Backpack.FindItemByType<Arrow>().Amount=100;p.Backpack.FindItemByType<Bolt>().Amount=100;Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Elemental arrow bindings");return p;}
    private static PoisonDaggerContactChecks.Recipient Target(PlayerMobile p,CombatAbility ability,bool physical=false,int resistance=0)
    {var t=MagicArrowContactChecks.Target(p);t.SetResistance(Element(ability),resistance);if(physical){t.SetResistance(ResistanceType.Physical,0);}return t;}
    private static WeaponFlight Launch(PlayerMobile p,CombatAbility ability,BaseCreature t)
    {
        int mana=NecromancerEffects.ManaBalance256(p),quantity=Ammo(p).Amount,cost=SkillMechanics.Values(p,ability).Mana256;
        Require(ClassCombat.Perform(p,ability,t,t.Location,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity,"Elemental arrow start phase");ClassCombat.ReleaseCast(p);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Elemental arrow release");var f=Flights(p).Single();string code=ability==CombatAbility.FireArrow?"fire":"cold";
        Require(BowMissileContact.Handles(f)&&f.Damage.Conversion==new WeaponElementConversion(code,3)&&f.Damage.Elements.PoisonMin==153&&f.Damage.PhysicalToMagicPercent==0&&Ammo(p).Amount==quantity-1&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Elemental released conversion/poison/resource packet");return f;
    }
    private static void Cold(PlayerMobile p,BaseCreature t,WeaponFlight f,int frames)
    {
        if(f.Ability!=CombatAbility.ColdArrow||!t.Alive){return;}
        if(frames==0){Require(!TimedEffects.TryActor(t,out var empty)||empty.Cold==null,"Cold-immune/failed arrow chilled");return;}
        Require(f.Damage.Elements.ColdFrames==100&&TimedEffects.TryActor(t,out var actor)&&actor.Cold?.Source==p.Serial.Value&&actor.ChillPercent>0&&Math.Abs((actor.ChillUntil-actor.Updated).TotalMilliseconds-frames*40)<2,"Delivered cold length/owner/source frames");
    }
    private static void Scenarios(PlayerMobile p,BaseWeapon bow,Item gloves,CombatAbility ability)
    {
        using var candidate=PlayerItemProcWorld.Candidate(p);var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();int before=0;BaseCreature recipient=null;
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&v.Packet.HitFlags==32&&SkillHealth.Balance256(recipient)==before,"Elemental arrow item phase/HP");visits.Add(v);}};
            foreach(string name in ability==CombatAbility.ColdArrow?new[]{"hit","miss","block","immune","removed","lethal","resisted"}:new[]{"hit","miss","block","immune","removed","lethal"})
            {
                MagicArrowContactChecks.Ready(p);var t=Target(p,ability,name=="hit",name=="immune"?100:name=="resisted"?50:0);recipient=t;if(name=="lethal"){SkillHealth.SetBalance256(t,1);}before=SkillHealth.Balance256(t);visits.Clear();WeaponMissiles.HitRoll=()=>name=="miss"?1:0;SourceBlocking.BlockRoll=n=>name=="block"?0:n-1;NpcWorldAllocation.RequireStream();var world=NpcWorldAllocation.Capture();var profile=Own(p,bow);int mana=NecromancerEffects.ManaBalance256(p),quantity=Ammo(p).Amount,cost=SkillMechanics.Values(p,ability).Mana256;
                try
                {
                    var f=Launch(p,ability,t);Require(visits.Count==0&&SkillHealth.Balance256(t)==before&&Json(Own(p,bow))==Json(profile)&&NpcWorldAllocation.Capture()==world,"Elemental arrow launch proc/damage");if(name=="removed"){p.AddToBackpack(bow);p.AddToBackpack(gloves);ClassGear.Refresh(p);}
                    var raw=f.Damage.Roll(t,_=>0,out _);var parts=SkillCombat.MitigatePrepared(p,t,raw with{Poison=0});int direct=NamedElements.To256(parts.Total);bool hit=name is not("miss" or "block"),proc=hit&&name!="removed";
                    MagicArrowContactChecks.Drain(p);Require(visits.Count==(proc?1:0)&&visits.All(v=>v.Outcomes.Single()is{CastAttempts:1,CastResults:1}),"Elemental arrow hit/block/miss/provider proc eligibility");
                    Require(before-SkillHealth.Balance256(t)==(hit?Math.Min(before,direct+153):0)&&NativeMonsterReturn.Session(t).Packets==(hit?1:0)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&p.Backpack.FindItemByType(bow is Crossbow?typeof(Bolt):typeof(Arrow)).Amount==quantity-1,"Elemental arrow typed HP/return/resource");
                    if(name=="hit"){Require(t.Notifications==1&&t.AtNotification==before&&visits[0].Packet.Physical256>0&&(ability==CombatAbility.FireArrow?visits[0].Packet.Fire256:visits[0].Packet.Cold256)>0,"Converted element/native notification");}
                    if(t.Alive){Require(TimedEffects.Slow(t,TimedEffects.Now)==(hit?20:0)&&TimedEffects.CanRegenerateHits(t)==(name=="miss"),"Elemental arrow item slow/accuracy PMH");Cold(p,t,f,hit&&name!="immune"?(name=="resisted"?50:100):0);}
                    ulong seed=world.Seed;var actor=profile.Owners.SingleOrDefault();ulong actorSeed=actor?.Seed??0;if(proc){if(actor==null){actorSeed=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref seed)));}SourceRandom.Roll(ref actorSeed);SourceRandom.Roll(ref seed);Require(Own(p,bow).Owners.Single().Seed==actorSeed,"Elemental arrow actor proc draws");}else if(actor!=null){Require(Own(p,bow).Owners.Single().Seed==actorSeed,"Rejected elemental arrow advanced actor");}Require(NpcWorldAllocation.Capture().Seed==seed,"Elemental arrow child/world draws");
                }
                catch(Exception e){throw new InvalidOperationException(ability+" contact "+name,e);}
                finally{MagicArrowContactChecks.Clean(p,t);if(bow.Parent!=p){Require(p.EquipItem(bow),"Restore elemental bow");}if(gloves.Parent!=p){Require(p.EquipItem(gloves),"Restore elemental gloves");}ClassGear.Refresh(p);SourceBlocking.BlockRoll=n=>n-1;}
            }
        }
        finally{PlayerItemProcWorld.Observed=old;}
    }
    private static void Check(bool reload)
    {
        using var rolls=new MagicArrowContactChecks.Rolls();var prior=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):null;var saved=new List<Saved>();
        for(int i=0;i<Abilities.Length;i++)
        {
            var ability=Abilities[i];PlayerMobile p;BaseWeapon bow;Item gloves,helm;int generation=0;
            if(!reload){p=Owner(3500+i*32,ability,false,out bow,out gloves,out helm);Scenarios(p,bow,gloves,ability);}
            else
            {
                var s=prior[i];generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);bow=(BaseWeapon)World.FindItem((Serial)s.Bow);gloves=World.FindItem((Serial)s.Gloves);helm=World.FindItem((Serial)s.Helm);var target=(BaseCreature)World.FindMobile((Serial)s.Target);var ammo=World.FindItem((Serial)s.Ammo);
                Require(generation<=2&&bow.Parent==p&&gloves.Parent==p&&helm.Parent==p&&Json(new[]{ClassGear.State(bow),ClassGear.State(gloves),ClassGear.State(helm)})==s.Gear&&Json(Own(p,bow))==s.Profile&&Json(Flights(p).Single())==Json(s.Flight)&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(target)==s.TargetLife&&NecromancerEffects.ManaBalance256(p)==s.Mana&&ammo.Amount==s.Quantity,"Saved elemental original/packet/proc/wound/resource state");
                p.MoveToWorld(s.Point,Map.Trammel);target.Frozen=target.CantWalk=true;target.AIObject.AITimer.Stop();target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);foreach(var r in Enum.GetValues<ResistanceType>()){target.SetResistance(r,100);}target.SetResistance(ResistanceType.Poison,0);target.SetResistance(Element(ability),0);
                var observer=PlayerItemProcWorld.Observed;int count=0,before=SkillHealth.Balance256(target);var f=s.Flight with{NextUtc=TimedEffects.Now};int direct=NamedElements.To256(SkillCombat.MitigatePrepared(p,target,f.Damage.Roll(target,_=>0)with{Poison=0}).Total);PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&SkillHealth.Balance256(target)==before&&v.Outcomes.Single()is{CastAttempts:1,CastResults:1},"Restored elemental arrow proc phase");count++;}};
                try{using(PlayerItemProcWorld.Candidate(p)){MagicArrowContactChecks.Hold(p,f);MagicArrowContactChecks.Drain(p);}Require(count==1&&before-SkillHealth.Balance256(target)==direct+153&&NecromancerEffects.ManaBalance256(p)==s.Mana&&ammo.Amount==s.Quantity,"Restored elemental arrow damage/proc/repayment");Cold(p,target,f,100);}finally{PlayerItemProcWorld.Observed=observer;MagicArrowContactChecks.Clean(p,target);}
            }
            var other=Owner(3600+i*32,ability,true,out _,out _,out _);var victim=Target(other,ability);MagicArrowContactChecks.Ready(other);int quantity=Ammo(other).Amount;using(PlayerItemProcWorld.Candidate(other)){var arrow=Launch(other,ability,victim);int direct=NamedElements.To256(SkillCombat.MitigatePrepared(other,victim,arrow.Damage.Roll(victim,_=>0)with{Poison=0}).Total);MagicArrowContactChecks.Drain(other);Require(victim.HitsMax*256-SkillHealth.Balance256(victim)==direct+153&&Ammo(other).Amount==quantity-1,"Crossbow elemental route");Cold(other,victim,arrow,100);}MagicArrowContactChecks.Clean(other,victim);other.Delete();
            MagicArrowContactChecks.Ready(p);var t=Target(p,ability);var flight=Launch(p,ability,t);WeaponMissiles.Tick(flight.NextUtc,onlyOwner:p.Serial.Value);flight=Flights(p).Single()with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};MagicArrowContactChecks.Hold(p,flight);var ammoItem=Ammo(p);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);NecromancerEffects.SetMana256(p,17);var point=p.Location;p.Internalize();saved.Add(new(p.Serial.Value,t.Serial.Value,bow.Serial.Value,gloves.Serial.Value,helm.Serial.Value,ammoItem.Serial.Value,point,Json(new[]{ClassGear.State(bow),ClassGear.State(gloves),ClassGear.State(helm)}),Json(Own(p,bow)),flight,SkillHealth.Balance256(p),SkillHealth.Balance256(t),NecromancerEffects.ManaBalance256(p),ammoItem.Amount,generation));
        }
        File.WriteAllText(Manifest,Json(saved));
    }
    private static void Input(NetState n,string label,CombatAbility ability)
    {
        var rolls=new MagicArrowContactChecks.Rolls();var p=Owner(3400,ability,false,out _,out _,out _);var t=Target(p,ability);MagicArrowContactChecks.Ready(p);var account=FixtureNetwork.Authenticate(n,p,"elemental-arrow-");var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256,quantity=Ammo(p).Amount;long deadline=Core.TickCount+6000;bool began=false,sawFlight=false;
        void Close(){MagicArrowContactChecks.Clean(p,t);PlayerItemProcWorld.Observed=old;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try{Require(Core.TickCount<deadline,"Elemental arrow input timeout");var s=ClassCombat.For(p);if(!began&&s.PendingAbility==ability){began=true;Require(visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity,"Wire elemental start/ammo/proc phase");Log.Information("{Label}: windup",label);}if(Flights(p).Length>0){sawFlight=true;}if(!sawFlight||Flights(p).Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}Require(began&&visits.Count==1&&visits[0].Phase=="domissiledamage"&&visits[0].Packet.Poison256==153&&visits[0].Outcomes.Single()is{CastAttempts:1,CastResults:1}&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity-1,"Wire elemental impact/proc/resources");Close();Log.Information("{Label}-result: passed",label);}catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Amazon,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
