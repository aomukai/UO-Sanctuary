// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;
public static class ItemProcCallbackChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ItemProcCallbackChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"item-proc-callback-fixtures.json");
    private sealed class Services(JsonElement input):ItemProcServices
    {
        private int V(int i)=>input[i].GetInt32();
        internal ulong Seed=ulong.Parse(input[7].GetString());
        internal readonly List<long[]> Trace=[];
        internal override bool HasSource=>V(1)!=0;
        internal override bool HasTarget=>V(2)!=0;
        internal override bool HasPacket=>V(3)!=0;
        internal override uint HitFlags=>HasPacket?(uint)V(4):throw new InvalidOperationException("Absent packet hitflag dereference");
        internal override ushort ResultFlags=>HasPacket?(ushort)V(5):throw new InvalidOperationException("Absent packet resultflag dereference");
        internal override int LevelBits=>V(11);
        internal override int Chance(int stat,int layer){Trace.Add([0,stat,layer]);return V(6);}
        internal override uint Roll(){uint n=SourceRandom.Roll(ref Seed);Trace.Add([1,n]);return n;}
        internal override ItemProcSkill Skill(int id){Trace.Add([2,id]);return V(8)!=0?new(V(9)!=0):null;}
        internal override (int X,int Y) FirstPathPoint()=>(17,-3);
        internal override void CastTarget(bool targetAsCaster,int skill,int level,int flag)
            =>Trace.Add([3,targetAsCaster?2:1,skill,level,2,flag]);
        internal override void CastPosition(int skill,int level,int x,int y,int flag)=>Trace.Add([4,1,skill,level,x,y,flag]);
    }
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("item-proc-callback-check",false);Register("item-proc-callback-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Shared source item-proc callback contract;cast services remain explicit",_=>Core.LoopContext.Post(()=>
    {
        try
        {
            using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-proc-callback-cases.json")));int count=0;
            foreach(var row in source.RootElement.EnumerateArray())
            {
                var s=new Services(row[0]);int result=ItemProcCallbacks.Run(row[0][0].GetInt32(),row[0][12].GetUInt32(),s);
                Require(result==row[1][0].GetInt32()&&s.Seed==ulong.Parse(row[1][1].GetString()),"Complete source proc result/RNG at "+count);
                var trace=row[2].EnumerateArray().Select(r=>r.EnumerateArray().Select(v=>v.GetInt64()).ToArray()).ToArray();
                Require(s.Trace.Count==trace.Length&&s.Trace.Zip(trace).All(p=>p.First.SequenceEqual(p.Second)),"Complete proc service ordering/caster/target/flag/position at "+count);count++;
            }
            Require(count==25920,"Incomplete source proc callback controls");
            Require(!AssassinCombat.Implemented(CombatAbility.ShadowWarrior)&&!AssassinCombat.Implemented(CombatAbility.ShadowMaster),"Proc contract enabled unfinished summon");
            const string scope="source-callback-contract-only:cast-services-not-spell-delivery";
            if(reload){Require(File.ReadAllText(Manifest)==scope,"Original explicit proc callback fixture scope");}else{File.WriteAllText(Manifest,scope);}
            Log.Information("Item proc callbacks:25920complete gate/decode/LCG/lookup/cast-role/flag/position/return controls;Blood chance gate shared;no generic NPC subscription or spell delivery");
            Log.Information("{Name}: passed",name);
        }
        catch(Exception e){Log.Error(e,"{Name}: failed",name);}
    }));
}
