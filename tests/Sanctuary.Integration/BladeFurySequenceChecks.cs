// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class BladeFurySequenceChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeFurySequenceChecks));
    public static void Configure()
    {
        Register("blade-fury-sequence-check",Check);
        Register("blade-fury-sequence-reload-check",Check);
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,
        "Fury fresh mode/action/end/expiry composition; accepted client input remains separate",_=>Core.LoopContext.Post(()=>
        {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blade-fury-sequence-source-probe.json")));
        int contexts=0,frames=0,ties=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();int mana=N(1),emits=0,made=0,paid=0;
            var services=new BladeFurySequenceServices(()=>mana,cost=>
                {if(mana<cost){return false;}mana-=cost;paid++;return true;},()=>true,()=>N(4)!=0,
                ()=>{emits++;if(N(5)!=0){made++;}return N(5)!=0;});
            var entered=BladeFurySequence.Enter(1000,N(0),services,192);var state=entered.State;
            for(int frame=1000;frame<=1032;frame++)
            {
                if(frame==N(3)){state=BladeFurySequence.Stop(state,frame);}
                if(frame==N(6)&&state.UsedSkill&&state.Channel.HasLease)
                {state=BladeFurySequence.StartCallback(state,frame,N(0),services).State;}
                state=BladeFurySequence.Advance(state,frame,N(0),services);
                var expected=row[7][frame-1000];int E(int i)=>expected[i].GetInt32();
                string context=$"case={contexts} frame={frame} rank={N(0)} mana={N(1)} stop={N(3)} callback={N(6)}";
                Require(state.Mode==E(0)&&state.UsedSkill==(E(1)!=0)&&state.Channel.Active==(E(2)!=0)
                    &&state.Channel.HasLease==(E(3)!=0)&&(!state.Channel.HasLease||state.Channel.Expires==E(4))
                    &&state.Channel.NextShot==E(5)&&state.Channel.Flags==expected[6].GetUInt32(),"Source sequence mode/state "+context);
                Require(mana==E(7)-(N(0)==1?E(10)*256:0)&&emits==E(8)&&made==E(9)&&paid==E(10)
                    &&state.Animation.Epoch256==E(11),"Source sequence resources/rewind "+context);
                var events=expected[12];
                Require(state.Animation.Events.Length==events.GetArrayLength(),"Source event count "+context);
                for(int i=0;i<state.Animation.Events.Length;i++)
                {var e=events[i];Require(state.Animation.Events[i]==new SourceAnimationEvent(e[0].GetInt32(),e[1].GetInt32(),e[2].GetInt32(),e[3].GetInt32()),"Source FIFO event "+context+" index="+i);}
                if(N(6)==1006&&N(3)==-1&&frame==1013&&N(1)>=1536&&N(4)!=0)
                {Require(emits==1&&!state.Channel.HasLease&&(state.Channel.Flags&64u)!=0,"Expiry/action tie added a blade after the earlier accepted callback shot");ties++;}
                var saved=JsonSerializer.Deserialize<BladeFurySequenceState>(JsonSerializer.Serialize(state));
                Require(JsonSerializer.Serialize(saved)==JsonSerializer.Serialize(state),"Sequence component JSON "+context);frames++;
            }
            contexts++;
        }
        Require(contexts==2880&&frames==95040&&ties>0,"Complete mode/sequence inventory");
        Require(!AssassinCombat.Implemented(CombatAbility.BladeFury),"Prepared sequence exposed learned input");
        Log.Information("Blade Fury sequence:2880 complete XY/ID fresh mode/action/end/resource compositions and95040 FIFO event frames match; flag64 entry clear, pending stop action, callback renewal/expiry tie, rank1 correction and component round-trip pass; current input remains separate");
    }
}
