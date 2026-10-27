// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class TotemAiChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(TotemAiChecks));
    private readonly record struct Request(int Kind,int A=0,int B=0,int C=0,int D=0);
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("totem-ai-check","Complete source Totem decisions and ordered services",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("totem-ai-check: passed");}catch(Exception e){Log.Error(e,"totem-ai-check: failed");}}));
    private static void Check()
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"totem-ai-source-probe.json")));int count=0;
        foreach(string species in new[]{"oaksage","heartofwolverine","spiritofbarbs"})
        foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
        foreach(var row in document.RootElement.EnumerateArray())
        {
            var input=row[0];int N(int i)=>input[i].GetInt32();var calls=new List<Request>();
            void Idle(int frames)=>calls.Add(new(0,frames));
            TotemTarget Find(int range){calls.Add(new(1,range));return new(N(1)!=0?3:0,N(2)!=0);}
            int Percent(int index){int roll=N(index==0?5:6);calls.Add(new(2,index,roll));return roll;}
            bool Escape(int target,int distance,bool delete){calls.Add(new(3,target,distance,delete?1:0));return N(7)!=0;}
            int Distance(){calls.Add(new(4));return N(3);}
            bool Warp(){calls.Add(new(5,40,70));return N(8)!=0;}
            bool Move(int mode,bool run,int speed,int leash){calls.Add(new(6,mode,run?1:0,speed,leash));return N(9)!=0;}
            bool Common(int target,bool combat,int flag,int leash){calls.Add(new(7,target,combat?1:0,flag,leash));return N(10)!=0;}
            DruidTotemAi.Think(new(N(0)!=0,N(4),difficulty,species),new(Idle,Find,Percent,Escape,Distance,Warp,Move,Common));
            var expected=row[1].EnumerateArray().Select(x=>new Request(x[0].GetInt32(),x[1].GetInt32(),x[2].GetInt32(),x[3].GetInt32(),x[4].GetInt32()));
            Require(calls.SequenceEqual(expected),"Totem source decision/order: "+input);count++;
        }
        Require(count==46089,"Incomplete source/difficulty matrix");
        bool refused=false;try{DruidTotemAi.Think(new(true,1),new(_=>{},_=>new(0,false),_=>100,(_,_,_)=>false,()=>0,()=>false,(_,_,_,_)=>false,(_,_,_,_)=>false));}
        catch(ArgumentException){refused=true;}Require(refused,"Invalid percentage silently accepted");
        Log.Information("Totem AI:5121complete source contexts across3current difficulty rows,46089native ordered decision comparisons pass for three mapped spirit rows; geometry and actual controller acceptance remain separate");
    }
}
