// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

internal sealed record GenericPetSearchUnit(GenericPetDecisionUnit Position,int Mode,uint Flags124,int ClassId,byte Selector445,
    bool HasMonsterAi,bool MonsterAiFlag8,int GridX0,int GridY4);
internal sealed record GenericPetSearchRoom(int Type74,uint Flags50,int Level);
internal sealed record GenericPetSearchSwarm(int Target,int Count8);
internal sealed class GenericPetSearchCell
{internal int Count,Distance,Target;}
internal sealed class GenericPetSearchContext
{
    internal byte GameSelector;
    internal int Mask,Owner,Bias;
    internal GenericPetParameters Parameters;
    internal int Target20,Target28,Target30,Target38,Target40;
    internal GenericPetSearchCell[] Grid;
    internal IReadOnlyList<GenericPetStateGroup> Groups;
    internal IReadOnlyList<GenericPetSearchSwarm> Swarm=[];
    internal int Count58,Count5c,Count60,Count64;
}
internal sealed record GenericPetSearchServices(Func<int,GenericPetSearchUnit> Unit,Func<int,int,byte,int> Valid372ec0,
    Func<int,int,int,int> Distance,Func<int,GenericPetSearchRoom> Room,Func<int,int,int,int> Collision27c710,
    Func<byte,int,byte?> LevelByte63,Func<int,int,int> Predicate40c450,Func<int,int> Predicate27ddb0,
    Func<int,int> Value3000,Func<byte,int,byte?> MonsterByte137,Func<int,int,bool> HasState);
