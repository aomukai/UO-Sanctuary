// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class BearSmiteChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BearSmiteChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("bear-smite-check","NPC326 prepared packets and source large-value mitigation; release/world remain separate",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("bear-smite-check: passed");}catch(Exception e){Log.Error(e,"bear-smite-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        Require(!DruidCombat.Implemented(CombatAbility.SummonGrizzly),"Packet preparation exposed incomplete Grizzly casting");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"bear-smite-source-probe.json")));
        int packets=0,reductions=0,large=0;
        foreach(var row in doc.RootElement.GetProperty("packets").EnumerateArray())
        {
            int N(int i)=>row[0][i].GetInt32();int O(int i)=>row[1][i].GetInt32();
            if(N(8) is 6 or 7 or 8){continue;} // Row/target/town start services are world boundaries.
            var calls=new List<int>();
            int Limited(int bound){calls.Add(bound);return N(5)!=0&&bound>0?bound-1:0;}
            uint Raw(){calls.Add(-1);return (uint)N(6);}
            BearSmiteMitigation Mitigate(DireWolfPacket p)
            {
                int R(int value)=>NamedElements.Reduce256(value,N(7),0,0,0).Damage256;
                return new(R(p.Physical256),R(p.Fire256),R(p.Lightning256),R(p.Cold256),ColdStates.LengthFrames(p.ColdFrames,N(7),false,false));
            }
            var weapon=new BearSmiteWeapon(N(1),N(2),N(3),N(4)!=0?512:0,N(4)!=0?1536:0,
                N(4)!=0?768:0,N(4)!=0?1792:0,N(4)!=0?1280:0,N(4)!=0?2048:0,N(4)!=0?31:0);
            var actual=DruidBearSmite.Prepare(N(0),N(9),10000000,weapon,Mitigate,Limited,Raw);
            Require(actual.ResultFlags==O(9)&&actual.Physical256==O(3)&&actual.Fire256==O(4)&&actual.Lightning256==O(5)
                &&actual.Cold256==O(6)&&actual.Total256==O(7)&&actual.StunFrames==O(8)&&actual.HitClass==O(10)
                &&actual.ColdFrames==O(20)&&actual.AbsorbedLife256==0,"Complete source prepared BearSmite packet: "+row[0].GetRawText());
            Require(calls.SequenceEqual(row[2].EnumerateArray().Select(x=>x.GetInt32()))
                &&calls.Count(x=>x!=0)==O(12)&&O(12)==O(13),"Start RNG ordering/zero-width roll/release snapshot changed");
            packets++;
        }
        Require(packets==1606,"Incomplete source prepared-packet comparison");
        foreach(var row in doc.RootElement.GetProperty("reduction").EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();
            var actual=NamedElements.Reduce256(N(0),N(1),checked(N(2)*256),N(3),checked(N(4)*256));
            Require(actual.Damage256==N(5)&&actual.Absorbed256==N(6),"Complete source resistance/absorption ordering: "+row.GetRawText());
            reductions++;if(N(0)>1048576){large++;}
        }
        Require(reductions==1248&&large>0,"Incomplete mitigation threshold coverage");
        Require(NamedElements.Reduce256(1048577,0,0,0,0).Damage256==1048577
            &&NamedElements.Reduce256(1048577,33,0,0,0).Damage256==702495
            &&NamedElements.Reduce256(1048577,0,0,37,0) is{Damage256:660632,Absorbed256:387945},
            "Zero-resistance bypass or source large-value early division changed");
        // Retain established native zero floor and wide ceiling outside the
        // supplementary legacy negative-MDR/signed-overflow acceptance domain.
        Require(NamedElements.Reduce256(1,33,256,37,256) is{Damage256:0,Absorbed256:0}
            &&NamedElements.Reduce256(int.MaxValue,-100,0,0,0).Damage256==int.MaxValue,"Existing native damage clamp changed");
        for(int rank=1;rank<=255;rank++)
        {
            var npc=DruidNpcSkills.BearSmite(rank);
            Require(npc.Rank==rank&&npc.Eval("calc1")==15*rank&&npc.Eval("calc2")==Math.Max(250,15+5*(rank-1))
                &&npc.Mana256==0,"NPC326rank/formulas distinct from player247");
        }
        Require(DruidNpcSkills.Rage().Eval("auralencalc")==500&&DruidNpcSkills.Rage().Eval("aurastatcalc1")==100,"New NPC row changed Fenris314");
        foreach(int rank in new[]{0,256})
        {
            bool refused=false;try{DruidBearSmite.Prepare(rank,1,10000000,new(30,60,25),_=>default,_=>0,()=>5);}catch(ArgumentOutOfRangeException){refused=true;}
            Require(refused,"Unsupported NPC rank accepted");
        }
        Log.Information("BearSmite prepared:1606complete source/native packets,1248complete resistance/absorb cases,255NPC ranks and unchanged Fenris314pass; deferred health,total versus post-allocation physical,zero stun,crit/hit-class and RNG order are distinct. Sequence/release/world are not claimed");
    }
}
