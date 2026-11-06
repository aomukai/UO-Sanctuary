// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record FrenzySpeedState(uint Owner,int Rank,int Charges,DateTime UntilUtc);
internal sealed record FrenzySpeed(int Velocity,int AttackRate);
public sealed class FrenzyProfiles:GenericPersistence
{
    public FrenzyProfiles():base("SanctuaryFrenzy",242){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(BarbarianFrenzyEffects.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Frenzy version");}BarbarianFrenzyEffects.Restore(JsonSerializer.Deserialize<FrenzySpeedState[]>(reader.ReadString()));}
}
internal static class BarbarianFrenzyEffects
{
    private static ImmutableDictionary<uint,FrenzySpeedState> _states=ImmutableDictionary<uint,FrenzySpeedState>.Empty;
    public static FrenzyProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()
    {
        var created=CorpseRecoveryHooks.Created;
        CorpseRecoveryHooks.Created=corpse=>{if(corpse.Owner is PlayerMobile owner){Clear(owner);}created?.Invoke(corpse);};
        Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Prune(TimedEffects.Now));
    }
    private static bool Eligible(Mobile owner)=>owner is PlayerMobile{Alive:true,Deleted:false} p
        &&ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)==true&&profile.Class==DiabloClass.Barbarian
        &&SkillMechanics.Rank(p,CombatAbility.Frenzy)>0;
    internal static FrenzySpeedState State(Mobile owner)=>StateAt(owner,TimedEffects.Now);
    private static FrenzySpeedState StateAt(Mobile owner,DateTime at)=>Eligible(owner)&&_states.TryGetValue(owner.Serial.Value,out var state)&&state.UntilUtc>at?state:null;
    internal static FrenzySpeed Values(int charges)
    {
        if(charges is <0 or >255){throw new ArgumentOutOfRangeException(nameof(charges));}
        if(charges==0){return new(0,0);}
        var row=SkillMechanics.For(CombatAbility.Frenzy);
        if(row.SourceId!=147||row.Text("aurastate")!="frenzy"||row.Text("aurastat1")!="velocitypercent"||row.Text("aurastatcalc1")!="dm34"
            ||row.Text("aurastat2")!="attackrate"||row.Text("aurastatcalc2")!="dm56")
        {throw new NotSupportedException("Unmapped Frenzy speed state");}
        var v=new SkillValues(row,key=>key==row.SourceKey?charges:0,_=>0,99);
        return new(checked((int)v.Eval("aurastatcalc1")),checked((int)v.Eval("aurastatcalc2")));
    }
    internal static int Velocity(Mobile owner)=>Values(State(owner)?.Charges??0).Velocity;
    internal static int AttackRate(Mobile owner)=>Values(State(owner)?.Charges??0).AttackRate;
    internal static bool Gain(PlayerMobile owner,SkillValues values,DateTime at)
    {
        if(!Eligible(owner)||values?.Ability!=CombatAbility.Frenzy||values.Rank is <1 or >255||at.Kind!=DateTimeKind.Utc){return false;}
        int frames=checked((int)values.Eval("auralencalc"));if(frames<1){throw new NotSupportedException("Unmapped Frenzy duration");}
        int charges=Math.Min(values.Rank,(StateAt(owner,at)?.Charges??0)+1);
        _states=_states.SetItem(owner.Serial.Value,new(owner.Serial.Value,values.Rank,charges,at.AddMilliseconds(frames*40L)));
        StatNetwork.Send(owner);return true;
    }
    internal static void Clear(Mobile owner)
    {
        if(owner==null){return;}bool had=_states.ContainsKey(owner.Serial.Value);_states=_states.Remove(owner.Serial.Value);
        if(had&&owner is PlayerMobile{Deleted:false} p){StatNetwork.Send(p);}
    }
    internal static void Prune(DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Frenzy clock must be UTC");}
        foreach(var pair in _states)
        {
            var actor=World.FindMobile((Serial)pair.Key);
            if(!Eligible(actor)||pair.Value.UntilUtc<=at){_states=_states.Remove(pair.Key);if(actor is PlayerMobile{Deleted:false} p){StatNetwork.Send(p);}}
        }
    }
    // Immutable state is safe on the persistence worker; pruning and network
    // refresh remain on the game loop. UTC expiry keeps running while offline.
    internal static FrenzySpeedState[] Snapshot()=>_states.Values.OrderBy(s=>s.Owner).ToArray();
    internal static void Restore(FrenzySpeedState[] saved)
    {
        if(saved==null||saved.Length>1000000){throw new InvalidDataException("Missing Frenzy states");}
        var next=ImmutableDictionary<uint,FrenzySpeedState>.Empty;
        foreach(var s in saved)
        {
            if(s==null||!((Serial)s.Owner).IsMobile||s.Rank is <1 or >255||s.Charges<1||s.Charges>s.Rank
                ||s.UntilUtc.Kind!=DateTimeKind.Utc||next.ContainsKey(s.Owner))
            {throw new InvalidDataException("Invalid Frenzy speed state");}
            next=next.Add(s.Owner,s);
        }
        _states=next;
    }
}
