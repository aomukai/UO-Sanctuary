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

public static class ChargeChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(ChargeChecks));
    private static bool _creating;
    private sealed record Saved(uint Owner,uint Enemy,Point3D Origin,int Map,int Life,int Mana,int EnemyLife,int Rank);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"charge-checks.json");
    [CallPriority(int.MaxValue)]
    public static void Configure()
    {
        FreezeRates();EventSink.WorldLoad+=()=>
        {if(File.Exists(Manifest) && World.FindMobile((Serial)JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)).Enemy) is BaseCreature actor){Stop(actor);}};
        Register("charge-check",Check);Register("charge-reload-check",Reload);
    }
    [CallPriority(int.MaxValue)]
    public static void Initialize()=>FreezeRates();
    private static void FreezeRates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mana=Mobile.ManaRegenRateHandler;var stamina=Mobile.StamRegenRateHandler;
        bool Fixture(Mobile m)=>m is PlayerMobile && (_creating || m.Name=="ChargeFixture-Paladin");
        Mobile.HitsRegenRateHandler=m=>Fixture(m)?TimeSpan.FromHours(1):hp?.Invoke(m)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=m=>Fixture(m)?TimeSpan.FromHours(1):mana?.Invoke(m)??Mobile.DefaultManaRate;
        Mobile.StamRegenRateHandler=m=>Fixture(m)?TimeSpan.FromHours(1):stamina?.Invoke(m)??Mobile.DefaultStamRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Charge phase/cadence/impact and transient action save checks",
        _=>Core.LoopContext.Post(()=>{try{Controlled(action);_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static int Life(Mobile actor)=>actor.Hits*256-SkillHealth.Remainder256(actor);
    private static void Stop(BaseCreature actor){actor.Frozen=actor.CantWalk=true;actor.Combatant=null;actor.AIObject?.AITimer.Stop();}
    private static void FreezeLife(Mobile actor)
    {
        TimedEffects.Poison(actor,actor,.000001,60000,TimedEffects.Now);
        TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==actor.Serial.Value?x with{UpdatedUtc=TimedEffects.Now.AddMinutes(5)}:x).ToArray());
    }
    private static void Mana(PlayerMobile owner){owner.Mana=owner.ManaMax;NecromancerEffects.Profiles.Store(owner,new());}
    private static void Ready(PlayerMobile owner)
    {
        PaladinCharge.Cancel(owner);var s=ClassCombat.For(owner);s.PendingAbility=CombatAbility.None;s.Zeal=null;s.ZealPaid=false;
        ClassCombat.Stop(owner);s.NextAction=s.RecoveryUntil=Core.TickCount;
    }
    private static Point3D Origin(PlayerMobile owner)
    {
        for(int x=2200;x<=2500;x+=50)for(int y=1000;y<=1300;y+=50)
        {
            Point3D p;try{p=ClassSkillChecks.Corridor(owner,x,y);}catch(InvalidOperationException){continue;}
            bool clear=true;foreach(var m in owner.Map.GetMobilesInRange<Mobile>(p,15)){if(m!=owner){clear=false;break;}}
            if(clear && Server.Spells.SpellHelper.CheckTown(p,owner) && Server.Spells.SpellHelper.CheckTown(new Point3D(p.X+5,p.Y,p.Z),owner)){return p;}
        }
        throw new InvalidOperationException("No independent Charge skill corridor");
    }
    private static void Controlled(Action action)
    {
        var hit=PaladinCharge.HitRoll;var roll=PaladinCharge.Roll;var elements=PaladinCharge.ElementRoll;
        try{PaladinCharge.HitRoll=()=>0;PaladinCharge.Roll=_=>0;PaladinCharge.ElementRoll=()=>0;action();}
        finally{PaladinCharge.HitRoll=hit;PaladinCharge.Roll=roll;PaladinCharge.ElementRoll=elements;}
    }
    private static void Probe()
    {
        string root=Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT");
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(root,"tools/reference/charge-engine-evidence.json")));
        foreach(var row in json.RootElement.GetProperty("probe").GetProperty("velocityCases").EnumerateArray())
        {Require(PaladinCharge.Velocity256(9,row[0].GetInt32(),150)==row[3].GetInt32(),"Charge velocity differs from original St31");}
        foreach(int rank in new[]{1,2,20,50})foreach(int hard in new[]{0,1,20})
        {
            var v=new SkillValues(SkillMechanics.For(CombatAbility.Charge),_=>rank,_=>hard,99);
            Require(v.Eval("calc1")==100+(rank-1)*25+hard*40 && v.Formula("toht")==50+(rank-1)*15 && v.Mana256==2304,"Pinned Charge rank/mana/synergies");
        }
        var slow=CombatTiming.Calculate(DiabloClass.Paladin,CombatAbility.Charge,new(AttackFamily.Swing,0));
        var fast=CombatTiming.Calculate(DiabloClass.Paladin,CombatAbility.Charge,new(AttackFamily.Swing,-30),1000,1000);
        Require(slow==fast && slow.Frames==15,"Charge sequence borrowed IAS/FCR attack timing");
    }
    private static long Begin(PlayerMobile owner,Mobile target,Point3D point,int input=0)
    {
        Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner);long now=Core.TickCount;
        bool result=input==0?ClassCombat.Perform(owner,CombatAbility.Charge,target,point,now)
            :input==1?ClassCombat.ContinueAttack(owner,CombatAbility.Charge,target,point,now):ClassCombat.ContinueCast(owner,CombatAbility.Charge,target,point,now);
        Require(result && ClassCombat.For(owner).Charge!=null && mana-NecromancerEffects.ManaBalance256(owner)==2304,"Charge start/payment failed");
        ClassCombat.ReleaseCast(owner);return now;
    }
    private static void Sequence(PlayerMobile owner,Ogre enemy,int input=0,bool miss=false)
    {
        var origin=owner.Location;enemy.MoveToWorld(new(origin.X+5,origin.Y,origin.Z),owner.Map);
        int hp=Life(enemy),mana=NecromancerEffects.ManaBalance256(owner),stamina=owner.Stam,rolls=0;
        int damage=ClassStats.SkillPhysicalRange256(owner,(int)SkillMechanics.Values(owner,CombatAbility.Charge).Eval("calc1"),0).Min*50/100;
        PaladinCharge.Roll=n=>{rolls++;return 0;};PaladinCharge.HitRoll=()=>miss?1:0;
        long start=Begin(owner,enemy,enemy.Location,input);var state=ClassCombat.For(owner);int frames=0;
        Require(!ClassCombat.ResolveAction(owner,start+39) && owner.Location==origin && Life(enemy)==hp,"Charge advanced before first frame");
        Require(!owner.Move(Direction.West) && !HitRecovery.Apply(owner,owner.HitsMax,Core.TickCount),"Charge movement/recovery lock failed");
        while(state.Charge.Phase==PaladinCharge.Phase.Moving)
        {
            Require(ClassCombat.ResolveAction(owner,state.Charge.NextFrame),"Charge moving frame missing");frames++;
            Require(Life(enemy)==hp && frames<30,"Charge dealt instant travel damage or failed to arrive");
            ClassCombat.Stop(owner);Require(state.Charge!=null,"Stop/release canceled committed Charge movement");
        }
        Require(state.Charge.Phase==PaladinCharge.Phase.Impact && owner.X==origin.X+4,"Charge did not arrive at melee contact");
        Require(frames==6,"Naked source velocity100/Run9 did not preserve FP travel budget");
        long impact=state.Charge.NextFrame;Require(!ClassCombat.ResolveAction(owner,impact-1) && Life(enemy)==hp,"Charge impact occurred early");
        Require(ClassCombat.ResolveAction(owner,impact) && hp-Life(enemy)==(miss?0:damage),"Charge impact damage or miss changed");
        Require(rolls==(miss?0:1) && mana-NecromancerEffects.ManaBalance256(owner)==2304 && owner.Stam==stamina,"Charge rerolled/repaid or drained movement stamina");
        long end=state.Charge.Until;Require(end-impact==8*40,"Charge impact rewind/outro differs from source");
        Require(!ClassCombat.ResolveAction(owner,impact+40) && Life(enemy)==hp-(miss?0:damage),"Charge duplicate impact");
        Require(!ClassCombat.ResolveAction(owner,end) && state.Charge==null && state.PendingAbility==CombatAbility.None,"Charge failed to finish its action");
        PaladinCharge.HitRoll=()=>0;PaladinCharge.Roll=_=>0;owner.MoveToWorld(origin,owner.Map);
    }
    private static void GroundAndBlock(PlayerMobile owner,Ogre enemy)
    {
        var p=owner.Location;enemy.MoveToWorld(new(p.X+12,p.Y,p.Z),owner.Map);Mana(owner);Begin(owner,null,new(p.X+4,p.Y,p.Z));var s=ClassCombat.For(owner);
        for(int i=0;s.Charge.Phase==PaladinCharge.Phase.Moving;i++){Require(i<30,"Ground Charge did not end");ClassCombat.ResolveAction(owner,s.Charge.NextFrame);}
        Require(owner.X==p.X+4 && s.Charge.Phase==PaladinCharge.Phase.Recovery,"Ground Charge invented an impact or did not traverse land");
        ClassCombat.ResolveAction(owner,s.Charge.Until);owner.MoveToWorld(p,owner.Map);
        enemy.MoveToWorld(new(p.X+5,p.Y,p.Z),owner.Map);var wall=new Blocker();wall.MoveToWorld(new(p.X+2,p.Y,p.Z),owner.Map);int hp=Life(enemy);
        try
        {
            Mana(owner);Begin(owner,enemy,enemy.Location);
            for(int i=0;s.Charge.Phase==PaladinCharge.Phase.Moving;i++){Require(i<30,"Blocked Charge did not end");ClassCombat.ResolveAction(owner,s.Charge.NextFrame);}
            Require(owner.X==p.X+1 && Life(enemy)==hp,"Charge crossed obstruction or hit through it");
        }
        finally{wall.Delete();Ready(owner);owner.MoveToWorld(p,owner.Map);}
    }
    private static void Gates(PlayerMobile owner,Ogre enemy)
    {
        Ready(owner);owner.Mana=0;NecromancerEffects.Profiles.Store(owner,new());
        Require(!ClassCombat.Perform(owner,CombatAbility.Charge,enemy,enemy.Location,Core.TickCount),"Charge started without source mana");Mana(owner);
        var bow=new Bow();Require(owner.EquipItem(bow),"Charge bow fixture equip");
        Require(!ClassCombat.Perform(owner,CombatAbility.Charge,enemy,enemy.Location,Core.TickCount),"Charge accepted a bow");bow.Delete();
        var p=owner.Location;enemy.MoveToWorld(new(p.X+1,p.Y,p.Z),owner.Map);Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner);
        Require(ClassCombat.Perform(owner,CombatAbility.Charge,enemy,enemy.Location,Core.TickCount) && ClassCombat.For(owner).PendingAbility==CombatAbility.BasicAttack
            && ClassCombat.For(owner).Charge==null && mana-NecromancerEffects.ManaBalance256(owner)==2304,"Adjacent Charge did not use ordinary attack fallback");Ready(owner);
        enemy.MoveToWorld(new(p.X+5,p.Y,p.Z),owner.Map);Mana(owner);Begin(owner,enemy,enemy.Location);var s=ClassCombat.For(owner);int hp=Life(enemy);
        var weapon=new Dagger();Require(owner.EquipItem(weapon),"Charge swap fixture equip");Require(!ClassCombat.ResolveAction(owner,s.Charge.NextFrame) && s.Charge==null && Life(enemy)==hp,"Weapon swap did not cancel Charge");weapon.Delete();Ready(owner);
    }
    private static void Transitions(PlayerMobile owner,Ogre enemy)
    {
        var p=owner.Location;var map=owner.Map;int hp=Life(enemy);
        foreach(int transition in new[]{0,1,2,3})
        {
            Mana(owner);Begin(owner,enemy,enemy.Location);var s=ClassCombat.For(owner);long frame=s.Charge.NextFrame;
            if(transition==0){owner.Frozen=true;}
            else if(transition==1){owner.Paralyzed=true;}
            else if(transition==2){owner.MoveToWorld(new(p.X,p.Y+1,p.Z),map);}
            else{owner.MoveToWorld(p,Map.Internal);}
            Require(!ClassCombat.ResolveAction(owner,frame) && s.Charge==null && Life(enemy)==hp,"Charge survived invalid owner transition");
            owner.Frozen=owner.Paralyzed=false;owner.MoveToWorld(p,map);Ready(owner);
        }
        // A tracked target may move during travel. Follow its current position, never
        // commit a hit at the original selected point or replay a late frame in a burst.
        Mana(owner);Begin(owner,enemy,enemy.Location);var state=ClassCombat.For(owner);
        enemy.MoveToWorld(new(p.X+4,p.Y,p.Z),map);
        Require(ClassCombat.ResolveAction(owner,state.Charge.NextFrame+4000) && owner.Location==p && Life(enemy)==hp,
            "Late Charge frame teleported, caught up or dealt travel damage");
        while(state.Charge.Phase==PaladinCharge.Phase.Moving){ClassCombat.ResolveAction(owner,state.Charge.NextFrame);}
        Require(owner.X==p.X+3 && Life(enemy)==hp,"Charge failed to track moved target");
        enemy.Delete();Require(!ClassCombat.ResolveAction(owner,state.Charge.NextFrame) && state.Charge.Phase==PaladinCharge.Phase.Recovery,
            "Deleted Charge target produced an impact");
        ClassCombat.ResolveAction(owner,state.Charge.Until);owner.MoveToWorld(p,map);Ready(owner);
    }
    private static void Check()
    {
        Probe();PlayerMobile owner;_creating=true;
        try{owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.Name="ChargeFixture-Paladin";}finally{_creating=false;}
        owner.AddItem(new Backpack());owner.MoveToWorld(Origin(owner),owner.Map);
        for(int i=0;i<40;i++){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength) && ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Charge source weapon stats");}
        var enemy=new Ogre{NoKillAwards=true};enemy.SetHits(30000);enemy.SetResistance(ResistanceType.Physical,50);enemy.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);Stop(enemy);FreezeLife(enemy);
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.Charge) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Charge)
            && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.Charge),"Charge bindings");
        Require(PaladinCharge.VelocityPercent(owner)==100,"Charge inherited a running temporary bonus");
        Gates(owner,enemy);Mana(owner);Sequence(owner,enemy);Mana(owner);Sequence(owner,enemy,1,true);Mana(owner);Sequence(owner,enemy,2);GroundAndBlock(owner,enemy);
        var transient=new Ogre{NoKillAwards=true};transient.SetHits(30000);transient.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);Stop(transient);
        enemy.MoveToWorld(new(owner.X+12,owner.Y,owner.Z),owner.Map);Transitions(owner,transient);enemy.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);
        Mana(owner);Begin(owner,enemy,enemy.Location);owner.Hits=owner.HitsMax-17;SkillHealth.Clear(owner);SkillHealth.Damage(owner,owner,19);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,enemy.Serial.Value,owner.Location,owner.Map.MapID,Life(owner),NecromancerEffects.ManaBalance256(owner),Life(enemy),SkillMechanics.Rank(owner,CombatAbility.Charge))));
    }
    private static void Reload()
    {
        Probe();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var enemy=World.FindMobile((Serial)saved.Enemy) as Ogre;
        Require(owner!=null && enemy!=null && Life(owner)==saved.Life && Life(enemy)==saved.EnemyLife && NecromancerEffects.ManaBalance256(owner)==saved.Mana,
            $"Charge life/mana changed through reload: owner {Life(owner)}/{saved.Life}, enemy {Life(enemy)}/{saved.EnemyLife}, mana {NecromancerEffects.ManaBalance256(owner)}/{saved.Mana}");
        var state=ClassCombat.For(owner);Require(state.Charge==null && state.PendingAbility==CombatAbility.None && SkillMechanics.Rank(owner,CombatAbility.Charge)==saved.Rank,"Charge replayed transient action or lost rank");
        owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);FreezeLife(enemy);Mana(owner);Sequence(owner,enemy,2);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Mana=NecromancerEffects.ManaBalance256(owner),EnemyLife=Life(enemy)}));
    }
}
