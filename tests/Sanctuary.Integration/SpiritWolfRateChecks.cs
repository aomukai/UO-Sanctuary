// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class SpiritWolfRateChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SpiritWolfRateChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("spirit-wolf-rate-check","Wolf nonweapon attack-rate composition",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("spirit-wolf-rate-check: passed");}catch(Exception e){Log.Error(e,"spirit-wolf-rate-check: failed");}}));
    private static void Numeric()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"spirit-wolf-rate-source-probe.json")));
        int count=0;var flags=new int[144];flags[9]=1;
        foreach(var row in source.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();int speed=SourceAttackRate.Speed(N(0),N(1),N(2),N(3)==1?N(4):0);
            Require(speed==N(5) && speed==N(6),"Complete source attack speed mismatch");
            var plan=SourceAnimationEvents.Initial(new(0,speed,13*256,flags,[]),0);
            var expected=row[8].EnumerateArray().Select(x=>new SourceAnimationEvent(x[0].GetInt32(),x[1].GetInt32(),x[2].GetInt32(),x[3].GetInt32())).ToArray();
            Require(plan.Events.SequenceEqual(expected),"Complete source attack event plan mismatch");count++;
        }
        Require(count==576,"Wolf attack-rate source inventory");
        Require(DruidWolfGeometry.Clock("A1",264)==(9,13) && DruidWolfGeometry.Clock("A1",88)==(27,38)
            && DruidWolfGeometry.Clock("A1",26)==(89,128),"Wolf animation rate boundary goldens");
    }
    private static void Check()
    {
        Numeric();var p=SpiritWolfWorldChecks.Fresh();var at=TimedEffects.Now.AddHours(1);
        var wolf=DruidSpiritWolfWorld.Create(p,new(p.X+1,p.Y,p.Z),at,41);
        var target=new GraveyardZombie{CantWalk=true,NoKillAwards=true};target.SetHits(10000);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();
        var initial=wolf.Prepared;var auras=PaladinAuras.Snapshot();
        void Through(int frame){var c=wolf.Prepared.Controller;DruidSpiritWolfWorld.Advance(wolf,c.NextUtc.AddMilliseconds((frame-c.Frame-1)*40L),true);}
        void Enter(){wolf.RestorePrepared(initial);Through(25);Require(wolf.Prepared.Controller.Mode==4,"Wolf failed A1entry");}
        try
        {
            Require(!ColdStates.Apply(target,wolf,100,Core.TickCount) && !GearEffects.ApplyChill(wolf,Core.TickCount,4000,target)
                && GearEffects.ChillPercent(wolf)==0 && wolf.ColdResistance==5 && NamedElements.Resolve256(target,wolf,256,"cold",enhance:false).Damage256>0,
                "Observed Spirit Wolf chill gate changed cold resistance or admitted a chill producer");
            Enter();Require(wolf.Prepared.Controller is{AttackSpeed:176,ActionAt:39,EndAt:44},"Base wolf attack clock");
            PaladinAuras.Restore(auras with{Leases=auras.Leases.Append(new PaladinAuraLease(wolf.Serial.Value,p.Serial.Value,CombatAbility.Fanaticism,1,50,at.AddHours(1))).ToArray()});
            Enter();Require(wolf.Prepared.Controller is{AttackSpeed:264,ActionAt:34,EndAt:38},"Live Fanaticism did not set wolf action/end schedule");
            // A rate write is not a reschedule request. Retain the queued
            // attack, then capture changed stats when the next A1begins.
            // Direct stat injection is a component control. Ordinary incoming
            // cold producers above correctly refuse this Spirit Wolf state.
            TimedEffects.Chill(wolf,50,60000,TimedEffects.Now);
            Through(33);Require(wolf.Prepared.Controller.StrikeRequests==0 && wolf.Prepared.Controller.AttackSpeed==264,"Mid-swing cold rescheduled queued action");
            Through(34);Require(wolf.Prepared.Controller.StrikeRequests==1,"Captured action event did not fire once");
            Through(38);int next=wolf.Prepared.Controller.NextThink;Through(next);
            Require(wolf.Prepared.Controller.Mode==4 && wolf.Prepared.Controller.AttackSpeed==176,"Next attack ignored live cold and haste");
            NecromancerEffects.Profiles.Store(wolf,new(Curse:new(CombatAbility.Decrepify,p.Serial.Value,1,at.AddHours(1),wolf.X,wolf.Y)));
            Enter();Require(SourceAttackRate.WolfRate(wolf)==50 && wolf.Prepared.Controller is{AttackSpeed:88,ActionAt:52,EndAt:63},"Combined slows were clamped before haste");
            bool overlap=false;try{Through(47);}catch(NotSupportedException e){overlap=e.Message.Contains("overlaps unfinished A1");}
            Require(overlap && wolf.Prepared.Controller.Fault!=null && wolf.Prepared.Controller.StrikeRequests==0,"Unverified AI overlap silently restarted an unfinished swing");
            TimedEffects.Slow(wolf,50,TimedEffects.Now);Enter();Require(wolf.Prepared.Controller is{AttackSpeed:26,ActionAt:114,EndAt:153},"Final15percent source clamp");
            TimedEffects.Clear(wolf);NecromancerEffects.Profiles.Clear(wolf);PaladinAuras.Restore(auras);
            Enter();Require(wolf.Prepared.Controller.AttackSpeed==176,"Expired/removed effects left stale source rate");
            var saved=wolf.Prepared;string text=wolf.SavedText;bool refused=false;
            try{wolf.RestorePrepared(saved with{Controller=saved.Controller with{AttackSpeed=-1}});}catch(InvalidDataException){refused=true;}
            Require(refused && wolf.SavedText==text,"Malformed attack speed mutated saved state");
            Log.Information("Spirit Wolf rate:576complete source attack writer/event compositions, live Fanaticism/Decrepify/item slow and supplied cold fields, observed incoming-chill refusal, queued swing retention/next-entry refresh, final clamp, explicit unfinished-A1overlap refusal and atomic saved speed controls pass; modern mid-swing/busy-mode parity remains unverified");
        }
        finally{PaladinAuras.Restore(auras);target.Delete();SpiritWolfWorldChecks.End(p);}
    }
}
