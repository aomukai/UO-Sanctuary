// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record SlowMissilesState(uint Target,uint Creator,int Rank,int VelocityPercent,int DamagePercent,DateTime UntilUtc);
// Captured at missile creation. Damage scaling is independent of canSlow.
public readonly record struct SlowMissilePayload(int VelocityPercent,int DamagePercent)
{
    public static SlowMissilePayload Normal=>new(100,100);
    // Unchanged legacy factory operation order, tested against its compiled block.
    internal static int VelocityFp8(int basis,int perLevel,int rank,int flags,int supplied,bool canSlow,int percent)
    {
        if(basis<0 || perLevel<0 || rank<0 || supplied<0 || percent is <0 or >100){throw new ArgumentOutOfRangeException();}
        long value=(flags&4)==0?((long)basis+(long)rank*perLevel/8)*256:(flags&16)==0?(long)supplied*256:supplied;
        if(canSlow){value=value*percent/100;}
        if(value!=0){value=value*75/100;}
        return checked((int)value);
    }
    // Explicit candidate payload operation; no gameplay producer consumes it yet.
    // Original controlled D2R tests establish channel/duration behavior, not the
    // precise modern FP256 rounding or order relative to all other factory terms.
    internal int Damage256(int value)
    {
        if(value<0 || DamagePercent is <0 or >100){throw new ArgumentOutOfRangeException();}
        return (int)((long)value*DamagePercent/100);
    }
    internal MissileSourceStats Scale(MissileSourceStats damage)
    {
        if(damage.BurnMin!=0 || damage.BurnMax!=0 || damage.BurnFrames!=0)
        {throw new NotSupportedException("Slow Missiles burning-rate consumer is not established");}
        // Scale raw endpoints/poison rates, preserving length/count and drain.
        // An aggregate poison total loses the existing source packet structure.
        return damage with
        {
            FireMin=Damage256(damage.FireMin),FireMax=Damage256(damage.FireMax),
            LightningMin=Damage256(damage.LightningMin),LightningMax=Damage256(damage.LightningMax),
            MagicMin=Damage256(damage.MagicMin),MagicMax=Damage256(damage.MagicMax),
            ColdMin=Damage256(damage.ColdMin),ColdMax=Damage256(damage.ColdMax),
            PoisonMin=Damage256(damage.PoisonMin),PoisonMax=Damage256(damage.PoisonMax)
        };
    }
}
public sealed class SlowMissilesProfiles:GenericPersistence
{
    private SlowMissilesState[] _snapshot;
    public SlowMissilesProfiles():base("SanctuarySlowMissiles",149){}
    public override void Serialize(){_snapshot=AmazonSlowMissiles.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Slow Missiles version");}AmazonSlowMissiles.Restore(JsonSerializer.Deserialize<SlowMissilesState[]>(reader.ReadString()));}
    public override void PostDeserialize()=>AmazonSlowMissiles.Prune();
}
// State/launch component in preparation. Not an available talent handler until
// all supported hostile missile consumers, target rules and cast flow are wired.
public static class AmazonSlowMissiles
{
    private static readonly Dictionary<uint,SlowMissilesState> _states=new();
    public static SlowMissilesProfiles Profiles{get;private set;}
    public static void Configure(){Profiles=new();EventSink.WorldSave+=Prune;}
    public static void Initialize()
    {
        var created=CorpseRecoveryHooks.Created;CorpseRecoveryHooks.Created=corpse=>{Clear(corpse.Owner);created?.Invoke(corpse);};
        Timer.StartTimer(TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(1),Prune);
    }
    private static bool Alive(Mobile target)=>target is{Alive:true,Deleted:false,IsDeadBondedPet:false};
    internal static SlowMissilesState State(Mobile target)=>Alive(target) && _states.TryGetValue(target.Serial.Value,out var state) && state.UntilUtc>TimedEffects.Now?state:null;
    internal static SlowMissilePayload Capture(Mobile owner,bool canSlow)
    {
        var state=State(owner);return state==null?SlowMissilePayload.Normal:new(canSlow?state.VelocityPercent:100,state.DamagePercent);
    }
    internal static SlowMissilesState Replacement(SlowMissilesState old,uint target,uint creator,int rank,int speed,int damage,DateTime until,DateTime at)
    {
        // Reuse the existing source state rank/refresh policy. Equal rank keeps
        // the original creator and captured values, replacing only expiration.
        if(old!=null && old.UntilUtc>at)
        {if(rank<old.Rank){return null;}if(rank==old.Rank){return old with{UntilUtc=until};}}
        return new(target,creator,rank,speed,damage,until);
    }
    internal static bool Apply(PlayerMobile creator,Mobile target,int rank,DateTime at)
    {
        if(!Alive(target) || creator==null || rank is <1 or >255){return false;}
        var values=SkillMechanics.Values(creator,CombatAbility.SlowMissiles,rank);
        int speed=(int)values.Eval("aurastatcalc1"),damage=(int)values.Eval("aurastatcalc2");
        if(speed is <0 or >100 || damage is <0 or >100){return false;}
        var next=Replacement(_states.GetValueOrDefault(target.Serial.Value),target.Serial.Value,creator.Serial.Value,values.Rank,speed,damage,
            at.AddMilliseconds(values.Eval("auralencalc")*40),at);
        if(next==null){return false;}_states[target.Serial.Value]=next;return true;
    }
    public static void Clear(Mobile target){if(target!=null){_states.Remove(target.Serial.Value);}}
    internal static void Prune()
    {foreach(var pair in _states.ToArray()){if(pair.Value.UntilUtc<=TimedEffects.Now || !Alive(World.FindMobile((Serial)pair.Key))){_states.Remove(pair.Key);}}}
    internal static SlowMissilesState[] Snapshot(){Prune();return _states.Values.OrderBy(x=>x.Target).ToArray();}
    internal static void Restore(SlowMissilesState[] states)
    {
        if(states==null || states.Length>1000000){throw new InvalidDataException("Missing Slow Missiles states");}
        _states.Clear();foreach(var state in states)
        {
            if(!((Serial)state.Target).IsMobile || state.Target==0 || !((Serial)state.Creator).IsMobile || state.Creator==0
                || state.Rank is <1 or >255 || state.VelocityPercent is <0 or >100 || state.DamagePercent is <0 or >100
                || !_states.TryAdd(state.Target,state)){throw new InvalidDataException("Invalid Slow Missiles state");}
        }
    }
}
