// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class PlayerInfernoChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PlayerInfernoChecks));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("player-inferno-check","Prepared shared player st11/do19 channel",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("player-inferno-check: passed");}catch(Exception e){Log.Error(e,"player-inferno-check: failed");}}));
    private static void Check()
    {
        Require(!DruidCombat.Implemented(CombatAbility.ArcticBlast)&&!SorceressCombat.Implemented(CombatAbility.Inferno),"Prepared channel enabled unresolved learned route");
        using var proof=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"player-inferno-channel-source-probe.json")));int compared=0,current=0;
        foreach(var row in proof.RootElement.EnumerateArray())foreach(var model in Enum.GetValues<PlayerInfernoCostModel>())
        {
            int C(int i)=>row[0][i].GetInt32();
            var ability=C(0)==0?CombatAbility.Inferno:CombatAbility.ArcticBlast;int rank=C(1),mana=C(2),bits=C(3),action=C(8);
            var s=new PlayerInfernoState((bits&1)!=0,(bits&2)!=0,(bits&2)!=0?1010:0,C(4));int emitted=0,made=0,paid=0,rewinds=0,allocated=0,range=0;
            var services=new PlayerInfernoServices(()=>{allocated++;return C(5)!=0;},()=>C(6)!=0,
                distance=>{emitted++;range=distance;if(C(7)!=0){made++;}return C(7)!=0;},()=>rewinds++);
            var resources=new PlayerInfernoResourceServices(()=>mana,cost=>{if(mana<cost){return false;}mana-=cost;paid++;return true;},services);
            int start=-1,result=-1;PlayerInfernoStep step;
            int duration=Math.Max(1,checked((int)PlayerInfernoResources.Values(ability,rank).Eval("calc1")));
            if(action==0){step=PlayerInfernoChannel.Start(s,1000,true,true,true,mana,1536,duration,services);s=step.State;start=step.Success?1:0;}
            if(action==1){step=PlayerInfernoChannel.Do(s,1000,true,true,true,duration,services);s=step.State;result=step.Success?1:0;}
            if(action is 2 or 4){step=PlayerInfernoResources.Start(s,1000,ability,rank,model,resources);s=step.State;start=step.Success?1:0;}
            if(action==3||action==4&&start==1){step=PlayerInfernoResources.Do(s,1000,ability,rank,resources);s=step.State;result=step.Success?1:0;}
            var actual=new[]{start,result,s.Active?1:0,s.Lease?1:0,s.Expires,s.Param1,(int)s.Flags,mana,emitted,made,paid,rewinds,allocated,range};
            var expected=row[1].EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            if(model==PlayerInfernoCostModel.CurrentTablePerInvocation&&rank==1){expected[7]-=expected[10]*96;}
            Require(actual.SequenceEqual(expected),"Complete st11/do19/resource comparison:"+row[0].GetRawText()+" "+model+" actual="+JsonSerializer.Serialize(actual)+" expected="+JsonSerializer.Serialize(expected));
            if(model==PlayerInfernoCostModel.LegacySource){compared++;}else{current++;}
        }
        Require(compared==19200&&current==19200,"Incomplete player channel comparisons");
        foreach(var ability in new[]{CombatAbility.Inferno,CombatAbility.ArcticBlast})for(int rank=1;rank<=255;rank++)
        {Require(PlayerInfernoResources.Cost256(ability,rank,PlayerInfernoCostModel.CurrentTablePerInvocation)==(23+rank)*4,"Pinned fractional per-invocation cost");}
        var owner=RecyclerVineChecks.Fresh();RecyclerVineChecks.Train(owner,"druid.arcticblast");
        try
        {
            int rewinds=0,emits=0;var services=new PlayerInfernoServices(()=>true,()=>true,_=>{emits++;return false;},()=>rewinds++);
            var resources=PlayerInfernoResources.Player(owner,services);NecromancerEffects.SetMana256(owner,1535);
            var refused=PlayerInfernoResources.Start(new(),1000,CombatAbility.ArcticBlast,1,PlayerInfernoCostModel.CurrentTablePerInvocation,resources);
            Require(!refused.Success&&NecromancerEffects.ManaBalance256(owner)==1535,"Below-threshold actual player changed mana");
            NecromancerEffects.SetMana256(owner,1536);var start=PlayerInfernoResources.Start(new(),1000,CombatAbility.ArcticBlast,1,PlayerInfernoCostModel.CurrentTablePerInvocation,resources);
            Require(start.Success&&start.State is{Active:true,Lease:true,Expires:1020,Param1:0}&&NecromancerEffects.ManaBalance256(owner)==1440,"Actual rank1paid six upfront or adopted legacy zero debit");
            var first=PlayerInfernoResources.Do(start.State,1010,CombatAbility.ArcticBlast,1,resources);
            Require(first.Success&&first.State.Param1==1&&emits==0&&rewinds==1&&NecromancerEffects.ManaBalance256(owner)==1440,"First do suppression or payment-on-start changed");
            var released=PlayerInfernoChannel.Release(first.State);var tail=PlayerInfernoResources.Do(released,1011,CombatAbility.ArcticBlast,1,resources);
            Require(tail.Success&&tail.State is{Active:false,Lease:true,Expires:1020}&&emits==1&&rewinds==2,"Inferno player tail incorrectly copied Blade Fury bit-gated rewind");
            var renewed=PlayerInfernoResources.Start(first.State,1012,CombatAbility.ArcticBlast,1,PlayerInfernoCostModel.CurrentTablePerInvocation,resources);
            Require(renewed.Success&&renewed.State.Expires==1018&&NecromancerEffects.ManaBalance256(owner)==1344,"Active-state bypass/renewal/actual fractional debit");
            var expired=PlayerInfernoChannel.Expire(renewed.State);
            Require(expired is{Active:false,Lease:false,Expires:0}&&(expired.Flags&64)!=0&&JsonSerializer.Deserialize<PlayerInfernoState>(JsonSerializer.Serialize(expired))==expired,"Expiry/JSON state lost bit/list distinction");
        }
        finally{owner.Delete();}
        Log.Information("Player Inferno/Arctic channel:19200complete-source legacy comparisons and19200explicit current-cost controls,510rank costs and actual player96FPdebit pass; first-do suppression,20/6frame lease and unconditional player rewind differ from Blade Fury; scheduling/current input and cold flight remain open");
    }
}
