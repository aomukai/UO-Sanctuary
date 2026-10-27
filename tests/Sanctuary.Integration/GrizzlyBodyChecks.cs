// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GrizzlyBodyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GrizzlyBodyChecks));
    private sealed record Saved(uint Owner,uint Grizzly,GrizzlyBodyState State,int Life256,int Maximum256,int Regeneration256,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"grizzly-body-fixtures.json");
    private static int _scene;
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("grizzly-body-check",Check);Register("grizzly-body-reload-check",Reload);}
    private static void Register(string command,Action action)=>ConsoleInputHandler.RegisterCommand(command,"Prepared saved bear life/ownership; AI and casting pending",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Command}: passed",command);}catch(Exception e){Log.Error(e,"{Command}: failed",command);}}));
    private static void Train(PlayerMobile p,string id)
    {
        Require(ClassCreation.Profiles.TryGet(p.Serial,out var profile) && TalentCatalog.TryGet(profile.Class,id,out _),"Fixture talent");
        TalentCatalog.TryGet(profile.Class,id,out var definition);
        foreach(var prerequisite in definition.Prerequisites){Train(p,prerequisite);}
        if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    private static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="GrizzlyBodyFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        Train(p,"druid.summongrizzly");FixtureSpace.Corridor(p,7100,24,1800+32*_scene++);return p;
    }
    private static void End(PlayerMobile p){PersistentMinions.Dismiss(p);p.Delete();}
    private static PlayerMobile Supplier()
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="WolfOrdersSupplier"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(p,99);Train(p,"barbarian.battleorders");return p;
    }
    private static Point3D Point(PlayerMobile p)=>new(p.X+1,p.Y,p.Z);
    private static void Snapshots(DruidGrizzlyBody bear)
    {
        var good=bear.Prepared;string before=bear.SavedText;
        var bad=new[]{good with{Snapshot=null},good with{Owner=0},good with{Profile="unknown"},
            good with{NextRecoveryUtc=DateTime.SpecifyKind(good.NextRecoveryUtc,DateTimeKind.Local)},
            good with{Snapshot=good.Snapshot with{Birth=good.Snapshot.Birth with{Life256=1}}},
            good with{Snapshot=good.Snapshot with{Birth=good.Snapshot.Birth with{PhysicalSunder=17}}},
            good with{Snapshot=good.Snapshot with{DireRank=256}}};
        foreach(var state in bad)
        {bool refused=false;try{bear.RestorePrepared(state);}catch(InvalidDataException){refused=true;}Require(refused&&bear.SavedText==before,"Invalid restore mutated saved bear");}
        bear.RestorePrepared(JsonSerializer.Deserialize<GrizzlyBodyState>(before));Require(bear.SavedText==before,"Immutable JSON snapshot round trip");
    }
    private static void LifeAndStats()
    {
        var p=Fresh();var supplier=Supplier();var at=TimedEffects.Now.AddHours(1);var bear=DruidGrizzlyBody.CreatePrepared(p,Point(p),at);
        var inner=AmazonInnerSight.Snapshot();
        try
        {
            Require(bear is{Blessed:false,BleedImmune:false,PoisonImmune:null,CanRegenHits:false,CanRegenMana:false,CanRegenStam:false,ControlSlots:0}
                && bear.ControlMaster==p && bear.SummonClass==DiabloClass.Druid && bear.Permitted(p)&&bear.CanBeDamaged(),"Grizzly inherited skeleton/Raven immunity or ownership");
            Require(bear.SourceLife256==240000 && bear.HitsMax==938 && bear.Hits==938 && SkillHealth.Balance256(bear)==240000
                && SkillHealth.Maximum256(bear)==240000 && SkillHealth.Full(bear),"937.5 source maximum rounded to native ceiling");
            Require(BattleOrders.Regeneration256(bear)==234 && bear.FireResistance==5 && bear.ColdResistance==5
                && bear.PoisonResistance==5 && bear.EnergyResistance==5 && bear.PhysicalResistance==0,"Grizzly source regeneration/resistances");
            Require(CreatureProfiles.Definition(bear).Reference=="druidbear" && SourceAuraTargets.Eligible(bear),"Source aura species identity");
            Require(!GemCombat.Undead(bear) && !NamedCombat.Demon(bear) && GemCombat.Drain(bear)==100
                && CreatureProfiles.For(bear).Traits is{Size:2,ChillEffectiveness:50,BlockChance:0}
                && CreatureProfiles.For(bear).HitRecovery is{Enabled:true,Frames:5,Speed:256},"Grizzly inherited skeleton species/drain or lost source footprint/recovery metadata");
            int basis=bear.CombatProfile.Defense;
            Require(ClassStats.Defense(bear)==ClassStats.ScaleDefense(basis,10),"Copied armor passive not in defense percent pool");
            BarbarianShout.Apply(supplier,bear,1,TimedEffects.Now);int shout=BarbarianShout.Defense(bear);
            Require(shout>0 && ClassStats.Defense(bear)==ClassStats.ScaleDefense(basis,10+shout),$"Armor/Shout pool: basis={basis},own={bear.SourceDefensePercent},shout={shout},actual={ClassStats.Defense(bear)},state={BarbarianShout.State(bear)}");
            AmazonInnerSight.Restore(inner.Append(new InnerSightState(bear.Serial.Value,p.Serial.Value,1,40,TimedEffects.Now.AddHours(1))).ToArray());
            Require(ClassStats.Defense(bear)==AmazonInnerSight.DefenseValue(basis,10+shout,40),"Inner Sight did not act on raw armor before the shared percent pool");
            SkillHealth.SetBalance256(bear,20000);bear.AdvanceRecovery(at.AddMilliseconds(40),true);
            Require(SkillHealth.Balance256(bear)==20234,"One source frame did not heal234FP");
            bear.AdvanceRecovery(at.AddMilliseconds(80),true);Require(SkillHealth.Balance256(bear)==20468,"FP recovery fraction was lost");
            SkillHealth.SetBalance256(bear,239999);bear.AdvanceRecovery(at.AddMilliseconds(120),true);
            Require(SkillHealth.Balance256(bear)==240000 && SkillHealth.Heal(bear,1000)==0,"Recovery exceeded fractional maximum");
            SkillHealth.SetBalance256(bear,20000);int before=SkillHealth.Balance256(bear);
            BattleOrders.Apply(supplier,bear,1,TimedEffects.Now);int maximum=bear.MaximumLife256;
            Require(maximum==324000 && bear.HitsMax==1266 && SkillHealth.Balance256(bear)==BattleOrders.Rescale256(before,240000,324000),$"Orders max/ratio: source={bear.SourceLife256},maximum={maximum},native={bear.HitsMax},seed={bear.HitsMaxSeed},life={SkillHealth.Balance256(bear)},before={before},state={BattleOrders.State(bear)}");
            Require(BattleOrders.Regeneration256(bear)==395,"Post-maximum source regen used skeleton row or rounded-up native maximum");
            before=SkillHealth.Balance256(bear);BattleOrders.Clear(bear);
            Require(bear.MaximumLife256==240000 && bear.HitsMax==938 && SkillHealth.Balance256(bear)==BattleOrders.Rescale256(before,maximum,240000)
                && BattleOrders.Regeneration256(bear)==292,"Orders removal source max/regen recomputation");
            var captured=bear.Prepared.Snapshot;var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
            Require(p.EquipItem(helm),"Real bear owner Tarnhelm equip");ClassGear.Refresh(p);
            Require(bear.Prepared.Snapshot==captured && DruidGrizzlyStats.Capture(p).Birth.Rank==2,"Owner gear rewrote saved birth or fresh capture ignored gear");
            var target=new GraveyardZombie{CantWalk=true};target.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();
            int life=SkillHealth.Balance256(target);bear.Combatant=target;bear.BeginMelee(target);bear.OnThink();
            Require(bear.Combatant==null && SkillHealth.Balance256(target)==life,"Prepared Grizzly leaked native skeleton combat");target.Delete();Incoming(p,bear);Snapshots(bear);
        }
        finally{AmazonInnerSight.Restore(inner);End(p);supplier.Delete();}
    }
    private static void Incoming(PlayerMobile owner,DruidGrizzlyBody body)
    {
        var enemy=new GraveyardZombie{CantWalk=true};enemy.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);enemy.AIObject.AITimer.Stop();
        try
        {
            int before=SkillHealth.Balance256(body);
            AOS.Damage(body,enemy,10,false,100,0,0,0,0,weaponDamage:true);
            Require(before-SkillHealth.Balance256(body)==10*256,"Native Grizzly physical damage/vulnerability");
            Require(SkillCombat.ResistedDamage(body,enemy,10,0,100,0,0,0,true,false) is{Total256:2432},
                "Unbuffed source-life summon bypassed fractional incoming mitigation");
            before=SkillHealth.Balance256(body);AOS.Damage(body,enemy,10,false,0,100,0,0,0,spellDamage:true);
            Require(before-SkillHealth.Balance256(body)==10*256*95/100,$"Native Grizzly fire mitigation: lost={before-SkillHealth.Balance256(body)}FP,fireResist={body.FireResistance}");
            before=SkillHealth.Balance256(body);AOS.Damage(body,enemy,10,false,0,0,0,100,0,spellDamage:true);
            Require(before-SkillHealth.Balance256(body)==10*256*95/100,"Native Grizzly poison mitigation inherited skeleton immunity");
            long tick=Core.TickCount;
            Require(!DruidSpiritWolfBody.ObservedChillImmune(body)&&ColdStates.Apply(enemy,body,100,tick)
                &&GearEffects.ChillPercent(body)==50,"Grizzly incorrectly inherited Spirit Grizzly chill exclusion");
            GearEffects.Clear(body);
            Require(FreezeStates.Apply(enemy,body,100,tick)&&NamedControl.FrozenRemaining(body)>0,"Grizzly source freeze eligibility");
            TimedEffects.ClearGear(body);
            var recovery=CreatureProfiles.For(body).HitRecovery;
            CreatureProfiles.Session(body).RecoveryUntil=Core.TickCount;
            CreatureHitRecovery.Apply(body,enemy,body.HitsMax,true);
            Require(CreatureHitRecovery.Remaining(body)>0&&CreatureHitRecovery.Remaining(body)<=CreatureHitRecovery.Frames(recovery)*40,
                "Grizzly incoming hit did not use the five-frame GH animation metadata");
        }
        finally{TimedEffects.ClearGear(body);enemy.Delete();}
    }
    private static void Lifecycle()
    {
        var p=Fresh();var at=TimedEffects.Now.AddHours(1);var bear=DruidGrizzlyBody.CreatePrepared(p,Point(p),at);var facets=SummonDifficulties.Facets();var floorTiles=new System.Collections.Generic.List<Item>();
        try
        {
            SkillHealth.SetBalance256(bear,20000);var before=bear.Prepared.Snapshot;
            bear.AdvanceRecovery(at.AddMinutes(5),false);
            Require(bear.Map==Map.Internal && SkillHealth.Balance256(bear)==20000 && bear.Prepared.Snapshot==before,"Offline frames healed or rewrote birth");
            var next=bear.Prepared.NextRecoveryUtc;bear.AdvanceRecovery(next,true);
            Require(bear.Map==p.Map && SkillHealth.Balance256(bear)==20234,"Resume replayed offline healing or failed ownership placement");
            Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _),"Park bear wardrobe");
            int life=SkillHealth.Balance256(bear);bear.AdvanceRecovery(bear.Prepared.NextRecoveryUtc.AddMinutes(5),true);
            Require(bear.Map==Map.Internal && SkillHealth.Balance256(bear)==life && bear.Permitted(p),"Inactive history healed or forgot bear permission");
            Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Druid,out _,out _),"Return learned bear history");
            bear.AdvanceRecovery(bear.Prepared.NextRecoveryUtc,true);Require(bear.Map==p.Map,"Wardrobe bear did not resume");
            var spirit=DruidSpiritWolfBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),at);
            var dire=DruidDireWolfBody.CreatePrepared(p,new(p.X+3,p.Y,p.Z),at);
            Require(spirit.ControlMaster==p&&dire.ControlMaster==p&&bear.ControlMaster==p,"Distinct wolf/bear families interfered");
            spirit.Delete();dire.Delete();
            var other=DruidGrizzlyBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),at);other.SetControlMaster(null);other.AdvanceRecovery(at.AddMilliseconds(40),true);
            Require(other.Deleted,"Orphan prepared bear survived");
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
            life=SkillHealth.Balance256(bear);int maximum=bear.MaximumLife256;var birth=bear.Prepared.Snapshot;
            // Provide legal elevated native space in the destination facet;
            // the Trammel corridor's floor does not exist in Felucca.
            int floor=Enumerable.Range(0x500,0x100).First(id=>TileData.ItemTable[id].Surface&&!TileData.ItemTable[id].Impassable&&TileData.ItemTable[id].CalcHeight==0);
            for(int dx=-4;dx<=4;dx++)for(int dy=-4;dy<=4;dy++)
            {
                var point=new Point3D(p.X+dx,p.Y+dy,100);
                Require(Map.Felucca.CanFit(point.X,point.Y,point.Z,16,true,true,false),"Felucca elevated fixture has an obstruction");
                var tile=new Static(floor);tile.MoveToWorld(point,Map.Felucca);floorTiles.Add(tile);
            }
            p.MoveToWorld(new(p.X,p.Y,100),Map.Felucca);
            Require(bear.Prepared.Difficulty==MonsterDifficulty.Hell && bear.Prepared.Snapshot==DruidGrizzlyStats.Rescale(birth,MonsterDifficulty.Hell)
                && SkillHealth.Balance256(bear)==SummonDifficulties.Life256(life,maximum,bear.MaximumLife256),"Owner facet rescale did not preserve birth ranks/life ratio");
            Require(bear.SourceLife256==208000&&bear.MaximumLife256==208000&&bear.Prepared.Snapshot.Birth.Regeneration256==203
                &&BattleOrders.Regeneration256(bear)==253,"Grizzly Hell HP/regen callback used Normal bases or skeleton row");
            int same=SkillHealth.Balance256(bear);SummonDifficulties.Sync(p);Require(SkillHealth.Balance256(bear)==same,"Repeated facet notification rounded life again");
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Nightmare)]);
            SummonDifficulties.Sync(p);
            Require(bear.Prepared.Difficulty==MonsterDifficulty.Nightmare&&bear.SourceLife256==208000&&SkillHealth.Balance256(bear)==same
                &&bear.Prepared.Snapshot==DruidGrizzlyStats.Rescale(birth,MonsterDifficulty.Nightmare),"Nightmare source reconstruction changed proportional injury");
            PersistentMinions.Suspend(p);bear.AdvanceRecovery(bear.Prepared.NextRecoveryUtc,true);
            Require(bear.Map==p.Map,"Shared recovery did not resume owner-facet placement");
        }
        finally{SummonDifficulties.SetFacets(facets);End(p);foreach(var tile in floorTiles){tile.Delete();}}
    }
    private static void Check()
    {
        Require(DruidCombat.Implemented(CombatAbility.SummonGrizzly),"Learned Grizzly route missing");LifeAndStats();Lifecycle();
        var p=Fresh();var bear=DruidGrizzlyBody.CreatePrepared(p,Point(p),TimedEffects.Now.AddDays(2));
        var supplier=Supplier();SkillHealth.SetBalance256(bear,20007);BattleOrders.Apply(supplier,bear,1,TimedEffects.Now.AddHours(1));supplier.Delete();PersistentMinions.Suspend(p);Snapshots(bear);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,bear.Serial.Value,bear.Prepared,SkillHealth.Balance256(bear),bear.MaximumLife256,BattleOrders.Regeneration256(bear),p.Map.MapID,p.Location)));
        Log.Information("Prepared Grizzly saved body:937.5Normal/812.5Nightmare-Hell life,234/203FP birth recovery, shared armor/BO pools, native typed damage/chill/freeze/GH, immutable gear, offline/wardrobe/facet/orphan and wolf/bear coexistence pass; AI112world,BearSmite and learned casting pending");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        var bear=World.FindMobile((Serial)saved.Grizzly) as DruidGrizzlyBody;
        Require(bear is{Deleted:false,Alive:true} && bear.ControlMaster==p && bear.Map==Map.Internal && bear.Permitted(p),"Saved bear ownership/history/parking");
        Require(bear.SavedText==JsonSerializer.Serialize(saved.State) && SkillHealth.Balance256(bear)==saved.Life256
            && bear.MaximumLife256==saved.Maximum256 && BattleOrders.Regeneration256(bear)==saved.Regeneration256,"Reload changed snapshot/fractional life/Orders/regen");Snapshots(bear);
        if(p.Map==Map.Internal){p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);}
        var next=bear.Prepared.NextRecoveryUtc;bear.AdvanceRecovery(next,false);
        Require(SkillHealth.Balance256(bear)==saved.Life256,"Reload replayed offline recovery");
        bear.AdvanceRecovery(bear.Prepared.NextRecoveryUtc,true);
        Require(bear.Map==p.Map && SkillHealth.Balance256(bear)==Math.Min(saved.Maximum256,saved.Life256+saved.Regeneration256),"Reload active frame recovery");
        PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{State=bear.Prepared,Life256=SkillHealth.Balance256(bear)}));
    }
}
