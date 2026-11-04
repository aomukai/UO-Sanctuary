// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;
using Server.Logging;

namespace Sanctuary;

internal sealed record PreparedKickPacket(KickDamageInput Input,PreparedWeaponImpact Impact,PlayerWeaponDrain Drain,
    int? ColdFrames,int FreezeFrames,bool SoftHit);

// Prepared kick damage and Tail's distinct fire consumer. Learned action,
// charged hit/block, payment and charge consumption remain separate gates.
public static class DragonKickPackets
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DragonKickPackets));
    private static readonly System.Collections.Generic.HashSet<string> BurstGaps=new();
    internal static PreparedKickPacket Prepare(PlayerMobile owner,Mobile target,int skillDamagePercent,double elementRoll,
        Func<int,int> physicalRoll,Func<int,int> poisonRoll,Random effects,bool orderedPoison=false)
    {
        if(owner==null || target==null){throw new ArgumentNullException();}
        var pair=WeaponHands.Pair(owner);
        using var selected=pair is{} weapons?WeaponHands.Use(owner,weapons.Primary):null;
        var weapon=CombatTiming.Equipped(owner);
        using var melee=weapon!=null?WeaponHands.UseMelee(owner,weapon):null;
        var states=AssassinCharges.Finisher(owner);
        var tiger=states.FirstOrDefault(x=>x.Ability==CombatAbility.TigerStrike);
        int enhanced=checked(skillDamagePercent+AssassinCharges.TigerPercent(tiger,SkillMechanics.Rank(owner,CombatAbility.TigerStrike)));
        var input=DragonKickDamage.Capture(owner,enhanced);
        int physical=DragonKickDamage.Roll256(input,physicalRoll);
        // The existing elemental collector is deterministic for physical damage
        // unless an explicit physical skill roll is requested. Discard that
        // weapon physical lane; it must never replace body/boot kick damage.
        var damage=GearEffects.Damage(owner,elementRoll,target,poisonRoll:poisonRoll,poisonFactory:orderedPoison?
            ()=>WeaponPoisonPackets.Melee(WeaponPoisonPackets.AddVenom(BowLaunch.RawPoison(owner,weapon),Venoms.State(owner)),
                NamedElements.MasteryPercent(owner,"pois"),128,poisonRoll,overrideFrames:Venoms.State(owner)?.Frames??0):null) with{Physical=physical/256.0};
        var fire=FistsFireRelease.Capture(owner,states.FirstOrDefault(x=>x.Ability==CombatAbility.FistsOfFire));
        var thunder=MartialLightning.Capture(owner,states.FirstOrDefault(x=>x.Ability==CombatAbility.ClawsOfThunder));
        var ice=MartialCold.Capture(owner,states.FirstOrDefault(x=>x.Ability==CombatAbility.BladesOfIce));
        var extra=new GearDamage(0,(fire?.Inline256??0)/256.0,(ice?.Inline256??0)/256.0,(thunder?.Inline256??0)/256.0,0,ColdMs:0);
        // Source kick hit flags3 bypass all ordinary physical critical branches.
        // The selected weapon still supplies its typed stats and drain/procs.
        var impact=GearEffects.PrepareWeaponImpact(owner,target,elementRoll,effects,damage,extra,
            projectileCritical:false,physicalToFirePercent:fire?.ConversionPercent??0);
        if(orderedPoison&&target is BaseCreature creature)
        {
            var poison=PoisonJavelins.PreparePoison(owner,creature,damage.SourcePoison.Rate256,damage.SourcePoison.Frames);
            impact=impact with{LifeDamage256=checked(NamedElements.To256(impact.Parts.Total)+poison.Rate256),AllocatedPoison=poison};
        }
        var cobra=states.FirstOrDefault(x=>x.Ability==CombatAbility.CobraStrike);
        var drain=WeaponLeech.Capture(owner,AssassinCharges.CobraDrain(cobra,SkillMechanics.Rank(owner,CombatAbility.CobraStrike)));
        if(damage.ColdMs%40!=0){throw new NotSupportedException("Kick cold duration has no exact source-frame projection");}
        int cold=checked(damage.ColdMs/40+(ice?.Frames??0));
        // Prgdam4 adds freeze from the entire accumulated cold length, including
        // item cold, after the inline Ice roll. Current Ice Param5 is one.
        int freeze=ice?.Count==3?cold/SkillMechanics.For(CombatAbility.BladesOfIce).Number("Param5"):0;
        return new(input,impact,drain,cold>0?cold:null,freeze,fire!=null || thunder!=null || ice!=null);
    }
    internal static int TailFire256(int physical256,SkillValues values,int mastery)
    {
        if(physical256<0 || values.Ability!=CombatAbility.DragonTail || values.Rank is <1 or >255)
        {throw new NotSupportedException("Unmapped Dragon Tail fire packet");}
        // Do50 adds skill percentage and current mastery before one source
        // percentage operation, including its large-value early division.
        int percent=checked((int)values.Eval("calc1")+mastery);
        long intermediate=physical256<=1048576
            ?percent<=65536?(long)physical256*percent
                :100<=percent>>4?(long)physical256*(percent/100):(long)physical256*percent/100
            :100<=physical256>>4?(long)percent*(physical256/100):(long)physical256*percent/100;
        if(intermediate is >int.MaxValue or <int.MinValue)
        {throw new NotSupportedException("Legacy Tail percentage overflows; current executable behavior is unverified");}
        return Math.Max(0,SourcePercentage.Calculate(physical256,percent,100));
    }
    internal static int TailBurst(PlayerMobile owner,Map map,Point3D center,int copiedPhysical256,SkillValues values)
    {
        if(owner is not{Alive:true,Deleted:false} || map==null || map==Map.Internal || owner.Map!=map){return 0;}
        int fire=TailFire256(copiedPhysical256,values,NamedElements.MasteryPercent(owner,"fire"));
        int radius=(int)values.Eval("aurarangecalc");
        if(radius is <1 or >64){throw new NotSupportedException("Unmapped Dragon Tail radius");}
        if(SpellHelper.IsTown(center,owner)){return 0;}
        int loss=0;
        // Source default0x8583 adds hostile eligibility but no optional LOS
        // or NOAURA gate. Shared native creature/height/sector projection is
        // bounded separately from full executable/PvP/flagged-missile parity.
        foreach(var target in SkillCombat.NearbySnapshot<BaseCreature>(map,center,(radius+1)/2))
        {
            if(FistsFireRelease.InRadius(center,target,radius) && ClassCombat.IsHostile(owner,target)
                && !SpellHelper.IsTown(target.Location,target))
            {
                if(!TailRecipient(owner,target,out string reason))
                {if(BurstGaps.Add(reason)){Log.Warning("Dragon Tail burst recipient unsupported: {Reason}",reason);}continue;}
                var result=TailImpact(owner,target,fire,TimedEffects.Now);
                loss=checked(loss+Math.Max(0,result.Before256-result.AfterHealth256));
            }
        }
        Effects.SendLocationEffect(center,map,0x36BD,10,10,0x489,0); // Identified fire-burst placeholder.
        return loss;
    }
    internal static bool TailRecipient(PlayerMobile owner,BaseCreature target,out string reason)
    {
        if(!InstantSpellNpcDamage.Supported(owner,target,"Dragon Tail",out reason)){return false;}
        var metadata=PlayerWeaponContact.Metadata(target,true);
        if(metadata!=null&&(metadata.Modes&(1<<PreparedMonsterReturn.Knockback))!=0
            &&(SourceKnockbacks.Rule(target) is not{Knockback:true,GetHit:true,Distance:5}||target.X==owner.X&&target.Y==owner.Y))
        {reason="Dragon Tail knockback trajectory/zero-vector path is not adapted";return false;}
        return true;
    }
    internal static PreparedMonsterDamageResult TailImpact(PlayerMobile owner,BaseCreature target,int fire256,DateTime at)
    {
        if(fire256<0){throw new NotSupportedException("Invalid Tail fire packet");}
        if(!TailRecipient(owner,target,out string reason)){throw new NotSupportedException(reason);}
        var metadata=PlayerWeaponContact.Metadata(target,true);NativeMonsterReturn.Session(target);
        PreparedMonsterDamagePacket Mitigate(PreparedMonsterDamagePacket p)
        {
            var parts=SkillCombat.MitigatePrepared(owner,target,new(0,p.Fire256/256.0,0,0,0),out int absorb);
            return p with{Total256=NamedElements.To256(parts.Total),Fire256=NamedElements.To256(parts.Fire),AbsorbedLife256=absorb};
        }
        // Do50 starts with flags9 (SUCCESS|KNOCKBACK); sub_6FD10140 adds
        // GETHIT for admitted interruptible NPCs. No weapon/missile events,
        // Thorns, item procs, source critical or mastery application is added.
        var services=new InstantSpellNpcDamage(owner,target,metadata,null,at,"Dragon Tail",()=>TelekinesisCombat.DefenderRoll(),
            knockback:SourceKnockbacks.BeginPrepared,playerMitigation:Mitigate);
        return PreparedMonsterDamage.ReleaseMissile(owner,target,new(0,fire256,0,0,13,Fire256:fire256),
            new(NamedCombat.Elite(target),()=>false,()=>metadata?.Velocity??0,()=>false),
            _=>throw new InvalidOperationException("Tail burst has no timed Stun"),services,at);
    }
}
