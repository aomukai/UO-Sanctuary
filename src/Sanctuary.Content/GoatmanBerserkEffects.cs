// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public sealed record GoatmanBerserkEffect(uint Actor,int Rank,int Flags,int DefenseOverride,DateTime? UntilUtc);
public sealed class GoatmanBerserkProfiles:GenericPersistence
{
    public GoatmanBerserkProfiles():base("SanctuaryGoatmanBerserk",186){}
    public override void Serialize(IGenericWriter writer)
    {writer.Write(0);writer.Write(JsonSerializer.Serialize(GoatmanBerserkEffects.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown NPC Berserk version");}
        GoatmanBerserkEffects.Restore(JsonSerializer.Deserialize<GoatmanBerserkEffect[]>(reader.ReadString()));
    }
}
// NPC418 self-state consumer. Eligibility/casting/mode scheduling belongs to
// the caller;this does not grant a skill or start a summon/world AI.
internal static class GoatmanBerserkEffects
{
    private static readonly Dictionary<uint,GoatmanBerserkEffect> States=new();
    internal static GoatmanBerserkProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(100),TimeSpan.FromMilliseconds(100),()=>Prune(TimedEffects.Now));
    private static bool Eligible(Mobile actor)=>actor is BaseCreature{Alive:true,Deleted:false};
    private static void Clock(DateTime at)
    {if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("NPC Berserk needs a UTC clock",nameof(at));}}
    internal static GoatmanBerserkEffect At(Mobile actor,DateTime at)
    {
        Clock(at);
        return Eligible(actor)&&States.TryGetValue(actor.Serial.Value,out var state)&&(!state.UntilUtc.HasValue||at<state.UntilUtc.Value)?state:null;
    }
    internal static int DefenseOverride(Mobile actor)=>At(actor,TimedEffects.Now)?.DefenseOverride??0;
    internal static GoatmanBerserkEffect Begin(BaseCreature actor,int rank,DateTime at)
    {
        Clock(at);if(!Eligible(actor)){throw new ArgumentException("NPC Berserk needs a living creature",nameof(actor));}
        var old=At(actor,at);var projected=GoatmanNpcMelee.StartBerserk(0,rank,old==null?null:new(old.Flags,0,old.Rank,0,old.DefenseOverride),true);
        var state=new GoatmanBerserkEffect(actor.Serial.Value,projected.Rank,projected.Flags,projected.ArmorOverride,at.AddMilliseconds(checked(projected.UntilFrame*40)));
        States[actor.Serial.Value]=state;return state;
    }
    internal static GoatmanBerserkEffect Impact(BaseCreature actor,int rank,DateTime at)
    {
        Clock(at);if(!Eligible(actor)){throw new ArgumentException("NPC Berserk needs a living creature",nameof(actor));}
        var old=At(actor,at);var projected=GoatmanNpcMelee.ImpactBerserk(rank,old==null?null:new(old.Flags,0,old.Rank,0,old.DefenseOverride),true);
        // Preserve the existing absolute expiry,including subframe precision.
        // A recreated TEMPONLY state has no timed expiry;the caller must send
        // its actual animation-mode transition/cancellation.
        var state=new GoatmanBerserkEffect(actor.Serial.Value,projected.Rank,projected.Flags,projected.ArmorOverride,old?.UntilUtc);
        States[actor.Serial.Value]=state;return state;
    }
    internal static void ModeChanged(Mobile actor,int previous,int next)
    {
        if(previous!=next&&actor!=null&&States.TryGetValue(actor.Serial.Value,out var state)&&(state.Flags&4)!=0){States.Remove(actor.Serial.Value);}
    }
    internal static void Clear(Mobile actor){if(actor!=null){States.Remove(actor.Serial.Value);}}
    private static void Prune(DateTime at)
    {
        foreach(var state in States.Values.ToArray())
        {if(!Eligible(World.FindMobile((Serial)state.Actor))||state.UntilUtc is{} until&&at>=until){States.Remove(state.Actor);}}
    }
    internal static GoatmanBerserkEffect[] Snapshot()
    {
        // GenericPersistence may call from a serialization worker. No pruning,
        // stat refresh,mode change or world mutation may occur here. TEMPONLY
        // is in-flight animation state and is deliberately not restored.
        var at=TimedEffects.Now;
        return States.Values.Where(s=>s.Flags==2&&s.UntilUtc is{} until&&until>at&&Eligible(World.FindMobile((Serial)s.Actor))).OrderBy(s=>s.Actor).ToArray();
    }
    internal static void Restore(GoatmanBerserkEffect[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing NPC Berserk states");}
        var next=new Dictionary<uint,GoatmanBerserkEffect>();
        foreach(var s in saved)
        {
            if(s==null||!((Serial)s.Actor).IsMobile||s.Rank is <1 or >255||s.Flags!=2||s.DefenseOverride!=-100
                ||s.UntilUtc is not{} until||until.Kind!=DateTimeKind.Utc||until==DateTime.MaxValue||!next.TryAdd(s.Actor,s))
            {throw new InvalidDataException("Invalid saved NPC Berserk state");}
        }
        States.Clear();foreach(var pair in next){States.Add(pair.Key,pair.Value);}
    }
}
