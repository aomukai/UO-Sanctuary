// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed class DualEquipProfiles : GenericPersistence
{
    private Dictionary<Serial,Serial> _primary=new();
    public DualEquipProfiles():base("SanctuaryWeaponIdentity",144){}
    internal Serial? Find(PlayerMobile owner)=>_primary.TryGetValue(owner.Serial,out var serial)?serial:null;
    internal void Store(PlayerMobile owner,Serial? primary)
    {if(primary is{} serial){_primary[owner.Serial]=serial;}else{_primary.Remove(owner.Serial);}}
    public override void Serialize(IGenericWriter writer)
    {
        var removed=new List<Serial>();foreach(var pair in _primary)
        {if(World.FindMobile(pair.Key) is not PlayerMobile{Deleted:false} owner || World.FindItem(pair.Value) is not BaseWeapon{Deleted:false} weapon || weapon.Parent!=owner){removed.Add(pair.Key);}}
        foreach(var serial in removed){_primary.Remove(serial);}
        writer.Write(0);writer.Write(_primary.Count);foreach(var pair in _primary){writer.Write(pair.Key.Value);writer.Write(pair.Value.Value);}
    }
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown weapon identity version.");}
        int count=reader.ReadInt();if(count is <0 or >1000000){throw new InvalidDataException("Invalid weapon identity count.");}
        var states=new Dictionary<Serial,Serial>();for(int i=0;i<count;i++)
        {var owner=(Serial)reader.ReadUInt();var item=(Serial)reader.ReadUInt();if(!owner.IsMobile || !item.IsItem || !states.TryAdd(owner,item)){throw new InvalidDataException("Invalid weapon identity.");}}
        _primary=states;
    }
}

