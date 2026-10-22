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

public static class PaladinResistanceChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(PaladinResistanceChecks));
    private sealed record Saved(uint[] Owners,uint Ally,uint Pet,Point3D Origin,PaladinAuraSave State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"paladin-resistance-checks.json");
    public static void Configure(){Register("paladin-resistance-check",Check);Register("paladin-resistance-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Paladin resistance auras, hard-point caps, native damage and saved pulses",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>
    {try{action();}catch(Exception e){_log.Error(e,"{Name}: failed",name);}});
    private static void Select(PlayerMobile owner,CombatAbility ability)=>Require(SkillBindings.Instance.Assign(owner,false,ability),"Resistance aura selection failed");
    private static void Train(PlayerMobile owner,string id,int rank)
    {while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Resistance hard rank");}}
    private static GraveyardSkeleton Pet(PlayerMobile owner)
    {
        var pet=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true,Controlled=true,ControlMaster=owner};
        pet.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);pet.AIObject.AITimer.Stop();Zero(pet);return pet;
    }
    private static void Zero(BaseCreature actor)
    {actor.SetResistance(ResistanceType.Fire,0);actor.SetResistance(ResistanceType.Cold,0);actor.SetResistance(ResistanceType.Energy,0);}
    private static void Check()
    {
        var fire=ClassSkillChecks.Player(DiabloClass.Paladin);fire.AddItem(new Backpack());
        fire.MoveToWorld(ClassSkillChecks.Corridor(fire,5720,1300),fire.Map);Train(fire,"paladin.resistfire",20);
        var cold=ClassSkillChecks.Player(DiabloClass.Paladin);cold.AddItem(new Backpack());cold.MoveToWorld(new(fire.X+1,fire.Y,fire.Z),fire.Map);
        var lightning=ClassSkillChecks.Player(DiabloClass.Paladin);lightning.MoveToWorld(new(fire.X+2,fire.Y,fire.Z),fire.Map);
        var salvation=ClassSkillChecks.Player(DiabloClass.Paladin);salvation.MoveToWorld(new(fire.X+3,fire.Y,fire.Z),fire.Map);
        var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.MoveToWorld(new(fire.X+2,fire.Y+1,fire.Z),fire.Map);
        var pet=Pet(cold);
        var hostile=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};hostile.MoveToWorld(new(fire.X+5,fire.Y,fire.Z),fire.Map);hostile.AIObject.AITimer.Stop();
        foreach(var ability in new[]{CombatAbility.ResistFire,CombatAbility.ResistCold,CombatAbility.ResistLightning})
        {
            var values=SkillMechanics.Values(cold,ability);
            Require(values.Eval("aurastatcalc1")==52 && SkillMechanics.Values(cold,ability,20).Eval("aurastatcalc1")==131
                && SkillMechanics.Values(cold,ability,20).Eval("aurastatcalc2")==1 && values.Mana256==0,"Pinned rank and hard-point maximum formula");
        }
        Require(SkillMechanics.Values(salvation,CombatAbility.Salvation).Eval("aurastatcalc1")==60
            && SkillMechanics.Values(salvation,CombatAbility.Salvation,20).Eval("aurastatcalc1")==108,"Salvation rank1/rank20 resistance");
        Require(NamedCombat.ResistanceCap(fire,ResistanceType.Fire)==85 && fire.FireResistance==0
            && NamedCombat.ResistanceCap(cold,ResistanceType.Cold)==75,"Inactive hard-point passive added actual resistance or rounded up");
        // A real supported all-skills item exercises effective rank without granting hard-point maximum resistance.
        var definition=NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm");var helm=NamedGear.Create(definition.Id,99,new Random(317),true);
        Require(cold.EquipItem(helm) && SkillMechanics.Rank(cold,CombatAbility.ResistCold)==2
            && NamedCombat.ResistanceCap(cold,ResistanceType.Cold)==75,"Soft point changed inactive maximum");
        // Equip real legal named resistance sources; do not bypass saved-gear validation.
        ally.RawStr=ally.RawDex=1000;
        var gear=new[]{"Kira's Guardian","The Ward","Nokozan Relic","Hotspur"}.Select(key=>
        {
            var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(317),true);
            Require(ally.EquipItem(item),"Overcap fixture equipment: "+key);return item;
        }).ToArray();
        Select(fire,CombatAbility.ResistFire);
        Require(PaladinAuras.Effect(ally,CombatAbility.ResistFire) is{Rank:20,Percent:131,MaximumResistance:20}
            && fire.FireResistance==95 && ally.FireResistance==95 && PaladinResistance.Maximum(fire,ResistanceType.Fire)==20
            && PaladinResistance.Bonus(hostile,ResistanceType.Fire)==0,"Active self/ally maximum, passive suppression or hostile filter");
        int raw=ClassGear.RawResistance(ally,ResistanceType.Fire);
        Require(raw>200 && NamedElements.Resistance(hostile,ally,"fire",ally.FireResistance,raw-100)==95
            && NamedElements.Resistance(hostile,ally,"fire",ally.FireResistance,raw-31)==31,"Player resistance capped before pierce");
        foreach(var item in gear){item.Delete();}
        int hp=ally.Hits;AOS.Damage(ally,hostile,100,false,0,100,0,0,0);
        Require(ally.Hits==hp-5,"Native player fire damage ignored 95-percent cap");
        hp=pet.Hits;AOS.Damage(pet,hostile,100,false,0,100,0,0,0);
        Require(pet.Hits==hp && NamedElements.Resistance(hostile,pet,"fire",pet.FireResistance)==131,"Native pet resistance lost aura or received player cap");
        var strong=PaladinAuras.Effect(ally,CombatAbility.ResistFire);Select(lightning,CombatAbility.ResistFire);
        Require(PaladinAuras.Effect(ally,CombatAbility.ResistFire)==strong,"Weaker rank altered stronger resistance maximum");
        Select(lightning,CombatAbility.ResistLightning);Select(cold,CombatAbility.ResistCold);Select(salvation,CombatAbility.Salvation);
        Require(PaladinAuras.Effect(ally,CombatAbility.ResistCold) is{Rank:2,Percent:66,MaximumResistance:1}
            && PaladinResistance.Bonus(ally,ResistanceType.Fire)==191 && PaladinResistance.Bonus(ally,ResistanceType.Cold)==126
            && PaladinResistance.Bonus(ally,ResistanceType.Energy)==112 && PaladinResistance.Bonus(ally,ResistanceType.Poison)==0
            && PaladinResistance.Maximum(ally,ResistanceType.Cold)==1 && NamedCombat.ResistanceCap(ally,ResistanceType.Cold)==76,
            "Different aura addition, hard-point active maximum, Salvation poison/cap exclusion");
        Require(ally.FireResistance==95 && ally.ColdResistance==76 && ally.EnergyResistance==76 && ally.PoisonResistance==0,"Native class resistance display");
        hp=ally.Hits;AOS.Damage(ally,hostile,100,false,0,0,100,0,0);Require(ally.Hits==hp-24,"Native cold damage lost soft-rank aura/hard-point maximum");
        ally.MoveToWorld(new(fire.X+45,fire.Y,fire.Z),fire.Map);
        Require(ally.FireResistance==95,"Recipient resistance vanished on range exit");
        Later(2300,"paladin-resistance-result",()=>
        {
            Require(ally.FireResistance==0 && ally.ColdResistance==0 && NamedCombat.ResistanceCap(ally,ResistanceType.Fire)==75,"Out-of-range resistance lease did not expire");
            Select(fire,CombatAbility.None);Require(NamedCombat.ResistanceCap(fire,ResistanceType.Fire)==85,"Active self state did not restore passive on selection change");
            Select(fire,CombatAbility.ResistFire);fire.Kill();fire.Resurrect();Require(!PaladinResistance.Active(fire) && PaladinAuras.Selected(fire)==CombatAbility.None,"Death retained resistance aura");
            Select(fire,CombatAbility.ResistFire);ClassCreation.Profiles.TryGet(fire.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(fire,profile);
            Require(!PaladinResistance.Active(fire) && NamedCombat.ResistanceCap(fire,ResistanceType.Fire)==75,"Respec retained active/passive maximum");
            // Rebuild the fixture's ranks after the real reset, keeping the production cleanup contract.
            Train(fire,"paladin.resistfire",20);Select(fire,CombatAbility.ResistFire);
            ally.MoveToWorld(new(fire.X+2,fire.Y+1,fire.Z),fire.Map);
            foreach(var pair in new[]{(fire,CombatAbility.ResistFire),(cold,CombatAbility.ResistCold),(lightning,CombatAbility.ResistLightning),(salvation,CombatAbility.Salvation)}){Select(pair.Item1,pair.Item2);}
            hostile.Delete();fire.Corpse?.Delete();Seed([fire,cold,lightning,salvation],ally,pet);
            _log.Information("paladin-resistance-result: passed");
        });
    }
    private static void Seed(PlayerMobile[] owners,PlayerMobile ally,BaseCreature pet)
    {
        uint[] ids=owners.Select(x=>x.Serial.Value).ToArray();var at=TimedEffects.Now.AddMinutes(5);var state=PaladinAuras.Snapshot();
        state=state with{Emitters=state.Emitters.Select(x=>ids.Contains(x.Owner)?x with{NextUtc=at}:x).ToArray(),
            Leases=state.Leases.Select(x=>ids.Contains(x.Source)?x with{UntilUtc=at.AddMilliseconds(40)}:x).ToArray()};
        PaladinAuras.Restore(state);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(ids,ally.Serial.Value,pet.Serial.Value,owners[0].Location,state)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var state=PaladinAuras.Snapshot();
        PaladinAuraSave Scope(PaladinAuraSave s)=>new(s.Emitters.Where(x=>saved.Owners.Contains(x.Owner)).ToArray(),s.Leases.Where(x=>saved.Owners.Contains(x.Source)).ToArray());
        Require(JsonSerializer.Serialize(Scope(state))==JsonSerializer.Serialize(Scope(saved.State)),"Saved resistance rank, maximum, source or expiry changed");
        var owners=saved.Owners.Select(x=>(PlayerMobile)World.FindMobile((Serial)x)).ToArray();var ally=(PlayerMobile)World.FindMobile((Serial)saved.Ally);var pet=(BaseCreature)World.FindMobile((Serial)saved.Pet);
        for(int i=0;i<owners.Length;i++){owners[i].MoveToWorld(new(saved.Origin.X+i,saved.Origin.Y,saved.Origin.Z),Map.Trammel);}
        Require(pet.IsStabled && pet.StabledBy==owners[1],"Native offline pet custody was not retained");
        owners[1].ClaimAutoStabledPets();
        Require(pet.Controlled && pet.ControlMaster==owners[1] && !pet.IsStabled,"Native pet claim did not restore custody");
        ally.MoveToWorld(new(saved.Origin.X+2,saved.Origin.Y+1,saved.Origin.Z),Map.Trammel);pet.MoveToWorld(new(saved.Origin.X+4,saved.Origin.Y,saved.Origin.Z),Map.Trammel);
        // Native creature templates reapply base resistances at load; this controlled target starts at zero again.
        Zero(pet);PaladinAuras.Clear(ally);PaladinAuras.Clear(pet);var now=TimedEffects.Now;
        PaladinAuras.Restore(PaladinAuras.Snapshot() with{Emitters=state.Emitters.Select(x=>saved.Owners.Contains(x.Owner)?x with{NextUtc=now}:x).ToArray()});
        Later(120,"paladin-resistance-reload-result",()=>
        {
            Require(ally.FireResistance==95 && ally.ColdResistance==76 && ally.EnergyResistance==76
                && PaladinAuras.Effect(ally,CombatAbility.ResistCold) is{Rank:2,MaximumResistance:1} lease && lease.UntilUtc<now.AddSeconds(3),"Restored resistance sources did not resume fresh short-lived applications");
            int hp=ally.Hits;AOS.Damage(ally,pet,100,false,0,100,0,0,0);
            Require(ally.Hits==hp,"Claimed allied pet bypassed friendly damage protection");
            var hostile=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};
            hostile.MoveToWorld(ally.Location,ally.Map);hostile.AIObject.AITimer.Stop();
            try{AOS.Damage(ally,hostile,100,false,0,100,0,0,0);Require(ally.Hits==hp-5,"Restored native resistance mitigation");}
            finally{hostile.Delete();}
            Require(NamedElements.Damage(null,pet,100,"fire")==0,$"Restored pet aura resistance: alive={pet.Alive}, controlled={pet.Controlled}, master={pet.ControlMaster?.Serial}, base={pet.FireResistance}, bonus={PaladinResistance.Bonus(pet,ResistanceType.Fire)}");
            PaladinAuras.Restore(state);_log.Information("paladin-resistance-reload-result: passed");
        });
    }
}
