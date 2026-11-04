// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record ReviveAnimationFlag(int Frame,int Kind);
internal sealed record ReviveAttackAnimation(string Source,string Code,int Frames,int Speed,ReviveAnimationFlag[] Flags,string Issue);
internal sealed record ReviveSwingPlan(string Source,string Animation,int FasterAttackRate,int ActorRate,int WeaponSpeed,int Speed,int Action,int End);

internal static class ReviveSwingClock
{
    private static readonly Lazy<Dictionary<string,ReviveAttackAnimation>> Rows=new(()=>
    {
        var rows=BalanceCatalog.Read<ReviveAttackAnimation>("revive-attack-clocks");
        if(rows.Length!=751||rows.Select(r=>r.Source).Distinct(StringComparer.Ordinal).Count()!=751
            ||rows.Any(r=>string.IsNullOrWhiteSpace(r.Source)||r.Code==null||r.Flags==null||r.Issue!=null&&string.IsNullOrWhiteSpace(r.Issue)
                ||r.Issue==null&&(r.Frames is <1 or >144||r.Speed is <1 or >32767||r.Flags.Length!=1||r.Flags[0].Kind!=1||r.Flags[0].Frame<0||r.Flags[0].Frame>=r.Frames)))
        {throw new InvalidDataException("Invalid pinned original A1 clocks");}
        return rows.ToDictionary(r=>r.Source,StringComparer.Ordinal);
    });
    internal static ReviveAttackAnimation Metadata(string source)=>source!=null&&Rows.Value.TryGetValue(source,out var row)
        ?row:throw new NotSupportedException("No original source A1 metadata");
    internal static ReviveSwingPlan Calculate(string source,int faster,int rate,int weaponSpeed)
    {
        var row=Metadata(source);if(row.Issue!=null){throw new NotSupportedException(row.Issue+": "+source);}
        int speed=SourceAttackRate.Speed(row.Speed,faster,checked(rate-weaponSpeed));var flags=new int[144];
        foreach(var flag in row.Flags){flags[flag.Frame]=flag.Kind;}
        var clock=SourceAnimationEvents.Initial(new(0,speed,row.Frames*256,flags,[]),0);
        var action=clock.Events.Where(e=>e.Type==SourceAnimationEvents.ModeChange&&e.Kind==1).ToArray();
        if(action.Length!=1){throw new NotSupportedException("Original A1 rate skips or duplicates its action event");}
        return new(source,row.Code,faster,rate,weaponSpeed,speed,action[0].Frame,clock.Events.Single(e=>e.Type==SourceAnimationEvents.EndAnimation).Frame);
    }
    internal static ReviveSwingPlan Capture(BaseCreature actor)
    {
        if(!ReviveEquipmentEvents.Handles(actor)||ReviveNativeActivation.State(actor)is not{} state)
        {throw new NotSupportedException("Original A1 clock requires its admitted frozen equipment context");}
        return Calculate(state.Setup.LifePlan.Source,ReviveEquipmentLife.Items(state),(state.FacetPlan?.Combat??state.Setup.CombatPlan).Level,SourceAttackRate.WolfRate(actor));
    }
    internal static ReviveSwingPlan Calculate(string source,FrozenEquipmentContext[] items,int level,int rate)
    {
        int ias=0,weaponSpeed=0,weapons=0;
        foreach(var item in items)
        {
            var basis=item.CombatBase(level);
            if(basis.Weapon)
            {
                if(basis.WeaponSpeed==null){throw new NotSupportedException("Historical frozen weapon has no captured base speed");}
                weapons++;weaponSpeed=checked(weaponSpeed+basis.WeaponSpeed.Value);
            }
            ias=checked(ias+item.Bonus("swing1",level)+item.Bonus("swing2",level)+item.Bonus("swing3",level));
        }
        if(weapons>1){throw new NotSupportedException("Original A1 clock needs explicit multiple-hand selection");}
        return Calculate(source,ias,rate,weaponSpeed);
    }
}
