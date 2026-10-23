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
    public static CombatStunProfiles Profiles{get;private set;}
    internal static Func<int,int> Roll=Utility.Random;
    public static void Configure()=>Profiles=new();
    public static void Initialize()
    {
        var created=CorpseRecoveryHooks.Created;
        CorpseRecoveryHooks.Created=corpse=>{Clear(corpse.Owner);created?.Invoke(corpse);};
    }
    public static void Clear(Mobile target){if(target!=null){_states.Remove(target.Serial.Value);}}
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
        bool unique=target is EncounterCreature{Grade:EncounterGrade.Champion or EncounterGrade.Unique or EncounterGrade.SuperUnique};
        int duration=Frames(frames,true,unique,boss,velocity,false,unique?Roll(100):0);
        DateTime until=(at??TimedEffects.Now).AddMilliseconds(duration*40);
        if(duration<=0 || until<=TimedEffects.Now){return false;}
        _states[target.Serial.Value]=new(target.Serial.Value,until);
        if(target is GraveyardUndead undead){undead.InterruptForRecovery();}
        else if(target is SkeletonGuardian guardian){guardian.InterruptForRecovery();}
        if(target.Spell is Spell spell){spell.Disturb(DisturbType.Hurt);}
        target.AIObject?.ResetApproachState();target.AIObject?.AITimer.Prod();
        target.FixedEffect(0x375A,5,5,0x481,0); // Identified placeholder for source stunned-state presentation.
        return true;
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
