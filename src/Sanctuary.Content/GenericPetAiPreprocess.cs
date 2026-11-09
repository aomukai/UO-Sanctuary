// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// These words are separate from the previously qualified three saved control
// words. Their birth values and persistence are not supplied by this adapter.
internal sealed class GenericPetAiSavedTarget(int id,int kind)
{internal int Id=id,Kind=kind;}
internal sealed class GenericPetAiPrePath(int room,int x,int y,int pattern,int active,ushort collision)
{internal int Room=room,X=x,Y=y,Pattern=pattern,Active=active;internal ushort Collision=collision;}
internal sealed record GenericPetAiPreUnit(GenericPetDecisionUnit Position,int ClassId,byte Selector,
    int Mode,uint Flags124,uint Flags128,int StateCount,bool HasRoot,int Root1c,uint Rootb30,
    GenericPetAiPrePath Path,GenericPetMovementUnit Movement);
internal sealed record GenericPetAiPreServices(Func<byte> Selector,Func<int,GenericPetAiPreUnit> Unit,
    Func<GenericPetAiControl,GenericPetAiSavedTarget> Memory,Func<int,byte?> PrimaryFlags,
    Func<byte,int,bool?> Walk,Func<int,int,int> Resolve,Func<int,int,int> Distance,
    Func<int,int,int,byte,int,ushort,int> Toward,Action<int,int,byte,ushort> TargetMove,
    Func<int,int,int,int,int,ushort> Collision,Func<int,int,int> DoorSearch,
    Func<byte,int,bool?> DoorMetadata,Func<int,int,bool> CanInteract,Action<int,int> Interact,Action<int,int> Idle);
internal static class GenericPetAiPreprocess
{
    // Complete43a6c0. Status21,door collision/search and saved-target handling
    // preserve their source order;native path/object services stay explicit.
    internal static bool Run(int actor,GenericPetAiTick tick,GenericPetAiPreServices services)
    {
        var unit=actor==0?null:services.Unit(actor);
        if(unit?.Position.Kind is 0 or 1 or 3)
        {
            if(unit.Selector is <1 or >3){throw new NotSupportedException("AI preprocessing requires selector1..3");}
            if(unit.StateCount>21&&unit.HasRoot&&unit.Root1c<0&&(unit.Rootb30&0x200000)!=0)
            {services.Idle(actor,3);return true;}
        }
        byte flags=services.PrimaryFlags(tick.RowA)??throw new NotSupportedException("AI preprocessing dereferences its primary row");
        if((flags&8)!=0)
        {
            if(unit==null||unit.Position.Kind==5){throw new NotSupportedException("Door preprocessing requires a non-tile unit");}
            var path=unit.Path??throw new NotSupportedException("Door preprocessing requires a path");
            if(path.Active==0){path.Collision=services.Collision(path.Room,unchecked((ushort)path.X),unchecked((ushort)path.Y),path.Pattern,0x7fff);}
            if((path.Collision&0x800)!=0)
            {
                int door=services.DoorSearch(actor,9);
                if(door!=0)
                {
                    var target=services.Unit(door);
                    bool usable=services.DoorMetadata(services.Selector(),target.ClassId)
                        ??throw new NotSupportedException("Door preprocessing dereferences object metadata");
                    if(usable)
                    {
                        if(services.CanInteract(actor,door)){services.Interact(actor,door);}
                        services.Idle(actor,5);return true;
                    }
                }
            }
        }
        return SavedTarget(actor,tick,services);
    }
    // Complete43a4f0. Reuse existing coordinate decoding and movement state;
    // no independent coordinate convention or movement executor is introduced.
    internal static bool SavedTarget(int actor,GenericPetAiTick tick,GenericPetAiPreServices services)
    {
        var memory=services.Memory(tick.Control)??throw new NotSupportedException("Saved-target preprocessing requires control memory");
        if(memory.Id==-1){return false;}
        int cls=actor==0?-1:services.Unit(actor).ClassId;
        int target=0;
        if(services.Walk(services.Selector(),cls)==true)
        {target=services.Resolve(memory.Kind,memory.Id);}
        if(target==0){memory.Id=-1;return false;}
        var candidate=services.Unit(target);
        bool invalid=(candidate.Flags124&0x10000)!=0||(candidate.Position.Kind switch
        {
            0=>candidate.Mode is 0 or 17,
            1=>candidate.Mode is 0 or 12,
            _=>(candidate.Flags128&0x4000000)!=0
        });
        if(invalid){memory.Id=-1;return false;}
        int distance=services.Distance(actor,target);
        if(distance>1)
        {
            if(distance<=20){return false;}
            var movement=Movement(actor,services);movement.Word20=7;movement.Byte28=40;
        }
        else
        {
            if(memory.Kind!=0){return false;}
            if(actor!=0)
            {
                Movement(actor,services).Byte28=19;
                var(x,y)=services.Unit(target).Position.Coordinates();
                if(services.Toward(actor,x,y,19,0,4)!=0){return true;}
            }
        }
        services.TargetMove(actor,target,19,0);return true;
    }
    private static GenericPetMovementUnit Movement(int actor,GenericPetAiPreServices services)
    {
        var unit=actor==0?null:services.Unit(actor);
        if(unit?.Position.Kind!=1||unit.Movement is not{HasMonsterData:true,HasMovementData:true} movement)
        {throw new NotSupportedException("Saved-target preprocessing dereferences monster movement data");}
        return movement;
    }
}
