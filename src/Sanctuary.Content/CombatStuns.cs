// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record CombatStunState(uint Target,DateTime UntilUtc);
public sealed class CombatStunProfiles:GenericPersistence
{
    public CombatStunProfiles():base("SanctuaryCombatStuns",134){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(CombatStuns.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown combat stun version");}
        CombatStuns.Restore(JsonSerializer.Deserialize<CombatStunState[]>(reader.ReadString()));
    }
    public override void PostDeserialize()=>CombatStuns.Snapshot();
}
public static class CombatStuns
{
    private static readonly Dictionary<uint,CombatStunState> _states=new();
    private static readonly Dictionary<uint,DateTime> _visuals=new();
    private static readonly Dictionary<Serial, DateTime> _encounterFreezes = new();
    public static CombatStunProfiles Profiles{get;private set;}
    internal static Func<int,int> Roll=Utility.Random;
    public static void Configure()=>Profiles=new();
    public static void Initialize()
    {
        var created=CorpseRecoveryHooks.Created;
        CorpseRecoveryHooks.Created=corpse=>{Clear(corpse.Owner);created?.Invoke(corpse);};
        Timer.StartTimer(TimeSpan.FromMilliseconds(125), TimeSpan.FromMilliseconds(125), RefreshVisuals);
    }
    public static void Clear(Mobile target)
    {
        if (target == null) { return; }
        _states.Remove(target.Serial.Value);
        EndVisual(target.Serial.Value);
    }
    public static int Remaining(Mobile target)
    {
        if(target is not BaseCreature{Alive:true,Deleted:false} || !_states.TryGetValue(target.Serial.Value,out var state)){return 0;}
        int remaining=(int)Math.Clamp(Math.Ceiling((state.UntilUtc-TimedEffects.Now).TotalMilliseconds),0,int.MaxValue);
        if(remaining==0){_states.Remove(target.Serial.Value);}return remaining;
    }
    internal static int Frames(int duration,bool monster,bool unique,bool boss,int velocity,bool hireling,int roll)
    {
        if(duration<=0){return 0;}
        int result=0;
        if(!monster){duration=Math.Min(duration,250);result=duration;}
        if((!unique || roll>=90) && !boss && velocity!=0)
        {result=hireling && duration>=13?13:Math.Min(duration,250);}
        return result;
    }
    internal static bool Rules(BaseCreature target,out bool boss,out int velocity)
    {
        if(target is AssassinShadowBody{SourceUnique:true} shadow)
        {
            // Intrinsic movement uses its separate animation profile. ExecuteEvents
            // reads the original monstats Velocity (currently zero), not movement speed.
            boss=NecromancerSummons.Source(shadow.SourceMonsterId,"boss")==1;
            velocity=NecromancerSummons.Source(shadow.SourceMonsterId,"Velocity");return true;
        }
        var profile=CreatureProfiles.For(target);
        velocity=0;
        boss=profile.Traits.Boss==true;
        if(profile.SourceStats.TryGetValue("boss",out var value) && value=="1"){boss=true;}
        if(profile.SourceStats.TryGetValue("Velocity",out var text) && int.TryParse(text,out velocity)){return true;}
        // Native encounter archetypes have an explicit movement profile instead
        // of a D2 monstats row. Only the nonzero eligibility bit is needed here.
        if(target is EncounterCreature && profile.Movement.WalkMs is >0){velocity=1;return true;}
        return false;
    }
    public static bool Apply(BaseCreature target,int frames,DateTime? at=null)
    {
        if(target is not{Alive:true,Deleted:false} || !Rules(target,out bool boss,out int velocity)){return false;}
        bool unique=PreparedMonsterStun.IsUnique(target);
        int duration=Frames(frames,true,unique,boss,velocity,false,unique?Roll(100):0);
        DateTime until=(at??TimedEffects.Now).AddMilliseconds(duration*40);
        if(duration<=0 || until<=TimedEffects.Now){return false;}
        return Commit(target,until);
    }
    // Recipient duration has already passed the source unique/boss/velocity/
    // hireling selection. Do not roll or resolve metadata a second time.
    internal static bool ApplyPreparedDuration(BaseCreature target,int duration,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc || duration is <1 or >250){throw new ArgumentOutOfRangeException();}
        if(target is not{Alive:true,Deleted:false}){return false;}
        var until=at.AddMilliseconds(duration*40);
        return until>TimedEffects.Now&&Commit(target,until);
    }

