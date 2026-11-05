// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GeneratedNpcStatChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GeneratedNpcStatChecks));
    private sealed record Held(int Case,uint Actor,uint[] Items,string[] Payloads);
    private sealed record Saved(uint Owner,Held[] Holders,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"generated-npc-stats-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("generated-npc-stats-check",false);Register("generated-npc-stats-reload-check",true);
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest)){foreach(var row in JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)).Holders){if(World.FindMobile((Serial)row.Actor)is BaseCreature b){b.AIObject?.AITimer.Stop();}}}};
    }
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Generated NPC item stat delivery and source dispatcher",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private sealed record Unit(int Id,int Kind,int Class,bool Broken=false);
    private sealed class Gates(int[] input):ItemStatUpdateServices<Unit>
    {
        internal readonly List<int[]> Calls=[];
        internal readonly Unit Target=new(1,input[0],input[1]),Item=new(2,input[2],0,(input[3]&256)!=0),Main=new(3,4,0,(input[3]&512)!=0),Other=new(4,1,0);
        private int Flags=>input[3];private void C(int code,int a=0,int b=0,int c=0)=>Calls.Add([code,a,b,c]);
        internal override int Kind(Unit u)=>u.Kind;internal override int Class(Unit u)=>u.Class;
        internal override bool PlayerBody(Unit u)=>(Flags&4)!=0;
        internal override bool IsType(Unit u,string type)=>type switch{"gem"=>(Flags&8)!=0,"rune"=>(Flags&16)!=0,"weap"=>(Flags&16384)!=0,_=>throw new InvalidOperationException()};
        internal override bool BaseRecord(Unit u)=>(Flags&32)!=0;internal override bool GemRecord(Unit u)=>(Flags&64)!=0;
        internal override int Quality(Unit u)=>(Flags&128)!=0?5:4;internal override bool Broken(Unit u)=>u.Broken;
        internal override bool DynamicPath(Unit u)=>(Flags&2048)!=0;
        internal override Unit MainWeapon(Unit u){C(5);return input[4]switch{1=>Item,2=>Main,_=>null};}
        internal override Unit Owner(Unit u){C(10,u.Id);return ReferenceEquals(u,Item)?input[5]switch{1=>Target,2=>Other,_=>null}:input[6]!=0?Target:null;}
        internal override void InvalidItem()=>C(1);internal override void GemProperties(int mode,Unit owner,Unit item,Unit target)=>C(2,mode,owner?.Id??0,2);
        internal override void Sets(Unit u,Unit item,bool remove)=>C(3,remove?1:0);internal override void Books(Unit u,Unit item,bool add)=>C(4,add?1:0);
        internal override void Merge(Unit u,Unit item,bool damage)=>C(6,item.Id,damage?1:0);internal override void Expire(Unit u,Unit item)=>C(7,item.Id);
        internal override void UpdateRate(Unit u)=>C(8);internal override void ClampVitals(Unit u)=>C(9);
    }
    private static void GateCases(JsonElement root)
    {
        int count=0;foreach(var pair in root.GetProperty("inputs").EnumerateArray().Zip(root.GetProperty("outputs").EnumerateArray()))
        {
            var a=pair.First.EnumerateArray().Select(v=>v.GetInt32()).ToArray();var s=new Gates(a);var target=(a[3]&1)!=0?s.Target:null;var item=(a[3]&2)!=0?s.Item:null;
            if(a[7]!=0){SourceItemStatUpdates.Transferred(s,item,target,(a[3]&4096)!=0,(a[3]&8192)!=0);}else{SourceItemStatUpdates.Update(s,item,target,(a[3]&4096)!=0);}
            Require(Json(s.Calls)==pair.Second.GetRawText(),"Source item update gate/order at "+count+" input="+pair.First.GetRawText()+" actual="+Json(s.Calls)+" expected="+pair.Second.GetRawText());count++;
        }
        Require(count==46800,"Incomplete source item-update gate controls");
    }
    private static EquipmentStatValue[] Basis(int level)=>new[]{(0,100),(1,20),(2,50),(3,30),(7,256000),(9,76800),(11,25600),(12,level),(21,7),(22,14),(31,80)}.Select(p=>new EquipmentStatValue(EquipmentStatKey.Pack(p.Item1),p.Item2)).ToArray();
    private static SkeletonGuardian Actor(PlayerMobile owner)
    {
        var b=new SkeletonGuardian{Name="Generated stat graph [storage fixture]",Frozen=true,CantWalk=true};b.Initialize(owner,2,0);Require(b.SetControlMaster(owner),"Stat holder owner");PersistentMinions.Profiles.Track(b);b.MoveToWorld(owner.Location,owner.Map);b.AIObject?.AITimer.Stop();return b;
    }
    private static PreparedItemRequest Request(JsonElement a)=>new(a[0].GetString(),a[1].GetInt32(),a[2].GetInt32(),a[3].GetInt32(),ulong.Parse(a[4].GetString()),ulong.Parse(a[5].GetString()),a[6].GetUInt32());
    private static bool Fields(EquipmentStatValue[] fields,JsonElement expected)
    {
        var a=fields.Where(v=>v.Value!=0).ToDictionary(v=>v.Packed,v=>v.Value);var b=expected.EnumerateArray().Where(v=>v[1].GetInt32()!=0).ToDictionary(v=>v[0].GetUInt32(),v=>v[1].GetInt32());
        return a.Count==b.Count&&a.All(p=>b.GetValueOrDefault(p.Key)==p.Value);
    }
    internal static void Compare(GeneratedNpcStatGraph graph,uint[] ids,JsonElement expected,int index,int stage)
    {
        Require(graph.Actor.Stats.Flags==expected[0].GetUInt32()&&Fields(graph.Actor.Stats.BaseValues,expected[1])&&Fields(graph.Stats,expected[2]),"Generated actor source flags/base/full at "+index+" stage "+stage+" actual="+Json(graph.Stats)+" expected="+expected.GetRawText());
        Require(expected[3].GetArrayLength()==ids.Length,"Source item count");
        for(int n=0;n<ids.Length;n++)
        {
            var unit=graph.Item(ids[n]).Unit;var e=expected[3][n];var owner=EquipmentStatGraph.Owner(unit,out _);
            Require(unit.Stats.Flags==e[0].GetUInt32()&&(owner==null?0:owner==graph.Actor?1:-1)==e[1].GetInt32()
                &&(unit.Stats.Recipient==null?0:unit.Stats.Recipient==graph.Actor?1:-1)==e[2].GetInt32()
                &&Fields(unit.Stats.BaseValues,e[3])&&Fields(unit.Stats.FullValues,e[4]),"Generated item source flags/topology/base/full at "+index+" item "+n+" stage "+stage);
        }
    }
    private static void Exercise(SkeletonGuardian actor,uint[] ids,JsonElement input,JsonElement expected,int index)
    {
        int level=input.GetProperty("Level").GetInt32(),clock=input.GetProperty("Clock").GetInt32();int life=SkillHealth.Balance256(actor),defense=ClassStats.Defense(actor);
        var original=ids.Select(id=>(GeneratedNpcItem)World.FindItem((Serial)id)).ToArray();var payloads=original.Select(i=>i.SavedText).ToArray();var changes=new List<int[]>();var rates=new List<int[]>();
        int Id(EquipmentStatUnit unit)=>unit==null?0:unit.Entity==actor?1:unit.Entity is GeneratedNpcItem item?Array.IndexOf(ids,item.Serial.Value)+2:-1;
        var graph=new GeneratedNpcStatGraph(actor,Basis(level),_=>clock,(node,source,key,before,after)=>changes.Add([Id(node.Owner),Id(source),(int)key,before,after]),true,
            _=>rates.Add([8,0,0,0]),_=>rates.Add([9,0,0,0]));
        Compare(graph,ids,expected[0][0],index,0);foreach(uint id in ids){graph.Update(id);}Compare(graph,ids,expected[0][1],index,1);
        graph.Values.SetBase(graph.Actor.Stats,EquipmentStatKey.Pack(12),level+1,graph.Actor);Compare(graph,ids,expected[0][2],index,2);
        foreach(uint id in ids.Reverse()){graph.Transferred(id);}Compare(graph,ids,expected[0][3],index,3);
        Require(Json(changes)==expected[1].GetRawText(),"Generated source value-change callback order at "+index+" actual="+Json(changes)+" expected="+expected[1].GetRawText());
        var wanted=expected[2].EnumerateArray().Where(v=>v[0].GetInt32()is 8 or 9).Select(v=>v.EnumerateArray().Select(n=>n.GetInt32()).ToArray()).ToArray();Require(Json(rates)==Json(wanted),"Generated source rate/vital service sequence");
        Require(original.Select(i=>i.SavedText).SequenceEqual(payloads)&&SkillHealth.Balance256(actor)==life&&ClassStats.Defense(actor)==defense,"Explicit numeric graph changed saved payload/live setters without admission");
    }
    private static void Check(bool reload)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generated-npc-stats-cases.json")));var root=doc.RootElement;if(!reload){GateCases(root);}
        using var raw=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-quality-current-cases.json")));var recipes=raw.RootElement.GetProperty("inputs");var items=root.GetProperty("items").EnumerateArray().ToArray();var inputs=root.GetProperty("numericInputs").EnumerateArray().ToArray();var outputs=root.GetProperty("numericOutputs").EnumerateArray().ToArray();Require(inputs.Length==1566&&outputs.Length==1566,"Generated stat compositions");
        var saved=reload?JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)):new Saved(0,[]);Require(!reload||saved.Generation<2,"Generated stat reload generation");PlayerMobile owner;
        if(reload){owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;Require(owner is{Alive:true,Deleted:false},"Original numeric owner lost");}
        else
        {
            owner=new PlayerMobile{Name="GeneratedNpcStatsOwnerFixture",Body=0x190,Player=true};ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,47);FixtureSpace.Place(owner,new(6100,1910,0),Map.Trammel);
            Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _)&&ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Stat holder talent fixture");
        }
        var holders=new List<Held>();
        for(int n=0;n<inputs.Length;n++)
        {
            var input=inputs[n];SkeletonGuardian actor;uint[] ids;
            if(reload)
            {
                var original=saved.Holders[n];Require(original.Case==n,"Numeric original context changed");actor=World.FindMobile((Serial)original.Actor)as SkeletonGuardian;ids=original.Items;
                Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster==owner&&GeneratedNpcEquipment.Items(actor).Select(i=>i.Serial.Value).SequenceEqual(ids)
                    &&ids.Select(id=>((GeneratedNpcItem)World.FindItem((Serial)id)).SavedText).SequenceEqual(original.Payloads),"Original numeric holder/item identity,payload or owner changed");
            }
            else
            {
                actor=Actor(owner);var made=new List<uint>();foreach(int i in input.GetProperty("Items").EnumerateArray().Select(v=>v.GetInt32()))
                {var item=items[i];var r=Request(recipes[item.GetProperty("Case").GetInt32()]);Require(GeneratedNpcEquipment.TryCreate(actor,owner,(EquipMonster)input.GetProperty("Kind").GetInt32(),r,item.GetProperty("Body").GetInt32(),out var created),"Numeric original delivery failed");made.Add(created.Serial.Value);}ids=made.ToArray();
            }
            Exercise(actor,ids,input,outputs[n],n);holders.Add(new(n,actor.Serial.Value,ids,ids.Select(id=>((GeneratedNpcItem)World.FindItem((Serial)id)).SavedText).ToArray()));
        }
        if(!reload)
        {
            var actor=Actor(owner);int changes=0;
            foreach(int? time in new int?[]{null,-1,360})
            {
                bool refused=false;try{_=new GeneratedNpcStatGraph(actor,Basis(37).Append(new(EquipmentStatKey.Pack(269),123)).ToArray(),_=>time,(_,_,_,_,_)=>changes++,false,_=>{},_=>{});}catch(NotSupportedException){refused=true;}Require(refused&&changes==0,"Missing/invalid time silently admitted or emitted callbacks");
            }
            Require(GeneratedNpcEquipment.TryCreate(actor,owner,EquipMonster.Other,new("ssd",37,3,0,123,456,511),4,out var item),"Guard item fixture");var graph=new GeneratedNpcStatGraph(actor,Basis(37),_=>null,(_,_,_,_,_)=>{},false,_=>{},_=>{});item.Delete();
            bool stale=false;try{graph.Read(31);}catch(NotSupportedException){stale=true;}Require(stale,"Deleted original remained readable through stale graph");actor.Delete();
        }
        File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,holders.ToArray(),reload?saved.Generation+1:0)));
        Log.Information("Generated NPC stats:46800complete dispatcher controls;1566source numeric compositions including18multi-item orders;original native holders/items retain raw source flags,arrays,damage policy,repeat/level/removal callbacks and continuation through two reloads;clock and stale guards pass;live setter/skill/event admission remains open");
    }
}
