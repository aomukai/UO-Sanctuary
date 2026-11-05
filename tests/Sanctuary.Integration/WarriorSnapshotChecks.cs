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

public static class WarriorSnapshotChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WarriorSnapshotChecks));
    private sealed record Actor(uint Body,int Life,int Maximum,int Rank,long Order,ulong Seed,SummonUpgradeSnapshot Upgrade);
    private sealed record Saved(uint Owner,Actor[] Actors,int Mana,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"warrior-snapshot-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("warrior-snapshot-check",Check);Register("warrior-snapshot-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Frozen learned warrior upgrade ranks and original saved actors",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static SkeletonWorldBody Raise(PlayerMobile owner)
    {
        var dead=new GraveyardZombie{NoKillAwards=true};dead.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);dead.Damage(100000,owner);if(dead.Alive){dead.Kill();}var corpse=dead.Corpse as Corpse;
        try
        {
            NecromancerEffects.SetMana256(owner,owner.ManaMax*256);int before=NecromancerEffects.ManaBalance256(owner),cost=SkeletonPets.Mana(owner)*256;
            Require(SkeletonPets.Raise(owner,corpse)&&NecromancerEffects.ManaBalance256(owner)==before-cost,"Learned warrior cast/payment");
            var b=NecromancerSkeletons.For(owner,CombatAbility.RaiseSkeleton).OfType<SkeletonWorldBody>().Last();
            b.StoreWorld(IronGolemWorld.Park(b.WorldState.Controller) with{NextUtc=TimedEffects.Now.AddDays(3)});return b;
        }
        finally{corpse?.Delete();dead.Delete();}
    }
    private static Actor Save(SkeletonWorldBody b)=>new(b.Serial.Value,SkillHealth.Balance256(b),SkillHealth.Maximum256(b),b.Rank,BattleCommand.PetOrder(b),NpcEquipmentRandom.State(b).Seed,SummonUpgradeSnapshots.State(b));
    private static void Check()
    {
        var p=PoisonDaggerChecks.Owner();FixtureSpace.Corridor(p,6250,40,1500);MagePetBuffChecks.Train(p,"necromancer.raiseskeleton",9);PoisonDaggerChecks.Named(p,"Wormskull");
        var old=Raise(p);SkillHealth.SetBalance256(old,1111);TimedEffects.PreventHealing(old,TimedEffects.Now);old.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);
        Require(old.Rank==10&&SkeletonPetUpgrades.HardRank(old)==9&&PaladinThorns.FlatDamage(old)==0,"Born soft-inclusive rank unlocked hard10");
        MagePetBuffChecks.Train(p,"necromancer.raiseskeleton",20);var newer=Raise(p);SkillHealth.SetBalance256(newer,2222);TimedEffects.PreventHealing(newer,TimedEffects.Now);newer.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);
        Require(SkeletonPetUpgrades.HardRank(old)==9&&old.CombatProfile.MoveMs==240&&PaladinThorns.FlatDamage(old)==0
            &&SkeletonPetUpgrades.HardRank(newer)==20&&newer.CombatProfile.MoveMs==120&&PaladinThorns.FlatDamage(newer)==34,"Owner training rewrote old upgrade set or new summon missed unlocks");
        var doomed=Raise(p);Require(SummonUpgradeSnapshots.State(doomed)!=null,"Missing new actor snapshot");doomed.Delete();Require(SummonUpgradeSnapshots.State(doomed)==null,"Deleted actor retained birth supplement");
        string original=SummonUpgradeSnapshots.SnapshotJson;var entries=JsonSerializer.Deserialize<SummonUpgradeSnapshot[]>(original);
        foreach(var bad in new[]{entries.Append(entries[0]).ToArray(),entries.Select((r,i)=>i==0?r with{HardRank=21}:r).ToArray()})
        {
            bool rejected=false;try{SummonUpgradeSnapshots.Restore(bad);}catch(InvalidDataException){rejected=true;}
            Require(rejected&&SummonUpgradeSnapshots.SnapshotJson==original,"Malformed restore mutated existing snapshots");
        }
        var legacy=new SkeletonGuardian();legacy.Initialize(p,10,0);legacy.SetControlMaster(p);
        Require(SummonUpgradeSnapshots.State(legacy)==null&&SkeletonPetUpgrades.HardRank(legacy)==20,"Historical actor was assigned invented birth metadata");legacy.Delete();
        var actors=new[]{Save(old),Save(newer)};PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,actors,NecromancerEffects.ManaBalance256(p))));
        Log.Information("Warrior snapshots:actual learned hard9+soft1 and hard20 births;later owner training preserves old set;new summon gets34ATD/ExtraFast;deletion/atomic malformed restore/explicit historical fallback;original wounds,resources,order and RNG staged");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Extra warrior snapshot reload");var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(ClassCreation.Profiles.GetRank(owner.Serial,"necromancer.raiseskeleton")==20&&NecromancerEffects.ManaBalance256(owner)==saved.Mana,"Saved owner ranks/mana changed");
        foreach(var row in saved.Actors)
        {
            var b=(SkeletonWorldBody)World.FindMobile((Serial)row.Body);
            Require(b.ControlMaster==owner&&SummonUpgradeSnapshots.State(b)==row.Upgrade&&SkeletonPetUpgrades.HardRank(b)==row.Upgrade.HardRank
                &&SkillHealth.Balance256(b)==row.Life&&SkillHealth.Maximum256(b)==row.Maximum&&b.Rank==row.Rank
                &&BattleCommand.PetOrder(b)==row.Order&&NpcEquipmentRandom.State(b).Seed==row.Seed,"Original actor snapshot,wounds,ranks,order or RNG changed on load");
            Require(PaladinThorns.FlatDamage(b)==(row.Upgrade.HardRank>=10?34:0)&&b.CombatProfile.MoveMs==(row.Upgrade.HardRank>=20?120:240),"Loaded upgrades read live owner rank");
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
}
