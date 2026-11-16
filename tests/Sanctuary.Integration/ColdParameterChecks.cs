// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ColdParameterChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ColdParameterChecks));
    private sealed record Saved(uint Owner,uint Body,uint Item,Point3D Point,string Gear,string Recipe,NpcEquipmentSeed Random,int Life,int Mana,int Frames,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"cold-parameter-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    internal static void Legacy(Item item)
    {
        var state=ClassGear.State(item);
        ClassGear.Profiles.Store(item,state with{NamedProperties=state.NamedProperties.Select(r=>r with{ColdDurationFrames=null}).ToArray()});
    }
    private sealed class EdgeRandom(bool high):Random
    {
        internal int Calls;
        public override int Next(int minimum,int maximum)
        {Require(minimum==1&&maximum==201,"Independent cold roll bounds differ");Calls++;return high?maximum-1:minimum;}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var prior=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateMana=m=>m.Name!="ColdParameterOwnerFixture"&&prior?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        Register("cold-parameter-check",()=>Check(false));Register("cold-parameter-reload-check",()=>Check(true));
        foreach(bool quick in new[]{false,true})
        {
            bool captured=quick;string label=quick?"cold-parameter-quick":"cold-parameter-right";
            Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>IronGolemCastChecks.Input(n,captured,label,
                p=>{var item=Original();item.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);return item;},Inspect)));
        }
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Saved independent cold parameter and whole-original learned Iron Golem",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Item Original()=>NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Baranar's Star").Id,99,new Random(12),true);
    private static int Frames(Item item)=>ClassGear.State(item).NamedProperties.Single(r=>r.Key=="p/9").ColdDurationFrames
        ??throw new InvalidOperationException("New original lacks its independent duration");
    private static void Boundaries()
    {
        var property=NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Baranar's Star").Properties.Single(x=>x.Code=="dmg-cold");
        foreach(bool high in new[]{false,true})
        {
            var random=new EdgeRandom(high);var roll=NamedPropertyRuntime.Roll("p/9",property,random);
            Require(random.Calls==1&&roll.Value==1&&roll.Maximum==200&&roll.ColdDurationFrames==(high?200:1),"Damage endpoints and independent draw were merged");
            NamedPropertyRuntime.Validate(property,roll);Require(JsonSerializer.Deserialize<NamedPropertyRoll>(Json(roll))==roll,"Cold roll roundtrip differs");
            foreach(int bad in new[]{-1,0,201})
            {bool refused=false;try{NamedPropertyRuntime.Validate(property,roll with{ColdDurationFrames=bad});}catch(InvalidDataException){refused=true;}Require(refused,"Invalid duration was accepted");}
        }
        var legacy=new NamedPropertyRoll("p/9",1,200);Require(!Json(legacy).Contains("ColdDurationFrames",StringComparison.Ordinal),"Old-range JSON shape changed");
        NamedPropertyRuntime.Validate(property,legacy);
        var fixedProperty=property with{Parameter=JsonSerializer.SerializeToElement(75)};var noDraw=new EdgeRandom(false);
        var fixedRoll=NamedPropertyRuntime.Roll("p/9",fixedProperty,noDraw);Require(noDraw.Calls==0&&fixedRoll.ColdDurationFrames==null,"Explicit duration drew an independent value");
        bool wrong=false;try{NamedPropertyRuntime.Validate(fixedProperty,fixedRoll with{ColdDurationFrames=75});}catch(InvalidDataException){wrong=true;}
        Require(wrong,"Independent duration was attached to a fixed-parameter property");
    }
    private static void Inspect(PlayerMobile owner,IronGolemBody body,Item item)
    {
        int frames=Frames(item);var random=NpcEquipmentRandom.State(body);string gear=Json(ClassGear.State(item));
        var context=IronGolemItems.Equipment(body,CreatureProfiles.Level(body));var graph=context.NumericGraph();var elements=context.Elements();
        Require(frames is>=1 and<=200&&item.Parent==body&&graph.Properties.Single(v=>v.Stat==56).Value==frames
            &&body.Prepared.Recipe.Properties.Single(v=>v.Stat==56).Value==frames&&elements.ColdFrames==frames
            &&elements.ColdMin==256&&elements.ColdMax==200*256&&elements.FireMin==256&&elements.LightningMin==256,
            "Whole-original birth,raw stat and combat cold duration differ");
        Require(Json(ClassGear.State(item))==gear&&NpcEquipmentRandom.State(body)==random,"Projection rerolled or rewrote original/RNG");
    }
    private static byte[] Encode()
    {var writer=new BufferWriter(new byte[16*1024*1024],true);ClassGear.Profiles.Serialize(writer);return writer.Buffer[..(int)writer.Position];}
    private static void LegacyBinary()
    {
        var item=Original();var state=ClassGear.State(item);byte[] original=Encode();
        try
        {
            // Actual version6 binary layout, from the preserved pre-change
            // GearProfiles writer. No missing duration is manufactured.
            var writer=new BufferWriter(new byte[65536],true);writer.Write(6);writer.Write(0);writer.Write(1);writer.Write(item.Serial.Value);
            writer.Write(state.BaseCode);writer.Write(state.Defense);writer.Write(state.StarterTeeth);writer.Write((byte)state.Roll.Rarity);
            writer.Write(state.Roll.ItemLevel);writer.Write(state.Roll.AffixLevel);writer.Write(state.Roll.RequiredLevel);writer.Write(0);
            writer.Write((byte)state.Tier);writer.Write((byte)state.Quality);writer.Write(state.NativeSockets);writer.Write(state.TinkerSockets);
            writer.Write(state.Identified);writer.Write(state.IdentifiedSaleOffer??0);writer.Write(state.CraftedEquipment);writer.Write(state.DurabilityPercent);writer.Write(0);
            writer.Write(state.VendorPurchasePrice??-1);RuneSockets.Write(writer,[]);GemSockets.Write(writer,[]);writer.Write(state.NamedItemId);
            writer.Write(state.NamedSockets);writer.Write(state.NamedProperties.Length);
            foreach(var property in state.NamedProperties){writer.Write(property.Key);writer.Write(property.Value);writer.Write(property.Maximum);}
            ClassGear.Profiles.Deserialize(new BufferReader(writer.Buffer[..(int)writer.Position]));
            var expected=state with{NamedProperties=state.NamedProperties.Select(r=>r with{ColdDurationFrames=null}).ToArray()};
            Require(Json(ClassGear.Profiles.Find(item.Serial))==Json(expected),"Version6 load changed old item properties or invented duration");
            byte[] upgraded=Encode();Require(new BufferReader(upgraded).ReadInt()==7,"Legacy load did not use new persistence version on write");
            ClassGear.Profiles.Deserialize(new BufferReader(upgraded));
            Require(Json(ClassGear.Profiles.Find(item.Serial))==Json(expected),"Version6-to7 roundtrip invented a missing duration");
        }
        finally{ClassGear.Profiles.Deserialize(new BufferReader(original));item.Delete();}
    }
    private static void Contact(PlayerMobile owner,IronGolemBody body,Item item)
    {
        var target=new GraveyardZombie{CantWalk=true,Frozen=true,NoKillAwards=true};target.SetHits(65000);target.MoveToWorld(new(body.X+1,body.Y,body.Z),body.Map);target.AIObject.AITimer.Stop();
        foreach(var resistance in Enum.GetValues<ResistanceType>()){target.SetResistance(resistance,0);}TimedEffects.PreventHealing(target,TimedEffects.Now);
        try
        {
            var at=TimedEffects.Now.AddMinutes(10);var prepared=IronGolemContact.Prepare(body,target,IronGolemContact.Require(body,owner,target),()=>1);
            Require(prepared.Impact.Packet.ColdFrames==Frames(item),"Released ordinary packet lost captured cold frames");
            int before=SkillHealth.Balance256(target);IronGolemContact.Release(body,owner,target,prepared,_=>true,at);
            Require(SkillHealth.Balance256(target)<before&&TimedEffects.TryActor(target,out var state)&&state.Cold?.Source==body.Serial.Value
                &&state.ChillUntil>at,"Real original golem contact did not apply damaging cold with NPC source");
        }
        finally{target.Delete();}
    }
    private static void Check(bool reload)
    {
        Boundaries();LegacyBinary();PlayerMobile p;IronGolemBody b;Item original;Point3D point;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;Require(generation<=2,"Extra cold-parameter reload");
            p=World.FindMobile((Serial)s.Owner)as PlayerMobile;b=World.FindMobile((Serial)s.Body)as IronGolemBody;original=World.FindItem((Serial)s.Item);point=s.Point;
            Require(p!=null&&b!=null&&original?.Parent==b,"Saved cold original/body identity changed");
            Require(Json(ClassGear.State(original))==s.Gear,"Saved cold property ledger changed");
            Require(Json(b.Prepared.Recipe)==s.Recipe,"Saved cold birth recipe changed");
            Require(NpcEquipmentRandom.State(b)==s.Random,"Saved cold source RNG changed");
            Require(SkillHealth.Balance256(b)==s.Life,"Saved cold pet wounds changed");
            Require(NecromancerEffects.ManaBalance256(p)==s.Mana,"Saved cold owner mana changed");
            Require(Frames(original)==s.Frames,"Saved cold duration changed");
            p.MoveToWorld(point,Map.Trammel);b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);var row=PersistentMinions.Profiles.Track(b);PersistentMinions.Profiles.Store(b,row with{Parked=false});
        }
        else
        {
            p=NpcFreezeHitChecks.Owner();p.Name="ColdParameterOwnerFixture";point=p.Location;original=Original();
            while(p.RawStr<160){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Cold owner Strength");}
            while(p.RawDex<60){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Cold owner Dexterity");}
            p.AddToBackpack(original);Require(p.EquipItem(original),"Whole original player equip");ClassGear.Refresh(p);
            Require(NamedGear.ColdDuration(p)==Frames(original)*40,"Player and retained original duration units differ");
            b=NpcFreezeHitChecks.Cast(p,original,"swing2",50);
        }
        Inspect(p,b,original);Contact(p,b,original);
        b.RestoreController(b.Prepared.Controller with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)});
        SkillHealth.SetBalance256(b,SkillHealth.Balance256(b)-257);PersistentMinions.Suspend(p);p.Internalize();
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,b.Serial.Value,original.Serial.Value,point,Json(ClassGear.State(original)),Json(b.Prepared.Recipe),
            NpcEquipmentRandom.State(b),SkillHealth.Balance256(b),NecromancerEffects.ManaBalance256(p),Frames(original),generation)));
    }
}
