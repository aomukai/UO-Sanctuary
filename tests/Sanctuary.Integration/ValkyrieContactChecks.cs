// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ValkyrieContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ValkyrieContactChecks));
    private sealed record Held(uint Body,string Text,string[] Items,NpcEquipmentSeed Random,int Life,string Resources,bool Supported,uint Target=0,IronGolemContact.Saved Pending=null,int TargetLife=0);
    private sealed record Saved(uint Owner,Point3D Point,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"valkyrie-contact-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("valkyrie-contact-check",false);Register("valkyrie-contact-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original Valkyrie full-fill ordered contact and original save checks",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public sealed class Recipient:GraveyardZombie
    {
        internal int Notifications,AtNotification;
        [Constructible]public Recipient(){CantWalk=true;Frozen=true;NoKillAwards=true;}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool willKill){Notifications++;AtNotification=SkillHealth.Balance256(this);base.OnDamage(amount,from,willKill);}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);Require(reader.ReadInt()==0,"Recipient save version");}
    }
    private static Held Store(AmazonValkyrieBody body,bool supported,uint target=0,IronGolemContact.Saved pending=null,int targetLife=0)
        =>new(body.Serial.Value,body.SavedText,ValkyrieEquipment.Originals(body).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.State(body),SkillHealth.Balance256(body),Json(GeneratedNpcLiveResources.State(body)),supported,target,pending,targetLife);
    private static Recipient Target(AmazonValkyrieBody body)
    {
        var owner=(PlayerMobile)body.ControlMaster;var target=new Recipient();target.SetHits(65000);target.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),body.Map);target.AIObject.AITimer.Stop();
        foreach(var type in Enum.GetValues<ResistanceType>()){target.SetResistance(type,0);}TimedEffects.PreventHealing(target,TimedEffects.Now);return target;
    }
    private static ulong HitSeed(AmazonValkyrieBody body,BaseCreature target,bool hit)
    {
        double chance=ClassStats.HitChance(ClassStats.AttackRatingAgainst(body,target,0),ClassStats.Defense(target),ClassStats.Level(body),ClassStats.Level(target));
        for(int n=1;n<10000;n++){ulong seed=SourceRandom.InitLow(n),copy=seed;if((SourceRandom.Roll(ref copy)%100/100.0<chance)==hit){return seed;}}
        throw new InvalidOperationException("No original Valkyrie result seed");
    }
    private static IronGolemContact.Prepared Prepare(AmazonValkyrieBody body,PlayerMobile owner,Recipient target,bool hit=true)
    {NpcEquipmentRandom.Set(body,HitSeed(body,target,hit));return ValkyrieEquipmentContact.Prepare(body,owner,target,_=>true,TimedEffects.Now);}
    private static void Contacts(AmazonValkyrieBody body,PlayerMobile owner)
    {
        var target=Target(body);
        try
        {
            int life=SkillHealth.Balance256(target);var prepared=Prepare(body,owner,target);var packet=prepared.Impact.Packet;
            Require((packet.ResultFlags&1)!=0&&packet.Total256>0&&packet.Physical256>0&&SkillHealth.Balance256(target)==life&&target.Notifications==0,"Full original allocation committed HP or lost ordinary weapon damage");
            ulong seed=NpcEquipmentRandom.State(body).Seed;
            if(NpcItemKnockback.Bonus(body)>0){SourceRandom.Roll(ref seed);}
            target.SetResistance(ResistanceType.Physical,100);int packets=NativeMonsterReturn.Session(target).Packets;
            var result=IronGolemContact.Release(body,owner,target,prepared,_=>true,TimedEffects.Now);
            Require(result.Exit==MonsterContactExit.Finished&&result.Commit!=null&&SkillHealth.Balance256(target)==life-packet.Total256
                &&target.Notifications==1&&target.AtNotification==life&&NativeMonsterReturn.Session(target).Packets==packets+1&&prepared.Consumed
                &&NpcEquipmentRandom.State(body).Seed==seed,"Original ordered HP/notification/free/event RNG changed or allocation was recomputed");
            bool refused=false;try{IronGolemContact.Release(body,owner,target,prepared,_=>true,TimedEffects.Now);}catch(InvalidOperationException){refused=true;}Require(refused,"Original contact replayed");
        }
        finally{target.Delete();}
        target=Target(body);
        try
        {
            int life=SkillHealth.Balance256(target);var miss=Prepare(body,owner,target,false);ulong seed=NpcEquipmentRandom.State(body).Seed;
            Require((miss.Impact.Packet.ResultFlags&1)==0&&miss.Impact.Packet.Total256==0,"Miss allocated original weapon damage");
            var result=IronGolemContact.Release(body,owner,target,miss,_=>true,TimedEffects.Now);
            Require(result.Commit==null&&SkillHealth.Balance256(target)==life&&target.Notifications==0&&NpcEquipmentRandom.State(body).Seed==seed,"Miss dealt HP or rerolled damage/events");
            var outOfRange=Prepare(body,owner,target);seed=NpcEquipmentRandom.State(body).Seed;
            result=IronGolemContact.Release(body,owner,target,outOfRange,_=>false,TimedEffects.Now);
            Require(result.Exit==MonsterContactExit.OutOfRangeFreed&&SkillHealth.Balance256(target)==life&&NpcEquipmentRandom.State(body).Seed==seed,"Out-of-range cached contact dealt HP or consumed event RNG");
        }
        finally{target.Delete();}
    }
    private static Held Pending(AmazonValkyrieBody body,PlayerMobile owner,bool supported)
    {
        if(!supported){return Store(body,false);}
        PlaceSource(body,owner);
        var target=Target(body);var contact=Prepare(body,owner,target);var saved=IronGolemContact.Capture(contact);contact.Cancel();
        PersistentMinions.Park(body);
        return Store(body,true,target.Serial.Value,saved,SkillHealth.Balance256(target));
    }
    private static void PlaceSource(AmazonValkyrieBody body,PlayerMobile owner)
    {
        if(body.Map!=Map.Internal){return;}
        Require(body.ControlSlots==0&&PersistentMinions.Profiles.Track(body)is{Slots:0},"Prepared source fixture changed follower-slot semantics");
        Require(PersistentMinions.FindPosition(owner,out var point),"No legal prepared-source landing");body.MoveToWorld(point,owner.Map);
        var roster=PersistentMinions.Profiles.Track(body);PersistentMinions.Profiles.Store(body,roster with{Parked=false});
    }
    private static void GuardMotion(AmazonValkyrieBody body)
    {
        ValkyrieGroundMotion.Activate(body,75,125,TimedEffects.Now.AddHours(1));string before=body.SavedText;var state=body.Ground.State.Controller;bool refused=false;
        try{body.Ground.Store(state with{Mode=4,Target=body.ControlMaster.Serial.Value,NextThink=0,ActionAt=1,EndAt=20});}catch(InvalidDataException){refused=true;}
        Require(refused&&body.SavedText==before&&ReferenceEquals(body.Ground.State.Controller,state)&&ValkyrieGroundMotion.Mode(body)==1,"Rejected body write published an unowned cached attack mode");
        PersistentMinions.Suspend((PlayerMobile)body.ControlMaster);
    }
    private static void Check(bool reload)
    {
        using var expected=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-contact-cases.json")));var cases=expected.RootElement.EnumerateArray().ToArray();Require(cases.Length==57&&cases.Count(c=>c.GetProperty("Supported").GetBoolean())==30,"Original full-graph contact corpus");
        PlayerMobile owner;Point3D point;int generation=0;var bodies=new List<AmazonValkyrieBody>();
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Contact reload count");generation=saved.Generation+1;owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;Require(owner is{Alive:true,Deleted:false},"Original contact owner lost");point=saved.Point;
            foreach(var held in saved.Bodies)
            {
                var b=World.FindMobile((Serial)held.Body)as AmazonValkyrieBody;Require(b is{Alive:true,Deleted:false}&&b.ControlMaster==owner&&Json(Store(b,held.Supported,held.Target,held.Pending,held.TargetLife))==Json(held),"Original contact admission/items/resources/wounds/RNG changed on actual reload");bodies.Add(b);
            }
            owner.MoveToWorld(point,Map.Trammel);PersistentMinions.Resume(owner);
            foreach(var held in saved.Bodies.Where(h=>h.Supported))
            {
                var b=(AmazonValkyrieBody)World.FindMobile((Serial)held.Body);var t=World.FindMobile((Serial)held.Target)as Recipient;
                Require(t is{Alive:true,Deleted:false}&&SkillHealth.Balance256(t)==held.TargetLife,"Original pending target changed before reload release");
                PlaceSource(b,owner);
                var contact=IronGolemContact.Restore(held.Pending);var result=IronGolemContact.Release(b,owner,t,contact,_=>true,TimedEffects.Now);
                Require(result.Commit!=null&&SkillHealth.Balance256(t)==held.TargetLife-held.Pending.Impact.Packet.Total256,
                    "Restored original packet lost ordered HP release:body="+b.Serial+",target="+t.Serial+",exit="+result.Exit+",commit="+Json(result.Commit)
                    +",expected="+(held.TargetLife-held.Pending.Impact.Packet.Total256)+",actual="+SkillHealth.Balance256(t)+",flags="+result.Packet.ResultFlags
                    +",maps="+b.Map+"/"+t.Map+"/"+owner.Map+",hostile="+ClassCombat.IsHostile(owner,t)+",sourceLife="+SkillHealth.Balance256(b));t.Delete();PersistentMinions.Park(b);
            }
        }
        else
        {
            owner=new PlayerMobile{Name="ValkyrieContactFixture",Body=0x190,Player=true};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);ClassSkillChecks.Train(owner,"amazon.valkyrie",20);FixtureSpace.Corridor(owner,6080,30,3680);point=owner.Location;
            using var damage=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-damage-cases.json")));var original=damage.RootElement.EnumerateArray().Where(c=>c.GetProperty("Critical").GetInt32()==1).ToArray();
            for(int i=0;i<cases.Length;i++)
            {
                var b=ValkyrieDamageChecks.Body(owner,original[i]);ValkyrieEquipmentResources.Activate(b,0,0,0,0,300);ValkyrieEquipmentDefense.Activate(b);var before=Store(b,false);bool admitted=true;
                try{ValkyrieEquipmentContact.Activate(b);}catch(NotSupportedException){admitted=false;}
                Require(admitted==cases[i].GetProperty("Supported").GetBoolean(),"Original loadout registration readiness differs case="+i);
                if(!admitted){Require(Json(Store(b,false))==Json(before),"Unsupported original proc changed items/admission/resources/RNG");}
                else
                {
                    foreach(var pair in cases[i].GetProperty("Bonuses").EnumerateObject()){Require(NpcEquipmentCombat.Bonus(b,pair.Name)==pair.Value.GetInt32(),"Original scalar differs from independent property/source reader: "+pair.Name+" case="+i);}
                    Require(NamedElements.MagicResistance(b)==b.EquipmentNumbers.Read(37),"Full original magic resistance counted twice");Contacts(b,owner);
                }
                bodies.Add(b);
            }
            // Exercise transactional controller writes on a motion-only body.
            GuardMotion(bodies[0]);owner.MoveToWorld(point,Map.Trammel);PersistentMinions.Resume(owner);
        }
        var helds=bodies.Select((b,i)=>Pending(b,owner,cases[i].GetProperty("Supported").GetBoolean())).ToArray();PersistentMinions.Suspend(owner);owner.Internalize();
        for(int i=0;i<bodies.Count;i++){Require(Json(Store(bodies[i],helds[i].Supported,helds[i].Target,helds[i].Pending,helds[i].TargetLife))==Json(helds[i]),"Parking changed original contact inputs");}
        File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,point,helds,generation)));
        Log.Information("Valkyrie contact:57original loadouts/330items;30full-fill ordered HP/event consumers and27atomic proc refusals;original pending packets/items/resources/wounds/RNG through two reloads;AI/cast remain gated");
    }
}
