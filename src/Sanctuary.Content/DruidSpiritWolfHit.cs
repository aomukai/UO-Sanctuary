// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal enum SpiritWolfHitOutcome{Invalid,Miss,Blocked,Hit,Unsupported}
internal sealed record SpiritWolfHitResult(SpiritWolfHitOutcome Outcome,uint Target,int Chance=0,
    int Cold256=0,int Fire256=0,int Lightning256=0,int ColdFrames=0,bool Critical=false,int LifeLost=0,string Reason=null);
internal readonly record struct SpiritWolfPacket(int Cold256,int Fire256,int Lightning256,int ColdFrames,bool Critical);

// Current copied wolf stats have only cold damage. The older ordinary-melee
// physical1..2floor is deliberately not inferred as current D2R behavior.
// This explicit cold-only profile remains provisional at that narrow branch.
internal static class DruidSpiritWolfHit
{
    internal static SpiritWolfPacket Packet(int coldMin,int coldMax,int coldFrames,int fireMin,int fireMax,
        int lightningMin,int lightningMax,int mastery,int critical,Func<int,int> limited,Func<uint> raw)
    {
        ArgumentNullException.ThrowIfNull(limited);ArgumentNullException.ThrowIfNull(raw);
        if(coldMin<0 || coldMax<coldMin || coldFrames<0 || fireMin<0 || fireMax<fireMin || lightningMin<0
            || lightningMax<lightningMin || critical is <0 or >100)
        {throw new ArgumentException("Invalid Spirit Wolf typed packet");}
        int Roll(int min,int max,int bonus=0)=>SourceElementRoll.Roll256(min,max,bonus,bonus,0,limited);
        // SUNITDMG_FillDamageValues visits fire, lightning, then cold. Each
        // upper bound is exclusive; intrinsic monster critical doubles all
        // rolled damage channels without changing the cold length.
        int fire=Roll(fireMin,fireMax),lightning=Roll(lightningMin,lightningMax),cold=Roll(coldMin,coldMax,mastery);
        bool doubled=critical>0 && raw()%100<critical;
        return new(checked(cold*(doubled?2:1)),checked(fire*(doubled?2:1)),checked(lightning*(doubled?2:1)),coldFrames,doubled);
    }
    internal static bool InRange(DruidSpiritWolfBody body,SpiritWolfWorldState state,BaseCreature target)
    {
        int distance=DruidWolfGeometry.MeleeDistance(DruidSpiritWolfWorld.Cell(state),PetOwnerRoutes.Cell(target),2,CreatureProfiles.For(target).Traits.Size.Value);
        // Ordinary monster GetResultFlags uses range bonus3, distinct from
        // the AI's melee bonus0. A moving target may still be reached at impact.
        return distance<=0 || distance<=5 && body.InLOS(target);
    }
    internal static int Chance(DruidSpiritWolfBody body,SpiritWolfWorldState state,BaseCreature target)
    {
        var birth=body.Prepared.Snapshot.Birth;
        var packet=state.AttackPacket??throw new InvalidOperationException("Spirit Wolf hit needs captured A1mode stats");
        int percent=(SorceressEffects.Enchantment(body)?.AttackPercent??0)+CombatAuras.AttackPercent(body)+NecromancerEffects.AttackStatPercent(body);
        // Source monster Dexterity starts at zero here. SkeletonGuardian's
        // native20Dex and the owner's item/mastery/species AR are not inherited.
        return AssassinTrapAttack.Chance(birth.Level,checked(birth.BaseAttack+birth.AttackBonus+packet.Attack),0,percent,
            ClassStats.Level(target),ClassStats.Defense(target));
    }
    private static (int Min,int Max) Aura(DruidSpiritWolfBody body,CombatAbility ability)
    {
        var aura=PaladinAuras.Effect(body,ability);
        return aura==null?(0,0):(NamedElements.To256(aura.WeaponMin),NamedElements.To256(aura.WeaponMax));
    }
    internal static SpiritWolfHitResult Resolve(DruidSpiritWolfBody body,PlayerMobile owner,SpiritWolfWorldState state,
        BaseCreature target,ref ulong seed)
    {
        uint id=target?.Serial.Value??0;
        if(!DruidSpiritWolfWorld.Eligible(body,owner,target) || !InRange(body,state,target) || !AlliedOverlap.Allowed(body))
        {return new(SpiritWolfHitOutcome.Invalid,id);}
        if(state.AttackPacket==null)
        {return new(SpiritWolfHitOutcome.Unsupported,id,Reason:"Spirit Wolf A1mode stats were not captured");}
        if(!SourceBlocking.TryRate(target,out _,out _))
        {return new(SpiritWolfHitOutcome.Unsupported,id,Reason:"Spirit Wolf recipient block supplier is not mapped");}
        body.DoHarmful(target,true);
        using var attempted=PaladinThorns.Attempt(body,target,false);
        SorceressEffects.MeleeAttempt(body,target);
        if(body.Deleted || !body.Alive || target.Deleted || !target.Alive){return new(SpiritWolfHitOutcome.Invalid,id);}
        int chance=Chance(body,state,target);
        if(!AssassinTrapAttack.Hit(chance,SourceRandom.Roll(ref seed)))
        {return new(SpiritWolfHitOutcome.Miss,id,chance);}
        // Ordinary melee blocks before typed damage is filled, irrespective
        // of whether its later packet has a physical component. The shared
        // block service owns its independent defender RNG/presentation.
        if(!SourceBlocking.TryBlock(target,true,out bool blocked))
        {return new(SpiritWolfHitOutcome.Unsupported,id,chance,Reason:"Spirit Wolf recipient block supplier changed");}
        if(blocked){return new(SpiritWolfHitOutcome.Blocked,id,chance);}
        var birth=body.Prepared.Snapshot.Birth;var enchant=SorceressEffects.Enchantment(body);
        var fire=Aura(body,CombatAbility.HolyFire);var ice=Aura(body,CombatAbility.HolyFreeze);var lightning=Aura(body,CombatAbility.HolyShock);
        ulong current=seed;int Limited(int bound)=>(int)SourceRandom.Limited(ref current,bound);uint Raw()=>SourceRandom.Roll(ref current);
        var packet=Packet(checked(birth.ColdMinimum*256+ice.Min),checked(birth.ColdMaximum*256+ice.Max),birth.ColdFrames,
            checked((enchant?.Min256??0)+fire.Min),checked((enchant?.Max256??0)+fire.Max),lightning.Min,lightning.Max,0,
            NecromancerSummons.Source("spiritwolf","Crit"),Limited,Raw);
        seed=current;
        var parts=SkillCombat.MitigatePrepared(body,target,new(0,packet.Fire256/256.0,packet.Cold256/256.0,packet.Lightning256/256.0,0));
        // Pure cold still makes an ordinary melee attempt (flat Thorns may
        // apply), but cannot supply physical leech, Life Tap or percent return.
        PreparedWeaponEvents.BeforeReturn(body,target,0,false);
        if(packet.Cold256>0){ColdStates.Apply(body,target,packet.ColdFrames,Core.TickCount);}
        int loss=SkillHealth.Damage(body,target,NamedElements.To256(parts.Total));
        CreatureHitRecovery.Apply(target,body,loss);CombatDamage.Finish(target,body);
        return new(SpiritWolfHitOutcome.Hit,id,chance,packet.Cold256,packet.Fire256,packet.Lightning256,packet.ColdFrames,packet.Critical,loss);
    }
}
