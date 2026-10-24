// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Logging;

namespace Sanctuary.Integration;

public static class FreezeStateChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FreezeStateChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("freeze-state-check","Source freeze-state, length and removal comparison",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("freeze-state-check: passed");}catch(Exception e){Log.Error(e,"freeze-state-check: failed");}}));
    private static void Require(bool valid,string why){if(!valid){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"freeze-state-source-probe.json")));
        var owner=MagicArrowChecks.Fresh("FreezeStateOwner");var target=MagicArrowChecks.Target(owner);int states=0,lengths=0,removals=0,callbacks=0;
        try
        {
            foreach(var row in source.RootElement.GetProperty("state").EnumerateArray())
            {
                int type=row[0].GetInt32(),diff=row[1].GetInt32(),effect=row[3].GetInt32(),frames=row[4].GetInt32(),flags=row[6].GetInt32();bool existing=row[5].GetInt32()!=0;
                int mode=frames<=0?0:FreezeStates.Mode(type,(flags&1)!=0,(flags&2)!=0,(flags&4)!=0,(flags&8)!=0,row[2].GetInt32()!=0?effect:null);
                Require((row[10].GetInt32()==1)==(mode==1) && (row[12].GetInt32()!=0)==(mode==2),"Compiled freeze mode/delegation gates");
                TimedEffects.Clear(target);long tick=Core.TickCount;var now=TimedEffects.TimeAt(tick);
                if(existing){TimedEffects.Actor(target,now).Control=new(now.AddMilliseconds(8000),default,default,0,0,0,new(41,now.AddMilliseconds(8040)));}
                if(mode==2)
                {
                    int raw=FreezeStates.RawFrames(frames,type==1,new[]{1,2,4}[diff]);Require(row[13].GetInt32()==1000+raw+1,"Compiled freeze AI wake frame");
                    Require(FreezeStates.Commit(owner,target,frames,type==1,new[]{1,2,4}[diff],tick),"Native freeze commit");var state=NamedControl.State(target);
                    int expiry=Math.Max(existing?200:0,Math.Max(1,raw));uint expected=row[9].GetInt32()==17?owner.Serial.Value:41;
                    Require(state.FreezeUntil==now.AddMilliseconds(expiry*40) && state.Freeze.Source==expected && state.Freeze.AiUntilUtc==now.AddMilliseconds((raw+1)*40L),"Compiled/native freeze expiry/original owner/event-setter projection");
                }
                else{Require((NamedControl.FrozenRemaining(target)>0)==existing,"Rejected/fallback source branch replaced freeze");}
                states++;
            }
            foreach(var row in source.RootElement.GetProperty("length").EnumerateArray())
            {
                int value=ColdStates.LengthFrames(row[0].GetInt32(),row[1].GetInt32(),row[2].GetInt32()!=0,row[3].GetInt32()!=0);
                Require(value==row[4].GetInt32() && value==row[5].GetInt32(),"Complete total-damage source length ordering");lengths++;
            }
            foreach(var row in source.RootElement.GetProperty("remove").EnumerateArray())
            {
                int type=row[0].GetInt32(),dead=row[1].GetInt32(),stay=row[2].GetInt32(),mode=row[5].GetInt32(),diff=row[6].GetInt32(),delay=row[7].GetInt32();bool retained=dead!=0 && stay!=0;
                Require(row[8].GetInt32()==(retained?1:0) && row[9].GetInt32()==(retained?0:1),"Source freeze removal/death-stay outcome");
                if(!retained && type==1 && row[3].GetInt32()!=0 && row[4].GetInt32()!=0)
                {Require(row[10].GetInt32()==1000+FreezeStates.RemovalDelay(delay,delay+diff*10,mode!=0),"Compiled/native AI removal delay");}
                else{Require(row[10].GetInt32()==0,"Unexpected source removal AI event");}removals++;
            }
            foreach(var row in source.RootElement.GetProperty("callback").EnumerateArray())
            {
                bool active=row[0].GetInt32()!=0 && row[1].GetInt32()!=0;
                int freeze=active?SourcePercentage.Calculate(row[3].GetInt32(),Math.Max(0,row[2].GetInt32()),100):99;
                Require(row[4].GetInt32()==(active?0:66) && row[5].GetInt32()==freeze && row[6].GetInt32()==123 && row[7].GetInt32()==789,"Source callback length-only conversion");callbacks++;
            }
            Require(states==12096 && lengths==224 && removals==1296 && callbacks==100,"Incomplete freeze source fixture");
            TimedEffects.Clear(target);long at=Core.TickCount;var start=TimedEffects.TimeAt(at);Require(FreezeStates.Apply(owner,target,50,at),"Real source freeze application");
            var actor=TimedEffects.Actor(target,start);Require(NamedControl.FrozenRemaining(target)>0 && actor.ChillPercent==0 && NamedControl.Think(target) && !NamedControl.SpecialAllowed(target),"Freeze control/independent chill consumers");
            var until=actor.Control.FreezeUntil;FreezeStates.Settle(target,actor,until);Require(actor.Control.Freeze.Removed && actor.Control.Freeze.AiUntilUtc==until.AddMilliseconds(600),"Mapped Normal skeleton source thaw delay");
            TimedEffects.Settle(target,actor.Control.Freeze.AiUntilUtc);Require(!TimedEffects.TryActor(target,out _),"Source freeze/AI expiry cleanup");
            foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
            {
                var variant=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};MonsterVariants.Assign(variant,difficulty);variant.MoveToWorld(new(owner.X+7,owner.Y,owner.Z),owner.Map);variant.AIObject.AITimer.Stop();variant.SetResistance(ResistanceType.Cold,0);
                try{long tick=Core.TickCount;var now=TimedEffects.TimeAt(tick);Require(FreezeStates.Apply(owner,variant,101,tick),"Actual variant freeze");TimedEffects.TryActor(variant,out var actor2);
                    int expected=difficulty==MonsterDifficulty.Normal?101:difficulty==MonsterDifficulty.Nightmare?50:25;
                    Require(actor2.Control.FreezeUntil==now.AddMilliseconds(expected*40) && actor2.ChillPercent==0 && CreatureHitRecovery.Remaining(variant)>0,"Variant frame truncation/movement control or duplicate chill");}
                finally{TimedEffects.Clear(variant);variant.Delete();}
            }
            foreach(var grade in new[]{EncounterGrade.Champion,EncounterGrade.Unique,EncounterGrade.SuperUnique})
            {
                var elite=new EncounterCreature{NoKillAwards=true,Frozen=true,CantWalk=true};var profile=EncounterProfiles.Profile("crypt-sentinel-90");elite.Assign(profile,profile.MinimumLevel,MonsterDifficulty.Normal,grade,ChampionKind.Champion,[],EliteAura.None,null,new Random(173));elite.MoveToWorld(new(owner.X+7,owner.Y,owner.Z),owner.Map);elite.AIObject.AITimer.Stop();elite.SetResistance(ResistanceType.Cold,0);
                try{Require(FreezeStates.Apply(owner,elite,100,Core.TickCount) && TimedEffects.Chill(elite,TimedEffects.Now)>0 && NamedControl.FrozenRemaining(elite)==0,"Actual native elite unique-bit projection did not chill instead of freeze");}
                finally{TimedEffects.Clear(elite);elite.Delete();}
            }
            TimedEffects.Clear(target);var other=MagicArrowChecks.Fresh("FreezeOtherOwner");try
            {
                Require(FreezeStates.Apply(owner,target,100,Core.TickCount) && FreezeStates.Apply(other,target,201,Core.TickCount),"Real cross-owner freeze refresh");var control=NamedControl.State(target);Require(control.Freeze.Source==owner.Serial.Value,"Freeze owner replaced");
                Require(NamedControl.FreezeSkill(other,target,40) && NamedControl.State(target).FreezeUntil==control.FreezeUntil && NamedControl.State(target).Freeze.Source==owner.Serial.Value,"Existing spell-freeze entry shortened shared state or replaced owner");
                TimedEffects.TryActor(target,out var pending);pending.Control=pending.Control with{FreezeUntil=TimedEffects.Now.AddMilliseconds(-40)};
                Require(FreezeStates.Apply(other,target,100,Core.TickCount) && NamedControl.State(target).Freeze.Source==other.Serial.Value,"Expired, unsettled freeze retained former owner");
                pending.Control=pending.Control with{Freeze=null,FreezeUntil=TimedEffects.Now.AddSeconds(9)};var retained=pending.Control.FreezeUntil;
                Require(FreezeStates.Apply(owner,target,1,Core.TickCount) && NamedControl.State(target).Freeze.Source==0 && NamedControl.State(target).FreezeUntil==retained,"Unknown older freeze provenance or longer duration replaced");
            }finally{TimedEffects.Clear(target);other.Delete();}
            var caster=new GraveyardLich{CantWalk=true,NoKillAwards=true,Name="FreezeStateCaster"};caster.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);caster.AIObject.AITimer.Stop();caster.SetResistance(ResistanceType.Cold,0);
            try
            {
                Require(FreezeStates.Apply(owner,caster,1,Core.TickCount) && NamedControl.State(caster)?.Freeze!=null,"Actual caster freeze fixture");TimedEffects.TryActor(caster,out var casterState);FreezeStates.Settle(caster,casterState,casterState.Control.FreezeUntil);
                // This custom caster has no pinned D2 species row. Supply the
                // post-thaw state with an inspection hold as a declared control
                // input; the mapped skeleton below derives its own real wait.
                casterState.Control=casterState.Control with{FreezeUntil=default,Freeze=new(owner.Serial.Value,TimedEffects.Now.AddSeconds(5),true)};
                Require(FreezeStates.Waiting(caster),"Declared source thaw window is not active");
                // Isolate the source thaw wait from the monster's unrelated
                // native attack cooldown, then call its real spell entry point.
                var nextAction=typeof(GraveyardUndead).GetField("_nextAction",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(nextAction!=null,"Native monster cooldown fixture");nextAction.SetValue(caster,Core.TickCount);
                bool spell=caster.BeginFirebolt(owner),special=NamedControl.SpecialAllowed(caster),targetAllowed=NamedControl.TargetAllowed(caster,owner);
                Require(!spell && !special && !targetAllowed,$"Direct attack bypassed post-thaw wait: spell={spell}, special={special}, target={targetAllowed}");
                FreezeStates.Settle(caster,casterState,casterState.Control.Freeze.AiUntilUtc);
                Require(NamedControl.SpecialAllowed(caster) && NamedControl.TargetAllowed(caster,owner) && caster.BeginFirebolt(owner),"Thaw wait did not release real spell action");
            }finally{TimedEffects.Clear(caster);caster.Delete();}
            var melee=new GraveyardSkeleton{CantWalk=true,NoKillAwards=true};melee.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);melee.AIObject.AITimer.Stop();melee.SetResistance(ResistanceType.Cold,0);
            try
            {
                Require(FreezeStates.Apply(owner,melee,1,Core.TickCount),"Actual melee freeze fixture");TimedEffects.TryActor(melee,out var meleeState);FreezeStates.Settle(melee,meleeState,meleeState.Control.FreezeUntil);
                var clock=typeof(GraveyardUndead).GetField("_nextAction",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);long ready=Core.TickCount-1;clock.SetValue(melee,ready);melee.BeginMelee(owner);
                Require(melee.NextMeleeDecision==ready,"Direct melee bypassed source thaw wait");FreezeStates.Settle(melee,meleeState,meleeState.Control.Freeze.AiUntilUtc);melee.BeginMelee(owner);
                Require(melee.NextMeleeDecision>ready,"Thaw wait did not release actual melee decision");
            }finally{TimedEffects.Clear(melee);melee.Delete();}
            Require(ColdStates.LengthFrames(3,33,false,true)==0 && ColdStates.LengthFrames(1,-100,false,true)==0,"Odd half-duration correction");
            var old=JsonSerializer.SerializeToNode(new NamedControlState(default,default,default,0,0,0));old.AsObject().Remove("Freeze");Require(old.Deserialize<NamedControlState>().Freeze==null,"Older saved control compatibility");
            Log.Information("Freeze state:12096 application,1296 removal,100 callback and224 length source/native cases passed");
        }finally{TimedEffects.Clear(target);target.Delete();owner.Delete();}
    }
}
