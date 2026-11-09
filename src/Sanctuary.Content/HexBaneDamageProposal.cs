// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record HexBaneDamagePlan(int Rank,int CapturedMagicMastery,HexBaneValues Values);
// Approved Sanctuary rule. Current28cc10 remains protected;the explicit
// adaptation is recorded in hex-bane-damage-approval.json.
internal static class HexBaneDamageProposal
{
    internal static bool Approved=>true;
    private static int Stored(int raw256,int mastery)
    {
        if(raw256<0){throw new ArgumentOutOfRangeException(nameof(raw256));}
        long enhanced=Math.Max(0,(long)raw256+(long)raw256*mastery/100);
        if(enhanced>int.MaxValue){throw new NotSupportedException("Bane mastered endpoint exceeds signed FP256 domain");}
        return(int)enhanced>>8;
    }
    internal static HexBaneDamagePlan Capture(SkillValues values,int magicMastery)
    {
        var basic=HexBane.Capture(values);
        return new(values.Rank,magicMastery,basic with{Minimum=Stored(values.Damage256(false),magicMastery),Maximum=Stored(values.Damage256(true),magicMastery)});
    }
    // The caller must first finish its hit/block/dodge result. The recipient
    // state cannot alter that already-decided attack;misses still apply it.
    internal static bool AfterMeleeResult(PlayerMobile owner,BaseCreature target,HexBaneBuff registration,bool attempted)
        =>attempted&&WeaponHexes.Equipped(owner)&&!NamedCombat.Ranged(owner)&&PlayerMeleeGeometry.Within(owner,target,0)
            &&HexBane.ApplyPrepared(owner,target,registration,true,TimedEffects.Now,replaceGroup:WeaponHexGroups.Approved);
}
