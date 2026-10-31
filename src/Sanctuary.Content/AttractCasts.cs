// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

// Do59 applies a selected-target curse and independent neighbor overrides.
// Its neighbors keep their existing curses; later arrivals get no override.
internal static class AttractCasts
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AttractCasts));
    private static bool Eligible(PlayerMobile owner,BaseCreature target)=>ClassCombat.IsHostile(owner,target)
        &&NecromancerCurseCasts.Eligible(target)&&NecromancerCurseCasts.AiEligible(target)
        &&SpellHelper.CheckTown(target.Location,target);
    internal static bool Apply(PlayerMobile owner,Mobile target,int rank)
    {
        if(rank is<1 or>255||target is not BaseCreature selected||!Eligible(owner,selected)){return false;}
        if(EliteAuras.Curses.Active(selected.Serial,TimedEffects.Now))
        {Log.Warning("Attract legacy elite-curse replacement is unsupported: {Target}",selected.Serial.Value);return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.Attract,rank);DateTime at=TimedEffects.Now;
        int resetFrames=NecromancerCurseCasts.AiFrames((int)values.Eval("auralencalc"),SummonDifficulties.For(owner.Map));
        if(!SourceCurseDuration.Incoming(selected,"attract",resetFrames,out int frames))
        {
            // Legacy do59 mutates alignment/targets before a failed factory,
            // potentially leaving orphaned neutral state. Current native curse
            // resistance has no verified implementation of that transition.
            Log.Warning("Attract rejected factory/alignment orphan branch is unsupported: {Target}",selected.Serial.Value);return false;
        }
        var state=NecromancerEffects.Profiles.Find(selected)??new();
        var next=NecromancerCurseCasts.Replacement(state.Curse,CombatAbility.Attract,owner.Serial.Value,rank,null,
            at.AddMilliseconds(frames*40L),at,owner.X,owner.Y);
        if(next==null){Log.Warning("Attract late factory failure transition is unsupported: {Target}",selected.Serial.Value);return false;}
        // Publishing the accepted curse models the selected unit's neutral
        // alignment before the synchronous neighbor scan. Late failure above
        // is an explicit unsupported branch, not invented orphan recovery.
        NecromancerEffects.Profiles.Store(selected,state with{Curse=next});
        int range=(int)values.Eval("aurarangecalc");var neighbors=new List<BaseCreature>();
        foreach(var actor in owner.Map.GetMobilesInRange<BaseCreature>(selected.Location,(int)Math.Ceiling(SkillGeometry.Tiles(range))))
        {
            if(actor!=selected&&AmazonInnerSight.InRadius(actor.X-selected.X,actor.Y-selected.Y,range)
                &&Math.Abs(actor.Z-selected.Z)<16&&Eligible(owner,actor)){neighbors.Add(actor);}
        }
        neighbors.Sort((a,b)=>a.Serial.CompareTo(b.Serial));
        foreach(var actor in neighbors)
        {
            var current=NecromancerEffects.Profiles.Find(actor)??new();var curse=current.Curse;
            // Do59 writes the same generic target slot used by Terror/Taunt.
            if(curse?.Terror is{} terror){curse=curse with{Terror=terror with{Override=0}};}
            NecromancerEffects.Profiles.Store(actor,current with{Curse=curse,Mind=SourceMind.Attract(actor,current,selected,resetFrames,at)});
            actor.AIObject?.AITimer.Prod();
        }
        NecromancerEffects.CurseApplied(owner,selected);return true;
    }
    internal static bool Release(PlayerMobile owner,Mobile target,SkillValues values)
    {
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        owner.PlaySound(0x1E5); // Identified generic cast-audio placeholder.
        return Apply(owner,target,values.Rank);
    }
}
