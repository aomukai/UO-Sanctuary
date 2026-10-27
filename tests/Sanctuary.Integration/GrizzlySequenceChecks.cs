// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class GrizzlySequenceChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GrizzlySequenceChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("grizzly-sequence-check","Grizzly shared sequence clock/rate and cached packet callbacks",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("grizzly-sequence-check: passed");}catch(Exception e){Log.Error(e,"grizzly-sequence-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static JsonDocument Probe(string name)=>JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,name+"-source-probe.json")));
    private static void Check()
    {
        Require(DruidCombat.Implemented(CombatAbility.SummonGrizzly),"Learned Grizzly route missing");
        using var clock=Probe("grizzly-sequence");int clocks=0;
        foreach(var row in clock.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();
            var next=AssassinSentrySequence.AdvanceIn(new(N(1),N(2)),DruidGrizzlySequence.Points,DruidGrizzlySequence.ActionIndex,N(0));
            Require(next.Point==N(3)&&next.Remaining==N(4)&&next.Action==N(5),"Complete Grizzly source clock mismatch");clocks++;
        }
        Require(clocks==5742,"Incomplete source clock fixture");
        using var rate=Probe("grizzly-rate");int rates=0;
        foreach(var row in rate.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();
            int speed=SourceAttackRate.Speed(N(0),N(1),N(2),N(3)!=0?N(4):0);
            Require(speed==N(5)&&speed==N(6)&&N(7)==1,"Complete Grizzly source rate mismatch");
            // This compares the shared initial planner only. Mode14below
            // uses the infinite callback, not these sparse queued requests.
            var flags=new int[144];flags[8]=1;
            var plan=SourceAnimationEvents.Initial(new(0,speed,DruidGrizzlySequence.Points,flags,[]),0);
            var expected=row[8].EnumerateArray().Select(x=>new SourceAnimationEvent(x[0].GetInt32(),x[1].GetInt32(),x[2].GetInt32(),x[3].GetInt32())).ToArray();
            Require(plan.Events.SequenceEqual(expected),"Source initial sequence planner mismatch");rates++;
        }
        Require(rates==576,"Incomplete Grizzly source rate fixture");
        using var composed=Probe("grizzly-smite-sequence");GrizzlySequenceState state=null;
        BearSmitePrepared prepared=null;int phases=0,contexts=0,draws=0,starts=0,ends=0;
        foreach(var row in composed.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();
            if(N(6)==0)
            {
                draws=starts=ends=0;int minimum=N(2)==0?30:496,maximum=N(2)==0?60:526;
                int Limited(int bound){if(bound>0){draws++;}return 0;}
                uint Raw(){draws++;return 5;}
                BearSmiteMitigation Mitigate(DireWolfPacket p)
                {int R(int value)=>NamedElements.Reduce256(value,N(5),0,0,0).Damage256;return new(R(p.Physical256),R(p.Fire256),R(p.Lightning256),R(p.Cold256),0);}
                state=DruidGrizzlySequence.Enter(N(0),N(3),29,()=>
                {starts++;return prepared=DruidBearSmite.Prepare(N(0),1,10000000,new(minimum,maximum,N(1)),Mitigate,Limited,Raw);});
                contexts++;
            }
            if(N(6)==4&&N(4)==6){state=DruidGrizzlySequence.Cancel(state);}
            // Native JSON component roundtrip: actual body reload/offline
            // cancellation remains a separate world-consumer acceptance.
            if(N(6)==4)
            {state=JsonSerializer.Deserialize<GrizzlySequenceState>(JsonSerializer.Serialize(state));DruidGrizzlySequence.Validate(state);}
            if(N(6)>0)
            {
                bool completing=state.Mode==14&&AssassinSentrySequence.Complete(state.Clock);
                state=DruidGrizzlySequence.Advance(state,(target,packet)=>
                {
                    Require(target==29&&packet==prepared,"Release rerolled/replaced the entry packet or target");
                    return N(4)!=8; // Complete do2marks64even with a missing target; missing row returns before it.
                });
                if(completing){ends++;}
            }
            Require(state.Mode==N(7)&&(state.Clock?.Point??0)==N(8)&&(state.Clock?.Remaining??0)==N(9)
                &&(state.Clock?.Action??0)==N(10)&&state.ServerDo==(N(11)!=0)&&state.Dispatches==N(12)&&ends==N(13),
                "Complete source per-frame mode14composition mismatch: "+row.GetRawText());
            Require(draws==N(15)&&starts==1&&prepared.Total256==N(16)&&prepared.Physical256==N(17),
                "Prepared packet/RNG changed during sequence");phases++;
        }
        Require(contexts==576&&phases==23616,"Incomplete source sequence/packet composition");
        var sample=DruidGrizzlySequence.Enter(1,248,29,()=>prepared);
        foreach(var invalid in new[]{sample with{Speed=0},sample with{Target=0},sample with{Clock=sample.Clock with{Point=1}},
            sample with{Packet=prepared with{StunFrames=1}},sample with{ServerDo=true}})
        {
            bool refused=false;try{DruidGrizzlySequence.Validate(invalid);}catch(InvalidDataException){refused=true;}
            Require(refused,"Malformed saved sequence accepted");
        }
        int attempts=0;
        foreach(int speed in new[]{0,4353})
        {
            bool refused=false;try{DruidGrizzlySequence.Enter(1,speed,29,()=>{attempts++;return prepared;});}catch(NotSupportedException){refused=true;}
            Require(refused&&attempts==0,"Unsupported entry prepared a packet");
        }
        Require(DruidGrizzlySequence.Enter(1,248,29,()=>null)==null,"Failed start retained a sequence");
        var canceled=DruidGrizzlySequence.Cancel(sample);int late=0;
        for(int frame=0;frame<40;frame++){canceled=DruidGrizzlySequence.Advance(canceled,(_,_)=>{late++;return true;});}
        Require(late==0&&canceled.Packet==null,"Canceled preparation replayed a late hit");
        Log.Information("Grizzly sequence:5742shared clock steps,576complete rate/planner contexts and576source/native mode14cached-packet contexts/23616phases pass; component JSON/guards/cancellation pass. Release HP/events/geometry,body save/offline and world/learned casting are not claimed");
    }
}
