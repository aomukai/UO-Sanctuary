// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record GenericPetOwnerLossUnit(GenericPetDecisionUnit Position,int ClassId,int Mode,uint Flags124,
    bool HasData,bool HasAi,int OwnerGame,int OwnerKind,int OwnerId,int Ai90,float Facing80);
internal sealed record GenericPetOwnerLossServices(Func<byte> Selector,Func<int,GenericPetOwnerLossUnit> Unit,
    Func<byte,int,bool?> Killable,Func<int,int,int,int> Resolve,Action<int,int> Detach35c970,
    Action<int,int> Notify333e10,Action<int> Clear333c20,Action<int,int> Credit3969f0,
    Func<int,int,GenericPetModeRequest> InitializeMode,Func<int,int,int,int> Direction,
    Action<GenericPetModeRequest,byte,int> ExecuteMode,Action<int,int> Notify35f7d0,
    Func<byte,int,GenericPetMonster2Metadata> Secondary,Action<int,int> Transform392aa0);
internal static class GenericPetOwnerLoss
{
    internal static GenericPetAiOverrideServices Bind(GenericPetAiOverrideServices original,GenericPetOwnerLossServices loss)
        =>original with{MissingOwner=(actor,killer,detach)=>Run(actor,killer,detach,loss)};
    // Complete706-byte392be0. This is source death orchestration,not a blanket
    // native Delete/Kill adapter. Side effects and mode execution remain explicit.
    internal static void Run(int actor,int killer,int detach,GenericPetOwnerLossServices services)
    {
        if(actor==0){return;}var initial=services.Unit(actor);bool wasHigh=false;
        if(initial.Position.Kind==1)
        {
            if(initial.Mode is 0 or 12||services.Killable(services.Selector(),initial.ClassId)!=true){return;}
            var current=services.Unit(actor);int owner=0;
            if(current is{Position.Kind:1,HasData:true,HasAi:true,OwnerGame:not 0})
            {owner=services.Resolve(current.OwnerGame,current.OwnerKind,current.OwnerId);}
            if(detach!=0&&owner!=0&&services.Unit(owner).Position.Kind==0)
            {wasHigh=(services.Unit(actor).Flags124&0x80000000)!=0;services.Detach35c970(owner,actor);}
            current=services.Unit(actor);
            if(current is{Position.Kind:1,HasData:true,HasAi:true,Ai90:not 0})
            {services.Notify333e10(actor,0x1722cd0);services.Clear333c20(actor);}
        }
        else if(initial.Position.Kind==0){if(initial.Mode is 0 or 17){return;}}
        else{return;}
        if(killer!=0&&(services.Unit(actor).Flags124&0x4000000)==0){services.Credit3969f0(killer,actor);}
        if(services.Unit(actor).Position.Kind!=1){return;}
        var request=services.InitializeMode(actor,0)??throw new NotSupportedException("Owner-loss death requires its initialized mode request");
        byte facing;
        if(killer!=0)
        {var(x,y)=services.Unit(killer).Position.Coordinates();facing=unchecked((byte)services.Direction(actor,x,y));}
        else
        {
            var current=services.Unit(actor);
            if(current.Position.Kind==5){throw new NotSupportedException("Current death-facing path rejects tile units");}
            float raw=current.Position.HasPath?current.Facing80:0;
            // Match cvttss2si including its indefinite integer;only AL is used.
            int value=float.IsNaN(raw)||raw>=2147483648f||raw< -2147483648f?int.MinValue:(int)raw;
            facing=unchecked((byte)value);
        }
        services.ExecuteMode(request with{Target=killer},facing,1);
        if((services.Unit(actor).Flags124&0x80000000)==0&&!wasHigh){services.Notify35f7d0(actor,killer);}
        var row=services.Secondary(services.Selector(),services.Unit(actor).ClassId);
        if(row==null||(row.Flags6&4)==0){return;}
        int cls=services.Unit(actor).ClassId;
        if(cls is 432 or 433){services.Transform392aa0(actor,cls==432?571:572);}
    }
}
