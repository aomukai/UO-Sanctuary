// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class NecromancerPetAiChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NecromancerPetAiChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("necromancer-pet-ai-check",
        "Complete prepared NecroPet AI67 ordered source comparison; live actor remains separate",_=>Core.LoopContext.Post(()=>
        {try{Check();Log.Information("necromancer-pet-ai-check: passed");}catch(Exception e){Log.Error(e,"necromancer-pet-ai-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"necromancer-pet-ai-source-probe.json")));
        int compared=0;var kinds=new HashSet<NecromancerPetAiKind>();
        NecromancerPetAiContext last=null;NecromancerPetAiServices lastServices=null;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            int N(int i)=>row[0][i].GetInt32();
            var c=new NecromancerPetAiContext(new(N(11),88,N(1)),N(18),N(19),8,123);
            var actual=new List<NecromancerPetAiRequest>();int rolls=0;
            void A(NecromancerPetAiKind kind,int a=0,int b=0,int d=0,int e=0)=>actual.Add(new(kind,a,b,d,e));
            var s=new NecromancerPetAiServices(actual.Add,
                ()=>{A(NecromancerPetAiKind.Owner);return N(0)!=0?17:0;},
                remembered=>{A(NecromancerPetAiKind.LookupPlayer,remembered);return N(2)!=0?17:0;},
                target=>{A(NecromancerPetAiKind.Distance,target);return target==17?N(3):N(7);},
                owner=>{A(NecromancerPetAiKind.OwnerTarget,owner);return new(N(6)!=0?20:0,999,true);},
                (ranged,radius)=>{A(NecromancerPetAiKind.Primary,ranged?1:0,radius);return new(N(4)!=0?10:0,N(5),N(12)!=0);},
                _=>N(8)!=0,
                target=>{A(NecromancerPetAiKind.MissileBarrier,target);return N(9)!=0;},
                target=>{A(NecromancerPetAiKind.ValidTarget,target);return N(10)!=0;},
                ()=>{int roll=N(15+rolls++);A(NecromancerPetAiKind.Percentage,roll);return roll;},
                (target,combat,flag,leash)=>{A(NecromancerPetAiKind.CommonMove,target,combat?1:0,flag,leash);return N(14)!=0;},
                ()=>{A(NecromancerPetAiKind.Town);return N(13)!=0;},
                (mode,run,velocity,leash)=>{A(NecromancerPetAiKind.PetMove,mode,run?1:0,velocity,leash);return false;});
            var state=NecromancerPetAi.Think(c,s);
            var expected=row[3].EnumerateArray().Select(x=>new NecromancerPetAiRequest((NecromancerPetAiKind)x[0].GetInt32(),
                x[1].GetInt32(),x[2].GetInt32(),x[3].GetInt32(),x[4].GetInt32())).ToArray();
            Require(state==new NecromancerPetAiState(row[1][0].GetInt32(),row[1][1].GetInt32(),row[1][2].GetInt32())
                &&rolls==row[2].GetInt32()&&actual.SequenceEqual(expected),
                "NecroPet source service order/state/random draws at context "+row[0].GetRawText()+";actual "+JsonSerializer.Serialize(actual));
            foreach(var request in actual){kinds.Add(request.Kind);}
            last=c;lastServices=s;compared++;
        }
        Require(compared==1598&&kinds.Count==19,"Incomplete NecroPet source branch/service coverage");
        // Refuse unsupported services rather than map malformed values onto
        // a different target, chance or velocity. Learned cast stays disabled.
        void Refuses(Action action)
        {
            bool rejected=false;try{action();}catch(Exception e)when(e is ArgumentException or NotSupportedException or OverflowException){rejected=true;}
            Require(rejected,"Malformed NecroPet query/context was accepted");
        }
        var controlled=last with{State=new(0,88,0),Velocity=9,Run=9};
        var services=lastServices with{Owner=()=>17,Distance=_=>0,OwnerTarget=_=>new(0,0,false),
            Primary=(_,_)=>new(10,0,true),ValidTarget=_=>true,CommonMove=(_,_,_,_)=>false,Town=()=>false};
        foreach(int roll in new[]{-1,100}){Refuses(()=>NecromancerPetAi.Think(controlled,services with{Percentage=()=>roll}));}
        Refuses(()=>NecromancerPetAi.Think(controlled,services with{Owner=()=>-1}));
        Refuses(()=>NecromancerPetAi.Think(controlled,services with{Distance=_=>-1}));
        Refuses(()=>NecromancerPetAi.Think(controlled,services with{Primary=(_,_)=>new(-1,0,false)}));
        Refuses(()=>NecromancerPetAi.Think(controlled,services with{Primary=(_,_)=>new(10,-1,false)}));
        Refuses(()=>NecromancerPetAi.Think(controlled,services with{Owner=null}));
        Refuses(()=>NecromancerPetAi.Think(controlled,services with{Request=null}));

        Log.Information("Necromancer pet AI67:1598 complete source ordered-service/state/draw comparisons and8 malformed-service guards pass; both parameter branches,owner recovery,target/visibility/validity gates,melee/ranged/follow/town/idle requests covered. Moving body,attack delivery,save and learned casting remain separate.");
    }
}
