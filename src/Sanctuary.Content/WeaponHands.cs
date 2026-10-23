// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record WeaponPropertyRule(string Code,bool DamageRelated);
public readonly record struct WeaponPair(BaseWeapon Primary,BaseWeapon Secondary);

// Selected hand is scoped to an attack; equipped primary identity is persisted
// separately so native presentation layers cannot change strike ownership.
public static class WeaponHands
{
    private static readonly Dictionary<string,bool> _damage=new(StringComparer.Ordinal);
    [ThreadStatic] private static Dictionary<Serial,Scope> _current;
    internal static void Load()
    {
        _damage.Clear();foreach(var row in BalanceCatalog.Read<WeaponPropertyRule>("class-weapon-properties")){_damage.Add(row.Code,row.DamageRelated);}
    }
    internal static bool DamageRelated(string code)=>_damage.GetValueOrDefault(code??"");
    internal static bool Eligible(BaseWeapon weapon)
    {
        if(weapon==null || ClassGear.Broken(weapon) || ClassGear.Slot(weapon)!=GearSlot.Weapon
            || ClassGear.State(weapon) is not{Identified:true} state || ClassGear.Base(weapon) is not{} basis
            || state.CraftedEquipment || state.Runes.Length!=0 || state.MaterialRolls.Length!=0){return false;}
        // Pure source one-handed bases and Barbarian one/two-handed swords.
        return !basis.TwoHanded || basis.OneOrTwoHanded && ClassItemTypes.Is(basis.Type,"swor");
    }
    internal static WeaponPair? Pair(PlayerMobile owner)
    {
        if(owner==null || ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)!=true || profile.Class!=DiabloClass.Barbarian
            || owner.FindItemOnLayer(Layer.OneHanded) is not BaseWeapon first || owner.FindItemOnLayer(Layer.TwoHanded) is not BaseWeapon second
            || !Eligible(first) || !Eligible(second)){return null;}
        int count=0,hands=0;foreach(var item in owner.Items)
        {if(item is BaseWeapon && ClassGear.Slot(item)==GearSlot.Weapon){count++;}if(item.Layer is Layer.OneHanded or Layer.TwoHanded){hands++;}}
        return count==2 && hands==2?DualEquip.Primary(owner)==second.Serial?new(second,first):new(first,second):null;
    }
    internal static bool Member(PlayerMobile owner,Item item)=>Pair(owner) is{} pair && (item==pair.Primary || item==pair.Secondary);
    internal static BaseWeapon Selected(PlayerMobile owner)
    {
        if(Pair(owner) is not{} pair){return null;}
        return _current!=null && _current.TryGetValue(owner.Serial,out var scope) && scope.Weapon.Parent==owner && (scope.Weapon==pair.Primary || scope.Weapon==pair.Secondary)
            ?scope.Weapon:pair.Primary;
    }
    internal static bool Contributes(PlayerMobile owner,Item item,string code)=>!Member(owner,item) || !DamageRelated(code) || item==Selected(owner);
    internal static bool TwoHanded(Item item)=>item.Parent is not PlayerMobile owner || !Member(owner,item)
        ?item.Layer==Layer.TwoHanded || ClassGear.Base(item) is{TwoHanded:true,OneOrTwoHanded:false}:false;
    internal static bool SecondaryForSequence(int frame,bool damageSelection)=>frame>0 && (!damageSelection || (frame&1)!=0);
    internal static IDisposable Use(PlayerMobile owner,BaseWeapon weapon)
    {
        if(!Member(owner,weapon)){throw new InvalidOperationException("Selected weapon is not in a supported native hand pair.");}
        _current??=new();_current.TryGetValue(owner.Serial,out var prior);var scope=new Scope(owner,weapon,prior);_current[owner.Serial]=scope;return scope;
    }
    private sealed class Scope(PlayerMobile owner,BaseWeapon weapon,Scope prior):IDisposable
    {
        internal BaseWeapon Weapon=>weapon;
        private bool _disposed;
        public void Dispose()
        {
            if(_disposed){return;}
            if(_current==null || !_current.TryGetValue(owner.Serial,out var current) || current!=this){throw new InvalidOperationException("Weapon hand scopes must close on their thread and in order.");}
            _disposed=true;if(prior==null){_current.Remove(owner.Serial);}else{_current[owner.Serial]=prior;}
        }
    }
}