    internal static void ApplyFamiliar(Mobile target) => ApplyEncounter(target, 75);

    internal static void ApplyEncounter(Mobile target, int frames) => TryApplyEncounter(target, frames);

    internal static bool TryApplyEncounter(Mobile target, int frames)
    {
        var now = TimedEffects.Now;
        frames = Math.Clamp(frames, 1, 250);
        int durationMs = frames * 40;
        var duration = TimeSpan.FromMilliseconds(durationMs);
        var until = now + duration;
        if (target is BaseCreature creature)
        {
            return (!_states.TryGetValue(creature.Serial.Value, out var state) || state.UntilUtc < until)
                && ApplyPreparedDuration(creature, frames, now);
        }
        if (target is not PlayerMobile { Alive: true, Deleted: false } player)
            return false;
        var existing = _encounterFreezes.GetValueOrDefault(player.Serial);
        if (existing >= until || player.Frozen && existing <= now)
            return false;

        _encounterFreezes[player.Serial] = until;
        ClassCombat.Stop(player);
        if (player.Spell is Spell spell)
            spell.Disturb(DisturbType.Hurt);
        player.Frozen = false;
        player.Freeze(duration);
        player.FixedEffect(SpellEffectGraphics.CombatStun, 10, (durationMs + 99) / 100);
        Timer.DelayCall(duration, () =>
        {
            if (_encounterFreezes.GetValueOrDefault(player.Serial) == until)
                _encounterFreezes.Remove(player.Serial);
        });
        return true;
    }
    private static bool Commit(BaseCreature target,DateTime until)
    {
        ReviveSwings.Cancel(target,false);
        _states[target.Serial.Value]=new(target.Serial.Value,until);
        if(target is GraveyardUndead undead){undead.InterruptForRecovery();}
        else if(target is SkeletonGuardian guardian){guardian.InterruptForRecovery();}
        if(target.Spell is Spell spell){spell.Disturb(DisturbType.Hurt);}
        target.AIObject?.ResetApproachState();target.AIObject?.AITimer.Prod();
        RefreshVisual(target, until, TimedEffects.Now);
        return true;
    }

    private static bool Visible(Mobile target) =>
        !World.Loading && target is { Alive: true, Deleted: false, Map: { } map } && map != Map.Internal;

    private static void RefreshVisual(Mobile target, DateTime until, DateTime now)
    {
        if (!Visible(target) || _visuals.GetValueOrDefault(target.Serial.Value) > now) { return; }
        int remaining = (int)Math.Clamp(Math.Ceiling((until - now).TotalMilliseconds), 1, 700);
        target.FixedEffect(SpellEffectGraphics.CombatStun, 10, (remaining + 49) / 50);
        _visuals[target.Serial.Value] = now.AddMilliseconds(Math.Min(remaining, 450));
    }

    private static void EndVisual(uint id)
    {
        if (!_visuals.Remove(id)) { return; }
        var target = World.FindMobile((Serial)id);
        if (Visible(target)) { target.FixedEffect(SpellEffectGraphics.CombatStun, 10, 0); }
    }

    private static void RefreshVisuals()
    {
        var now = TimedEffects.Now;
        foreach (var (id, state) in _states.ToArray())
        {
            if (state.UntilUtc > now) { RefreshVisual(World.FindMobile((Serial)id), state.UntilUtc, now); }
        }
        foreach (var id in _visuals.Keys.ToArray())
        {
            if (!_states.TryGetValue(id, out var state) || state.UntilUtc <= now || !Visible(World.FindMobile((Serial)id)))
            {
                EndVisual(id);
            }
        }
    }
    public static CombatStunState[] Snapshot()
    {
        foreach(var state in _states.Values.ToArray())
        {if(state.UntilUtc<=TimedEffects.Now || World.FindMobile((Serial)state.Target) is not BaseCreature{Alive:true,Deleted:false}){_states.Remove(state.Target);}}
        return _states.Values.OrderBy(x=>x.Target).ToArray();
    }
    public static void Restore(CombatStunState[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing combat stuns");}
        var states=new Dictionary<uint,CombatStunState>();
        foreach(var state in saved)
        {if(!((Serial)state.Target).IsMobile || state.UntilUtc.Kind!=DateTimeKind.Utc || !states.TryAdd(state.Target,state)){throw new InvalidDataException("Invalid combat stun");}}
        _states.Clear();foreach(var state in states){_states.Add(state.Key,state.Value);}
    }
}
