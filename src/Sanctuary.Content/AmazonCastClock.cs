// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal sealed record AmazonCastRow(string WeaponClass,int Frames,int Speed,int Action,int Flag);
internal static class AmazonCastClock
{
    private static readonly Dictionary<string,AmazonCastRow> Rows=new(StringComparer.Ordinal);
    public static void Configure()
    {
        foreach(var row in BalanceCatalog.Read<AmazonCastRow>("amazon-cast-clock"))
        {
            if(row.Frames!=20||row.Speed!=256||row.Action is <10 or >13||row.Flag is not(1 or 2)||!Rows.TryAdd(row.WeaponClass,row))
            {throw new InvalidDataException("Unsupported pinned Amazon SC animation");}
        }
        foreach(string key in new[]{"HTH","1HS","1HT","2HS","2HT","STF","BOW","XBW"})
        {if(!Rows.ContainsKey(key)){throw new InvalidDataException("Missing Amazon SC weapon class");}}
        if(Rows.Count!=8){throw new InvalidDataException("Unexpected Amazon SC weapon class");}
        foreach(var ability in new[]{CombatAbility.InnerSight,CombatAbility.SlowMissiles,CombatAbility.Decoy})
        {
            var row=SkillMechanics.For(ability);
            if(row.Text("anim")!="SC"||row.Text("seqtrans")!="SC")
            {throw new InvalidDataException("Amazon skill no longer selects SC");}
        }
    }
    internal static ActionTiming Calculate(string weaponClass,int fcr)
    {
        if(weaponClass==null||!Rows.TryGetValue(weaponClass,out var row))
        {throw new NotSupportedException("Amazon SC needs a classified source weapon animation");}
        if(fcr is <0 or >10000){throw new NotSupportedException("Unsupported Amazon FCR");}
        int effective=120*fcr/(120+fcr);
        int advance=row.Speed*Math.Min(175,100+effective)/100;
        return new((row.Frames*256+advance-1)/advance,(row.Action*256+advance-1)/advance);
    }
    internal static bool TryTiming(PlayerMobile owner,out ActionTiming timing)
    {
        string key=WeaponAnimationClasses.Equipped(owner);
        if(key==null||!Rows.ContainsKey(key)){timing=default;return false;}
        timing=Calculate(key,CombatTiming.Fcr(owner));return true;
    }
    internal static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now,
        Func<PlayerMobile,CombatAbility,Serial,Point3D,bool> validTarget)
    {
        if(ability is not(CombatAbility.InnerSight or CombatAbility.Decoy))
        {throw new NotSupportedException("Only admitted learned Amazon SC routes may use this entry");}
        if(!TryTiming(owner,out var timing))
        {owner.SendMessage(0x22,"This weapon's Amazon casting animation is not classified yet.");return false;}
        return SkillCombat.Begin(owner,ability,selected,point,now,validTarget,timing:timing);
    }
}
