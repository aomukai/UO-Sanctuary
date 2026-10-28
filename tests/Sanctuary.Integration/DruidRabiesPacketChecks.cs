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

public static class DruidRabiesPacketChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidRabiesPacketChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("druid-rabies-packet-check","Prepared Rabies poison capture and remaining infection length",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("druid-rabies-packet-check: passed");}catch(Exception e){Log.Error(e,"druid-rabies-packet-check: failed");}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile owner,CombatAbility ability,int rank=1)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(DiabloClass.Druid,row.TalentId,out var talent),"Druid talent");
        foreach(string prerequisite in talent.Prerequisites)
        {if(ClassCreation.Profiles.GetRank(owner.Serial,prerequisite)==0){Train(owner,SkillMechanics.All.Single(x=>x.TalentId==prerequisite).Ability);}}
        while(ClassCreation.Profiles.GetRank(owner.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,row.TalentId,out _),"Train "+row.TalentId);}
    }
    private static void Check()
    {
        var definition=SkillMechanics.For(CombatAbility.Rabies);
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-rabies-factory-source-probe.json")));
        var names=source.RootElement.GetProperty("damageFields").EnumerateArray().Select(x=>x.GetString()).ToArray();int compared=0;
        foreach(var row in source.RootElement.GetProperty("damage").EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();int Expected(string field)=>row[5][Array.IndexOf(names,field)].GetInt32();
            var values=new SkillValues(definition,key=>key==definition.SourceKey?V(0):0,key=>key=="Plague Poppy"?V(1):0,99);
            var packet=DruidRabiesPackets.Prepare(values,V(2));var p=packet.Elements;
            Require(p.PoisonMin==Expected("nPoisonMinDamage")&&p.PoisonMax==Expected("nPoisonMaxDamage")&&p.PoisonFrames==Expected("nPoisonLength")&&p.PoisonCount==0,"Rabies source poison endpoint/length/count");
            Require(packet.Min256==0&&packet.Max256==0&&packet.Percent==0&&packet.DemonPercent==0&&packet.UndeadPercent==0&&!packet.Critical&&packet.PoisonMastery==0
                &&p==new MissileSourceStats(PoisonMin:p.PoisonMin,PoisonMax:p.PoisonMax,PoisonFrames:p.PoisonFrames),"Rabies source-zero packet inherited weapon/item/mastery");
            int draws=0;int Random(int span){draws++;return 0;}
            packet.Roll(null,Random,out int rate);Require(rate==p.PoisonMin&&draws==1,"Rabies packet consumes other damage RNG or applies mastery twice");compared++;
        }
        Require(compared==12240,"Rabies source damage context count");
        using var spread=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-rabies-spread-source-probe.json")));int windows=0;
        foreach(var row in spread.RootElement.EnumerateArray())
        {
            // Unchanged source hit53 under its ordinary successful owner/table
            // scene, after a child was emitted, gives the reference window.
            if(row[5].GetInt32()!=0||row[12].GetInt32()==0){continue;}
            int rank=row[0].GetInt32(),expected=row[16].GetInt32();
            bool valid=DruidRabiesPackets.TryInfectionLength(row[15].GetInt32(),1000,100+10*(rank-1),out int frames);
            Require(valid==(expected==2)&&(!valid||frames==row[18].GetInt32()),"Rabies absolute deadline/source hit window");windows++;
        }
        Require(windows>100,"Missing source infection windows");
        Require(!DruidRabiesPackets.TryInfectionLength(long.MaxValue,0,100,out _)
            &&!DruidRabiesPackets.TryInfectionLength(100,-1,100,out _),"Invalid/overflowing infection clock accepted");
        var owner=MagicArrowChecks.Fresh("RabiesPacketFixture",DiabloClass.Druid);
        try
        {
            Train(owner,CombatAbility.Rabies);int mana=NecromancerEffects.ManaBalance256(owner);var charges=DruidAttackCharges.Snapshot();
            var initial=DruidRabiesPackets.Capture(owner,1);Train(owner,CombatAbility.PoisonCreeper);
            var synergy=DruidRabiesPackets.Capture(owner,1);
            Require(initial.Elements.PoisonMin==48&&initial.Elements.PoisonMax==112&&synergy.Elements.PoisonMin==57&&synergy.Elements.PoisonMax==134,"Live Rabies hard Creeper synergy capture");
            var helm=NamedGear.Create("unique/72",99,new Random(31),true);owner.AddToBackpack(helm);Require(owner.EquipItem(helm),"Real all-skill helm");ClassGear.Refresh(owner);
            Require(DruidRabiesPackets.Capture(owner,1)==synergy&&SkillMechanics.Rank(owner,CombatAbility.Rabies)>1,"Child lost retained rank or used soft Creeper synergy");
            Train(owner,CombatAbility.PoisonCreeper,2);var updated=DruidRabiesPackets.Capture(owner,1);
            Require(updated.Elements.PoisonMin==67&&synergy.Elements.PoisonMin==57&&initial.Elements.PoisonMin==48,"Child failed live synergy or mutated an existing packet");
            var weapon=new Dagger();ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));owner.AddToBackpack(weapon);Require(owner.EquipItem(weapon),"Actual weapon equip");ClassGear.Refresh(owner);
            Require(DruidRabiesPackets.Capture(owner,1)==updated,"Child inherited equipped weapon damage");owner.AddToBackpack(weapon);ClassGear.Refresh(owner);
            Require(DruidRabiesPackets.Capture(owner,1)==updated&&NecromancerEffects.ManaBalance256(owner)==mana&&charges.SequenceEqual(DruidAttackCharges.Snapshot()),"Packet capture requires weapon or changes resources/charges");
        }
        finally{owner.Delete();}
        Log.Information("Rabies prepared packet: {Count} full-source damage comparisons / {Windows} source infection windows and live hard/soft/retained-rank/weapon capture pass; casting/spread/world unconnected",compared,windows);
    }
}
