// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GeneratedNpcResourceChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GeneratedNpcResourceChecks));
    private sealed record Held(int Case,uint Actor,uint[] Items,string[] Payloads);
    private sealed record Saved(uint Owner,Held[] Holders,int Generation);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(string command in new[]{"generated-npc-resources-check","generated-npc-resources-reload-check"})
        {ConsoleInputHandler.RegisterCommand(command,"Generated original item resource callback composition",_=>Core.LoopContext.Post(()=>
            {try{Check(command.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",command);}catch(Exception e){Log.Error(e,"{Name}: failed",command);}}));}
    }
    private static EquipmentStatValue[] Fields(params(int Stat,int Value)[] pairs)=>pairs.Where(p=>p.Value!=0)
        .Select(p=>new EquipmentStatValue(EquipmentStatKey.Pack(p.Stat),p.Value)).OrderBy(p=>p.Packed).ToArray();
    private static void BranchCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-resource-callbacks-source-cases.json")));int count=0;
        foreach(var a in doc.RootElement.EnumerateArray())
        {
            if(a[1].GetInt32()!=1){continue;}
            int stat=a[0].GetInt32(),before=a[2].GetInt32(),next=a[3].GetInt32(),current=a[4].GetInt32();
            var actor=new EquipmentStatUnit(new object(),1);var fields=Fields((stat,next),(stat-1,current),(74,17));
            actor.Stats=new(){Owner=actor,OwnerKind=1,Flags=EquipmentStatNode.Extended,BaseValues=fields,FullValues=(EquipmentStatValue[])fields.Clone()};
            var values=new EquipmentStatValues(_=>0,_=>null,_=>null,(_,_,_,_,_)=>{});
            NpcItemResourceCallbacks.Changed(values,actor.Stats,EquipmentStatKey.Pack(stat),before,next,3);
            Require(EquipmentStatValues.Total(actor.Stats,EquipmentStatKey.Pack(stat-1))==a[5].GetInt32(),"Source callback balance "+count);
            int regen=stat==7&&before>0&&before!=next&&current>0?3*(next>>8)>>4:17;
            Require(EquipmentStatValues.Total(actor.Stats,EquipmentStatKey.Pack(74))==regen,"Source callback regeneration gate "+count);count++;
        }
        Require(count==540,"Missing original NPC branch controls");
        foreach(var input in new[]{(Next:0,Rate:3),(Next:16640001,Rate:3),(Next:512,Rate:-1),(Next:512,Rate:32768)})
        {
            var actor=new EquipmentStatUnit(new object(),1);var fields=Fields((6,127),(7,256),(74,17));
            actor.Stats=new(){Owner=actor,OwnerKind=1,Flags=EquipmentStatNode.Extended,BaseValues=fields,FullValues=(EquipmentStatValue[])fields.Clone()};
            var values=new EquipmentStatValues(_=>0,_=>null,_=>null,(_,_,_,_,_)=>{});bool refused=false;
            try{NpcItemResourceCallbacks.Changed(values,actor.Stats,EquipmentStatKey.Pack(7),256,input.Next,input.Rate);}catch(NotSupportedException){refused=true;}
            Require(refused&&EquipmentStatValues.Total(actor.Stats,EquipmentStatKey.Pack(6))==127&&EquipmentStatValues.Total(actor.Stats,EquipmentStatKey.Pack(74))==17,"Unsupported resource domain partially wrote balances");
        }
    }
    private static void Check(bool reload)
    {
        BranchCases();
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Path.Join(Core.BaseDirectory,"generated-npc-stats-fixtures.json")));
        string manifest=Path.Join(Core.BaseDirectory,"generated-npc-resources-fixtures.json");
        if(reload)
        {
            var prior=JsonSerializer.Deserialize<Saved>(File.ReadAllText(manifest));
            Require(saved.Owner==prior.Owner&&saved.Generation==prior.Generation+1&&JsonSerializer.Serialize(saved.Holders)==JsonSerializer.Serialize(prior.Holders),"Resource original identity witness changed across reload");
        }
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generated-npc-resources-cases.json")));
        var inputs=doc.RootElement.GetProperty("numericInputs").EnumerateArray().ToArray();var outputs=doc.RootElement.GetProperty("numericOutputs").EnumerateArray().ToArray();
        Require(inputs.Length==1566&&outputs.Length==6264&&saved.Holders.Length==1566,"Missing resource compositions/original holders");
        for(int variant=0;variant<4;variant++)for(int n=0;n<inputs.Length;n++)
        {
            var held=saved.Holders[n];var actor=World.FindMobile((Serial)held.Actor)as BaseCreature;var input=inputs[n];var ids=held.Items;var expected=outputs[variant*inputs.Length+n];
            Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster?.Serial.Value==saved.Owner&&GeneratedNpcEquipment.Items(actor).Select(i=>i.Serial.Value).SequenceEqual(ids),"Original resource graph owner/inventory lost");
            int level=input.GetProperty("Level").GetInt32(),clock=input.GetProperty("Clock").GetInt32();var native=BattleOrders.Capture(actor);var changes=new List<int[]>();var services=new List<int[]>();
            int Id(EquipmentStatUnit unit)=>unit==null?0:unit.Entity==actor?1:unit.Entity is GeneratedNpcItem item?Array.IndexOf(ids,item.Serial.Value)+2:-1;
            var basis=Fields((0,100),(1,20),(2,50),(3,30),(6,variant==1?1:variant==2?0:123457),(7,256000),(8,variant==1?1:variant==2?0:34567),(9,76800),
                (10,variant==1?1:variant==2?0:12799),(11,25600),(12,level),(21,7),(22,14),(31,80),(74,17));
            var graph=new GeneratedNpcStatGraph(actor,basis,_=>clock,(node,source,key,before,after)=>changes.Add([Id(node.Owner),Id(source),(int)key,before,after]),true,
                _=>services.Add([8,0,0,0]),_=>services.Add([9,0,0,0]),variant==3?0:3);
            GeneratedNpcStatChecks.Compare(graph,ids,expected[0][0],n,0);
            foreach(uint id in ids){graph.Update(id);}GeneratedNpcStatChecks.Compare(graph,ids,expected[0][1],n,1);
            graph.Values.SetBase(graph.Actor.Stats,EquipmentStatKey.Pack(12),level+1,graph.Actor);GeneratedNpcStatChecks.Compare(graph,ids,expected[0][2],n,2);
            foreach(uint id in ids.Reverse()){graph.Transferred(id);}GeneratedNpcStatChecks.Compare(graph,ids,expected[0][3],n,3);
            Require(JsonSerializer.Serialize(changes)==expected[1].GetRawText(),"Resource composition callback order "+variant+"/"+n);
            var wanted=expected[2].EnumerateArray().Where(v=>v[0].GetInt32()is 8 or 9).Select(v=>v.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).ToArray();
            Require(JsonSerializer.Serialize(services)==JsonSerializer.Serialize(wanted),"Resource composition rate/clamp requests");
            Require(BattleOrders.Capture(actor)==native&&ids.Select(id=>((GeneratedNpcItem)World.FindItem((Serial)id)).SavedText).SequenceEqual(held.Payloads),"Numeric resource callback changed native pools/original item payload without live admission");
        }
        File.WriteAllText(manifest,JsonSerializer.Serialize(saved));
        Log.Information("Generated NPC resources:6264source item/resource compositions;540NPC branch controls;injured/tiny/empty/zero-regen;construction/repeat/level/removal;original native items reused without live setter admission");
    }
}
