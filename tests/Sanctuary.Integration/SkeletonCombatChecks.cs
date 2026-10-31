// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class SkeletonCombatChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SkeletonCombatChecks));
    private sealed record Saved(uint Owner,uint Actor,ulong Seed,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"skeleton-combat-fixtures.json");
    public static void Configure()
    {
        Register("skeleton-combat-check",Check);Register("skeleton-combat-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("skeleton-combat-live","Connected source-timed skeleton attack",_=>Core.LoopContext.Post(()=>
            FixtureNetwork.Connected("skeleton-combat-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Learned skeleton unarmed FP damage,critical,clock and saved stream",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureSkeletonCombat";
        while(ClassCreation.Profiles.GetRank(p.Serial,"necromancer.raiseskeleton")<4)
        {Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.raiseskeleton",out _),"Train Raise Skeleton4");}
        return p;
    }
    private static RaisedSkeleton Pet(PlayerMobile p)
    {
        var enemy=new GraveyardZombie{NoKillAwards=true};enemy.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);enemy.Damage(100000,p);if(enemy.Alive){enemy.Kill();}
        var corpse=(Corpse)enemy.Corpse;Require(SkeletonPets.Raise(p,corpse),"Actual learned skeleton creation");corpse.Delete();
        var pet=NecromancerSkeletons.For(p,CombatAbility.RaiseSkeleton).OfType<RaisedSkeleton>().Single();pet.AIObject.AITimer.Stop();return pet;
    }
    private static void Clocks(RaisedSkeleton pet)
    {
        Require(pet.SourceAttackClock==(12,19)&&pet.CombatProfile.AttackMs==760,"Pinned224speed/action10/end16 clock");
        TimedEffects.Slow(pet,50,TimedEffects.Now);Require(pet.SourceAttackClock==(23,37),"50percent slow must use source rate then frame planner");
        TimedEffects.Clear(pet);Require(pet.SourceAttackClock==(12,19),"Cleared slow changed baseline");
        Require(DruidWolfGeometry.Clock("A1",SourceAttackRate.Speed(224,0,275),"necroskeleton-animation")== (7,11),"175percent final attack-rate cap");
        Require(NecromancerSkeletonBirth.Prepare(99,84,255,MonsterDifficulty.Normal).Life256>0,"Cross-facet control must start in a safe Normal domain");
        bool rejected=false;try{NecromancerSkeletonBirth.Preflight(99,84,255);}catch(NotSupportedException){rejected=true;}
        Require(rejected,"A later Hell-facet overflow was not refused before creation");
    }
    private static void Damage(PlayerMobile p,RaisedSkeleton pet)
    {
        var target=PoisonDaggerChecks.Target(p);target.SetResistance(ResistanceType.Physical,0);TimedEffects.PreventHealing(target,TimedEffects.Now);
        try
        {
            Require(NpcEquipmentCombat.Context(pet)==null&&NpcEquipmentDamage.Capture(pet)==new NpcPhysicalStats(1,2,0,7,0,0,false),"Unequipped skeleton source stats");
            var chances=NpcEquipmentCritical.Capture(pet);Require(chances==new NpcCriticalChances(0,0,0,5),"Source intrinsic5percent or inherited owner critical");
            var low=NpcEquipmentDamage.Roll(pet,target,_=>0,()=>99);var high=NpcEquipmentDamage.Roll(pet,target,n=>n-1,()=>99);
            Require(low.Damage.Physical256==273&&high.Damage.Physical256==546&&!low.PhysicalCritical&&!low.Damage.Intrinsic,"Exclusive FP range273..546");
            var crit=NpcEquipmentDamage.Roll(pet,target,_=>0,()=>0);
            Require(crit.Damage.Physical256==546&&crit.Damage.Intrinsic&&!crit.PhysicalCritical,"Intrinsic critical doubles NPC damage only once");
            int before=SkillHealth.Balance256(target);Require(NpcEquipmentDamage.TryResolve(pet,target,out _,_=>0,()=>99)&&before-SkillHealth.Balance256(target)==273,"Actual fractional unarmed damage");
            var impact=NpcEquipmentImpact.Prepare(pet,target,5,_=>0,()=>0);Require(impact.Packet.Physical256==546&&impact.Packet.Total256==546,"Unarmed ordinary allocation route");
            PoisonDaggerChecks.Named(p,"Bloodfist");PoisonDaggerChecks.Named(p,"Wormskull");
            Require(pet.Rank==4&&pet.Mastery==0&&NpcEquipmentDamage.Capture(pet)==new NpcPhysicalStats(1,2,0,7,0,0,false)
                &&NpcEquipmentCritical.Capture(pet)==chances&&NpcEquipmentPoison.Capture(pet).Stats==new MissileSourceStats()&&pet.SourceAttackClock==(12,19),"Owner gear/ranks/poison/IAS leaked to an existing pet");
            var caster=new PlayerMobile{Player=true,Body=0x190};ClassCreation.Profiles.Assign(caster,DiabloClass.Sorceress);
            try
            {
                SorceressEffects.CastEnchant(caster,pet,SkillMechanics.Values(caster,CombatAbility.Enchant,1));var enchant=SorceressEffects.Enchantment(pet);
                var fire=NpcEquipmentDamage.Roll(pet,target,_=>0,()=>99);var doubled=NpcEquipmentDamage.Roll(pet,target,_=>0,()=>0);
                Require(enchant!=null&&fire.Damage.Elements.Fire==enchant.Min256/256.0&&doubled.Damage.Elements.Fire==enchant.Min256/128.0,"Unarmed pet Enchant/critical delivery");
            }
            finally{SorceressEffects.Profiles.Clear(pet);caster.Delete();}
            NpcEquipmentRandom.Set(pet,SourceRandom.InitLow(0));ulong expected=SourceRandom.InitLow(0);
            int physical=NpcEquipmentDamage.Roll256(pet.UnarmedPhysical,n=>(int)SourceRandom.Limited(ref expected,n));bool intrinsic=SourceRandom.Roll(ref expected)%100<5;
            before=SkillHealth.Balance256(target);
            typeof(SkeletonGuardian).GetMethod("Strike",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pet,[target]);
            Require(before-SkillHealth.Balance256(target)==physical*(intrinsic?2:1)&&NpcEquipmentRandom.State(pet).Seed==expected,"Actual skeleton Strike bypassed the shared packet/stream");
            var original=NpcEquipmentRandom.Snapshot();
            bool invalid=false;try{NpcEquipmentRandom.Restore(original.Append(new NpcEquipmentSeed(pet.Serial.Value,p.Serial.Value,1,0,true)).ToArray());}catch(InvalidDataException){invalid=true;}
            Require(invalid&&NpcEquipmentRandom.Snapshot().SequenceEqual(original),"Malformed unarmed binding was not rejected atomically");
        }
        finally{target.Corpse?.Delete();target.Delete();}
    }
    private static void Check()
    {
        var p=Owner();var pet=Pet(p);Clocks(pet);Damage(p,pet);
        NpcEquipmentRandom.Set(pet,SourceRandom.InitLow(173));NpcEquipmentRandom.Raw(pet);var state=NpcEquipmentRandom.State(pet);
        Require(state.Unarmed&&state.Item==0&&NpcEquipmentRandom.Snapshot().Contains(state),"Unarmed stream did not enter persistence");
        PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,pet.Serial.Value,state.Seed,0)));
        Log.Information("Skeleton combat: FP273..546,5percent intrinsic,actual Strike/Enchant and owner exclusion,source480/760ms clock/slows,atomic stream persistence and cross-facet overflow preflight pass;ordered contact/AI remains separate");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var pet=(RaisedSkeleton)World.FindMobile((Serial)s.Actor);var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        var state=NpcEquipmentRandom.State(pet);Require(pet.ControlMaster==p&&pet.Map==Map.Internal&&state is{Unarmed:true,Item:0}&&state.Seed==s.Seed&&pet.SourceAttackClock==(12,19),"Saved unarmed actor/stream/clock or offline replay");
        ulong seed=s.Seed;uint expected=SourceRandom.Roll(ref seed);Require(NpcEquipmentRandom.Raw(pet)==expected&&NpcEquipmentRandom.State(pet).Seed==seed,"Unarmed stream did not resume exactly once");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Seed=seed,Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner();network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));
        var account=new Account("skeleton-combat-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=p;network.Account=account;network.Mobile=p;p.NetState=network;
        var pet=Pet(p);pet.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);pet.AIObject.AITimer.Stop();
        var target=PoisonDaggerChecks.Target(p);target.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);target.SetResistance(ResistanceType.Physical,0);TimedEffects.PreventHealing(target,TimedEffects.Now);
        NpcEquipmentRandom.Set(pet,SourceRandom.InitLow(0));ulong expectedSeed=SourceRandom.InitLow(0);SourceRandom.Limited(ref expectedSeed,100);
        int expected=NpcEquipmentDamage.Roll256(pet.UnarmedPhysical,n=>(int)SourceRandom.Limited(ref expectedSeed,n));if(SourceRandom.Roll(ref expectedSeed)%100<5){expected*=2;}
        int before=SkillHealth.Balance256(target);bool early=false;
        void Clean(){pet.Delete();target.Corpse?.Delete();target.Delete();p.Delete();}
        FixtureClock.NextLoop("skeleton-combat-live-result",()=>
        {
            try
            {
                Require(pet.BeginMelee(target)==TimeSpan.FromMilliseconds(760),"Actual A1 cycle was not760ms");Log.Information("skeleton-combat-live: ready");
                Timer.StartTimer(TimeSpan.FromMilliseconds(400),()=>
                {try{Require(SkillHealth.Balance256(target)==before,"A1 hit before the source action frame12");early=true;}catch(Exception e){Clean();Log.Error(e,"skeleton-combat-live-result: failed");}});
                Timer.StartTimer(TimeSpan.FromMilliseconds(1000),()=>
                {
                    try
                    {
                        Require(early&&before-SkillHealth.Balance256(target)==expected&&NpcEquipmentRandom.State(pet).Seed==expectedSeed&&network.Running,"Actual480ms release/FP packet/source draw order");
                        Clean();Log.Information("skeleton-combat-live-result: passed");
                    }
                    catch(Exception e){Clean();Log.Error(e,"skeleton-combat-live-result: failed");}
                });
            }
            catch{Clean();throw;}
        });
    }
}
