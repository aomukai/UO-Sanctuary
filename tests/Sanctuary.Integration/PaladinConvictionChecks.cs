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

public static class PaladinConvictionChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(PaladinConvictionChecks));
    private sealed record Saved(uint Owner,uint Ally,uint Pet,uint Enemy,Point3D Origin,PaladinAuraSave Auras,ConvictionLease[] Hostile);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"paladin-conviction-checks.json");
    public static void Configure(){Register("paladin-conviction-check",Check);Register("paladin-conviction-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Conviction hostile leases, base immunity, defense, priority and native reload checks",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>
    {try{action();}catch(Exception e){_log.Error(e,"{Name}: failed",name);}});
    private static void FreezeRecovery(Mobile actor)=>TimedEffects.Poison(actor,actor,.000001,60000,TimedEffects.Now);
    private static void Resist(BaseCreature actor)
    {actor.SetResistance(ResistanceType.Physical,0);actor.SetResistance(ResistanceType.Fire,100);actor.SetResistance(ResistanceType.Cold,80);actor.SetResistance(ResistanceType.Energy,130);actor.SetResistance(ResistanceType.Poison,100);}
    private static T Place<T>(PlayerMobile owner,T actor,int dx) where T:BaseCreature
    {
        actor.NoKillAwards=actor.Frozen=actor.CantWalk=true;actor.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();
        actor.SetHits(20000);actor.Hits=actor.HitsMax;Resist(actor);FreezeRecovery(actor);return actor;
    }
    private static int Resistance(PlayerMobile owner,Mobile target,string element,int baseline,int? pierce=null)=>NamedElements.Resistance(owner,target,element,baseline,pierce);
    private static void Copies(PaladinAuraSave state,ConvictionLease[] hostile,uint owner)
    {
        var current=PaladinAuras.Snapshot();
        PaladinAuras.Restore(new(current.Emitters.Where(x=>x.Owner!=owner).Concat(state.Emitters).ToArray(),
            current.Leases.Where(x=>x.Source!=owner || x.Ability!=CombatAbility.Conviction).Concat(state.Leases).ToArray()));
        PaladinConviction.Restore(PaladinConviction.Snapshot().Where(x=>x.Source!=owner).Concat(hostile).ToArray());
    }
    private static void Cases(PlayerMobile owner,BaseCreature enemy)
    {
        Require(PaladinConviction.Effect(enemy) is{Rank:20,DefensePercent:90,Fire:-25,Cold:-125,Lightning:-25},"Source stat snapshots from original immunity");
        Require(Resistance(owner,enemy,"fire",enemy.FireResistance)==75 && Resistance(owner,enemy,"cold",enemy.ColdResistance)==-45
            && Resistance(owner,enemy,"ltng",enemy.EnergyResistance,500)==105 && Resistance(owner,enemy,"pois",enemy.PoisonResistance)==100
            && Resistance(owner,enemy,"mag",0)==0 && Resistance(owner,enemy,"physical",0)==0,"Conviction element exclusions/immunity/pierce");
        int hp=enemy.Hits;AOS.Damage(enemy,owner,100,false,0,100,0,0,0,spellDamage:true);
        Require(enemy.Hits==hp-25,"Native resisted damage ignored hostile aura");
        int basis=CreatureProfiles.Defense(enemy);
        Require(ClassStats.Defense(enemy)==ClassStats.ScaleDefense(basis,-90) && ClassStats.ScaleDefense(1,-49)==1
            && ClassStats.ScaleDefense(17,-90)==2,"Defense signed division or native/class defense consumer");
        var ordinary=PaladinAuras.Snapshot();PaladinAuras.ElementalWeapon(new(enemy.Serial.Value,owner.Serial.Value,CombatAbility.Defiance,1,100,TimedEffects.Now.AddMinutes(1)));
        Require(ClassStats.Defense(enemy)==ClassStats.ScaleDefense(basis,10),"Conviction multiplied after rather than added to defense skill percent");PaladinAuras.Restore(ordinary);
        var necro=ClassSkillChecks.Player(DiabloClass.Necromancer);necro.MoveToWorld(new(owner.X+1,owner.Y+1,owner.Z),owner.Map);
        Require(NecromancerEffects.Apply(necro,enemy,CombatAbility.LowerResist,20),"Lower Resist fixture");
        Require(Resistance(owner,enemy,"fire",enemy.FireResistance)==63 && Resistance(owner,enemy,"cold",enemy.ColdResistance)==-100
            && Resistance(owner,enemy,"ltng",enemy.EnergyResistance)==93 && Resistance(owner,enemy,"ltng",enemy.EnergyResistance,7)==86
            && Resistance(owner,enemy,"pois",enemy.PoisonResistance)==88,"Combined reductions must use original immunity independently");
        hp=enemy.Hits;AOS.Damage(enemy,owner,100,false,0,0,0,0,100,spellDamage:true);
        Require(enemy.Hits==hp-7,"Combined immunity break did not reach native damage");
        NecromancerEffects.ClearOrdinaryCurse(enemy);necro.Delete();
        // Same-rank update refreshes but retains the first supplier; weaker updates do not extend stronger lists.
        var other=ClassSkillChecks.Player(DiabloClass.Paladin);other.MoveToWorld(new(owner.X+1,owner.Y+1,owner.Z),owner.Map);
        var before=PaladinConviction.Effect(enemy);
        Require(!PaladinConviction.Apply(other,enemy,SkillMechanics.Values(other,CombatAbility.Conviction,1),TimedEffects.Now.AddMinutes(2))
            && PaladinConviction.Effect(enemy)==before,"Weaker aura changed/extended stronger hostile list");
        Require(PaladinConviction.Apply(other,enemy,SkillMechanics.Values(other,CombatAbility.Conviction,20),before.UntilUtc)
            && PaladinConviction.Effect(enemy).Source==owner.Serial.Value,"Equal-rank source ownership replacement");
        var hostiles=PaladinConviction.Snapshot();
        Require(!PaladinConviction.Apply(other,owner,SkillMechanics.Values(other,CombatAbility.Conviction,19),before.UntilUtc),"Higher supplier rank did not protect itself");
        Require(PaladinConviction.Apply(other,owner,SkillMechanics.Values(other,CombatAbility.Conviction,20),before.UntilUtc)
            && Resistance(other,owner,"fire",owner.FireResistance)==-100,"Equal supplier level was treated as strictly higher");
        PaladinConviction.Restore(hostiles);other.Delete();
    }
    private static void Elite(PlayerMobile owner,BaseCreature pet)
    {
        var profile=EncounterProfiles.Profile("crypt-sentinel-90");var elite=new EncounterCreature();
        elite.Assign(profile,profile.MinimumLevel,MonsterDifficulty.Hell,EncounterGrade.Unique,ChampionKind.Champion,
            [EliteModifier.AuraEnchanted],EliteAura.Conviction,null,new Random(173));
        elite.NoKillAwards=elite.Frozen=elite.CantWalk=true;elite.Home=owner.Location;elite.HomeMap=owner.Map;
        elite.MoveToWorld(new(owner.X,owner.Y+1,owner.Z),owner.Map);elite.AIObject.AITimer.Stop();EliteAuras.Observe(elite);
        try
        {
            int rank=EliteAuras.Level(owner,EliteAura.Conviction,true);Require(rank is >0 and <20,"Elite Conviction fixture level/hostility");
            Require(PaladinConviction.SupplierRank(elite)==rank && !PaladinConviction.Apply(owner,elite,SkillMechanics.Values(owner,CombatAbility.Conviction,1),TimedEffects.Now.AddSeconds(2))
                && PaladinConviction.Apply(owner,elite,SkillMechanics.Values(owner,CombatAbility.Conviction,20),TimedEffects.Now.AddSeconds(2)),"Higher monster supplier did not reject weaker/accept stronger class aura");
            EliteAuras.Refresh(owner);EliteAuras.Refresh(pet);
            Require(owner.FireResistance<0 && Resistance(owner,owner,"fire",owner.FireResistance)==0,
                $"Supplier did not strip/protect against existing elite modifier: native{owner.FireResistance}, resolved{Resistance(owner,owner,"fire",owner.FireResistance)}");
            int hp=owner.Hits;AOS.Damage(owner,elite,10,false,0,100,0,0,0,spellDamage:true);
            Require(owner.Hits==hp-10,"Native elite damage lost higher supplier protection");
            Require(PaladinConviction.SupplierRank(pet)==0 && pet.FireResistance<100,"Supplier protected its pet as if the pet had an aura");
            var auras=PaladinAuras.Snapshot();var hostile=PaladinConviction.Snapshot();
            // Isolated hostile-player consumer fixtures; production shared-world PvP remains disallowed.
            PaladinAuras.Restore(auras with{Leases=auras.Leases.Where(x=>x.Target!=owner.Serial.Value || x.Ability!=CombatAbility.Conviction).ToArray()});
            Require(PaladinConviction.Apply(owner,owner,SkillMechanics.Values(owner,CombatAbility.Conviction,1),TimedEffects.Now.AddSeconds(2)),"Weak fixture lease");
            int legacy=EliteAuras.ConvictionOffset(owner,"fire");
            Require(Resistance(owner,owner,"fire",owner.FireResistance)==legacy,"Class and legacy elite Conviction stacked");
            PaladinConviction.Apply(owner,owner,SkillMechanics.Values(owner,CombatAbility.Conviction,20),TimedEffects.Now.AddSeconds(2));
            Require(Resistance(owner,owner,"fire",owner.FireResistance)==-100,"Stronger class Conviction failed to replace legacy amount");
            PaladinConviction.Restore(hostile);PaladinAuras.Restore(auras);
        }
        finally{EliteAuras.Forget(elite);elite.Delete();EliteAuras.Refresh(owner);EliteAuras.Refresh(pet);}
    }
    private static void Check()
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.AddItem(new Backpack());owner.MoveToWorld(ClassSkillChecks.Corridor(owner,5720,2000),owner.Map);FreezeRecovery(owner);
        while(ClassCreation.Profiles.GetRank(owner.Serial,"paladin.conviction")<20){Require(ClassCreation.Profiles.TryTrain(owner.Serial,"paladin.conviction",out _),"Conviction training");}
        var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);FreezeRecovery(ally);
        var pet=Place(owner,new GraveyardSkeleton(),2);Require(pet.SetControlMaster(owner),"Conviction ally pet");
        var enemy=Place(owner,new Ogre(),4);var boundary=Place(owner,new Ogre(),10);var outside=Place(owner,new Ogre(),11);
        var one=SkillMechanics.Values(owner,CombatAbility.Conviction,1);var twenty=SkillMechanics.Values(owner,CombatAbility.Conviction,20);
        Require(one.Eval("aurastatcalc1")==-49 && one.Eval("aurastatcalc2")==-30 && one.Eval("aurarangecalc")==20 && one.Mana256==0
            && twenty.Eval("aurastatcalc1")==-90 && twenty.Eval("aurastatcalc2")==-125
            && SkillMechanics.Values(owner,CombatAbility.Conviction,25).Eval("aurastatcalc2")==-150
            && SkillMechanics.Values(owner,CombatAbility.Conviction,26).Eval("aurastatcalc2")==-150,"Pinned Conviction fields/formulas");
        owner.Mana=0;Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.Conviction) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Conviction)
            && ClassAbilities.SelfTarget(CombatAbility.Conviction) && PaladinConviction.SupplierRank(owner)==0,"Conviction right/self/delayed source state");
        Later(2200,"paladin-conviction-result",()=>
        {
            Require(PaladinConviction.SupplierRank(owner)==20 && PaladinConviction.Effect(owner)==null && PaladinConviction.Effect(ally)==null
                && PaladinConviction.Effect(pet)==null && PaladinConviction.Effect(enemy)!=null && PaladinConviction.Effect(boundary)!=null
                && PaladinConviction.Effect(outside)==null && !PaladinAuras.ManaSuppressed(owner),"Hostile fixed-radius/free aura or supplier/friend filtering");
            Cases(owner,enemy);Elite(owner,pet);boundary.Delete();outside.Delete();
            Seed(owner,ally,pet,enemy);_log.Information("paladin-conviction-result: passed");
        });
    }
    private static void Seed(PlayerMobile owner,PlayerMobile ally,BaseCreature pet,BaseCreature enemy)
    {
        // Snapshot inspection hold only; ordinary duration/pulse checks remain separate.
        var at=TimedEffects.Now.Add(FixtureSpace.Retention);var all=PaladinAuras.Snapshot();
        var auras=new PaladinAuraSave(all.Emitters.Where(x=>x.Owner==owner.Serial.Value).Select(x=>x with{NextUtc=at}).ToArray(),
            all.Leases.Where(x=>x.Source==owner.Serial.Value && x.Ability==CombatAbility.Conviction).Select(x=>x with{UntilUtc=at.AddSeconds(3)}).ToArray());
        var hostile=PaladinConviction.Snapshot().Where(x=>x.Source==owner.Serial.Value && World.FindMobile((Serial)x.Target) is{Alive:true,Deleted:false}).Select(x=>x with{UntilUtc=at.AddSeconds(3)}).ToArray();
        Copies(auras,hostile,owner.Serial.Value);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,ally.Serial.Value,pet.Serial.Value,enemy.Serial.Value,owner.Location,auras,hostile)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var all=PaladinAuras.Snapshot();
        var state=new PaladinAuraSave(all.Emitters.Where(x=>x.Owner==saved.Owner).ToArray(),all.Leases.Where(x=>x.Source==saved.Owner && x.Ability==CombatAbility.Conviction).ToArray());
        var hostile=PaladinConviction.Snapshot().Where(x=>x.Source==saved.Owner).ToArray();
        Require(JsonSerializer.Serialize(state)==JsonSerializer.Serialize(saved.Auras) && JsonSerializer.Serialize(hostile)==JsonSerializer.Serialize(saved.Hostile),$"Saved hostile/supplier ranks, owner, deltas or deadlines changed: aura{JsonSerializer.Serialize(state)} vs{JsonSerializer.Serialize(saved.Auras)}; hostile{JsonSerializer.Serialize(hostile)} vs{JsonSerializer.Serialize(saved.Hostile)}");
        var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);owner.MoveToWorld(saved.Origin,Map.Felucca);FreezeRecovery(owner);
        var ally=(PlayerMobile)World.FindMobile((Serial)saved.Ally);ally.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);FreezeRecovery(ally);
        owner.ClaimAutoStabledPets();var pet=(BaseCreature)World.FindMobile((Serial)saved.Pet);Require(pet.ControlMaster==owner,"Conviction pet claim");
        pet.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);pet.Frozen=pet.CantWalk=true;pet.AIObject.AITimer.Stop();Resist(pet);FreezeRecovery(pet);
        var enemy=(BaseCreature)World.FindMobile((Serial)saved.Enemy);enemy.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);enemy.Frozen=enemy.CantWalk=true;enemy.AIObject.AITimer.Stop();Resist(enemy);FreezeRecovery(enemy);
        Cases(owner,enemy);
        enemy.SetResistance(ResistanceType.Fire,90);Require(Resistance(owner,enemy,"fire",enemy.FireResistance)==65,"Saved application-time delta changed before a pulse");
        Copies(saved.Auras with{Emitters=saved.Auras.Emitters.Select(x=>x with{NextUtc=TimedEffects.Now}).ToArray()},saved.Hostile,saved.Owner);
        Later(120,"paladin-conviction-reload-result",()=>
        {
            Require(PaladinConviction.Effect(enemy) is{Fire:-125} && Resistance(owner,enemy,"fire",enemy.FireResistance)==-35,"Resumed pulse did not recompute original nonimmune stat");
            PaladinConviction.Restore(PaladinConviction.Snapshot().Select(x=>x.Target==enemy.Serial.Value?x with{UntilUtc=TimedEffects.Now.AddMilliseconds(80)}:x).ToArray());
            var paused=PaladinAuras.Snapshot();PaladinAuras.Restore(paused with{Emitters=paused.Emitters.Select(x=>x.Owner==saved.Owner?x with{NextUtc=TimedEffects.Now.AddMinutes(5)}:x).ToArray()});
            Later(160,"paladin-conviction-reload-result",()=>
            {
                Require(PaladinConviction.Effect(enemy)==null && Resistance(owner,enemy,"fire",enemy.FireResistance)==90,"Expired hostile aura still reduced resistance");
                Copies(saved.Auras,saved.Hostile,saved.Owner);_log.Information("paladin-conviction-reload-result: passed");
            });
        });
    }
}
