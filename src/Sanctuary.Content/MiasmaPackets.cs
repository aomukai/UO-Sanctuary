// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record MiasmaDamage(int BoltMinimum256,int BoltMaximum256,int CloudMinimum256,int CloudMaximum256,
    int CloudFrames,int CloudRadius,int Mana256);

// Prepared numerical suppliers only. New hit41/cloud-do41 dispatch, geometry,
// hit-delay identity and creator timing must be connected before learned casts.
internal static class MiasmaPackets
{
    internal static MiasmaDamage Capture(SkillValues values)
    {
        var skill=SkillMechanics.For(CombatAbility.MiasmaBolt);
        var cloud=new MissileValues("miasmaboltcloud",values);
        if(values.Ability!=CombatAbility.MiasmaBolt||values.Rank is <1 or >255||skill.SourceId!=395||skill.Text("EType")!="mag"
            ||skill.Text("srvmissile")!="miasmabolt"||skill.Number("SrcDam")!=0
            ||cloud.Text("EType")!="mag"||cloud.Number("SrcDamage")!=0||cloud.Number("SrcMissDmg")!=0||cloud.Text("Skill")!="0"
            ||cloud.Number("pSrvDoFunc")!=41||cloud.Number("Explosion")!=1||cloud.Number("NextDelay")!=25)
        {throw new NotSupportedException("Unsupported Miasma damage route");}
        return new(values.Damage256(false),values.Damage256(true),cloud.ElementDamage256(false),cloud.ElementDamage256(true),
            checked((int)cloud.Eval("Range")),checked((int)cloud.Eval("Radius")),values.Mana256);
    }
    internal static MiasmaDamage Capture(PlayerMobile owner)
    {
        if(owner is not{Alive:true,Deleted:false}||ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)!=true||profile.Class!=DiabloClass.Warlock)
        {throw new NotSupportedException("Unsupported Miasma owner");}
        // Modern mastery creator/roll placement is not established by the
        // legacy magic branch. Do not import elemental mastery timing.
        if(NamedElements.MasteryPercent(owner,"mag")!=0)
        {throw new NotSupportedException("Miasma modern magic mastery timing remains unverified");}
        return Capture(SkillMechanics.Values(owner,CombatAbility.MiasmaBolt));
    }
}
