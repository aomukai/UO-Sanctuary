// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

// This records the release's base roll, caster-level reduction and calc1 HP
// increase. Full retained stat lists/passive events remain a separate bridge.
public sealed record ReviveLifePlan(string Source,MonsterDifficulty Difficulty,int OriginalLevel,int CasterLevel,int MasteryRank,
    ulong Seed,ulong NextSeed,int Minimum,int Maximum,int Rolled256,int Scaled256,int Maximum256);

internal static class ReviveLife
{
    internal static (ulong NextSeed,int Rolled256,int Scaled256,int Maximum256) Roll(int minimum,int maximum,int originalLevel,int casterLevel,ulong before,int lifePercent)
    {
        if(minimum<0||maximum<minimum||maximum>int.MaxValue/256){throw new NotSupportedException("Unsupported Revive HP roll domain");}
        ulong seed=before;int rolled=checked((int)(SourceRandom.Limited(ref seed,checked(maximum-minimum+1))+minimum)*256);
        int scaled=originalLevel!=0&&casterLevel<originalLevel?Math.Max(1,SourcePercentage.Calculate(rolled,casterLevel,originalLevel)):rolled;
        int result=checked(scaled+SourcePercentage.Calculate(scaled,lifePercent,100));
        return(seed,rolled,scaled,result);
    }
    internal static ReviveLifePlan Calculate(string source,CorpseBaseSnapshot corpse,int casterLevel,int masteryRank)
    {
        if(corpse is not{Level:>0,Seed:not null}||casterLevel is <1 or >99||masteryRank is <0 or >255)
        {throw new NotSupportedException("Revive source life requires saved level/difficulty/seed and explicit caster ranks");}
        var bounds=CorpseBaseSnapshots.Bounds(source,corpse);
        if(bounds.Minimum<0||bounds.Maximum<bounds.Minimum||bounds.Maximum>int.MaxValue/256)
        {throw new NotSupportedException("Revive HP roll is outside the supported positive FP256 domain");}
        var values=new SkillValues(SkillMechanics.For(CombatAbility.Revive),key=>key=="Skeleton Mastery"?masteryRank:key=="Revive"?1:0,_=>0,casterLevel);
        int percent=checked((int)values.Eval("calc1"));
        var result=Roll(bounds.Minimum,bounds.Maximum,corpse.Level,casterLevel,corpse.Seed.Value,percent);
        if(result.Maximum256<=0||result.Maximum256>int.MaxValue-255){throw new NotSupportedException("Revive maximum is outside positive source FP256 range");}
        return new(source,corpse.Difficulty,corpse.Level,casterLevel,masteryRank,corpse.Seed.Value,result.NextSeed,bounds.Minimum,bounds.Maximum,result.Rolled256,result.Scaled256,result.Maximum256);
    }
    internal static void Validate(ReviveLifePlan plan)
    {
        try
        {
            if(plan==null||!Enum.IsDefined(plan.Difficulty)){throw new InvalidDataException("Invalid source Revive life plan");}
            var snapshot=new CorpseBaseSnapshot(1,plan.OriginalLevel,plan.Difficulty,CorpseBaseSnapshots.Base256(plan.Source,plan.Difficulty,plan.OriginalLevel),plan.Seed);
            if(Calculate(plan.Source,snapshot,plan.CasterLevel,plan.MasteryRank)!=plan)
            {throw new InvalidDataException("Source Revive life plan does not reproduce its saved roll/scaling");}
        }
        catch(Exception e)when(e is ArgumentException or NotSupportedException or OverflowException)
        {throw new InvalidDataException("Invalid source Revive life domain",e);}
    }
    internal static ReviveNativeSetup Setup(PlayerMobile owner,Corpse corpse,int mana,int stamina)
    {
        var unit=SourceCorpseIdentity.For(corpse);
        var plan=Calculate(unit.Snapshot.Source,unit.Snapshot.BaseLife,ClassStats.Level(owner),SkillMechanics.Rank(owner,CombatAbility.SkeletonMastery));
        int whole=(plan.Maximum256+255)/256;
        int rank=SkillMechanics.Rank(owner,CombatAbility.Revive);
        return new(rank,whole,whole,mana,stamina,plan,ReviveCombat.Capture(owner,plan,rank));
    }
    internal static int? Maximum256(BaseCreature actor)
    {
        var state=ReviveNativeActivation.State(actor);
        if((state?.FacetPlan?.Life??state?.Setup.LifePlan) is not{} plan){return null;}
        long maximum=plan.Maximum256+ClassResources.Percentage(plan.Maximum256,BattleOrders.LifePercent(actor));
        if(maximum is <1 or >int.MaxValue-255){throw new NotSupportedException("Buffed Revive life exceeds positive source FP256 range");}
        return (int)maximum;
    }
}