public static class DualEquip
{
    private static readonly Dictionary<Serial,Transaction> _transactions=new();
    public static DualEquipProfiles Profiles{get;private set;}
    public static void Configure()
    {
        Profiles=new();ClassGearHooks.EquipTransaction=Prepare;
        ClassGearHooks.ConflictingLayer=(mobile,first,second)=>mobile is PlayerMobile owner && _transactions.TryGetValue(owner.Serial,out var transaction)
            && transaction.PairConflict(first,second)?false:null;
    }
    internal static bool Deferred(PlayerMobile owner)=>_transactions.ContainsKey(owner.Serial);
    internal static Serial? Primary(PlayerMobile owner)=>Profiles?.Find(owner);
    private static bool Barbarian(PlayerMobile owner)=>ClassCreation.Profiles?.TryGet(owner.Serial,out var p)==true && p.Class==DiabloClass.Barbarian;
    private static bool Weapon(Item item)=>item is BaseWeapon && ClassGear.Converted(item) && ClassGear.Slot(item)==GearSlot.Weapon;
    private static bool Hybrid(Item item)=>item is BaseWeapon && ClassGear.Base(item) is{TwoHanded:true,OneOrTwoHanded:true} basis && ClassItemTypes.Is(basis.Type,"swor");
    private static Layer Canonical(Item item)=>ClassGear.Base(item)?.TwoHanded==true?Layer.TwoHanded:Layer.OneHanded;
    private static bool IdentityUsable(Item item)=>Weapon(item) && !ClassGear.Broken(item) && ClassGear.State(item) is{Identified:true};
    private static bool Owned(PlayerMobile owner,Serial? serial)=>serial is{} id && World.FindItem(id) is{} item && item.Parent==owner && owner.Items.Contains(item) && Weapon(item);
    private static IClassEquipTransaction Prepare(PlayerMobile owner,Item item)
    {
        if(owner.Deleted || item is not{Deleted:false} || !ClassCombat.Enabled(owner) || !ClassGear.Converted(item)
            || item.Parent==owner || !Weapon(item) && item is not BaseShield){return null;}
        if(Deferred(owner)){return Denied.Instance;}
        Item companion=null;int hands=0;foreach(var worn in owner.Items)
        {if(worn.Layer is Layer.OneHanded or Layer.TwoHanded){companion=worn;hands++;}}
        Layer layer=item is BaseShield?Layer.TwoHanded:Canonical(item);bool pair=false;Item shifted=null;
        if(hands==1)
        {
            if(item is BaseWeapon candidate && companion is BaseWeapon existing && WeaponHands.CanPair(owner,candidate,existing))
            {layer=existing.Layer==Layer.OneHanded?Layer.TwoHanded:Layer.OneHanded;pair=true;}
            else if(Barbarian(owner) && item is BaseShield && Hybrid(companion) && companion.Layer==Layer.TwoHanded){shifted=companion;}
            else if(Barbarian(owner) && Hybrid(item) && companion is BaseShield){layer=Layer.OneHanded;}
        }
        return new Transaction(owner,item,companion,shifted,layer,pair);
    }
    internal static void Changed(PlayerMobile owner)
    {
        if(World.Loading || Deferred(owner) || Profiles==null){return;}
        if(!WeaponHands.Supports(owner)){Profiles.Store(owner,null);return;}
        BaseWeapon sole=null;int weapons=0;bool shield=false,otherHand=false;
        foreach(var item in owner.Items)
        {
            if(item.Layer is not(Layer.OneHanded or Layer.TwoHanded)){continue;}
            if(Weapon(item)){sole=(BaseWeapon)item;weapons++;}
            else if(item is BaseShield){shield=true;}else{otherHand=true;}
        }
        if(!Owned(owner,Profiles.Find(owner)))
        {Profiles.Store(owner,weapons==1 && IdentityUsable(sole)?sole.Serial:null);}
        if(weapons==1 && !otherHand)
        {
            Layer layer=shield && Hybrid(sole)?Layer.OneHanded:Canonical(sole);
            if(owner.FindItemOnLayer(layer) is not{} current || current==sole){sole.Layer=layer;}
        }
    }
    private static void Refresh(PlayerMobile owner)
    {
        if(owner.Deleted || !ClassCombat.Enabled(owner)){return;}
        ClassGear.Refresh(owner);BladeFuryModes.EquipmentChanged(owner);DruidArcticModes.EquipmentChanged(owner);StatNetwork.Send(owner);ClassGear.Changed(owner);
    }
    private sealed class Denied:IClassEquipTransaction
    {
        internal static readonly Denied Instance=new();public bool Allowed=>false;
        public void Complete(bool equipped){}public void Dispose(){}
    }
    private sealed class Transaction:IClassEquipTransaction
    {
        private readonly PlayerMobile _owner;
        private readonly Item _item,_companion,_shifted;
        private readonly Layer _oldLayer,_companionLayer;
        private readonly Serial? _primary;
        private readonly IEntity _parent;
        private readonly Point3D _location;
        private readonly Map _map;
        private readonly bool _quest,_pair;
        private readonly long _combat;
        private bool _success,_disposed;
        public bool Allowed=>true;
        internal Transaction(PlayerMobile owner,Item item,Item companion,Item shifted,Layer layer,bool pair)
        {
            _owner=owner;_item=item;_companion=companion;_shifted=shifted;_oldLayer=item.Layer;_companionLayer=shifted?.Layer??Layer.Invalid;
            _primary=Profiles.Find(owner);_parent=item.Parent;_location=item.Location;_map=item.Map;_quest=item.QuestItem;_combat=owner.NextCombatTime;_pair=pair;
            _transactions.Add(owner.Serial,this);item.Layer=layer;if(shifted!=null){shifted.Layer=Layer.OneHanded;}
        }
        public void Complete(bool equipped)=>_success=equipped && _item.Parent==_owner;
        internal bool PairConflict(Item first,Item second)=>_pair && (first==_item && second==_companion || first==_companion && second==_item)
            && _item is BaseWeapon && _companion is BaseWeapon;
        public void Dispose()
        {
            if(_disposed){return;}_disposed=true;
            try
            {
                if(_success)
                {
                    if(WeaponHands.Supports(_owner) && !Owned(_owner,_primary))
                    {Profiles.Store(_owner,IdentityUsable(_companion) && _companion.Parent==_owner?_companion.Serial:IdentityUsable(_item)?_item.Serial:null);}
                }
                else
                {
                    if(!_item.Deleted)
                    {
                        if(_item.Parent==_owner && _parent!=_owner)
                        {
                            if(_parent is Container{Deleted:false} pack){pack.AddItem(_item);_item.Location=_location;}
                            else if(_parent is Mobile{Deleted:false} mobile){mobile.AddItem(_item);_item.Location=_location;}
                            else{_item.MoveToWorld(_location,_map??Map.Internal);}
                        }
                        _item.Layer=_oldLayer;_item.QuestItem=_quest;
                        if(_item.Parent!=_owner)
                        {foreach(string suffix in new[]{"Str","Dex","Int"}){_owner.RemoveStatMod($"{_item.Serial}{suffix}");}}
                    }
                    if(_shifted is{Deleted:false} && _shifted.Parent==_owner){_shifted.Layer=_companionLayer;}
                    Profiles.Store(_owner,_primary);_owner.NextCombatTime=_combat;
                }
            }
            finally{_transactions.Remove(_owner.Serial);}
            if(_success){Changed(_owner);}Refresh(_owner);
        }
    }
}
