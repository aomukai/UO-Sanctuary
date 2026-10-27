// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal enum DireWolfHitOutcome{Invalid,Miss,Blocked,Hit,Unsupported}
internal sealed record DireWolfHitResult(DireWolfHitOutcome Outcome,uint Target,int Chance=0,
    int Physical256=0,int Cold256=0,int Fire256=0,int Lightning256=0,bool Critical=false,int LifeLost=0,string Reason=null);
internal readonly record struct DireWolfPacket(int Physical256,int Fire256,int Cold256,int Lightning256,int ColdFrames,bool Critical);

internal static class DruidDireWolfHit
{
    // Ordinary unarmed nonplayer fill: source Strength is zero, not the
    // inherited native skeleton's20 nor the owner's attributes/equipment.
    // All damagepercent sources aggregate once before FP256 endpoints roll.
    internal static DireWolfPacket Packet(int minimum,int maximum,int percent,int fireMin,int fireMax,
        int lightningMin,int lightningMax,int coldMin,int coldMax,int coldFrames,int critical,Func<int,int> limited,Func<uint> raw)
    {
        ArgumentNullException.ThrowIfNull(limited);ArgumentNullException.ThrowIfNull(raw);
        if(minimum<0 || maximum<minimum || critical is <0 or >100){throw new ArgumentException("Invalid Fenris physical packet");}
        int low=checked(Math.Max(1,minimum)*256),high=checked(Math.Max(2,maximum)*256);
        if(high<=low){high=checked(low+256);}
        percent=Math.Max(-90,percent);
        low=checked(low+SourcePercentage.Calculate(low,percent,100));high=checked(high+SourcePercentage.Calculate(high,percent,100));
        int physical=Math.Max(0,checked(low+(high>low?limited(high-low):0)));
        // Reuse the already checked fire/lightning/cold order and exclusive
        // ranges, but defer intrinsic critical until physical is present too.
        var typed=DruidSpiritWolfHit.Packet(coldMin,coldMax,coldFrames,fireMin,fireMax,lightningMin,lightningMax,0,0,limited,raw);
        bool doubled=critical>0 && raw()%100<critical;int factor=doubled?2:1;
        return new(checked(physical*factor),checked(typed.Fire256*factor),checked(typed.Cold256*factor),checked(typed.Lightning256*factor),coldFrames,doubled);
    }
    internal static bool InRange(DruidDireWolfBody body,DireWolfWorldState state,BaseCreature target)
    {
        int distance=DruidWolfGeometry.MeleeDistance(DruidDireWolfWorld.Cell(state),PetOwnerRoutes.Cell(target),2,CreatureProfiles.For(target).Traits.Size.Value);
        return distance<=0 || distance<=5 && body.InLOS(target);
    }
    internal static int Chance(DruidDireWolfBody body,DireWolfWorldState state,BaseCreature target)
    {
        var birth=body.Prepared.Snapshot.Birth;var packet=state.AttackPacket??throw new InvalidOperationException("Fenris hit requires captured A1 stats");
        int percent=(SorceressEffects.Enchantment(body)?.AttackPercent??0)+CombatAuras.AttackPercent(body)+NecromancerEffects.AttackStatPercent(body);
        return AssassinTrapAttack.Chance(birth.Level,checked(birth.BaseAttack+birth.AttackBonus+packet.Attack),0,percent,ClassStats.Level(target),ClassStats.Defense(target));
    }
    internal static int DamagePercent(DruidDireWolfBody body,DireWolfWorldState state)=>checked(body.Prepared.Snapshot.Birth.DamagePercent
        +(state.RageUntil>state.Frame?state.RagePercent:0)+CombatAuras.DamagePercent(body)+NecromancerEffects.DamageStatPercent(body));
    private static (int Min,int Max) Aura(DruidDireWolfBody body,CombatAbility ability)
    {var aura=PaladinAuras.Effect(body,ability);return aura==null?(0,0):(NamedElements.To256(aura.WeaponMin),NamedElements.To256(aura.WeaponMax));}
    internal static DireWolfHitResult Resolve(DruidDireWolfBody body,PlayerMobile owner,DireWolfWorldState state,BaseCreature target,ref ulong seed)
    {
        uint id=target?.Serial.Value??0;
        if(!DruidDireWolfWorld.Eligible(body,owner,target) || !InRange(body,state,target) || !AlliedOverlap.Allowed(body)){return new(DireWolfHitOutcome.Invalid,id);}
        if(state.AttackPacket is not{} attack){return new(DireWolfHitOutcome.Unsupported,id,Reason:"Fenris A1 stats were not captured");}
        if(!SourceBlocking.TryRate(target,out _,out _)){return new(DireWolfHitOutcome.Unsupported,id,Reason:"Fenris recipient block supplier is not mapped");}
        body.DoHarmful(target,true);using var attempted=PaladinThorns.Attempt(body,target,false);SorceressEffects.MeleeAttempt(body,target);
        if(body.Deleted || !body.Alive || target.Deleted || !target.Alive){return new(DireWolfHitOutcome.Invalid,id);}
        int chance=Chance(body,state,target);
        if(!AssassinTrapAttack.Hit(chance,SourceRandom.Roll(ref seed))){return new(DireWolfHitOutcome.Miss,id,chance);}
        if(!SourceBlocking.TryBlock(target,true,out bool blocked)){return new(DireWolfHitOutcome.Unsupported,id,chance,Reason:"Fenris block supplier changed");}
        if(blocked){return new(DireWolfHitOutcome.Blocked,id,chance);}
        var enchant=SorceressEffects.Enchantment(body);var fire=Aura(body,CombatAbility.HolyFire);var cold=Aura(body,CombatAbility.HolyFreeze);var lightning=Aura(body,CombatAbility.HolyShock);
        ulong current=seed;int Limited(int bound)=>(int)SourceRandom.Limited(ref current,bound);uint Raw()=>SourceRandom.Roll(ref current);
        var packet=Packet(attack.Minimum,attack.Maximum,DamagePercent(body,state),checked((enchant?.Min256??0)+fire.Min),checked((enchant?.Max256??0)+fire.Max),
            lightning.Min,lightning.Max,cold.Min,cold.Max,0,NecromancerSummons.Source("fenris","Crit"),Limited,Raw);seed=current;
        var parts=SkillCombat.MitigatePrepared(body,target,new(packet.Physical256/256.0,packet.Fire256/256.0,packet.Cold256/256.0,packet.Lightning256/256.0,0),out int absorbed);
        PreparedWeaponEvents.BeforeReturn(body,target,NamedElements.To256(parts.Physical),false,absorbedLife256:absorbed);
        int loss=SkillHealth.Damage(body,target,NamedElements.To256(parts.Total));CreatureHitRecovery.Apply(target,body,loss,true);CombatDamage.Finish(target,body);
        return new(DireWolfHitOutcome.Hit,id,chance,packet.Physical256,packet.Cold256,packet.Fire256,packet.Lightning256,packet.Critical,loss);
    }
}
