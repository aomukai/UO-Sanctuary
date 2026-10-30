using System;
using System.Collections.Generic;
using System.IO;

namespace Sanctuary;

// Native mobile life is integral. Opt-in summons retain their source maximum
// in FP256 and use the ceiling only for the native display/storage field.
internal interface ISourceLifeMaximum
{
    int MaximumLife256 { get; }
}

public sealed class SkillHealthProfiles : GenericPersistence
{
    internal readonly Dictionary<Serial, (int Fraction, int ObservedHits)> Remainders = new();
    public SkillHealthProfiles() : base("SanctuarySkillHealth", 114) { }
    public override void Serialize(IGenericWriter writer)
    {
        Prune(); writer.Write(0); writer.Write(Remainders.Count);
        foreach (var (serial, state) in Remainders) { writer.Write(serial.Value); writer.Write(state.Fraction); writer.Write(state.ObservedHits); }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown skill-health version"); }
        int count = reader.ReadInt(); if (count is < 0 or > 1000000) { throw new InvalidDataException("Invalid skill-health count"); }
        Remainders.Clear();
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt(); int fraction = reader.ReadInt(), hits = reader.ReadInt();
            if (!serial.IsMobile || fraction is < 0 or > 255 || hits < 0 || !Remainders.TryAdd(serial, (fraction, hits)))
            { throw new InvalidDataException("Invalid skill-health state"); }
        }
    }
    public void Prune()
    {
        var remove = new List<Serial>();
        foreach (var serial in Remainders.Keys) { if (World.FindMobile(serial) is not { Alive: true, Deleted: false }) { remove.Add(serial); } }
        foreach (var serial in remove) { Remainders.Remove(serial); }
    }
    public override void PostDeserialize() => Prune();
}

public static class SkillHealth
{
    private static Mobile _selfCost;
    internal static bool IsSelfCost(Mobile source,Mobile target)=>_selfCost!=null && source==target && target==_selfCost;
    internal static int PayLife(Mobile owner,int damage256)
    {
        var previous=_selfCost;
        try{_selfCost=owner;return Damage(owner,owner,damage256);}
        finally{_selfCost=previous;}
    }
    public static SkillHealthProfiles Profiles { get; private set; }
    public static void Configure() { Profiles = new(); EventSink.WorldSave += Profiles.Prune; Server.Mobiles.ClassCombatHooks.ApplyDamage256=Damage; }
    public static int Remainder256(Mobile target) => Profiles.Remainders.GetValueOrDefault(target.Serial).Fraction;
    public static void Clear(Mobile target) => Profiles.Remainders.Remove(target.Serial);
    internal static int? SourceMaximum256(Mobile target)=>target is ISourceLifeMaximum source?source.MaximumLife256
        :target is Server.Mobiles.BaseCreature creature?ReviveLife.Maximum256(creature):null;
    internal static int Maximum256(Mobile target)=>PaladinConversion.Maximum256(target)??(target is AmazonDecoy decoy?decoy.Maximum256:SourceMaximum256(target)??target.HitsMax*256);
    internal static bool Full(Mobile target)=>Balance256(target)>=Maximum256(target);
    internal static int Balance256(Mobile target)
    {
        var old=Profiles.Remainders.GetValueOrDefault(target.Serial);
        int fraction=target.Hits>old.ObservedHits && target.Hits>=target.HitsMax?0:old.Fraction;
        return target.Hits*256-fraction;
    }
    internal static void SetBalance256(Mobile target,int amount256)
    {
        amount256=Math.Clamp(amount256,0,Maximum256(target));int displayed=(amount256+255)/256;
        target.Hits=displayed;int remainder=displayed*256-amount256;
        if(remainder==0){Clear(target);}else{Profiles.Remainders[target.Serial]=(remainder,displayed);}
    }
    public static int Heal(Mobile target, int healing256)
    {
        if (healing256<=0 || target is not { Alive:true,Deleted:false } || target.IsDeadBondedPet) { return 0; }
        var old=Profiles.Remainders.GetValueOrDefault(target.Serial);
        int fraction=target.Hits>old.ObservedHits && target.Hits>=target.HitsMax ? 0 : old.Fraction;
        long before=(long)target.Hits*256-fraction;
        long after=Math.Min(Maximum256(target),before+healing256);
        int displayed=(int)((after+255)/256);target.Hits=displayed;
        int remaining=(int)(displayed*256L-after);
        if (remaining==0) { Clear(target); } else { Profiles.Remainders[target.Serial]=(remaining,displayed); }
        return (int)Math.Max(0,after-before);
    }
    public static int Damage(Mobile source, Mobile target, int damage256)
    {
        if (damage256 <= 0 || target is not { Alive: true, Deleted: false } || !target.CanBeDamaged()) { return 0; }
        var old = Profiles.Remainders.GetValueOrDefault(target.Serial);
        int fraction = target.Hits > old.ObservedHits && target.Hits >= target.HitsMax ? 0 : old.Fraction;
        long total = (long)fraction + damage256;
        int before = target.Hits;
        bool lethal=(long)before*256-total<256;
        int whole=lethal?before:(int)Math.Min(total/256,int.MaxValue);
        if (whole>0) { target.Damage(whole, source); CombatDamage.Finish(target, source); }
        if (!lethal && target is { Alive: true, Deleted: false } && total % 256 != 0) { Profiles.Remainders[target.Serial] = ((int)(total % 256), target.Hits); }
        else { Clear(target); }
        return Math.Max(0, before - target.Hits);
    }
}
