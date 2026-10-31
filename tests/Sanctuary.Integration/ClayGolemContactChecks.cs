// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ClayGolemContactChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ClayGolemContactChecks));
    private sealed record Saved(uint Owner,uint Actor,ulong Seed,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"clay-golem-contact-fixtures.json");
    public static void Configure(){Register("clay-golem-contact-check",Check);Register("clay-golem-contact-reload-check",Reload);}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Clay prepared start/contact/block/health/return",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureClayContact";
        foreach(var pair in new[]{("necromancer.claygolem",4),("necromancer.golemmastery",20)})
        {while(ClassCreation.Profiles.GetRank(p.Serial,pair.Item1)<pair.Item2){Require(ClassCreation.Profiles.TryTrain(p.Serial,pair.Item1,out _),"Train "+pair.Item1);}}
        return p;
    }
    private static ClayGolemBody Pet(PlayerMobile p)
    {
        Require(NecromancerSummons.Create(p,CombatAbility.ClayGolem,Serial.Zero,new(p.X+1,p.Y,p.Z)),"Learned Clay creation");
        var pet=NecromancerGolems.For(p).OfType<ClayGolemBody>().Single();pet.AIObject.AITimer.Stop();return pet;
    }
    private static GraveyardSkeleton Target(PlayerMobile p)
    {
        var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);t.SetResistance(ResistanceType.Physical,0);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static IronGolemContact.Prepared Prepare(PlayerMobile p,ClayGolemBody pet,BaseCreature target)
    {
        NpcEquipmentRandom.Set(pet,SourceRandom.InitLow(0));return UnarmedSummonContact.Prepare(pet,p,target,UnarmedSummonContact.Require(pet,p,target));
    }
    private static void Check()
    {
        var p=Owner();var pet=Pet(p);pet.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
        var target=Target(p);var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try
        {
            int before=SkillHealth.Balance256(target);var prepared=Prepare(p,pet,target);var packet=prepared.Impact.Packet;ulong seed=NpcEquipmentRandom.State(pet).Seed;
            Require((packet.ResultFlags&1)!=0&&packet.Physical256>4*256&&SkillHealth.Balance256(target)==before,"Prepared GolemMastery20 result rolled incorrectly or committed HP early");
            target.SetResistance(ResistanceType.Physical,100);
            var result=UnarmedSummonContact.Release(pet,p,target,prepared,TimedEffects.Now);
            Require(result.Exit==MonsterContactExit.Finished&&before-SkillHealth.Balance256(target)==packet.Total256&&NpcEquipmentRandom.State(pet).Seed==seed&&prepared.Consumed,"Captured mitigation was recomputed or source RNG replayed on release");
            bool twice=false;try{UnarmedSummonContact.Release(pet,p,target,prepared,TimedEffects.Now);}catch(InvalidOperationException){twice=true;}
            Require(twice,"Prepared NPC packet released twice");
            target.SetResistance(ResistanceType.Physical,0);SkillHealth.SetBalance256(target,target.HitsMax*256);
            prepared=Prepare(p,pet,target);before=SkillHealth.Balance256(target);seed=NpcEquipmentRandom.State(pet).Seed;
            var dropped=IronGolemContact.Release(pet,p,target,prepared,_=>false,TimedEffects.Now);
            Require(dropped.Exit==MonsterContactExit.OutOfRangeFreed&&SkillHealth.Balance256(target)==before&&NpcEquipmentRandom.State(pet).Seed==seed,"Out-of-range stored packet hit or rerolled");
            var shields=SourceBlocking.Snapshot();
            try
            {
                string source=CreatureProfiles.Definition(target).Reference;
                SourceBlocking.Restore(shields.Where(x=>x.Actor!=target.Serial.Value).Append(new(target.Serial.Value,source,1)).ToArray());
                SourceBlocking.BlockRoll=_=>0;Require(SourceBlocking.TryRate(target,out int rate,out _)&&rate>0,"Actual recipient shield missing");
                prepared=Prepare(p,pet,target);Require((prepared.Impact.Packet.ResultFlags&16)!=0&&(prepared.Impact.Packet.ResultFlags&1)==0&&prepared.Impact.Packet.Total256==0,"NPC shield did not block before damage fill");
                before=SkillHealth.Balance256(target);UnarmedSummonContact.Release(pet,p,target,prepared,TimedEffects.Now);
                Require(SkillHealth.Balance256(target)==before,"Blocked Clay attack changed recipient HP");
            }
            finally{SourceBlocking.Restore(shields);SourceBlocking.BlockRoll=_=>99;}
            SkillHealth.SetBalance256(target,target.HitsMax*256);
            Require(NecromancerEffects.Apply(p,target,CombatAbility.LifeTap,1),"Actual Life Tap curse");
            PaladinAuras.ElementalWeapon(new(target.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,4,TimedEffects.Now.AddHours(1),DamagePercent:250));
            prepared=Prepare(p,pet,target);int physical=prepared.Impact.Packet.Physical256;
            int healing=NecromancerEffects.LifeTapHealing256(physical,50),returned=(physical*250/100)*75/100+4*256*75/100,start=returned-healing/2;
            Require(pet.PhysicalResistance==25&&PaladinThorns.FlatDamage(target)==4,"Pinned flat Thorns component");
            Require(start>256&&start<returned&&start+healing<SkillHealth.Maximum256(pet),"Ordered heal/return fixture bounds");
            SkillHealth.SetBalance256(pet,start);before=SkillHealth.Balance256(target);
            UnarmedSummonContact.Release(pet,p,target,prepared,TimedEffects.Now);
            Require(pet.Alive&&SkillHealth.Balance256(pet)==start+healing-returned&&before-SkillHealth.Balance256(target)==prepared.Impact.Packet.Total256,$"Life Tap/HP/percentage-return differs:alive={pet.Alive},HP={SkillHealth.Balance256(pet)}/{start+healing-returned},targetLoss={before-SkillHealth.Balance256(target)}/{prepared.Impact.Packet.Total256},physical={physical},heal={healing},return={returned},resist={pet.PhysicalResistance}");
            PaladinAuras.Clear(target);NecromancerEffects.ClearOrdinaryCurse(target);
            // Real actor hook: capture, then interrupt before the source action.
            CreatureProfiles.Session(pet).RecoveryUntil=0;
            typeof(SkeletonGuardian).GetField("_nextAttack",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pet,0L);
            NpcEquipmentRandom.Set(pet,SourceRandom.InitLow(0));before=SkillHealth.Balance256(target);pet.BeginMelee(target);
            Require(pet.PendingContact!=null&&SkillHealth.Balance256(target)==before,"Actual A1 did not capture without HP");
            var canceled=pet.PendingContact;seed=NpcEquipmentRandom.State(pet).Seed;pet.InterruptForRecovery();
            Require(pet.PendingContact==null&&canceled.Consumed&&SkillHealth.Balance256(target)==before&&NpcEquipmentRandom.State(pet).Seed==seed,"Recovery cancellation replayed or retained a packet");
            CreatureProfiles.Session(pet).RecoveryUntil=0;
            typeof(SkeletonGuardian).GetField("_nextAttack",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pet,0L);
            NpcEquipmentRandom.Set(pet,SourceRandom.InitLow(0));before=SkillHealth.Balance256(target);pet.BeginMelee(target);
            prepared=pet.PendingContact;seed=NpcEquipmentRandom.State(pet).Seed;
            Require(prepared!=null,"Second real Clay contact did not capture");
            typeof(ClayGolemBody).GetMethod("ResolveMeleeContact",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pet,[target]);
            pet.InterruptForRecovery();
            Require(pet.PendingContact==null&&prepared.Consumed&&before-SkillHealth.Balance256(target)==prepared.Impact.Packet.Total256
                &&NpcEquipmentRandom.State(pet).Seed==seed&&TimedEffects.Slow(target,TimedEffects.Now)==pet.ReactiveSlowPercent,"Actual Clay release lost captured packet or retained outgoing-slow adapter");
            TimedEffects.Clear(target);
            // Suspension cancels an actual pending start without replaying its RNG or HP.
            CreatureProfiles.Session(pet).RecoveryUntil=0;
            typeof(SkeletonGuardian).GetField("_nextAttack",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pet,0L);
            NpcEquipmentRandom.Set(pet,SourceRandom.InitLow(0));pet.BeginMelee(target);canceled=pet.PendingContact;seed=NpcEquipmentRandom.State(pet).Seed;before=SkillHealth.Balance256(target);
            Require(canceled!=null,"Suspend fixture did not start a contact");pet.SuspendActions();
            Require(canceled.Consumed&&pet.PendingContact==null&&NpcEquipmentRandom.State(pet).Seed==seed&&SkillHealth.Balance256(target)==before,"Suspension retained/replayed pending Clay damage");
            // The explicit preflight fallback preserves unmapped native mobs.
            var native=new Dog{NoKillAwards=true,FightMode=FightMode.Closest};native.MoveToWorld(target.Location,p.Map);native.AIObject.AITimer.Stop();
            try
            {
                typeof(SkeletonGuardian).GetField("_nextAttack",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pet,0L);pet.BeginMelee(native);
                Require(pet.PendingContact==null&&pet.LegacyContactReason!=null,"Unmapped native recipient silently claimed ordered coverage");pet.InterruptForRecovery();
            }
            finally{native.Delete();}
            // Exact-zero health enters death only through the shared return.
            SkillHealth.SetBalance256(target,target.HitsMax*256);prepared=Prepare(p,pet,target);int exact=prepared.Impact.Packet.Total256;
            SkillHealth.SetBalance256(target,exact);UnarmedSummonContact.Release(pet,p,target,prepared,TimedEffects.Now);
            Require(!target.Alive&&target.LastKiller==pet&&ClassLoot.Killer(target)==p,"Ordered exact-life kill lost native death or owner credit");
            NpcEquipmentRandom.Set(pet,SourceRandom.InitLow(173));var saved=new Saved(p.Serial.Value,pet.Serial.Value,NpcEquipmentRandom.State(pet).Seed,0);
            PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
            Log.Information("Clay ordered contact:capture/no early HP,GolemMastery20,immutable mitigation,no reroll/replay,range free,NPC shield,Life Tap-before-lethal-return,recovery/suspend cancel,actual release/retained outgoing slow,flagged native fallback and exact-zero owner kill pass");
        }
        finally{SourceBlocking.BlockRoll=block;target.Corpse?.Delete();target.Delete();}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var pet=(ClayGolemBody)World.FindMobile((Serial)s.Actor);var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        Require(pet.ControlMaster==p&&pet.Map==Map.Internal&&pet.PendingContact==null&&NpcEquipmentRandom.State(pet).Seed==s.Seed,"Saved parked actor replayed a canceled contact or lost source stream");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
