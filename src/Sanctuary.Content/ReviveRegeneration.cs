// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public sealed record ReviveRegenerationSource(string Source,int DamageRegen);
internal static class ReviveRegeneration
{
    private sealed class Clock { internal long Last;internal int Milliseconds; }
    private static readonly ConditionalWeakTable<BaseCreature,Clock> Clocks=new();
    private static readonly Dictionary<string,int> Rates=new(StringComparer.Ordinal);
    internal static void Load()
    {
        Rates.Clear();
        foreach(var row in BalanceCatalog.Read<ReviveRegenerationSource>("revive-regeneration"))
        {
            if(row.DamageRegen is <0 or >255||string.IsNullOrWhiteSpace(row.Source)||!Rates.TryAdd(row.Source,row.DamageRegen))
            {throw new InvalidDataException("Invalid Revive regeneration source row");}
        }
        if(Rates.Count!=751){throw new InvalidDataException("Incomplete Revive regeneration catalogue");}
    }
    internal static int Rate(string source)=>Rates.TryGetValue(source,out int rate)?rate:
        throw new NotSupportedException("This monster has no pinned regeneration rate.");
    internal static bool Handles(Mobile actor)=>actor is BaseCreature creature
        &&ReviveNativeActivation.State(creature)?.Setup.RegenerationRate!=null;
    internal static int PerFrame(int maximum256,int rate)
    {
        if(maximum256<=0||rate is <0 or >255){throw new ArgumentOutOfRangeException();}
        // The server MaxHP callback runs after Revive's positive-HP passive
        // increase. It truncates life to whole points before DamageRegen.
        return checked((int)((long)rate*(maximum256>>8)>>4));
    }
    internal static void Validate(ReviveNativeSetup setup)
    {
        if(setup.RegenerationRate is not{} rate){return;}
        if(setup.LifePlan==null||rate!=Rate(setup.LifePlan.Source))
        {throw new InvalidDataException("Revive regeneration does not match its pinned source");}
    }
    internal static void Recover(BaseCreature actor,bool active,long now)
    {
        var clock=Clocks.GetValue(actor,static _=>new Clock());
        if(ReviveNativeActivation.State(actor)?.Setup.RegenerationRate is not{} rate||!active
            ||actor.Poisoned||!TimedEffects.CanRegenerateHits(actor)||SkillHealth.Full(actor))
        {clock.Last=now;clock.Milliseconds=0;return;}
        if(clock.Last==0||now<clock.Last){clock.Last=now;clock.Milliseconds=0;return;}
        // Existing summon convention: do not turn offline or paused-sector
        // time into a large healing burst when the actor resumes.
        long elapsed=Math.Clamp(now-clock.Last,0,1000);clock.Last=now;
        long total=clock.Milliseconds+elapsed;int frames=(int)(total/40);clock.Milliseconds=(int)(total%40);
        if(frames==0){return;}
        long healing=(long)frames*PerFrame(SkillHealth.Maximum256(actor),rate);
        if(healing>0){SkillHealth.Heal(actor,(int)Math.Min(int.MaxValue,healing));}
        if(SkillHealth.Full(actor)){clock.Milliseconds=0;}
    }
}
