// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class SavedItemSpecialPropertyChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SavedItemSpecialPropertyChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private sealed class Target(ulong seed):EquipmentPropertyServices
    {
        internal ulong Seed=seed;
        internal int Draws,Count;
        internal bool Socketed;
        internal readonly List<int[]> Trace=[];
        internal override EquipmentPropertyBase Basis=>new(0,0,0,0,0,0,0,0);
        internal override bool Weapon=>false;
        internal override bool Armor=>false;
        internal override bool Throwable=>false;
        internal override int BaseValue(int stat)=>stat==194?Count:0;
        internal override void SetBase(int stat,int value)
        {Require(stat==194,"Socket property wrote a different base stat");Count=value;Trace.Add([2,stat,value,0]);}
        internal void Flag(){Socketed=true;Trace.Add([1,0x800,1,0]);}
        internal override bool GetOrCreateList(int state,int flags)=>throw new InvalidOperationException("Socket property allocated a magic list");
        internal override int ListValue(int stat,int layer)=>throw new InvalidOperationException("Socket property read a magic list");
        internal override void SetList(int stat,int value,int layer)=>throw new InvalidOperationException("Socket property wrote a magic list");
        internal override void AddList(int stat,int value,int layer)=>throw new InvalidOperationException("Socket property added a magic stat");
        internal override int Roll(int bound){int n=(int)SourceRandom.Limited(ref Seed,bound);Draws++;Trace.Add([6,bound,n,0]);return n;}
        internal override void SetEthereal()=>throw new InvalidOperationException("Socket property changed ethereality");
    }
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"saved-socket-property-cases.json")));
        var root=doc.RootElement;int count=0;
        foreach(var row in root.GetProperty("cases").EnumerateArray())
        {
            var a=row[0];int I(int n)=>a[n].GetInt32();var target=new Target(ulong.Parse(a[16].GetString()));int result=0;
            bool valid=I(0)==4&&I(1)!=0&&I(2)!=0;
            int level=Math.Max(1,I(9)),cap=I(14)==0?I(5):level>40?I(7):level>25?I(6):I(5);
            if(valid){result=EquipmentSocketProperty.Apply(new(I(3),I(4),Math.Min(cap,I(8))),new(I(11),I(12),I(13)),I(10),target,target.Flag);}
            Require(result==row[1].GetInt32()&&(target.Socketed?0x800:0)==row[2].GetInt32()
                &&target.Count==row[3].GetInt32()&&target.Seed==ulong.Parse(row[5].GetString())&&target.Draws==row[6].GetInt32()
                &&JsonSerializer.Serialize(target.Trace)==row[7].GetRawText(),"Complete socket property differs at "+count);
            int changedLevel=valid&&I(3)*I(4)>0&&I(14)!=0?level:I(9);
            Require(changedLevel==row[4].GetInt32(),"Source max-socket helper level normalization differs");count++;
        }
        Require(count==29240,"Incomplete socket property source matrix");
        int bases=0;
        foreach(var b in root.GetProperty("bases").EnumerateArray())
        {
            string code=b.GetProperty("Code").GetString();var v=b.GetProperty("Values").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            foreach(int level in new[]{1,25,26,40,41,99})
            {var geometry=EquipmentSocketProperty.Geometry(code,level);Require(geometry==new EquipmentSocketGeometry(v[0],v[1],Math.Min(v[level>40?4:level>25?3:2],v[5])),"Owned socket geometry/cap differs: "+code);}
            bases++;
        }
        Require(bases==692,"Incomplete owned socket base catalogue");
        Log.Information("Saved socket property:29240complete-source traces/692owned bases;flags/base194/range/first-result/RNG/level boundaries pass;no live-item mutation");
    }
    public static void Configure()
    {
        foreach(string name in new[]{"saved-item-special-properties-check","saved-item-special-properties-reload-check"})
        {ConsoleInputHandler.RegisterCommand(name,"Saved socket property and current cap contract",_=>Core.LoopContext.Post(()=>
        {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
}
