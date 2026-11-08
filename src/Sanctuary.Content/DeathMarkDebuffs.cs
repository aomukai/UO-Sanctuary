// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record DeathMarkDebuff(uint Target,uint Creator,int Rank,DateTime UntilUtc);
public sealed class DeathMarkDebuffProfiles:GenericPersistence
{
    private DeathMarkDebuff[] _snapshot;
    public DeathMarkDebuffProfiles():base("SanctuaryDeathMarkDebuffs",247){}
    public override void Serialize(){DeathMarkDebuffs.Prune();_snapshot=DeathMarkDebuffs.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Death Mark debuff version");}DeathMarkDebuffs.Restore(JsonSerializer.Deserialize<DeathMarkDebuff[]>(reader.ReadString()));}
    public override void PostDeserialize()=>DeathMarkDebuffs.Prune();
}

// Proposed stat application for the captured factory's state212. Current
// binder45f8d0 is protected;learned routes stay closed pending the explicit
// proposal decision and remaining controller/cast integration.
internal static class DeathMarkDebuffs
{
    private static ImmutableDictionary<uint,DeathMarkDebuff> _states=ImmutableDictionary<uint,DeathMarkDebuff>.Empty;
    public static void Configure()=>_=new DeathMarkDebuffProfiles();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(250),TimeSpan.FromMilliseconds(250),Prune);
    internal static DeathMarkDebuff State(Mobile target,DateTime? at=null)=>target is{Alive:true,Deleted:false}
        &&_states.TryGetValue(target.Serial.Value,out var s)&&s.UntilUtc>(at??TimedEffects.Now)?s:null;
    internal static int Armor(Mobile target)=>State(target)is{} s?checked(-50-35*(s.Rank-1)):0;
    internal static int Reduction(Mobile target)=>State(target)is{} s?checked(-5-2*(s.Rank-1)):0;
    internal static int DurationFrames(int rank)=>checked(125+13*(rank-1));
    internal static bool ApplyPrepared(PlayerMobile owner,Mobile target,int rank,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||rank is <1 or >255){throw new ArgumentException("Death Mark needs a qualified UTC/rank");}
        if(owner is not{Alive:true,Deleted:false}||target is not{Alive:true,Deleted:false,Blessed:false}||owner==target||owner.Map==null||owner.Map!=target.Map
            ||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Warlock||!ClassCombat.IsHostile(owner,target)) {return false;}
        var row=SkillMechanics.For(CombatAbility.DeathMark);
        if(row.SourceId!=375||row.Text("auratargetstate")!="death_mark"||row.Text("aurastat1")!="armorclass"
            ||row.Text("aurastat2")!="normal_damage_reduction"||row.Text("aurastat3")!="magic_damage_reduction"||PaladinCleansing.IsCurse("death_mark"))
        {throw new NotSupportedException("Unqualified Death Mark stat binding");}
        var old=State(target,at);if(old!=null&&rank<old.Rank){return false;}
        uint creator=old?.Rank==rank?old.Creator:owner.Serial.Value;
        _states=_states.SetItem(target.Serial.Value,new(target.Serial.Value,creator,rank,at.AddMilliseconds(DurationFrames(rank)*40L)));
        owner.DoHarmful(target,true);target.Delta(MobileDelta.Stat);
        target.FixedEffect(0x375A,10,15,0x48A,0); // Identified prepared Death Mark target placeholder.
        return true;
    }
    internal static void Clear(Mobile target){if(target!=null){_states=_states.Remove(target.Serial.Value);target.Delta(MobileDelta.Stat);}}
    internal static void Prune()
    {foreach(var s in _states.Values){var target=World.FindMobile((Serial)s.Target);if(State(target)==null){_states=_states.Remove(s.Target);target?.Delta(MobileDelta.Stat);}}}
    internal static DeathMarkDebuff[] Snapshot()=>_states.Values.OrderBy(s=>s.Target).ToArray();
    internal static void Restore(DeathMarkDebuff[] states)
    {
        var next=ImmutableDictionary<uint,DeathMarkDebuff>.Empty;
        foreach(var s in states??throw new InvalidDataException("Missing Death Mark debuffs"))
        {if(s==null||!((Serial)s.Target).IsMobile||!((Serial)s.Creator).IsMobile||s.Rank is <1 or >255||s.UntilUtc.Kind!=DateTimeKind.Utc||s.UntilUtc<=DateTime.UnixEpoch||next.ContainsKey(s.Target)){throw new InvalidDataException("Invalid Death Mark debuff");}next=next.Add(s.Target,s);}
        _states=next;
    }
}
