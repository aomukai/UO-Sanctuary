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

public static class PaladinOffenseChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(PaladinOffenseChecks));
    private sealed record Saved(uint[] Owners,uint Ally,uint Pet,uint Enemy,Point3D Origin,PaladinAuraSave State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"paladin-offense-checks.json");
    public static void Configure(){Register("paladin-offense-check",Check);Register("paladin-offense-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Concentration damage/recovery and Fanaticism SIAS/AR/caster bonuses",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>
    {try{action();}catch(Exception e){_log.Error(e,"{Name}: failed",name);}});
    private static void Select(PlayerMobile actor,CombatAbility ability)=>Require(SkillBindings.Instance.Assign(actor,false,ability),"Offensive aura selection");
    private static GraveyardSkeleton Enemy(PlayerMobile owner,int dx)
    {
        var actor=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};actor.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();
        actor.SetHits(20000);actor.Hits=actor.HitsMax;actor.SetResistance(ResistanceType.Physical,0);actor.SetResistance(ResistanceType.Fire,0);return actor;
    }
    private static void Lifecycle()
    {
        var actor=ClassSkillChecks.Player(DiabloClass.Paladin);actor.AddItem(new Backpack());Select(actor,CombatAbility.Concentration);actor.Kill();actor.Resurrect();
        Require(PaladinAuras.Effect(actor,CombatAbility.Concentration)==null && PaladinAuras.Selected(actor)==CombatAbility.None,"Death retained Concentration");
        Select(actor,CombatAbility.Fanaticism);ClassCreation.Profiles.TryGet(actor.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(actor,profile);
        Require(PaladinOffense.AttackRate(actor)==0 && PaladinAuras.Selected(actor)==CombatAbility.None,"Respec retained Fanaticism");actor.Corpse?.Delete();actor.Delete();
    }
    private static void Check()
    {
        Lifecycle();var concentration=ClassSkillChecks.Player(DiabloClass.Paladin);concentration.MoveToWorld(ClassSkillChecks.Corridor(concentration,5720,2190),concentration.Map);
        var fanatic=ClassSkillChecks.Player(DiabloClass.Paladin);fanatic.MoveToWorld(new(concentration.X+1,concentration.Y,concentration.Z),concentration.Map);
        var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.MoveToWorld(new(concentration.X+2,concentration.Y,concentration.Z),concentration.Map);ally.RawStr=ally.RawDex=1000;
        var pet=Enemy(concentration,3);Require(pet.SetControlMaster(ally),"Offense pet owner");var enemy=Enemy(concentration,4);
        var sword=new Longsword{Resource=CraftResource.Valorite};var state=ClassGear.Profiles.Convert(sword,99,GearRarity.Normal,new Random(1));
        ClassGear.Profiles.Store(sword,state with{CraftedEquipment=true,MaterialRolls=[new("damage-fire",5)]});Require(ally.EquipItem(sword),"Offense material weapon");
        var before=GearEffects.Damage(ally,.5);int baselineBonus=ClassStats.DamageBonus(ally,sword),rating=ClassStats.AttackRating(ally),petRating=ClassStats.AttackRating(pet);
        TimeSpan petDelay=((BaseWeapon)pet.Weapon).GetDelay(pet);int cast=CombatTiming.For(ally,CombatAbility.Nova).Frames;
        var c20=SkillMechanics.Values(concentration,CombatAbility.Concentration,20);var f20=SkillMechanics.Values(fanatic,CombatAbility.Fanaticism,20);
        Require(c20.Eval("aurastatcalc1")==345 && c20.Eval("aurastatcalc2")==20
            && f20.Eval("aurastatcalc1")==35 && f20.Eval("aurastatcalc2")==135 && f20.Eval("aurastatcalc3")==186 && f20.Eval("passivecalc1")==373,"Pinned rank20 offense values");
        Select(concentration,CombatAbility.Concentration);fanatic.Mana=0;NecromancerEffects.Profiles.Store(fanatic,new());Select(fanatic,CombatAbility.Fanaticism);
        Require(PaladinAuras.Effect(fanatic,CombatAbility.Fanaticism) is{Percent:14,DamagePercent:25,AttackPercent:40}
            && PaladinAuras.Effect(ally,CombatAbility.Fanaticism) is{DamagePercent:25}
            && PaladinAuras.DamagePercent(ally)==85 && PaladinAuras.DamagePercent(fanatic)==85,"Legacy zero-mana self override or allied damage values");
        fanatic.Mana=1;Select(fanatic,CombatAbility.Fanaticism);
        Require(PaladinAuras.Effect(fanatic,CombatAbility.Fanaticism).DamagePercent==50 && PaladinAuras.DamagePercent(fanatic)==110
            && fanatic.Mana==1 && PaladinOffense.HammerBonus(ally)==30,"Positive-mana self override, cost or Hammer primitive");
        var after=GearEffects.Damage(ally,.5);
        Require(ClassStats.DamageBonus(ally,sword)==baselineBonus+85 && after.Physical>before.Physical && after.Fire==before.Fire
            && after.Cold==before.Cold && after.Lightning==before.Lightning,"Offensive physical damage amplified material elements");
        Require(ClassStats.AttackRating(ally)==rating*140/100 && ClassStats.AttackRating(pet)==petRating*140/100,"Fanaticism attack rating consumer");
        Require(CombatTiming.Ias(ally)==0 && CombatTiming.For(ally,CombatAbility.Nova).Frames==cast
            && CombatTiming.Calculate(DiabloClass.Sorceress,CombatAbility.BasicAttack,new(AttackFamily.Unarmed,0),skillAttackRate:14).Frames==14
            && CombatTiming.Calculate(DiabloClass.Sorceress,CombatAbility.BasicAttack,new(AttackFamily.Unarmed,0),ias:14).Frames==15
            && CombatTiming.Calculate(DiabloClass.Sorceress,CombatAbility.BasicAttack,new(AttackFamily.Unarmed,0),ias:10000,skillAttackRate:35).Frames==9,
            "SIAS was treated as item IAS/FCR or missed the rate cap");
        Require(Math.Abs(((BaseWeapon)pet.Weapon).GetDelay(pet).TotalMilliseconds-petDelay.TotalMilliseconds*100/114)<.001,"Native weapon delay lost Fanaticism");
        int hp=enemy.Hits;AOS.Damage(enemy,pet,100,false,100,0,0,0,0,weaponDamage:true);Require(enemy.Hits==hp-185,"Native pet physical damage did not add both auras once");
        hp=enemy.Hits;SkillCombat.Hit(ally,enemy,10,"fire",false);Require(enemy.Hits==hp-13,"Concentration amplified a normal elemental spell");
        var skeleton=new SkeletonGuardian();skeleton.Initialize(concentration,20,5);Require(skeleton.SetControlMaster(concentration),"Offense skeleton owner");skeleton.MoveToWorld(new(enemy.X-1,enemy.Y,enemy.Z),enemy.Map);skeleton.AIObject.AITimer.Stop();
        Select(concentration,CombatAbility.Concentration);Select(fanatic,CombatAbility.Fanaticism);
        Require(skeleton.SkillWeaponDamage==(82,85) && skeleton.BeginMelee(enemy).TotalMilliseconds==640,"Summon enhanced damage or source-frame attack-speed consumer");skeleton.Delete();
        Recovery(ally,enemy,fanatic);
        var stronger=ClassSkillChecks.Player(DiabloClass.Paladin);stronger.MoveToWorld(new(concentration.X+1,concentration.Y+1,concentration.Z),concentration.Map);
        Require(ClassCreation.Profiles.TryTrain(stronger.Serial,"paladin.fanaticism",out _),"Stronger Fanaticism fixture");Select(stronger,CombatAbility.Fanaticism);
        var lease=PaladinAuras.Effect(ally,CombatAbility.Fanaticism);Require(lease is{Rank:2,Percent:18,DamagePercent:33,AttackPercent:45},"Higher Fanaticism stat packet");
        Select(fanatic,CombatAbility.Fanaticism);Require(PaladinAuras.Effect(ally,CombatAbility.Fanaticism)==lease,"Weaker Fanaticism extended or stacked stronger state");Select(stronger,CombatAbility.None);stronger.Delete();
        Later(2300,"paladin-offense-result",()=>
        {
            Require(PaladinAuras.Effect(ally,CombatAbility.Fanaticism)==null,"Weaker pulse extended stronger aura lease");
            Select(fanatic,CombatAbility.Fanaticism);Select(concentration,CombatAbility.Concentration);Seed([concentration,fanatic],ally,pet,enemy);
            _log.Information("paladin-offense-result: passed");
        });
    }
    private static void Recovery(PlayerMobile ally,Mobile enemy,PlayerMobile fanatic)
    {
        // Each reload measures one nonlethal hit; reset fixture life rather than accumulating three half-life hits.
        ally.Hits=ally.HitsMax;
        var saved=PaladinAuras.Snapshot();
        PaladinAuras.Restore(saved with{Leases=saved.Leases.Where(x=>x.Target!=ally.Serial.Value || x.Ability!=CombatAbility.Fanaticism).ToArray()});
        var session=ClassCombat.For(ally);session.NextAction=session.RecoveryUntil=Core.TickCount;session.PendingAbility=CombatAbility.None;
        Require(ClassCombat.Perform(ally,CombatAbility.BasicAttack,enemy,enemy.Location,Core.TickCount),"Real pending attack fixture");long deadline=session.NextAction,effect=session.EffectAt;
        Select(fanatic,CombatAbility.Fanaticism);
        Require(session.NextAction==deadline && session.EffectAt==effect,"Aura activation retimed a committed action");
        var roll=PaladinOffense.RollPercent;
        try
        {
            PaladinOffense.RollPercent=()=>19;
            int hp=ally.Hits;AOS.Damage(ally,enemy,Math.Max(1,ally.HitsMax/2),false,100,0,0,0,0);
            Require(ally.Hits<hp && !HitRecovery.Active(ally,Core.TickCount) && session.PendingAbility==CombatAbility.BasicAttack
                && session.NextAction==deadline && session.EffectAt==effect,"Concentration19/20 guard canceled or retimed committed action");
            session.PendingAbility=CombatAbility.None;
            Require(!HitRecovery.Apply(ally,ally.HitsMax,Core.TickCount),"Concentration lost active post-impact animation protection");
            PaladinOffense.RollPercent=()=>20;Require(HitRecovery.Apply(ally,ally.HitsMax,Core.TickCount) && session.NextAction==deadline,"Concentration20 boundary prevented recovery or refunded deadline");
            session.NextAction=session.RecoveryUntil=Core.TickCount;PaladinOffense.RollPercent=()=>0;
            Require(HitRecovery.Apply(ally,ally.HitsMax,Core.TickCount),"Neutral actor received Concentration recovery immunity");
        }
        finally{PaladinOffense.RollPercent=roll;session.RecoveryUntil=Core.TickCount;session.NextAction=Core.TickCount;session.PendingAbility=CombatAbility.None;}
    }
    private static void Seed(PlayerMobile[] owners,PlayerMobile ally,BaseCreature pet,BaseCreature enemy)
    {
        // Snapshot inspection hold only; ordinary duration/pulse checks remain separate.
        uint[] ids=owners.Select(x=>x.Serial.Value).ToArray();var state=PaladinAuras.Snapshot();var at=TimedEffects.Now.Add(FixtureSpace.Retention);
        state=state with{Emitters=state.Emitters.Select(x=>ids.Contains(x.Owner)?x with{NextUtc=at}:x).ToArray(),Leases=state.Leases.Select(x=>ids.Contains(x.Source)?x with{UntilUtc=at.AddMilliseconds(40)}:x).ToArray()};
        PaladinAuras.Restore(state);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(ids,ally.Serial.Value,pet.Serial.Value,enemy.Serial.Value,owners[0].Location,state)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var state=PaladinAuras.Snapshot();
        PaladinAuraSave Scope(PaladinAuraSave x)=>new(x.Emitters.Where(x=>saved.Owners.Contains(x.Owner)).ToArray(),x.Leases.Where(x=>saved.Owners.Contains(x.Source)).ToArray());
        Require(JsonSerializer.Serialize(Scope(state))==JsonSerializer.Serialize(Scope(saved.State)),"Saved offense rank/source/stat/deadline changed");
        var owners=saved.Owners.Select(x=>(PlayerMobile)World.FindMobile((Serial)x)).ToArray();var ally=(PlayerMobile)World.FindMobile((Serial)saved.Ally);var pet=(BaseCreature)World.FindMobile((Serial)saved.Pet);var enemy=(BaseCreature)World.FindMobile((Serial)saved.Enemy);
        for(int i=0;i<owners.Length;i++){owners[i].MoveToWorld(new(saved.Origin.X+i,saved.Origin.Y,saved.Origin.Z),Map.Trammel);}
        ally.MoveToWorld(new(saved.Origin.X+2,saved.Origin.Y,saved.Origin.Z),Map.Trammel);ally.ClaimAutoStabledPets();Require(pet.Controlled && pet.ControlMaster==ally,"Offense native pet claim");
        pet.MoveToWorld(new(saved.Origin.X+3,saved.Origin.Y,saved.Origin.Z),Map.Trammel);enemy.MoveToWorld(new(saved.Origin.X+4,saved.Origin.Y,saved.Origin.Z),Map.Trammel);enemy.SetResistance(ResistanceType.Physical,0);
        PaladinAuras.Clear(ally);PaladinAuras.Clear(pet);var now=TimedEffects.Now;
        PaladinAuras.Restore(PaladinAuras.Snapshot() with{Emitters=state.Emitters.Select(x=>saved.Owners.Contains(x.Owner)?x with{NextUtc=now}:x).ToArray()});
        Later(120,"paladin-offense-reload-result",()=>
        {
            Require(PaladinAuras.DamagePercent(ally)==85 && PaladinOffense.AttackRate(ally)==14 && PaladinAuras.Effect(ally,CombatAbility.Concentration) is{UninterruptChance:20}
                && PaladinAuras.Effect(ally,CombatAbility.Fanaticism) is{DamagePercent:25,AttackPercent:40} lease && lease.UntilUtc<now.AddSeconds(3),"Restored offense sources failed to resume fresh packets");
            int hp=enemy.Hits;AOS.Damage(enemy,pet,100,false,100,0,0,0,0,weaponDamage:true);Require(enemy.Hits==hp-185,"Restored native physical damage");
            Require(CombatTiming.Calculate(DiabloClass.Sorceress,CombatAbility.BasicAttack,new(AttackFamily.Unarmed,0),skillAttackRate:PaladinOffense.AttackRate(ally)).Frames==14,"Restored skill attack-rate consumer");
            Recovery(ally,enemy,owners[1]);PaladinAuras.Restore(state);_log.Information("paladin-offense-reload-result: passed");
        });
    }
}
