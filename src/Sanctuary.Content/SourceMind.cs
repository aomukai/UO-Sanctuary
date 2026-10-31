// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary;

// The source's generic monster target override has its own AIRESET deadline.
// Removing the visible curse restores alignment, not this override or RNG.
public sealed record MindAiState(ulong Seed,int Map,DateTime UntilUtc,int Mode=3,uint Focus=0,long Draws=0);
public static class SourceMind
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SourceMind));
    private static readonly HashSet<Serial> Tracked=new();
    private static readonly Dictionary<Serial,int> Selecting=new();
    internal static bool Neutral(Mobile actor)=>!PaladinConversion.Active(actor)
        &&NecromancerEffects.Curse(actor)?.Ability is CombatAbility.Confuse or CombatAbility.Attract;
    private static int Alignment(Mobile actor)=>Neutral(actor)?1:actor is PlayerMobile
        ||PaladinConversion.Active(actor)||actor is BaseCreature c&&c.GetMaster() is PlayerMobile?2:0;
    private static bool Aligned(int a,int b)=>a==b&&a is 0 or 2;
    private static bool Candidate(Mobile actor,Mobile target)=>target!=actor&&target is{Alive:true,Deleted:false,Blessed:false}
        &&target.Map==actor.Map&&(target is PlayerMobile||target is BaseCreature c&&c is not BaseVendor&&c.CanBeDamaged()
            &&(Neutral(c)||c.GetMaster() is PlayerMobile||PaladinConversion.SourceRule(c)?.Evil==true));
    public static void Initialize()
    {
        var enemy=ClassCombatHooks.CreatureEnemy;
        ClassCombatHooks.CreatureEnemy=(actor,target)=>Enemy(actor,target)??enemy?.Invoke(actor,target);
        var friend=ClassCombatHooks.CreatureFriend;
        ClassCombatHooks.CreatureFriend=(actor,target)=>Friend(actor,target)??friend?.Invoke(actor,target);
        Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),Expire);
    }
    private static bool? Enemy(BaseCreature actor,Mobile target)
    {
        // Conversion's ownership/friendship contract retains precedence.
        if(PaladinConversion.Active(actor)||PaladinConversion.Active(target)){return null;}
        if(Selecting.TryGetValue(actor.Serial,out int alignment)){return Candidate(actor,target)&&SourceTerror.Distance(actor,target)<=35&&!Aligned(alignment,Alignment(target));}
        var mind=NecromancerEffects.Profiles.Find(actor)?.Mind;
        if(mind is{Mode:2 or 3}&&mind.UntilUtc>TimedEffects.Now&&mind.Focus==target?.Serial.Value){return Candidate(actor,target);}
        return Neutral(actor)||Neutral(target)?Candidate(actor,target):null;
    }
    private static bool? Friend(BaseCreature actor,Mobile target)
    {
        if(PaladinConversion.Active(actor)||PaladinConversion.Active(target)){return null;}
        if(actor==target){return true;}
        if(Selecting.TryGetValue(actor.Serial,out int alignment)&&Candidate(actor,target)){return Aligned(alignment,Alignment(target));}
        if(Neutral(actor)||Neutral(target)){return false;}
        return NecromancerEffects.Profiles.Find(actor)?.Mind is{Mode:2 or 3} mind&&mind.UntilUtc>TimedEffects.Now&&mind.Focus==target?.Serial.Value?false:null;
    }
    internal static MindAiState Confuse(BaseCreature actor,NecromancerState state,int frames,DateTime at)
        =>new(state.Mind?.Seed??SourceRandom.InitLow(unchecked((int)Random.Shared.NextInt64(1L<<32))),actor.Map.MapID,at.AddMilliseconds(frames*40L));
    internal static MindAiState Attract(BaseCreature actor,NecromancerState state,Mobile target,int frames,DateTime at)
        =>new(state.Mind?.Seed??SourceRandom.InitLow(unchecked((int)Random.Shared.NextInt64(1L<<32))),actor.Map.MapID,
            at.AddMilliseconds(frames*40L),2,target.Serial.Value,state.Mind?.Draws??0);
    internal static NecromancerState Superseded(NecromancerState state)=>state.Mind is{} mind
        ?state with{Mind=mind with{Mode=0,Focus=0}}:state;
    internal static void Validate(MindAiState state)
    {
        if(state.Mode is not(0 or 2 or 3)||state.Map<0||state.Map>=Map.Maps.Length||Map.Maps[state.Map]==null||Map.Maps[state.Map]==Map.Internal
            ||state.UntilUtc.Kind!=DateTimeKind.Utc||state.Draws<0||state.Mode==2&&state.Focus==0||state.Focus!=0&&!((Serial)state.Focus).IsMobile)
        {throw new InvalidDataException("Invalid generic mind target override");}
    }
    internal static void Changed(Mobile actor,NecromancerState state)
    {
        if(state?.Mind is{Mode:2 or 3}||state?.Curse?.Ability is CombatAbility.Confuse or CombatAbility.Attract){Tracked.Add(actor.Serial);}
        else{Tracked.Remove(actor.Serial);}
    }
    internal static void AfterLoad(IEnumerable<KeyValuePair<Serial,NecromancerState>> states)
    {Tracked.Clear();foreach(var row in states){if(World.FindMobile(row.Key) is{} actor){Changed(actor,row.Value);}}Expire();}
    private static void Expire()
    {
        foreach(var serial in Tracked.ToArray())
        {
            var actor=World.FindMobile(serial);var state=actor==null?null:NecromancerEffects.Profiles.Find(actor);
            if(state==null){Tracked.Remove(serial);continue;}
            bool changed=false;
            if(state.Curse is{Ability:CombatAbility.Confuse or CombatAbility.Attract} curse&&curse.UntilUtc<=TimedEffects.Now)
            {state=state with{Curse=null};changed=true;}
            if(state.Mind is{Mode:2 or 3} mind&&mind.UntilUtc<=TimedEffects.Now){state=Superseded(state);changed=true;}
            if(changed){NecromancerEffects.Profiles.Store(actor,state);if(actor is BaseCreature creature){creature.ForceReacquire();}}
        }
    }
    internal static void ClearOverride(BaseCreature actor)
    {if(NecromancerEffects.Profiles.Find(actor) is{} state){NecromancerEffects.Profiles.Store(actor,Superseded(state));}}
    internal static Mobile AttractedTarget(BaseCreature actor)
    {
        if(NecromancerEffects.Profiles.Find(actor)?.Mind is not{Mode:2} mind){return null;}
        if(mind.UntilUtc>TimedEffects.Now&&mind.Map==actor.Map?.MapID
            &&World.FindMobile((Serial)mind.Focus) is BaseCreature{Alive:true,Deleted:false} target&&target.Map==actor.Map){return target;}
        ClearOverride(actor);return null;
    }
    internal static void Think(BaseCreature actor)
    {
        var state=NecromancerEffects.Profiles.Find(actor);var mind=state?.Mind;
        if(mind is not{Mode:2 or 3}||mind.UntilUtc<=TimedEffects.Now||actor.AIObject is not{} ai||actor.Map==null||actor.Map==Map.Internal
            ||actor.Frozen||actor.Paralyzed||NamedControl.FrozenRemaining(actor)>0||FreezeStates.Waiting(actor)
            ||state.Curse?.Terror!=null||NecromancerEffects.Vision(state.Curse)){return;}
        if(actor.Map.MapID!=mind.Map||actor.Controlled||actor.Summoned||PaladinConversion.Active(actor))
        {Log.Warning("Generic mind override world transition unsupported: {Target}",actor.Serial.Value);NecromancerEffects.Profiles.Store(actor,Superseded(state));return;}
        if(mind.Mode==2)
        {
            if(AttractedTarget(actor) is{} attracted)
            {actor.Combatant=attracted;actor.FocusMob=null;ai.Action=ActionType.Combat;}
            return; // Explicit target override does not roll or use the scan clock.
        }
        // Use the existing native acquisition clock and scan/LOS/food/ordering
        // services. They remain world adapters, not source-node parity claims.
        if(actor.NextReacquireTime>Core.TickCount){return;}
        ulong seed=mind.Seed;int bit=(int)(SourceRandom.Roll(ref seed)&1);int alignment=Alignment(actor);
        if(alignment==1){alignment=bit!=0?2:0;}else if(bit!=0){alignment=alignment==0?2:0;}
        Selecting[actor.Serial]=alignment;
        Mobile target=null;
        try{if(ai.AcquireFocusMob(18,actor.FightMode,false,false,true)){target=actor.FocusMob;}}
        finally{Selecting.Remove(actor.Serial);}
        var next=mind with{Seed=seed,Draws=checked(mind.Draws+1),Mode=target==null?0:3,Focus=target?.Serial.Value??0};
        NecromancerEffects.Profiles.Store(actor,state with{Mind=next});
        if(target==null){return;} // Source clears mode3 when its scan finds none.
        actor.Combatant=target;actor.FocusMob=null;ai.Action=ActionType.Combat;
        // Continue ordinary species AI, including its own abilities and timing.
    }
}
