// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class BarbarianAttackChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BarbarianAttackChecks));
    private sealed record Saved(uint Owner,uint Enemy,uint Weapon,uint Helm,uint Control,Point3D Origin,int Map,int Mana,int Life,CombatStunState State);
    private sealed class Rolls(bool critical=false):Random
    {public int Calls;public override int Next(int maximum){Calls++;return critical && Calls==1?0:maximum-1;}}
    private static string Manifest=>Path.Join(Core.BaseDirectory,"barbarian-attacks.json");
    public static void Configure()
    {
        var mana=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=actor=>actor.Name=="BarbarianAttackFixture"?TimeSpan.FromHours(1):mana?.Invoke(actor)??Mobile.DefaultManaRate;

        Register("barbarian-attacks-check",()=>Controlled(Check));Register("barbarian-attacks-reload-check",()=>Controlled(Reload));
        EventSink.WorldLoad+=()=>
        {
            if(!File.Exists(Manifest)){return;}var state=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
            foreach(uint id in new[]{state.Enemy,state.Control}){if(World.FindMobile((Serial)id) is BaseCreature target){Stop(target);}}
        };
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Bash/Stun prepared attacks, resources, control and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static int Life(Mobile actor)=>actor.Hits*256-SkillHealth.Remainder256(actor);
    private static void Controlled(Action action)
    {
        var hit=BarbarianMelee.HitRoll;var element=BarbarianMelee.ElementRoll;var physical=BarbarianMelee.PhysicalRoll;var effects=BarbarianMelee.Effects;
        try{BarbarianMelee.HitRoll=()=>0;BarbarianMelee.ElementRoll=()=>0;BarbarianMelee.PhysicalRoll=_=>0;BarbarianMelee.Effects=new Rolls();action();}
        finally{BarbarianMelee.HitRoll=hit;BarbarianMelee.ElementRoll=element;BarbarianMelee.PhysicalRoll=physical;BarbarianMelee.Effects=effects;}
    }
    private static void Ready(PlayerMobile owner){ClassCombat.Stop(owner);ClassCombat.For(owner).NextAction=ClassCombat.For(owner).RecoveryUntil=Core.TickCount;}
    private static void Mana(PlayerMobile owner){owner.Mana=owner.ManaMax;NecromancerEffects.Profiles.Store(owner,new());}
    private static void Stop(BaseCreature target){target.Frozen=target.CantWalk=true;target.Combatant=null;target.AIObject?.AITimer.Stop();}
    private static void Freeze(Mobile actor)
    {
        TimedEffects.Poison(actor,actor,.000001,60000,TimedEffects.Now);
        TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==actor.Serial.Value?x with{UpdatedUtc=TimedEffects.Now.AddMinutes(10)}:x).ToArray());
    }
    private static void Resist(BaseCreature target,int physical)
    {
        target.SetResistance(ResistanceType.Physical,physical);target.SetResistance(ResistanceType.Fire,0);target.SetResistance(ResistanceType.Cold,0);target.SetResistance(ResistanceType.Energy,0);
    }
    private static Item Equip(PlayerMobile owner,Item item)
    {
        if(item is BaseWeapon && CombatTiming.Equipped(owner) is{} old){owner.AddToBackpack(old);}
        ClassGear.Profiles.Convert(item,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(item),"Attack fixture equipment "+item.GetType().Name);ClassGear.Refresh(owner);return item;
    }
    private static Item Named(PlayerMobile owner,string key)
    {
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(173),true);
        if(item is BaseWeapon && CombatTiming.Equipped(owner) is{} old){owner.AddToBackpack(old);}
        Require(owner.EquipItem(item),"Attack fixture named item "+key);ClassGear.Refresh(owner);return item;
    }
    private static void Train(PlayerMobile owner,CombatAbility ability,int rank)
    {var id=SkillMechanics.For(ability).TalentId;while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Cannot train "+id);}}
    private static void Goldens()
    {
        foreach(var ability in new[]{CombatAbility.Bash,CombatAbility.Stun})
        {
            var row=SkillMechanics.For(ability);var one=new SkillValues(row,key=>key==row.SourceKey?1:0,_=>0,99);
            var twenty=new SkillValues(row,key=>key==row.SourceKey?20:0,_=>20,99);
            Require(one.Mana256==512 && twenty.Mana256==512,"Pinned attack mana golden");
            if(ability==CombatAbility.Bash)
            {Require(one.Eval("calc1")==50 && one.Eval("calc2")==1 && one.Eval("ToHitCalc")==20 && twenty.Eval("calc1")==245 && twenty.Eval("calc2")==20 && twenty.Eval("ToHitCalc")==215,"Pinned Bash goldens");}
            else{Require(one.Eval("calc1")==0 && one.DurationFrames==30 && one.Eval("ToHitCalc")==15 && twenty.Eval("calc1")==160 && twenty.DurationFrames==226 && twenty.Eval("ToHitCalc")==210,"Pinned Stun goldens");}
        }
    }
    private static int Cast(PlayerMobile owner,BaseCreature enemy,CombatAbility ability,int input=0)
    {
        Ready(owner);int hp=Life(enemy),mana=NecromancerEffects.ManaBalance256(owner);long now=Core.TickCount;
        Require(input==0?ClassCombat.ContinueAttack(owner,ability,enemy,enemy.Location,now):input==1?ClassCombat.ContinueCast(owner,ability,enemy,enemy.Location,now):ClassCombat.Perform(owner,ability,enemy,enemy.Location,now),"Attack input begin "+ability);
        ClassCombat.ReleaseCast(owner);var session=ClassCombat.For(owner);
        Require(session.PendingAbility==ability && session.BarbarianAttack!=null && NecromancerEffects.ManaBalance256(owner)==mana-512 && Life(enemy)==hp,"Attack early damage or incorrect start payment");
        Require(!ClassCombat.ResolveAction(owner,session.EffectAt-1) && session.BarbarianAttack!=null,"Early attack event");
        Require(ClassCombat.ResolveAction(owner,session.EffectAt) && session.BarbarianAttack==null && NecromancerEffects.ManaBalance256(owner)==mana-512,"Attack event/one payment");
        Require(!ClassCombat.ResolveAction(owner,session.EffectAt),"Attack event replayed");return hp-Life(enemy);
    }
    private static void Gates(PlayerMobile owner,Skeleton enemy,Item weapon)
    {
        foreach(var ability in new[]{CombatAbility.Bash,CombatAbility.Stun})
        {
            Require(SkillBindings.Instance.Assign(owner,true,ability) && SkillBindings.Instance.Assign(owner,false,ability) && SkillBindings.Instance.AssignQuick(owner,0,ability),"Attack L/R/quick binding");
            var timing=CombatTiming.Weapon(CombatTiming.Equipped(owner));var plain=CombatTiming.Calculate(DiabloClass.Barbarian,ability,timing);
            Require(plain==CombatTiming.Calculate(DiabloClass.Barbarian,CombatAbility.BasicAttack,timing) && plain==CombatTiming.Calculate(DiabloClass.Barbarian,ability,timing,fcr:100)
                && CombatTiming.Calculate(DiabloClass.Barbarian,ability,timing,ias:100).Frames<plain.Frames,"Attack used cast timing/FCR or ignored IAS");
            Ready(owner);owner.Mana=1;NecromancerEffects.Profiles.Store(owner,new());
            Require(!ClassCombat.Perform(owner,ability,enemy,enemy.Location,Core.TickCount) && ClassCombat.For(owner).BarbarianAttack==null,"Unpaid attack started");Mana(owner);
            var point=enemy.Location;enemy.MoveToWorld(new(owner.X+6,owner.Y,owner.Z),owner.Map);Ready(owner);
            Require(!ClassCombat.Perform(owner,ability,enemy,enemy.Location,Core.TickCount),"Attack used spell reach");enemy.MoveToWorld(point,owner.Map);
            foreach(int cancel in new[]{0,1,2,3,4})
            {
                Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner),hp=Life(enemy);var origin=owner.Location;
                Require(ClassCombat.Perform(owner,ability,enemy,enemy.Location,Core.TickCount),"Cancellation fixture start");var state=ClassCombat.For(owner);long end=state.NextAction;
                if(cancel==0){owner.Frozen=true;}else if(cancel==1){owner.Paralyzed=true;}else if(cancel==2){owner.MoveToWorld(new(origin.X,origin.Y+1,origin.Z),owner.Map);}else if(cancel==3){owner.AddToBackpack(weapon);}else{Require(HitRecovery.Apply(owner,owner.HitsMax,Core.TickCount),"Native attack hit-recovery interrupt");}
                Require(!ClassCombat.ResolveAction(owner,state.EffectAt) && state.BarbarianAttack==null && state.NextAction==end && Life(enemy)==hp && NecromancerEffects.ManaBalance256(owner)==mana-512,"Cancelled attack damaged/refunded/replayed");
                owner.Frozen=owner.Paralyzed=false;owner.MoveToWorld(origin,owner.Map);if(weapon.Parent!=owner){Require(owner.EquipItem(weapon),"Cancelled weapon restore");}Mana(owner);
            }
            Ready(owner);Mana(owner);var stepOrigin=owner.Location;Require(ClassCombat.Perform(owner,ability,enemy,enemy.Location,Core.TickCount),"Accepted movement cancellation start");
            var stepState=ClassCombat.For(owner);long deadline=stepState.NextAction;int paid=NecromancerEffects.ManaBalance256(owner),beforeMove=Life(enemy);
            owner.Direction=Direction.West;Require(owner.Move(Direction.West) && stepState.PendingAbility==CombatAbility.None && stepState.BarbarianAttack==null,"Accepted step retained prepared attack");
            owner.Direction=Direction.East;Require(owner.Move(Direction.East) && owner.Location==stepOrigin && !ClassCombat.ResolveAction(owner,stepState.EffectAt)
                && stepState.NextAction==deadline && NecromancerEffects.ManaBalance256(owner)==paid && Life(enemy)==beforeMove,"Returning to origin revived/refunded prepared attack");
            // A held refresh queues the next target; it must not replace the prepared combat record.
            Ready(owner);Mana(owner);var other=new Skeleton{NoKillAwards=true};other.SetHits(30000);other.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);Stop(other);int untouched=Life(other);
            Require(ClassCombat.ContinueAttack(owner,ability,enemy,enemy.Location,Core.TickCount) && ClassCombat.ContinueAttack(owner,ability,other,other.Location,Core.TickCount),"Held target refresh");
            var held=ClassCombat.For(owner);Require(held.PendingTarget==enemy.Serial && held.BarbarianAttack.Target==enemy.Serial && held.CastTarget==other.Serial,"Held refresh replaced prepared target");
            ClassCombat.ReleaseCast(owner);Require(ClassCombat.ResolveAction(owner,held.EffectAt) && Life(other)==untouched,"Held target refresh delivered to unprepared enemy");other.Delete();
            Ready(owner);var bow=Equip(owner,new Bow());Require(!ClassCombat.Perform(owner,ability,enemy,enemy.Location,Core.TickCount),"Attack accepted bow");bow.Delete();Require(owner.EquipItem(weapon),"Attack restore melee");ClassGear.Refresh(owner);
        }
    }
    private static void Damage(PlayerMobile owner,Skeleton enemy)
    {
        foreach(var ability in new[]{CombatAbility.Bash,CombatAbility.Stun})
        {
            Require(SkillBindings.Instance.Assign(owner,true,ability) && SkillBindings.Instance.Assign(owner,false,ability),"Attack damage binding");
            foreach(int input in new[]{0,1,2})
            {
                Resist(enemy,50);Mana(owner);var values=SkillMechanics.Values(owner,ability);var raw=GearEffects.Damage(owner,0,enemy,(int)values.Eval("calc1"),_=>0);
                var first=new Rolls(true);BarbarianMelee.Effects=first;int hp=Life(enemy);
                Ready(owner);int balance=NecromancerEffects.ManaBalance256(owner);Require(ClassCombat.Perform(owner,ability,enemy,enemy.Location,Core.TickCount),"Snapshot attack begin");var state=ClassCombat.For(owner);var packet=state.BarbarianAttack;
                int expected=packet.Impact.LifeDamage256.Value;
                Require(packet.Hit && packet.Impact.Parts.Physical==raw.Physical+values.Eval("calc2") && first.Calls==1 && Life(enemy)==hp,"Prepared critical/resistance/flat order");
                // Prepared source data is retained even if stats/resistance change before the event.
                Resist(enemy,100);int strength=owner.RawStr;owner.RawStr+=50;ClassCombat.ReleaseCast(owner);
                Require(ClassCombat.ResolveAction(owner,state.EffectAt) && hp-Life(enemy)==expected && first.Calls==1 && NecromancerEffects.ManaBalance256(owner)==balance-512,"Prepared packet rerolled/resisted/paid at impact");owner.RawStr=strength;TimedEffects.ClearGear(enemy);
                BarbarianMelee.Effects=new Rolls();Resist(enemy,50);Mana(owner);
                int loss=Cast(owner,enemy,ability,input);Require(loss>0,"Actual attack delivered no damage");TimedEffects.ClearGear(enemy);
            }
            Resist(enemy,100);Mana(owner);BarbarianMelee.Effects=new Rolls(true);
            int immune=Cast(owner,enemy,ability);Require(immune==0,"Postallocation flat physical field incorrectly increased stored life damage, or Stun pierced immunity");
            Resist(enemy,0);Mana(owner);BarbarianMelee.Effects=new Rolls();BarbarianMelee.HitRoll=()=>1;
            int rolls=0;BarbarianMelee.ElementRoll=()=>{rolls++;return 0;};int hpMiss=Life(enemy);
            Require(Cast(owner,enemy,ability)==0 && Life(enemy)==hpMiss && rolls==0,"Miss dealt damage or rolled elements");BarbarianMelee.HitRoll=()=>0;BarbarianMelee.ElementRoll=()=>0;
        }
    }
    private static void Gear(PlayerMobile owner,Skeleton enemy,Item original)
    {
        var sword=Named(owner,"Hellplague");var values=SkillMechanics.Values(owner,CombatAbility.Bash);Resist(enemy,0);Ready(owner);Mana(owner);
        var raw=GearEffects.Damage(owner,0,enemy,(int)values.Eval("calc1"),_=>0);var rolls=new Rolls(true);BarbarianMelee.Effects=rolls;
        Require(ClassCombat.Perform(owner,CombatAbility.Bash,enemy,enemy.Location,Core.TickCount),"Named attack begin");var state=ClassCombat.For(owner);var packet=state.BarbarianAttack;
        Require(raw.Fire>0 && packet.Impact.Parts.Fire==raw.Fire && packet.Impact.Parts.Physical==2*raw.Physical+values.Eval("calc2"),"Named elements doubled or mastery/local ED/flat composed incorrectly");
        int hp=Life(enemy);ClassCombat.ReleaseCast(owner);Require(ClassCombat.ResolveAction(owner,state.EffectAt) && hp-Life(enemy)==packet.Impact.LifeDamage256.Value,"Actual named elemental attack differs from prepared packet");TimedEffects.ClearGear(enemy);sword.Delete();Require(owner.EquipItem(original),"Named attack restore source weapon");ClassGear.Refresh(owner);BarbarianMelee.Effects=new Rolls();
        var halberd=Equip(owner,new Halberd());Require(CombatTiming.Range(owner,CombatAbility.Bash)==CombatTiming.Range(owner,CombatAbility.BasicAttack),"Twohand Bash reach");Mana(owner);Cast(owner,enemy,CombatAbility.Bash,2);halberd.Delete();Require(owner.EquipItem(original),"Twohand restore source weapon");ClassGear.Refresh(owner);
    }
    private static void Death(Skeleton target,PlayerMobile reference)
    {
        var actor=new PlayerMobile{Body=0x190,Name="BarbarianAttackFixture",Player=true};actor.AddItem(new Backpack());ClassCreation.Profiles.Assign(actor,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(actor,99);
        FixtureSpace.Place(actor,new(reference.X,reference.Y+3,reference.Z),reference.Map);Train(actor,CombatAbility.Bash,1);actor.Hits=actor.HitsMax;Mana(actor);target.MoveToWorld(new(actor.X+1,actor.Y,actor.Z),actor.Map);int hp=Life(target);
        Require(ClassCombat.Perform(actor,CombatAbility.Bash,target,target.Location,Core.TickCount),"Death cancellation start");long at=ClassCombat.For(actor).EffectAt;actor.Kill();
        Require(!actor.Alive && ClassCombat.For(actor).BarbarianAttack==null && !ClassCombat.ResolveAction(actor,at) && Life(target)==hp,"Death retained/delivered paid prepared attack");actor.Corpse?.Delete();actor.Delete();target.MoveToWorld(new(reference.X+1,reference.Y,reference.Z),reference.Map);
    }
    private static void Seed(PlayerMobile owner,Skeleton enemy,Item weapon,Item helm,Skeleton control)
    {
        Stop(enemy);Stop(control);Freeze(owner);Freeze(enemy);Freeze(control);Ready(owner);Mana(owner);
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.Bash) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Stun),"Saved attack bindings");
        var state=CombatStuns.Snapshot().Single(x=>x.Target==control.Serial.Value) with{UntilUtc=TimedEffects.Now.AddMinutes(10)};
        CombatStuns.Restore(CombatStuns.Snapshot().Where(x=>x.Target!=state.Target).Append(state).ToArray());
        // Save one paid, uncommitted action. Active combat input/prepared packet must not replay after restart.
        int hp=Life(enemy);Require(ClassCombat.Perform(owner,CombatAbility.Bash,enemy,enemy.Location,Core.TickCount),"Saved pending Bash start");ClassCombat.ReleaseCast(owner);
        var pending=ClassCombat.For(owner);pending.EffectAt=Core.TickCount+600000;pending.BarbarianAttack=pending.BarbarianAttack with{EffectAt=pending.EffectAt}; // Fixture-only pending inspection hold.
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,enemy.Serial.Value,weapon.Serial.Value,helm.Serial.Value,control.Serial.Value,owner.Location,owner.Map.MapID,NecromancerEffects.ManaBalance256(owner),hp,state)));
    }
    private static void Check()
    {
        Goldens();var owner=ClassSkillChecks.Player(DiabloClass.Barbarian);owner.Name="BarbarianAttackFixture";if(owner.Backpack==null){owner.AddItem(new Backpack());}
        owner.MoveToWorld(ClassSkillChecks.Corridor(owner,6050,1700),owner.Map);
        while(owner.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Attack Strength");}while(owner.RawDex<200){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Attack Dexterity");}
        var weapon=Equip(owner,new Longsword());Freeze(owner);var enemy=new Skeleton{NoKillAwards=true};enemy.SetHits(30000);enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Stop(enemy);Freeze(enemy);
        Gates(owner,enemy,weapon);Death(enemy,owner);Damage(owner,enemy);var helm=Named(owner,"Tarnhelm");
        var values=SkillMechanics.Values(owner,CombatAbility.Bash);Require(values.Rank==2 && values.Eval("calc1")==60 && values.Eval("ToHitCalc")==30,"Soft Bash/hard synergies mixed");
        values=SkillMechanics.Values(owner,CombatAbility.Stun);Require(values.Rank==2 && values.Eval("calc1")==8 && values.DurationFrames==36,"Soft Stun/hard War Cry synergy mixed");Gear(owner,enemy,weapon);
        Train(owner,CombatAbility.Bash,20);Train(owner,CombatAbility.Stun,20);Require(SkillMechanics.Rank(owner,CombatAbility.Bash)==21 && SkillMechanics.Values(owner,CombatAbility.Bash).Eval("calc1")==250 && SkillMechanics.Values(owner,CombatAbility.Stun).Eval("calc1")==160,"Actual allocated/effective synergy ranks");Damage(owner,enemy);
        enemy.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);var control=new Skeleton{NoKillAwards=true};control.SetHits(30000);control.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);control.AIObject.AITimer.Stop();Freeze(control);Resist(control,100);var origin=control.Location;
        Require(SourceKnockbacks.Rule(control) is{Knockback:true,GetHit:true,Distance:5},"Bash control fixture lacks mapped knockback");Mana(owner);SkillBindings.Instance.Assign(owner,true,CombatAbility.Bash);Cast(owner,control,CombatAbility.Bash);
        Require(SourceKnockbacks.Remaining(control)>0 && control.Location==origin,"Bash skipped timed knockback or displaced instantly");
        int observation=(CreatureHitRecovery.Frames(CreatureProfiles.For(control).HitRecovery)+8)*40+300;bool moved=false;
        Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),(observation+39)/40,()=>
        {moved|=control.Location!=origin;control.Combatant=null;control.AIObject?.AITimer.Stop();});
        Timer.StartTimer(TimeSpan.FromMilliseconds(observation),()=>Controlled(()=>
        {
            try
            {
                Require(SourceKnockbacks.Remaining(control)==0 && moved,$"Bash knockback remaining={SourceKnockbacks.Remaining(control)}, origin={origin}, actual={control.Location}");control.AIObject.AITimer.Stop();control.Combatant=null;control.MoveToWorld(origin,owner.Map);Mana(owner);
                SkillBindings.Instance.Assign(owner,true,CombatAbility.Stun);SkillBindings.Instance.Assign(owner,false,CombatAbility.Stun);Cast(owner,control,CombatAbility.Stun);
                int duration=SkillMechanics.Values(owner,CombatAbility.Stun).DurationFrames*40;Require(CombatStuns.Remaining(control)>0 && CombatStuns.Remaining(control)<=duration && !control.Move(Direction.East),"Stun duration/native movement gate");
                int life=Life(owner);((BaseWeapon)control.Weapon).OnSwing(control,owner);Require(Life(owner)==life,"Stunned creature attacked");
                Timer.StartTimer(TimeSpan.FromMilliseconds(duration+180),()=>Controlled(()=>
                {
                    try
                    {
                        Require(CombatStuns.Remaining(control)==0,"Stun failed natural expiry");control.Combatant=null;control.AIObject.AITimer.Stop();Require(CombatDisplacement.TryStep(control,Direction.East),"Stun expiry did not permit movement");control.MoveToWorld(origin,owner.Map);Mana(owner);Cast(owner,control,CombatAbility.Stun);
                        enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);control.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);Seed(owner,enemy,weapon,helm,control);Log.Information("barbarian-attacks-result: passed");
                    }
                    catch(Exception e){Log.Error(e,"barbarian-attacks-result: failed");}
                }));
            }
            catch(Exception e){Log.Error(e,"barbarian-attacks-result: failed");}
        }));
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var enemy=(Skeleton)World.FindMobile((Serial)saved.Enemy);var control=(Skeleton)World.FindMobile((Serial)saved.Control);
        owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);Stop(enemy);Stop(control);Freeze(owner);Freeze(enemy);Freeze(control);
        Require(Life(enemy)==saved.Life && NecromancerEffects.ManaBalance256(owner)==saved.Mana && ClassCombat.For(owner).PendingAbility==CombatAbility.None && ClassCombat.For(owner).BarbarianAttack==null,$"Restart state: life={Life(enemy)}/{saved.Life}, mana={NecromancerEffects.ManaBalance256(owner)}/{saved.Mana}, pending={ClassCombat.For(owner).PendingAbility}, prepared={ClassCombat.For(owner).BarbarianAttack!=null}");
        Require(SkillBindings.Instance.Get(owner).Left==CombatAbility.Bash && SkillBindings.Instance.Get(owner).Right==CombatAbility.Stun && SkillMechanics.Rank(owner,CombatAbility.Bash)==21 && SkillMechanics.Rank(owner,CombatAbility.Stun)==21,"Attack gear/rank/bindings lost");
        Require(CombatStuns.Snapshot().Single(x=>x.Target==saved.Control)==saved.State && CombatStuns.Remaining(control)>0,"Saved Stun deadline lost");
        var weapon=World.FindItem((Serial)saved.Weapon);Require(CombatTiming.Equipped(owner)?.Serial==weapon.Serial,"Saved attack weapon lost");Mana(owner);Damage(owner,enemy);Seed(owner,enemy,weapon,World.FindItem((Serial)saved.Helm),control);
    }
}
