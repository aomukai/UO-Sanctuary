// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

// Prepared do6 release component. Public input/availability stays gated until
// the remaining native hostile projectile producers consume the shared state.
internal static class AmazonSlowMissilesCast
{
    internal static bool SourceAllowed(bool eligible,bool speciesBoss)=>eligible&&!speciesBoss;
    internal static bool Target(PlayerMobile owner,BaseCreature target,int radius)
    {
        if(owner==null || target==null || target.Map!=owner.Map
            || !ClassCombat.IsHostile(owner,target) || !SourceAuraTargets.Eligible(target)) { return false; }
        // Reuse the existing source species table, never elite/champion grade.
        // EncounterCreature retains its explicit native archetype adapter;
        // an unclassified native species does not acquire a guessed D2 identity.
        var source=PaladinConversion.SourceRule(target);
        if(target is not EncounterCreature && source==null) { return false; }
        return SourceAllowed(true,source?.Boss==true)
            && AmazonInnerSight.InRadius(target.X-owner.X,target.Y-owner.Y,radius)
            && Math.Abs(target.Z-owner.Z)<16 && !SpellHelper.IsTown(target.Location,target);
        // Filter 0xC583 has no optional LOS bit 0x200. Do not use Targets(),
        // which would add a line-of-sight condition absent from this search.
    }
    internal static bool Commit(PlayerMobile owner,DateTime at)
    {
        if(owner is not {Alive:true,Deleted:false,Frozen:false,Paralyzed:false}
            || owner.Map==null || owner.Map==Map.Internal || at.Kind!=DateTimeKind.Utc
            || !AlliedOverlap.Allowed(owner) || SpellHelper.IsTown(owner.Location,owner)
            || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Amazon
            || ClassCreation.Profiles.GetRank(owner.Serial,"amazon.slowmissiles")<1
            || SkillMechanics.Rank(owner,CombatAbility.SlowMissiles) is <1 or >255) { return false; }
        var row=SkillMechanics.For(CombatAbility.SlowMissiles);
        if(row.Number("srvdofunc")!=6 || row.Number("aurafilter")!=50563
            || row.Text("auratargetstate")!="slowmissiles" || row.Text("anim")!="SC")
        { throw new NotSupportedException("Unsupported Slow Missiles cast route"); }
        var values=SkillMechanics.Values(owner,CombatAbility.SlowMissiles);
        if(!NecromancerEffects.Pay(owner,values.Mana256)) { return false; }
        int radius=(int)values.Eval("aurarangecalc");var targets=new List<BaseCreature>();
        foreach(var target in owner.Map.GetMobilesInRange<BaseCreature>(owner.Location,(int)Math.Ceiling(SkillGeometry.Tiles(radius))))
        { if(Target(owner,target,radius)) { targets.Add(target); } }
        targets.Sort((a,b)=>a.Serial.CompareTo(b.Serial));
        foreach(var target in targets) { AmazonSlowMissiles.Apply(owner,target,values.Rank,at); }
        owner.FixedEffect(0x375A,10,10,0x481,0); // Identified cast_slowmissiles placeholder.
        return true; // Empty searches and refused weaker states still pay once.
    }
}
