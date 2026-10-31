// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class SummonResistChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SummonResistChecks));
    private sealed record Pet(uint Serial,string Type,int Captured,int[] Resists,int Life,int Maximum);
    private sealed record Saved(uint Owner,int Rank,Pet[] Pets);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"summon-resist-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()
    {Register("summon-resist-check",Check);Register("summon-resist-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Summon Resist shared curve and recipient checks",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static SkillValues Values(CombatAbility ability,int rank)
        =>new(SkillMechanics.For(ability),k=>ability==CombatAbility.SummonResist?rank:k=="IronGolem"?1:0,_=>0,99);
    private static int[] Resists(SkeletonGuardian b)=>[b.PhysicalResistance,b.FireResistance,b.ColdResistance,b.EnergyResistance,b.PoisonResistance];
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"summon-resist-source-cases.json")));int count=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();Require(Values(CombatAbility.SummonResist,V(0)).Diminishing(1,2)==V(1),"Retained shared dm12 rank "+V(0));
            if(V(6)==0){continue;} // Native planner describes accepted allocation.
            var n=IronGolemBirth.Prepare(Values(CombatAbility.IronGolem,1),MonsterDifficulty.Normal,true,new(306*256,140,0),_=>0,V(1),
                name=>name switch{"item_absorbfire_percent"=>V(3),"item_absorblight_percent"=>V(4),"item_absorbcold_percent"=>V(5),_=>throw new NotSupportedException(name)});
            string[] names=["fireresist","lightresist","coldresist","poisonresist"];int mask=0;
            for(int i=0;i<4;i++)
            {
                var stat=n.Resistances.SingleOrDefault(x=>x.Name==names[i]);if(stat!=null){mask|=1<<i;}
                Require((stat?.Value??0)==V(8+i),"Complete source absorb/channel value "+count);
            }
            Require(mask==V(7),"Complete source channel presence "+count);count++;
        }
        Require(count==6912,"Incomplete allocation-accepted source comparisons");
        foreach(var (rank,percent) in new[]{(7,52),(13,61),(19,65)}){Require(Values(CombatAbility.SummonResist,rank).Diminishing(1,2)==percent,"Pinned user tooltip reading");}
    }
    private static void Train(PlayerMobile p,int rank)
    {while(ClassCreation.Profiles.GetRank(p.Serial,"necromancer.summonresist")<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.summonresist",out _),"Train Summon Resist");}}
    private static SkeletonGuardian Create(PlayerMobile p,CombatAbility kind)
    {
        SkeletonGuardian b;
        if(kind==CombatAbility.RaiseSkeleton)
        {var s=new RaisedSkeleton();s.InitializeExact(p,SkillMechanics.Rank(p,kind),SkillMechanics.Rank(p,CombatAbility.SkeletonMastery));b=s;}
        else if(kind==CombatAbility.IronGolem)
        {
            var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Crown of Thieves").Id,99,new Random(1107),true);
            var point=new Point3D(p.X+2,p.Y,p.Z);item.MoveToWorld(point,p.Map);b=IronGolemBody.CreatePrepared(p,item,point);
        }
        else
        {
            NecromancerMinion n=kind switch{CombatAbility.ClayGolem=>new ClayGolemBody(),CombatAbility.BloodGolem=>new BloodGolemBody(),CombatAbility.FireGolem=>new FireGolemDeathBody(),_=>new NecromancerMinion()};
            n.Prepare(p,kind);b=n;
        }
        Require(b.SetControlMaster(p),"Recipient owner");PersistentMinions.Profiles.Track(b);b.AIObject?.AITimer.Stop();b.SuspendActions();b.Internalize();return b;
    }
    private static void Verify(SkeletonGuardian b,CombatAbility kind,int bonus)
    {
        string key=SkillMechanics.For(kind).Text("summon");int F(string stat)=>NecromancerSummons.Source(key,stat,b.SummonDifficulty);
        int fire=F("ResFi")+bonus;
        if(kind==CombatAbility.FireGolem){fire=F("ResFi")+checked((int)((NecromancerMinion)b).CapturedValues.Eval("aurastatcalc1"));}
        if(kind==CombatAbility.IronGolem){fire+=33;} // Original named Crown's fixed fire resistance.
        int[] expected=[F("ResDm"),fire,F("ResCo")+bonus,F("ResLi")+bonus,F("ResPo")+bonus];
        Require(Resists(b).SequenceEqual(expected),$"Recipient {kind} expected={string.Join(',',expected)} actual={string.Join(',',Resists(b))}");
        if(b is NecromancerMinion m){Require(m.SummonMagicResistance==F("ResMa"),"Summon Resist altered magic resistance");}
    }
    private static void Check()
    {
        Source();var p=ClassSkillChecks.Player();p.Name="PoisonDaggerFixtureSummonResist";p.AddItem(new Backpack());var pets=new List<(SkeletonGuardian Body,CombatAbility Kind,int Bonus)>();
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Recipient fixture strength");}
        try
        {
            int mana=NecromancerEffects.ManaBalance256(p);
            Require(!ClassCombat.Available(p,CombatAbility.SummonResist)&&!SkillBindings.Instance.Assign(p,false,CombatAbility.SummonResist)
                &&!SkillBindings.Instance.AssignQuick(p,7,CombatAbility.SummonResist)&&NecromancerEffects.ManaBalance256(p)==mana,"Passive became an active/mana cast");
            foreach(var (rank,percent) in new[]{(7,52),(13,61),(19,65)})
            {
                Train(p,rank);Require(NecromancerSummons.Resistance(p)==percent,"Live hard-rank supplier");
                foreach(var old in pets){Verify(old.Body,old.Kind,old.Bonus);}
                foreach(var kind in new[]{CombatAbility.RaiseSkeleton,CombatAbility.RaiseSkeletalMage,CombatAbility.ClayGolem,CombatAbility.BloodGolem,CombatAbility.FireGolem,CombatAbility.IronGolem})
                {var b=Create(p,kind);pets.Add((b,kind,percent));Verify(b,kind,percent);}
            }
            var helm=PoisonDaggerChecks.Named(p,"Wormskull");Require(SkillMechanics.Rank(p,CombatAbility.SummonResist)==20&&NecromancerSummons.Resistance(p)==66,"Actual equipped +skill supplier");
            foreach(var old in pets){Verify(old.Body,old.Kind,old.Bonus);}
            var fresh=Create(p,CombatAbility.RaiseSkeleton);pets.Add((fresh,CombatAbility.RaiseSkeleton,66));Verify(fresh,CombatAbility.RaiseSkeleton,66);
            p.Backpack.DropItem(helm);ClassGear.Refresh(p);Require(NecromancerSummons.Resistance(p)==65,"Item removal effective rank");
            foreach(var old in pets){Verify(old.Body,old.Kind,old.Bonus);}
            foreach(var item in pets){SkillHealth.SetBalance256(item.Body,SkillHealth.Maximum256(item.Body)-257);TimedEffects.PreventHealing(item.Body,TimedEffects.Now);}
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,19,pets.Select(x=>new Pet(x.Body.Serial.Value,x.Body.GetType().Name,x.Bonus,Resists(x.Body),SkillHealth.Balance256(x.Body),SkillHealth.Maximum256(x.Body))).ToArray())));p.Internalize();
        }
        catch{foreach(var x in pets){x.Body.Delete();}p.Delete();throw;}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        Require(p!=null&&SkillMechanics.Rank(p,CombatAbility.SummonResist)==saved.Rank&&NecromancerSummons.Resistance(p)==65&&saved.Pets.Length==19,"Saved supplier/recipient cohort");
        foreach(var s in saved.Pets)
        {
            var b=World.FindMobile((Serial)s.Serial) as SkeletonGuardian;
            Require(b is{Alive:true,Deleted:false}&&b.GetType().Name==s.Type&&b.ControlMaster==p&&Resists(b).SequenceEqual(s.Resists)
                &&SkillHealth.Balance256(b)==s.Life&&SkillHealth.Maximum256(b)==s.Maximum,$"Saved recipient {s.Serial}/{s.Type} changed");
        }
    }
}
