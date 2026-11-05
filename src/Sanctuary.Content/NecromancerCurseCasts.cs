// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary;

internal static class NecromancerCurseCasts
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NecromancerCurseCasts));
    private static readonly HashSet<string> NoWalk=new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<Type> Unmapped=new();
    private static readonly HashSet<string> UnmappedAi=new();
    internal static bool Handles(CombatAbility ability)=>ability is CombatAbility.AmplifyDamage or CombatAbility.Weaken or CombatAbility.Decrepify or CombatAbility.LowerResist or CombatAbility.IronMaiden or CombatAbility.LifeTap or CombatAbility.DimVision or CombatAbility.Terror or CombatAbility.Confuse;
    public static void Configure()
    {
        foreach(string source in BalanceCatalog.Read<string>("source-curse-no-walk"))
        {if(string.IsNullOrWhiteSpace(source)||!NoWalk.Add(source)){throw new InvalidOperationException("Invalid curse walk-mode exclusions");}}
    }
    internal static bool WalkMode(string source)=>source!=null&&!NoWalk.Contains(source);
    internal static bool AiEligible(BaseCreature target)
    {
        // Enemy-owned summons/PvP and native bard AI coexistence have no
        // verified source-state adapter. Keep those cases explicitly bounded.
        if(target is{Controlled:true} or{Summoned:true} || target is BaseHire || target.BardProvoked || SourceMind.Neutral(target)){return false;}
        if(target is EncounterCreature encounter)
        {return encounter.Grade is not(EncounterGrade.Unique or EncounterGrade.SuperUnique);}
        var row=PaladinConversion.SourceRule(target);
        if(row==null || row.Base=="imp1")
        {
            string key=target.GetType().Name+":"+(row?.Base??"unmapped");
            if(UnmappedAi.Add(key)){Log.Warning("AI curse eligibility is unsupported: {Target}",key);}
            return false;
        }
        return row.Evil&&row.SwitchAi&&row.Walk&&!row.Boss&&!row.Npc&&row.Attackable;
    }
    internal static int AiFrames(int frames,MonsterDifficulty difficulty)=>difficulty switch
    {MonsterDifficulty.Normal=>frames,MonsterDifficulty.Nightmare=>frames/2,MonsterDifficulty.Hell=>frames/4,
        _=>throw new NotSupportedException("Unknown AI curse facet difficulty")};
    internal static bool Eligible(BaseCreature target)
    {
        if(target is not{Alive:true,Deleted:false}||target is BaseVendor){return false;}
        // Native encounter profiles explicitly model champion traits. They
        // remain an encounter adapter, not a certified D2 species identity.
        if(target is EncounterCreature encounter)
        {return !(encounter.Grade==EncounterGrade.Champion&&encounter.Champion==ChampionKind.Possessed)&&target.CanBeDamaged();}
        string source=CreatureProfiles.Definition(target).Reference;
        if(SourceAuraTargets.Rule(source) is not{} rule)
        {if(Unmapped.Add(target.GetType())){Log.Warning("Necromancer curse recipient lacks source species rules: {Type}",target.GetType().Name);}return false;}
        // Do30/filter3 does not impose Inner Sight's noAura restriction. Its
        // receiver instead requires walk-mode bit2, attackability and no NPC.
        return !rule.Npc&&rule.Attackable&&WalkMode(source);
    }
    internal static NecromancerCurse Replacement(NecromancerCurse old,CombatAbility ability,uint owner,int rank,int? penalty,DateTime until,DateTime at,int x,int y,LowerResistDeltas elements=null)
    {
        if(old is{Ability:CombatAbility.Attract}&&old.UntilUtc>at){return null;}
        if(old!=null&&old.Ability==ability&&old.UntilUtc>at)
        {
            if(rank<old.Rank){return null;}
            // Decrepify's physical resistance is slot3: the callback rewrites
            // secondary stats even when the factory retains the old creator.
            if(rank==old.Rank){return old with{UntilUtc=until,PhysicalDelta=ability==CombatAbility.Decrepify?penalty:old.PhysicalDelta,
                // Fire is the primary stat. An old absent value remains unknown;
                // only the three secondary slots are written by the callback.
                ElementDeltas=ability==CombatAbility.LowerResist?elements with{Fire=old.ElementDeltas?.Fire}:old.ElementDeltas};}
        }
        return new(ability,owner,rank,until,x,y,PhysicalDelta:penalty,ElementDeltas:elements);
    }
    internal static int PhysicalPenalty(int full,int baseResistance,bool monsterImmunity)=>monsterImmunity&&baseResistance>=100?full/5:full;
    internal static LowerResistDeltas ElementPenalties(int full,int fire,int lightning,int cold,int poison,bool monsterImmunity)
        =>new(PhysicalPenalty(full,fire,monsterImmunity),PhysicalPenalty(full,lightning,monsterImmunity),
            PhysicalPenalty(full,cold,monsterImmunity),PhysicalPenalty(full,poison,monsterImmunity));
    internal static bool Apply(PlayerMobile owner,Mobile target,CombatAbility ability,int rank)
        =>Handles(ability)&&rank is>=1 and<=255&&Apply(owner,owner,target,ability,rank,SkillMechanics.Values(owner,ability,rank),TimedEffects.Now);
    private static bool Apply(Mobile caster,PlayerMobile owner,Mobile target,CombatAbility ability,int rank,SkillValues values,DateTime at,bool indirect=false)
    {
        if(!Handles(ability)||rank is<1 or>255||!ClassCombat.IsHostile(owner,target)||target is not BaseCreature creature||!Eligible(creature)){return false;}
        if(ability is CombatAbility.DimVision or CombatAbility.Terror or CombatAbility.Confuse && !AiEligible(creature)){return false;}
        if(ability==CombatAbility.Terror&&!SourceTerror.Supported(creature))
        {
            string key="Terror:"+creature.GetType().Name+":"+CreatureProfiles.Definition(creature).Reference;
            if(UnmappedAi.Add(key)){Log.Warning("Learned Terror AI transition is unsupported: {Target}",key);}
            return false;
        }
        // The earlier monster-cast Amplify store has no rank/creator payload.
        // Its compound replacement cannot yet be mapped without inventing one.
        if(EliteAuras.Curses.Unknown(target.Serial,TimedEffects.Now))
        {Log.Warning("{Ability} legacy elite-curse replacement is not mapped: {Target}",ability,target.Serial.Value);return false;}
        EliteAuras.Refresh(target); // Remove an expired native resistance mod before reading its base pool.
        int frames=(int)values.Eval("auralencalc");
        // Do30 uses game difficulty before the curse-resistance factory. In
        // Sanctuary that is the owner's configured facet, not the NPC's spawn.
        if(ability is CombatAbility.DimVision or CombatAbility.Terror or CombatAbility.Confuse){frames=AiFrames(frames,SummonDifficulties.For(owner.Map));}
        int aiFrames=frames; // AIRESET uses this duration before curse resistance.
        if(!SourceCurseDuration.Incoming(target,SkillMechanics.For(ability).Text("auratargetstate"),frames,out frames)){return false;}
        var state=NecromancerEffects.Profiles.Find(target)??new();
        // For current native hostile actors, PhysicalResistance is the base
        // pool: curse/skill mitigation does not mutate that property. Dynamic
        // source base/accrued item graphs remain a separately recorded gap.
        int? penalty=ability switch
        {
            CombatAbility.AmplifyDamage=>AmplifyDamageCasts.Penalty(EliteAuras.UnderlyingPhysical(target),target is not BaseHire),
            CombatAbility.Decrepify=>PhysicalPenalty((int)values.Eval("aurastatcalc3"),EliteAuras.UnderlyingPhysical(target),target is not BaseHire),
            _=>null
        };
        var next=Replacement(state.Curse??EliteAuras.Curses.Metadata(target.Serial),ability,caster.Serial.Value,rank,penalty,
            at.AddMilliseconds(frames*40L),at,caster.X,caster.Y,
            ability==CombatAbility.LowerResist?ElementPenalties(-values.Diminishing(5,6),
                target.FireResistance-EliteAuras.ConvictionOffset(target,"fire"),
                target.EnergyResistance-EliteAuras.ConvictionOffset(target,"ltng"),
                target.ColdResistance-EliteAuras.ConvictionOffset(target,"cold"),target.PoisonResistance,target is not BaseHire):null);
        if(next==null){return false;}
        if(ability==CombatAbility.DimVision){next=next with{Motion=NecromancerEffects.CaptureMotion(creature)};}
        // The source dispatcher reinitializes AI11 on every accepted factory
        // result, including an equal-rank refresh. Capture before Store/Reset
        // can retire the old control state; never substitute the curse caster.
        if(ability==CombatAbility.Terror){next=next with{Terror=SourceTerror.Learned(creature,rank,at)};}
        if(ability==CombatAbility.Confuse){state=state with{Mind=SourceMind.Confuse(creature,state,aiFrames,at)};}
        NecromancerEffects.Profiles.Store(target,state with{Curse=next});
        if(ability==CombatAbility.DimVision){SourceBlind.Attach(creature,state.Curse?.Ability!=CombatAbility.DimVision||state.Curse.UntilUtc<=at);}
        if(ability==CombatAbility.Terror){SourceTerror.Reset(creature);}
        NecromancerEffects.CurseApplied(caster,target,indirect);return true;
    }
    // Qualified native elite callback. Its existing caller owns proc RNG,
    // selection and geometry;only the shared receiver/group is bound here.
    internal static bool NpcAmplify(EncounterCreature caster,Mobile target,int rank)
    {
        if(caster is not{Alive:true,Deleted:false}||target is not{Alive:true,Deleted:false}||rank is<1 or>255
            ||target is EncounterCreature{Grade:EncounterGrade.Champion,Champion:ChampionKind.Possessed}){return false;}
        DateTime at=TimedEffects.Now;
        if(EliteAuras.Curses.Unknown(target.Serial,at)){Log.Warning("NPC Amplify unknown legacy replacement is unsupported:{Target}",target.Serial.Value);return false;}
        EliteAuras.Refresh(target);
        var row=SkillMechanics.For(CombatAbility.AmplifyDamage);
        var values=new SkillValues(row,k=>k==row.SourceKey?rank:0,_=>0,ClassStats.Level(caster));
        if(!SourceCurseDuration.Incoming(target,row.Text("auratargetstate"),(int)values.Eval("auralencalc"),out int frames)){return false;}
        var state=NecromancerEffects.Profiles.Find(target)??new();
        var next=Replacement(state.Curse??EliteAuras.Curses.Metadata(target.Serial),CombatAbility.AmplifyDamage,caster.Serial.Value,rank,
            AmplifyDamageCasts.Penalty(EliteAuras.UnderlyingPhysical(target),target is BaseCreature and not BaseHire),at.AddMilliseconds(frames*40L),at,caster.X,caster.Y);
        if(next==null){return false;}
        if(!EliteAuras.ValidMetadata(next)){Log.Warning("NPC Amplify retained primary/creator is unsupported:{Target}",target.Serial.Value);return false;}
        EliteAuras.Curses.Apply(target,next);EliteAuras.Refresh(target);NecromancerEffects.CurseApplied(caster,target);return true;
    }
    private static void Area(Mobile caster,PlayerMobile owner,CombatAbility ability,Point3D center,SkillValues values,DateTime at,bool indirect=false)
    {
        int radius=(int)values.Eval("aurarangecalc");var targets=new List<BaseCreature>();
        foreach(var target in caster.Map.GetMobilesInRange<BaseCreature>(center,(int)Math.Ceiling(SkillGeometry.Tiles(radius))))
        {
            // Source filter3 has no optional recipient LOS gate. The real
            // caster supplies origin/identity; its owner supplies allegiance.
            if(AmazonInnerSight.InRadius(target.X-center.X,target.Y-center.Y,radius)&&Math.Abs(target.Z-center.Z)<16
                &&ClassCombat.IsHostile(owner,target)){targets.Add(target);}
        }
        targets.Sort((a,b)=>a.Serial.CompareTo(b.Serial));foreach(var target in targets){Apply(caster,owner,target,ability,values.Rank,values,at,indirect);}
    }
    internal static bool NpcLifeTap(SkeletonGuardian caster,BaseCreature target,int rank,DateTime at)
        =>NpcDo30(caster,target,CombatAbility.LifeTap,rank,at);
    internal static bool NpcDo30(SkeletonGuardian caster,BaseCreature target,CombatAbility ability,int rank,DateTime at)
        => target is not{Alive:true,Deleted:false} ? false : NpcDo30(caster,NpcItemTarget.Capture(target),ability,rank,at);
    internal static bool NpcDo30(SkeletonGuardian caster,NpcItemTarget target,CombatAbility ability,int rank,DateTime at)
    {
        if(caster is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Deleted:false} owner}
            ||target==null||caster.Map==null||caster.Map==Map.Internal||owner.Map!=caster.Map
            ||rank is<1 or>63||MonsterUninterruptibles.Active(caster)){return false;}
        var center=target.Point(caster.Map);
        var row=SkillMechanics.For(ability);_=ItemSkillExecution.RequireCurse(row.SourceId);
        var values=new SkillValues(row,key=>string.Equals(key,row.SourceKey,StringComparison.OrdinalIgnoreCase)?rank:0,_=>0,ClassStats.Level(caster));
        // Preserve the item-cast caller's combat target while retaining native
        // aggression/credit. Direct DoHarmful would retarget to each recipient.
        Area(caster,owner,ability,center,values,at,indirect:true);
        return true;
    }
    internal static bool Release(PlayerMobile owner,CombatAbility ability,Point3D center,SkillValues values)
    {
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        Area(owner,owner,ability,center,values,TimedEffects.Now);
        owner.PlaySound(0x1E5); // Identified generic cast-audio placeholder.
        return true; // A valid empty or resisted area still spends mana once.
    }
}
