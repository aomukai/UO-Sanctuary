// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record MiasmaChainNumbers(int Rank,int DirectMinimum256,int DirectMaximum256,int CloudMinimum256,int CloudMaximum256,
    int Mana256,int LinkFrames,int MaximumChains,int LinkLength,EntropyCloudShape Shape);
internal sealed record MiasmaChainBirth(MiasmaChainNumbers Numbers,int HardBolt,int HardAbyss,int MagicMastery,FirestormModifiers Modifiers,SlowMissilePayload Slow);

// Captured suppliers for the distinct711/712/713/696graph. Attachment,cloud
// corridor/stamp geometry and action-rate policy remain separate world rules.
internal static class MiasmaChainBirths
{
    internal static MiasmaChainNumbers Numbers(SkillValues values)
    {
        _=MiasmaChainLaunch.PinnedMaker(values);var link=MiasmaChainLaunch.Missile("miasmachains",values);var maker=MiasmaChainLaunch.Missile("miasmachainsmaker",values);
        var cloud=MiasmaChainLaunch.Missile("miasmachainscloud",values);
        if(link.Number("CanSlow")!=1||link.Number("GetHit")!=1||link.Number("Size")!=2||link.Number("Param2")!=0||link.Number("Param3")!=1
            ||cloud.Number("CanSlow")!=1||cloud.Number("GetHit")!=1||cloud.Number("Param1")!=1||cloud.Number("Explosion")!=1
            ||maker.Number("Param1")!=0||maker.Number("Param2")!=0||maker.Number("Param4")!=1||maker.Number("SrvCalc1")!=40)
        {throw new NotSupportedException("Pinned Chain graph flags changed");}
        var shape=WarlockEntropyClouds.Capture(values);
        return new(values.Rank,values.Damage256(false),values.Damage256(true),cloud.ElementDamage256(false),cloud.ElementDamage256(true),
            values.Mana256,checked((int)link.Eval("Range")),checked((int)maker.Eval("CltCalc1")),maker.Number("SrvCalc1"),shape);
    }
    private static SkillValues Values(int rank,int bolt,int abyss,EntropyCloudShape shape)=>new(SkillMechanics.For(CombatAbility.MiasmaChain),
        k=>k=="Miasma Chains"?rank:k=="Enhanced Entropy"?shape.EffectiveRank:0,
        k=>k=="Miasma Bolt"?bolt:k=="Abyss"?abyss:k=="Enhanced Entropy"?shape.HardRank:0,99);
    internal static MiasmaChainBirth Capture(PlayerMobile p)
    {
        if(p is not{Alive:true,Deleted:false}||ClassCreation.Profiles.TryGet(p.Serial,out var c)!=true||c.Class!=DiabloClass.Warlock)
        {throw new NotSupportedException("Chain requires a living Warlock/Witch");}
        var v=SkillMechanics.Values(p,CombatAbility.MiasmaChain);int Hard(string k)=>checked((int)v.Reference("skill",k,"blvl"));
        var b=new MiasmaChainBirth(Numbers(v),Hard("Miasma Bolt"),Hard("Abyss"),NamedElements.MasteryPercent(p,"mag"),DruidFirestorm.Modifiers(p),AmazonSlowMissiles.Capture(p,true));Validate(b);return b;
    }
    internal static void Validate(MiasmaChainBirth b)
    {
        if(b?.Numbers is not{} n||n.Rank is <1 or >255||b.HardBolt is <0 or >20||b.HardAbyss is <0 or >20||n.Shape is not{} s
            ||s.EffectiveRank is <0 or >255||s.HardRank is <0 or >20||b.Slow.VelocityPercent is <0 or >100||b.Slow.DamagePercent is <0 or >100)
        {throw new InvalidDataException("Invalid Chain birth");}
        if(n!=Numbers(Values(n.Rank,b.HardBolt,b.HardAbyss,s))||n.LinkFrames!=125||n.MaximumChains is <5 or >10||n.LinkLength!=40)
        {throw new InvalidDataException("Saved Chain suppliers changed");}
        MiasmaBoltBirths.ValidatePacket(n.DirectMinimum256,n.DirectMaximum256,b.MagicMastery,b.Modifiers);
        MiasmaBoltBirths.ValidatePacket(n.CloudMinimum256,n.CloudMaximum256,b.MagicMastery,b.Modifiers);
    }
}
