// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcDamageFillChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcDamageFillChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static int[] Array(JsonElement a)=>a.EnumerateArray().Select(n=>n.GetInt32()).ToArray();
    private static int[] Fields(NpcFillPacket p)=>[p.Physical,p.Fire,p.Lightning,p.Cold,p.Magic,p.Poison,p.ColdFrames,p.PoisonFrames,p.StunFrames,p.LifeDrain,p.ManaDrain,p.StaminaDrain,p.AdditionalPercent,(int)p.HitFlags,p.ResultFlags,p.ConversionType,p.ConversionPercent,p.FreezeFrames,p.BurnFrames,p.HitClass,p.Overlay];
    private static NpcFillPacket Packet(int[] a)=>new(a[0],a[1],a[2],a[3],a[4],a[5],a[6],a[7],a[8],a[9],a[10],a[11],a[12],(uint)a[13],a[14],a[15],a[16],a[17],a[18],a[19],a[20]);
    private sealed class Services(JsonElement row):NpcDamageFillServices
    {
        private readonly int[] A=Array(row.GetProperty("args"));
        private readonly Dictionary<int,int> Stats=row.GetProperty("stats").EnumerateArray().ToDictionary(v=>v[0].GetInt32(),v=>v[1].GetInt32());
        internal readonly List<int[]> Trace=[];
        private int Draws,Ac=20;
        internal override bool AttackerPresent=>true;internal override bool DefenderPresent=>true;
        internal override int ActorKind=>A[0];internal override bool GoodAlignment=>A[2]!=0;
        internal override bool TargetMonster=>A[5]!=15;internal override bool Demon=>A[12]!=0;internal override bool Undead=>A[13]!=0;
        internal override int MonsterType=>A[11];internal override int Armor{get=>Ac;set{Ac=value;Trace.Add([4,31,value]);}}
        internal override bool Blunt=>A[7]!=0&&A[9]!=0;internal override int Hireling=>A[1];internal override int IntrinsicCritical=>A[10];
        private int Read(int n)=>Stats.GetValueOrDefault(n);
        internal override int Stat(NpcFillStat s)=>Read((int)s);
        internal override IEnumerable<EquipmentStatValue> TypeDamage=>Enumerable.Range(1,130).Select(i=>new EquipmentStatValue(EquipmentStatKey.Pack(180,i),(i-1)%3==0?-5:10));
        internal override bool TypeMatches(int layer,int type)=>layer%3==type%3;
        internal override int WeaponCritical()=>A[7]!=0?35:0;
        internal override void Enter(bool ranged)=>Trace.Add([2,ranged?1:0,0]);
        internal override void Restore(bool ranged)=>Trace.Add([3,ranged?1:0,0]);
        internal override void Overlay(int value)=>Trace.Add([6,value,0]);
        internal override int Physical(int extra,int initial,int share)
        {
            bool armed=A[7]!=0;int min=armed&&A[8]!=0?23:21;
            return NpcEquipmentDamage.Roll256(new(Read(min),Read(min+1),Read(111),Read(25)+extra+(armed?50:0),Read(0),Read(2),armed,100,75,Read(18),Read(17)),Limited,share,initial);
        }
        internal override int Limited(int bound)
        {int value=A[5]%2!=0?bound-1:0;Trace.Add([0,bound,value]);if(A[14]!=0){Stats[337]=100;Stats[326]=3;}return value;}
        internal override uint Raw()
        {
            int[][] values=[[0,0,0,0],[99,0,99,0],[35,50,45,5],[99,99,99,99]];int value=values[A[6]][Draws++%4];Trace.Add([1,value,0]);
            if(A[14]!=0){Stats[329]=Read(329)+7;}return(uint)value;
        }
        internal override NpcFillPacket AttackedInMelee(NpcFillPacket p)
        {Trace.Add([5,3,0]);return A[15]!=0?p with{Physical=p.Physical+257,ConversionType=10,ConversionPercent=37}:p;}
    }
    internal static void SourceCases(string file="npc-damage-fill-cases.json",int expectedCount=5120)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,file)));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var s=new Services(row);var args=Array(row.GetProperty("args"));var p=NpcDamageFill.Fill(s,Packet(Array(row.GetProperty("initial"))),args[3]!=0,args[4]);
            Require(Fields(p).SequenceEqual(Array(row.GetProperty("output"))),"Full source fill fields at "+count+" args="+Json(args)+" got="+Json(Fields(p))+" expected="+row.GetProperty("output").GetRawText());
            Require(s.Armor==row.GetProperty("armor").GetInt32(),"Full source target armor at "+count);
            if(count==20&&expectedCount==5120)
            {
                // Source variant5 removes initial cold and applies -100% mastery:
                // no cold damage and no cold length. Exercise the older actual
                // equipment packet too,which previously returned raw ColdFrames.
                var raw=new MissileSourceStats(ColdMin:5*256,ColdMax:8*256,ColdFrames:31);
                var rolled=NpcEquipmentDamage.RollCaptured(1,2,new(1,2,0,0,0,0,false),new(0,0,0,0),
                    ()=>new(raw,0,0,-100,0),()=>new(1,1,new(),0,0),_=>0,()=>99);
                Require(p.Cold==0&&p.ColdFrames==0&&rolled.Damage.Elements.Cold==0&&rolled.Damage.Elements.ColdMs==0&&rolled.ColdFrames==p.ColdFrames,"Zero-cold ordinary packet must match full source without chill length");
            }

            Require(Json(s.Trace)==row.GetProperty("trace").GetRawText(),"Full source RNG/entry/event/overlay/restore chronology at "+count+" got="+Json(s.Trace)+" expected="+row.GetProperty("trace").GetRawText());count++;
        }
        Require(count==expectedCount,"Missing full source fill contexts");
    }
    private sealed record Held(int Case,uint Actor,uint[] Items,string[] Payloads);
    private sealed record Saved(uint Owner,Held[] Holders,int Generation);
    private static EquipmentStatValue[] Basis()=>new[]{(0,17),(2,11),(21,3),(22,11),(23,13),(24,21),(111,2),(25,25),(18,37),(17,51),(48,2),(49,6),(50,3),(51,7),(54,5),(55,8),(52,1),(53,3),(56,31),(66,47),(329,37),(330,27),(331,51),(357,13),(332,25),(57,769),(58,1537),(59,103),(326,2),(337,50),(141,45),(120,-25),(121,50),(122,-25),(60,1),(61,3),(62,2),(63,4),(64,3),(65,5)}.Select(p=>new EquipmentStatValue(EquipmentStatKey.Pack(p.Item1),p.Item2)).ToArray();
    private static void Bridge(bool reload)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Path.Join(Core.BaseDirectory,"generated-npc-stats-fixtures.json")));
        string manifest=Path.Join(Core.BaseDirectory,"npc-damage-fill-fixtures.json");
        if(reload){using var witness=JsonDocument.Parse(File.ReadAllText(manifest));var old=witness.RootElement.GetProperty("Original").Deserialize<Saved>();Require(old.Generation+1==saved.Generation&&old.Owner==saved.Owner&&Json(old.Holders)==Json(saved.Holders),"Fill original identity/payload witness changed");}
        using var cases=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generated-npc-physical-cases.json")));
        var hands=cases.RootElement.GetProperty("hands").EnumerateArray().Take(64).Concat(cases.RootElement.GetProperty("hands").EnumerateArray().Where(a=>a.GetProperty("Case").GetInt32()>=1548).Take(32)).ToArray();
        var records=new List<object>();int n=0;
        foreach(var hand in hands)
        {
            var held=saved.Holders[hand.GetProperty("Case").GetInt32()];var actor=World.FindMobile((Serial)held.Actor)as BaseCreature;
            Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster?.Serial.Value==saved.Owner,"Original fill recipient lost");
            var native=BattleOrders.Capture(actor);var ids=held.Items;var trace=new List<int[]>();
            GeneratedNpcStatGraph Graph()=>new(actor,Basis(),_=>100,(_,_,_,_,_)=>{},true,_=>{},_=>{});
            var graph=Graph();var preview=Graph();
            GeneratedNpcWeaponFrame Frame(GeneratedNpcStatGraph g)
            {
                int p=hand.GetProperty("Primary").GetInt32(),s=hand.GetProperty("Secondary").GetInt32();
                return g.Weapons(p==0?0:ids[p-1],s==0?0:ids[s-1],hand.GetProperty("Skill").GetBoolean(),hand.GetProperty("Row").GetBoolean(),hand.GetProperty("Selection").GetInt32(),hand.GetProperty("Frame").GetInt32(),hand.GetProperty("Flags").GetUInt32());
            }
            bool ranged=n%2!=0;int share=new[]{0,1,64,128,255}[n%5],armor=20;var w=Frame(graph);var pw=Frame(preview);pw.Enter(ranged);
            int range=n%3==0?2:1;var physical=pw.Physical(48,range);int crit=pw.Mastery(2,48,range);bool blunt=pw.SelectedBlunt;
            var stats=Enumerable.Range(0,368).Select(id=>new[]{id,preview.Read(id)}).Where(a=>a[1]!=0).ToArray();var typed=preview.Stats.Where(s=>s.Stat==180).Take(128).Select(s=>new[]{(int)(ushort)s.Packed,s.Value}).ToArray();
            var target=new GeneratedNpcFillTarget(true,true,n%2!=0,n%3==0,7,()=>armor,v=>{armor=v;trace.Add([4,31,v]);},(layer,type)=>layer%3==type%3);
            int draw=0;int Limited(int bound){int v=n%2==0?0:bound-1;trace.Add([0,bound,v]);return v;}uint Raw(){int v=new[]{99,0,99,0}[draw++%4];trace.Add([1,v,0]);return(uint)v;}
            NpcFillPacket Event(NpcFillPacket p){trace.Add([5,3,0]);return p with{Physical=p.Physical+257,ConversionType=10,ConversionPercent=37};}
            var initial=new NpcFillPacket(Physical:257,ColdFrames:7,PoisonFrames:11,AdditionalPercent:17,ConversionType:n%4==0?5:0,ConversionPercent:37,HitClass:3);
            var result=w.Fill(target,initial,48,range,true,n%2,n%3==0?0:5,Limited,Raw,Event,ranged,share);
            pw.Restore(ranged);Require(Json(graph.Stats)==Json(preview.Stats),"Fill did not restore original graph topology "+n);
            Require(BattleOrders.Capture(actor)==native&&ids.Select(id=>((GeneratedNpcItem)World.FindItem((Serial)id)).SavedText).SequenceEqual(held.Payloads),"Fill mutated native pools or original item payloads");
            var physicalValues=new[]{physical.Minimum,physical.Maximum,physical.NormalDamage,physical.DamagePercent,physical.Strength,physical.Dexterity,physical.Weapon?1:0,physical.StrengthCoefficient,physical.DexterityCoefficient,physical.MinimumPercent,physical.MaximumPercent};
            records.Add(new{args=new[]{1,n%2,1,ranged?1:0,share,n%2,blunt?1:0,n%3==0?0:5,n%2,n%3==0?1:0},stats,typed,physical=physicalValues,mastery=crit,initial=Fields(initial),output=Fields(result),armor,trace});
            if(n==0)
            {
                var original=(GeneratedNpcItem)World.FindItem((Serial)ids[0]);actor.RemoveItem(original);
                try{bool refused=false;try{_=w.Fill(target,initial,48,range,true,0,0,Limited,Raw,Event);}catch(NotSupportedException){refused=true;}Require(refused,"Full fill accepted a transferred original item");}
                finally{actor.AddItem(original);}
            }
            n++;
        }
        File.WriteAllText(manifest,Json(new{Original=saved,Bridge=records}));
        Require(n==96,"Missing original-item full-fill bridge contexts");
    }
    public static void Configure()
    {
        foreach(string command in new[]{"npc-damage-fill-check","npc-damage-fill-reload-check"})
        {ConsoleInputHandler.RegisterCommand(command,"Pinned full NPC damage fill and saved original-item bridge",_=>Core.LoopContext.Post(()=>
        {try{SourceCases();Bridge(command.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("NPC full damage fill:5120source contexts;96original-item bridge exports;fields,RNG,event,overlay and restore chronology;no live HP admission");Log.Information("{Name}: passed",command);}catch(Exception e){Log.Error(e,"{Name}: failed",command);}}));}
    }
}