internal static class GenericPetSearch
{
    internal static IReadOnlyList<GenericPetStateGroup> InlineGroups(GenericPetSearchStates prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if(prepared.Groups.Count>10){throw new NotSupportedException("Search caller group storage exceeds verified inline capacity");}
        foreach(var group in prepared.Groups)
        {if(group.States.Count>10){throw new NotSupportedException("Search caller state storage exceeds verified inline capacity");}}
        return prepared.Groups;
    }
    internal static int Squared(GenericPetDecisionUnit unit,int x,int y)
    {var(px,py)=unit.Coordinates();unchecked{int dx=px-x,dy=py-y;return dx*dx+dy*dy;}}
    private static bool Dead(GenericPetSearchUnit u)=>(u.Flags124&0x10000)!=0||u.Mode==0||u.Mode==(u.Position.Kind==1?12:17);
    private static (float X,float Y) GridPoint(GenericPetSearchUnit u)
    {
        if(u.Position.Kind is 2 or 4 or 5)
        {var(x,y)=u.Position.Coordinates();return ((float)(uint)x,(float)(uint)y);}
        return u.Position.HasPath?((float)u.GridX0*(1f/65536f),(float)u.GridY4*(1f/65536f)):(0,0);
    }
    private static int Truncate(float value)=>float.IsNaN(value)||value>=2147483648f||value< -2147483648f?int.MinValue:(int)value;
    internal static int Visit(GenericPetSearchContext c,int pet,int target,IList<GenericPetCombatCandidate> rows,GenericPetSearchServices s)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(s);
        if(c.Parameters==null||pet==0||target==0||pet==target){return 0;}
        var u=s.Unit(target);if((uint)u.Position.Kind>1||s.Valid372ec0(pet,target,1)==0){return 0;}
        int D(int origin,int unit){var(x,y)=s.Unit(unit).Position.Coordinates();return s.Distance(origin,x,y);}
        unchecked
        {
            int distance=D(pet,target),ownerDistance=c.Owner!=0?D(c.Owner,target):0;
            bool far=distance>1024;if(far&&c.Owner!=0&&ownerDistance>1024){return 0;}
            bool dead=Dead(u),flag4=(u.Flags124&4)!=0,canRanged=false,canMelee=false;
            if(!far&&c.Mask!=0)
            {
                if(dead){if(u.Position.Kind!=1||(c.Mask&8)==0){return 0;}}
                else if(!flag4){return 0;}
                canRanged=true;
                var room=s.Room(pet);
                bool shortcut=room is{}&&(room.Type74==1||room.Type74==2&&(room.Flags50&0x80000)!=0);
                if(!shortcut&&u.Position.Kind==1)
                {
                    if(!u.HasMonsterAi){throw new NotSupportedException("Current search requires monster AI data on this path");}
                    shortcut=u.MonsterAiFlag8;
                }
                if(shortcut||u.Position.Kind!=1){canMelee=canRanged=true;}
                else
                {
                    if((c.Mask&0x2e)!=0){canRanged=s.Collision27c710(pet,target,4)==0;}
                    canMelee=true;
                    if((c.Mask&9)!=0)
                    {
                        var secondRoom=s.Room(pet);byte? level=s.LevelByte63(c.GameSelector,secondRoom?.Level??0);
                        canMelee=level.HasValue&&level.Value!=0&&distance>level.Value*level.Value
                            ?s.Predicate40c450(pet,target)!=0:s.Collision27c710(pet,target,4)==0;
                    }
                }
            }
            if(dead)
            {
                if(!canMelee&&!canRanged||u.Position.Kind!=1||(c.Mask&8)==0){return 0;}
                if(c.Target38!=0){int old=D(pet,c.Target38),current=D(pet,target);if(current>=old){return 0;}}
                if(s.Predicate27ddb0(target)!=0){c.Target38=target;}return 0;
            }
            if(!flag4||far){return 0;}
            if(c.Owner!=0)
            {
                if(ownerDistance<100){c.Count58++;}
                if((c.Mask&16)!=0&&(c.Target30==0||ownerDistance<D(c.Owner,c.Target30))){c.Target30=target;}
            }
            c.Count64++;if(distance<100){c.Count5c++;}if(distance<25){c.Count60++;}
            var p=c.Parameters;
            if(c.Mask!=0&&(p.Flags&1)==0)
            {
                int value=s.Value3000(target);
                if(value>0)
                {
                    int Bonus(int unit)
                    {
                        var info=s.Unit(unit);return info.Position.Kind==0?14:info.Position.Kind==1?s.MonsterByte137(info.Selector445,info.ClassId)??0:0;
                    }
                    value+=Bonus(target);int previous=0;
                    if(c.Target40!=0){int bonus=Bonus(c.Target40);previous=s.Value3000(c.Target40)+bonus;}
                    if(value>previous){c.Target40=target;}
                }
            }
            if(canMelee&&(c.Mask&1)!=0)
            {
                bool replace=c.Target20==0;
                if(!replace&&c.Target20!=c.Bias)
                {
                    int old=D(pet,c.Target20),current=D(pet,target);
                    if(current>=p.MeleeMinSquared&&current<=p.MeleeMaxSquared&&(target==c.Bias||target==c.Target40)){replace=true;}
                    else if(current<old)
                    {
                        replace=true;
                        if(p.SwarmRaw>0)
                        {foreach(var entry in c.Swarm){if(entry.Target!=target){continue;}replace=entry.Count8<=p.SwarmRaw;break;}}
                    }
                }
                if(replace){c.Target20=target;}
            }
            if(!canRanged){return 0;}
            if((c.Mask&2)!=0)
            {
                bool replace=c.Target28==0;
                if(!replace&&c.Target28!=c.Bias)
                {
                    int old=D(pet,c.Target28),current=D(pet,target);
                    bool wasWithin=old>=p.RangedMinSquared&&old<=p.RangedMaxSquared;
                    replace=current>=p.RangedMinSquared&&current<=p.RangedMaxSquared
                        &&(target==c.Bias||target==c.Target40||!wasWithin||current<old);
                }
                if(replace){c.Target28=target;}
            }
            if(c.Grid!=null&&(c.Mask&4)!=0)
            {
                if(c.Grid.Length!=64){throw new NotSupportedException("Current search requires an8x8grid");}
                var(tx,ty)=GridPoint(u);var(px,py)=GridPoint(s.Unit(pet));
                int x=Math.Clamp(Truncate(px-tx)/8+4,0,7),y=Math.Clamp(Truncate(py-ty)/8+4,0,7);
                var cell=c.Grid[y+x*8];cell.Count++;if(target==c.Bias){cell.Count++;}
                if(cell.Target==0||distance<D(pet,cell.Target)){cell.Distance=distance;cell.Target=target;}
            }
            if(c.Groups!=null&&(c.Mask&32)!=0)
            {
                ArgumentNullException.ThrowIfNull(rows);
                foreach(var group in c.Groups)
                {
                    int previous=rows[group.Slot].Target;if(previous!=0&&distance>=D(pet,previous)){continue;}
                    bool present=false;foreach(int state in group.States){if(s.HasState(target,state)){present=true;break;}}
                    if(!present){rows[group.Slot]=rows[group.Slot] with{Target=target};}
                }
            }
            return 0;
        }
    }
}
