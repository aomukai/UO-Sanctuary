// SPDX-License-Identifier: GPL-3.0-or-later
namespace Sanctuary;

// Shared older selector and damage-list entry/restore callers. Inventory
// primary/secondary identity and used-skill state are explicit services.
// This does not extend the old player class policy to modern Warlock hands.
internal abstract class SourceWeaponServices<T> where T:class
{
    internal abstract bool Inventory(T actor);
    internal abstract bool Dual(T actor);
    internal abstract T Equipped(T actor);
    internal abstract T Primary(T actor);
    internal abstract T Secondary(T actor);
    internal abstract T Composite(T actor,int part);
    internal abstract bool Skill(T actor);
    internal abstract bool SkillRow(T actor);
    internal abstract int Selection(T actor);
    internal abstract int SequenceFrame(T actor);
    internal abstract uint SkillFlags(T actor);
    internal abstract bool Weapon(T item);
    internal abstract bool Equippable(T item);
    internal abstract T Owner(T item);
    internal abstract void Merge(T actor,T item,bool damage);
}
internal static class SourceWeaponSelection
{
    internal static bool Equippable(bool present,int kind,bool hasData,uint flags)=>present&&(kind!=4||!hasData||(flags&0x4100)==0);
    internal static T Select<T>(SourceWeaponServices<T> s,T actor)where T:class
    {
        if(actor==null||!s.Inventory(actor)){return null;}
        var equipped=s.Equipped(actor);
        if(!s.Dual(actor)||!s.Skill(actor)||!s.SkillRow(actor)){return equipped;}
        T weapon=null;
        switch(s.Selection(actor))
        {
            case 2:
                weapon=s.Composite(actor,5);var other=s.Composite(actor,6);
                if(weapon==null||!s.Weapon(weapon)||other!=null&&s.Weapon(other)&&(s.SkillFlags(actor)&0x2000)!=0){weapon=other;}
                break;
            case 3:weapon=s.Composite(actor,((s.SequenceFrame(actor)>>8)%2)!=0?6:5);break;
            case 4:equipped=null;break;
            case 1:weapon=s.Composite(actor,6);break;
            default:
                weapon=s.Composite(actor,5);if(weapon==null||!s.Weapon(weapon)){weapon=s.Composite(actor,6);}break;
        }
        return s.Weapon(weapon)&&s.Equippable(weapon)?weapon:equipped;
    }
    internal static void Enter<T>(SourceWeaponServices<T> s,T actor,bool reset)where T:class
    {
        if(reset)
        {
            var first=s.Primary(actor);if(first!=null&&s.Owner(first)!=null){s.Merge(actor,first,false);}
            var second=s.Secondary(actor);if(second!=null&&second!=first&&s.Owner(second)!=null){s.Merge(actor,second,false);}
        }
        if(!s.Dual(actor)){return;}
        var weapon=Select(s,actor);var primary=s.Primary(actor);
        if(weapon!=null)
        {
            if(weapon==primary)
            {
                var second=s.Secondary(actor);
                if(s.Owner(primary)!=null){s.Merge(actor,primary,true);}
                if(second!=null&&s.Owner(second)!=null){s.Merge(actor,second,false);}
            }
            else
            {
                if(s.Owner(weapon)!=null){s.Merge(actor,weapon,true);}
                if(primary!=null&&s.Owner(primary)!=null){s.Merge(actor,primary,false);}
            }
        }
        else if(primary!=null&&s.Owner(primary)!=null){s.Merge(actor,primary,true);}
    }
    internal static void RestorePrimary<T>(SourceWeaponServices<T> s,T actor,bool enabled)where T:class
    {
        if(!enabled){return;}
        var first=s.Primary(actor);if(first!=null&&s.Owner(first)!=null){s.Merge(actor,first,true);}
        var second=s.Secondary(actor);if(second!=null&&second!=first&&s.Owner(second)!=null){s.Merge(actor,second,false);}
    }
}
