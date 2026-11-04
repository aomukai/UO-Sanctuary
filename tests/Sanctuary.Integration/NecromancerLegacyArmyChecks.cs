// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class NecromancerLegacyArmyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NecromancerLegacyArmyChecks));
    private sealed record Actor(uint Serial,string Type,int Element,int Life,int Maximum,int Level,int Rank,int Mastery,long Order,string Ranks,Point3D Point);
    private sealed record Seed(uint Owner,int Life,int Maximum,int Mana256,Point3D Point,int Map,Actor[] Actors,int ElementAttempts);
    private sealed record Mage(uint Actor,MageCriticalSeed Seed);
    private sealed record Progress(int Generation,Mage[] Mages);
    private static string FileFor(string name)=>Path.Join(Core.BaseDirectory,name+".json");
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("necro-legacy-army-check","Upgrade authentic earlier-writer warrior/Mage actors",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("necro-legacy-army-check: passed");}catch(Exception e){Log.Error(e,"necro-legacy-army-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Clear(NecromancerMinion actor)
    {var snapshot=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(snapshot with{Bolts=snapshot.Bolts.Where(b=>b.Actor!=actor.Serial.Value).ToArray()});}
    private static void Check()
    {
        var seed=JsonSerializer.Deserialize<Seed>(File.ReadAllText(FileFor("necro-legacy-army-seed")));
        var prior=File.Exists(FileFor("necro-legacy-army-progress"))?JsonSerializer.Deserialize<Progress>(File.ReadAllText(FileFor("necro-legacy-army-progress"))):null;
        var owner=World.FindMobile((Serial)seed.Owner)as PlayerMobile;
        Require(owner is{Alive:true,Deleted:false}&&owner.Hits==seed.Life&&owner.HitsMax==seed.Maximum&&NecromancerEffects.ManaBalance256(owner)==seed.Mana256,"Original army owner wounds/mana changed");
        Require(seed.Actors.Length==5&&seed.Actors.Count(a=>a.Element<0)==1&&seed.Actors.Where(a=>a.Element>=0).Select(a=>a.Element).Order().SequenceEqual(new[]{0,1,2,3}),"Actual old army element coverage incomplete");
        foreach(string skill in new[]{"raiseskeleton","skeletonmastery","skeletalmage"})
        {Require(ClassCreation.Profiles.GetRank(owner.Serial,"necromancer."+skill)==20,"Old owner hard ranks changed");}
        owner.MoveToWorld(seed.Point,Map.Maps[seed.Map]);var results=new System.Collections.Generic.List<Mage>();
        foreach(var row in seed.Actors)
        {
            var actor=World.FindMobile((Serial)row.Serial)as SkeletonGuardian;
            // The old writer floored 221*2.7 to 596 whole HP. The corrected
            // FP256 source maximum is 152755 (596.699...), displayed as 597.
            // Preserve actual wounded HP; do not freeze the old display floor.
            int maximum=row.Element<0?row.Maximum:597;
            Require(actor is{Alive:true,Deleted:false}&&actor.GetType().FullName==row.Type&&actor.ControlMaster==owner
                &&actor.Hits==row.Life&&actor.HitsMax==maximum&&actor.Level==row.Level&&actor.Rank==row.Rank&&actor.Mastery==row.Mastery
                &&BattleCommand.PetOrder(actor)==row.Order,
                $"Actual old army actor was replaced/reset: element={row.Element},life={actor?.Hits}/{actor?.HitsMax},expected={row.Life}/{row.Maximum},order={(actor==null?-1:BattleCommand.PetOrder(actor))}/{row.Order}");
            if(row.Element<0)
            {Require(actor.GetType()==typeof(SkeletonGuardian)&&SkeletonPetUpgrades.FlatDamage(actor)==34&&SkeletonPetUpgrades.MovementPercent(actor)>0,"Original warrior lost approved hard-point unlocks");continue;}
            var mage=(NecromancerMinion)actor;int element=row.Element==0?1:row.Element;
            Require(row.Maximum==596&&row.Rank==20&&row.Mastery==20&&SkillHealth.Maximum256(mage)==152755
                &&SkillHealth.Balance256(mage)==row.Life*256,"Old Mage source maximum correction or exact wounded life changed");
            Require(mage.MageElement==element&&mage.Ranks==row.Ranks,"Original Mage element mapping or captured context changed");
            Require(!NecromancerMageMissiles.Snapshot().Bolts.Any(b=>b.Actor==mage.Serial.Value),"Old writer invented/replayed a spell flight");
            var oldSeed=NecromancerMageMissiles.ActorSeed(mage);
            Require(prior==null?oldSeed==null:oldSeed==prior.Mages.Single(m=>m.Actor==mage.Serial.Value).Seed,"Original adopted Mage stream changed on reload");
            mage.MoveToWorld(row.Point,owner.Map);mage.AIObject.AITimer.Stop();
            Require(MageWorld.TryAdopt(mage,TimedEffects.Now)&&!mage.UsesNativeSummonTimer,"Actual old Mage failed shared-controller adoption");
            Require(mage.Hits==row.Life&&mage.HitsMax==maximum&&BattleCommand.PetOrder(mage)==row.Order,"Adoption healed/replaced/reordered actual old Mage");
            if(prior==null){Require(MageWorld.For(mage).World.State.Controller is{Mode:1,Frame:0,NextThink:1,ActionAt:0,EndAt:0},"Old Mage fabricated a historical frame/action");}
            var target=new GraveyardZombie{Frozen=true,CantWalk=true,NoKillAwards=true};
            target.MoveToWorld(new(mage.X+2,mage.Y,mage.Z),mage.Map);target.AIObject.AITimer.Stop();target.SetHits(20000);target.Hits=target.HitsMax;
            foreach(var resistance in Enum.GetValues<ResistanceType>()){target.SetResistance(resistance,0);}int life=target.Hits;
            try
            {
                Require(MageBasicSkills.Launch(mage,target,TimedEffects.Now)>0,"Adopted old Mage failed its approved basic spell");
                var flights=NecromancerMageMissiles.Snapshot().Bolts.Where(b=>b.Actor==mage.Serial.Value).ToArray();
                var expected=element==1?CombatAbility.IceBolt:element==2?CombatAbility.FireBolt:CombatAbility.ChargedBolt;
                Require(flights.Length==(element==3?66:3)&&flights.All(f=>f.Packet.Spell==expected&&f.Packet.Element==(element==1?"cold":element==2?"fire":"ltng")),"Old Mage failed triple basic spell/count or kept old poison");
                for(int frame=0;frame<80;frame++)
                foreach(var flight in NecromancerMageMissiles.Snapshot().Bolts.Where(b=>b.Actor==mage.Serial.Value).ToArray())
                {NecromancerMageMissiles.Tick(flight.NextUtc,true,flight.Id);}
                Require(target.Hits<life,"Approved upgraded basic did not damage the actual target");
                Require((MagePetBuffs.Armor(mage)!=null)==(element==1),"Converted/original ice Mage fixed armor mapping changed");
            }
            finally{Clear(mage);target.Corpse?.Delete();target.Delete();}
            MageWorld.For(mage).World.Suspend();mage.Internalize();
            Require(mage.Hits==row.Life&&mage.HitsMax==maximum&&BattleCommand.PetOrder(mage)==row.Order,"Basic casting changed original Mage resources/order");
            results.Add(new(mage.Serial.Value,NecromancerMageMissiles.ActorSeed(mage)));
        }
        owner.Internalize();Require(owner.Hits==seed.Life&&NecromancerEffects.ManaBalance256(owner)==seed.Mana256,"Army upgrade/basic casts charged or healed the original owner");
        File.WriteAllText(FileFor("necro-legacy-army-progress"),JsonSerializer.Serialize(new Progress((prior?.Generation??0)+1,results.ToArray())));
    }
}
