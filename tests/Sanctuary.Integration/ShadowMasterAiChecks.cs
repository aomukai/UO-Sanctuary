// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class ShadowMasterAiChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowMasterAiChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("shadow-master-ai-check","Whole Master AI106 selection policy against unchanged source control traces",_=>Core.LoopContext.Post(()=>{try{Check();Log.Information("shadow-master-ai-check: passed");}catch(Exception e){Log.Error(e,"shadow-master-ai-check: failed");}}));
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static int[] Read(JsonElement r)=>r.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private sealed class Probe:ShadowMasterServices
    {
        internal readonly int[] C,S,G,Rolls,Outcomes;internal readonly int[][] Owned;
        internal readonly Dictionary<int,int[]> Units,Rows;internal readonly List<int[]> Trace=new();internal int RngAt,ActionAt,LeftId;
        internal Probe(JsonElement input)
        {
            C=Read(input.GetProperty("context"));S=Read(input.GetProperty("scan"));G=Read(input.GetProperty("groups"));Rolls=Read(input.GetProperty("rolls"));Outcomes=Read(input.GetProperty("outcomes"));
            Units=input.GetProperty("units").EnumerateArray().Select(Read).ToDictionary(r=>r[0]);Owned=input.GetProperty("skills").EnumerateArray().Select(Read).ToArray();Rows=Owned.ToDictionary(r=>r[0]);LeftId=C[19];
        }
        private void Add(params int[] x)=>Trace.Add(x);private int Id(ShadowMasterUnit u)=>u?.Id??0;
        private int Value(ShadowMasterUnit u,int field)=>u==null?0:Units[u.Id][field];private int Roll()=>Rolls[RngAt++%Rolls.Length];
        internal override ShadowMasterUnit Unit(int id)=>id==0?null:new(id,Units[id][1]);
        internal override ShadowMasterUnit Self=>Unit(1);
        internal override bool HasSkills=>C[10]!=0;
        internal override ShadowMasterUnit Owner(ShadowMasterUnit u){Add(2,Id(u));return Unit(Value(u,4));}
        internal override void DeleteThink()=>Add(3);
        internal override int Squared(ShadowMasterUnit a,ShadowMasterUnit b){int d=Value(b,6);Add(4,Id(a),Id(b),d);return d;}
        internal override bool CommonMove(ShadowMasterUnit t,ShadowMasterUnit o,bool combat,int flags,int leash){Add(5,Id(t),Id(o),combat?1:0,flags,leash,C[18]);return C[18]!=0;}
        internal override ShadowMasterUnit Target(ShadowMasterUnit u){int target=Value(u,5);Add(6,Id(u),target);return Unit(target);}
        internal override bool TrySkill(int id,bool combat,ShadowMasterUnit t,int x,int y){int result=t!=null&&(t.Id==1||t.Id==Units[1][4])?0:Outcomes[ActionAt++%Outcomes.Length];Add(7,id,combat?1:0,Id(t),x,y,result);return result!=0;}
        internal override IEnumerable<int> Skills(){Add(8);foreach(var row in Owned){yield return row[0];}}
        internal override ShadowMasterSkill Row(int id)
        {Add(31,id);return !Rows.TryGetValue(id,out var r)||r[1]==0?null:new(r[0],r[2],r[3],r[4],r[5],r[6],r[7],r[8]!=0,r[9],r[10],r[11]);}
        internal override bool State(ShadowMasterUnit u,int state){bool active=u!=null&&state>=0&&state<31&&(Value(u,11)&(1<<state))!=0;Add(30,Id(u),state,active?1:0);return active;}
        internal override int? Group(int state){int value=state>=0&&state<G.Length?G[state]:-1;Add(9,state,value);return value<0?null:value;}
        internal override int StateCount=>C[21];
        internal override int Percentage(){int value=Roll()%100;Add(10,value);return value;}
        internal override int? Left(){Add(11,LeftId);return LeftId<0?null:LeftId;}
        internal override void SetLeft(int id){LeftId=id;Add(12,id);}
        internal override bool Dead(ShadowMasterUnit t){int value=Value(t,2);Add(29,Id(t),value);return value!=0;}
        internal override bool Enemy(ShadowMasterUnit t){int value=Value(t,3);Add(28,Id(t),value);return value!=0;}
        internal override void Idle(int n)=>Add(1,n);
        internal override ShadowMasterScan Scan(){Add(16);return new(S[0],S[1],S[2],S[3],S[4],S[5],S[6],S[7],S[8]);}
        internal override bool Special(ShadowMasterUnit t){int value=Value(t,7);Add(17,Id(t),value);return value!=0;}
        internal override int LifePercent(){Add(18,C[14]);return C[14];}
        internal override bool Progressive(){Add(13,C[15]);return C[15]!=0;}
        internal override bool ClearMissileLine(ShadowMasterUnit t){Add(15,Id(t));return C[16]!=0;}
        internal override uint Raw(){int value=Roll();Add(14,value);return(uint)value;}
        internal override void Run(ShadowMasterUnit t)=>Add(26,Id(t));
        internal override bool Retreat(ShadowMasterUnit t,int flags,int mode){Add(27,Id(t),flags,mode,C[17]);return C[17]!=0;}
        internal override int Resistance(ShadowMasterUnit t,int stat){int value=Value(t,12+stat);Add(19,Id(t),stat,value);return value;}
        internal override int Rank(int id){int value=Rows[id][12];Add(20,id,value);return value;}
        internal override int Limited(int maximum){int value=Roll()%Math.Max(maximum,1);Add(21,maximum,value);return value;}
        internal override int? Charges(int id){Add(22,id);var r=Rows[id];if(r[13]==0){return null;}Add(23,id,r[14]);return r[14];}
        internal override int? MissileRange(int id){int range=Owned.FirstOrDefault(r=>r[10]==id)?[15]??-1;Add(24,id,range);return range<0?null:range;}
        internal override int? Drain(ShadowMasterUnit t)=>t!=null&&Value(t,8)!=0&&Value(t,9)!=0?Value(t,10):null;
        internal override bool Melee(ShadowMasterUnit t){int value=Value(t,18);Add(25,Id(t),value);return value!=0;}
    }
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-master-ai-cases.json")));int compared=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=new Probe(row.GetProperty("input"));var c=p.C;var expected=row.GetProperty("expected");
            var context=new ShadowMasterContext(c[0],c[1],c[2],c[3],c[4],c[5],c[6],c[7]!=0,c[8],p.Unit(c[9]));
            var actual=ShadowMasterAi.Think(context,new(c[11],c[12],c[13]),p);var events=expected.GetProperty("trace").EnumerateArray().Select(Read).ToArray();
            int diff=Enumerable.Range(0,Math.Max(events.Length,p.Trace.Count)).FirstOrDefault(i=>i>=events.Length||i>=p.Trace.Count||!events[i].SequenceEqual(p.Trace[i]),-1);
            if(Json(new[]{actual.Repeat,actual.Skill,actual.Rank})!=expected.GetProperty("memory").GetRawText()||p.LeftId!=expected.GetProperty("left").GetInt32()
                ||p.RngAt!=expected.GetProperty("rng").GetInt32()||p.ActionAt!=expected.GetProperty("actions").GetInt32()||diff>=0)
            {throw new InvalidOperationException("Master AI source mismatch context="+compared+" event="+diff+" expected="+(diff>=0&&diff<events.Length?Json(events[diff]):"end")+" actual="+(diff>=0&&diff<p.Trace.Count?Json(p.Trace[diff]):"end")+" memory="+Json(actual));}
            compared++;
        }
        if(compared!=497){throw new InvalidDataException("Incomplete whole Master AI source contexts: "+compared);}
        Log.Information("Whole Master AI106:497complete source contexts,all AI types/thresholds,RNG/query/action order,owner/leash/buff/target/retreat decisions,ordered positive candidates/reverse attempts,passive left selection and repeat memory;world bridge/automatic activation remain unqualified");
    }
}
