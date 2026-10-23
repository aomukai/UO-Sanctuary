// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class DualEquipChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DualEquipChecks));
    private sealed record Saved(uint Owner,uint First,uint Second,Point3D Origin,int Map);
    private sealed record Manifest(Saved Reverse,Saved Network);
    public sealed class RejectPlayer:PlayerMobile
    {
        public bool Reject;
        [Constructible] public RejectPlayer(){}
        public RejectPlayer(Serial serial):base(serial){}
        public override bool CheckEquip(Item item)=>!Reject && base.CheckEquip(item);
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);reader.ReadInt();}
    }
    private sealed class EquipSpell(bool raises=false):ISpell
    {
        public bool BlocksMovement=>false;public bool IsCasting=>false;
        public bool OnCasterEquipping(Item item){if(raises){throw new InvalidOperationException("Expected equip rejection fixture");}return false;}
        public void OnCasterHurt(){}public void OnCasterKilled(){}public void OnConnectionChanged(){}public void FinishSequence(){}
        public bool OnCasterMoving(Direction d)=>true;public bool OnCasterUsingObject(IEntity e)=>true;public bool OnCastInTown(Region r)=>true;
    }
    private static string PathName=>Path.Join(Core.BaseDirectory,"dual-equip.json");
    public static void Configure(){Register("dual-equip-check",Check);Register("dual-equip-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Native equip transaction, primary identity and actual equip packet",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}}));
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static PlayerMobile Fresh(bool rejects=false,DiabloClass character=DiabloClass.Barbarian)
    {
        PlayerMobile p=rejects?new RejectPlayer():new PlayerMobile();p.Body=0x190;p.Player=true;p.Name="DualEquipFixture";p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,character);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<250){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Fixture Strength");}
        while(p.RawDex<180){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Fixture Dexterity");}
        FixtureSpace.Place(p,new(6100,1800,0),Map.Trammel);p.Hits=p.HitsMax;return p;
    }
    private static BaseWeapon Named(PlayerMobile owner,string key)
    {
        var weapon=(BaseWeapon)NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(173),true);owner.AddToBackpack(weapon);return weapon;
    }
    private static CrescentBlade Hybrid(PlayerMobile owner)
    {var item=new CrescentBlade();ClassGear.Profiles.Convert(item,99,GearRarity.Normal,new Random(173));owner.AddToBackpack(item);return item;}
    private static HeaterShield Shield(PlayerMobile owner)
    {var item=new HeaterShield();ClassGear.Profiles.Convert(item,99,GearRarity.Normal,new Random(173));owner.AddToBackpack(item);return item;}
    private static Saved Save(PlayerMobile p,BaseWeapon first,BaseWeapon second)=>new(p.Serial.Value,first.Serial.Value,second.Serial.Value,p.Location,p.Map.MapID);
    private static void Pair(PlayerMobile p,BaseWeapon first,BaseWeapon second)
    {
        Require(WeaponHands.Pair(p)==new WeaponPair(first,second) && DualEquip.Primary(p)==first.Serial && CombatTiming.Equipped(p)==first,"Primary identity/pair order");
        Require(ClassGear.Active(p,first) && ClassGear.Active(p,second) && first.Layer!=second.Layer,"Pair equipment contributions/layers");
    }
    private static void Failures()
    {
        var p=(RejectPlayer)Fresh(true);var first=Named(p,"Hellplague");var second=Named(p,"Crushflange");Require(p.EquipItem(first),"Primary for failure tests");
        var oldLayer=second.Layer;var primary=DualEquip.Primary(p);var parent=second.Parent;var location=second.Location;int strength=p.Str;
        p.Reject=true;Require(!p.EquipItem(second),"CheckEquip veto ignored");p.Reject=false;
        Require(second.Layer==oldLayer && second.Parent==parent && first.Layer==Layer.OneHanded && DualEquip.Primary(p)==primary && !DualEquip.Deferred(p),"CheckEquip rollback");
        foreach(bool raises in new[]{false,true})
        {
            long clock=p.NextCombatTime;second.QuestItem=true;p.Spell=new EquipSpell(raises);bool exception=false;
            try{Require(!p.EquipItem(second),"Late spell veto ignored");}catch(InvalidOperationException ex) when(ex.Message=="Expected equip rejection fixture"){exception=true;}
            finally{p.Spell=null;}
            Require(exception==raises && second.Parent==parent && second.Location==location && second.Layer==oldLayer && second.QuestItem
                && p.Str==strength && p.NextCombatTime==clock && DualEquip.Primary(p)==primary && !DualEquip.Deferred(p),"Late veto/exception rollback leaked inventory/layer/quest/stats/clock/identity");
            second.QuestItem=false;
        }
        var hybrid=Hybrid(p);p.AddToBackpack(first);Require(p.EquipItem(hybrid) && hybrid.Layer==Layer.TwoHanded,"Hybrid failure primary");var shield=Shield(p);
        p.Reject=true;Require(!p.EquipItem(shield),"Shield veto ignored");p.Reject=false;
        Require(hybrid.Layer==Layer.TwoHanded && shield.Parent==p.Backpack && DualEquip.Primary(p)==hybrid.Serial,"Companion layer rollback");
        Require(p.EquipItem(shield) && hybrid.Layer==Layer.OneHanded && !WeaponHands.TwoHanded(hybrid),"Hybrid with shield one-handed footprint");
        p.AddToBackpack(shield);Require(hybrid.Layer==Layer.TwoHanded && WeaponHands.TwoHanded(hybrid),"Shield removal solo hybrid footprint");
        p.Delete();
    }
    private static void Restrictions()
    {
        var p=Fresh();var first=Named(p,"Hellplague");var second=Named(p,"Felloak");Require(p.EquipItem(first) && p.EquipItem(second),"Normal pair");Pair(p,first,second);
        var third=Named(p,"Deathspade");Require(!p.EquipItem(third) && third.Parent==p.Backpack,"Third hand accepted");Pair(p,first,second);
        p.AddToBackpack(first);Require(DualEquip.Primary(p)==second.Serial && second.Layer==Layer.OneHanded,"Primary removal did not promote/canonicalize remaining weapon");
        Require(p.EquipItem(first),"Reequip older created weapon");Pair(p,second,first);
        p.AddToBackpack(first);var other=Fresh(character:DiabloClass.Paladin);other.Backpack.DropItem(first);Require(other.EquipItem(first) && first.Layer==Layer.OneHanded,"Transferred offhand retained two-handed footprint");
        var extra=Named(other,"Felloak");Require(!other.EquipItem(extra),"Non-Barbarian dual equip");other.Delete();
        var polearm=new Halberd();ClassGear.Profiles.Convert(polearm,99,GearRarity.Normal,new Random(173));p.AddToBackpack(polearm);Require(!p.EquipItem(polearm),"True two-handed weapon paired");
        var raw=new Mace{Resource=CraftResource.Valorite};var state=ClassGear.Profiles.Convert(raw,99,GearRarity.Normal,new Random(173));ClassGear.Profiles.Store(raw,state with{CraftedEquipment=true,MaterialRolls=[new("damage-fire",5)]});p.AddToBackpack(raw);Require(!p.EquipItem(raw),"Unsupported crafted/material pair");
        int hp=third.HitPoints;third.HitPoints=0;Require(!p.EquipItem(third),"Broken second weapon accepted");third.HitPoints=hp;
        p.Delete();
    }
    private static Saved Reverse()
    {
        var p=Fresh();var first=Hybrid(p);var second=Named(p,"Hellplague");Require(p.EquipItem(first) && first.Layer==Layer.TwoHanded,"Solo hybrid equip");
        Require(p.EquipItem(second) && second.Layer==Layer.OneHanded,"Reverse-layer pair equip");Pair(p,first,second);
        Require(!WeaponHands.TwoHanded(first) && ClassGear.Damage(first).Min==ClassGear.Base(first).MinDamage && CombatTiming.Weapon(first).Family==AttackFamily.Swing,"Paired primary hybrid damage/timing footprint");
        Require(CombatTiming.Animation(first)==WeaponAnimation.Slash1H,"Paired hybrid animation footprint");
        var saved=Save(p,first,second);return saved;
    }
    private static void Attach(PlayerMobile owner)
    {
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=owner;owner.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Dual equip network bootstrap");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(owner);
    }
    private static void Network(PlayerMobile owner,BaseWeapon primary,BaseWeapon incoming,Saved reverse)
    {
        Attach(owner);Require(primary.Parent==owner || owner.EquipItem(primary),"Network primary equip");
        owner.Lift(incoming,1,out bool rejected,out var reason);Require(!rejected && owner.Holding==incoming,$"Native lift rejected: {reason}");
        long deadline=Core.TickCount+5000;
        Log.Information("dual-equip-network-ready: {Owner} {Item}",owner.Serial.Value,incoming.Serial.Value);
        void Poll()
        {
            try
            {
                if(incoming.Parent==owner)
                {
                    Pair(owner,primary,incoming);Require(incoming.Layer==Layer.TwoHanded && owner.Holding==null && incoming.HeldBy==null,"Actual equip packet/holding ownership");
                    File.WriteAllText(PathName,JsonSerializer.Serialize(new Manifest(reverse,Save(owner,primary,incoming))));Log.Information("dual-equip-network-result: passed");return;
                }
                if(Core.TickCount-deadline>=0){throw new InvalidOperationException("Equip packet did not commit");}
                Timer.StartTimer(TimeSpan.FromMilliseconds(40),Poll);
            }
            catch(Exception ex){Log.Error(ex,"dual-equip-network-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(40),Poll);
    }
    private static void Check()
    {
        Failures();Restrictions();var reverse=Reverse();var owner=Fresh();var primary=Named(owner,"Felloak");var incoming=Named(owner,"Hellplague");Network(owner,primary,incoming,reverse);
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(PathName));var row=saved.Reverse;var owner=(PlayerMobile)World.FindMobile((Serial)row.Owner);var first=(BaseWeapon)World.FindItem((Serial)row.First);var second=(BaseWeapon)World.FindItem((Serial)row.Second);owner.MoveToWorld(row.Origin,Map.Maps[row.Map]);ClassGear.Refresh(owner);
        Pair(owner,first,second);Require(first.Layer==Layer.TwoHanded && second.Layer==Layer.OneHanded && CombatTiming.Weapon(first).Family==AttackFamily.Swing,"Saved primary identity became layer/serial order");
        Failures();Restrictions();row=saved.Network;owner=(PlayerMobile)World.FindMobile((Serial)row.Owner);first=(BaseWeapon)World.FindItem((Serial)row.First);second=(BaseWeapon)World.FindItem((Serial)row.Second);owner.MoveToWorld(row.Origin,Map.Maps[row.Map]);ClassGear.Refresh(owner);Pair(owner,first,second);
        owner.AddToBackpack(second);Network(owner,first,second,saved.Reverse);
    }
}
