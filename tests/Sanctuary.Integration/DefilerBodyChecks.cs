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

public static class DefilerBodyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DefilerBodyChecks));
    private sealed record Saved(uint Owner,uint Body,uint Helm,bool Female,int Mana,int Life,int Maximum,string State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"defiler-body-fixtures.json");
    public static void Configure(){Register("defiler-body-check",Check);Register("defiler-body-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Defiler independent vital suppliers and original saved Gazer body",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int Curve(int rank,int min,int max)=>rank<=0?0:min+(max-min)*Math.Min(100,110*rank/(rank+6))/100;
    private static SkillValues Values(int rank,int mastery,int oath,int hard)=>new(SkillMechanics.For(CombatAbility.SummonDefiler),
        key=>key=="Summon Defiler"?rank:key=="Demonic Mastery"?mastery:key=="Blood Oath"?oath:0,key=>key=="Demonic Mastery"?hard:0,47);
    private static void Model()
    {
        int contexts=0,bases=0;
        foreach(int rank in Enumerable.Range(1,255))foreach(var pair in new[]{(0,0),(17,16),(255,20),(3,0)})foreach(int oath in new[]{0,6})
        {
            var values=Values(rank,pair.Item1,oath,pair.Item2);var s=DefilerPrepared.Suppliers(values,_=>7);
            var blood=SkillMechanics.For(CombatAbility.BloodOath);int life=35*(rank-1)+(oath<=0?0:blood.Parameter(5)+(oath-1)*blood.Parameter(6));
            Require(s.LifePercent==life&&s.AttackSpeed==Math.Min(pair.Item1<=0?0:5+pair.Item1-1,25)&&s.VelocityPercent==Curve(pair.Item1,5,39)
                &&s.DefenseBonus==125+20*(rank-1)&&s.PhysicalResistBonus==Curve(oath,1,12)&&s.PhysicalSunder4==7&&s.PhysicalSunder7==7
                &&s.PetMaximum==(pair.Item2>=10?3:pair.Item2>=5?2:1)&&s.Mana256==(rank+69)*128&&s.DelayFrames==15&&s.HealthLinkRank==rank&&s.SelfSkillRank==rank,
                "Defiler independent vital supplier context");contexts++;
            foreach(int level in new[]{1,47,99})foreach(var difficulty in Enum.GetValues<MonsterDifficulty>())
            {
                var b=DefilerPrepared.Birth(values,difficulty,level,_=>7);int hp=new[]{140,190,300}[(int)difficulty]*256;
                Require(b.Vitals.PetLevel==Math.Min(level,rank+3*level/4)&&b.Vitals.BaseLife256==hp
                    &&b.Vitals.MaximumLife256==hp+(int)((long)hp*life/100)&&b.Suppliers==s,"Defiler noRatio base/level/life projection");bases++;
            }
        }
        Require(contexts==2040&&bases==18360,"Missing Defiler vital matrix");
        try{Values(1,17,6,16).Eval("passivecalc2");throw new InvalidOperationException("Unverified raw damage expression was repaired");}catch(InvalidDataException){}
        Require(!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Unfinished Defiler learned cast enabled");
        Log.Information("Defiler:2040independent supplier/18360noRatio vital contexts;raw damage/Health Link remain disabled");
    }
    private static void Numeric(WarlockDefilerBody b)
    {
        var state=b.Prepared;var snapshot=state.Snapshot;var v=snapshot.Birth.Vitals;var resistance=b.SourceResistances();
        Require(b.Body==22&&b.BaseSoundID==377&&b.SummonClass==DiabloClass.Warlock&&b.SourceMonsterId=="warputriddefiler"
            &&b.SourceLife256==v.MaximumLife256&&b.MaximumLife256==v.MaximumLife256&&b.CombatProfile.Level==v.PetLevel
            &&b.CombatProfile.AttackRating==snapshot.Birth.ProjectedAttack&&b.CombatProfile.Defense==snapshot.Birth.ProjectedArmor
            &&b.SkillWeaponDamage==(0,0)&&b.CanBeDamaged()&&!b.CanRegenHits&&!b.CanRegenMana&&!b.CanRegenStam
            &&!b.BleedImmune&&b.PoisonImmune==null&&b.FireResistance==resistance.Fire&&b.PoisonResistance==resistance.Poison,"Defiler mapped vital body inherited UO stats/offense/immunities");
        var metadata=NativeMonsterReturn.Metadata(b);Require(metadata is{Class:744,Base:744,Modes:8192,Velocity:6,Killable:true},"Defiler shared incoming return identity");
    }
    private static Saved Scene(bool female)
    {
        var p=TaintedFireBallChecks.Owner();if(female){p.Female=true;p.Body=0x191;}ClassSkillChecks.Train(p,"warlock.summondefiler");
        var facets=SummonDifficulties.Facets();WarlockDefilerBody b=null;BaseCreature enemy=null;
        try
        {
            int mana=NecromancerEffects.ManaBalance256(p);b=WarlockDefilerBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now.AddDays(2));Numeric(b);var original=b.Prepared.Snapshot;
            Require(original.Birth.Suppliers.Rank==2&&original.MasteryRank==17&&original.HardMastery==16,"Defiler captured effective/hard ranks");
            enemy=FireBoltChecks.Target(p,8);int before=SkillHealth.Balance256(b);
            NpcFireSpellHit.Deliver(enemy,p,b,100*256,TimedEffects.Now,"Defiler qualified incoming");
            Require(before-SkillHealth.Balance256(b)==67*256&&NativeMonsterReturn.Session(b).Packets==1,"Defiler ordinary incoming fire/return path");
            bool refused=false;try{b.BeginMelee(enemy);}catch(NotSupportedException){refused=true;}Require(refused,"Unverified Defiler offense was enabled");
            var text=b.SavedText;int life=SkillHealth.Balance256(b);
            try{b.RestorePrepared(b.Prepared with{Snapshot=original with{Birth=original.Birth with{Suppliers=original.Birth.Suppliers with{PhysicalSunder4=1,PhysicalSunder7=1}}}});throw new InvalidOperationException("Unverified sunder accepted");}catch(InvalidDataException){}
            Require(b.SavedText==text&&SkillHealth.Balance256(b)==life,"Rejected Defiler state changed life/capture");
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Nightmare),new(Map.Ilshenar.MapID,MonsterDifficulty.Hell)]);
            foreach(var map in new[]{Map.Felucca,Map.Ilshenar,Map.Trammel})
            {
                int old=SkillHealth.Balance256(b),maximum=b.MaximumLife256;p.MoveToWorld(new(1200,1200,0),map);SummonDifficulties.Sync(p);Numeric(b);
                Require(b.Prepared.Snapshot==DefilerPrepared.Rescale(original,SummonDifficulties.For(map))
                    &&SkillHealth.Balance256(b)==SummonDifficulties.Life256(old,maximum,b.MaximumLife256),"Defiler facet rescale recaptured ranks or lost life percentage");
            }
            Require(NecromancerEffects.ManaBalance256(p)==mana,"Prepared Defiler creation/rescaling spent mana");
            SkillHealth.SetBalance256(b,b.MaximumLife256-257);PersistentMinions.Suspend(p);
            b.RestorePrepared(b.Prepared with{NextRecoveryUtc=TimedEffects.Now.AddDays(2)});var saved=new Saved(p.Serial.Value,b.Serial.Value,p.FindItemOnLayer(Layer.Helm).Serial.Value,female,mana,SkillHealth.Balance256(b),b.MaximumLife256,b.SavedText);
            p.Internalize();b=null;p=null;return saved;
        }
        finally{SummonDifficulties.SetFacets(facets);enemy?.Delete();b?.Delete();p?.Delete();}
    }
    private static void Check(){Model();File.WriteAllText(Manifest,JsonSerializer.Serialize(new[]{Scene(false),Scene(true)}));}
    private static void Reload()
    {
        foreach(var saved in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var b=(WarlockDefilerBody)World.FindMobile((Serial)saved.Body);
            Require(b.ControlMaster==p&&p.Female==saved.Female&&b.SavedText==saved.State&&SkillHealth.Balance256(b)==saved.Life&&b.MaximumLife256==saved.Maximum
                &&NecromancerEffects.ManaBalance256(p)==saved.Mana&&World.FindItem((Serial)saved.Helm)?.Parent==p,"Original Warlock/Witch Defiler/helm/wounds/mana lost on reload");
            Numeric(b);Require(b.Map==Map.Internal,"Parked Defiler returned without owner action");
        }
    }
}
