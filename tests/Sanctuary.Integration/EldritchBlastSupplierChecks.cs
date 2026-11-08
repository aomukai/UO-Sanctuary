// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class EldritchBlastSupplierChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(EldritchBlastSupplierChecks));
    private sealed record Saved(uint Owner,uint Helm,bool Female,EldritchBlastNumbers Numbers,int Mana256);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"eldritch-blast-suppliers-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("eldritch-blast-suppliers-check",Check);Register("eldritch-blast-suppliers-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Eldritch Blast numeric suppliers,not periodic combat",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static SkillValues Values(int rank,int warp,int purge,int ward)=>new(SkillMechanics.For(CombatAbility.EldritchBlast),
        k=>k=="Eldritch Blast"?rank:255,k=>k=="Blade Warp"?warp:k=="Hex Purge"?purge:k=="Psychic Ward"?ward:0,99);
    private static void Numbers()
    {
        // Independent explicit table tiers; no generic SkillValues curve call
        // is used to derive these controls. Boundary ranks distinguish tiers.
        int Base(int rank)=>2+Math.Min(rank-1,7)*3+Math.Clamp(rank-8,0,8)*6
            +Math.Clamp(rank-16,0,6)*9+Math.Clamp(rank-22,0,6)*15+Math.Max(rank-28,0)*17;
        int count=0;
        foreach(int rank in new[]{1,2,8,9,16,17,22,23,28,29,40,255})
        foreach(var hard in new[]{(0,0,0),(1,0,20),(0,20,1),(20,20,20)})
        {
            var d=EldritchBlastSuppliers.Capture(Values(rank,hard.Item1,hard.Item2,hard.Item3));
            // Each synergy step is50%; preserve half-point FP256 endpoints.
            int minimum=Base(rank)*256,maximum=(Base(rank)+4)*256;
            minimum+=minimum*((hard.Item1+hard.Item2)*50)/100;
            maximum+=maximum*((hard.Item1+hard.Item2)*50)/100;
            Require(d==new EldritchBlastNumbers(rank,minimum,maximum,1000+50*hard.Item3,30,5,5,5,(10+rank)*128,721),"Pinned tiers/hard synergies/duration/cost or unused parameter leakage");count++;
        }
        Require(count==48,"Missing tier/synergy controls");
        Require(EldritchBlastSuppliers.Capture(Values(1,1,0,0)).MagicMinimum256==768,"Half-step synergy FP precision");
        foreach(int rank in new[]{0,256})
        {bool refused=false;try{EldritchBlastSuppliers.Capture(Values(rank,0,0,0));}catch(NotSupportedException){refused=true;}Require(refused,"Out-of-domain rank accepted");}
        bool wrong=false;try{EldritchBlastSuppliers.Capture(new SkillValues(SkillMechanics.For(CombatAbility.Nova),_=>1,_=>0,99));}catch(NotSupportedException){wrong=true;}
        Require(wrong&&!WarlockCombat.Implemented(CombatAbility.EldritchBlast),"Wrong skill or premature learned admission");
    }
    private static void Train(PlayerMobile owner,string id,int rank)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Warlock,id,out var talent),"Warlock talent");
        foreach(var pre in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(owner.Serial,pre)==0){Train(owner,pre,1);}}
        while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}
    }
    private static void Check()
    {
        Numbers();var saved=new Saved[2];int i=0;
        foreach(bool female in new[]{false,true})
        {
            var owner=new PlayerMobile{Player=true,Female=female,Body=female?0x191:0x190,Name="EldritchSupplierFixture"};owner.AddItem(new Backpack());
            ClassCreation.Profiles.Assign(owner,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(owner,99);
            FixtureSpace.Place(owner,new(6100,female?2030:2000,0),Map.Trammel);owner.RawStr=1000;
            Train(owner,"warlock.eldritchblast",20);Train(owner,"warlock.bladewarp",2);Train(owner,"warlock.hexpurge",3);Train(owner,"warlock.psychicward",4);
            NecromancerEffects.SetMana256(owner,owner.ManaMax*256);
            var before=EldritchBlastSuppliers.Capture(owner);int mana=NecromancerEffects.ManaBalance256(owner);
            Require(before==EldritchBlastSuppliers.Capture(Values(20,2,3,4)),"Hard-point fixture dependencies");
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);
            owner.AddToBackpack(helm);Require(owner.EquipItem(helm),"Equip real +skills");ClassGear.Refresh(owner);
            var captured=SkillMechanics.Values(owner,CombatAbility.EldritchBlast);var numbers=EldritchBlastSuppliers.Capture(owner);
            Require(numbers==EldritchBlastSuppliers.Capture(Values(21,2,3,4))&&numbers.DurationFrames==1200,"Effective rank/hard-only damage and duration");
            owner.AddToBackpack(helm);ClassGear.Refresh(owner);Train(owner,"warlock.psychicward",5);
            Require(EldritchBlastSuppliers.Capture(captured)==numbers&&EldritchBlastSuppliers.Capture(owner).DurationFrames==1250,"Captured context mutated after gear/training");
            Require(NecromancerEffects.ManaBalance256(owner)==mana,"Numeric capture spent mana");
            Require(owner.EquipItem(helm),"Restore saved gear");ClassGear.Refresh(owner);numbers=EldritchBlastSuppliers.Capture(owner);
            Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.EldritchBlast)&&!SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.EldritchBlast),"Incomplete periodic route bound");
            saved[i++]=new(owner.Serial.Value,helm.Serial.Value,female,numbers,mana);
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
        Log.Information("Eldritch Blast:48 independent tier/hard-point controls;real Warlock/Witch +skills,immutable captured context,fractional mana and separate raw periodic fields. No combat/periodic callback admitted.");
    }
    private static void Reload()
    {
        foreach(var saved in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);ClassGear.Refresh(owner);
            Require(owner.Female==saved.Female&&World.FindItem((Serial)saved.Helm)?.Parent==owner,"Saved owner/original helm");
            Require(EldritchBlastSuppliers.Capture(owner)==saved.Numbers&&NecromancerEffects.ManaBalance256(owner)==saved.Mana256,"Saved hard/effective skills or resources changed");
            Require(!SkillBindings.Instance.Assign(owner,false,CombatAbility.EldritchBlast),"Reload enabled unqualified periodic route");
        }
    }
}
