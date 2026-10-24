// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public sealed record HolyFreezeSourceRule(string Source,bool PrimeEvil,int Normal,int Nightmare,int Hell);
public sealed record HolyFreezeLease(uint Target,uint Source,int Rank,int Velocity,int Attack,int Other,bool Shatter,DateTime UntilUtc);
public sealed class HolyFreezeProfiles : GenericPersistence
{
    public HolyFreezeProfiles():base("SanctuaryHolyFreeze",138){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(PaladinHolyFreeze.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Holy Freeze version");}
        PaladinHolyFreeze.Restore(JsonSerializer.Deserialize<HolyFreezeLease[]>(reader.ReadString()));
    }
}

public static class PaladinHolyFreeze
{
    private static readonly Dictionary<string,HolyFreezeSourceRule> _rules=new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<uint,HolyFreezeLease> _leases=new();
    private static readonly List<uint> _expired=new();
    public static HolyFreezeProfiles Profiles{get;private set;}
    internal static Func<int> RollPercent=static()=>Utility.Random(100);
    public static void Configure()=>Profiles=new();
    public static void Load()
    {
        _rules.Clear();foreach(var row in BalanceCatalog.Read<HolyFreezeSourceRule>("holy-freeze-source-rules")){_rules.Add(row.Source,row);}
    }
    public static void Initialize()=>Timer.StartTimer(TimeSpan.Zero,TimeSpan.FromMilliseconds(40),Prune);
    public static HolyFreezeLease Effect(Mobile target)=>target is{Deleted:false,Alive:true} && _leases.TryGetValue(target.Serial.Value,out var lease)
        && lease.UntilUtc>TimedEffects.Now?lease:null;
    public static int VelocitySlow(Mobile target)=>Effect(target)?.Velocity ?? 0;
    public static int AttackSlow(Mobile target)=>Effect(target)?.Attack ?? 0;
    // Recorded independently: native UO spell/cast/other animation modes need source-mode adapters.
    public static int OtherSlow(Mobile target)=>Effect(target)?.Other ?? 0;
    // Target slow expiry uses the generic removal callback, which does not clear independent SHATTER.
    // Modern corpse/death/chill state interactions still require comparison.
    public static bool Shattered(Mobile target)=>target!=null && _leases.TryGetValue(target.Serial.Value,out var lease)
        && lease.Shatter;
    internal static HolyFreezeSourceRule SourceRule(Mobile target)=>target is BaseCreature creature && _rules.TryGetValue(CreatureProfiles.Definition(creature).Reference ?? "",out var rule)?rule:null;
    public static bool Eligible(Mobile target,out int coldEffect)
    {
        coldEffect=0;
        if(target is not BaseCreature creature || !_rules.TryGetValue(CreatureProfiles.Definition(creature).Reference ?? "",out var rule)
            || rule.PrimeEvil){return false;}
        coldEffect=MonsterVariants.Difficulty(creature) switch{MonsterDifficulty.Nightmare=>rule.Nightmare,MonsterDifficulty.Hell=>rule.Hell,_=>rule.Normal};
        return coldEffect<0; // Cold immunity and Cannot Be Frozen are separate from this source gate.
    }
    internal static void Slow(PlayerMobile owner,Mobile target,SkillValues values,DateTime now)
    {
        if(!Eligible(target,out int coldEffect)){return;}
        int frames=Math.Max(5,(int)values.Eval("perdelay"));
        int Cap(string field)=>(int)Math.Clamp(-Math.Max(coldEffect,values.Eval(field)),0,100);
        _leases.TryGetValue(target.Serial.Value,out var previous);var old=Effect(target);
        if(old!=null && old.Rank>values.Rank){return;}
        uint source=old!=null && old.Rank==values.Rank?old.Source:owner.Serial.Value;
        _leases[target.Serial.Value]=new(target.Serial.Value,source,values.Rank,Cap("aurastatcalc1"),Cap("aurastatcalc2"),Cap("aurastatcalc3"),previous?.Shatter ?? false,now.AddMilliseconds((frames+1)*40));
    }
    internal static void RollShatter(Mobile target)
    {
        if(_leases.TryGetValue(target.Serial.Value,out var lease)){_leases[target.Serial.Value]=lease with{Shatter=RollPercent()<20};}
    }
    private static void Prune()
    {
        _expired.Clear();foreach(var (serial,lease) in _leases)
        {if(lease.UntilUtc<=TimedEffects.Now && !lease.Shatter || World.FindMobile((Serial)serial) is not{Deleted:false,Alive:true}){_expired.Add(serial);}}
        foreach(uint serial in _expired){_leases.Remove(serial);}
    }
    public static HolyFreezeLease[] Snapshot()=>new List<HolyFreezeLease>(_leases.Values).ToArray();
    public static void Restore(HolyFreezeLease[] leases)
    {
        _leases.Clear();foreach(var lease in leases ?? throw new InvalidDataException("Missing Holy Freeze state"))
        {
            if(lease.Rank<1 || lease.Velocity is <0 or >100 || lease.Attack is <0 or >100 || lease.Other is <0 or >100
                || lease.UntilUtc.Kind!=DateTimeKind.Utc || !_leases.TryAdd(lease.Target,lease)){throw new InvalidDataException("Invalid Holy Freeze state");}
        }
    }
}
