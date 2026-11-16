// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class PlayerItemProcHostChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(PlayerItemProcHostChecks));
    private sealed record Saved(uint First,uint Second,uint Primary,uint Secondary,uint Helm,uint Target1,uint Target2,Point3D Point1,Point3D Point2,string State,string[] Gear,int[] Life,int[] Mana,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"player-item-proc-host-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"player-item-proc-host-reload-check":"player-item-proc-host-check";
            ConsoleInputHandler.RegisterCommand(name,"Saved original proc providers,player streams,equipment/hand/trade/event history",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static PlayerMobile Owner(DiabloClass type,int y)
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="PlayerItemFireBallFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,type);ClassCreation.Profiles.AdvanceLevel(p,99);
        foreach(var stat in new[]{CharacterStat.Strength,CharacterStat.Dexterity,CharacterStat.Energy})
        {while((stat==CharacterStat.Strength?p.RawStr:stat==CharacterStat.Dexterity?p.RawDex:p.RawInt)<100){Require(ClassCreation.Profiles.TrySpendStat(p,stat),"Allocate host stats");}}
        FixtureSpace.Corridor(p,6200,32,y);p.Hits=p.HitsMax;SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-513);NecromancerEffects.SetMana256(p,0);return p;
    }
    private static Item Prepare(PlayerMobile p,Item item,int body,int attack,int incoming=0)
    {
        ClassGear.Profiles.Convert(item,99,GearRarity.Normal,new Random(173));p.AddToBackpack(item);var values=new EquipmentStatValues(_=>body,_=>null,_=>null,(_,_,_,_,_)=>{});
        var graph=new ItemPropertyCreationContext(item,values,default,false,false,false,SourceRandom.InitLow(1),[]);
        var retained=new System.Collections.Generic.List<(NamedProperty Property,NamedPropertyRoll Roll)>();
        foreach(var entry in new[]{(Code:"att-skill",Chance:attack),(Code:"gethit-skill",Chance:incoming)})
        {
            if(entry.Chance<=0){continue;}
            var property=new NamedProperty(entry.Code,entry.Code,entry.Code,true,JsonSerializer.SerializeToElement("Fire Ball"),entry.Chance,20);
            retained.Add((property,NamedPropertyRuntime.Roll(entry.Code,property,new Random(173))));
        }
        Require(EquipmentRawProperties.ProjectRecorded(item,graph.Unit,values,default,false,false,false,retained).Length==0,"Saved proc provider projection omitted fields");
        var projected=graph.Capture() with{HasProperties=graph.Unit.Stats.Last!=null,Properties=graph.Unit.Stats.Last?.BaseValues.ToArray()??[]};
        PlayerItemProcs.Register(item,body,projected);Require(p.EquipItem(item),"Equip actual proc carrier");ClassGear.Refresh(p);return item;
    }
    private static IronGolemFireBallChecks.Recipient Target(PlayerMobile p)
    {var t=new IronGolemFireBallChecks.Recipient();t.SetHits(50000);t.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}return t;}
    private static int Chance(PlayerMobile p,int stat=195)=>PlayerItemProcs.Chance(p,stat,(47<<6)|20);
    private static PlayerProcOwner State(PlayerMobile p)=>PlayerItemProcs.Capture().Owners.Single(o=>o.Actor==p.Serial.Value);
    private static void Emit(PlayerMobile p,IronGolemFireBallChecks.Recipient t,string phase="domeleeattack")
    {
        var before=State(p);ulong expected=before.Seed;uint roll=SourceRandom.Roll(ref expected);int chance=Chance(p,phase.StartsWith("damaged",StringComparison.Ordinal)?201:195);int allocations=0,hp=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);long first=SorceressSpellMissiles.Snapshot().NextId;
        var result=PlayerItemProcs.Dispatch(p,phase,NpcItemTarget.Capture(t),t.Location,new(0,0,0,0,4,HitFlags:32),false,TimedEffects.Now,_=>{allocations++;return new(true,SourceRandom.InitLow(73));},actions:true);
        Require(result.Length==1&&State(p).Seed==expected&&allocations==((roll%100<chance)?1:0),"Source event registration,chance or player stream differs");
        var flights=SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value&&f.Id>first).ToArray();Require(flights.Length==allocations,"Callback/factory count differs");
        foreach(var f in flights)
        {
            int raw=SorceressSpellMissiles.Roll(f,out _);for(int n=0;n<60&&FireBoltChecks.State(f.Id)is{} live;n++){SorceressSpellMissiles.Tick(live.NextUtc,true,live.Id);}
            Require(hp-SkillHealth.Balance256(t)==raw&&t.Source==p.Serial.Value,"Registered player proc failed actual native damage/source");
        }
        Require(NecromancerEffects.ManaBalance256(p)==mana,"Registered proc spent player mana");
    }
    private static void Check(bool reload)
    {
        Require(!NamedGear.Available("unique/254"),"Prepared host admitted incomplete Hellslayer");PlayerMobile p,q;Item a,b,h;IronGolemFireBallChecks.Recipient t,u;int generation=0;
        if(!reload)
        {
            p=Owner(DiabloClass.Barbarian,3550);q=Owner(DiabloClass.Paladin,3610);PlayerItemProcs.InitializeActor(p,SourceRandom.InitLow(13));PlayerItemProcs.InitializeActor(q,SourceRandom.InitLow(27));
            a=Prepare(p,new Dagger(),4,25);b=Prepare(p,new Broadsword(),5,50);h=Prepare(p,new BoneHelm(),1,10,100);t=Target(p);u=Target(q);
            Require(WeaponHands.Pair(p)is{} pair&&pair.Primary==a&&pair.Secondary==b,"Native primary/secondary pair");Require(Chance(p)==35&&Chance(p,201)==100,"Primary and global provider aggregation");
            using(WeaponHands.Use(p,(BaseWeapon)b)){Require(Chance(p)==60&&Chance(p,201)==100,"Selected-secondary damage toggle");Emit(p,t);}
            Require(Chance(p)==35,"Hand scope did not restore primary event stats");
            var seed=State(p).Seed;var events=Json(State(p).Events);for(int n=0;n<3;n++){ClassGear.Refresh(p);}Require(State(p).Seed==seed&&Json(State(p).Events)==events,"Unchanged refresh reset stream or subscriptions");
            int durability=((BaseWeapon)a).HitPoints;((BaseWeapon)a).HitPoints=0;ClassGear.Refresh(p);Require(!ClassGear.Active(p,a)&&!ClassGear.Active(p,b)&&Chance(p)==10,"Host disagrees with existing invalid-pair slot arbitration after break");((BaseWeapon)a).HitPoints=durability;ClassGear.Refresh(p);Require(Chance(p)==35,"Repair lost original proc fields");
            var original=ClassGear.State(h);ClassGear.Profiles.Store(h,original with{Identified=false});ClassGear.Refresh(p);Require(Chance(p)==25&&Chance(p,201)==0,"Unidentified provider stayed active");ClassGear.Profiles.Store(h,original);ClassGear.Refresh(p);Require(Chance(p)==35&&Chance(p,201)==100,"Identification lost proc state");
            p.AddToBackpack(h);ClassGear.Refresh(p);Require(Chance(p)==25,"Backpack provider stayed active");Require(p.EquipItem(h),"Reequip helmet");ClassGear.Refresh(p);
            var fields=Json(PlayerItemProcs.Capture().Items);p.AddToBackpack(b);q.AddToBackpack(b);Require(q.EquipItem(b),"Trade exact secondary to another class");ClassGear.Refresh(p);ClassGear.Refresh(q);
            Require(Chance(p)==35&&Chance(q)==50&&Json(PlayerItemProcs.Capture().Items)==fields,"Trade rerolled item providers or retained old ownership");
        }
        else
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;Require(generation<=2,"Extra host reload");
            p=(PlayerMobile)World.FindMobile((Serial)saved.First);q=(PlayerMobile)World.FindMobile((Serial)saved.Second);a=World.FindItem((Serial)saved.Primary);b=World.FindItem((Serial)saved.Secondary);h=World.FindItem((Serial)saved.Helm);t=(IronGolemFireBallChecks.Recipient)World.FindMobile((Serial)saved.Target1);u=(IronGolemFireBallChecks.Recipient)World.FindMobile((Serial)saved.Target2);
            Require(a.Parent==p&&h.Parent==p&&b.Parent==q&&Json(PlayerItemProcs.Capture())==saved.State,"Saved original subscriptions,player RNG or trade identity changed");
            Require(new[]{a,b,h}.Select(i=>Json(ClassGear.State(i))).SequenceEqual(saved.Gear)&&new[]{p,q}.Select(SkillHealth.Balance256).SequenceEqual(saved.Life)&&new[]{p,q}.Select(NecromancerEffects.ManaBalance256).SequenceEqual(saved.Mana),"Saved gear,wounds or mana changed");
            p.MoveToWorld(saved.Point1,Map.Trammel);q.MoveToWorld(saved.Point2,Map.Trammel);Require(Chance(p)==35&&Chance(q)==50&&Chance(p,201)==100,"Loaded provider/recipient mapping differs");
        }
        var raw=PlayerItemProcs.Capture();var seedBefore=State(p).Seed;var inactive=PlayerItemProcs.Dispatch(p,"domeleeattack",NpcItemTarget.Capture(t),t.Location,new(0,0,0,0,0,HitFlags:32),false,TimedEffects.Now,_=>throw new InvalidOperationException("Offline allocation"),actions:false);
        Require(inactive.Length==0&&State(p).Seed==seedBefore,"Offline host fought or advanced RNG");
        Emit(p,t);Emit(q,u);Emit(p,t,"domissileattack");Emit(p,t,"damagedinmelee");
        // An allocator exception occurs after a real source draw. It must keep
        // the advanced player stream,while the frame/event list closes cleanly.
        bool threw=false;int allocations=0;
        try{PlayerItemProcs.Dispatch(p,"damagedinmelee",NpcItemTarget.Capture(t),t.Location,new(0,0,0,0,4,HitFlags:32),false,TimedEffects.Now,_=>{allocations++;throw new ArithmeticException("Host allocation probe");},actions:true);}
        catch(ArithmeticException){threw=true;}
        Require(threw&&allocations==1&&PlayerItemSpellCasts.Context(p)==null&&!NpcItemSpellCasts.KeepsCombatTarget(p),"Exception lost actual player event cleanup");
        var stable=Json(PlayerItemProcs.Capture());var invalid=PlayerItemProcs.Capture();invalid.Items[0].State.Properties[0]=new(EquipmentStatKey.Pack(195,(49<<6)|20),10);bool refused=false;
        try{PlayerItemProcs.Restore(invalid);}catch(InvalidDataException){refused=true;}Require(refused&&Json(PlayerItemProcs.Capture())==stable,"Invalid persisted source body partly restored");
        var isolated=PlayerItemProcs.Capture();isolated.Items[0].State.Properties[0]=new(0,0);Require(Json(PlayerItemProcs.Capture())==stable,"Player proc snapshot aliases saved original fields");
        var point=p.Location;var other=q.Location;p.Internalize();q.Internalize();
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,q.Serial.Value,a.Serial.Value,b.Serial.Value,h.Serial.Value,t.Serial.Value,u.Serial.Value,point,other,Json(PlayerItemProcs.Capture()),new[]{a,b,h}.Select(i=>Json(ClassGear.State(i))).ToArray(),new[]{p,q}.Select(SkillHealth.Balance256).ToArray(),new[]{p,q}.Select(NecromancerEffects.ManaBalance256).ToArray(),generation)));
    }
}
