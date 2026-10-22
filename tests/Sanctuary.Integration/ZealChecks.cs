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

public static class ZealChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(ZealChecks));
    private static bool _creating;
    private sealed record Saved(uint Owner,uint Enemy,int Life,int Mana,int EnemyLife,int Rank,CombatAbility Left,CombatAbility Right,Point3D Origin,int Map);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"zeal-checks.json");
    [CallPriority(int.MaxValue)]
    public static void Configure()
    {
        FreezeManaRate();
        EventSink.WorldLoad+=()=>
        {
            if(File.Exists(Manifest) && World.FindMobile((Serial)JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)).Enemy) is BaseCreature actor){Stop(actor);}
        };
        Register("zeal-check",Check);Register("zeal-reload-check",Reload);
    }
    [CallPriority(int.MaxValue)]
    public static void Initialize()=>FreezeManaRate();
    private static void FreezeManaRate()
    {
        // Hold only this fixture's native timer, including constructor/deserialization startup.
        var previous=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=actor=>actor is PlayerMobile && (_creating || actor.Name=="ZealFixture-Paladin")
            ?TimeSpan.FromHours(1):previous?.Invoke(actor)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Zeal source timing, cyclic targets, one start payment and transient sequence persistence",
        _=>Core.LoopContext.Post(()=>{try{Controlled(action);_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static int Life(Mobile actor)=>actor.Hits*256-SkillHealth.Remainder256(actor);
    private static void Stop(BaseCreature actor){actor.Frozen=actor.CantWalk=true;actor.Combatant=null;actor.AIObject?.AITimer.Stop();}
    private static void Freeze(Mobile actor)
    {
        TimedEffects.Poison(actor,actor,.000001,60000,TimedEffects.Now);
        TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==actor.Serial.Value?x with{UpdatedUtc=TimedEffects.Now.AddMinutes(5)}:x).ToArray());
    }
    private static Point3D Origin(PlayerMobile owner)
    {
        // Other suites retain actors for reload checks. Keep Zeal's cyclic target pool independent.
        for(int x=2200;x<=2500;x+=50)for(int y=1000;y<=1300;y+=50)
        {
            Point3D start;
            try{start=ClassSkillChecks.Corridor(owner,x,y);}catch(InvalidOperationException){continue;}
            var point=new Point3D(start.X+2,start.Y,start.Z);bool clear=true;
            foreach(var actor in owner.Map.GetMobilesInRange<Mobile>(point,20))
            {if(actor!=owner && !actor.Deleted){clear=false;break;}}
            if(clear && Server.Spells.SpellHelper.CheckTown(point,owner)
                && Server.Spells.SpellHelper.CheckTown(new Point3D(point.X+1,point.Y+1,point.Z),owner)
                && owner.Map.CanSpawnMobile(point.X+1,point.Y+1,point.Z)
                && owner.Map.LineOfSight(new Point3D(point.X,point.Y,point.Z+12),new Point3D(point.X+1,point.Y+1,point.Z+12))){return point;}
        }
        throw new InvalidOperationException("No independent Zeal corridor/diagonal target area");
    }
    private static void Ready(PlayerMobile owner)
    {
        var s=ClassCombat.For(owner);s.Zeal=null;s.ZealPaid=false;s.ZealHits=0;s.PendingAbility=CombatAbility.None;s.Target=Serial.Zero;
        ClassCombat.ReleaseCast(owner);s.NextAction=s.RecoveryUntil=Core.TickCount;
    }
    private static void Mana(PlayerMobile owner){owner.Mana=owner.ManaMax;NecromancerEffects.Profiles.Store(owner,new());}
    private static void Controlled(Action action)
    {
        var roll=PaladinZeal.Roll;var hit=PaladinZeal.HitRoll;var element=PaladinZeal.ElementRoll;
        try{PaladinZeal.Roll=_=>0;PaladinZeal.HitRoll=()=>0;PaladinZeal.ElementRoll=()=>0;action();}
        finally{PaladinZeal.Roll=roll;PaladinZeal.HitRoll=hit;PaladinZeal.ElementRoll=element;}
    }
    private static void Probe()
    {
        string root=Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT");
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(root,"tools/reference/zeal-engine-evidence.json")));
        foreach(var row in json.RootElement.GetProperty("probe").GetProperty("timingCases").EnumerateArray())
        {
            var timing=PaladinZeal.Timing(row[0].GetString()[4..],row[1].GetInt32());int n=0;
            foreach(var hit in row[3].EnumerateArray()){Require(hit.GetInt32()==++n*timing.EffectFrame,"Zeal source event/rollback mismatch");}
            Require(row[4].GetInt32()==timing.EffectFrame*(n-1)+timing.Frames,"Zeal source final backswing mismatch");
        }
        foreach(var row in json.RootElement.GetProperty("probe").GetProperty("selectionCases").EnumerateArray())
        {
            int missing=row[1].GetInt32();uint[] ids=missing==1?[30,20]:missing==2?[30,10]:[30,10,20];
            Require(PaladinZeal.NextSerial(ids,unchecked((uint)row[0].GetInt32()))==row[2].GetUInt32(),"Zeal cyclic GUID order mismatch");
        }
        foreach(int rank in new[]{1,2,3,4,5,20,21,50})foreach(int hard in new[]{0,1,20})
        {
            var v=new SkillValues(SkillMechanics.For(CombatAbility.Zeal),_=>rank,id=>id=="Sacrifice"?hard:0,99);
            Require(v.Eval("calc1")==Math.Min(rank+1,5) && v.Eval("calc2")==Math.Max(0,rank-4)*6+hard*12 && v.Formula("toht")==rank*10 && v.Mana256==512,"Pinned Zeal rank/synergy/mana fields");
        }
    }
    private static void Begin(PlayerMobile owner,Mobile target,int input=0)
    {
        Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner);
        Require(input==0?ClassCombat.ContinueAttack(owner,CombatAbility.Zeal,target,target.Location,Core.TickCount)
            :input==1?ClassCombat.ContinueCast(owner,CombatAbility.Zeal,target,target.Location,Core.TickCount)
            :ClassCombat.Perform(owner,CombatAbility.Zeal,target,target.Location,Core.TickCount),$"Zeal input rejected: available={PaladinCombat.Available(owner,CombatAbility.Zeal)}, target={PaladinZeal.ValidTarget(owner,target.Serial)}, hostile={ClassCombat.IsHostile(owner,target)}, overlap={AlliedOverlap.Allowed(owner)}, frozen={owner.Frozen}, paralyzed={owner.Paralyzed}, spell={owner.Spell}, owner={owner.Location}/{owner.Map}, enemy={target.Location}/{target.Map}, timing={ClassCombat.For(owner).NextAction-Core.TickCount}");
        Require(ClassCombat.For(owner).PendingAbility==CombatAbility.Zeal && mana-NecromancerEffects.ManaBalance256(owner)==512,
            $"Zeal start/payment failed: pending={ClassCombat.For(owner).PendingAbility}, paid={mana-NecromancerEffects.ManaBalance256(owner)}, validTarget={PaladinZeal.ValidTarget(owner,target.Serial)}, location={owner.Location}");
        ClassCombat.ReleaseCast(owner);
    }
    private static void Sequence(PlayerMobile owner,BaseCreature target,int input=0,bool miss=false)
    {
        int hp=Life(target),mana=NecromancerEffects.ManaBalance256(owner),rolls=0;PaladinZeal.Roll=n=>{rolls++;return 0;};PaladinZeal.HitRoll=()=>miss?1:0;
        int count=(int)SkillMechanics.Values(owner,CombatAbility.Zeal).Eval("calc1");
        int damage=ClassStats.SkillPhysicalRange256(owner,(int)SkillMechanics.Values(owner,CombatAbility.Zeal).Eval("calc2"),0).Min*50/100;
        Begin(owner,target,input);var state=ClassCombat.For(owner);long first=state.EffectAt;
        Require(!ClassCombat.ResolveAction(owner,first-1) && Life(target)==hp,"Zeal hit before the event frame");
        Require(PaladinZeal.Locked(owner,Core.TickCount) && !owner.Move(Direction.North),"Zeal windup allowed movement");
        Require(!HitRecovery.Apply(owner,owner.HitsMax,Core.TickCount),"Zeal windup interrupted by hit recovery");
        for(int i=0;i<count;i++)
        {
            long at=i==0?first:state.Zeal.EffectAt;
            Require(ClassCombat.ResolveAction(owner,at),"Zeal strike missing");
            Require(Life(target)==hp-(miss?0:damage*(i+1)),"Zeal wrong damage/count or instantaneous extra hit");
            Require(mana-NecromancerEffects.ManaBalance256(owner)==512,"Zeal charged mana per strike");
            if(i+1<count)
            {
                int before=Life(target);Require(!ClassCombat.ResolveAction(owner,state.Zeal.EffectAt-1) && Life(target)==before,"Zeal repeated early");
                ClassCombat.Stop(owner);Require(state.Zeal.Remaining==count-i-1,"Releasing/stop canceled committed Zeal series");
                Require(!HitRecovery.Apply(owner,owner.HitsMax,Core.TickCount),"Zeal repeat interrupted by hit recovery");
            }
        }
        Require(rolls==(miss?0:count),"Zeal damage was rolled for a miss or more than once per hit");
        Require(state.Zeal.Remaining==0 && PaladinZeal.Locked(owner,Core.TickCount),"Zeal final backswing unlocked early");
        Require(!ClassCombat.ResolveAction(owner,state.NextAction) && state.Zeal==null,"Zeal did not release final backswing");
        PaladinZeal.Roll=_=>0;PaladinZeal.HitRoll=()=>0;
    }
    private static void Gates(PlayerMobile owner,Ogre enemy)
    {
        LowMana(owner,enemy);Mana(owner);
        var bow=new Bow();owner.AddToBackpack(bow);Require(owner.EquipItem(bow),"Zeal bow fixture equip");Ready(owner);
        Require(!ClassCombat.Perform(owner,CombatAbility.Zeal,enemy,enemy.Location,Core.TickCount),"Zeal accepted a bow");bow.Delete();
        var place=enemy.Location;enemy.MoveToWorld(new(owner.X+9,owner.Y,owner.Z),owner.Map);Ready(owner);
        Require(!ClassCombat.Perform(owner,CombatAbility.Zeal,enemy,enemy.Location,Core.TickCount),"Zeal accepted out-of-range target");enemy.MoveToWorld(place,owner.Map);
        Begin(owner,enemy);var lost=ClassCombat.For(owner);long end=lost.NextAction;
        enemy.MoveToWorld(new(owner.X+9,owner.Y,owner.Z),owner.Map);
        Require(!ClassCombat.ResolveAction(owner,lost.EffectAt) && lost.Zeal is{Remaining:0} && PaladinZeal.Locked(owner,Core.TickCount),"Missing first target canceled the scheduled Zeal backswing");
        Require(!ClassCombat.ResolveAction(owner,end) && lost.Zeal==null,"Missing-target Zeal backswing did not finish");
        enemy.MoveToWorld(place,owner.Map);
        Begin(owner,enemy);var state=ClassCombat.For(owner);var weapon=new Dagger();Require(owner.EquipItem(weapon),"Zeal weapon change fixture");int hp=Life(enemy);
        Require(!ClassCombat.ResolveAction(owner,state.EffectAt) && Life(enemy)==hp && !state.ZealPaid,"Zeal weapon-change cancellation failed");weapon.Delete();
        Begin(owner,enemy);state=ClassCombat.For(owner);Require(ClassCombat.ResolveAction(owner,state.EffectAt),"Zeal first strike for cancellation");hp=Life(enemy);
        weapon=new Dagger();Require(owner.EquipItem(weapon),"Zeal repeat weapon fixture");
        Require(!ClassCombat.ResolveAction(owner,state.Zeal.EffectAt) && Life(enemy)==hp && state.Zeal==null,"Zeal repeat survived weapon change");weapon.Delete();Ready(owner);
        var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.MoveToWorld(new(owner.X,owner.Y+1,owner.Z),owner.Map);
        Require(PaladinZeal.Target(owner,ally.Serial)?.Serial==enemy.Serial,"Zeal selected an allied player");ally.Delete();
    }
    private static void LowMana(PlayerMobile owner,Ogre enemy)
    {
        var binding=SkillBindings.Instance.Get(owner);
        foreach(int input in new[]{0,1,2})foreach(int balance in new[]{0,256,511})
        {
            Ready(owner);owner.Mana=0;NecromancerEffects.Profiles.Store(owner,new());NecromancerEffects.AddMana(owner,balance);
            long now=Core.TickCount;
            Require(input==0?ClassCombat.ContinueAttack(owner,CombatAbility.Zeal,enemy,enemy.Location,now)
                :input==1?ClassCombat.ContinueCast(owner,CombatAbility.Zeal,enemy,enemy.Location,now)
                :ClassCombat.Perform(owner,CombatAbility.Zeal,enemy,enemy.Location,now),"Approved low-mana Zeal fallback rejected");
            var state=ClassCombat.For(owner);var ordinary=CombatTiming.For(owner,CombatAbility.BasicAttack);
            Require(state.PendingAbility==CombatAbility.BasicAttack && state.Timing==ordinary && state.Zeal==null && !state.ZealPaid && state.ZealHits==0
                && !PaladinZeal.Locked(owner,now) && NecromancerEffects.ManaBalance256(owner)==balance,"Fallback used Zeal timing/lock/payment/series");
            Require(input==2 || state.HeldAbility==CombatAbility.Zeal && state.PendingLeft==(input==0),"Fallback lost held Zeal/input side");
            int life=Life(enemy);Require(!ClassCombat.ResolveAction(owner,state.EffectAt-1) && Life(enemy)==life,"Fallback attacked before ordinary event");
            Require(ClassCombat.ResolveAction(owner,state.EffectAt) && state.PendingAbility==CombatAbility.None && state.Zeal==null
                && NecromancerEffects.ManaBalance256(owner)==balance,"Fallback did not finish one ordinary unpaid attack");
            Require(!ClassCombat.ResolveAction(owner,state.NextAction),"Fallback created an extra series strike");
            if(input!=2)
            {
                long next=state.NextAction;state.HoldUntil=next+CombatProtocol.HoldLeaseMs;
                ClassCombat.UpdateCombat(owner,next);
                Require(state.PendingAbility==CombatAbility.BasicAttack && state.HeldAbility==CombatAbility.Zeal,"Held low-mana Zeal did not repeat ordinary attack");
                Require(ClassCombat.ResolveAction(owner,state.EffectAt),"Repeated fallback did not resolve");
                NecromancerEffects.AddMana(owner,512-balance);next=state.NextAction;state.HoldUntil=next+CombatProtocol.HoldLeaseMs;
                ClassCombat.UpdateCombat(owner,next);
                Require(state.PendingAbility==CombatAbility.Zeal && state.ZealPaid && NecromancerEffects.ManaBalance256(owner)==0,"Held Zeal did not resume at exactly two mana");
            }
            var current=SkillBindings.Instance.Get(owner);
            Require(current.Left==binding.Left && current.Right==binding.Right && SkillBindings.Instance.GetQuick(owner.Serial,DiabloClass.Paladin,0)==CombatAbility.Zeal,"Fallback changed talent bindings");
        }
        Ready(owner);
        owner.Mana=0;NecromancerEffects.Profiles.Store(owner,new());
        var location=enemy.Location;enemy.MoveToWorld(new(owner.X+9,owner.Y,owner.Z),owner.Map);
        Require(!ClassCombat.Perform(owner,CombatAbility.Zeal,enemy,enemy.Location,Core.TickCount),"Low-mana fallback bypassed melee targeting");
        enemy.MoveToWorld(location,owner.Map);
        Require(ClassCombat.Perform(owner,CombatAbility.Zeal,enemy,enemy.Location,Core.TickCount),"Fallback cancellation fixture");
        var weapon=new Dagger();Require(owner.EquipItem(weapon),"Fallback weapon-change fixture");int hp=Life(enemy);
        Require(!ClassCombat.ResolveAction(owner,ClassCombat.For(owner).EffectAt) && Life(enemy)==hp,"Fallback survived ordinary weapon-change cancellation");
        weapon.Delete();Ready(owner);
    }
    private static void Targets(PlayerMobile owner,Ogre a)
    {
        var b=new Ogre{NoKillAwards=true};b.SetHits(30000);b.SetResistance(ResistanceType.Physical,50);b.MoveToWorld(new(owner.X,owner.Y+1,owner.Z),owner.Map);Stop(b);Freeze(b);
        int ahp=Life(a),bhp=Life(b);Begin(owner,a);var state=ClassCombat.For(owner);
        Require(ClassCombat.ResolveAction(owner,state.EffectAt) && Life(a)<ahp && Life(b)==bhp && state.Zeal.Target==b.Serial,"Zeal failed source-order retarget");
        Require(ClassCombat.ResolveAction(owner,state.Zeal.EffectAt) && Life(b)<bhp && state.Zeal.Target==a.Serial,"Zeal failed source-order wrap");
        a.MoveToWorld(new(owner.X+9,owner.Y,owner.Z),owner.Map);bhp=Life(b);
        Require(ClassCombat.ResolveAction(owner,state.Zeal.EffectAt) && Life(b)<bhp,"Zeal did not retarget an escaped target");
        b.Delete();Require(!ClassCombat.ResolveAction(owner,state.Zeal.EffectAt) && state.Zeal.Remaining==0,"Zeal invented a target after all targets disappeared");
        Ready(owner);a.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
    }
    private static void Gear(PlayerMobile owner,Ogre enemy)
    {
        for(int i=0;i<80;i++){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Zeal source weapon Strength");}
        for(int i=0;i<40;i++){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Zeal source weapon Dexterity");}
        foreach(var (weapon,code) in new (BaseWeapon,string)[]{(new Mace(),"1HS"),(new Maul(),"STF"),(new Halberd(),"STF")})
        {
            Ready(owner);ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(weapon),"Zeal converted weapon equip");
            Require(PaladinZeal.AnimationCode(owner)==code,"Zeal ignored source weapon animation class");
            var row=PaladinZeal.Animation(code);var timing=CombatTiming.For(owner,CombatAbility.Zeal);
            Require(timing.EffectFrame>0 && row.Frames>row.Event,"Zeal source weapon timing");
            Mana(owner);Sequence(owner,enemy,2);weapon.Delete();
        }
        while(ClassCreation.Profiles.GetRank(owner.Serial,"paladin.sacrifice")<20){Require(ClassCreation.Profiles.TryTrain(owner.Serial,"paladin.sacrifice",out _),"Zeal hard Sacrifice synergy");}
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);
        Require(owner.EquipItem(helm),"Zeal actual soft-rank gear");
        var values=SkillMechanics.Values(owner,CombatAbility.Zeal);Require(values.Rank==5 && values.Eval("calc1")==5 && values.Eval("calc2")==246,"Zeal soft rank changed hard Sacrifice synergy");
        Mana(owner);Sequence(owner,enemy,1);helm.Delete();
    }
    private static void StartRankSnapshot(PlayerMobile owner,Ogre enemy)
    {
        Require(SkillMechanics.Rank(owner,CombatAbility.Zeal)==1,"Zeal snapshot fixture must start at rank1");
        Mana(owner);Begin(owner,enemy);var state=ClassCombat.For(owner);
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);
        Require(owner.EquipItem(helm) && SkillMechanics.Rank(owner,CombatAbility.Zeal)==2,"Zeal windup soft-rank change");
        Require(ClassCombat.ResolveAction(owner,state.EffectAt) && state.Zeal.Remaining==1,"Zeal resnapshotted its hit count after starting");
        Require(ClassCombat.ResolveAction(owner,state.Zeal.EffectAt) && state.Zeal.Remaining==0,"Rank1 Zeal gained a third attack from windup gear");
        helm.Delete();Ready(owner);
    }
    private static void LateAndDiagonal(PlayerMobile owner,Ogre enemy)
    {
        enemy.MoveToWorld(new(owner.X+1,owner.Y+1,owner.Z),owner.Map);Mana(owner);Sequence(owner,enemy,2);
        Mana(owner);Begin(owner,enemy);var state=ClassCombat.For(owner);Require(ClassCombat.ResolveAction(owner,state.EffectAt),"Zeal late fixture start");
        int hp=Life(enemy),remaining=state.Zeal.Remaining;long late=state.Zeal.EffectAt+2000;
        int damage=ClassStats.SkillPhysicalRange256(owner,(int)SkillMechanics.Values(owner,CombatAbility.Zeal).Eval("calc2"),0).Min*50/100;
        Require(ClassCombat.ResolveAction(owner,late) && hp-Life(enemy)==damage && state.Zeal.Remaining==remaining-1 && state.Zeal.EffectAt>late,"Late Zeal update burst multiple strikes");
        Ready(owner);enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
    }
    private static void Check()
    {
        Probe();PlayerMobile owner;_creating=true;
        try{owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.Name="ZealFixture-Paladin";}finally{_creating=false;}
        owner.AddItem(new Backpack());owner.MoveToWorld(Origin(owner),owner.Map);Freeze(owner);
        Require(owner.Move(Direction.West) && owner.Move(Direction.East),"Zeal movement-control fixture needs a passable corridor");
        var enemy=new Ogre{NoKillAwards=true};enemy.SetHits(30000);enemy.SetResistance(ResistanceType.Physical,50);enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Stop(enemy);Freeze(enemy);
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.Zeal) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Zeal) && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.Zeal),"Zeal bindings");
        Gates(owner,enemy);StartRankSnapshot(owner,enemy);Mana(owner);Sequence(owner,enemy);Mana(owner);Sequence(owner,enemy,1,true);
        while(ClassCreation.Profiles.GetRank(owner.Serial,"paladin.zeal")<4){Require(ClassCreation.Profiles.TryTrain(owner.Serial,"paladin.zeal",out _),"Zeal rank4");}
        Mana(owner);Sequence(owner,enemy,2);Targets(owner,enemy);Gear(owner,enemy);LateAndDiagonal(owner,enemy);
        // A committed start has paid mana; an in-progress input/animation is transient, never replayed by loading a save.
        Mana(owner);Begin(owner,enemy);owner.Hits=owner.HitsMax-11;SkillHealth.Clear(owner);SkillHealth.Damage(owner,owner,17);
        var binding=SkillBindings.Instance.Get(owner);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,enemy.Serial.Value,Life(owner),NecromancerEffects.ManaBalance256(owner),Life(enemy),SkillMechanics.Rank(owner,CombatAbility.Zeal),binding.Left,binding.Right,owner.Location,owner.Map.MapID)));
    }
    private static void Reload()
    {
        Probe();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var enemy=World.FindMobile((Serial)saved.Enemy) as Ogre;
        Require(owner!=null && enemy!=null && Life(owner)==saved.Life && Life(enemy)==saved.EnemyLife && NecromancerEffects.ManaBalance256(owner)==saved.Mana,$"Zeal resources changed over reload: owner {Life(owner)}/{saved.Life}, enemy {Life(enemy)}/{saved.EnemyLife}, mana {NecromancerEffects.ManaBalance256(owner)}/{saved.Mana}, max {owner.HitsMax}/{owner.ManaMax}");
        var binding=SkillBindings.Instance.Get(owner);Require(binding.Left==saved.Left && binding.Right==saved.Right && SkillMechanics.Rank(owner,CombatAbility.Zeal)==saved.Rank,"Zeal rank/bindings lost");
        var state=ClassCombat.For(owner);Require(state.PendingAbility==CombatAbility.None && state.Zeal==null && !state.ZealPaid,"Zeal replayed an old attack or payment after reload");
        // Native offline players load on Internal; simulate login's return to their saved world position.
        owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);Freeze(owner);Freeze(enemy);Sequence(owner,enemy,1);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Mana=NecromancerEffects.ManaBalance256(owner),EnemyLife=Life(enemy)}));
    }
}
