// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GoatmanNpcMeleeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GoatmanNpcMeleeChecks));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Register(string n)=>ConsoleInputHandler.RegisterCommand(n,"Goatman NPC Stun/Berserk preparation",_=>Core.LoopContext.Post(()=>{try{Check();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    public static void Configure(){Register("goatman-npc-melee-check");Register("goatman-npc-melee-reload-check");}
    private static int[] Ints(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"goatman-npc-melee-source-probe.json")));int packets=0,states=0,remainder=0;
        Require(NecromancerSummons.Source("wargoatman","Crit")==5,"Owned Goatman intrinsic critical source row missing");
        foreach(var row in doc.RootElement.GetProperty("packets").EnumerateArray())
        {
            var p=Ints(row[0]);var expected=Ints(row[1]);var bounds=new List<int>();int draws=0;
            int Limited(int n){bounds.Add(n);if(n<=0){return 0;}draws++;return p[6]!=0?n-1:0;}
            uint Raw(){bounds.Add(-1);draws++;return (uint)p[7];}
            int Reduce(int n)=>NamedElements.Reduce256(n,p[8],0,0,0).Damage256;
            var weapon=new GoatmanNpcWeapon(p[2],p[3],p[4],p[5]!=0?512:0,p[5]!=0?1536:0,p[5]!=0?768:0,p[5]!=0?1792:0,p[5]!=0?1280:0,p[5]!=0?2048:0,p[5]!=0?31:0);
            var packet=GoatmanNpcMelee.Prepare(p[0],p[1],p[9],10000000,weapon,d=>new(Reduce(d.Physical256),Reduce(d.Magic256),Reduce(d.Fire256),Reduce(d.Lightning256),Reduce(d.Cold256),d.ColdFrames*(100-p[8])/100,d.StunFrames),Limited,Raw);
            int[] actual=[1,packet.Physical256,packet.Magic256,packet.Fire256,packet.Lightning256,packet.Cold256,packet.ColdFrames,packet.StunFrames,packet.Total256,packet.ResultFlags,packet.HitClass,packet.CriticalOverlay==54?1:0,draws];
            Require(actual.SequenceEqual(expected),"NPC packet differs from complete shared source at"+packets+":"+string.Join(',',actual));Require(bounds.SequenceEqual(Ints(row[3])),"NPC RNG request order/ranges differ");
            if(p[0]==418&&packet.Physical256>0&&packet.Magic256>0){remainder++;}packets++;
        }
        foreach(var row in doc.RootElement.GetProperty("states").EnumerateArray())
        {
            var p=Ints(row[0]);var before=p[1]==0?null:new GoatmanBerserkState(p[1],1234,7,0,-100);var start=GoatmanNpcMelee.StartBerserk(1000,p[0],before,p[2]!=0);
            int alloc=p[1]==0?1:0,toggle=p[1]==0&&p[2]!=0?1:0,refresh=start!=null?1:0,events=start!=null?1:0;
            int[] Snapshot(GoatmanBerserkState s)=>[s!=null?1:0,s?.Flags??0,s?.UntilFrame??0,s?.Rank??0,alloc,toggle,refresh,events];
            Require(row[1].GetInt32()==1&&Snapshot(start).SequenceEqual(Ints(row[2])),"NPC Berserk timed start state differs");
            var prior=p[4]!=0?null:start;if(prior==null){alloc++;}var impact=GoatmanNpcMelee.ImpactBerserk(p[5],prior,p[2]!=0);
            if(impact!=null){toggle++;refresh++;Require(impact is{ArmorOverride:-100,PhysicalResist:0},"NPC Berserk uses wrong state stats");}
            Require(Snapshot(impact).SequenceEqual(Ints(row[4])),"NPC Berserk release refresh/temporary fallback differs");
            Require(row[3].GetInt32()==(impact==null?1:0)&&row[5].GetInt32()==(impact==null?0:64),"Source target-absent do2completion boundary changed");states++;
        }
        Require(packets==24000&&states==216&&remainder>0,"NPC source controls or early-division conversion remainder missing");
        for(int rank=1;rank<=255;rank++)
        {
            var stun=GoatmanNpcSkills.Values(416,rank);var berserk=GoatmanNpcSkills.Values(418,rank);
            Require(stun.Eval("calc1")==0&&stun.Eval("ToHitCalc")==10+5*rank&&stun.DurationFrames==30+Math.Min(rank-1,15)*5+Math.Max(rank-16,0)*2,"NPC Stun rows inherited player damage/duration synergies");
            var own=GoatmanNpcSkills.Values(416,rank,npcLearned:key=>key=="Concentrate"?3:0);Require(own.Eval("ToHitCalc")==25+5*rank,"NPC explicit own skill context");
            Require(berserk.Eval("calc1")==104+rank&&berserk.Eval("calc4")==100&&berserk.Eval("calc2")==75-Math.Min(110*rank/(rank+6)*50/100,50),"NPC Berserk uses player ED/synergy or wrong duration");
            Require(GoatmanNpcSkills.Values(417,rank).Eval("calc1")==0&&GoatmanNpcSkills.Values(419,rank).Eval("calc5")==11,"Raw Frenzy/Cleave source suppliers missing");
        }
        try{GoatmanNpcMelee.Prepare(417,1,1,100,null,null,null,null);throw new InvalidOperationException("Unverified new Frenzy start was enabled");}catch(NotSupportedException){}
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"NPC preparation enabled unfinished Goatman actor");
        Log.Information("Goatman NPC melee:24000complete-source packets/216Berserk state phases;shared rolls/conversion/critical reused,current callbacks/release/world actor remain gated");
    }
}
