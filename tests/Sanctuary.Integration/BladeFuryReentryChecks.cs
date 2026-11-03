// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BladeFuryReentryChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeFuryReentryChecks));
    private sealed record Saved(uint Owner,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blade-fury-reentry-fixtures.json");
    private static bool _creating;
    private static void Rates()
    {var old=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>_creating||p.Name=="BladeFuryReentryFixture"?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;}
    public static void Configure()
    {
        Rates();Register("blade-fury-reentry-check",Differential);Register("blade-fury-reentry-reload-check",()=>{Differential();Reload();});
        Register("blade-fury-reentry-live-check",()=>FixtureNetwork.Connected("blade-fury-reentry-live-result",network=>FixtureClock.NextLoop("blade-fury-reentry-live-result",()=>WorldCase(network))));
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Released-state fresh Fury entry; unsupported fresh rewinds and learned input remain gated",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blade-fury-reentry-source-probe.json")));int contexts=0,frames=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();int mana=N(1),emits=0,made=0,paid=0;
            var services=new BladeFurySequenceServices(()=>mana,cost=>{if(mana<cost){return false;}mana-=cost;paid++;return true;},()=>true,()=>N(5)!=0,()=>{emits++;if(N(6)!=0){made++;}return N(6)!=0;});
            var previous=new BladeFurySequenceState(BladeFurySequence.NeutralMode,false,999,7,
                new(false,N(2)!=0?1005:0,N(3),192){Lease=N(2)!=0},BladeFuryAnimation.Create(990*256,256) with{Events=[new(BladeFurySequence.RemoveState,1005,0),new(SourceAnimationEvents.ModeChange,1001,4,8),new(SourceAnimationEvents.EndAnimation,1002,0)]});
            var entered=BladeFurySequence.ReenterReleased(previous,1000,N(0),services);var state=entered.State;
            for(int frame=1000;frame<=1023;frame++)
            {
                state=BladeFurySequence.Advance(state,frame,N(0),services);var e=row[7][frame-1000];int E(int i)=>e[i].GetInt32();string context=$"case={contexts} frame={frame} rank={N(0)} mana={N(1)} lease={N(2)} deadline={N(3)}";
                Require(state.Mode==E(0)&&state.UsedSkill==(E(1)!=0)&&state.Channel.Active==(E(2)!=0)&&state.Channel.HasLease==(E(3)!=0)
                    &&(!state.Channel.HasLease||state.Channel.Expires==E(4))&&state.Channel.NextShot==E(5)&&state.Channel.Flags==e[6].GetUInt32(),"Source reentry state "+context);
                Require(mana==E(7)-(N(0)==1?E(10)*256:0)&&emits==E(8)&&made==E(9)&&paid==E(10)&&state.Animation.Epoch256==E(11),
                    $"Source reentry resources {context}; mana={mana}/{E(7)} emits={emits}/{E(8)} paid={paid}/{E(10)} epoch={state.Animation.Epoch256}/{E(11)}");
                var events=e[12];Require(state.Animation.Events.Length==events.GetArrayLength(),"Reentry event count "+context);
                for(int i=0;i<state.Animation.Events.Length;i++){var item=events[i];Require(state.Animation.Events[i]==new SourceAnimationEvent(item[0].GetInt32(),item[1].GetInt32(),item[2].GetInt32(),item[3].GetInt32()),"Reentry FIFO event "+context);}
                frames++;
            }
            contexts++;
        }
        Require(contexts==576&&frames==13824,"Released-state reentry inventory");
        var basis=new BladeFurySequenceState(1,false,999,0,new(false,1005,999,192){Lease=true},BladeFuryAnimation.Create(990*256,256));
        var unused=new BladeFurySequenceServices(()=>4096,_=>throw new Exception("Unsupported entry debited"),()=>throw new Exception("Unsupported entry allocated"),()=>throw new Exception("Unsupported entry aimed"),()=>throw new Exception("Unsupported entry emitted"));
        foreach(var unsupported in new[]{basis with{Channel=basis.Channel with{Active=true}},basis with{Channel=basis.Channel with{NextShot=1001}}})
        {bool refused=false;try{BladeFurySequence.ReenterReleased(unsupported,1000,3,unused);}catch(NotSupportedException){refused=true;}Require(refused,"Unverified fresh sequence rewind was guessed");}
        Require(!AssassinCombat.Implemented(CombatAbility.BladeFury),"Prepared reentry enabled an unfinished learned skill");
    }
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static void WorldCase(NetState network)
    {
        PlayerMobile p;_creating=true;try{p=BladeBodyChecks.Fresh(7100,24);}finally{_creating=false;}p.Name="BladeFuryReentryFixture";
        FixtureNetwork.Authenticate(network,p,"fury-reentry-");bool keep=!File.Exists(Manifest);
        try
        {
            DateTime epoch=DateTime.UtcNow.AddDays(2);var point=new Point3D(p.X+12,p.Y,p.Z);NecromancerEffects.SetMana256(p,1663);
            Require(BladeFuryModes.BeginPrepared(p,Serial.Zero,point,epoch),"Original Fury entry");long original=BladeFuryModes.State(p).Id;
            Require(BladeFuryModes.StopIntent(p,epoch.AddMilliseconds(40)),"Original stop intent");BladeFuryModes.Advance(p,epoch.AddMilliseconds(19*40));
            Require(!BladeFuryModes.Active(p)&&BladeFuryModes.State(p).Sequence.Channel is{Active:false,Lease:true,NextShot:16}&&Own(p).Length==1&&NecromancerEffects.ManaBalance256(p)==1407,"Released mode/end/retained list setup");
            Require(BladeFuryModes.BeginPrepared(p,Serial.Zero,point,epoch.AddMilliseconds(20*40)),"Ready released-state fresh entry was refused");
            var second=BladeFuryModes.State(p);Require(second.Id>original&&second.EpochUtc==epoch&&second.Sequence.Channel is{Active:false,Lease:true,Expires:27,NextShot:24}
                &&Own(p).Length==2&&NecromancerEffects.ManaBalance256(p)==1151,"Reentry reset six-mana gate/reactivated state or lost immediate payment/blade");
            Require(!BladeFuryModes.BeginPrepared(p,Serial.Zero,point,epoch.AddMilliseconds(20*40))&&Own(p).Length==2&&NecromancerEffects.ManaBalance256(p)==1151,"Duplicate active entry paid/emitted twice");
            BladeFuryModes.Advance(p,epoch.AddMilliseconds(21*40));Require(BladeFuryModes.State(p).Sequence.Channel.HasLease,"Stale original removal callback erased renewed list");
            BladeFuryModes.Advance(p,epoch.AddMilliseconds(39*40));Require(BladeFuryModes.State(p)==null&&Own(p).Length==2&&NecromancerEffects.ManaBalance256(p)==1151,"Renewed expiry/action/end added a blade/payment or retained mode");
            Require(!BladeFuryModes.BeginPrepared(p,Serial.Zero,point,epoch.AddMilliseconds(40*40))&&BladeFuryModes.State(p)==null,"Expired state bypassed fresh six-mana threshold");
            BladeFuryModes.Cancel(p);WeaponMissiles.Cancel(p);
            if(keep){p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,1151)));}
            Log.Information("blade-fury-reentry-live-result: passed");
        }
        catch{p.Delete();throw;}
        finally{BladeFuryModes.Cancel(p);WeaponMissiles.Cancel(p);if(!keep){p.Delete();}}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)s.Owner) as PlayerMobile;
        Require(p is{Alive:true,Deleted:false}&&NecromancerEffects.ManaBalance256(p)==s.Mana&&BladeFuryModes.State(p)==null&&Own(p).Length==0,"Original reentry character/resources changed or transient input replayed");
    }
}
