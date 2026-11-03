// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// The actual monster relocation caller always forces removal (argument1).
// Other unit kinds and conditional corpse removal are deliberately separate.
internal static class GenericPetMonsterPlacementHooks
{
    private static GenericPetFootprintRequest Request(GenericPetCommitPath path)
    {
        if(path?.Actor is not{Kind:1}){throw new NotSupportedException("Placement collision hooks require a monster dynamic path");}
        return new(path.Room,(ushort)(path.X>>16),(ushort)(path.Y>>16),path.Size5c,path.Mask60);
    }
    internal static GenericPetMonsterPlacementServices Bind(GenericPetMonsterPlacementServices placement,GenericPetCommitPath path,GenericPetCollisionFootprintServices footprints)
        =>placement with
        {
            BeforeSearch=force=>
            {
                if(force!=1){throw new NotSupportedException("Only the actual forced monster-placement removal is bound");}
                GenericPetCollisionFootprints.RemoveSize(Request(path),footprints);
            },
            AfterSearch=()=>GenericPetCollisionFootprints.AddSize(Request(path),footprints)
        };
}
