// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;

namespace Sanctuary;

internal sealed record BladeFuryResourceServices(Func<int> Mana256,Func<int,bool> Debit,
    Func<bool> Allocate,Func<bool> Aim,Func<bool> Emit,Action Rewind);

// Ordinary learned-player start26/do48 resource composition. World eligibility,
// used-skill selection, charges/Blood Mana and held-input scheduling are callers.
// Current tables override the supplementary old consumer's rank-one zero debit.
internal static class BladeFuryResources
{
    private static SkillDefinition Row()
    {
        var row=SkillMechanics.For(CombatAbility.BladeFury);
        if(row.SourceId!=266||row.Number("srvstfunc")!=26||row.Number("srvdofunc")!=48
            ||row.Number("usemanaondo")!=1||row.Text("srvmissilea")!="bladefragment1")
        {throw new NotSupportedException("Blade Fury resource binding differs from pinned start26/do48/missile506");}
        return row;
    }
    internal static int Cost256(int rank)
    {
        if(rank is <1 or >255){throw new ArgumentOutOfRangeException(nameof(rank));}
        var row=Row();
        return new SkillValues(row,key=>key==row.SourceKey?rank:0,_=>0,1).Mana256;
    }

    internal static BladeFuryResourceServices Player(PlayerMobile owner,Func<bool> allocate,
        Func<bool> aim,Func<bool> emit,Action rewind)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return new(()=>NecromancerEffects.ManaBalance256(owner),cost=>NecromancerEffects.Pay(owner,cost),
            allocate,aim,emit,rewind);
    }

    private static BladeFuryChannelServices Channel(BladeFuryResourceServices services,int cost)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(services.Mana256);ArgumentNullException.ThrowIfNull(services.Debit);
        return new(services.Allocate,services.Aim,services.Emit,()=>services.Debit(cost),services.Rewind);
    }

    internal static BladeFuryChannelStep Start(BladeFuryChannelState state,int frame,int rank,
        BladeFuryResourceServices services)
    {
        ArgumentNullException.ThrowIfNull(state);
        int cost=Cost256(rank);var channel=Channel(services,cost);
        if(services.Mana256()<cost){return new(state,false);}
        var row=Row();
        return BladeFuryChannel.Start(state,frame,true,true,506,
            services.Mana256(),checked(row.Number("startmana")*256),row.Number("Param4"),channel);
    }

    internal static BladeFuryChannelStep Do(BladeFuryChannelState state,int frame,int rank,
        BladeFuryResourceServices services)
    {
        ArgumentNullException.ThrowIfNull(state);
        int cost=Cost256(rank);var channel=Channel(services,cost);
        if(services.Mana256()<cost){return new(state,false);}
        var row=Row();
        var step=BladeFuryChannel.Do(state,frame,true,true,506,row.Number("Param4"),channel);
        if(step.Success){channel.Consume();}
        return step;
    }
}
