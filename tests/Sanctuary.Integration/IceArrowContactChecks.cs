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

public static class IceArrowContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(IceArrowContactChecks));
    private sealed record Saved(uint Owner,uint Target,uint Bow,uint Gloves,uint Helm,uint Ammo,Point3D Point,string Gear,string Profile,WeaponFlight Flight,int Life,int TargetLife,int Mana,int Quantity,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"ice-arrow-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static WeaponFlight[] Flights(PlayerMobile p)=>MagicArrowContactChecks.Flights(p);
    private static PlayerProcSave Own(PlayerMobile p,Item bow)=>MagicArrowContactChecks.Own(p,bow);
    private static Item Ammo(PlayerMobile p)=>BowAmmunition.Stack(p,CombatTiming.Equipped(p));
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"ice-arrow-contact-reload-check":"ice-arrow-contact-check";ConsoleInputHandler.RegisterCommand(name,"Ice Arrow ordered packet freeze,item procs,cold fallback and saved original",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"right","quick"})
        {string label="ice-arrow-contact-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated Ice Arrow item hit proc and freeze packet",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(int row,bool crossbow,out BaseWeapon bow,out Item gloves,out Item helm)
    {var p=MagicArrowContactChecks.Owner(row,crossbow,out bow,out gloves,out helm);ClassSkillChecks.Train(p,"amazon.icearrow");p.Backpack.FindItemByType<Arrow>().Amount=100;p.Backpack.FindItemByType<Bolt>().Amount=100;Require(SkillBindings.Instance.Assign(p,false,CombatAbility.IceArrow)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.IceArrow),"Ice Arrow contact bindings");return p;}
    private static BaseCreature Target(PlayerMobile p,string scenario="normal")
    {
        BaseCreature t;
        if(scenario=="elite")
        {var elite=new EncounterCreature{NoKillAwards=true,Frozen=true,CantWalk=true};var profile=EncounterProfiles.Profile("crypt-sentinel-90");elite.Assign(profile,profile.MinimumLevel,MonsterDifficulty.Normal,EncounterGrade.Champion,ChampionKind.Champion,[],EliteAura.None,null,new Random(173));elite.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);t=elite;}
        else if(scenario is "nightmare" or "hell"){t=new GraveyardSkeleton{NoKillAwards=true,Frozen=true,CantWalk=true};MonsterVariants.Assign(t,scenario=="nightmare"?MonsterDifficulty.Nightmare:MonsterDifficulty.Hell);t.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);}
        else{t=MagicArrowContactChecks.Target(p);}
        t.SetHits(60000);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Poison,0);t.SetResistance(ResistanceType.Cold,scenario=="immune"?100:scenario=="resisted"?50:0);if(scenario=="normal"){t.SetResistance(ResistanceType.Physical,0);}return t;
    }
    private static WeaponFlight Launch(PlayerMobile p,BaseCreature t)
    {
        int mana=NecromancerEffects.ManaBalance256(p),quantity=Ammo(p).Amount,cost=SkillMechanics.Values(p,CombatAbility.IceArrow).Mana256;
        Require(ClassCombat.Perform(p,CombatAbility.IceArrow,t,t.Location,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity,"Ice Arrow start phase");ClassCombat.ReleaseCast(p);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Ice Arrow release");var f=Flights(p).Single();
        Require(BowMissileContact.Handles(f)&&f.Damage.FreezeColdLength&&f.Damage.Conversion==null&&f.Damage.PhysicalToMagicPercent==0&&f.Damage.Elements.ColdFrames==50&&f.Damage.Elements.PoisonMin==153&&Ammo(p).Amount==quantity-1&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Ice Arrow released freeze/physical/poison/resources");return f;
    }
    private static void State(PlayerMobile p,BaseCreature t,string name)
    {
        if(!t.Alive){return;}bool freeze=name is not("miss" or "block" or "immune" or "elite");TimedEffects.TryActor(t,out var actor);
        if(!freeze){Require(actor?.Control?.Freeze==null&&(name=="elite"?actor?.Cold?.Source==p.Serial.Value&&actor.ChillPercent>0:actor?.Cold==null),"Ice Arrow freeze refusal or elite chill fallback");return;}
        int frames=name=="hell"?12:name is "resisted" or "nightmare"?25:50;
        Require(actor?.Control?.Freeze?.Source==p.Serial.Value&&actor.Cold==null&&actor.ChillPercent==0&&Math.Abs((actor.Control.FreezeUntil-actor.Updated).TotalMilliseconds-frames*40)<2,"Ice Arrow freeze length,owner,difficulty or duplicate chill");
    }
    private static void Scenarios(PlayerMobile p,BaseWeapon bow,Item gloves)
    {
        using var candidate=PlayerItemProcWorld.Candidate(p);var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();BaseCreature recipient=null;int before=0,freeze=0,cold=0;
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&v.Packet.HitFlags==32&&v.Packet.FreezeFrames==freeze&&v.Packet.ColdFrames==cold&&SkillHealth.Balance256(recipient)==before,"Ice Arrow actual pre-HP freeze packet/event");visits.Add(v);}};
            foreach(string name in new[]{"normal","resisted","immune","miss","block","removed","lethal","elite","nightmare","hell"})
            {
                MagicArrowContactChecks.Ready(p);var t=Target(p,name);recipient=t;if(name=="lethal"){SkillHealth.SetBalance256(t,1);}before=SkillHealth.Balance256(t);freeze=name=="immune"||name=="elite"?0:name=="resisted"?25:50;cold=name=="elite"?50:0;visits.Clear();WeaponMissiles.HitRoll=()=>name=="miss"?1:0;SourceBlocking.BlockRoll=n=>name=="block"?0:n-1;NpcWorldAllocation.RequireStream();var world=NpcWorldAllocation.Capture();var profile=Own(p,bow);int mana=NecromancerEffects.ManaBalance256(p),quantity=Ammo(p).Amount,cost=SkillMechanics.Values(p,CombatAbility.IceArrow).Mana256;
                try
                {
                    var f=Launch(p,t);Require(visits.Count==0&&SkillHealth.Balance256(t)==before&&Json(Own(p,bow))==Json(profile)&&NpcWorldAllocation.Capture()==world,"Ice Arrow early proc/damage/RNG");if(name=="removed"){p.AddToBackpack(bow);p.AddToBackpack(gloves);ClassGear.Refresh(p);}var raw=f.Damage.Roll(t,_=>0);int direct=NamedElements.To256(SkillCombat.MitigatePrepared(p,t,raw with{Poison=0}).Total);bool hit=name is not("miss" or "block"),proc=hit&&name!="removed";
                    MagicArrowContactChecks.Drain(p);Require(visits.Count==(proc?1:0)&&visits.All(v=>v.Outcomes.Single()is{CastAttempts:1,CastResults:1}),"Ice Arrow hit/block/miss/provider proc gate");
                    Require(before-SkillHealth.Balance256(t)==(hit?Math.Min(before,direct+153):0)&&NativeMonsterReturn.Session(t).Packets==(hit?1:0)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&p.Backpack.FindItemByType(bow is Crossbow?typeof(Bolt):typeof(Arrow)).Amount==quantity-1,"Ice Arrow typed HP/return/resources");State(p,t,name);
                    if(name=="normal"){Require(visits[0].Packet.Physical256>0&&visits[0].Packet.Cold256>0,"Ice Arrow lost physical/cold split");}
                    if(name=="lethal"){Require(t.Corpse is Corpse corpse&&CorpseSkillRules.Shattered(corpse)&&!CorpseSkillRules.Usable(p,corpse,20),"Lethal Ice Arrow did not mark frozen corpse before death");}
                    if(t.Alive){Require(TimedEffects.CanRegenerateHits(t)==(name=="miss"),"Ice Arrow accuracy PMH");}
                    ulong seed=world.Seed;var actor=profile.Owners.SingleOrDefault();ulong actorSeed=actor?.Seed??0;if(proc){if(actor==null){actorSeed=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref seed)));}SourceRandom.Roll(ref actorSeed);SourceRandom.Roll(ref seed);Require(Own(p,bow).Owners.Single().Seed==actorSeed,"Ice Arrow actor draws");}else if(actor!=null){Require(Own(p,bow).Owners.Single().Seed==actorSeed,"Rejected Ice Arrow advanced actor");}Require(NpcWorldAllocation.Capture().Seed==seed,"Ice Arrow child/world draws");
                }
                catch(Exception e){throw new InvalidOperationException("Ice Arrow contact "+name,e);}
                finally{MagicArrowContactChecks.Clean(p,t);if(bow.Parent!=p){Require(p.EquipItem(bow),"Restore Ice bow");}if(gloves.Parent!=p){Require(p.EquipItem(gloves),"Restore Ice gloves");}ClassGear.Refresh(p);SourceBlocking.BlockRoll=n=>n-1;}
            }
        }
        finally{PlayerItemProcWorld.Observed=old;}
    }
    private static void Check(bool reload)
    {
        using var rolls=new MagicArrowContactChecks.Rolls();PlayerMobile p;BaseWeapon bow;Item gloves,helm;int generation=0;
        if(!reload){p=Owner(3000,false,out bow,out gloves,out helm);Scenarios(p,bow,gloves);}
        else
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);bow=(BaseWeapon)World.FindItem((Serial)s.Bow);gloves=World.FindItem((Serial)s.Gloves);helm=World.FindItem((Serial)s.Helm);var target=(BaseCreature)World.FindMobile((Serial)s.Target);var ammo=World.FindItem((Serial)s.Ammo);
            Require(generation<=2&&bow.Parent==p&&gloves.Parent==p&&helm.Parent==p&&Json(new[]{ClassGear.State(bow),ClassGear.State(gloves),ClassGear.State(helm)})==s.Gear&&Json(Own(p,bow))==s.Profile&&Json(Flights(p).Single())==Json(s.Flight)&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(target)==s.TargetLife&&NecromancerEffects.ManaBalance256(p)==s.Mana&&ammo.Amount==s.Quantity,"Saved Ice original/packet/proc/wound/resources");
            p.MoveToWorld(s.Point,Map.Trammel);target.Frozen=target.CantWalk=true;target.AIObject.AITimer.Stop();target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);foreach(var r in Enum.GetValues<ResistanceType>()){target.SetResistance(r,100);}target.SetResistance(ResistanceType.Poison,0);target.SetResistance(ResistanceType.Cold,0);
            var observer=PlayerItemProcWorld.Observed;int count=0,before=SkillHealth.Balance256(target);var f=s.Flight with{NextUtc=TimedEffects.Now};int direct=NamedElements.To256(SkillCombat.MitigatePrepared(p,target,f.Damage.Roll(target,_=>0)with{Poison=0}).Total);PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Packet.FreezeFrames==50&&v.Packet.ColdFrames==0&&v.Phase=="domissiledamage"&&SkillHealth.Balance256(target)==before&&v.Outcomes.Single()is{CastAttempts:1,CastResults:1},"Restored Ice packet/proc phase");count++;}};
            try{using(PlayerItemProcWorld.Candidate(p)){MagicArrowContactChecks.Hold(p,f);MagicArrowContactChecks.Drain(p);}Require(count==1&&before-SkillHealth.Balance256(target)==direct+153&&NecromancerEffects.ManaBalance256(p)==s.Mana&&ammo.Amount==s.Quantity,"Restored Ice damage/proc/repayment");State(p,target,"normal");}finally{PlayerItemProcWorld.Observed=observer;MagicArrowContactChecks.Clean(p,target);}
        }
        var other=Owner(3200,true,out _,out _,out _);var victim=Target(other);MagicArrowContactChecks.Ready(other);using(PlayerItemProcWorld.Candidate(other)){Launch(other,victim);MagicArrowContactChecks.Drain(other);}State(other,victim,"normal");MagicArrowContactChecks.Clean(other,victim);other.Delete();
        MagicArrowContactChecks.Ready(p);var t=Target(p,"removed");var flight=Launch(p,t);WeaponMissiles.Tick(flight.NextUtc,onlyOwner:p.Serial.Value);flight=Flights(p).Single()with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};MagicArrowContactChecks.Hold(p,flight);var ammoItem=Ammo(p);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);NecromancerEffects.SetMana256(p,17);var point=p.Location;p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,t.Serial.Value,bow.Serial.Value,gloves.Serial.Value,helm.Serial.Value,ammoItem.Serial.Value,point,Json(new[]{ClassGear.State(bow),ClassGear.State(gloves),ClassGear.State(helm)}),Json(Own(p,bow)),flight,SkillHealth.Balance256(p),SkillHealth.Balance256(t),NecromancerEffects.ManaBalance256(p),ammoItem.Amount,generation)));
    }
    private static void Input(NetState n,string label)
    {
        var rolls=new MagicArrowContactChecks.Rolls();var p=Owner(3400,false,out _,out _,out _);var t=Target(p);MagicArrowContactChecks.Ready(p);var account=FixtureNetwork.Authenticate(n,p,"ice-arrow-contact-");var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.IceArrow).Mana256,quantity=Ammo(p).Amount;long deadline=Core.TickCount+6000;bool began=false,sawFlight=false;
        void Close(){MagicArrowContactChecks.Clean(p,t);PlayerItemProcWorld.Observed=old;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try{Require(Core.TickCount<deadline,"Ice Arrow input timeout");var session=ClassCombat.For(p);if(!began&&session.PendingAbility==CombatAbility.IceArrow){began=true;Require(visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity,"Wire Ice start/ammo/proc phase");Log.Information("{Label}: windup",label);}if(Flights(p).Length>0){sawFlight=true;}if(!sawFlight||Flights(p).Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}Require(began&&visits.Count==1&&visits[0].Phase=="domissiledamage"&&visits[0].Packet.FreezeFrames==50&&visits[0].Packet.ColdFrames==0&&visits[0].Outcomes.Single()is{CastAttempts:1,CastResults:1}&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity-1&&NamedControl.FrozenRemaining(t)>0&&TimedEffects.Chill(t,TimedEffects.Now)==0,"Wire Ice packet/freeze/proc/resources");Close();Log.Information("{Label}-result: passed",label);}catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.IceArrow,(int)DiabloClass.Amazon,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
