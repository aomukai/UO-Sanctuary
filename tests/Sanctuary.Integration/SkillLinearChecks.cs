// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class SkillLinearChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SkillLinearChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("skill-linear-check","Source linear formulas and absent synergy ranks",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("skill-linear-check: passed");}catch(Exception e){Log.Error(e,"skill-linear-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        var absent=new SkillValues(SkillMechanics.For(CombatAbility.SummonSpiritWolf),key=>key=="Summon Spirit Wolf"?1:0,_=>0,99);
        Require(absent.Eval("EDmgSymPerCalc")==0,"Unlearned Grizzly donated a nonzero linear damage synergy");
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"skill-linear-source-probe.json")));int count=0;
        foreach(var row in source.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();var parameters=new Dictionary<string,int>();for(int i=1;i<=8;i++){parameters[i.ToString(CultureInfo.InvariantCulture)]=12345;}
            int a=N(0)+1,b=N(0)+2;parameters[a.ToString(CultureInfo.InvariantCulture)]=N(2);parameters[b.ToString(CultureInfo.InvariantCulture)]=N(3);
            var definition=SkillMechanics.For(CombatAbility.SummonGrizzly) with{SourceParameters=parameters};
            var values=new SkillValues(definition,_=>N(1),_=>0,99);
            Require(values.Linear(a,b)==N(4) && values.Formula("ln"+a+b)==N(4),"Complete linear source arm / native resolver comparison at rank "+N(1));count++;
        }
        Require(count==9252,"Incomplete linear formula source capture");
        SkillValues Wolf(int bear)=>new(SkillMechanics.For(CombatAbility.SummonSpiritWolf),key=>key=="Summon Spirit Wolf"?1:key=="Summon Grizzly"?bear:0,_=>0,99);
        Require(Wolf(0).Eval("EDmgSymPerCalc")==0 && Wolf(0).Damage256(false)==512 && Wolf(0).Damage256(true)==1536,"Unlearned Grizzly donated cold damage");
        Require(Wolf(1).Eval("EDmgSymPerCalc")==25 && Wolf(1).Damage256(false)==640 && Wolf(1).Damage256(true)==1920,"Learned Grizzly baseline lost");
        var golem=new SkillValues(SkillMechanics.For(CombatAbility.ClayGolem),key=>key=="Clay Golem"?1:0,_=>0,99);
        Require(golem.Formula("skill('Golem Mastery'.ln12)")==0 && golem.Formula("skill('Golem Mastery'.ln56)")==0,"Unlearned Golem Mastery donated linear stats");
        CurseRank();
        Log.Information("Linear skill formulas:9252 unchanged source arms match; absent Grizzly/mastery and learned baseline controls pass");
    }
    private static void Train(PlayerMobile owner,string id,int rank=1)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var definition),"Curse rank talent");
        foreach(string prerequisite in definition.Prerequisites){Train(owner,prerequisite);}
        while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}
    }
    private static void CurseRank()
    {
        var owner=new PlayerMobile{Body=0x190,Player=true,Name="CurseRankOwner"};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,99);Train(owner,"necromancer.attract",3);
        FixtureSpace.Corridor(owner,7040,12,3500);
        var target=new GraveyardZombie{CantWalk=true,NoKillAwards=true};target.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);target.AIObject.AITimer.Stop();
        try
        {
            Require(!NecromancerEffects.Apply(owner,target,CombatAbility.Attract,0) && NecromancerEffects.Curse(target)==null,"Rank-zero curse created an effect");
            var values=new SkillValues(SkillMechanics.For(CombatAbility.Attract),_=>1,_=>0,99);int frames=(int)values.Eval("auralencalc");var start=TimedEffects.Now;
            Require(NecromancerEffects.Apply(owner,target,CombatAbility.Attract,1),"Captured rank-one curse refused");
            var curse=NecromancerEffects.Curse(target);
            Require(curse is{Rank:1} && curse.UntilUtc>=start.AddMilliseconds(frames*40)
                && curse.UntilUtc<=TimedEffects.Now.AddMilliseconds(frames*40),"Stored curse rank disagrees with supplied-rank duration");
            var state=NecromancerEffects.Profiles.Find(target);NecromancerEffects.Profiles.Store(target,state with{Curse=null});
            var updated=SkillMechanics.Values(owner,CombatAbility.Attract);start=TimedEffects.Now;frames=(int)updated.Eval("auralencalc");
            Require(updated.Rank==3 && NecromancerEffects.Apply(owner,target,CombatAbility.Attract,3),"Fresh live-rank curse refused");curse=NecromancerEffects.Curse(target);
            Require(curse is{Rank:3} && curse.UntilUtc>=start.AddMilliseconds(frames*40)
                && curse.UntilUtc<=TimedEffects.Now.AddMilliseconds(frames*40),"Fresh curse did not use current supplied rank");
        }
        finally{target.Delete();owner.Delete();}
    }
}
