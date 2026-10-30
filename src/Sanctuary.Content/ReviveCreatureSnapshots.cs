// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server.Mobiles;
namespace Sanctuary;

// Immutable value payload; JSON retains independent profile dictionaries and
// value equality through save/reload. It never holds a native actor reference.
public sealed record ReviveCreatureSnapshot(int Version,string RuntimeType,string Assembly,int AiType,int FightMode,
    string Name,int Body,int Hue,int RawStrength,int RawDexterity,int RawIntelligence,int DamageMinimum,int DamageMaximum,
    string ActiveCombatJson,bool NativeAbilityStateCaptured=false);
internal static class ReviveCreatureSnapshots
{
    internal static ReviveCreatureSnapshot TryCapture(BaseCreature actor,string source,out string issue)
    {
        try{var result=Capture(actor,source);issue=null;return result;}
        catch(Exception e)when(e is InvalidDataException or NotSupportedException or JsonException or ArgumentException)
        {issue="Original creature reconstruction data is unavailable: "+e.GetType().Name;return null;}
    }
    internal static ReviveCreatureSnapshot Capture(BaseCreature actor,string source)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var profile=CreatureProfiles.For(actor);bool mapped=profile.Enabled&&profile.ApplyStats;
        // Snapshot() previews authored variant values. A death record must use
        // the existing active consumers,including their native fallback lane.
        var combat=new CreatureSnapshot(actor.GetType().FullName,MonsterVariants.Difficulty(actor),source,
            CreatureProfiles.Definition(actor).Status,CreatureProfiles.Level(actor),actor.HitsMax,
            CreatureProfiles.AttackRating(actor),CreatureProfiles.Defense(actor),
            new(actor.PhysicalResistance,mapped?profile.Resistances.Magic??0:0,actor.FireResistance,
                actor.EnergyResistance,actor.ColdResistance,actor.PoisonResistance),profile);
        int minimum=mapped?profile.Attack1.MinDamage??actor.DamageMin:actor.DamageMin;
        int maximum=mapped?profile.Attack1.MaxDamage??actor.DamageMax:actor.DamageMax;
        var snapshot=new ReviveCreatureSnapshot(0,actor.GetType().FullName,actor.GetType().Assembly.GetName().Name,
            (int)actor.AI,(int)actor.FightMode,actor.Name,actor.Body.BodyID,actor.Hue,actor.RawStr,actor.RawDex,actor.RawInt,
            minimum,maximum,JsonSerializer.Serialize(combat));
        Validate(snapshot,source);return snapshot;
    }
    internal static CreatureSnapshot Combat(ReviveCreatureSnapshot snapshot)
    {
        try{return JsonSerializer.Deserialize<CreatureSnapshot>(snapshot.ActiveCombatJson)??throw new InvalidDataException("Missing historical active combat profile");}
        catch(JsonException e){throw new InvalidDataException("Invalid historical active combat JSON",e);}
    }
    internal static void Validate(ReviveCreatureSnapshot snapshot,string source)
    {
        if(snapshot.Version!=0||string.IsNullOrWhiteSpace(snapshot.RuntimeType)||string.IsNullOrWhiteSpace(snapshot.Assembly)
            ||snapshot.Body<0||snapshot.Hue<0||snapshot.DamageMinimum< -1||snapshot.DamageMaximum< -1
            ||snapshot.DamageMinimum>=0&&snapshot.DamageMaximum<snapshot.DamageMinimum||string.IsNullOrWhiteSpace(snapshot.ActiveCombatJson)
            ||snapshot.NativeAbilityStateCaptured)
        {throw new InvalidDataException("Invalid or unsupported historical Revive creature descriptor");}
        var combat=Combat(snapshot);
        if(combat.Type!=snapshot.RuntimeType||string.IsNullOrWhiteSpace(source)||combat.Reference!=source
            ||!Enum.IsDefined(combat.Difficulty)||combat.Level<1||combat.Life<1||combat.AttackRating<0||combat.Defense<0
            ||combat.Resistances==null||combat.Profile==null)
        {throw new InvalidDataException("Historical Revive identity/combat descriptor disagrees with its corpse");}
        // NativeAbilityStateCaptured stays false until type-specific state and
        // special-ability restoration are actually implemented and verified.
        // Do not resolve or instantiate a saved type while loading a world.
    }
}
