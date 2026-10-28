// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class PlayerInfernoSequenceChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PlayerInfernoSequenceChecks));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Prepared player st11/do19 mode/event composition",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("player-inferno-sequence-check");Register("player-inferno-sequence-reload-check");}
    private static void Check()
    {
        using var proof=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"player-inferno-sequence-source-probe.json")));
        int contexts=0,frames=0,currentContexts=0,currentFrames=0,jsonContinuations=0;
        foreach(var row in proof.RootElement.EnumerateArray())foreach(var model in Enum.GetValues<PlayerInfernoCostModel>())
        {
            int N(int i)=>row[i].GetInt32();
            // With1536initial mana,the corrected rank1cost can change later
            // eligibility after stop or exhaustion. Keep that divergence out
            // of legacy frame equivalence instead of assuming a zero debit.
            if(model==PlayerInfernoCostModel.CurrentTablePerInvocation&&N(1)==1&&N(2)==1536){continue;}
            int mana=N(2),emits=0,made=0,paid=0;var ability=N(0)==0?CombatAbility.Inferno:CombatAbility.ArcticBlast;
            var services=new PlayerInfernoSequenceServices(()=>mana,cost=>{if(mana<cost){return false;}mana-=cost;paid++;return true;},
                ()=>true,()=>N(5)!=0,_=>{emits++;if(N(6)!=0){made++;}return N(6)!=0;});
            var state=PlayerInfernoSequence.EnterFresh(1000,ability,N(1),model,services,192).State;
            for(int frame=1000;frame<=1040;frame++)
            {
                if(frame==N(4)){state=PlayerInfernoSequence.Stop(state,frame);}
                bool callback=N(7)==1?frame==1006:N(7)==2&&frame>=1006&&(frame-1006)%2==0;
                if(callback&&state.UsedSkill){state=PlayerInfernoSequence.StartCallback(state,frame,N(1),services).State;}
                if(frame==N(8)&&state.UsedSkill){state=PlayerInfernoSequence.Cancel(state,frame);}
                state=PlayerInfernoSequence.Advance(state,frame,N(1),services);var expected=row[9][frame-1000];int E(int i)=>expected[i].GetInt32();
                string context=$"case={contexts}/{currentContexts} model={model} frame={frame} kind={N(0)} rank={N(1)} mana={N(2)} stop={N(4)} schedule={N(7)} cancel={N(8)}";
                Require(state.Mode==E(0)&&state.UsedSkill==(E(1)!=0)&&state.Channel.Active==(E(2)!=0)&&state.Channel.Lease==(E(3)!=0)
                    &&(!state.Channel.Lease||state.Channel.Expires==E(4))&&state.Channel.Param1==E(5)&&state.Channel.Flags==expected[6].GetUInt32(),"Source sequence mode/state "+context);
                int adjustment=model==PlayerInfernoCostModel.CurrentTablePerInvocation&&N(1)==1?E(10)*96:0;
                Require(mana==E(7)-adjustment&&emits==E(8)&&made==E(9)&&paid==E(10)&&state.Animation.Epoch256==E(11),"Source sequence resources/rewind "+context);
                var events=expected[12];Require(state.Animation.Events.Length==events.GetArrayLength(),"Source event count "+context);
                for(int i=0;i<state.Animation.Events.Length;i++)
                {var e=events[i];Require(state.Animation.Events[i]==new SourceAnimationEvent(e[0].GetInt32(),e[1].GetInt32(),e[2].GetInt32(),e[3].GetInt32()),"Source FIFO event "+context+" index="+i);}
                if(frame is 1012 or 1020)
                {state=JsonSerializer.Deserialize<PlayerInfernoSequenceState>(JsonSerializer.Serialize(state));jsonContinuations++;}
                if(model==PlayerInfernoCostModel.LegacySource){frames++;}else{currentFrames++;}
            }
            if(model==PlayerInfernoCostModel.LegacySource){contexts++;}else{currentContexts++;}
        }
        Require(contexts==4608&&frames==188928&&currentContexts==4224&&currentFrames==173184&&jsonContinuations==17664,"Incomplete sequence composition inventory");
        int ledger=1536,debits=0,attempts=0;
        var controls=new PlayerInfernoSequenceServices(()=>ledger,cost=>{if(ledger<cost){return false;}ledger-=cost;debits++;return true;},
            ()=>true,()=>true,_=>{attempts++;return true;});
        var finite=PlayerInfernoSequence.EnterFresh(1000,CombatAbility.ArcticBlast,1,PlayerInfernoCostModel.CurrentTablePerInvocation,controls).State;
        int touched=0;var forbidden=new PlayerInfernoSequenceServices(()=>{touched++;return 4096;},_=>{touched++;return true;},
            ()=>{touched++;return true;},()=>{touched++;return true;},_=>{touched++;return true;});
        for(int frame=1001;frame<=1002;frame++)
        {
            bool refused=false;try{PlayerInfernoSequence.StartCallback(finite,frame,1,forbidden);}catch(NotSupportedException){refused=true;}
            Require(refused&&touched==0,"Unsupported early reentry touched allocation,aim,missile or resource services");
        }
        for(int frame=1000;frame<=1040;frame++)
        {
            if(frame>=1006&&(frame-1006)%2==0&&finite.UsedSkill){finite=PlayerInfernoSequence.StartCallback(finite,frame,1,controls).State;}
            finite=PlayerInfernoSequence.Advance(finite,frame,1,controls);
        }
        Require(ledger==0&&debits==16&&attempts==14&&!finite.UsedSkill&&!finite.Channel.Lease,
            "Finite rank1current-cost control retained the old free channel or failed independent expiry");
        Require(!DruidCombat.Implemented(CombatAbility.ArcticBlast)&&!SorceressCombat.Implemented(CombatAbility.Inferno),"Prepared sequence exposed learned channel");
        Log.Information("Player Inferno/Arctic sequence:4608complete-source contexts/188928frames plus4224explicit current-cost controls/173184frames,17664JSON continuations pass; finite rank1control16debits/14attempts and early-reentry no-side-effect guards pass; modern input/rate and cold flight remain open");
    }
}
