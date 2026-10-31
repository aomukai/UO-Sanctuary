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

public static class FireGolemAbsorptionChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FireGolemAbsorptionChecks));
    private sealed record Saved(uint Owner,uint Actor,int Life,int Maximum,Point3D Point,int Map,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"fire-golem-absorption-fixtures.json");
    public static void Configure(){Register("fire-golem-absorption-check",Check);Register("fire-golem-absorption-reload-check",Reload);}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Fire Golem accrued absorption/cap/FP healing and native delivery",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner(int rank)
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureFireAbsorption";
        void Train(string id,int n){TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string pre in row.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,pre)==0){Train(pre,1);}}while(ClassCreation.Profiles.GetRank(p.Serial,id)<n){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}}
        Train("necromancer.firegolem",rank);return p;
    }
    private static FireGolemBody Pet(PlayerMobile p)
    {
        Require(NecromancerSummons.Create(p,CombatAbility.FireGolem,Serial.Zero,new(p.X+1,p.Y,p.Z)),"Learned Fire creation");
        var b=NecromancerGolems.For(p).OfType<FireGolemBody>().Single();b.AIObject.AITimer.Stop();b.SuspendActions();b.Frozen=true;TimedEffects.PreventHealing(b,TimedEffects.Now);return b;
    }
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"fire-golem-absorption-source-cases.json")));int count=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();var got=NamedElements.Reduce256(V(0),V(1),V(2),V(3)+V(4),V(5)*256);
            Require(got==new ElementReduction(V(6),V(7)),"Complete source absorption case "+count);count++;
        }
        Require(count==2916,"Incomplete Fire absorption source matrix");
    }
    private sealed class Phase(FireGolemBody body,int before,int absorbed):PreparedMonsterDamageServices
    {
        internal int Events;internal bool Before,Commit;
        internal override void StoreLastAttacker(){}
        internal override PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket p){Events++;return p;}
        internal override PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket p){Events++;return p;}
        internal override void BeforeHealth(PreparedMonsterDamagePacket p){Require(Events==2&&SkillHealth.Balance256(body)==before,"Prepared absorption healed before source event phase");Before=true;}
        internal override void BeforeHealthCommit(Mobile source,BaseCreature target,int after)
        {Require(Before&&SkillHealth.Balance256(body)==Math.Min(SkillHealth.Maximum256(body),before+absorbed),"Absorb did not heal before health subtraction");Commit=true;base.BeforeHealthCommit(source,target,after);}
        internal override void ApplyCold(int frames){}internal override void SurvivedDamage(){}
        internal override PreparedMonsterDamagePacket Killed(PreparedMonsterDamagePacket p)=>p;internal override PreparedMonsterDamagePacket Kill(PreparedMonsterDamagePacket p)=>p;
        internal override void PresentOverlay(int overlay){}internal override int BaseHitClass()=>0;
        internal override PreparedMonsterDamagePacket MeleeAttack(PreparedMonsterDamagePacket p)=>p;internal override PreparedMonsterDamagePacket AttackedInMelee(PreparedMonsterDamagePacket p)=>p;
        internal override bool AttackerInActiveMode()=>true;internal override void BeforeThorns(PreparedMonsterDamagePacket p){}
        internal override void Return(ref PreparedMonsterDamagePacket p,Action death)=>throw new InvalidOperationException("Commit fixture must not claim full recipient return");
        internal override void FreePacket()=>throw new InvalidOperationException("No allocated packet in qualified-health fixture");
    }
    private static void Native(PlayerMobile p,FireGolemBody b)
    {
        var enemy=PoisonDaggerChecks.Target(p);var conviction=PaladinConviction.Snapshot();var caster=new PlayerMobile{Player=true,Body=0x190};ClassCreation.Profiles.Assign(caster,DiabloClass.Paladin);
        try
        {
            int max=SkillHealth.Maximum256(b);Require(b.FireAbsorb==88&&b.FireResistance==112&&NamedElements.Defense(b),"Actual rank20fire pool/defensive routing");
            SkillHealth.SetBalance256(b,max-100*256);int life=SkillHealth.Balance256(b);
            Require(NamedElements.Resolve256(enemy,b,25600,"fire",enhance:false)==default(ElementReduction),"Natural immunity generated damage/absorb healing");
            AOS.Damage(b,enemy,100,false,0,100,0,0,0,spellDamage:true);Require(SkillHealth.Balance256(b)==life,"Natural fire immunity did not block native delivery");
            Require(PaladinConviction.Apply(caster,b,SkillMechanics.Values(caster,CombatAbility.Conviction,12),TimedEffects.Now.AddMinutes(1)),"Apply Conviction lease");
            Require(NamedElements.Resistance(enemy,b,"fire",b.FireResistance)==95,"Conviction did not break112fire immunity at one fifth");
            Require(NamedElements.Resolve256(enemy,b,25600,"fire",enhance:false)==new ElementReduction(768,512)&&SkillHealth.Balance256(b)==life,"Accrued88percent ignored40cap or pure resolve healed");
            AOS.Damage(b,enemy,100,false,0,100,0,0,0,spellDamage:true);Require(SkillHealth.Balance256(b)==life-256,"Native spell did not heal2then damage3 through broken immunity");
            life=SkillHealth.Balance256(b);AOS.Damage(b,enemy,100,false,0,100,0,0,0,weaponDamage:true);Require(SkillHealth.Balance256(b)==life-256,"Native weapon fire used a different cap/order");
            SkillHealth.SetBalance256(b,max);AOS.Damage(b,enemy,100,false,0,100,0,0,0,spellDamage:true);Require(SkillHealth.Balance256(b)==max-768,"Full-health absorb was banked beyond max life");
            SkillHealth.SetBalance256(b,max-10000);life=SkillHealth.Balance256(b);
            var parts=SkillCombat.MitigatePrepared(enemy,b,new(0,100,0,0,0),out int absorbed);
            Require(parts.Fire==3&&absorbed==512&&SkillHealth.Balance256(b)==life,"Prepared allocation healed early or changed fire component");
            var phase=new Phase(b,life,absorbed);var packet=new PreparedMonsterDamagePacket(0,768,0,0,5,Fire256:768,AbsorbedLife256:absorbed);
            PreparedMonsterDamage.CommitQualified(enemy,b,packet,new(false,()=>false,()=>10,()=>false),_=>throw new InvalidOperationException("Zero stun drew RNG"),phase,TimedEffects.Now);
            Require(phase.Before&&phase.Commit&&SkillHealth.Balance256(b)==life-256,"Shared qualified health phase changed absorb order");
        }
        finally{PaladinConviction.Restore(conviction);caster.Delete();enemy.Delete();}
        // Controlled vulnerable seed for FP low-bit/reload checks, independent
        // of the short-lived Conviction lease above. This is a fixture state.
        b.SetResistance(ResistanceType.Fire,0);SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-10000);
    }
    private static void Mixed()
    {
        var p=Owner(1);var b=Pet(p);var enemy=PoisonDaggerChecks.Target(p);Item original=null;
        try
        {
            original=NamedGear.Create("unique/297",99,new Random(317),true);Require(original!=null&&NamedGear.Available("unique/297"),"Real Steel Shade unavailable");original.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);
            Require(IronGolemItems.TryAttach(p,b,original,out string why),"Declared original-stat control: "+why);b.SetResistance(ResistanceType.Fire,0);
            var item=NpcEquipmentCombat.Context(b).Value;int flat=item.Bonus("abs-fire");Require(flat is >=5 and <=11&&item.Bonus("abs-fire%")==0&&GemCombat.Reduction(b)==0&&b.FireAbsorb==36,"Real innate/own-item stat control");
            Require(NamedElements.Resolve256(enemy,b,25600,"fire",enhance:false)==new ElementReduction((64-flat)*256,(36+flat)*256),"Innate percent and own flat absorption did not compose in order");
            SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-10000);int life=SkillHealth.Balance256(b);
            AOS.Damage(b,enemy,1,false,0,100,0,0,0,spellDamage:true);Require(SkillHealth.Balance256(b)==life+256,"Own flat absorption failed full one-point native absorption/healing");
            var state=ClassGear.State(original);ClassGear.Profiles.Store(original,state with{Identified=false});
            Require(NamedElements.Resolve256(enemy,b,25600,"fire",enhance:false)==new ElementReduction(16384,9216),"Unidentified original left absorb stats");ClassGear.Profiles.Store(original,state);
            IronGolemItems.DestroyBoundItem(b);Require(original.Deleted&&NamedElements.Resolve256(enemy,b,25600,"fire",enhance:false)==new ElementReduction(16384,9216),"Removed original left cached absorb stats");
        }
        finally{if(IronGolemItems.State(b)!=null){IronGolemItems.DestroyBoundItem(b);}original?.Delete();enemy.Delete();b.Delete();p.Delete();}
    }
    private static void Legacy()
    {
        var p=Owner(20);var b=new NecromancerMinion();b.Prepare(p,CombatAbility.FireGolem);Require(b.SetControlMaster(p),"Legacy owner");b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.AIObject.AITimer.Stop();b.Frozen=true;b.SetResistance(ResistanceType.Fire,0);
        var enemy=PoisonDaggerChecks.Target(p);
        try{Require(NamedElements.Resolve256(enemy,b,25600,"fire",enhance:false)==new ElementReduction(3072,22528),"Historical adapter changed silently");}
        finally{enemy.Delete();b.Delete();p.Delete();}
    }
    private static void Check()
    {
        Source();Mixed();Legacy();var p=Owner(20);var b=Pet(p);Native(p,b);var saved=new Saved(p.Serial.Value,b.Serial.Value,SkillHealth.Balance256(b),SkillHealth.Maximum256(b),p.Location,p.Map.MapID,0);
        PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
        Log.Information("Fire absorption:2916complete source reducer cases,40percent cap,Conviction immunity break,native spell/weapon/full-health and qualified FP heal-before-damage,real own-item flat mix/identify/remove and explicit historical control pass");
    }
    private static void Reload()
    {
        Source();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(FireGolemBody)World.FindMobile((Serial)s.Actor);
        Require(b.Map==Map.Internal&&b.ControlMaster==p&&SkillHealth.Balance256(b)==s.Life&&SkillHealth.Maximum256(b)==s.Maximum&&b.FireResistance==0,"Parked exact Fire absorb fixture changed offline");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.AIObject.AITimer.Stop();b.Frozen=true;b.SetResistance(ResistanceType.Fire,0);
        var enemy=PoisonDaggerChecks.Target(p);int before=SkillHealth.Balance256(b);
        try{AOS.Damage(b,enemy,1,false,0,100,0,0,0,spellDamage:true);AOS.Damage(b,enemy,1,false,0,100,0,0,0,weaponDamage:true);Require(SkillHealth.Balance256(b)==before-104,"Reloaded native FP absorb lost52low bits per hit");}
        finally{enemy.Delete();}
        PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Life=SkillHealth.Balance256(b),Generation=s.Generation+1}));
    }
}
