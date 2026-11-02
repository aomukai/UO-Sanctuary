// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class EnergyShieldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(EnergyShieldChecks));
    private sealed record Saved(uint Owner,uint Target,uint Orb,int Mana,int Life,int TargetLife,int Map,Point3D Point,EnergyShieldState Shield,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"energy-shield-fixtures.json");
    public static void Configure()
    {
        var prior=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="EnergyShieldFixture"&&prior?.Invoke(p)!=false;
        Register("energy-shield-check",Check);Register("energy-shield-reload-check",Reload);
        foreach(bool quick in new[]{false,true}){string label=quick?"energy-shield-quick":"energy-shield-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    [CallPriority(int.MaxValue)]
    public static void Initialize(){var prior=ClassGearHooks.CanRegenerateHits;ClassGearHooks.CanRegenerateHits=p=>p.Name!="EnergyShieldFixture"&&prior?.Invoke(p)!=false;}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Energy Shield source cast,ordered absorption and original saved states",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,CombatAbility ability,int rank)
    {
        string id=SkillMechanics.For(ability).TalentId;ClassCreation.Profiles.TryGet(p.Serial,out var profile);Require(TalentCatalog.TryGet(profile.Class,id,out var talent),"Shield talent");
        foreach(string prior in talent.Prerequisites){Require(TalentCatalog.TryGet(profile.Class,prior,out var pre),"Shield prerequisite");Train(p,ClassAbilities.Skills.Single(x=>x.TalentId==pre.Id).Ability,1);}
        while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Shield train "+id);}
    }
    private static PlayerMobile Owner(bool learned=true)
    {
        var p=FireBoltChecks.Owner(CombatAbility.FireBolt,5800);p.Name="EnergyShieldFixture";
        if(learned){Train(p,CombatAbility.EnergyShield,1);Require(SkillBindings.Instance.Assign(p,false,CombatAbility.EnergyShield)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.EnergyShield)&&!SkillBindings.Instance.Assign(p,true,CombatAbility.EnergyShield),"Shield right/quick only");}
        return p;
    }
    private static SkillValues Values(int rank,int hard=0)=>new(SkillMechanics.For(CombatAbility.EnergyShield),k=>k=="Energy Shield"?rank:255,k=>k=="Telekinesis"?hard:0,99);
    private static void Check()
    {
        Goldens();Source();CastSource();Admission();Snapshots();Native();Save();
        Log.Information("Energy Shield:765rank/hard goldens,25344complete-source kernel/792native absorption comparisons,15cast-list comparisons,right-only once5mana,cancel/refusal,retained rank/live hard,legacy and original saved native hits pass");
    }
    private static void Goldens()
    {
        for(int rank=1;rank<=255;rank++)foreach(int hard in new[]{0,1,20})
        {
            int n=rank-1;int percent=Math.Min(95,20+Math.Min(n,7)*5+Math.Min(Math.Max(0,n-7),8)*2+Math.Max(0,n-15));var v=Values(rank,hard);
            Require(v.Mana256==1280&&v.Eval("auralencalc")==3600+600*n&&v.Eval("calc1")==percent&&v.Eval("calc2")==32-hard,"Independent Energy Shield curve");
        }
    }
    private static EnergyShieldDamage Raw(int pattern)
    {
        int[] x=new int[8];if(pattern is >=1 and <=8){x[pattern-1]=1001;}
        if(pattern==9){x=[1001,513,257,2049,1025,401,999,1777];}if(pattern==10){x=[1048577,1001,0,0,0,1048577,0,0];}
        return new(x[0],x[1],x[2],x[3],x[4],x[5],x[6],x[7]);
    }
    private static int[] Parts(EnergyShieldDamage x)=>[x.Physical,x.Fire,x.Lightning,x.Cold,x.Magic,x.LifeDrain,x.ManaDrain,x.StaminaDrain];
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"energy-shield-source-cases.json")));int kernels=0,native=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            int I(int i)=>r[0][i].GetInt32();var result=EnergyShields.Calculate(Raw(I(4)),I(0),I(1),I(2),I(3) is 0 or 2);
            Require(Parts(result.Damage).SequenceEqual(r[1].EnumerateArray().Skip(1).Take(8).Select(v=>v.GetInt32()))&&result.Mana==r[1][9].GetInt32(),"Complete-source event24/percentage kernel");kernels++;
        }
        var p=Owner(false);try
        {
            foreach(int hard in new[]{0,1,20})
            {
                if(hard>0){Train(p,CombatAbility.Telekinesis,hard);}p.RawInt=50000;
                Require(p.ManaMax>=50000,"Native high-mana boundary fixture ceiling");
                foreach(var r in doc.RootElement.EnumerateArray())
                {
                    int I(int i)=>r[0][i].GetInt32();if(I(3)!=1||I(5)!=-1||I(2)!=32-hard){continue;}
                    var raw=Raw(I(4));SorceressEffects.Profiles.Store(p,new(Shield:new(TimedEffects.Now.AddMinutes(1),I(1))));NecromancerEffects.SetMana256(p,I(0));
                    var after=SorceressEffects.Absorb(p,new(raw.Physical/256.0,raw.Fire/256.0,raw.Cold/256.0,raw.Lightning/256.0,13,Magic:raw.Magic/256.0));
                    Require(after.Physical*256==r[1][1].GetInt32()&&after.Fire*256==r[1][2].GetInt32()&&after.Lightning*256==r[1][3].GetInt32()
                        &&after.Cold*256==r[1][4].GetInt32()&&after.Magic*256==r[1][5].GetInt32()&&after.Poison==13&&NecromancerEffects.ManaBalance256(p)==r[1][9].GetInt32()
                        &&(SorceressEffects.Shield(p)==null)==(r[1][10].GetInt32()!=0),"Complete-source native five-channel absorption/mana/depletion");native++;
                }
            }
            var negative=new EnergyShieldDamage(-1,0,0,0,0);Require(EnergyShields.Calculate(negative,1,20,12,false).Damage==negative,"Nonpositive source lane changed");
            Require(EnergyShields.Calculate(negative,1,0,12,false)==new EnergyShieldResult(negative,1,0),"Zero multiplier consumed mana");
            Require(kernels==25344&&native==792,"Incomplete source/native absorption coverage");
        }
        finally{FireBoltChecks.Clean(p);}
    }
    private static void CastSource()
    {
        var p=Owner();try
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"energy-shield-cast-source-cases.json")));int compared=0;
            foreach(var r in doc.RootElement.EnumerateArray())
            {
                int I(int i)=>r[0][i].GetInt32();if(I(0)!=0||I(3)!=1||I(4)!=3600||I(5)!=1){continue;}
                SorceressEffects.Profiles.Clear(p);var old=new EnergyShieldState(TimedEffects.Now.AddMinutes(10),7){Rank=I(1)};
                if(I(1)>0){SorceressEffects.Profiles.Store(p,new(Shield:old));}
                var prepared=EnergyShields.Prepare(Values(I(2)),TimedEffects.Now);bool applied=EnergyShields.Apply(p,prepared);
                Require(applied==(r[1][0].GetInt32()!=0)&&SorceressEffects.Shield(p)==(applied?prepared:old),"Complete-source do23 list replacement/refusal");compared++;
            }
            Require(compared==15,"Incomplete source list coverage");
        }
        finally{FireBoltChecks.Clean(p);}
    }
    private static void Admission()
    {
        var p=Owner();try
        {
            int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.ContinueCast(p,CombatAbility.EnergyShield,null,p.Location,Core.TickCount),"Shield windup");long release=ClassCombat.For(p).EffectAt;
            Require(NecromancerEffects.ManaBalance256(p)==mana&&SorceressEffects.Shield(p)==null,"Shield paid during windup");ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,release)&&NecromancerEffects.ManaBalance256(p)==mana&&SorceressEffects.Shield(p)==null,"Cancelled Shield paid/applied");
            NecromancerEffects.SetMana256(p,1279);Require(!SorceressCombat.Release(p,CombatAbility.EnergyShield,Serial.Zero,p.Location)&&NecromancerEffects.ManaBalance256(p)==1279&&SorceressEffects.Shield(p)==null,"Insufficient fractional Shield mana");
            NecromancerEffects.SetMana256(p,mana);long next=Math.Max(Core.TickCount,ClassCombat.For(p).NextAction);
            Require(ClassCombat.ContinueCast(p,CombatAbility.EnergyShield,null,p.Location,next),"Shield second windup");release=ClassCombat.For(p).EffectAt;Train(p,CombatAbility.EnergyShield,2);
            Require(ClassCombat.ResolveAction(p,release)&&NecromancerEffects.ManaBalance256(p)==mana-1280&&SorceressEffects.Shield(p) is{Rank:2},"Release rank or once-only five mana");
            ClassCombat.Stop(p);p.Internalize();mana=NecromancerEffects.ManaBalance256(p);
            Require(!SorceressCombat.Release(p,CombatAbility.EnergyShield,Serial.Zero,p.Location)&&NecromancerEffects.ManaBalance256(p)==mana,"Internal caster paid");
            FixtureSpace.Corridor(p,5800,32,3000);p.Kill();mana=NecromancerEffects.ManaBalance256(p);
            Require(!SorceressCombat.Release(p,CombatAbility.EnergyShield,Serial.Zero,p.Location)&&NecromancerEffects.ManaBalance256(p)==mana,"Dead caster paid");
        }
        finally{ClassCombat.Stop(p);FireBoltChecks.Clean(p);}
    }
    private static void Snapshots()
    {
        var p=Owner();try
        {
            var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");Require(SorceressCombat.Release(p,CombatAbility.EnergyShield,Serial.Zero,p.Location),"Real gear shield");var shield=SorceressEffects.Shield(p);
            p.Backpack.DropItem(orb);ClassGear.Refresh(p);int mana=NecromancerEffects.ManaBalance256(p);Require(shield.Rank>SkillMechanics.Rank(p,CombatAbility.EnergyShield),"Real retained Shield rank");
            Require(!SorceressCombat.Release(p,CombatAbility.EnergyShield,Serial.Zero,p.Location)&&NecromancerEffects.ManaBalance256(p)==mana&&SorceressEffects.Shield(p)==shield,"Weaker cast replaced/extended/paid");
            var raw=new GearDamage(1,0,0,0,0);int percent=(int)Values(shield.Rank).Eval("calc1");var after=SorceressEffects.Absorb(p,raw);
            Require(after.Physical*256==256-SourcePercentage.Calculate(256,percent,100)&&SorceressEffects.Shield(p)==shield,"Gear removal changed retained absorption");
            Train(p,CombatAbility.Telekinesis,20);mana=NecromancerEffects.ManaBalance256(p);after=SorceressEffects.Absorb(p,raw);
            int amount=SourcePercentage.Calculate(256,percent,100);Require(mana-NecromancerEffects.ManaBalance256(p)==SourcePercentage.Calculate(amount,12,16),"Live hard Telekinesis efficiency");
            var legacy=JsonSerializer.Deserialize<EnergyShieldState>(JsonSerializer.Serialize(new{shield.UntilUtc,AbsorbPercent=37}));
            Require(legacy.Rank==0,"Historical JSON without Rank did not retain its legacy identity");
            SorceressEffects.Profiles.Store(p,new(Shield:legacy));after=SorceressEffects.Absorb(p,raw);
            Require(after.Physical*256==256-SourcePercentage.Calculate(256,37,100)&&SorceressEffects.Shield(p).Rank==0,"Legacy Shield reconstructed rank");
            Require(p.EquipItem(orb),"Restore real gear");ClassGear.Refresh(p);Require(SorceressCombat.Release(p,CombatAbility.EnergyShield,Serial.Zero,p.Location),"Legacy recast");shield=SorceressEffects.Shield(p);
            Require(SorceressCombat.Release(p,CombatAbility.EnergyShield,Serial.Zero,p.Location)&&SorceressEffects.Shield(p).Rank==shield.Rank&&SorceressEffects.Shield(p).UntilUtc>=shield.UntilUtc,"Equal-rank refresh");
        }
        finally{FireBoltChecks.Clean(p);}
    }
    private static void Native()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,1);try
        {
            Train(p,CombatAbility.Telekinesis,20);Require(SorceressCombat.Release(p,CombatAbility.EnergyShield,Serial.Zero,p.Location),"Native shield");int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(p);
            var prior=ClassGearHooks.Resistance;try
            {
                ClassGearHooks.Resistance=(q,type)=>q==p&&type==ResistanceType.Fire?50:prior?.Invoke(q,type);
                AOS.Damage(p,t,100,false,0,100,0,0,0,spellDamage:true);
                Require(SkillHealth.Balance256(p)==life-40*256&&NecromancerEffects.ManaBalance256(p)==mana-15*256,"Native pre-resistance order");
            }
            finally{ClassGearHooks.Resistance=prior;}
            SorceressEffects.Profiles.Store(p,new(Shield:new(TimedEffects.Now.AddMilliseconds(-1),95){Rank=99}));mana=NecromancerEffects.ManaBalance256(p);
            Require(SorceressEffects.Absorb(p,new(1,0,0,0,0)).Physical==1&&NecromancerEffects.ManaBalance256(p)==mana,"Expired Shield absorbed");
        }
        finally{FireBoltChecks.Clean(p,t);}
    }
    private static void Save()
    {
        var p=Owner();Train(p,CombatAbility.EnergyShield,20);Train(p,CombatAbility.Telekinesis,20);var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");Require(SorceressCombat.Release(p,CombatAbility.EnergyShield,Serial.Zero,p.Location),"Saved paid shield");
        var t=FireBoltChecks.Target(p,1);AOS.Damage(p,t,11,false,100,0,0,0,0,spellDamage:true);SkillHealth.Damage(p,p,5*256+13);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,orb.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),SkillHealth.Balance256(t),p.Map.MapID,p.Location,SorceressEffects.Shield(p),0)));p.Internalize();
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);
        Require(s.Generation<2&&World.FindItem((Serial)s.Orb)?.Parent==p&&SorceressEffects.Profiles.Find(p)?.Shield==s.Shield&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(t)==s.TargetLife,"Original Shield actors/gear/wounds/mana/deadline changed");
        Require(SkillBindings.Instance.Get(p).Right==CombatAbility.EnergyShield&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Sorceress,7)==CombatAbility.EnergyShield,"Saved Shield binding");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);t.AIObject.AITimer.Stop();bool active=s.Shield.UntilUtc>TimedEffects.Now;int expected=active?EnergyShields.Calculate(new(11*256,0,0,0,0),s.Mana,(int)Values(s.Shield.Rank).Eval("calc1"),12,false).Mana:s.Mana;
        AOS.Damage(p,t,11,false,100,0,0,0,0,spellDamage:true);
        Require(NecromancerEffects.ManaBalance256(p)==expected&&SkillHealth.Balance256(p)<s.Life&&SorceressEffects.Profiles.Find(p)?.Shield==s.Shield,"Saved native Shield hit failed or refreshed/reset state");
        p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Mana=NecromancerEffects.ManaBalance256(p),Life=SkillHealth.Balance256(p),Generation=s.Generation+1}));
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var account=FixtureNetwork.Authenticate(network,p,"energy-shield-input-");int mana=NecromancerEffects.ManaBalance256(p);bool began=false;long deadline=Core.TickCount+5000;
        void Finish(){FireBoltChecks.Clean(p);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Shield input timeout");
                if(ClassCombat.For(p).PendingAbility==CombatAbility.EnergyShield&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana&&SorceressEffects.Shield(p)==null,"Input paid during windup");began=true;Log.Information("{Label}: windup",label);}
                if(SorceressEffects.Shield(p)==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                Require(began&&SorceressEffects.Shield(p).Rank==1&&NecromancerEffects.ManaBalance256(p)==mana-1280,"Actual Shield input rank/payment");Finish();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Finish();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)CombatAbility.EnergyShield,(int)DiabloClass.Sorceress,p.X,p.Y,p.Z);Observe();});
    }
}
