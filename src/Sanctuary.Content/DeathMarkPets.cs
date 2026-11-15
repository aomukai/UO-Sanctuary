// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
namespace Sanctuary;

internal sealed class DeathMarkControlList(int identity,int state,int flags,DeathMarkControlList next=null)
{
    internal readonly int Identity=identity,State=state;
    internal int Flags=flags;
    internal DeathMarkControlList Next=next;
}
internal sealed class DeathMarkControlRoot(int flags,DeathMarkControlList first=null,DeathMarkControlList second=null)
{
    internal int Flags=flags;
    internal DeathMarkControlList First=first,Second=second;
}
internal sealed record DeathMarkPetContext(bool Present,short Overlay,byte UnitSelector,byte GameSelector);
internal sealed record DeathMarkPetServices(Func<Point2D,int> Place,Action<short> Overlay,Action<int,int> TargetCommand,Action<int> Idle,
    Func<DeathMarkControlRoot> ControlRoot,Action<byte,DeathMarkControlList> Unlink,Action<byte,DeathMarkControlList> Free);

// Complete current callback44aa70 over explicit placement/command/stat-list
// services. The source reloads the pet's root before each requested state;
// unlink may mutate its lists and flags before the conditional free.
internal static class DeathMarkPets
{
    private static DeathMarkControlList Find(DeathMarkControlList node,int state)
    {for(;node!=null;node=node.Next){if(node.State==state){return node;}}return null;}
    internal static void Run(DeathMarkPetContext c,DeathMarkDirective directive,DeathMarkPetServices s)
    {
        if(c is not{Present:true}||directive==null||directive.Remaining<=0){return;}
        if(s.Place(new(directive.X,directive.Y))==0){return;}
        s.Overlay(c.Overlay);
        if(directive.TargetType!=0){s.TargetCommand(directive.TargetType,directive.TargetId);}
        s.Idle(1);
        if(directive.ClearControl)
        {
            foreach(int state in new[]{21,1,11})
            {
                var root=s.ControlRoot();if(root==null||root.Flags>=0){continue;}
                var list=Find(root.First,state)??Find(root.Second,state);if(list==null){continue;}
                s.Unlink(c.UnitSelector,list);if(list.Flags>=0){s.Free(c.GameSelector,list);}
            }
        }
        directive.Remaining=unchecked(directive.Remaining-1);
    }
    internal static DeathMarkPetServices BindPlacement(DeathMarkPetServices services,GenericPetMonsterPlacementUnit unit,GenericPetMonsterPlacementServices placement)
        =>services with{Place=point=>GenericPetMonsterPlacement.Place(unit,0,point.X,point.Y,0,placement)};
}

// Shared manager-backed state lists for original and bound native bodies.
// Do not clear unrelated fear, blindness, poison or untagged UO controls.
internal static class DeathMarkControlStates
{
    internal static void Require(Mobile body,DeathMarkDirective directive,DateTime at)
    {
        if(directive.ClearControl&&NamedControl.State(body)is{Freeze:null} legacy&&legacy.FreezeUntil>at)
        {throw new NotSupportedException("Legacy untagged freeze has no qualified source list");}
    }
    internal static DeathMarkControlRoot Root(Mobile body,DateTime at)
    {
        var lists=new List<DeathMarkControlList>();
        if(CombatStuns.Remaining(body)>0){lists.Add(new(21,21,-1));}
        if(TimedEffects.TryActor(body,out var actor))
        {
            if(actor.Control is{Freeze:{Removed:false}} control&&control.FreezeUntil>at){lists.Add(new(1,1,-1));}
            if(actor.Cold!=null&&actor.ChillUntil>at){lists.Add(new(11,11,-1));}
        }
        for(int i=1;i<lists.Count;i++){lists[i-1].Next=lists[i];}
        return new(-1,lists.FirstOrDefault());
    }
    internal static void Remove(Mobile body,DeathMarkControlList list,DateTime at)
    {
        switch(list.State)
        {case 21:CombatStuns.Clear(body);break;case 1:FreezeStates.Remove(body,at);break;case 11:ColdStates.Remove(body,at);break;
            default:throw new NotSupportedException("Death Mark attempted an unrelated control state");}
        list.Flags=0; // Selected manager owns removal;no raw allocator is emulated.
    }
}
