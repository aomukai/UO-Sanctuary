// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.LegacySeed;

public static class LegacyArmySeed
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(LegacyArmySeed));
    private sealed record Actor(uint Serial, string Type, int Element, int Life, int Maximum, int Level, int Rank,
        int Mastery, long Order, string Ranks, Point3D Point);
    public static void Configure() => ConsoleInputHandler.RegisterCommand("necro-legacy-army-seed", "Create original army actors with the installed old factories", _ => Core.LoopContext.Post(() =>
    {
        try { Seed(); Log.Information("necro-legacy-army-seed: passed"); }
        catch (Exception e) { Log.Error(e, "necro-legacy-army-seed: failed"); }
    }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    private static Corpse Corpse(PlayerMobile owner, int offset)
    {
        var victim = new GraveyardZombie { Frozen=true, CantWalk=true, NoKillAwards=true };
        victim.MoveToWorld(CombatPractice.FindFloor(owner.Map, owner.X + offset, owner.Y), owner.Map);
        victim.AIObject.AITimer.Stop(); victim.Damage(victim.Hits+1,owner);
        return victim.Corpse as Corpse ?? throw new InvalidOperationException("Old factory needs its actual monster corpse");
    }
    private static Actor Inspect(SkeletonGuardian actor, int element)
    {
        actor.Hits = Math.Max(1, actor.HitsMax * 2 / 3 + 1);
        var row = new Actor(actor.Serial.Value, actor.GetType().FullName, element, actor.Hits, actor.HitsMax, actor.Level,
            actor.Rank, actor.Mastery, BattleCommand.Snapshot().Pets.Single(p=>p.Pet==actor.Serial.Value).Order,
            actor is NecromancerMinion mage ? mage.Ranks : null, actor.Location);
        actor.AIObject?.AITimer.Stop(); actor.Combatant=null; actor.Internalize(); return row;
    }
    private static void Seed()
    {
        var owner = new PlayerMobile { Player=true, Body=0x190, Name="ColdArmorFixtureArmyLegacy" };
        owner.AddItem(new Backpack()); ClassCreation.Profiles.Assign(owner, DiabloClass.Necromancer);
        ClassCreation.Profiles.AdvanceLevel(owner,99);
        // Use the retained old fixture's floor builder, not a natural-map
        // assumption. The helper is internal in that unchanged old DLL.
        var place=Assembly.Load("Sanctuary.Integration").GetType("Sanctuary.Integration.FixtureSpace")
            .GetMethod("Corridor",BindingFlags.Static|BindingFlags.NonPublic);
        Require(place!=null,"Retained old fixture corridor helper missing");
        place.Invoke(null,new object[]{owner,7050,24,2050});
        foreach(var tree in TalentCatalog.ForClass(DiabloClass.Necromancer).Trees)
        foreach(var talent in tree.Skills.OrderBy(t=>t.RequiredLevel))
        { Require(ClassCreation.Profiles.TryTrain(owner.Serial,talent.Id,out _),"Old prerequisite training: "+talent.Id); }
        foreach(string skill in new[]{"raiseskeleton","skeletonmastery","skeletalmage"})
        for(int rank=1;rank<20;rank++)
        { Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer."+skill,out _),"Old rank training: "+skill); }
        owner.Hits=owner.HitsMax; owner.Mana=owner.ManaMax;
        var warriorCorpse=Corpse(owner,1);
        Require(SkeletonPets.Raise(owner,warriorCorpse),
            $"Actual old paid warrior factory refused: rank={SkeletonPets.Rank(owner)},mana={owner.Mana}/{SkeletonPets.Mana(owner)},alive={owner.Alive},usable={SkeletonPets.Usable(owner,warriorCorpse)},spawn={owner.Map.CanSpawnMobile(warriorCorpse.X,warriorCorpse.Y,warriorCorpse.Z)},see={owner.CanSee(warriorCorpse)},los={owner.InLOS(warriorCorpse)},loot={warriorCorpse.CanLoot(owner,null)},criminal={warriorCorpse.IsCriminalAction(owner)},flags={warriorCorpse.OwnerWasBaseCreature}/{warriorCorpse.OwnerWasSummoned}/{warriorCorpse.IsBones},point={warriorCorpse.Location},owner={owner.Location}");
        var warrior=owner.AllFollowers.OfType<SkeletonGuardian>().Single(p=>p.GetType()==typeof(SkeletonGuardian));
        var actors=new List<Actor>{Inspect(warrior,-1)};
        var elements=new HashSet<int>(); int attempts=0;
        while(elements.Count<4&&attempts++<256)
        {
            var corpse=Corpse(owner,3+attempts%4);
            var before=owner.AllFollowers.Select(p=>p.Serial.Value).ToHashSet();
            Require(NecromancerSummons.Create(owner,CombatAbility.RaiseSkeletalMage,corpse.Serial,corpse.Location),"Actual old Mage corpse factory refused");
            var mage=owner.AllFollowers.OfType<NecromancerMinion>().Single(p=>!before.Contains(p.Serial.Value));
            if(!elements.Add(mage.Element)){mage.Delete();continue;}
            actors.Add(Inspect(mage,mage.Element));
        }
        Require(elements.SetEquals(new[]{0,1,2,3}),"Old RNG did not produce all four authentic element slots");
        owner.Hits=owner.HitsMax-41; Require(NecromancerEffects.Pay(owner,128),"Old army half-mana payment");
        var point=owner.Location; int map=owner.Map.MapID; owner.Internalize();
        File.WriteAllText(Path.Join(Core.BaseDirectory,"necro-legacy-army-seed.json"),JsonSerializer.Serialize(new
        { Owner=owner.Serial.Value, Life=owner.Hits, Maximum=owner.HitsMax, Mana256=NecromancerEffects.ManaBalance256(owner), Point=point, Map=map,
            Actors=actors.ToArray(), ElementAttempts=attempts }));
    }
}
