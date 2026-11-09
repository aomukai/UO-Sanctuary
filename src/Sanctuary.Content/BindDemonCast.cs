// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Views of fields used by current do158. These are not new live controllers or
// saved formats; target eligibility and the binding helper remain mandatory.
internal sealed record BindDemonSkill(short StateA0);
internal sealed class BindDemonControl(ushort flags38,GenericPetOwnerControl owner)
{internal ushort Flags38=flags38;internal GenericPetOwnerControl Owner=owner;}
internal sealed class BindDemonData(ushort flags30,int field74,BindDemonControl control)
{internal ushort Flags30=flags30;internal int Field74=field74;internal BindDemonControl Control=control;}
internal sealed class BindDemonUnit(int kind,uint flags124,int field12c,GenericPetPathTarget path,BindDemonData data,int firstC0)
{
    internal int Kind=kind,Field12c=field12c,FirstC0=firstC0;
    internal uint Flags124=flags124;
    internal GenericPetPathTarget Path=path;
    internal BindDemonData Data=data;
}
internal sealed class BindDemonCounter(int value){internal int Value=value;}
internal sealed record BindDemonCastServices(Func<int,GenericPetAiGame> Game,Func<byte,int,BindDemonSkill> Skill,
    Func<int,BindDemonUnit> Unit,Action<int,int> Refresh,Func<byte,int> StateCount,
    Func<int,int,int,int,bool> Eligible,Action<int> Stop333830,Action<int> Stop3333e0,
    Func<int,int,BindDemonCounter> Counter,Func<int,int> Value24eb30,Action<int,int,int> Transfer35f7d0,
    Func<int,int> NextEvent,Action<int> FreeEvent,Func<int,int,int,int> Resolve,Action<int> Stop333270,
    Func<int,int,int,int,int,int,bool,bool> Bind44b3b0,Action<int> UpdateRoom);
internal static class BindDemonCast
{
    // Complete709byte44bc50,dispatcher-linked do158. External effects are
    // explicit; a successful return does not certify eligibility or binding.
    internal static int Run(int game,int caster,int skill,int rank,BindDemonCastServices services)
    {
        if(game==0){throw new NotSupportedException("Bind Demon requires its game");}
        var row=services.Skill(services.Game(game).Selector106,skill);if(row==null){return 0;}
        if(caster==0){services.Refresh(game,0);throw new NotSupportedException("Bind Demon dereferences a missing caster");}
        var source=services.Unit(caster);source.Flags124|=64;services.Refresh(game,caster);
        source=services.Unit(caster);
        if(source.Kind==5){throw new NotSupportedException("Bind Demon requires a non-tile caster");}
        int target=source.Path?.Target??0;if(target==0||target==caster){return 0;}
        if(row.StateA0<0){return 0;}
        byte selector=services.Game(game).Selector106;
        if(selector is <1 or >3){throw new NotSupportedException("Bind Demon state lookup requires selector1..3");}
        if(row.StateA0>=services.StateCount(selector)){return 0;}
        if(!services.Eligible(game,caster,target,skill)){return 1;}
        var unit=services.Unit(target);var control=unit.Kind==1?unit.Data?.Control:null;
        if(control==null){throw new NotSupportedException("Eligible Bind Demon target requires monster AI control");}
        ushort flags=control.Flags38;
        if((flags&1)!=0){if((flags&2)!=0){services.Stop333830(target);}else{services.Stop3333e0(target);}}
        unit=services.Unit(target);
        if(!(unit.Kind==1&&unit.Data is{} present&&(present.Flags30&2)!=0))
        {
            int index=unit.Kind==1?unit.Data?.Field74??0:0;
            var counter=services.Counter(game,index);int value=services.Value24eb30(target);
            if(value==0&&counter!=null){counter.Value=unchecked(counter.Value+1);}
        }
        services.Transfer35f7d0(game,target,caster);
        int node=services.Unit(target).FirstC0;
        while(node!=0){int next=services.NextEvent(node);services.FreeEvent(node);node=next;}
        unit=services.Unit(target);bool monster=unit.Kind==1;unit.FirstC0=0;
        var owner=monster?unit.Data?.Control?.Owner:null;
        if(owner is{Game:not 0})
        {
            int found=services.Resolve(owner.Game,owner.Type,owner.Id);
            if(found!=0&&services.Unit(found).Kind==1)
            {if(found==target){services.Stop3333e0(target);}else{services.Stop333270(target);}}
        }
        int context=services.Unit(caster).Field12c;
        if(services.Bind44b3b0(game,caster,target,skill,rank,context,true))
        {
            unit=services.Unit(target);unit.Flags124|=0x800;
            if(unit.Kind==1&&unit.Data!=null){unit.Data.Flags30|=8;}
            services.UpdateRoom(target);
        }
        return 1;
    }
}
