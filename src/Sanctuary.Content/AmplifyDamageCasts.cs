// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary;

internal static class AmplifyDamageCasts
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AmplifyDamageCasts));
    private static readonly HashSet<string> NoWalk=new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<Type> Unmapped=new();
    public static void Configure()
    {
        foreach(string source in BalanceCatalog.Read<string>("source-curse-no-walk"))
        {if(string.IsNullOrWhiteSpace(source)||!NoWalk.Add(source)){throw new InvalidOperationException("Invalid curse walk-mode exclusions");}}
    }
    internal static bool WalkMode(string source)=>source!=null&&!NoWalk.Contains(source);
    internal static bool Eligible(BaseCreature target)
    {
        if(target is not{Alive:true,Deleted:false}||target is BaseVendor){return false;}
        // Native encounter profiles explicitly model champion traits. They
        // remain an encounter adapter, not a certified D2 species identity.
        if(target is EncounterCreature encounter)
        {return !(encounter.Grade==EncounterGrade.Champion&&encounter.Champion==ChampionKind.Possessed)&&target.CanBeDamaged();}
        string source=CreatureProfiles.Definition(target).Reference;
        if(SourceAuraTargets.Rule(source) is not{} rule)
        {if(Unmapped.Add(target.GetType())){Log.Warning("Amplify Damage recipient lacks source species rules: {Type}",target.GetType().Name);}return false;}
        // Do30/filter3 does not impose Inner Sight's noAura restriction. Its
        // receiver instead requires walk-mode bit2, attackability and no NPC.
        return !rule.Npc&&rule.Attackable&&WalkMode(source);
    }
    internal static int Penalty(int baseResistance,bool monsterImmunity)=>monsterImmunity&&baseResistance>=100?-20:-100;
    internal static NecromancerCurse Replacement(NecromancerCurse old,uint owner,int rank,int penalty,DateTime until,DateTime at,int x,int y)
    {
        if(old is{Ability:CombatAbility.Attract}&&old.UntilUtc>at){return null;}
        if(old is{Ability:CombatAbility.AmplifyDamage}&&old.UntilUtc>at)
        {if(rank<old.Rank){return null;}if(rank==old.Rank){return old with{UntilUtc=until};}}
        return new(CombatAbility.AmplifyDamage,owner,rank,until,x,y,PhysicalDelta:penalty);
    }
    internal static bool Apply(PlayerMobile owner,Mobile target,int rank)
    {
        if(rank is<1 or>255||!ClassCombat.IsHostile(owner,target)||target is not BaseCreature creature||!Eligible(creature)){return false;}
        // The earlier monster-cast Amplify store has no rank/creator payload.
        // Its compound replacement cannot yet be mapped without inventing one.
        if(EliteAuras.Curses.Active(target.Serial,TimedEffects.Now))
        {Log.Warning("Amplify Damage legacy elite-curse replacement is not mapped: {Target}",target.Serial.Value);return false;}
        EliteAuras.Refresh(target); // Remove an expired native resistance mod before reading its base pool.
        var values=SkillMechanics.Values(owner,CombatAbility.AmplifyDamage,rank);
        int frames=(int)values.Eval("auralencalc");
        if(!SourceCurseDuration.Incoming(target,"amplifydamage",frames,out frames)){return false;}
        var state=NecromancerEffects.Profiles.Find(target)??new();DateTime at=TimedEffects.Now;
        // For current native hostile actors, PhysicalResistance is the base
        // pool: curse/skill mitigation does not mutate that property. Dynamic
        // source base/accrued item graphs remain a separately recorded gap.
        var next=Replacement(state.Curse,owner.Serial.Value,rank,Penalty(target.PhysicalResistance,target is not BaseHire),
            at.AddMilliseconds(frames*40L),at,owner.X,owner.Y);
        if(next==null){return false;}
        NecromancerEffects.Profiles.Store(target,state with{Curse=next});NecromancerEffects.CurseApplied(owner,target);return true;
    }
    internal static bool Release(PlayerMobile owner,Point3D center,SkillValues values)
    {
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        int radius=(int)values.Eval("aurarangecalc");var targets=new List<BaseCreature>();
        foreach(var target in owner.Map.GetMobilesInRange<BaseCreature>(center,(int)Math.Ceiling(SkillGeometry.Tiles(radius))))
        {
            // Reuse the existing half-subtile squared-distance mapping. The
            // source filter3 has no optional LOS flag between area recipients.
            if(AmazonInnerSight.InRadius(target.X-center.X,target.Y-center.Y,radius)&&Math.Abs(target.Z-center.Z)<16
                &&ClassCombat.IsHostile(owner,target)){targets.Add(target);}
        }
        targets.Sort((a,b)=>a.Serial.CompareTo(b.Serial));foreach(var target in targets){Apply(owner,target,values.Rank);}
        owner.PlaySound(0x1E5); // Identified generic cast-audio placeholder.
        return true; // A valid empty or resisted area still spends mana once.
    }
}
