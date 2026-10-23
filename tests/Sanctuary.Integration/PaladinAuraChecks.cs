using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class PaladinAuraChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(PaladinAuraChecks));
    private sealed record Saved(uint[] Owners,uint Ally,uint Enemy,Point3D Origin,PaladinAuraSave State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"paladin-aura-checks.json");
    public static void Configure(){Register("paladin-aura-check",Check);Register("paladin-aura-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Selected Paladin auras, stat consumers, stacking and saved refresh",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int milliseconds,string result,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(milliseconds),()=>
    {try{action();}catch(Exception e){_log.Error(e,"{Name}: failed",result);}});
    private static GraveyardSkeleton Enemy(PlayerMobile owner,int x)
    {
        var enemy=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(new(owner.X+x,owner.Y,owner.Z),owner.Map);
        enemy.AIObject.AITimer.Stop();enemy.SetHits(20000);enemy.Hits=enemy.HitsMax;enemy.SetResistance(ResistanceType.Physical,0);return enemy;
    }
    private static void Select(PlayerMobile owner,CombatAbility ability)=>Require(SkillBindings.Instance.Assign(owner,false,ability),"Right aura assignment failed");
    private static void Lifecycle()
    {
        var actor=ClassSkillChecks.Player(DiabloClass.Paladin);actor.AddItem(new Backpack());
        try
        {
            Select(actor,CombatAbility.Might);Require(PaladinAuras.DamagePercent(actor)==40,"Lifecycle aura fixture");
            actor.Kill();actor.Resurrect();
            Require(PaladinAuras.Selected(actor)==CombatAbility.None && PaladinAuras.DamagePercent(actor)==0,"Death retained an aura emitter or self lease");
            Select(actor,CombatAbility.Defiance);ClassCreation.Profiles.TryGet(actor.Serial,out var profile);
            ClassCreation.Profiles.ResetAllocatedPoints(actor,profile);
            Require(PaladinAuras.Selected(actor)==CombatAbility.None && PaladinAuras.DefensePercent(actor)==0 && PaladinAuras.AttackPercent(actor)==0,"Respec retained emitter/stat/passive");
        }
        finally{actor.Corpse?.Delete();actor.Delete();}
    }
    private static void Check()
    {
        Lifecycle();
        var owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.MoveToWorld(ClassSkillChecks.Corridor(owner,5720,1810),owner.Map);
        var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.RawStr=1000;ally.RawDex=400;ally.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);
        var outside=ClassSkillChecks.Player();outside.MoveToWorld(new(owner.X+9,owner.Y,owner.Z),owner.Map);
        var necro=ClassSkillChecks.Player();necro.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);
        var skeleton=new SkeletonGuardian();skeleton.Initialize(necro,20,5);Require(skeleton.SetControlMaster(necro),"Aura skeleton ownership");skeleton.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);skeleton.Frozen=true;skeleton.AIObject.AITimer.Stop();
        var golem=new NecromancerMinion();golem.Prepare(necro,CombatAbility.ClayGolem);Require(golem.SetControlMaster(necro),"Aura golem ownership");golem.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);golem.Frozen=true;golem.AIObject.AITimer.Stop();
        var barrier=BoneBarrierSegment.Spawn(necro,CombatAbility.BoneWall,new(owner.X+6,owner.Y,owner.Z),SkillMechanics.Values(necro,CombatAbility.BoneWall),null);
        var pet=Enemy(owner,4);pet.ControlMaster=necro;pet.Controlled=true;var enemy=Enemy(owner,5);
        int petDefense=ClassStats.Defense(pet),petRating=ClassStats.AttackRating(pet);
        var sword=new Longsword{Resource=CraftResource.Valorite};var state=ClassGear.Profiles.Convert(sword,99,GearRarity.Normal,new Random(1));
        ClassGear.Profiles.Store(sword,state with{CraftedEquipment=true,MaterialRolls=[new("damage-fire",5)]});Require(ally.EquipItem(sword),"Aura weapon fixture");
        Require(SkillMechanics.Values(owner,CombatAbility.Might).Mana256==0,"Aura cost display inherited ordinary minmana");
        int bonus=ClassStats.DamageBonus(ally,sword);var before=GearEffects.Damage(ally,.5);Require(before.Fire>0,"Nonzero material element fixture");
        owner.Mana=0;var session=ClassCombat.For(owner);session.NextAction=Core.TickCount+10000;long nextAction=session.NextAction;
        Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.Might),"Right-only aura accepted in left slot");
        Require(ClassCombat.Perform(owner,CombatAbility.Might,null,owner.Location,Core.TickCount),"Aura quick cast selection");
        Require(owner.Mana==0 && session.PendingAbility==CombatAbility.None && session.NextAction==nextAction
            && SkillBindings.Instance.Get(owner).Right==CombatAbility.Might && ClassCombat.ActionInterval(owner,CombatAbility.Might)==0,
            "Aura charged mana, animated or consumed an action cycle");
        var aura=PaladinAuras.Effect(ally,CombatAbility.Might);var after=GearEffects.Damage(ally,.5);
        Require(aura is{Rank:1,Percent:40} && Math.Abs((aura.UntilUtc-TimedEffects.Now).TotalMilliseconds-2040)<100
            && PaladinAuras.DamagePercent(owner)==40 && PaladinAuras.DamagePercent(outside)==0 && PaladinAuras.DamagePercent(enemy)==0,
            "Source self/ally radius, hostile exclusion, rank or lease lifetime");
        Require(!ClassCombat.ContinueCast(owner,CombatAbility.Defiance,null,owner.Location,Core.TickCount)
            && ClassCombat.ContinueCast(owner,CombatAbility.Might,null,owner.Location,Core.TickCount)
            && PaladinAuras.Effect(ally,CombatAbility.Might)==aura,"Held input changed an unbound aura or refreshed its lease");
        Require(ClassStats.DamageBonus(ally,sword)==bonus+40 && after.Physical>before.Physical && after.Fire==before.Fire
            && after.Cold==before.Cold && after.Lightning==before.Lightning && after.Poison==before.Poison,"Might amplified an elemental/material packet");
        Require(skeleton.CombatProfile.MinDamage==59 && skeleton.SkillWeaponDamage==(69,72),"Might did not add to skeleton enhanced damage");
        int percent=(int)SkillMechanics.Values(necro,CombatAbility.ClayGolem).Eval("passivecalc3");
        Require(golem.SkillWeaponDamage.Min==Math.Max(1,NecromancerSummons.Source("ClayGolem","A1MinD")*(140+percent)/100),"Golem additive aura damage");
        int life=enemy.Hits;AOS.Damage(enemy,pet,100,false,100,0,0,0,0,weaponDamage:true);
        Require(enemy.Hits==life-140,"Native controlled weapon attack lost Might");
        life=enemy.Hits;SkillCombat.HitParts(skeleton,enemy,new(skeleton.SkillWeaponDamage.Min,0,0,0,0),false,weapon:true);
        Require(enemy.Hits==life-69,"Custom skeleton attack lost or double-applied Might");
        SorceressEffects.CastArmor(ally,CombatAbility.FrozenArmor,SkillMechanics.Values(ally,CombatAbility.FrozenArmor));
        Select(owner,CombatAbility.Defiance);
        Require(PaladinAuras.DamagePercent(owner)==0 && PaladinAuras.DamagePercent(ally)==40 && PaladinAuras.DefensePercent(ally)==70
            && ClassStats.Defense(ally)==200 && ClassStats.Defense(pet)==petDefense*170/100 && PaladinAuras.DefensePercent(barrier)==0,"Immediate self replacement, recipient linger or additive armor defense");
        ally.MoveToWorld(new(owner.X+20,owner.Y,owner.Z),owner.Map);
        Later(300,"paladin-aura-result",()=>Require(PaladinAuras.DefensePercent(ally)==70 && PaladinAuras.DamagePercent(ally)==40,"Aura vanished immediately on range exit"));
        Later(2250,"paladin-aura-result",()=>
        {
            Require(PaladinAuras.DefensePercent(ally)==0 && PaladinAuras.DamagePercent(ally)==0,"Out-of-range recipient lease was refreshed");
            outside.Delete();skeleton.Delete();golem.Delete();barrier.Delete();
            ally.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);int allyRating=ClassStats.AttackRating(ally);Select(owner,CombatAbility.BlessedAim);
            Require(PaladinAuras.AttackPercent(owner)==75 && PaladinAuras.AttackPercent(ally)==75 && ClassStats.AttackRating(ally)==allyRating*175/100
                && ClassStats.AttackRating(pet)==petRating*175/100,"Blessed Aim stacked its five-percent passive");
            Require(ClassCreation.Profiles.TryTrain(owner.Serial,"paladin.blessedaim",out _),"Blessed Aim hard point fixture");
            Require(PaladinAuras.AttackPercent(owner)==75,"Aura rank changed before its periodic refresh");
            Later(2150,"paladin-aura-result",()=>
            {
                Require(PaladinAuras.AttackPercent(owner)==90 && PaladinAuras.AttackPercent(ally)==90,"Periodic current-rank refresh failed");
                pet.Delete();necro.Delete();
                Select(owner,CombatAbility.Might);Require(PaladinAuras.AttackPercent(owner)==10,"Inactive Blessed Aim passive did not use allocated ranks");
                Stacking(owner,ally,enemy);
            });
        });
    }
    private static void Stacking(PlayerMobile owner,PlayerMobile ally,GraveyardSkeleton enemy)
    {
        var stronger=ClassSkillChecks.Player(DiabloClass.Paladin);stronger.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        var equal=PaladinAuras.Effect(ally,CombatAbility.Might);Select(stronger,CombatAbility.Might);
        Require(PaladinAuras.Effect(ally,CombatAbility.Might) is{Rank:1,Percent:40} refreshed && refreshed.Source==equal.Source && refreshed.UntilUtc>=equal.UntilUtc,"Equal aura summed or changed source identity");
        Require(ClassCreation.Profiles.TryTrain(stronger.Serial,"paladin.might",out _),"Stronger Might rank");Select(stronger,CombatAbility.Might);
        var lease=PaladinAuras.Effect(ally,CombatAbility.Might);Require(lease is{Rank:2,Percent:50},"Higher same aura did not replace");
        Select(owner,CombatAbility.Might);Require(PaladinAuras.Effect(ally,CombatAbility.Might)==lease,"Weaker aura refreshed or summed with stronger");
        Select(stronger,CombatAbility.None);
        Later(2250,"paladin-aura-result",()=>
        {
            Require(PaladinAuras.Effect(ally,CombatAbility.Might)==null,"Weaker pulse extended stronger aura lease");
            Select(owner,CombatAbility.Might);Require(PaladinAuras.DamagePercent(ally)==40,"Expired higher aura rejected weaker reapplication");
            Seed(owner,stronger,ally,enemy);_log.Information("paladin-aura-result: passed");
        });
    }
    private static void Seed(PlayerMobile owner,PlayerMobile defender,PlayerMobile ally,GraveyardSkeleton enemy)
    {
        var aim=ClassSkillChecks.Player(DiabloClass.Paladin);aim.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);
        SorceressEffects.Profiles.Clear(ally);PaladinAuras.Clear(ally);
        Select(owner,CombatAbility.Might);Select(defender,CombatAbility.Defiance);Select(aim,CombatAbility.BlessedAim);
        Require(PaladinAuras.DamagePercent(ally)==40 && PaladinAuras.DefensePercent(ally)==70 && PaladinAuras.AttackPercent(ally)==75,"Independent allied aura combination");
        var snapshot=PaladinAuras.Snapshot();var at=TimedEffects.Now.AddMinutes(10);uint[] owners=[owner.Serial.Value,defender.Serial.Value,aim.Serial.Value];
        // Hold only test deadlines across the growing regression suite; source periods/lifetimes were checked above.
        snapshot=snapshot with{Emitters=snapshot.Emitters.Select(x=>owners.Contains(x.Owner)?x with{NextUtc=at}:x).ToArray(),
            Leases=snapshot.Leases.Select(x=>owners.Contains(x.Source)?x with{UntilUtc=at.AddMilliseconds(40)}:x).ToArray()};
        PaladinAuras.Restore(snapshot);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owners,ally.Serial.Value,enemy.Serial.Value,owner.Location,snapshot)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var snapshot=PaladinAuras.Snapshot();
        PaladinAuraSave Scope(PaladinAuraSave state)=>new(state.Emitters.Where(x=>saved.Owners.Contains(x.Owner)).ToArray(),state.Leases.Where(x=>saved.Owners.Contains(x.Source)).ToArray());
        Require(JsonSerializer.Serialize(Scope(snapshot))==JsonSerializer.Serialize(Scope(saved.State)),"Saved emitter selection, rank, stat lease or absolute deadline changed");
        var owners=saved.Owners.Select(x=>World.FindMobile((Serial)x) as PlayerMobile).ToArray();var ally=World.FindMobile((Serial)saved.Ally) as PlayerMobile;
        Require(owners.All(x=>x is{Alive:true}) && ally is{Alive:true},"Saved aura players missing");
        for(int i=0;i<owners.Length;i++){owners[i].MoveToWorld(new(saved.Origin.X+i,saved.Origin.Y,saved.Origin.Z),Map.Trammel);}
        ally.MoveToWorld(new(saved.Origin.X+2,saved.Origin.Y,saved.Origin.Z),Map.Trammel);ally.RawDex=400;
        PaladinAuras.Clear(ally);var at=TimedEffects.Now;
        PaladinAuras.Restore(PaladinAuras.Snapshot() with{Emitters=snapshot.Emitters.Select(x=>saved.Owners.Contains(x.Owner)?x with{NextUtc=at}:x).ToArray()});
        Later(120,"paladin-aura-reload-result",()=>
        {
            Require(PaladinAuras.DamagePercent(ally)==40 && PaladinAuras.DefensePercent(ally)==70 && PaladinAuras.AttackPercent(ally)==75
                && ClassStats.Defense(ally)==170 && PaladinAuras.Effect(ally,CombatAbility.Might).UntilUtc<at.AddSeconds(3),"Saved selected auras failed to resume actual short-lived stat applications");
            PaladinAuras.Restore(snapshot);_log.Information("paladin-aura-reload-result: passed");
        });
    }
}
