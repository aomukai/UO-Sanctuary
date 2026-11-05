// SPDX-License-Identifier: GPL-3.0-or-later
namespace Sanctuary;

internal abstract class ItemStatUpdateServices<T> where T:class
{
    internal abstract int Kind(T unit);
    internal abstract int Class(T unit);
    internal abstract bool PlayerBody(T unit);
    internal abstract bool IsType(T item,string type);
    internal abstract bool BaseRecord(T item);
    internal abstract bool GemRecord(T item);
    internal abstract int Quality(T item);
    internal abstract bool Broken(T item);
    internal abstract bool DynamicPath(T unit);
    internal abstract T MainWeapon(T unit);
    internal abstract T Owner(T item);
    internal abstract void InvalidItem();
    internal abstract void GemProperties(int mode,T owner,T item,T target);
    internal abstract void Sets(T unit,T item,bool remove);
    internal abstract void Books(T unit,T item,bool add);
    internal abstract void Merge(T unit,T item,bool damage);
    internal abstract void Expire(T unit,T item);
    internal abstract void UpdateRate(T unit);
    internal abstract void ClampVitals(T unit);
}

// Complete older item-stat update/removal callers and CanDualWield policy.
// Gem/set/player/world/native callbacks are services,not silently implemented
// by the NPC graph. Current Warlock player hand rules remain separate.
internal static class SourceItemStatUpdates
{
    internal static bool CanDualWield(int kind,int sourceClass)=>kind==0&&sourceClass is 4 or 6||kind==1&&sourceClass is 1 or 2;
    private static bool Dual<T>(ItemStatUpdateServices<T> s,T unit)where T:class
    {int kind=s.Kind(unit);return kind is 0 or 1&&CanDualWield(kind,s.Class(unit));}
    internal static void Update<T>(ItemStatUpdateServices<T> s,T item,T unit,bool force)where T:class
    {
        if(unit==null||item==null){return;}
        if(s.Kind(item)!=4){s.InvalidItem();return;}
        if(s.Kind(unit)==0&&s.PlayerBody(unit)){return;}
        if(s.IsType(item,"gem"))
        {
            if(s.Kind(unit)!=4||!s.BaseRecord(item)||!s.GemRecord(item)){return;}
            s.GemProperties(2,null,item,unit);
        }
        else if(s.IsType(item,"rune"))
        {
            if(s.Kind(unit)!=4||!s.BaseRecord(item)||!s.GemRecord(item)){return;}
            s.GemProperties(5,unit,item,unit);
        }
        else if(s.Quality(item)==5){s.Sets(unit,item,false);}
        if(s.Kind(unit)==0){s.Books(unit,item,true);}
        T main=null;if(s.Kind(unit)==0){main=s.MainWeapon(unit);}
        bool damage=true;
        if(Dual(s,unit)&&item!=main&&s.IsType(item,"weap")){damage=false;}
        if(!s.Broken(item)||force){s.Merge(unit,item,damage);}
        if(s.DynamicPath(unit)){s.UpdateRate(unit);}
    }
    internal static void Transferred<T>(ItemStatUpdateServices<T> s,T item,T target,bool force,bool updateVitals)where T:class
    {
        if(target==null||item==null||s.Kind(item)!=4||s.Kind(target)==0&&s.PlayerBody(target)||target!=s.Owner(item)){return;}
        if(s.Quality(item)==5){s.Sets(target,item,true);}
        T main=null;if(s.Kind(target)==0){main=s.MainWeapon(target);}
        if(!s.Broken(item)||force){s.Expire(target,item);}
        if(Dual(s,target)&&main!=null&&s.IsType(item,"weap")&&main!=item
            &&(!s.Broken(main)||force)&&s.Owner(main)!=null){s.Merge(target,main,true);}
        if(s.Kind(target)==0){s.Books(target,item,false);}
        if(updateVitals){s.ClampVitals(target);}
    }
}
