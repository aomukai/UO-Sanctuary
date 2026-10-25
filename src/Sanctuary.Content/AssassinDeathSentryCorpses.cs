// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

internal sealed record DeathCorpseSelection(Corpse Corpse,int OriginalGuid,int Distance,uint[] UnsupportedCorpses);
internal enum DeathExplosionStatus { Success,NotEligible,MissingSnapshot,MissingSeed }
internal sealed record DeathExplosionResult(DeathExplosionStatus Status,int Targets=0,int WholeLoss=0,ulong? AdvancedSeed=null,CorpseExplosionPacket Packet=null);

internal static class AssassinDeathSentryCorpses
{
    internal static DeathExplosionResult Explode(AssassinSentryBody actor,Corpse corpse)
    {
        if(actor?.Prepared?.Creation is not{Ability:CombatAbility.DeathSentry} creation)
        {throw new InvalidOperationException("Corpse explosion requires a captured Death Sentry actor");}
        if(World.FindMobile((Serial)actor.Prepared.Owner) is not PlayerMobile owner){return new(DeathExplosionStatus.NotEligible);}
        return Explode(actor,owner,corpse,creation.Rank,creation.Level,creation.Passive("passive_fire_pierce"),target=>actor.TrapTarget(owner,target));
    }
    // Native sector/item-list order is an explicit projection of the source
    // room/unit lists. Keep first eligible order; never sort by distance or
    // prefilter the later AI104 remembered-corpse/explosion-distance checks.
    internal static DeathCorpseSelection Select(BaseCreature actor,PlayerMobile owner,BaseCreature enemy,int rank)
    {
        var unsupported=new List<uint>();var candidates=new List<SentryCorpseCandidate>();var bodies=new Dictionary<int,Corpse>();
        if(actor is not{Alive:true,Deleted:false} || enemy is not{Alive:true,Deleted:false} || actor.Map==null || actor.Map==Map.Internal || enemy.Map!=actor.Map || rank<1)
        {return new(null,0,0,Array.Empty<uint>());}
        foreach(var corpse in actor.Map.GetItemsInRange<Corpse>(enemy.Location,5))
        {
            if(!CorpseSkillRules.UsableAt(owner,corpse,actor,double.PositiveInfinity)){continue;}
            if(!CorpseSkillRules.TryBaseLife(corpse,out var life) || !((Serial)life.OriginalMonsterSerial).IsMobile)
            {unsupported.Add(corpse.Serial.Value);continue;}
            int guid=checked((int)life.OriginalMonsterSerial);
            candidates.Add(new(guid,corpse.X*2,corpse.Y*2));bodies.Add(guid,corpse);
        }
        var selected=AssassinSentryCorpses.Select(true,true,rank,true,enemy.X*2,enemy.Y*2,candidates);
        if(selected==null){return new(null,0,0,unsupported.ToArray());}
        return new(bodies[selected.Guid],selected.Guid,SkillGeometry.SourceDistance(selected.X-enemy.X*2,selected.Y-enemy.Y*2),unsupported.ToArray());
    }

    // NPC do55 mechanics using an explicitly supplied actor/level/pierce and
    // allegiance service. Learned placement and the AI/mode dispatcher remain
    // callers. No player actor is substituted for the real damage source.
    internal static DeathExplosionResult Explode(BaseCreature actor,PlayerMobile owner,Corpse corpse,int rank,int casterLevel,int firePierce,Func<BaseCreature,bool> eligible)
    {
        ArgumentNullException.ThrowIfNull(eligible);
        if(rank is <1 or >255 || casterLevel is <1 or >99){throw new NotSupportedException("Unsupported Death Sentry cast context");}
        if(!CorpseSkillRules.UsableAt(owner,corpse,actor,double.PositiveInfinity) || SpellHelper.IsTown(corpse.Location,actor))
        {return new(DeathExplosionStatus.NotEligible);}
        var row=CorpseSkillRules.Profiles.Find(corpse);
        if(row?.BaseLife is not{} life){return new(DeathExplosionStatus.MissingSnapshot);}
        if(life.Seed is not{} stream){return new(DeathExplosionStatus.MissingSeed);}
        var values=SentryNpcSkills.Values(312,rank,casterLevel,_=>0);
        if(SentryNpcSkills.For(312).Number("srvdofunc")!=55 || SentryNpcSkills.For(312).Text("EType")!="fire")
        {throw new NotSupportedException("Unsupported NPC corpse explosion producer");}
        var packet=CorpseExplosionPackets.Create(life.Base256,casterLevel,life.Level,checked((int)values.Eval("aurarangecalc")),
            checked((int)values.Eval("calc1")),checked((int)values.Eval("calc2")),checked((int)values.Eval("calc3")),bound=>(int)SourceRandom.Limited(ref stream,bound));
        // This synchronous main-loop reservation makes the shared corpse roll
        // and its seed advancement indivisible to other corpse consumers.
        CorpseSkillRules.Profiles.Store(row with{NoSelect=true,BaseLife=life with{Seed=stream}});
        var map=corpse.Map;var center=corpse.Location;NecromancerCombat.Consume(corpse);
        int targets=0,totalLoss=0,nativeRange=(packet.OuterRange+1)/2;
        long outerSquared=(long)packet.OuterRange*packet.OuterRange;
        var area=new List<BaseCreature>();foreach(var target in map.GetMobilesInRange<BaseCreature>(center,nativeRange)){area.Add(target);}
        foreach(var target in area)
        {
            int dx=(target.X-center.X)*2,dy=(target.Y-center.Y)*2;long squared=(long)dx*dx+(long)dy*dy;
            if(target==actor || target.Deleted || !target.Alive || target.Map!=map || Math.Abs(target.Z-center.Z)>=16
                || squared>outerSquared || SpellHelper.IsTown(target.Location,target) || !eligible(target)){continue;}
            var hit=packet.Recipient(checked((int)squared));actor.DoHarmful(target,true);
            // The source fire writer adds the supplied fire amount directly;
            // the caster's fire mastery does not multiply corpse base life.
            var physical=NamedElements.Resolve256(actor,target,hit.Physical256,"physical",enhance:false,pierceOverride:0);
            var fire=NamedElements.Resolve256(actor,target,hit.Fire256,"fire",enhance:false,pierceOverride:firePierce);
            if(fire.Absorbed256>0){SkillHealth.Heal(target,fire.Absorbed256);}
            int loss=SkillHealth.Damage(actor,target,checked(physical.Damage256+fire.Damage256));
            CreatureHitRecovery.Apply(target,actor,loss);CombatDamage.Finish(target,actor);targets++;totalLoss=checked(totalLoss+loss);
        }
        Effects.SendLocationEffect(center,map,0x36BD,15); // Identified explosion placeholder.
        return new(DeathExplosionStatus.Success,targets,totalLoss,stream,packet);
    }
}
