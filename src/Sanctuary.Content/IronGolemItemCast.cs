// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

internal abstract class IronGolemTargetServices
{
    internal abstract bool ResolveTarget();
    internal abstract int TargetType();
    internal abstract int TargetMode();
    internal abstract bool Metal();
    internal abstract bool Identified();
    internal abstract bool HasStatOwner();
}
internal sealed record IronGolemSummonRequest(int Monster,int Mode,int PetType,int Maximum,Point2D Point)
{internal int Flags=>1;internal int AiSpecialState=>0;}
internal abstract class IronGolemCastServices:IronGolemTargetServices
{
    internal abstract void SetDoFlag();
    internal abstract bool HasSkill(int skill);
    internal abstract (int Monster,int Mode) ResolveSummon(int skill,int rank);
    internal abstract byte PetType();
    internal abstract int PetTypeCount();
    internal abstract int PetMaximum(int skill,int rank);
    internal abstract Point2D TargetPoint();
    internal abstract bool Create(IronGolemSummonRequest request);
    internal abstract void DeleteGroundUnit();
    internal abstract void FreeCollision();
    internal abstract void RemoveFromRoom();
    internal abstract (byte First,byte Second) BodyLocations();
    internal abstract void SetCursorMode();
    // Source do57ignores the equip return. The real actor adapter must reject
    // unsupported inventory contexts before invoking the canonical cast path.
    internal abstract void EquipOriginal(byte firstBodyLocation);
    internal abstract void BaseStats(int rank);
    internal abstract void PassiveStats(int skill,int rank);
    internal abstract void Resistance();
    internal abstract void UpdateClient();
    internal abstract void UpdateAi();
}
internal static class IronGolemItemCast
{
    internal static bool Eligible(IronGolemTargetServices s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return s.ResolveTarget()&&s.TargetType()==4&&s.TargetMode()==3&&s.Metal()&&s.Identified()&&!s.HasStatOwner();
    }
    internal static bool Release(int skill,int rank,IronGolemCastServices s)
    {
        if(!Eligible(s)){return false;}
        s.SetDoFlag();
        if(!s.HasSkill(skill)){return false;}
        var summon=s.ResolveSummon(skill,rank);
        if(summon.Monster<0){return false;}
        int type=s.PetType();if(type>=s.PetTypeCount()){type=0;}
        int maximum=s.PetMaximum(skill,rank);
        var request=new IronGolemSummonRequest(summon.Monster,summon.Mode,type,maximum,s.TargetPoint());
        if(!s.Create(request)){return false;}
        s.DeleteGroundUnit();s.FreeCollision();s.RemoveFromRoom();
        var slots=s.BodyLocations();s.SetCursorMode();s.EquipOriginal(slots.First);
        s.BaseStats(rank);s.PassiveStats(skill,rank);s.Resistance();s.UpdateClient();s.UpdateAi();return true;
    }
}
