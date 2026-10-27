// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal enum GrizzlyHitOutcome{Invalid,Miss,Blocked,Hit,Unsupported}
internal sealed record GrizzlyHitResult(GrizzlyHitOutcome Outcome,uint Target,int Chance=0,int Physical256=0,int Total256=0,
    int Cold256=0,int Fire256=0,int Lightning256=0,bool Critical=false,int LifeLost=0,string Reason=null);

internal static class DruidGrizzlyHit
{
    internal static bool InRange(DruidGrizzlyBody body,GrizzlyWorldState state,BaseCreature target)
    {
        int distance=DruidWolfGeometry.MeleeDistance(DruidGrizzlyWorld.Cell(state),PetOwnerRoutes.Cell(target),2,CreatureProfiles.For(target).Traits.Size.Value);
        return distance<=0||distance<=5&&body.InLOS(target); // Monster release bonus3 plus MeleeRng2.
    }
    internal static int Chance(DruidGrizzlyBody body,GrizzlyWorldState state,BaseCreature target)
    {
        var birth=body.Prepared.Snapshot.Birth;var attack=state.AttackPacket??throw new InvalidOperationException("Grizzly attack stats were not captured");
        int percent=(SorceressEffects.Enchantment(body)?.AttackPercent??0)+CombatAuras.AttackPercent(body)+NecromancerEffects.AttackStatPercent(body);
        return AssassinTrapAttack.Chance(birth.Level,checked(birth.BaseAttack+birth.AttackBonus+attack.Attack),0,percent,ClassStats.Level(target),ClassStats.Defense(target));
    }
    internal static int DamagePercent(DruidGrizzlyBody body)=>checked(body.Prepared.Snapshot.Birth.DamagePercent
        +CombatAuras.DamagePercent(body)+NecromancerEffects.DamageStatPercent(body));
    private static (int Min,int Max) Aura(DruidGrizzlyBody body,CombatAbility ability)
    {var aura=PaladinAuras.Effect(body,ability);return aura==null?(0,0):(NamedElements.To256(aura.WeaponMin),NamedElements.To256(aura.WeaponMax));}
    private static BearSmiteMitigation Mitigate(DruidGrizzlyBody body,BaseCreature target,DireWolfPacket packet)
    {
        var parts=SkillCombat.MitigatePrepared(body,target,new(packet.Physical256/256.0,packet.Fire256/256.0,
            packet.Cold256/256.0,packet.Lightning256/256.0,0),out int absorbed);
        int coldFrames=ColdStates.LengthFrames(packet.ColdFrames,NamedElements.Resistance(body,target,"cold",target.ColdResistance),false,false);
        return new(NamedElements.To256(parts.Physical),NamedElements.To256(parts.Fire),NamedElements.To256(parts.Lightning),NamedElements.To256(parts.Cold),coldFrames,absorbed);
    }
    internal static BearSmitePrepared Prepare(DruidGrizzlyBody body,PlayerMobile owner,GrizzlyWorldState state,BaseCreature target,
        bool smite,int npcRank,ref ulong seed,out int chance,out bool blockMode)
    {
        chance=0;blockMode=false;
        if(!DruidGrizzlyWorld.Eligible(body,owner,target)||!InRange(body,state,target)||!AlliedOverlap.Allowed(body)){return null;}
        if(!SourceBlocking.TryRate(target,out _,out _)){throw new NotSupportedException("Grizzly recipient block supplier is not mapped");}
        var attack=state.AttackPacket??throw new InvalidOperationException("Grizzly mode stats were not captured");
        chance=Chance(body,state,target);int flags=0;
        if(AssassinTrapAttack.Hit(chance,SourceRandom.Roll(ref seed)))
        {
            if(!SourceBlocking.TryRoll(target,true,out bool blocked,out blockMode)){throw new NotSupportedException("Grizzly block supplier changed");}
            flags=blocked?16:DruidBearSmite.Successful;
        }
        var enchant=SorceressEffects.Enchantment(body);var fire=Aura(body,CombatAbility.HolyFire);
        var cold=Aura(body,CombatAbility.HolyFreeze);var lightning=Aura(body,CombatAbility.HolyShock);
        ulong current=seed;int Limited(int bound)=>(int)SourceRandom.Limited(ref current,bound);uint Raw()=>SourceRandom.Roll(ref current);
        BearSmitePrepared prepared;
        if(smite)
        {
            prepared=DruidBearSmite.Prepare(npcRank,flags,Math.Max(0,SkillHealth.Balance256(target)),
                new(attack.Minimum,attack.Maximum,DamagePercent(body),checked((enchant?.Min256??0)+fire.Min),checked((enchant?.Max256??0)+fire.Max),
                    lightning.Min,lightning.Max,cold.Min,cold.Max),packet=>Mitigate(body,target,packet),Limited,Raw);
        }
        else if((flags&DruidBearSmite.Successful)!=0)
        {
            var raw= DruidDireWolfHit.Packet(attack.Minimum,attack.Maximum,DamagePercent(body),
                checked((enchant?.Min256??0)+fire.Min),checked((enchant?.Max256??0)+fire.Max),
                lightning.Min,lightning.Max,cold.Min,cold.Max,0,NecromancerSummons.Source("druidbear","Crit"),Limited,Raw);
            var p=Mitigate(body,target,raw);
            prepared=new(flags,p.Physical256,p.Fire256,p.Lightning256,p.Cold256,p.ColdFrames,
                checked(p.Physical256+p.Fire256+p.Lightning256+p.Cold256),0,raw.Critical?0x10:0,p.Absorbed256,raw.Critical);
        }
        else{prepared=new(flags,0,0,0,0,0,0,0,0,0,false);}
        seed=current;return prepared;
    }
    internal static GrizzlyHitResult Release(DruidGrizzlyBody body,PlayerMobile owner,GrizzlyWorldState state,BaseCreature target,
        BearSmitePrepared packet,int chance,bool blockMode,bool smite)
    {
        uint id=target?.Serial.Value??0;
        if(packet==null||!DruidGrizzlyWorld.Eligible(body,owner,target)||!InRange(body,state,target)||!AlliedOverlap.Allowed(body))
        {return new(GrizzlyHitOutcome.Invalid,id,chance);}
        body.DoHarmful(target,true);using var attempted=PaladinThorns.Attempt(body,target,false);
        if((packet.ResultFlags&DruidBearSmite.Successful)==0)
        {
            SorceressEffects.MeleeAttempt(body,target);
            if((packet.ResultFlags&16)!=0){SourceBlocking.Present(target,blockMode);return new(GrizzlyHitOutcome.Blocked,id,chance);}
            return new(GrizzlyHitOutcome.Miss,id,chance);
        }
        // ExecuteEvents: on-damage Life Tap precedes the physical HP clip;
        // deferred absorption heals before cached total commits. The separate
        // post-allocation BearSmite physical field never becomes extra HP loss.
        NecromancerEffects.LifeTap(body,target,packet.Physical256);
        int clipped=Math.Min(Math.Max(0,packet.Physical256),Math.Max(0,SkillHealth.Balance256(target)));
        if(packet.AbsorbedLife256>0){SkillHealth.Heal(target,packet.AbsorbedLife256);}
        int lost=SkillHealth.Damage(body,target,packet.Total256);
        if(packet.ColdFrames>0){ColdStates.Commit(body,target,packet.ColdFrames,true,ColdStates.Effect(target),NecromancerEffects.DifficultyDivisor(target),Core.TickCount);}
        SorceressEffects.MeleeAttempt(body,target);
        PaladinThorns.Prepared(body,target,packet.Physical256,false,clipped);
        bool knocked=smite&&(packet.ResultFlags&DruidBearSmite.Knockback)!=0&&SourceKnockbacks.Begin(body,target);
        if(!knocked){CreatureHitRecovery.Apply(target,body,lost,true);}
        CombatDamage.Finish(target,body);
        return new(GrizzlyHitOutcome.Hit,id,chance,packet.Physical256,packet.Total256,packet.Cold256,packet.Fire256,
            packet.Lightning256,packet.Critical,lost);
    }
    internal static GrizzlyHitResult Ordinary(DruidGrizzlyBody body,PlayerMobile owner,GrizzlyWorldState state,BaseCreature target,ref ulong seed)
    {
        var packet=Prepare(body,owner,state,target,false,1,ref seed,out int chance,out bool blockMode);
        return Release(body,owner,state,target,packet,chance,blockMode,false);
    }
}
