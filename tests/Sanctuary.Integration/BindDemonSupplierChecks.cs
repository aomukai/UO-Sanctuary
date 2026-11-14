// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BindDemonSupplierChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BindDemonSupplierChecks));
    private sealed record Saved(int Generation,uint Owner,uint Helm,BindDemonSuppliers Frozen,BindDemonSuppliers Current);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bind-demon-suppliers-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"bind-demon-suppliers-reload-check":"bind-demon-suppliers-check";
            ConsoleInputHandler.RegisterCommand(name,"Bind Demon pinned monster/stat suppliers,real gear and saved captures",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static BindDemonSuppliers Numbers(int rank,int mastery=0,int hardMastery=0,int oath=0,int death=0)
        =>BindDemonPrepared.Capture(new(SkillMechanics.For(CombatAbility.BindDemon),key=>key switch
            {"Bind Demon"=>rank,"Demonic Mastery"=>mastery,"Blood Oath"=>oath,_=>0},
            key=>key switch{"Bind Demon"=>Math.Min(rank,20),"Demonic Mastery"=>hardMastery,"Death Mark"=>death,_=>0},99),_=>0);
    private static void Values()
    {
        var rows=BindDemonPrepared.Catalog();Require(rows.Length==171&&rows.Count(r=>r.IndexScope=="MonsterClass")==149&&rows.Count(r=>r.IndexScope=="NamedEncounter")==22,"Catalog namespaces/count");
        Require(BindDemonPrepared.Monster("fallen1")is{SourceIndex:19,IndexScope:"MonsterClass",UnderlingCount:0}
            &&BindDemonPrepared.Monster("Bishibosh")is{SourceIndex:0,IndexScope:"NamedEncounter",UnderlingCount:0},"Named encounter index confused with monster class");
        Require(rows.Count(r=>r.UnderlingCount==5)==13&&rows.Count(r=>r.UnderlingCount==7)==5,"Retained underling census");
        Require(BindDemonPrepared.Monster("fallen1").Calcs[3]=="((100-hpct)/((elte==1+elte==4)?20:((elte==2)?10:5)))","Source chained equality silently repaired");
        foreach(var (rank,mana,damage,armor,flat,m5,m10,m15,m20)in new[]{(1,200,75,100,50,0,0,0,0),(5,216,95,120,150,5,0,0,0),
            (10,236,120,145,275,5,6,0,0),(15,256,145,170,400,5,6,27,0),(20,276,170,195,525,5,6,27,30),(255,1216,1345,1370,6400,5,6,27,30)})
        {
            var n=Numbers(rank);Require(n.Mana256==mana&&n.StartMana==9&&n.DamagePercent==damage&&n.Armor==armor&&n.FlatDamage==flat
                &&n.Modifier5==m5&&n.Modifier10==m10&&n.Modifier15==m15&&n.Modifier20==m20
                &&n.BoundMaximum==1&&n.SharedMaximum==1&&n.AttackSpeed==0&&n.VelocityPercent==0,"Literal own-rank supplier endpoints");BindDemonPrepared.Validate(n);
        }
        var all=Numbers(20,20,10,20,20);Require(all is{AttackSpeed:24,VelocityPercent:33,AttackBonus:2400,DamagePercent:370,Armor:195,FlatDamage:525,
            SummonResist:66,PhysicalResist:10,SharedMaximum:3,ChanceSkillTerm:75},"Mastery/Oath/hard Death Mark contexts");
        Require(Numbers(20,21,4).SharedMaximum==1&&Numbers(20,21,5).SharedMaximum==2&&Numbers(20,21,9).SharedMaximum==2&&Numbers(20,21,10).SharedMaximum==3
            &&Numbers(20,21).AttackSpeed==25&&Numbers(255,255,20,255,20).ChanceSkillTerm==84,"Hard shared pool versus soft speed cap/chance cap");
        int calls=0;var split=BindDemonPrepared.Capture(new(SkillMechanics.For(CombatAbility.BindDemon),_=>1,_=>0,99),_=>++calls==1?5:7);
        Require(calls==2&&split is{InheritedPhysical5:5,InheritedPhysical7:7},"Separate accrued sunder fields collapsed");BindDemonPrepared.Validate(split);
        int rejected=0;
        foreach(Action bad in new Action[]{()=>Numbers(0),()=>Numbers(256),()=>Numbers(1,-1),()=>Numbers(1,1,21),()=>Numbers(1,1,1,256),
            ()=>Numbers(1,1,1,1,21),()=>BindDemonPrepared.Monster("unknown-native-demon"),()=>BindDemonPrepared.Validate(all with{FlatDamage=1})})
        {try{bad();}catch(Exception e)when(e is InvalidDataException or NotSupportedException or ArgumentException){rejected++;}}
        Require(rejected==8,"Malformed/unsupported supplier context accepted");
        Log.Information("Bind Demon suppliers:171raw monster rows,149class/22named,18underling-bearing entries;literal rank/mana/stat/modifier endpoints,hard pool,soft caps,separate sunder and8refusals pass;no chance evaluator or actor invented");
    }
    private static void Scene(NetState network)
    {
        Values();var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.Female=true;p.AddItem(new Backpack());
        ClassSkillChecks.Train(p,"warlock.binddemon",19);ClassSkillChecks.Train(p,"warlock.demonicmastery",4);ClassSkillChecks.Train(p,"warlock.bloodoath",2);ClassSkillChecks.Train(p,"warlock.deathmark",4);
        FixtureSpace.Corridor(p,6900,6,3300);var account=FixtureNetwork.Authenticate(network,p,"bind-demon-suppliers-");bool kept=false;
        try
        {
            var helm=PoisonDaggerChecks.Named(p,"Tarnhelm");ClassGear.Refresh(p);var frozen=BindDemonPrepared.Capture(p);
            Require(frozen is{Rank:20,HardRank:19,MasteryRank:5,HardMastery:4,OathRank:3,HardDeathMark:4,SharedMaximum:1,ChanceSkillTerm:59,FlatDamage:525},"Real Witch/soft ranks or hard pool/chance context");
            ClassSkillChecks.Train(p,"warlock.demonicmastery",5);ClassSkillChecks.Train(p,"warlock.deathmark",5);var trained=BindDemonPrepared.Capture(p);
            Require(trained is{MasteryRank:6,HardMastery:5,SharedMaximum:2,ChanceSkillTerm:60}&&frozen.SharedMaximum==1&&frozen.ChanceSkillTerm==59,"Hard points failed new snapshot or rewrote old one");
            p.AddToBackpack(helm);ClassGear.Refresh(p);var current=BindDemonPrepared.Capture(p);Require(current is{Rank:19,HardRank:19,MasteryRank:5,OathRank:2,HardDeathMark:5,SharedMaximum:2,FlatDamage:500},"Gear removal snapshot");
            Require(!WarlockCombat.Implemented(CombatAbility.BindDemon)&&ConsumeEffects.State(p)==null,"Suppliers opened a world route");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,helm.Serial.Value,frozen,current)));kept=true;
            Log.Information("Bind Demon suppliers:actual Witch/Tarnhelm soft ranks versus hard Mastery5pool and hard DeathMark chance;old capture remains frozen after training/gear changes;no payment,bind or world mutation");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){p.Delete();}}
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Too many Bind supplier reloads");
        var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var account=FixtureNetwork.Authenticate(network,p,"bind-demon-suppliers-reload-");
        try
        {
            BindDemonPrepared.Validate(saved.Frozen);BindDemonPrepared.Validate(saved.Current);
            Require(p.Female&&BindDemonPrepared.Capture(p)==saved.Current&&saved.Frozen is{Rank:20,HardMastery:4,SharedMaximum:1}
                &&p.FindItemOnLayer(Layer.Helm)==null&&p.Backpack.Items.Any(i=>i.Serial.Value==saved.Helm),"Original Witch/gear/ranks or frozen supplier records changed");
            Require(!WarlockCombat.Implemented(CombatAbility.BindDemon),"Reload opened Bind Demon");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Log.Information("Bind Demon suppliers:original Witch,backpacked real helm,hard/soft ranks and frozen/current captures retain through reload;no bound actor or production persistence added");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
