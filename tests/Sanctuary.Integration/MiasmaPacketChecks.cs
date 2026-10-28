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

public static class MiasmaPacketChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MiasmaPacketChecks));
    private sealed record Saved(uint Owner,uint Helm,bool Female,MiasmaDamage Damage);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"miasma-packet-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Register(string n,Action run)=>ConsoleInputHandler.RegisterCommand(n,"Prepared Miasma numeric suppliers and real trained rank persistence",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    public static void Configure(){Register("miasma-packets-check",Check);Register("miasma-packets-reload-check",Reload);}
    private static SkillValues Values(int rank,int chains,int abyss,int entropy=0)=>new(SkillMechanics.For(CombatAbility.MiasmaBolt),
        key=>key=="Miasma Bolt"?rank:key=="Enhanced Entropy"?entropy:255,
        key=>key=="Miasma Chains"?chains:key=="Abyss"?abyss:0,99);
    private static void Model()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"miasma-packets-source-probe.json")));int count=0;
        foreach(var row in source.RootElement.EnumerateArray())
        {
            int kind=row[0].GetInt32(),rank=row[1].GetInt32();var v=Values(rank,row[2].GetInt32(),row[3].GetInt32());var d=MiasmaPackets.Capture(v);
            Require((kind==0?d.BoltMinimum256:d.CloudMinimum256)==row[5].GetInt32()&&(kind==0?d.BoltMaximum256:d.CloudMaximum256)==row[6].GetInt32(),"Miasma differs from complete source creator/getter/writer");
            Require(d.CloudFrames==50&&d.CloudRadius==2&&d.Mana256==(4+rank-1)*128,"Miasma unenhanced duration/radius/fractional mana");count++;
        }
        Require(count==9180,"Missing complete-source Miasma packet controls");
        var one=MiasmaPackets.Capture(Values(1,0,0));Require(one==new MiasmaDamage(256,512,768,1280,50,2,512),"Miasma rank1golden");
        var twenty=MiasmaPackets.Capture(Values(20,0,0));Require(twenty.BoltMinimum256==22*256&&twenty.BoltMaximum256==25*256&&twenty.CloudMinimum256==38*256&&twenty.CloudMaximum256==59*256&&twenty.Mana256==2944,"Miasma rank20released damage golden");
        var rounding=MiasmaPackets.Capture(Values(1,1,0));Require(rounding.BoltMinimum256==281&&rounding.BoltMaximum256==563&&rounding.CloudMinimum256==768&&rounding.CloudMaximum256==1536,"Skill/missile synergy rounding collapsed");
        foreach(int entropy in new[]{0,1,4,5,9,10,20,21,255})
        {
            var v=Values(20,1,20,entropy);var d=MiasmaPackets.Capture(v);var cloud=new MissileValues("miasmaboltcloud",v);
            Require(d.CloudFrames==50+Math.Min(5*entropy,100)&&d.CloudRadius==(entropy>=10?4:entropy>=5?3:2),"Enhanced Entropy effective rank/cap/thresholds");
            Require(cloud.Number("Param4")==50&&v.Param(4)==0&&cloud.Number("Param5")==5&&cloud.Eval("EDmgSymPerCalc")==420,"Missile/skill parameter scopes merged");
            Require(JsonSerializer.Deserialize<MiasmaDamage>(JsonSerializer.Serialize(d))==d,"Miasma packet JSON roundtrip");
        }
        try{MiasmaPackets.Capture(Values(0,0,0));throw new InvalidOperationException("Rank0packet accepted");}catch(NotSupportedException){}
        try{MiasmaPackets.Capture(Values(256,0,0));throw new InvalidOperationException("Rank256packet accepted");}catch(NotSupportedException){}
        Require(!WarlockCombat.Implemented(CombatAbility.MiasmaBolt),"Prepared packet enabled incomplete learned skill");
        Log.Information("Miasma packets:9180full-source comparisons, magic-only distinct synergy rounding, own parameters, entropy thresholds/cap and fractional mana pass; learned cast gated");
    }
    private static void Train(PlayerMobile p,string id,int rank=1)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Warlock,id,out var row),"Warlock talent");
        foreach(string pre in row.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,pre)==0){Train(p,pre);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    private static void Check()
    {
        Model();var saved=new Saved[2];int i=0;
        foreach(bool female in new[]{false,true})
        {
            var p=new PlayerMobile{Body=female?0x191:0x190,Female=female,Player=true,Name="MiasmaPacketFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);
            FixtureSpace.Place(p,new(6100,1620,0),Map.Trammel);while(p.RawStr<50){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Fixture Strength");}
            foreach(string id in new[]{"warlock.miasmabolt","warlock.miasmachain","warlock.abyss"}){Train(p,id,20);}
            Require(SkillMechanics.Rank(p,CombatAbility.EnhancedEntropy)==1,"Prerequisite hard rank");var before=MiasmaPackets.Capture(p);int mana=p.Mana;
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Real +skill helm");ClassGear.Refresh(p);
            var v=SkillMechanics.Values(p,CombatAbility.MiasmaBolt);var d=MiasmaPackets.Capture(p);
            Require(d==MiasmaPackets.Capture(Values(21,20,20,2))&&d.CloudFrames==60&&before.CloudFrames==55,"Actual hard synergies/effective rank/entropy supplier");
            Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.MiasmaBolt)&&!SkillBindings.Instance.AssignQuick(p,0,CombatAbility.MiasmaBolt),"Unfinished Miasma input accepted");
            p.AddToBackpack(helm);ClassGear.Refresh(p);Require(MiasmaPackets.Capture(p)==before&&MiasmaPackets.Capture(v)==d&&p.Mana==mana,"Captured context mutated/live rank failed or numeric supplier charged mana");
            Require(p.EquipItem(helm),"Helm restore");ClassGear.Refresh(p);saved[i++]=new(p.Serial.Value,helm.Serial.Value,female,d);
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        Model();foreach(var s in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var helm=World.FindItem((Serial)s.Helm);ClassGear.Refresh(p);
            Require(p.Female==s.Female&&helm.Parent==p&&SkillMechanics.Rank(p,CombatAbility.MiasmaBolt)==21&&MiasmaPackets.Capture(p)==s.Damage,"Saved Warlock/Witch/gear/numeric payload");
            Require(!SkillBindings.Instance.Assign(p,false,CombatAbility.MiasmaBolt),"Reload enabled unfinished Miasma");
        }
    }
}
