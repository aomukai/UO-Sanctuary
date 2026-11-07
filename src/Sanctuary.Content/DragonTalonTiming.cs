// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server.Mobiles;

namespace Sanctuary;

internal sealed record DragonTalonTimingPlan(string Profile,int Rank,int Advance,int[] Contacts,int EndFrame);

// Prepared original-author calculator projection. This does not register a
// learned cast, choose modern charge consumption, or certify D2R execution.
internal static class DragonTalonTiming
{
    internal const string Profile="original-calculator-b7d7910-human-talon-v1";
    private static readonly Lazy<bool> Validated=new(Validate);
    private static bool Validate()
    {
        var rows=BalanceCatalog.Read<SourceCastRow>("assassin-kick-clock");
        var keys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in rows)
        {
            if(row.Frames!=13||row.Speed!=256||row.Action!=4||row.Flag!=1||!keys.Add(row.WeaponClass))
            {throw new InvalidDataException("Unsupported owned Assassin KK animation");}
        }
        if(!keys.SetEquals(new[]{"HTH","1HS","1HT","2HS","2HT","STF","BOW","XBW","HT1","HT2"}))
        {throw new InvalidDataException("Missing owned Assassin KK weapon profiles");}
        var skill=SkillMechanics.For(CombatAbility.DragonTalon);
        if(skill.SourceId!=255||skill.Text("anim")!="KK"||skill.Text("seqtrans")!="KK"||skill.Number("seqnum")!=19
            ||skill.Number("srvstfunc")!=24||skill.Number("srvdofunc")!=42||skill.Text("calc1")!="lvl/6+1")
        {throw new InvalidDataException("Unsupported prepared Dragon Talon clock route");}
        return true;
    }
    internal static int Count(int rank)
    {if(rank is <1 or >255){throw new ArgumentOutOfRangeException(nameof(rank));}return rank/6+1;}
    internal static int Advance(int primaryWsm,int ias,int slow,int skillRate)
        =>CombatTiming.FrameAdvance(new(AttackFamily.Thrust,primaryWsm),ias,slow,skillRate);
    internal static int Advance(PlayerMobile owner)
    {
        if(owner==null||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Assassin
            ||DruidPlayerForms.State(owner)!=null)
        {throw new NotSupportedException("Prepared Talon timing requires an ordinary Assassin context");}
        var pair=WeaponHands.Pair(owner);var primary=pair?.Primary??CombatTiming.Equipped(owner);
        using var hand=pair is{} weapons?WeaponHands.Use(owner,weapons.Primary):null;
        return Advance(CombatTiming.Weapon(primary).Modifier,CombatTiming.Ias(owner),GearEffects.AttackSlowTotal(owner),CombatTiming.SkillAttackRate(owner));
    }
    internal static DragonTalonTimingPlan Plan(int rank,int advance)
    {
        _=Validated.Value;
        // Fixed owned base256 and the existing human15..175percent clamp.
        if(advance is <38 or >448){throw new NotSupportedException("Unmapped prepared Talon frame advance");}
        int count=Count(rank),step=AmazonFend.Ceiling(4,advance),last=AmazonFend.Ceiling(13,advance)-1;
        return new(Profile,rank,advance,Enumerable.Range(1,count).Select(i=>checked(i*step)).ToArray(),checked((count-1)*step+last));
    }
    internal static DragonTalonTimingPlan Plan(PlayerMobile owner)=>Plan(SkillMechanics.Rank(owner,CombatAbility.DragonTalon),Advance(owner));
}
