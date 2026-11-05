// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GeneratedNpcPhysicalChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GeneratedNpcPhysicalChecks));
    private sealed record Held(int Case,uint Actor,uint[] Items,string[] Payloads);
    private sealed record Saved(uint Owner,Held[] Holders,int Generation);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    public static void Configure()
    {
        foreach(string command in new[]{"generated-npc-physical-check","generated-npc-physical-reload-check"})
        {ConsoleInputHandler.RegisterCommand(command,"Original generated item selected physical rolls",_=>Core.LoopContext.Post(()=>
            {try{Check(command.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",command);}catch(Exception e){Log.Error(e,"{Name}: failed",command);}}));}
    }
    private static void MasteryCases(JsonElement root)
    {
        int count=0;
        foreach(var a in root.GetProperty("mastery").EnumerateArray())
        {
            int mode=a[0].GetInt32(),throwing=a[1].GetInt32(),kind=a[2].GetInt32(),pattern=a[3].GetInt32();
            var full=new List<EquipmentStatValue>();
            for(int stat=342;stat<=347;stat++)for(int layer=1;layer<=40;layer++)
            {
                int value=pattern switch{0=>layer,1=>-layer,2=>layer==40?999:-1,_=>layer==12?55:layer};
                full.Add(new(EquipmentStatKey.Pack(stat,layer),value));
            }
            Require(SourceWeaponMastery.Read(full,mode!=1,mode!=2,mode!=3,throwing!=0,kind,type=>type is 12 or 40)==a[4].GetInt32(),"Complete source layered mastery control "+count);count++;
        }
        Require(count==160,"Missing mastery controls");
    }
    private static EquipmentStatValue[] Basis(int level)=>new[]{(0,100),(1,20),(2,50),(3,30),(7,256000),(9,76800),(11,25600),(12,level),(21,7),(22,14),(31,80),(18,37),(17,51),(185,89),(186,60),(187,300)}
        .Select(p=>new EquipmentStatValue(EquipmentStatKey.Pack(p.Item1),p.Item2))
        .Concat(new[]{(342,45,25),(343,45,50),(344,45,15),(345,45,35),(346,45,65),(347,45,30),(342,69,40),(343,69,80),(344,69,25)}
            .Select(p=>new EquipmentStatValue(EquipmentStatKey.Pack(p.Item1,p.Item2),p.Item3))).ToArray();
    private static void Check(bool reload)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generated-npc-physical-cases.json")));var root=doc.RootElement;MasteryCases(root);
        string manifest=Path.Join(Core.BaseDirectory,"generated-npc-physical-fixtures.json");var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Path.Join(Core.BaseDirectory,"generated-npc-stats-fixtures.json")));
        if(reload){var prior=JsonSerializer.Deserialize<Saved>(File.ReadAllText(manifest));Require(saved.Generation==prior.Generation+1&&saved.Owner==prior.Owner&&Json(saved.Holders)==Json(prior.Holders),"Weapon original native identity/payload witness changed");}
        var hands=root.GetProperty("hands").EnumerateArray().ToArray();var outputs=root.GetProperty("numericOutputs").EnumerateArray().ToArray();Require(hands.Length==3276&&outputs.Length==3276,"Missing hand graph compositions");
        for(int n=0;n<hands.Length;n++)
        {
            var a=hands[n];int shape=a.GetProperty("Case").GetInt32();var input=root.GetProperty("numericInputs")[shape];var held=saved.Holders[shape];var ids=held.Items;
            var actor=World.FindMobile((Serial)held.Actor)as BaseCreature;Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster?.Serial.Value==saved.Owner,"Original weapon recipient lost");
            var native=BattleOrders.Capture(actor);var changes=new List<int[]>();
            int Id(EquipmentStatUnit unit)=>unit==null?0:unit.Entity==actor?1:unit.Entity is GeneratedNpcItem item?Array.IndexOf(ids,item.Serial.Value)+2:-1;
            var graph=new GeneratedNpcStatGraph(actor,Basis(input.GetProperty("Level").GetInt32()),_=>input.GetProperty("Clock").GetInt32(),
                (node,source,key,before,after)=>changes.Add([Id(node.Owner),Id(source),(int)key,before,after]),true,_=>{},_=>{});
            int primary=a.GetProperty("Primary").GetInt32(),secondary=a.GetProperty("Secondary").GetInt32(),selection=a.GetProperty("Selection").GetInt32(),frame=a.GetProperty("Frame").GetInt32();
            uint flags=a.GetProperty("Flags").GetUInt32();bool used=a.GetProperty("Skill").GetBoolean(),row=a.GetProperty("Row").GetBoolean(),reset=a.GetProperty("Reset").GetInt32()!=0;
            var weapon=graph.Weapons(primary==0?0:ids[primary-1],secondary==0?0:ids[secondary-1],used,row,selection,frame,flags);
            void Compare(int stage)
            {
                var expected=outputs[n][0][stage];uint selected=weapon.Selected;int expectedId=expected[0].GetInt32();
                Require((selected==0?0:Array.IndexOf(ids,selected)+2)==expectedId,"Source selected original identity "+n+"/"+stage);
                GeneratedNpcStatChecks.Compare(graph,ids,expected[1],n,stage);
                var physical=expected[2];Require(GeneratedNpcEquipment.WieldType(actor)==physical[0].GetInt32(),"Source wield type "+n+"/"+stage);
                int range=n%3==0?2:1;
                for(int kind=0;kind<3;kind++){Require(weapon.Mastery(kind,48,range)==physical[1][kind].GetInt32(),"Source selected mastery "+n+"/"+stage+"/"+kind);}
                int roll=0;
                foreach(int added in new[]{-150,0,125})
                {
                    var stats=weapon.Physical(48,range,added);
                    Require(stats.MinimumPercent==EquipmentStatValues.SourceRead(graph.Actor.Stats,EquipmentStatKey.Pack(18),EquipmentStatValues.Definition(18))&&stats.MaximumPercent==EquipmentStatValues.SourceRead(graph.Actor.Stats,EquipmentStatKey.Pack(17),EquipmentStatValues.Definition(17)),"Pinned source endpoint stat IDs "+n+"/"+stage);
                    if(n==0&&stage==0&&added==0)
                    {
                        var wrong=stats with{MinimumPercent=graph.Read(186),MaximumPercent=graph.Read(187)};
                        Require(NpcEquipmentDamage.Roll256(stats,_=>0)!=NpcEquipmentDamage.Roll256(wrong,_=>0),"Regression control failed to distinguish old stat binding");
                    }
                    foreach(int share in new[]{64,128,255})foreach(int edge in new[]{0,1})
                    {
                        int bound=0,draws=0;int damage=NpcEquipmentDamage.Roll256(stats,b=>{bound=b;draws++;return edge==0?0:b-1;},share);
                        var want=physical[2][roll++];
                        Require(damage==want[0].GetInt32()&&bound==want[1].GetInt32()&&draws==want[2].GetInt32(),"Source physical roll/RNG chronology "+n+"/"+stage+"/"+(roll-1)+" got "+damage+","+bound+","+draws+" expected "+want.GetRawText());
                    }
                }
            }
            Compare(0);weapon.Enter(reset);Compare(1);weapon.Enter(reset);Compare(2);weapon.Restore(reset);Compare(3);
            weapon=graph.Weapons(primary==0?0:ids[primary-1],secondary==0?0:ids[secondary-1],used,row,selection==0?1:0,frame+256,flags);weapon.Enter(reset);Compare(4);
            if(n==0)
            {
                var original=(GeneratedNpcItem)World.FindItem((Serial)ids[0]);uint selected=weapon.Selected;
                actor.RemoveItem(original);
                try
                {
                    void Refuses(Action operation)
                    {bool refused=false;try{operation();}catch(NotSupportedException){refused=true;}Require(refused,"Immutable physical frame accepted transferred original item");}
                    Refuses(()=>{_=weapon.Selected;});Refuses(()=>{_=weapon.Physical(48,1);});Refuses(()=>{_=weapon.Mastery(1,48,1);});
                    Refuses(()=>weapon.Enter());Refuses(()=>weapon.Restore());
                }
                finally{actor.AddItem(original);}
                Require(weapon.Selected==selected,"Restored original item identity changed physical selection");
            }
            Require(Json(changes)==outputs[n][1].GetRawText(),"Source selected-hand callback order "+n);
            Require(BattleOrders.Capture(actor)==native&&ids.Select(id=>((GeneratedNpcItem)World.FindItem((Serial)id)).SavedText).SequenceEqual(held.Payloads),"Numeric hand context mutated native pools or original item payloads");
        }
        File.WriteAllText(manifest,Json(saved));
        Log.Information("Generated NPC physical:3276source item/hand compositions;294840physical rolls and RNG chronology;160layered mastery controls;original identities,all arrays,wield,mastery and callbacks survive two reloads;no live combat admission");
    }
}
