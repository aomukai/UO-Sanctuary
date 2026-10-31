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

public static class ClayGolemCombatChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ClayGolemCombatChecks));
    private sealed record Saved(uint Owner,uint Actor,ulong Seed,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"clay-golem-combat-fixtures.json");
    public static void Configure()
    {
        Register("clay-golem-combat-check",Check);Register("clay-golem-combat-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("clay-golem-combat-live","Connected source-timed Clay attack",_=>Core.LoopContext.Post(()=>
            FixtureNetwork.Connected("clay-golem-combat-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Learned Clay unarmed FP damage,critical,clock and saved stream",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureClayCombat";
        while(ClassCreation.Profiles.GetRank(p.Serial,"necromancer.claygolem")<4)
        {Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.claygolem",out _),"Train Clay Golem4");}
        return p;
    }
    private static ClayGolemBody Pet(PlayerMobile p)
    {
        Require(NecromancerSummons.Create(p,CombatAbility.ClayGolem,Serial.Zero,new(p.X+1,p.Y,p.Z)),"Actual learned Clay creation");
        var pet=NecromancerGolems.For(p).OfType<ClayGolemBody>().Single();pet.AIObject.AITimer.Stop();return pet;
    }
    private static void Clocks(ClayGolemBody pet)
    {
        Require(pet.SourceAttackClock==(10,18)&&pet.CombatProfile.AttackMs==720,"Pinned232speed/action9/end16 clock");
        TimedEffects.Slow(pet,50,TimedEffects.Now);Require(pet.SourceAttackClock==(20,36),"50percent slow must use source rate then frame planner");
        TimedEffects.Clear(pet);Require(pet.SourceAttackClock==(10,18),"Cleared slow changed baseline");
        Require(DruidWolfGeometry.Clock("A1",SourceAttackRate.Speed(232,0,275),"claygolem-animation")== (6,11),"175percent final attack-rate cap");
    }
    private static void Damage(PlayerMobile p,ClayGolemBody pet)
    {
        var target=PoisonDaggerChecks.Target(p);target.SetResistance(ResistanceType.Physical,0);TimedEffects.PreventHealing(target,TimedEffects.Now);
        try
        {
            Require(NpcEquipmentCombat.Context(pet)==null&&NpcEquipmentDamage.Capture(pet)==new NpcPhysicalStats(2,5,0,105,0,0,false),"Unequipped Clay source stats");
            var chances=NpcEquipmentCritical.Capture(pet);Require(chances==new NpcCriticalChances(0,0,0,5),"Source intrinsic5percent or inherited owner critical");
            var low=NpcEquipmentDamage.Roll(pet,target,_=>0,()=>99);var high=NpcEquipmentDamage.Roll(pet,target,n=>n-1,()=>99);
            Require(low.Damage.Physical256==1049&&high.Damage.Physical256==2623&&!low.PhysicalCritical&&!low.Damage.Intrinsic,"Exclusive FP range1049..2623");
            var crit=NpcEquipmentDamage.Roll(pet,target,_=>0,()=>0);
            Require(crit.Damage.Physical256==2098&&crit.Damage.Intrinsic&&!crit.PhysicalCritical,"Intrinsic critical doubles NPC damage only once");
            int before=SkillHealth.Balance256(target);Require(NpcEquipmentDamage.TryResolve(pet,target,out _,_=>0,()=>99)&&before-SkillHealth.Balance256(target)==1049,"Actual fractional unarmed damage");
            var impact=NpcEquipmentImpact.Prepare(pet,target,5,_=>0,()=>0);Require(impact.Packet.Physical256==2098&&impact.Packet.Total256==2098,"Unarmed ordinary allocation route");
            PoisonDaggerChecks.Named(p,"Bloodfist");PoisonDaggerChecks.Named(p,"Wormskull");
            Require(pet.Rank==4&&pet.Mastery==0&&NpcEquipmentDamage.Capture(pet)==new NpcPhysicalStats(2,5,0,105,0,0,false)
                &&NpcEquipmentCritical.Capture(pet)==chances&&NpcEquipmentPoison.Capture(pet).Stats==new MissileSourceStats()&&pet.SourceAttackClock==(10,18),"Owner gear/ranks/poison/IAS leaked to an existing pet");
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
            Require(before-SkillHealth.Balance256(target)==physical*(intrinsic?2:1)&&NpcEquipmentRandom.State(pet).Seed==expected,"Actual Clay Strike bypassed the shared packet/stream");
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
        Log.Information("Clay combat: FP1049..2623,5percent intrinsic,actual Strike/Enchant and owner exclusion,source400/720ms clock/slows,atomic stream persistence pass;contact controls are separate;AI remains open");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var pet=(ClayGolemBody)World.FindMobile((Serial)s.Actor);var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        var state=NpcEquipmentRandom.State(pet);Require(pet.ControlMaster==p&&pet.Map==Map.Internal&&state is{Unarmed:true,Item:0}&&state.Seed==s.Seed&&pet.SourceAttackClock==(10,18),"Saved unarmed actor/stream/clock or offline replay");
        ulong seed=s.Seed;uint expected=SourceRandom.Roll(ref seed);Require(NpcEquipmentRandom.Raw(pet)==expected&&NpcEquipmentRandom.State(pet).Seed==seed,"Unarmed stream did not resume exactly once");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Seed=seed,Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner();network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));
        var account=new Account("clay-golem-combat-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=p;network.Account=account;network.Mobile=p;p.NetState=network;
        var pet=Pet(p);pet.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);pet.AIObject.AITimer.Stop();
        var target=PoisonDaggerChecks.Target(p);target.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);target.SetResistance(ResistanceType.Physical,0);TimedEffects.PreventHealing(target,TimedEffects.Now);
        NpcEquipmentRandom.Set(pet,SourceRandom.InitLow(0));ulong expectedSeed=SourceRandom.InitLow(0);SourceRandom.Limited(ref expectedSeed,100);
        int expected=NpcEquipmentDamage.Roll256(pet.UnarmedPhysical,n=>(int)SourceRandom.Limited(ref expectedSeed,n));if(SourceRandom.Roll(ref expectedSeed)%100<5){expected*=2;}
        int before=SkillHealth.Balance256(target);bool early=false;
        var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        void Clean(){SourceBlocking.BlockRoll=block;pet.Delete();Require(NpcEquipmentRandom.State(pet)==null,"Deleted Clay retained source stream");target.Corpse?.Delete();target.Delete();p.Delete();}
        FixtureClock.NextLoop("clay-golem-combat-live-result",()=>
        {
            try
            {
                Require(pet.BeginMelee(target)==TimeSpan.FromMilliseconds(720),"Actual A1 cycle was not720ms");
                Require(pet.PendingContact!=null&&pet.PendingContact.Impact.Packet.Total256==expected&&NpcEquipmentRandom.State(pet).Seed==expectedSeed,"A1 did not prepare the exact packet/stream at start");Log.Information("clay-golem-combat-live: ready");
                Timer.StartTimer(TimeSpan.FromMilliseconds(380),()=>
                {try{Require(SkillHealth.Balance256(target)==before,"A1 hit before the source action frame10");early=true;}catch(Exception e){Clean();Log.Error(e,"clay-golem-combat-live-result: failed");}});
                // Observe after action400ms and before the next cycle720ms, whose
                // later attacks must not contaminate this first-cycle observation.
                Timer.StartTimer(TimeSpan.FromMilliseconds(520),()=>
                {
                    try
                    {
                        Require(early&&before-SkillHealth.Balance256(target)==expected&&NpcEquipmentRandom.State(pet).Seed==expectedSeed&&network.Running, $"Actual400ms release/FP packet/source draw order: early={early}, loss={before-SkillHealth.Balance256(target)}/{expected}, seed={NpcEquipmentRandom.State(pet).Seed}/{expectedSeed}, network={network.Running}");
                        Clean();Log.Information("clay-golem-combat-live-result: passed");
                    }
                    catch(Exception e){Clean();Log.Error(e,"clay-golem-combat-live-result: failed");}
                });
            }
            catch{Clean();throw;}
        });
    }
}
