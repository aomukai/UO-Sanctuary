// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public sealed record HolyBoltCapture(FirestormModifiers Modifiers,SlowMissilePayload Slow);

// Shared typed holy receiver; the original magic bypass and ally policy
// are selected by the parent skill. Old uncaptured rays retain compatibility.
internal static class HolyBoltDamage
{
    internal static void Route(SkillValues values)
    {
        bool secondary=values.Ability==CombatAbility.FistOfTheHeavens;
        string missile=HolyBolts.Missile(values.Ability);int M(string field)=>NecromancerSummons.Source(missile,field);
        var row=SkillMechanics.For(values.Ability);int speed=secondary?12:20;
        if(values.Ability is not (CombatAbility.HolyBolt or CombatAbility.FistOfTheHeavens)||values.Rank is <1 or >255
            ||(secondary?row.SourceId!=121||row.Text("srvmissilea")!="fistoftheheavensdelay"||row.Text("EType")!="ltng"||M("ApplyMastery")!=0||row.Number("TargetAlly")!=0||row.Number("TargetPet")!=0
                :row.SourceId!=101||row.Text("srvmissile")!="holybolt"||row.Text("EType")!="mag")
            ||M("pSrvDoFunc")!=1||M("pSrvHitFunc")!=7||M("pSrvDmgFunc")!=15||M("sHitPar1")!=1||M("sHitPar2")!=3||M("sHitPar3")!=0||M("dParam1")!=0
            ||M("Holy")!=(secondary?3:1)||M("Vel")!=speed||M("MaxVel")!=speed||M("Range")!=50||M("Activate")!=(secondary?2:0)||M("InitSteps")!=1
            ||M("CanSlow")!=1||M("GetHit")!=1||M("ResultFlags")!=4||M("HitFlags")!=0||M("HitClass")!=0)
        {throw new NotSupportedException("Unsupported pinned Holy Bolt route");}
        if(NecromancerSummons.Fields(missile)["EType"].GetString()!="mag"
            ||secondary&&NecromancerSummons.Fields("fistoftheheavensdelay")["HitSubMissile1"].GetString()!=missile)
        {throw new NotSupportedException("Unsupported pinned holy child factory");}
    }
    internal static bool Supported(PlayerMobile owner,Mobile actor,out string reason)
        =>InstantSpellNpcDamage.Supported(owner,actor,"Holy Bolt",out reason);
    internal static PreparedMonsterDamageResult Deliver(PlayerMobile owner,BaseCreature target,int magic256,HolyBoltCapture captured,DateTime at,CombatAbility ability=CombatAbility.HolyBolt)
    {
        if(!Supported(owner,target,out string reason)||!HolyBolts.FullDamage(target)||ClassCombat.Friendly(owner,target)
            ||ability is not (CombatAbility.HolyBolt or CombatAbility.FistOfTheHeavens)||magic256<0||captured?.Modifiers==null||at.Kind!=DateTimeKind.Utc)
        {throw new NotSupportedException(reason??"Unsupported Holy Bolt damage recipient/capture");}
        bool bypass=GemCombat.Undead(target)||ability==CombatAbility.FistOfTheHeavens&&NamedCombat.Demon(target); // Holy1 versus Holy3.
        PreparedMonsterDamagePacket Mitigate(PreparedMonsterDamagePacket packet)
        {
            var mods=captured.Modifiers with{Potion=CombatPotions.BonusAt(owner,"magical-penetration",at)};
            var raw=DruidMoltenBoulder.Potion(mods,new(0,0,0,0,0,Magic:packet.Magic256/256.0));
            // Only the original magic lane bypasses positive resistance/MDR/
            // absorb. Approved potion elements keep their ordinary defenses.
            var parts=SkillCombat.MitigatePrepared(owner,target,bypass?raw with{Magic=0}:raw,out int absorb);
            if(bypass)
            {
                int resistance=Math.Min(0,NamedElements.Resistance(owner,target,"mag",NamedElements.MagicResistance(target)));
                var reduced=NamedElements.Reduce256(packet.Magic256,resistance,0,0,0);
                parts=parts with{Magic=reduced.Damage256/256.0};
            }
            return packet with{Total256=NamedElements.To256(parts.Total),Magic256=NamedElements.To256(parts.Magic),Fire256=NamedElements.To256(parts.Fire),
                Cold256=NamedElements.To256(parts.Cold),Lightning256=NamedElements.To256(parts.Lightning),Poison256=NamedElements.To256(parts.Poison),AbsorbedLife256=absorb};
        }
        var metadata=PlayerWeaponContact.Metadata(target,true);NativeMonsterReturn.Session(target);
        var services=new InstantSpellNpcDamage(owner,target,metadata,captured.Modifiers,at,"Holy Bolt",()=>TelekinesisCombat.DefenderRoll(),playerMitigation:Mitigate);
        return PreparedMonsterDamage.ReleaseMissile(owner,target,new(0,magic256,0,0,5,Magic256:magic256),
            new(NamedCombat.Elite(target),()=>false,()=>metadata?.Velocity??0,()=>false),
            _=>throw new InvalidOperationException("Holy Bolt has no timed stun"),services,at);
    }
}
