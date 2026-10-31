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

public static class BloodGolemContactChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BloodGolemContactChecks));
    private sealed record Saved(uint Owner,uint Actor,ulong Seed,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blood-golem-contact-fixtures.json");
    public static void Configure(){Register("blood-golem-contact-check",Check);Register("blood-golem-contact-reload-check",Reload);}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Blood prepared start/contact/block/health/return",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureBloodContact";
        void Train(string id,int rank){TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string prior in row.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(prior,1);}}while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}}
        Train("necromancer.bloodgolem",4);Train("necromancer.golemmastery",20);
        return p;
    }
    private static BloodGolemBody Pet(PlayerMobile p)
    {
        Require(NecromancerSummons.Create(p,CombatAbility.BloodGolem,Serial.Zero,new(p.X+1,p.Y,p.Z)),"Learned Blood creation");
        var pet=NecromancerGolems.For(p).OfType<BloodGolemBody>().Single();pet.AIObject.AITimer.Stop();return pet;
    }
    private static GraveyardSkeleton Target(PlayerMobile p)
    {
        var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);t.SetResistance(ResistanceType.Physical,0);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static IronGolemContact.Prepared Prepare(PlayerMobile p,BloodGolemBody pet,BaseCreature target)
    {
        NpcEquipmentRandom.Set(pet,SourceRandom.InitLow(0));return UnarmedSummonContact.Prepare(pet,p,target,UnarmedSummonContact.Require(pet,p,target));
    }
    private sealed class Supplied(int[] input):BloodHealingServices
    {
        internal readonly int[] Hp=new int[2],Max=new int[2],Overlays=new int[2];internal readonly System.Collections.Generic.List<(int,int)> Writes=[];
        internal void Init(){int injury=input[5];Max[0]=injury==5?3000001:8001;Max[1]=injury==5?4000001:12005;Hp[0]=injury==0||injury==2?Max[0]:injury==4?Max[0]-1:1001;Hp[1]=injury==0||injury==3?Max[1]:injury==4?Max[1]-257:2003;}
        internal override bool Attacker=>(input[8]&1)!=0;internal override bool Target=>(input[8]&2)!=0;internal override bool Damage=>(input[8]&4)!=0;
        internal override int TargetKind=>input[6];internal override int Drain=>input[2];internal override int Alignment=>input[3];internal override bool Owner=>input[4]!=0;
        internal override bool OwnerDead=>input[4]==2;internal override bool AttackerDead=>input[7]!=0;internal override int Rank=>input[0];internal override int Share=>30;
        internal override int Life(bool owner)=>Hp[owner?0:1];internal override int Maximum(bool owner)=>Max[owner?0:1];
        internal override void SetLife(bool owner,int value){int id=owner?0:1;Hp[id]=value;Writes.Add((id,value));}internal override void Overlay(bool owner)=>Overlays[owner?0:1]++;
    }
    private static void Source()
    {
        using var heal=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blood-golem-healing-source-cases.json")));int count=0;
        foreach(var row in heal.RootElement.EnumerateArray())
        {
            var input=row[0].EnumerateArray().Select(x=>x.GetInt32()).ToArray();var s=new Supplied(input);s.Init();
            int result=BloodGolemHealing.Execute(s,input[1],input[5]==5?2000000:10001);var want=row[1].EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            Require(new[]{result,s.Hp[0],s.Hp[1],s.Overlays[0],s.Overlays[1]}.SequenceEqual(want)&&s.Writes.SequenceEqual(row[2].EnumerateArray().Select(x=>(x[0].GetInt32(),x[1].GetInt32()))),"Complete healing callback/write order "+count);count++;
        }
        Require(count==15559&&BloodGolemHealing.Conversion(12)==130&&new SkillValues(SkillMechanics.For(CombatAbility.BloodGolem),_=>12,_=>0,99).Diminishing(1,2)==129,"Healing source coverage or explicit legacy/Fog rounding distinction");
        using var incoming=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blood-golem-incoming-source-cases.json")));count=0;
        foreach(var row in incoming.RootElement.EnumerateArray())
        {
            int V(int i)=>row[0][i].GetInt32();var got=BloodGolemHealing.Incoming(V(0)!=0,V(0)==2,V(1),V(2),V(3),V(4)!=0,V(5)!=0,V(5)==1,V(6));
            Require(got.Result==row[1][0].GetInt32()&&got.OwnerLife==row[1][1].GetInt32()&&got.Damage==row[1][2].GetInt32()&&row[2].GetArrayLength()==(got.Written?1:0),"Complete incoming callback "+count);count++;
        }
        Require(count==3456,"Incomplete incoming source contexts");BloodGolemHealing.RequireCurrent();
    }
    private static void Healing(PlayerMobile p,BloodGolemBody pet)
    {
        var target=new GraveyardZombie{Name="PoisonDaggerFixtureBloodHeal",NoKillAwards=true,Frozen=true,CantWalk=true};target.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();target.SetHits(50000);target.Hits=target.HitsMax;target.SetResistance(ResistanceType.Physical,0);TimedEffects.PreventHealing(target,TimedEffects.Now);
        try
        {
            Require(GemCombat.Drain(target)==100&&BloodGolemHealing.Alignment(target)==0,"Actual healing recipient drain/alignment");
            foreach(var missing in new[]{(10000,20000),(0,20000),(10000,0),(1,257)})
            {
                SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-missing.Item1);SkillHealth.SetBalance256(pet,SkillHealth.Maximum256(pet)-missing.Item2);
                var prepared=Prepare(p,pet,target);int packet=prepared.Impact.Packet.Physical256,healing=SourcePercentage.Calculate(packet,108,100);
                int ownerShare=Math.Min(missing.Item1,SourcePercentage.Calculate(healing,30,100)),petShare=Math.Min(missing.Item2,healing-ownerShare),extra=Math.Min(missing.Item1-ownerShare,healing-ownerShare-petShare);
                int owner=SkillHealth.Balance256(p),body=SkillHealth.Balance256(pet),victim=SkillHealth.Balance256(target);ulong seed=NpcEquipmentRandom.State(pet).Seed;
                Require(healing>258&&owner==SkillHealth.Maximum256(p)-missing.Item1&&body==SkillHealth.Maximum256(pet)-missing.Item2,"Healing started before contact");
                UnarmedSummonContact.Release(pet,p,target,prepared,TimedEffects.Now);
                Require(SkillHealth.Balance256(p)==owner+ownerShare+extra&&SkillHealth.Balance256(pet)==body+petShare&&victim-SkillHealth.Balance256(target)==prepared.Impact.Packet.Total256&&NpcEquipmentRandom.State(pet).Seed==seed,"Actual FP owner/pet/overflow healing or packet order");
            }
            int hp=SkillHealth.Balance256(p);AOS.Damage(pet,target,7,false,100,0,0,0,0,weaponDamage:true);AOS.Damage(pet,target,7,false,100,0,0,0,0,archer:true,weaponDamage:true);
            Require(SkillHealth.Balance256(p)==hp,"Current Param5zero incoming hits transferred owner life");
            SkillHealth.SetBalance256(pet,SkillHealth.Maximum256(pet));
            int ownerBefore=SkillHealth.Balance256(p),bodyBefore=SkillHealth.Balance256(pet);
            var blocked=IronGolemContact.Prepare(pet,target,UnarmedSummonContact.Require(pet,p,target),()=>16);UnarmedSummonContact.Release(pet,p,target,blocked,TimedEffects.Now);
            Require(SkillHealth.Balance256(p)==ownerBefore&&SkillHealth.Balance256(pet)==bodyBefore,"Blocked contact executed healing");
        }
        finally{target.Corpse?.Delete();target.Delete();}
    }
    private static void Check()
    {
        Source();var p=Owner();var pet=Pet(p);pet.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
        var target=Target(p);var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try
        {
            Healing(p,pet);
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
                Require(SkillHealth.Balance256(target)==before,"Blocked Blood attack changed recipient HP");
            }
            finally{SourceBlocking.Restore(shields);SourceBlocking.BlockRoll=_=>99;}
            SkillHealth.SetBalance256(target,target.HitsMax*256);
            Require(NecromancerEffects.Apply(p,target,CombatAbility.LifeTap,1),"Actual Life Tap curse");
            PaladinAuras.ElementalWeapon(new(target.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,4,TimedEffects.Now.AddHours(1),DamagePercent:250));
            prepared=Prepare(p,pet,target);int physical=prepared.Impact.Packet.Physical256;
            int healing=NecromancerEffects.LifeTapHealing256(physical,50),returned=physical*250/100+4*256,start=returned-healing/2;
            Require(pet.PhysicalResistance==0&&PaladinThorns.FlatDamage(target)==4,"Pinned flat Thorns component");
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
            Require(prepared!=null,"Second real Blood contact did not capture");
            typeof(BloodGolemBody).GetMethod("ResolveMeleeContact",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pet,[target]);
            pet.InterruptForRecovery();
            Require(pet.PendingContact==null&&prepared.Consumed&&before-SkillHealth.Balance256(target)==prepared.Impact.Packet.Total256
                &&NpcEquipmentRandom.State(pet).Seed==seed,"Actual Blood release lost captured packet ");
            TimedEffects.Clear(target);
            // Suspension cancels an actual pending start without replaying its RNG or HP.
            CreatureProfiles.Session(pet).RecoveryUntil=0;
            typeof(SkeletonGuardian).GetField("_nextAttack",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pet,0L);
            NpcEquipmentRandom.Set(pet,SourceRandom.InitLow(0));pet.BeginMelee(target);canceled=pet.PendingContact;seed=NpcEquipmentRandom.State(pet).Seed;before=SkillHealth.Balance256(target);
            Require(canceled!=null,"Suspend fixture did not start a contact");pet.SuspendActions();
            Require(canceled.Consumed&&pet.PendingContact==null&&NpcEquipmentRandom.State(pet).Seed==seed&&SkillHealth.Balance256(target)==before,"Suspension retained/replayed pending Blood damage");
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
            Log.Information("Blood ordered contact:capture/no early HP,GolemMastery20,immutable mitigation,no reroll/replay,range free,NPC shield,Life Tap-before-lethal-return,recovery/suspend cancel,actual release,flagged native fallback and exact-zero owner kill pass");
        }
        finally{SourceBlocking.BlockRoll=block;target.Corpse?.Delete();target.Delete();}
    }
    private static void Reload()
    {
        Source();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var pet=(BloodGolemBody)World.FindMobile((Serial)s.Actor);var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        Require(pet.ControlMaster==p&&pet.Map==Map.Internal&&pet.PendingContact==null&&NpcEquipmentRandom.State(pet).Seed==s.Seed,"Saved parked actor replayed a canceled contact or lost source stream");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
