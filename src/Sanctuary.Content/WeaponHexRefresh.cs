// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

// Refresh only an existing captured attachment. Never reapply the caster's
// current weapon buff, transfer ownership, refill Purge charges or revive an
// expired/depleted Hex. Older duration-less attachments expire normally.
internal static class WeaponHexRefresh
{
    internal static void RequirePrepared(Mobile target)
    {
        var bane=HexBane.Debuff(target);var siphon=HexSiphon.Debuff(target);var purge=HexPurge.Debuff(target);
        int count=(bane!=null?1:0)+(siphon!=null?1:0)+(purge!=null?1:0);
        if(count>1){throw new NotSupportedException("Multiple target Hex groups are unsupported");}
        if(bane is{RefreshFrames:null}||siphon is{RefreshFrames:null})
        {throw new NotSupportedException("Legacy Bane/Siphon has no captured refresh duration; let it expire or reapply the Hex");}
    }
    internal static bool RefreshPrepared(Mobile target,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Hex refresh clock must be UTC");}
        if(target is not{Alive:true,Deleted:false}){return false;}
        RequirePrepared(target);
        var bane=HexBane.Debuff(target);var siphon=HexSiphon.Debuff(target);var purge=HexPurge.Debuff(target);
        if(bane!=null)
        {
            return HexBane.RefreshPrepared(bane,at);
        }
        if(siphon!=null)
        {
            return HexSiphon.RefreshPrepared(siphon,at);
        }
        return purge!=null&&HexPurge.RefreshPrepared(purge,at);
    }
    internal static bool HasHex(Mobile target)=>HexBane.Debuff(target)!=null||HexSiphon.Debuff(target)!=null||HexPurge.Debuff(target)!=null;
}
