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

public static class SanctuaryAuraChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(SanctuaryAuraChecks));
    private sealed record Saved(uint Owner,uint Target,Point3D Origin,PaladinAuraSave Aura,SourceKnockbackState Motion);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sanctuary-aura-checks.json");
    public static void Configure(){Register("sanctuary-aura-check",Check);Register("sanctuary-aura-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Sanctuary undead supplier, pulse, native knockback and persistence",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>
        {try{action();}catch(Exception e){_log.Error(e,"{Name}: failed",name);}});
    private static SkillValues Values(int rank,int hard=0)=>new(SkillMechanics.For(CombatAbility.Sanctuary),k=>k=="Sanctuary"?rank:40,k=>k=="Sanctuary"?rank:hard,99);
    private static Point3D Place(PlayerMobile owner)
    {
        for(int x=2400;x<=3500;x+=50)for(int y=1000;y<=1300;y+=50)
        {
            Point3D p;try{p=ClassSkillChecks.Corridor(owner,x,y);}catch(InvalidOperationException){continue;}
            bool clear=true;foreach(var actor in owner.Map.GetMobilesInRange<Mobile>(p,20)){if(actor!=owner){clear=false;break;}}
            if(clear && Server.Spells.SpellHelper.CheckTown(p,owner)){return p;}
        }
        throw new InvalidOperationException("No independent Sanctuary corridor");
    }
    private static GraveyardSkeleton Enemy(PlayerMobile owner,int dx,int dy=0)
    {
        var enemy=new GraveyardSkeleton{NoKillAwards=true};enemy.MoveToWorld(new(owner.X+dx,owner.Y+dy,owner.Z),owner.Map);
        enemy.AIObject.AITimer.Stop();enemy.SetHits(20000);enemy.Hits=enemy.HitsMax;enemy.Poison=Poison.Lesser;enemy.SetResistance(ResistanceType.Physical,0);
        return enemy;
    }
    private static void Check()
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.AddItem(new Backpack());owner.RawStr=owner.RawDex=1000;owner.MoveToWorld(Place(owner),owner.Map);
        var one=Values(1);var twenty=Values(20,20);
        Require(one.Mana256==256 && one.Damage256(false)==2048 && one.Damage256(true)==4096 && one.Eval("passivecalc1")==150
            && one.Eval("passivecalc2")==100 && twenty.Eval("passivecalc1")==720 && twenty.Eval("passivecalc2")==1050
            && twenty.Damage256(false)==54067 && twenty.Damage256(true)==66355,"Pinned Sanctuary goldens");
        Require(SourceKnockbacks.Offset(1,0,5)==(1,0,98304) && SourceKnockbacks.Offset(-1,0,5)==(-1,0,98304)
            && SourceKnockbacks.Offset(1,1,5)==(1,1,139022),"Explicit source-subtile/native endpoint projection");
        var target=Enemy(owner,1);var far=Enemy(owner,5);var beast=new Ogre{Frozen=true};beast.MoveToWorld(new(owner.X,owner.Y+1,owner.Z),owner.Map);beast.AIObject.AITimer.Stop();
        var immune=new LichLord{Frozen=true};MonsterVariants.Assign(immune,MonsterDifficulty.Hell);immune.MoveToWorld(new(owner.X-1,owner.Y,owner.Z),owner.Map);immune.AIObject.AITimer.Stop();immune.Poison=Poison.Lesser;
        Require(GemCombat.Undead(immune) && NamedElements.MagicResistance(immune)==100 && SourceKnockbacks.Rule(immune)==null,"Magic-immune/unmapped control fixture");
        var ally=ClassSkillChecks.Player(DiabloClass.Necromancer);ally.MoveToWorld(new(owner.X,owner.Y-1,owner.Z),owner.Map);
        var pet=Enemy(owner,0,-2);Require(pet.SetControlMaster(ally),"Sanctuary pet ownership");
        int hp=target.Hits,ihp=immune.Hits,bhp=beast.Hits,fhp=far.Hits,ph=pet.Hits;var start=target.Location;
        var roll=PaladinSanctuary.Roll;PaladinSanctuary.Roll=_=>0;
        Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.Sanctuary) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Sanctuary),"Sanctuary right-only authority");
        Require(!PaladinSanctuary.Active(owner) && hp==target.Hits && target.Location==start,"Sanctuary incorrectly immediate");
        Later(2300,"sanctuary-aura-result",()=>
        {
            try
            {
                Require(target.Hits<hp && target.X==start.X+1 && target.Y==start.Y && SourceKnockbacks.Remaining(target)>0,$"Natural pulse/motion/GH: loss={hp-target.Hits}, position={target.Location}, start={start}, remaining={SourceKnockbacks.Remaining(target)}, state={JsonSerializer.Serialize(SourceKnockbacks.Snapshot())}");
                Require(immune.Hits==ihp && beast.Hits==bhp && far.Hits==fhp && pet.Hits==ph && SourceKnockbacks.Remaining(immune)==0
                    && !PaladinSanctuary.Active(ally) && !PaladinSanctuary.Active(pet),"Undead/immune/range/friendly/supplier-only or unknown-control gates");
                Require(!target.Move(Direction.East),"Ordinary movement bypassed knockback/GH lock");
                Require(SkillBindings.Instance.Assign(owner,false,CombatAbility.None),"Sanctuary stop");
                Supplier(owner,target,beast);ZeroMana(owner,target,one);
                Later(1200,"sanctuary-aura-result",()=>
                {
                    Require(SourceKnockbacks.Remaining(target)==0,"Knockback recovery failed to release");
                    Terrain(owner,target);
                    Later(1200,"sanctuary-aura-result",()=>
                    {
                        Require(SourceKnockbacks.Remaining(target)==0,"Blocked knockback retained lock");
                        ChargeKnockback(owner,target);Seed(owner,target);_log.Information("sanctuary-aura-result: passed");
                    });
                });
            }
            finally{PaladinSanctuary.Roll=roll;}
        });
    }
    private static void Supplier(PlayerMobile owner,BaseCreature target,BaseCreature beast)
    {
        owner.Mana=owner.ManaMax;var baseline=GearEffects.Damage(owner,.5,target);int before=ClassStats.AttackRatingAgainst(owner,target,100);
        PaladinSanctuary.Pulse(owner,Values(1),TimedEffects.Now);
        Require(PaladinSanctuary.DamagePercent(owner,target)==150 && PaladinSanctuary.AttackFlat(owner,target)==100
            && PaladinSanctuary.DamagePercent(owner,beast)==0 && GearEffects.Damage(owner,.5,target).Physical>baseline.Physical,"Undead-only supplier damage/AR packet");
        int delta=ClassStats.AttackRatingAgainst(owner,target,100)-before;
        // Fixture has one hard Blessed Aim point:100base +100skill +5passive percent.
        Require(delta==205,"Flat undead AR must precede skill/passive percentages; delta="+delta);
        target.SetResistance(ResistanceType.Physical,100);int hp=target.Hits;SkillHealth.Clear(target);
        Require(SkillCombat.Hit(owner,target,100,"physical")==100 && target.Hits==hp-100,"Class physical immunity clamp");
        hp=target.Hits;AOS.Damage(target,owner,100,false,100,0,0,0,0,weaponDamage:false);Require(target.Hits==hp-100,"Native physical immunity clamp");
        target.SetResistance(ResistanceType.Physical,-50);Require(NamedElements.Resistance(owner,target,"physical",-50)==-50,"Sanctuary erased negative physical resistance");
        Require(PaladinSanctuary.PhysicalResistance(owner,beast,100)==100,"Sanctuary physical clamp leaked to beast");
        Require(SkillBindings.Instance.Assign(owner,false,CombatAbility.Might),"Sanctuary supplier replacement");
        // Direct helper pulse did not select the aura, so explicitly remove its supplier lease.
        PaladinAuras.Clear(owner);Require(!PaladinSanctuary.Active(owner),"Supplier clear retained Sanctuary");
    }
    private static void ZeroMana(PlayerMobile owner,BaseCreature target,SkillValues values)
    {
        SourceKnockbacks.Clear(target);target.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);target.SetResistance(ResistanceType.Physical,0);
        owner.Mana=0;int hp=target.Hits;PaladinSanctuary.Pulse(owner,values,TimedEffects.Now);
        Require(owner.Mana==0 && PaladinSanctuary.Active(owner) && PaladinSanctuary.DamagePercent(owner,target)==0
            && PaladinSanctuary.AttackFlat(owner,target)==0 && target.Hits<hp && SourceKnockbacks.Remaining(target)>0,"Zero-mana pulse/knockback or provisional supplier threshold");
        PaladinAuras.Clear(owner); // Stop future repeats while observing the committed impulse.
    }
    private static void Terrain(PlayerMobile owner,BaseCreature target)
    {
        target.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);var wall=new Blocker();wall.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);
        var position=target.Location;Require(SourceKnockbacks.Begin(owner,target),"Blocked-path knockback start");
        Later(200,"sanctuary-aura-result",()=>{try{Require(target.Location==position && SourceKnockbacks.Remaining(target)>0,"Knockback crossed native obstruction or skipped GH");}finally{wall.Delete();}});
    }
    private static void ChargeKnockback(PlayerMobile owner,BaseCreature target)
    {
        SourceKnockbacks.Clear(target);target.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);owner.Mana=owner.ManaMax;
        var state=ClassCombat.For(owner);ClassCombat.Stop(owner);state.NextAction=state.RecoveryUntil=Core.TickCount;
        var roll=PaladinCharge.HitRoll;PaladinCharge.HitRoll=()=>0;
        try
        {
            Require(ClassCombat.Perform(owner,CombatAbility.Charge,target,target.Location,Core.TickCount),"Charge knockback fixture start");
            int frames=0;while(state.Charge is{Phase:PaladinCharge.Phase.Moving} flight)
            {ClassCombat.ResolveAction(owner,flight.NextFrame);Require(++frames<30,"Charge knockback fixture travel");}
            Require(state.Charge is{Phase:PaladinCharge.Phase.Impact},"Charge knockback fixture arrival");
            ClassCombat.ResolveAction(owner,state.Charge.NextFrame);
            Require(SourceKnockbacks.Remaining(target)>0,"Real Charge impact did not request mapped source knockback");
        }
        finally{PaladinCharge.HitRoll=roll;PaladinCharge.Cancel(owner);}
    }
    private static void Seed(PlayerMobile owner,BaseCreature target)
    {
        target.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);owner.Mana=owner.ManaMax;Require(SkillBindings.Instance.Assign(owner,false,CombatAbility.Sanctuary),"Seed Sanctuary selection");
        PaladinSanctuary.Pulse(owner,Values(1),TimedEffects.Now);var deadline=TimedEffects.Now.AddMinutes(10);
        var motion=SourceKnockbacks.Snapshot().Single(x=>x.Target==target.Serial.Value) with{NextUtc=deadline};
        SourceKnockbacks.Restore(SourceKnockbacks.Snapshot().Select(x=>x.Target==target.Serial.Value?motion:x).ToArray());
        var aura=PaladinAuras.Snapshot();aura=aura with{Emitters=aura.Emitters.Select(x=>x.Owner==owner.Serial.Value?x with{NextUtc=deadline}:x).ToArray(),
            Leases=aura.Leases.Select(x=>x.Source==owner.Serial.Value?x with{UntilUtc=deadline}:x).ToArray()};PaladinAuras.Restore(aura);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,target.Serial.Value,owner.Location,aura,motion)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var target=(BaseCreature)World.FindMobile((Serial)saved.Target);owner.MoveToWorld(saved.Origin,Map.Trammel);
        Require(SourceKnockbacks.Snapshot().Single(x=>x.Target==saved.Target)==saved.Motion && target.Location==saved.Motion.Position,"Saved knockback position/phase/budget/deadline changed");
        Require(PaladinSanctuary.DamagePercent(owner,target)==150 && PaladinSanctuary.AttackFlat(owner,target)==100,"Saved supplier stats lost");
        SourceKnockbacks.Restore(SourceKnockbacks.Snapshot().Select(x=>x.Target==saved.Target?x with{NextUtc=TimedEffects.Now}:x).ToArray());
        Later(200,"sanctuary-aura-reload-result",()=>
        {
            Require(target.Location.X==saved.Motion.Position.X+1 && SourceKnockbacks.Remaining(target)>0,"Saved accepted knockback failed to resume");
            Later(1000,"sanctuary-aura-reload-result",()=>
            {
                Require(SourceKnockbacks.Remaining(target)==0,"Resumed knockback/GH retained movement lock");
                target.MoveToWorld(saved.Motion.Position,owner.Map);SourceKnockbacks.Restore(SourceKnockbacks.Snapshot().Append(saved.Motion).ToArray());
                // Aura leases were not mutated here; preserve other fixtures added after the initial seed.
                _log.Information("sanctuary-aura-reload-result: passed");
            });
        });
    }
}
