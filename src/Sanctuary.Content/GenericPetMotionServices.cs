// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Neutral layout fields from the captured current moving predicate.
internal sealed record GenericPetMovingUnit(GenericPetDecisionUnit Unit,int Mode,bool HasAux100,bool HasAux18,uint AuxFlags14,byte PathByte48);
internal static class GenericPetMotionServices
{
    // Pinned image163a3f0 player dispatch identity:0other,1moving,2conditional.
    private static ReadOnlySpan<byte> PlayerModes=>[0,0,1,1,0,0,1,2,2,0,2,2,2,2,2,2,2,0,2,1];
    // Pinned image1723f90,three signed words for each of16monster modes.
    private static ReadOnlySpan<int> MonsterModes=>[0,1,0,0,1,0,1,0,0,0,1,0,0,0,1,0,0,1,0,1,0,0,0,1,0,0,1,0,0,1,0,0,1,0,0,1,0,1,0,1,0,0,1,0,0,1,0,0];
    private static int MonsterMode(int mode,Func<int,byte?> byteAt)
    {
        if(mode>=16){return 0;}
        if(mode<0){throw new NotSupportedException("Current negative monster mode reads outside the verified table");}
        int offset=mode*3;if(MonsterModes[offset]!=0){return 1;}
        if(MonsterModes[offset+1]!=0||MonsterModes[offset+2]==0){return 0;}
        return (byteAt(mode>>3)??0)&(1<<(mode&7));
    }
    internal static bool Moving(GenericPetMovingUnit source,Func<int,byte?> monsterByte)
    {
        if(source?.Unit==null){throw new NotSupportedException("Current moving predicate requires a unit");}
        var unit=source.Unit;bool candidate=true;
        if(unit.Kind==1)
        {candidate=source.HasAux100&&source.HasAux18&&(source.AuxFlags14&1)!=0||MonsterMode(source.Mode,monsterByte)!=0;}
        else if(unit.Kind==0)
        {
            if((uint)source.Mode>=PlayerModes.Length){throw new NotSupportedException("Current player mode outside verified dispatch table");}
            int mode=PlayerModes[source.Mode];candidate=mode==1||mode==2&&source.Mode!=13&&source.HasAux100&&source.HasAux18&&(source.AuxFlags14&1)!=0;
        }
        if(unit.Kind==5||!unit.HasPath){throw new NotSupportedException("Current moving predicate requires a non-tile path");}
        return candidate&&(source.PathByte48&0x20)!=0;
    }
    internal static int Toward(GenericPetMovementUnit pet,GenericPetDecisionUnit coordinates,GenericPetActionRandom random,
        int x,int y,byte step,int hint,ushort flags,GenericPetMovementServices movement)
    {
        if(pet==null){return 0;}
        if(coordinates==null){throw new NotSupportedException("Missing source movement coordinates");}
        var(px,py)=coordinates.Coordinates();
        // The current helper deliberately compares unsigned32 coordinates.
        // Equality follows the negative-step branch, including step zero.
        int nextX=unchecked(px+((uint)x<(uint)px?step:-step));
        int nextY=unchecked(py+((uint)y<(uint)py?step:-step));
        return GenericPetMovement.Execute(pet,random,hint==1?15:2,0,nextX,nextY,1,flags,movement);
    }
    internal static GenericPetFollowExecutionServices Bind(GenericPetFollowExecutionServices leaves,GenericPetMovementUnit pet,
        GenericPetDecisionUnit coordinates,GenericPetActionRandom random,Func<int> frame,GenericPetMovementServices movement)
        =>GenericPetFollowMovement.Bind(leaves,pet,random,frame,movement,
            (x,y,step,hint,flags)=>Toward(pet,coordinates,random,x,y,step,hint,flags,movement));
}
