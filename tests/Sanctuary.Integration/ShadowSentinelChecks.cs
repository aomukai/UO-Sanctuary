// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowSentinelChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowSentinelChecks));
    private sealed record Held(uint Blade,string State);
    private sealed record Saved(uint Owner,uint[] Casters,Held[] Blades,int Mana,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-sentinel-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool v,string why){if(!v){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("shadow-sentinel-check",false);Register("shadow-sentinel-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Root-owned intrinsic Shadow Sentinel factory and birth weapon snapshot",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Arm(BladeSentinelBody blade)
    {
        var before=blade.Prepared;var frozen=before.FirstMissile;Require(frozen!=null&&!before.Creation.MissileArmed,"Missing deferred birth packet");blade.AdvanceController(before.NextFrameUtc,true);
        var missile=BladeSentinelMissiles.Snapshot().Missiles.Single(x=>x.Owner==blade.Serial.Value);
        Require(blade.Prepared.FirstMissile==null&&Json(blade.Prepared.Creation)==Json(frozen.Creation)&&missile.Damage==frozen.Damage&&missile.TargetDefenseDelta==frozen.TargetDefenseDelta,"First AI arm reread owner gear/stats or lost frozen attack inheritance");
        string packet=Json(missile.Damage);blade.AdvanceController(blade.Prepared.NextFrameUtc,true);Require(BladeSentinelMissiles.Snapshot().Missiles.Count(x=>x.Owner==blade.Serial.Value)==1&&Json(BladeSentinelMissiles.Snapshot().Missiles.Single(x=>x.Owner==blade.Serial.Value).Damage)==packet,"Repeated AI rearmed or rewrote following packet");
    }
    private static BladeSentinelBody Create(AssassinShadowBody caster,PlayerMobile owner,DateTime at)
    {
        caster.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);caster.ChangeSkills(s=>{s.Assign(257,12);s.Use(257);});
        try
        {
            int mana=NecromancerEffects.ManaBalance256(owner),own=NecromancerEffects.ManaBalance256(caster);var rng=NpcEquipmentRandom.Require(caster);
            var blade=ShadowBladeSentinels.CreatePrepared(caster,new(caster.X+6,caster.Y,caster.Z),at);var s=blade.Prepared;
            Require(s.Owner==owner.Serial.Value&&s.Spawn.Caster==caster.Serial.Value&&s.Creation.Rank==caster.OwnedSkillRank(257)&&s.Creation.Level==OriginalNpcEquipment.Level(caster)&&s.Spawn.BaseOwnerLevel==ClassStats.Level(owner)&&blade.Location==caster.Location,"Nested source rank/root ownership/caster origin or level lanes changed");
            var basis=BladeSentinelSummons.Capture(owner,caster.OwnedSkillRank(257));Require(s.Creation.BaseStatsLevel==basis.Creation.BaseStatsLevel&&s.Creation.Defense==basis.Creation.Defense&&s.Creation.Attack==basis.Creation.Attack,"Shadow gear/body stats replaced root summon base/passives");
            Require(NecromancerEffects.ManaBalance256(owner)==mana&&NecromancerEffects.ManaBalance256(caster)==own&&NpcEquipmentRandom.Require(caster)==rng&&AssassinTrapRoster.For(owner).Any(e=>e.Actor==blade.Serial.Value),"Prepared Sentinel spent resources,rolled Shadow critical RNG or lost root roster");return blade;
        }finally{caster.ChangeSkills(s=>s.Use(null));}
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;AssassinShadowBody[] casters;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));p=(PlayerMobile)World.FindMobile((Serial)s.Owner);casters=s.Casters.Select(id=>(AssassinShadowBody)World.FindMobile((Serial)id)).ToArray();generation=s.Generation+1;
            foreach(var h in s.Blades){var blade=(BladeSentinelBody)World.FindMobile((Serial)h.Blade);Require(Json(blade.Prepared)==h.State&&blade.Prepared.FirstMissile!=null,"Reload changed pending original birth packet/state");}
            Require(NecromancerEffects.ManaBalance256(p)==s.Mana,"Reload changed root mana");p.MoveToWorld(new(6460,3570,0),Map.Trammel);
            foreach(var h in s.Blades){var blade=(BladeSentinelBody)World.FindMobile((Serial)h.Blade);Arm(blade);blade.Delete();}
        }
        else
        {
            p=BladeBodyChecks.Fresh(6460,30,3570);ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Root strength");}while(p.RawDex<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Root dexterity");}
            ClassSkillChecks.Train(p,"assassin.shadowwarrior",20);ClassSkillChecks.Train(p,"assassin.shadowmaster",20);var world=NpcWorldAllocation.Capture();casters=new AssassinShadowBody[2];try{int i=0;foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}){NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));casters[i]=AssassinShadowBody.CreateInitialized(p,ability,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(casters[i]);ShadowEquipmentDefense.Activate(casters[i]);i++;}}finally{NpcWorldAllocation.Restore(world);}
        }
        Require(SkillMechanics.Rank(p,CombatAbility.BladeSentinel)==0,"Fixture must not borrow a learned root Sentinel rank");
        var claw=new Tekagi();ClassGear.Profiles.Convert(claw,99,GearRarity.Normal,new Random(173));p.AddToBackpack(claw);if(CombatTiming.Equipped(p)is{} old){p.AddToBackpack(old);}Require(p.EquipItem(claw),"Equip root snapshot claw");ClassGear.Refresh(p);
        var held=new Held[2];int index=0;
        foreach(var caster in casters)
        {
            var at=TimedEffects.Now.Add(FixtureSpace.Retention);var blade=Create(caster,p,at);var packet=blade.Prepared.FirstMissile;
            blade.RescaleDifficulty(MonsterDifficulty.Hell);Require(blade.Prepared.FirstMissile.Damage==packet.Damage&&blade.Prepared.FirstMissile.Creation.Defense==blade.Prepared.Creation.Defense,"Pending difficulty rescale rewrote damage or lost base-stat update");blade.RescaleDifficulty(MonsterDifficulty.Normal);
            p.AddToBackpack(claw);ClassGear.Refresh(p);var fresh=BladeSentinelLaunch.Capture(p,blade.Prepared.Creation,new Random(173));Require(fresh.Damage.Min256!=packet.Damage.Min256,"Gear swap did not distinguish captured root weapon");Arm(blade);blade.Delete();Require(p.EquipItem(claw),"Restore root claw");ClassGear.Refresh(p);
            var pending=Create(caster,p,at);held[index++]=new(pending.Serial.Value,Json(pending.Prepared));PersistentMinions.Park(caster);
        }
        // The saved pending packets must remain frozen while the root has
        // no weapon. Each is armed from its original packet on the next boot.
        p.AddToBackpack(claw);claw.Delete();ClassGear.Refresh(p);p.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,casters.Select(c=>c.Serial.Value).ToArray(),held,NecromancerEffects.ManaBalance256(p),generation)));

        Log.Information("Nested Sentinel factory:own rank/origin/current level,root player base/passives/weapon snapshot and roster,no player/Shadow mana or Shadow critical RNG;birth gear frozen before first AI,once-only arming,pre-arm difficulty rescale and saved pending packets pass. Own cast/localdelay admission remains separate.");
    }
}
