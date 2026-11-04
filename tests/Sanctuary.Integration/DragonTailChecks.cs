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
namespace Sanctuary.Integration;

public static class DragonTailChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DragonTailChecks));
    private const CombatAbility Ability=CombatAbility.DragonTail;
    private sealed record Saved(uint Owner,uint Target,uint Helm,uint Boots,Point3D Origin,int Mana,int Health,MartialCharge Charge,uint PendingOwner,uint PendingTarget,int PendingMana,SourceKnockbackState Motion);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"dragon-tail-fixtures.json");
    private sealed class Fixed:Random{public override int Next(int max)=>Math.Max(0,max-1);}
    private sealed class Control:IDisposable
    {
        private readonly Func<double> Hit=DragonTails.HitRoll,Element=DragonTails.ElementRoll;
        private readonly Func<int,int> Physical=DragonTails.PhysicalRoll,Poison=DragonTails.PoisonRoll,Block=SourceBlocking.BlockRoll,Choice=SourceBlocking.ChoiceRoll,
            Fire=FistsFireRelease.DamageRoll,Cold=MartialCold.DamageRoll,Lightning=MartialLightning.DamageRoll;
        private readonly Random Effects=DragonTails.Effects;
        internal Control(){DragonTails.HitRoll=DragonTails.ElementRoll=()=>0;DragonTails.PhysicalRoll=DragonTails.PoisonRoll=FistsFireRelease.DamageRoll=MartialCold.DamageRoll=MartialLightning.DamageRoll=_=>0;DragonTails.Effects=new Fixed();SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>99;}
        public void Dispose(){DragonTails.HitRoll=Hit;DragonTails.ElementRoll=Element;DragonTails.PhysicalRoll=Physical;DragonTails.PoisonRoll=Poison;DragonTails.Effects=Effects;SourceBlocking.ChoiceRoll=Choice;SourceBlocking.BlockRoll=Block;FistsFireRelease.DamageRoll=Fire;MartialCold.DamageRoll=Cold;MartialLightning.DamageRoll=Lightning;}
    }
    private static bool Creating;
    private static void Rates()
    {
        var mana=Mobile.ManaRegenRateHandler;var life=Mobile.HitsRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>Creating||p.Name?.StartsWith("DragonTailFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):mana?.Invoke(p)??Mobile.DefaultManaRate;
        Mobile.HitsRegenRateHandler=p=>Creating||p.Name?.StartsWith("DragonTailFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):life?.Invoke(p)??Mobile.DefaultHitsRate;
    }
    public static void Configure()
    {
        Rates();Register("dragon-tail-check",Save);Register("dragon-tail-reload-check",Reload);Register("dragon-tail-mechanics-check",Mechanics);
        foreach(string mode in new[]{"left","right","quick"}){string label="dragon-tail-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        Rates();if(!File.Exists(Manifest)){return;}var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        foreach(uint id in new[]{saved.Target,saved.PendingTarget}){if(World.FindMobile((Serial)id) is BaseCreature actor){actor.Frozen=actor.CantWalk=true;actor.AIObject?.AITimer.Stop();}}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Learned Dragon Tail original kick/progressive/fire delivery and saved inputs",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh(int column=6640,int row=3500)
    {
        Creating=true;try{var p=BladeBodyChecks.Fresh(column,30,row);p.Name="DragonTailFixture";while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}var w=new Dagger();ClassGear.Profiles.Convert(w,99,GearRarity.Normal,new Random(173));Require(p.EquipItem(w),"Actual dagger");ClassGear.Refresh(p);return p;}finally{Creating=false;}
    }
    private static BaseCreature Target(PlayerMobile p,int dx=1)
    {
        var t=new CobraPassiveTarget{Name="DragonTailFixtureTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;
        foreach(var r in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(r,0);}return t;
    }
    private static Item Named(PlayerMobile p,string source)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Fixed(),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Actual named "+source);ClassGear.Refresh(p);return item;}
    private static void Hold(PlayerMobile p,CombatAbility ability,int count)
    {
        int rank=SkillMechanics.Rank(p,ability);var v=SkillMechanics.Values(p,ability);
        var charge=new MartialCharge(p.Serial.Value,ability,count,rank,count*(int)v.Eval("aurastatcalc2"),TimedEffects.Now.Add(FixtureSpace.Retention));
        AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=p.Serial.Value||x.Ability!=ability).Append(charge).ToArray());
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static DragonTails.Prepared Begin(PlayerMobile p,BaseCreature t)
    {Ready(p);Require(ClassCombat.Perform(p,Ability,t,t.Location,Core.TickCount),"Learned Tail begin");return ClassCombat.For(p).TailAttack;}
    private static bool Finish(PlayerMobile p)=>ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"player-melee-geometry-cases.json")));int count=0;
        foreach(var row in doc.RootElement.GetProperty("range").EnumerateArray())
        {
            var v=row[0];int V(int i)=>v[i].GetInt32();var a=new MeleeGeometryUnit(new(40,40),2,0,-1,V(0),HasRoom:V(6)!=0);
            var b=new MeleeGeometryUnit(new(40+V(1),40+V(2)),V(3),1,V(4));int calls=0;
            bool answer=SourceMeleeGeometry.Within(a,b,V(5),(x,y,mask)=>
            {var expected=row[2][calls++];Require(x.X==expected[0].GetInt32()&&x.Y==expected[1].GetInt32()&&y.X==expected[2].GetInt32()&&y.Y==expected[3].GetInt32()&&mask==expected[4].GetInt32(),"Original player collision endpoints");return V(7)!=0;});
            Require(answer==(row[1].GetInt32()!=0)&&calls==row[2].GetArrayLength(),"Complete player inventory/range source differential");count++;
        }
        Require(count==68040,"Incomplete player range controls");
        var weapons=BalanceCatalog.Read<PlayerWeaponReach>("player-melee-ranges");count=0;
        foreach(var row in doc.RootElement.GetProperty("items").EnumerateArray())
        {if(row[1].GetInt32()!=0&&row[2].GetInt32()==4){Require(PlayerMeleeGeometry.ForCode(weapons[row[0].GetInt32()].Code)==row[3].GetInt32(),"Original item getter versus runtime weapon lookup");count++;}}
        Require(count==306,"Incomplete runtime weapon adders");
        Require(DragonTails.ProgressiveOrder.SequenceEqual(new[]{CombatAbility.TigerStrike,CombatAbility.CobraStrike,CombatAbility.PhoenixStrike,CombatAbility.FistsOfFire,CombatAbility.BladesOfIce,CombatAbility.ClawsOfThunder}),"Pinned ascending pgsv-state order");
    }
    private static void Knockback()
    {
        var p=Fresh(6620);var t=Target(p);try
        {
            int hp=SkillHealth.Balance256(t),owner=SkillHealth.Balance256(p);var result=DragonKickPackets.TailImpact(p,t,256,TimedEffects.Now);
            Require(hp-SkillHealth.Balance256(t)==256&&result.Packet.ResultFlags==13&&NativeMonsterReturn.Session(t).LastMode==PreparedMonsterReturn.Knockback&&CombatStuns.Remaining(t)==0,"Copied flags9/13 fire failed damage/knockback or invented Stun");
            t.SetResistance(ResistanceType.Fire,100);hp=SkillHealth.Balance256(t);PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,1000,TimedEffects.Now.AddDays(1)));
            result=DragonKickPackets.TailImpact(p,t,256,TimedEffects.Now);Require(SkillHealth.Balance256(t)==hp&&SkillHealth.Balance256(p)==owner&&NativeMonsterReturn.Session(t).LastMode==PreparedMonsterReturn.Knockback,"Immune burst lost knockback or reflected melee Thorns");
            var motion=SourceKnockbacks.Snapshot().Single(x=>x.Target==t.Serial.Value);var origin=t.Location;SourceKnockbacks.Tick(motion.NextUtc);
            Require(t.Location==origin&&SourceKnockbacks.Snapshot().Single(x=>x.Target==t.Serial.Value).Travel16==motion.Travel16,"Frozen source trajectory advanced");
            t.Frozen=t.CantWalk=false;
            for(int n=0;n<20;n++){var state=SourceKnockbacks.Snapshot().SingleOrDefault(x=>x.Target==t.Serial.Value);if(state==null||state.Recovering){break;}SourceKnockbacks.Tick(state.NextUtc);}
            Require(t.Location==motion.End&&t.Location!=origin&&NativeMonsterReturn.Session(t).LastMode==PreparedMonsterReturn.GetHit,"Live source knockback endpoint/post-recovery failed");
            SourceKnockbacks.Clear(t);t.MoveToWorld(p.Location,p.Map);Require(!DragonKickPackets.TailRecipient(p,t,out _),"Unknown zero-vector knockback accepted");
        }
        finally{SourceKnockbacks.Clear(t);t.Delete();p.Delete();}
    }
    private static void Mechanics()
    {
        Differential();using var control=new Control();Knockback();var p=Fresh();var t=Target(p);var b=Target(p,3);
        try
        {
            Require(AssassinCombat.Implemented(Ability)&&ClassCombat.Available(p,Ability)&&SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Learned availability/bindings");
            NecromancerEffects.SetMana256(p,100*256+17);var pending=Begin(p,t);int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t),near=SkillHealth.Balance256(b);
            Require(pending!=null&&pending.Kick!=null&&ClassCombat.For(p).Timing==DragonTailPreparation.Timing(p)&&NecromancerEffects.ManaBalance256(p)==mana,"Start allocation/deferred mana/KK");
            int physical=NamedElements.To256(pending.Kick.Impact.Parts.Physical),fire=DragonKickPackets.TailFire256(physical,SkillMechanics.Values(p,Ability),0);
            Require(!ClassCombat.ResolveAction(p,pending.EffectAt-1)&&Finish(p)&&!Finish(p),"Single action event");
            Require(NecromancerEffects.ManaBalance256(p)==mana-2560&&hp-SkillHealth.Balance256(t)==physical+fire&&near-SkillHealth.Balance256(b)==fire,"Kick and independently mitigated physical-seeded burst");
            DragonTails.HitRoll=()=>1;mana=NecromancerEffects.ManaBalance256(p);hp=SkillHealth.Balance256(t);Begin(p,t);Require(Finish(p)&&NecromancerEffects.ManaBalance256(p)==mana-2560&&SkillHealth.Balance256(t)==hp,"Released miss did not cost10mana or damaged");
            Hold(p,CombatAbility.TigerStrike,3);var original=AssassinCharges.State(p);pending=Begin(p,t);Require((pending.Flags&1)!=0,"Charged accuracy did not bypass failed roll");Finish(p);Require(AssassinCharges.State(p) is{Count:2}&&AssassinCharges.State(p).UntilUtc==original.UntilUtc,"Successful one-charge consumption/deadline");
            var shield=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};shield.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);shield.AIObject?.AITimer.Stop();shield.SetHits(50000);shield.Hits=shield.HitsMax;
            try{SourceBlocking.BlockRoll=_=>0;original=AssassinCharges.State(p);mana=NecromancerEffects.ManaBalance256(p);hp=SkillHealth.Balance256(shield);pending=Begin(p,shield);Require((pending.Flags&16)!=0,"Actual charged shield block");Require(Finish(p)&&AssassinCharges.State(p)==original&&SkillHealth.Balance256(shield)==hp&&NecromancerEffects.ManaBalance256(p)==mana-2560,"Blocked release consumed/damaged or failed payment");}finally{shield.Delete();SourceBlocking.BlockRoll=_=>99;}
            AssassinCharges.Clear(p);DragonTails.HitRoll=()=>0;t.SetResistance(ResistanceType.Physical,100);near=SkillHealth.Balance256(b);Begin(p,t);Require(Finish(p)&&SkillHealth.Balance256(b)==near,"Physical immunity produced Tail fire seed");t.SetResistance(ResistanceType.Physical,0);
            Hold(p,CombatAbility.TigerStrike,3);original=AssassinCharges.State(p);pending=Begin(p,t);mana=NecromancerEffects.ManaBalance256(p);ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,pending.EffectAt)&&AssassinCharges.State(p)==original&&NecromancerEffects.ManaBalance256(p)==mana,"Cancelled windup paid or consumed");
            var origin=p.Location;pending=Begin(p,t);p.Direction=Direction.West;Require(p.Move(Direction.West)&&ClassCombat.For(p).TailAttack==null&&!ClassCombat.ResolveAction(p,pending.EffectAt)&&AssassinCharges.State(p)==original&&NecromancerEffects.ManaBalance256(p)==mana,"Accepted movement kept/paid pending Tail");p.MoveToWorld(origin,p.Map);
            pending=Begin(p,t);var weapon=CombatTiming.Equipped(p);p.AddToBackpack(weapon);ClassGear.Refresh(p);Require(ClassCombat.For(p).TailAttack==null&&!ClassCombat.ResolveAction(p,pending.EffectAt)&&AssassinCharges.State(p)==original,"Equipment change retained queued Tail");Require(p.EquipItem(weapon),"Re-equip original dagger");ClassGear.Refresh(p);
            pending=Begin(p,t);Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount)&&ClassCombat.For(p).TailAttack==null&&!ClassCombat.ResolveAction(p,pending.EffectAt)&&AssassinCharges.State(p)==original,"Hit recovery retained queued Tail");
            pending=Begin(p,t);NecromancerEffects.SetMana256(p,2559);Require(!Finish(p)&&AssassinCharges.State(p)==original&&NecromancerEffects.ManaBalance256(p)==2559,"Insufficient release mana paid or consumed");
            NecromancerEffects.SetMana256(p,2559);Ready(p);Require(ClassCombat.Perform(p,Ability,t,t.Location,Core.TickCount)&&ClassCombat.For(p).PendingAbility==CombatAbility.BasicAttack&&ClassCombat.For(p).TailAttack==null,"Low-mana Basic Attack fallback");ClassCombat.Stop(p);
            AssassinCharges.Clear(p);NecromancerEffects.SetMana256(p,100*256+17);pending=Begin(p,t);var point=t.Location;t.MoveToWorld(new(t.X+10,t.Y,t.Z),t.Map);hp=SkillHealth.Balance256(t);Require(Finish(p)&&hp-SkillHealth.Balance256(t)==DragonKickPackets.TailFire256(NamedElements.To256(pending.Kick.Impact.Parts.Physical),SkillMechanics.Values(p,Ability),0),"Older do50 retained-target burst after lost melee contact");t.MoveToWorld(point,t.Map);
            Hold(p,CombatAbility.BladesOfIce,3);Named(p,"Raven Frost");pending=Begin(p,t);Require(pending.Kick.FreezeFrames==pending.Kick.ColdFrames&&pending.Contact.FreezeFrames>0,"Item-plus-Ice freeze length");Require(Finish(p)&&FreezeStates.Shattered(t),"Inline Ice freeze did not reach prepared contact");AssassinCharges.Clear(p);TimedEffects.ClearGear(t);
            var dagger=CombatTiming.Equipped(p);p.AddToBackpack(dagger);var bow=new Bow();ClassGear.Profiles.Convert(bow,99,GearRarity.Normal,new Random(173));Require(p.EquipItem(bow),"Actual bow");ClassGear.Refresh(p);
            Require(PlayerMeleeGeometry.Reach(p)==0&&CombatTiming.Range(p,Ability)<bow.MaxRange,"Kick inherited bow missile reach");Begin(p,t);Require(Finish(p),"Bow kick required arrows or failed melee allocation");p.AddToBackpack(bow);Require(p.EquipItem(dagger),"Restore dagger");ClassGear.Refresh(p);
            // Original release ordering: progressive fire can kill before the
            // weapon packet is delivered; the copied physical seed survives.
            Hold(p,CombatAbility.FistsOfFire,2);SkillHealth.SetBalance256(t,256);int packets=NativeMonsterReturn.Session(t).Packets;pending=Begin(p,t);
            Require(Finish(p)&&!t.Alive&&NativeMonsterReturn.Session(t).Packets==packets&&AssassinCharges.State(p,CombatAbility.FistsOfFire) is{Count:1},"Progressive area was delivered after the parent kick or lost consumption");
        }
        finally{t.Corpse?.Delete();t.Delete();b.Delete();p.Corpse?.Delete();p.Delete();}
        p=Fresh(6640,3600);t=Target(p);b=Target(p,3);
        try
        {
            // Full copied physical, not clipped target HP, seeds lethal AoE.
            var fragile=Target(p,2);SkillHealth.SetBalance256(fragile,256);
            SkillHealth.SetBalance256(t,256);int near=SkillHealth.Balance256(b);var pending=Begin(p,t);int copied=NamedElements.To256(pending.Kick.Impact.Parts.Physical);
            Require(Finish(p)&&!t.Alive&&near-SkillHealth.Balance256(b)==DragonKickPackets.TailFire256(copied,SkillMechanics.Values(p,Ability),0)&&copied>256&&!fragile.Alive,"Lethal parent/neighbor lost full physical seed,center or stable enumeration");fragile.Corpse?.Delete();fragile.Delete();
            t.Delete();t=Target(p);Begin(p,t);SkillHealth.SetBalance256(p,256);PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,1000,TimedEffects.Now.AddDays(1)));
            near=SkillHealth.Balance256(b);Require(Finish(p)&&!p.Alive&&SkillHealth.Balance256(b)==near,"Owner killed by contact retaliation still emitted Tail fire");
        }
        finally{t.Corpse?.Delete();t.Delete();b.Delete();p.Corpse?.Delete();p.Delete();}
    }
    private static void Save()
    {
        using var control=new Control();var p=Fresh(6660);ClassSkillChecks.Train(p,"assassin.dragontail",20);var helm=Named(p,"Tarnhelm");var boots=Named(p,"Gorerider");var t=Target(p);
        Require(SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Saved binding assignment");
        NecromancerEffects.SetMana256(p,100*256+17);Hold(p,CombatAbility.TigerStrike,3);Begin(p,t);Require(Finish(p)&&AssassinCharges.State(p) is{Count:2}&&SkillMechanics.Rank(p,Ability)==21,"Actual effective21 paid kick");
        var pending=Fresh(6660,3700);var other=Target(pending);var queued=Begin(pending,other);var s=ClassCombat.For(pending);s.EffectAt=Core.TickCount+FixtureSpace.RetentionMilliseconds;s.TailAttack=queued with{EffectAt=s.EffectAt};
        var motion=SourceKnockbacks.Snapshot().Single(x=>x.Target==t.Serial.Value) with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};
        SourceKnockbacks.Restore(SourceKnockbacks.Snapshot().Where(x=>x.Target!=t.Serial.Value).Append(motion).ToArray());
        var origin=p.Location;p.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,helm.Serial.Value,boots.Serial.Value,origin,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(t),AssassinCharges.State(p),pending.Serial.Value,other.Serial.Value,NecromancerEffects.ManaBalance256(pending),motion)));
    }
    private static void Reload()
    {
        using var control=new Control();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var pending=(PlayerMobile)World.FindMobile((Serial)s.PendingOwner);var other=World.FindMobile((Serial)s.PendingTarget);
        Require(World.FindItem((Serial)s.Helm)?.Parent==p&&World.FindItem((Serial)s.Boots)?.Parent==p&&SkillMechanics.Rank(p,Ability)==21&&AssassinCharges.State(p)==s.Charge,"Original gear/rank/charge changed on reload");
        Require(NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(t)==s.Health&&ClassCombat.For(pending).TailAttack==null&&ClassCombat.For(pending).PendingAbility==CombatAbility.None&&NecromancerEffects.ManaBalance256(pending)==s.PendingMana&&other.Hits==other.HitsMax,"Original wounds/mana changed or queued windup replayed");
        var motion=SourceKnockbacks.Snapshot().Single(x=>x.Target==t.Serial.Value);
        Require(motion.Target==s.Motion.Target&&motion.Position==s.Motion.Position&&motion.End==s.Motion.End&&motion.Travel16==s.Motion.Travel16&&motion.Budget16==s.Motion.Budget16&&motion.PauseFrozen,"Original burst knockback lost its trajectory on reload");
        Require(SkillBindings.Instance.Get(p)==(Ability,Ability)&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Assassin,7)==Ability,"Original bindings changed");
        p.MoveToWorld(s.Origin,Map.Trammel);Begin(p,t);Require(Finish(p)&&AssassinCharges.State(p) is{Count:1},"Restored charge failed learned finisher");
        AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=p.Serial.Value).Append(s.Charge).ToArray());NecromancerEffects.SetMana256(p,s.Mana);SkillHealth.SetBalance256(t,s.Health);SourceKnockbacks.Restore(SourceKnockbacks.Snapshot().Where(x=>x.Target!=t.Serial.Value).Append(s.Motion with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}).ToArray());p.Internalize();
    }
    private static void Input(NetState network,string label)
    {
        var control=new Control();var p=Fresh(6680);var t=Target(p);Hold(p,CombatAbility.TigerStrike,2);
        Require(SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Natural input bindings");FixtureNetwork.Authenticate(network,p,"dragon-tail-input-");Ready(p);
        int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t);bool windup=false;long end=Core.TickCount+5000;
        void Observe()
        {
            try
            {
                var session=ClassCombat.For(p);
                if(!windup&&session.PendingAbility==Ability){Require(session.TailAttack!=null&&NecromancerEffects.ManaBalance256(p)==mana&&session.Timing==DragonTailPreparation.Timing(p),"Natural input paid before action or wrong clock");windup=true;Log.Information("{Label}: windup",label);}
                if(SkillHealth.Balance256(t)==hp){Require(Core.TickCount<end,"Natural Tail input timeout");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(windup&&NecromancerEffects.ManaBalance256(p)==mana-2560&&AssassinCharges.State(p) is{Count:1},"Natural input repeated payment or consumption");
                control.Dispose();t.Delete();p.Delete();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){control.Dispose();t.Delete();p.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Assassin,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
