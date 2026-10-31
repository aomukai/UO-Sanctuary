// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class FireGolemCombatChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FireGolemCombatChecks));
    private sealed record Saved(uint Owner,uint Actor,ulong Seed,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"fire-golem-combat-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("fire-golem-combat-check",Check);Register("fire-golem-combat-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("fire-golem-combat-live","Connected Fire packet and G3 release",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("fire-golem-combat-live-result",Live)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Fire accrued elemental packet,critical,contact and saved stream",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureFireCombat";
        void Train(string id,int rank){TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string prior in row.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(prior,1);}}while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}}
        Train("necromancer.firegolem",4);return p;
    }
    private static FireGolemBody Pet(PlayerMobile p)
    {
        Require(NecromancerSummons.Create(p,CombatAbility.FireGolem,Serial.Zero,new(p.X+1,p.Y,p.Z)),"Actual learned Fire creation");
        var b=NecromancerGolems.For(p).OfType<FireGolemBody>().Single();b.AIObject.AITimer.Stop();return b;
    }
    private static GraveyardSkeleton Target(PlayerMobile p)
    {
        var target=PoisonDaggerChecks.Target(p);target.SetResistance(ResistanceType.Physical,0);target.SetResistance(ResistanceType.Fire,0);
        TimedEffects.PreventHealing(target,TimedEffects.Now);return target;
    }
    private static int Expected(FireGolemBody b,ref ulong seed,bool hit)
    {
        if(hit){SourceRandom.Limited(ref seed,100);}
        ulong next=seed;int physical=NpcEquipmentDamage.Roll256(b.UnarmedPhysical,n=>(int)SourceRandom.Limited(ref next,n));
        var fire=b.InnateFire256;int elemental=fire.Min+(int)SourceRandom.Limited(ref next,fire.Max-fire.Min);
        bool critical=SourceRandom.Roll(ref next)%100<5;seed=next;return checked((physical+elemental)*(critical?2:1));
    }
    private static void Damage(PlayerMobile p,FireGolemBody b)
    {
        var target=Target(p);
        try
        {
            Require(NpcEquipmentCombat.Context(b)==null&&NpcEquipmentDamage.Capture(b)==new NpcPhysicalStats(10,27,0,0,0,0,false)&&b.InnateFire256==(9472,14592),"Captured Fire physical/whole-point innate units");
            var chances=NpcEquipmentCritical.Capture(b);Require(chances==new NpcCriticalChances(0,0,0,5),"Fire intrinsic or inherited owner critical");
            var low=NpcEquipmentDamage.Roll(b,target,_=>0,()=>99);var high=NpcEquipmentDamage.Roll(b,target,n=>n-1,()=>99);
            Require(low.Damage.Physical256==2560&&high.Damage.Physical256==6911&&low.Damage.Elements.Fire==37&&high.Damage.Elements.Fire==14591/256.0,"Exclusive combined FP ranges");
            var critical=NpcEquipmentDamage.Roll(b,target,_=>0,()=>0);
            Require(critical.Damage.Physical256==5120&&critical.Damage.Elements.Fire==74&&critical.Damage.Intrinsic&&!critical.PhysicalCritical,"Intrinsic critical must double both channels once");
            int life=SkillHealth.Balance256(target);Require(NpcEquipmentDamage.TryResolve(b,target,out _,_=>0,()=>99)&&life-SkillHealth.Balance256(target)==12032,"Native combined fractional damage");
            var impact=NpcEquipmentImpact.Prepare(b,target,5,_=>0,()=>0);Require(impact.Packet.Total256==24064&&impact.Packet.Physical256==5120&&impact.Packet.Fire256==18944,"Prepared combined allocation");
            PoisonDaggerChecks.Named(p,"Bloodfist");PoisonDaggerChecks.Named(p,"Wormskull");
            Require(b.Rank==4&&NpcEquipmentDamage.Capture(b)==new NpcPhysicalStats(10,27,0,0,0,0,false)&&b.InnateFire256==(9472,14592)
                &&NpcEquipmentPoison.Capture(b).Stats==new MissileSourceStats()&&NpcEquipmentCritical.Capture(b)==chances,"Owner gear/ranks/poison/critical leaked into existing Fire");
            var caster=new PlayerMobile{Player=true,Body=0x190};ClassCreation.Profiles.Assign(caster,DiabloClass.Sorceress);
            try
            {
                SorceressEffects.CastEnchant(caster,b,SkillMechanics.Values(caster,CombatAbility.Enchant,1));var enchant=SorceressEffects.Enchantment(b);
                var enchanted=NpcEquipmentDamage.Roll(b,target,_=>0,()=>99);var doubled=NpcEquipmentDamage.Roll(b,target,_=>0,()=>0);
                Require(enchant!=null&&enchanted.Damage.Elements.Fire==(9472+enchant.Min256)/256.0&&doubled.Damage.Elements.Fire==(9472+enchant.Min256)/128.0,"Enchant must accrue before one fire roll and intrinsic critical");
            }
            finally{SorceressEffects.Profiles.Clear(b);caster.Delete();}
            NpcEquipmentRandom.Set(b,SourceRandom.InitLow(0));ulong seed=SourceRandom.InitLow(0);int expected=Expected(b,ref seed,false);life=SkillHealth.Balance256(target);
            typeof(SkeletonGuardian).GetMethod("Strike",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(b,[target]);
            Require(life-SkillHealth.Balance256(target)==expected&&NpcEquipmentRandom.State(b).Seed==seed,"Fire Strike repeated the historical follow-up fire hit or changed the stream");
        }
        finally{target.Corpse?.Delete();target.Delete();}
    }
    private static void Contact(PlayerMobile p,FireGolemBody b)
    {
        var target=Target(p);target.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);
        var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try
        {
            var rules=UnarmedSummonContact.Require(b,p,target);int life=SkillHealth.Balance256(target);
            NpcEquipmentRandom.Set(b,SourceRandom.InitLow(0));var prepared=UnarmedSummonContact.Prepare(b,p,target,rules);
            Require(prepared.Impact.Packet.Fire256>0&&SkillHealth.Balance256(target)==life,"Start allocation must retain fire without early HP loss");
            int total=prepared.Impact.Packet.Total256;ulong seed=NpcEquipmentRandom.State(b).Seed;
            target.SetResistance(ResistanceType.Fire,95);
            UnarmedSummonContact.Release(b,p,target,prepared,TimedEffects.Now);
            Require(life-SkillHealth.Balance256(target)==total&&NpcEquipmentRandom.State(b).Seed==seed,"Release changed captured mitigation or rerolled damage");
            int after=SkillHealth.Balance256(target);bool refused=false;
            try{UnarmedSummonContact.Release(b,p,target,prepared,TimedEffects.Now);}catch(InvalidOperationException){refused=true;}
            Require(refused&&SkillHealth.Balance256(target)==after,"Duplicate Fire release repeated damage");
            var cancelled=UnarmedSummonContact.Prepare(b,p,target,rules);cancelled.Cancel();
            refused=false;try{UnarmedSummonContact.Release(b,p,target,cancelled,TimedEffects.Now);}catch(InvalidOperationException){refused=true;}
            Require(refused&&SkillHealth.Balance256(target)==after,"Cancelled Fire packet applied damage");
            target.SetResistance(ResistanceType.Fire,0);SkillHealth.SetBalance256(target,SkillHealth.Maximum256(target));
            NpcEquipmentRandom.Set(b,SourceRandom.InitLow(0));var dropped=UnarmedSummonContact.Prepare(b,p,target,rules);life=SkillHealth.Balance256(target);
            Require(IronGolemContact.Release(b,p,target,dropped,_=>false,TimedEffects.Now).Exit==MonsterContactExit.OutOfRangeFreed&&SkillHealth.Balance256(target)==life,"Out-of-range combined Fire packet applied damage");
            var shields=SourceBlocking.Snapshot();
            try
            {
                string source=CreatureProfiles.Definition(target).Reference;
                SourceBlocking.Restore(shields.Where(x=>x.Actor!=target.Serial.Value).Append(new(target.Serial.Value,source,1)).ToArray());SourceBlocking.BlockRoll=_=>0;
                NpcEquipmentRandom.Set(b,SourceRandom.InitLow(0));var blocked=UnarmedSummonContact.Prepare(b,p,target,rules);
                Require((blocked.Impact.Packet.ResultFlags&16)!=0&&blocked.Impact.Packet.Total256==0&&blocked.Impact.Packet.Fire256==0,"Shield must block before physical/fire fill");
                UnarmedSummonContact.Release(b,p,target,blocked,TimedEffects.Now);Require(SkillHealth.Balance256(target)==life,"Blocked Fire packet applied damage");
            }
            finally{SourceBlocking.Restore(shields);SourceBlocking.BlockRoll=_=>99;}
            Require(NecromancerEffects.Apply(p,target,CombatAbility.LifeTap,1),"Actual Life Tap curse");
            PaladinAuras.ElementalWeapon(new(target.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,4,TimedEffects.Now.AddHours(1),DamagePercent:250));
            NpcEquipmentRandom.Set(b,SourceRandom.InitLow(0));var returned=UnarmedSummonContact.Prepare(b,p,target,rules);
            int physical=returned.Impact.Packet.Physical256,healing=NecromancerEffects.LifeTapHealing256(physical,50),thorns=physical*250/100+4*256,start=thorns-healing/2;
            Require(b.PhysicalResistance==0&&start>256&&start+healing<SkillHealth.Maximum256(b),"Fire Life Tap/Thorns bounds");
            SkillHealth.SetBalance256(b,start);life=SkillHealth.Balance256(target);UnarmedSummonContact.Release(b,p,target,returned,TimedEffects.Now);
            Require(b.Alive&&SkillHealth.Balance256(b)==start+healing-thorns&&life-SkillHealth.Balance256(target)==returned.Impact.Packet.Total256,"Life Tap/Thorns must use physical only and heal before return");
            PaladinAuras.Clear(target);NecromancerEffects.ClearOrdinaryCurse(target);
        }
        finally{PaladinAuras.Clear(target);NecromancerEffects.ClearOrdinaryCurse(target);SourceBlocking.BlockRoll=block;target.Corpse?.Delete();target.Delete();}
    }
    private static void Check()
    {
        var p=Owner();var b=Pet(p);Damage(p,b);Contact(p,b);
        NpcEquipmentRandom.Set(b,SourceRandom.InitLow(173));NpcEquipmentRandom.Raw(b);var state=NpcEquipmentRandom.State(b);
        Require(state.Unarmed&&state.Item==0,"Fire stream needs its actual unarmed binding");
        PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,state.Seed,0)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var b=(FireGolemBody)World.FindMobile((Serial)saved.Actor);var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(b.ControlMaster==p&&b.Map==Map.Internal&&NpcEquipmentRandom.State(b) is{Unarmed:true,Item:0} state&&state.Seed==saved.Seed&&b.InnateFire256==(9472,14592),"Saved Fire binding/stats/offline stream");
        ulong seed=saved.Seed;uint next=SourceRandom.Roll(ref seed);Require(NpcEquipmentRandom.Raw(b)==next&&NpcEquipmentRandom.State(b).Seed==seed,"Fire stream failed to resume exactly once");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Seed=seed,Generation=saved.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner();network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));
        var account=new Account("fire-golem-combat-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=p;network.Account=account;network.Mobile=p;p.NetState=network;
        var b=Pet(p);b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);var target=Target(p);target.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);
        NpcEquipmentRandom.Set(b,SourceRandom.InitLow(0));ulong seed=SourceRandom.InitLow(0);int expected=Expected(b,ref seed,true),life=SkillHealth.Balance256(target);bool early=false,cleaned=false;
        var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        void Clean(){if(cleaned){return;}cleaned=true;SourceBlocking.BlockRoll=block;b.Delete();target.Corpse?.Delete();target.Delete();p.Delete();account.Delete();}
        FixtureClock.NextLoop("fire-golem-combat-live-result",()=>
        {
            try
            {
                Require(b.BeginMelee(target)==TimeSpan.FromMilliseconds(680)&&b.PendingContact!=null&&b.PendingContact.Impact.Packet.Total256==expected&&NpcEquipmentRandom.State(b).Seed==seed,"G3 start must prepare the exact combined packet/stream");
                Log.Information("fire-golem-combat-live: ready");
                Timer.StartTimer(TimeSpan.FromMilliseconds(380),()=>{try{Require(SkillHealth.Balance256(target)==life,"Fire hit before400ms");early=true;}catch(Exception e){Clean();Log.Error(e,"fire-golem-combat-live-result: failed");}});
                Timer.StartTimer(TimeSpan.FromMilliseconds(520),()=>
                {
                    if(cleaned){return;}
                    try{Require(early&&life-SkillHealth.Balance256(target)==expected&&NpcEquipmentRandom.State(b).Seed==seed&&network.Running,"G3 release must commit the captured combined packet once");Clean();Require(NpcEquipmentRandom.State(b)==null,"Deleted Fire retained stream");Log.Information("fire-golem-combat-live-result: passed");}
                    catch(Exception e){Clean();Log.Error(e,"fire-golem-combat-live-result: failed");}
                });
            }
            catch{Clean();throw;}
        });
    }
}
