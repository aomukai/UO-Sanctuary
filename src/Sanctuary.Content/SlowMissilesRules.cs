// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record SlowMissilesNativeTarget(string Type,bool Boss,bool Npc,bool NoAura,bool Attackable);
// Explicit Sanctuary classifications, never aliases to invented D2 species.
// The complete target/producer/order proposal must be approved before learned
// admission. Existing prepared callers retain their original strict filters.
internal static class SlowMissilesRules
{
    internal static bool Approved=>false;
    private static readonly Dictionary<string,SlowMissilesNativeTarget> Native=new(StringComparer.Ordinal);
    private static readonly HashSet<string> Warned=new(StringComparer.Ordinal);
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SlowMissilesRules));
    public static void Configure()
    {
        foreach(var r in BalanceCatalog.Read<SlowMissilesNativeTarget>("slow-missiles-native-targets"))
        {if(r==null||r.Type!="Sanctuary.GraveyardLich"||!Native.TryAdd(r.Type,r)){throw new InvalidDataException("Invalid explicit Slow Missiles native target");}}
        if(Native.Count!=1){throw new InvalidDataException("Missing explicit native Slow Missiles classification");}
    }
    internal static bool NativeEligible(BaseCreature target)
    {
        if(target is not{Alive:true,Deleted:false}||!target.CanBeDamaged()||CreatureProfiles.For(target).Traits.Boss==true){return false;}
        for(var type=target.GetType();type!=null;type=type.BaseType)
        {if(Native.TryGetValue(type.FullName,out var row)){return !row.Boss&&!row.Npc&&!row.NoAura&&row.Attackable;}}
        string name=target.GetType().FullName;
        if(Warned.Add(name)){Log.Warning("Slow Missiles native target classification is unresolved: {Type}",name);}return false;
    }
}
