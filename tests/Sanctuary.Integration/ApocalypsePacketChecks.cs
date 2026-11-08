// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ApocalypsePacketChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ApocalypsePacketChecks));
    private sealed record Saved(uint Owner,uint First,uint Second,ApocalypsePacket Packet,Point3D Point,int Map,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"apocalypse-packets-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("apocalypse-packets-check",Check);Register("apocalypse-packets-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Apocalypse approved endpoints and shared explosion roll",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Source()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"spell-packet-roll-cases.json")));int n=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            var i=row.GetProperty("input");var o=row.GetProperty("output");int I(string k)=>i.GetProperty(k).GetInt32();ulong seed=i.GetProperty("seed").GetUInt64();
            var value=SourceSpellPacketRoll.PhysicalFire(ref seed,I("pmin"),I("pmax"),I("fmin"),I("fmax"),I("physical"),I("fire"));
            Require(value.Physical256==o.GetProperty("physical").GetInt32()&&value.Fire256==o.GetProperty("fire").GetInt32()&&seed==o.GetProperty("seed").GetUInt64(),"Current packet wrapper mismatch "+n);
            foreach(var copy in o.GetProperty("copies").EnumerateArray())
            {Require(copy[0].GetInt32()==value.Physical256&&copy[1].GetInt32()==value.Fire256,"Current scalar copy mismatch");}n++;
        }
        Require(n==132,"Incomplete packet source fixture");
        using var maximum=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"apocalypse-maximum-cases.json")));int endpoints=0;
        foreach(var item in maximum.RootElement.EnumerateArray())
        {
            int rank=item.GetProperty("rank").GetInt32(),hard=item.GetProperty("hard").GetInt32(),mastery=item.GetProperty("mastery").GetInt32();
            var definition=SkillMechanics.For(CombatAbility.Apocalypse);
            var values=new SkillValues(definition,k=>k==definition.SourceKey?rank:40,k=>k==definition.SourceKey?1:hard,99);
            Require(ApocalypsePackets.Endpoint(values,true,mastery)==item.GetProperty("maximum256").GetInt32(),"Current maximum getter mismatch "+rank+"/"+hard+"/"+mastery);endpoints++;
        }
        Require(endpoints==108,"Incomplete current maximum fixture");
        // Independent tier accumulation: effective own rank, hard synergies.
        int[] mins=[23,46,69,75,95],maxs=[25,50,75,95,115];
        foreach(int rank in new[]{1,2,8,9,16,17,22,23,28,29,40,255})
        foreach(int hard in new[]{0,1,20})
        {
            var row=SkillMechanics.For(CombatAbility.Apocalypse);
            var values=new SkillValues(row,k=>k==row.SourceKey?rank:40,k=>k==row.SourceKey?1:hard,99);
            int min=80,max=100;
            for(int r=2;r<=rank;r++){int tier=r<=8?0:r<=16?1:r<=22?2:r<=28?3:4;min+=mins[tier];max+=maxs[tier];}
            int Scale(int v)=>(int)(v*256L+v*256L*(hard*20)/100);
            Require(values.Damage256(false)==Scale(min)&&values.Damage256(true)==Scale(max),"Approved table endpoint/synergy boundary "+rank+"/"+hard);
        }
    }
    private static Item Equip(PlayerMobile owner,string name)
    {
        var row=NamedGear.Catalog.Items.Single(x=>x.SourceKey==name);Require(NamedGear.Available(row.Id),"Unavailable endpoint fixture "+name);
        var item=NamedGear.Create(row.Id,99,new Random(173),true);owner.AddToBackpack(item);Require(owner.EquipItem(item),"Endpoint gear refused "+name);ClassGear.Refresh(owner);return item;
    }
    private static ApocalypsePacket Endpoints(PlayerMobile owner)
    {
        ulong seed=SourceRandom.InitLow(42);var basis=ApocalypsePackets.Capture(owner,ref seed);
        Require(basis.Rank==1&&basis.Minimum256==96*256&&basis.Maximum256==120*256,"Rank1 hard synergy endpoints");
        owner.RawStr=owner.RawDex=1000;if(owner.Backpack==null){owner.AddItem(new Backpack());}
        var gloves=Equip(owner,"Magefist");seed=basis.SeedBefore;var soft=ApocalypsePackets.Capture(owner,ref seed);
        Require(soft.Rank==2&&soft.Minimum256==103*256*120/100&&soft.Maximum256==125*256*120/100
            &&ClassCreation.Profiles.GetRank(owner.Serial,"warlock.ringoffire")==1,"+skills altered hard synergy or base endpoint");
        // Ormus' Robes is currently unavailable in the named-item catalog.
        // Exercise an explicit supplied fire modifier through the production
        // endpoint helper;do not bypass item admission to fabricate real gear.
        var values=SkillMechanics.Values(owner,CombatAbility.Apocalypse);int bonus=15;
        Require(ApocalypsePackets.Endpoint(values,false,bonus)==(int)((long)soft.Minimum256*(100+bonus)/100)
            &&ApocalypsePackets.Endpoint(values,true,bonus)==(int)((long)soft.Maximum256*(100+bonus)/100),"Fire enhancement omitted or doubled");
        gloves.Delete();ClassGear.Refresh(owner);
        ClassSkillChecks.Train(owner,"warlock.apocalypse",2);
        Require(basis.Rank==1&&basis.Minimum256==96*256&&basis.Maximum256==120*256,"Captured explosion changed after gear/training");
        return basis;
    }
    private static void Hits(PlayerMobile owner,BaseCreature first,BaseCreature second,ApocalypsePacket packet)
    {
        ApocalypsePackets.Validate(packet);bool rejected=false;
        try{ApocalypsePackets.Validate(packet with{Fire256=packet.Fire256+1});}catch(InvalidDataException){rejected=true;}
        Require(rejected,"Altered packet accepted");
        foreach(var target in new[]{first,second}){target.SetResistance(ResistanceType.Fire,0);}
        int a=SkillHealth.Balance256(first),b=SkillHealth.Balance256(second);
        var x=ApocalypsePackets.ReleasePrepared(owner,first,packet,pierceOverride:0);var y=ApocalypsePackets.ReleasePrepared(owner,second,packet,pierceOverride:0);
        Require(x.Applied&&y.Applied&&x.Damage256==y.Damage256&&a-SkillHealth.Balance256(first)==b-SkillHealth.Balance256(second)
            &&x.Damage256==NamedElements.Resolve256(owner,first,packet.Fire256,"fire",enhance:false,pierceOverride:0,skillResistanceDelta:-5).Damage256,
            "Shared explosion rerolled,enhanced again or read new rank");
        int before=SkillHealth.Balance256(first);Require(!ApocalypsePackets.ReleasePrepared(owner,first,packet,false).Applied&&SkillHealth.Balance256(first)==before,"Allocation refusal packet");
        Require(!WarlockCombat.Implemented(CombatAbility.Apocalypse),"Packet work enabled unfinished launch/area");
    }
    private static void Check()
    {
        Source();var owner=ClassSkillChecks.Player(DiabloClass.Warlock);FixtureSpace.Corridor(owner,6400,45,2700);var first=FireBoltChecks.Target(owner,5);var second=FireBoltChecks.Target(owner,6);
        var packet=Endpoints(owner);Hits(owner,first,second,packet);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,first.Serial.Value,second.Serial.Value,packet,owner.Location,owner.Map.MapID,0)));
        Log.Information("Apocalypse packets:132 current wrapper/seed comparisons;108 current maximum getter comparisons;36 pinned boundary/hard-synergy pairs;real Magefist +skills/supplied fire modifier;one explosion roll reused across two actual HP recipients;immutable saved packet fixture. Endpoint fallback approved;active explosion persistence/area/launch unimplemented.");
    }
    private static void Reload()
    {
        Source();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)s.Owner);owner.MoveToWorld(s.Point,Map.Maps[s.Map]);
        Require(s.Generation<2,"Extra packet reload");Hits(owner,(BaseCreature)World.FindMobile((Serial)s.First),(BaseCreature)World.FindMobile((Serial)s.Second),s.Packet);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
