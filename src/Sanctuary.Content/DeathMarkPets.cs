// SPDX-License-Identifier: GPL-3.0-or-later
using System;
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
