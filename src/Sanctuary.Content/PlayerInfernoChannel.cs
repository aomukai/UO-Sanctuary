// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record PlayerInfernoState(bool Active=false,bool Lease=false,int Expires=0,int Param1=0,uint Flags=64);
internal sealed record PlayerInfernoStep(PlayerInfernoState State,bool Success);
internal sealed record PlayerInfernoServices(Func<bool> Allocate,Func<bool> Aim,Func<int,bool> Emit,Action Rewind);

// Complete player branch of st11/StartInferno/do19/DoInferno. Accepted start
// intents, generic wrappers, sequence scheduling and modern input are callers.
internal static class PlayerInfernoChannel
{
    private static void Validate(PlayerInfernoState s,int frame,PlayerInfernoServices services)
    {
        ArgumentNullException.ThrowIfNull(s);ArgumentNullException.ThrowIfNull(services);
        if(frame<0||frame>int.MaxValue-20||s.Expires<0||s.Lease&&s.Expires==0||!s.Lease&&s.Expires!=0||s.Param1<0)
        {throw new ArgumentException("Unsupported player Inferno clock/state");}
        ArgumentNullException.ThrowIfNull(services.Allocate);ArgumentNullException.ThrowIfNull(services.Aim);
        ArgumentNullException.ThrowIfNull(services.Emit);ArgumentNullException.ThrowIfNull(services.Rewind);
    }
    internal static PlayerInfernoStep Start(PlayerInfernoState s,int frame,bool record,bool validMissile,bool used,
        int mana256,int threshold256,int range,PlayerInfernoServices services)
    {
        Validate(s,frame,services);
        if(mana256<0||threshold256<0){throw new ArgumentOutOfRangeException();}
        if(!record||!validMissile||!s.Active&&mana256<threshold256){return new(s,false);}
        if(!used){throw new NotSupportedException("Player Inferno start requires an actual used skill");}
        if(s.Lease)
        {
            services.Rewind();s=s with{Expires=checked(frame+6)};
            // Source renews first and returns1even if the nested do cannot aim.
            var step=Do(s,frame,record,validMissile,used,range,services);
            return new(step.State,true);
        }
        if(!services.Allocate()){return new(s,false);}
        return new(s with{Active=true,Lease=true,Expires=checked(frame+20),Param1=0},true);
    }
    internal static PlayerInfernoStep Do(PlayerInfernoState s,int frame,bool record,bool validMissile,bool used,
        int range,PlayerInfernoServices services)
    {
        Validate(s,frame,services);
        if(!record||!validMissile||!services.Aim()||!used){return new(s,false);}
        if(s.Param1!=0){services.Emit(Math.Max(1,range));}
        s=s with{Param1=1};
        // The player route rewinds even after its active bit is cleared. Do
        // not copy Blade Fury's active-bit-gated rewind or NPC duration branch.
        services.Rewind();return new(s,true);
    }
    internal static PlayerInfernoState Release(PlayerInfernoState s)=>s with{Active=false};
    internal static PlayerInfernoState Expire(PlayerInfernoState s)=>s with{Active=false,Lease=false,Expires=0,Flags=s.Flags|64u};
}

internal enum PlayerInfernoCostModel{LegacySource,CurrentTablePerInvocation}
internal sealed record PlayerInfernoResourceServices(Func<int> Mana256,Func<int,bool> Debit,PlayerInfernoServices Channel);

// Ordinary learned-player resource composition. Current-table cost selection
// fixes the supplementary old rank1zero control without guessing its frequency.
internal static class PlayerInfernoResources
{
    internal static SkillDefinition Row(CombatAbility ability)
    {
        if(ability is not(CombatAbility.Inferno or CombatAbility.ArcticBlast)){throw new NotSupportedException("Not a player Inferno skill");}
        var r=SkillMechanics.For(ability);bool cold=ability==CombatAbility.ArcticBlast;
        if(r.SourceId!=(cold?230:41)||r.Number("srvstfunc")!=11||r.Number("srvdofunc")!=19||r.Number("usemanaondo")!=0
            ||r.Number("seqnum")!=(cold?18:6)||r.Number("seqinput")!=10||r.Number("startmana")!=6
            ||r.Text("srvmissilea")!=(cold?"arcticblast1":"infernoflame1"))
        {throw new NotSupportedException("Player Inferno binding differs from pinned rows");}
        return r;
    }
    internal static SkillValues Values(CombatAbility ability,int rank)
    {
        if(rank is <1 or >255){throw new ArgumentOutOfRangeException(nameof(rank));}
        var row=Row(ability);return new(row,key=>key==row.SourceKey?rank:0,_=>0,1);
    }
    internal static int Cost256(CombatAbility ability,int rank,PlayerInfernoCostModel model)
    {
        if(!Enum.IsDefined(model)){throw new NotSupportedException("Player Inferno cost model must be explicit");}
        var values=Values(ability,rank);return model==PlayerInfernoCostModel.LegacySource&&rank==1?0:values.Mana256;
    }
    internal static PlayerInfernoResourceServices Player(PlayerMobile owner,PlayerInfernoServices channel)
        =>new(()=>NecromancerEffects.ManaBalance256(owner),cost=>NecromancerEffects.Pay(owner,cost),channel);
    internal static PlayerInfernoStep Start(PlayerInfernoState s,int frame,CombatAbility ability,int rank,
        PlayerInfernoCostModel model,PlayerInfernoResourceServices services)
    {
        var values=Values(ability,rank);int cost=Cost256(ability,rank,model);
        // Source ordinary eligibility uses the table even when the old rank1
        // consumer later debits zero. Preserve that control independently.
        if(services.Mana256()<values.Mana256){return new(s,false);}
        var step=PlayerInfernoChannel.Start(s,frame,true,true,true,services.Mana256(),Row(ability).Number("startmana")*256,
            Math.Max(1,checked((int)values.Eval("calc1"))),services.Channel);
        if(step.Success){services.Debit(cost);}return step;
    }
    internal static PlayerInfernoStep Do(PlayerInfernoState s,int frame,CombatAbility ability,int rank,PlayerInfernoResourceServices services)
    {
        var values=Values(ability,rank);
        // Current usemanaondo0/start11means the ordinary do wrapper neither
        // prechecks nor charges mana here. It does not establish client reentry.
        return PlayerInfernoChannel.Do(s,frame,true,true,true,Math.Max(1,checked((int)values.Eval("calc1"))),services.Channel);
    }
}
