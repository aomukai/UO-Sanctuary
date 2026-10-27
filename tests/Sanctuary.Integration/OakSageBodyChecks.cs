// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary.Integration;

public static class OakSageBodyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(OakSageBodyChecks));
    private sealed record Saved(uint Owner,uint Oak,OakBodyState Body,int Life,int Maximum,int Regeneration,int OwnerLife,int OwnerMaximum,OakLease Self,OakLease Recipient,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"oak-sage-body-fixtures.json");
    private static int _scene;
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("oak-sage-body-check",Check);Register("oak-sage-body-reload-check",Reload);}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        // Inspection players retain an exact injury across the other tests'
        // wall-clock time. Production recovery and spirit frame regen stay intact.
        var previous=ClassGearHooks.CanRegenerateHits;
        ClassGearHooks.CanRegenerateHits=actor=>actor.Name?.StartsWith("OakSageBodyFixture",StringComparison.Ordinal)!=true&&previous?.Invoke(actor)!=false;
    }
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Oak body/life aura and lifecycle",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Train(PlayerMobile p,string id,int rank=1)
    {
        ClassCreation.Profiles.TryGet(p.Serial,out var profile);Require(TalentCatalog.TryGet(profile.Class,id,out var definition),"Fixture talent");
        foreach(var prior in definition.Prerequisites){Train(p,prior);}
        while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    private static PlayerMobile Fresh(DiabloClass kind=DiabloClass.Druid)
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="OakSageBodyFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,kind);ClassCreation.Profiles.AdvanceLevel(p,99);
        Train(p,kind==DiabloClass.Druid?"druid.oaksage":"barbarian.battleorders");
        FixtureSpace.Corridor(p,7000,24,2200+32*_scene++);return p;
    }
    private static Point3D Point(PlayerMobile p)
    {
        for(int i=1;i<12;i++){var point=new Point3D(p.X+i,p.Y,p.Z);if(p.Map.CanSpawnMobile(point)){return point;}}
        throw new InvalidOperationException("No fixture spirit point");
    }
    private static void End(PlayerMobile p)
    {
        foreach(var pet in (p.AllFollowers??[]).OfType<BaseCreature>().ToArray()){DruidOakAuras.Clear(pet);BattleOrders.Clear(pet);}
        DruidOakAuras.Clear(p);BattleOrders.Clear(p);PersistentMinions.Dismiss(p);p.Delete();
    }
    private static void Malformed(DruidOakBody body)
    {
        var good=body.Prepared;string before=body.SavedText;
        foreach(var bad in new[]{good with{Owner=0},good with{Birth=null},good with{Profile="unknown"},
            good with{NextRecoveryUtc=DateTime.SpecifyKind(good.NextRecoveryUtc,DateTimeKind.Local)},
            good with{Birth=good.Birth with{Level=100}},good with{Birth=good.Birth with{Life256=1}}})
        {bool refused=false;try{body.RestorePrepared(bad);}catch(InvalidDataException){refused=true;}Require(refused&&body.SavedText==before,"Malformed Oak changed saved bytes");}
        var save=DruidOakAuras.Snapshot();string json=JsonSerializer.Serialize(save);
        var valid=new OakLease(body.Serial.Value,body.Serial.Value,body.Serial.Value,1,30,TimedEffects.Now.AddHours(2),true);
        foreach(var bad in new[]{save with{Leases=null},save with{Emitters=null},save with{Leases=[valid,valid]},
            save with{Leases=[valid with{Rank=0}]},save with{Leases=[valid with{Percent=31}]},save with{Leases=[valid with{Creator=0}]},
            save with{Leases=[valid with{UntilUtc=DateTime.SpecifyKind(valid.UntilUtc,DateTimeKind.Local)}]}})
        {bool refused=false;try{DruidOakAuras.Restore(bad);}catch(InvalidDataException){refused=true;}Require(refused&&JsonSerializer.Serialize(DruidOakAuras.Snapshot())==json,"Malformed aura restore was not atomic");}
    }
    private static void LifeAndStacking()
    {
        var p=Fresh();var supplier=Fresh(DiabloClass.Barbarian);var at=TimedEffects.Now.AddHours(1);var body=DruidOakBody.CreatePrepared(p,Point(p),at);
        try
        {
            Require(body is{Blessed:false,BleedImmune:false,PoisonImmune:null,CanRegenHits:false,CanRegenMana:false,CanRegenStam:false,ControlSlots:0}
                && body.ControlMaster==p && body.Permitted(p) && body.CanBeDamaged(),"Oak inherited skeleton/Raven immunity or ownership");
            Require(body.MaximumLife256==15360 && body.HitsMax==60 && BattleOrders.Regeneration256(body)==11
                && ClassStats.Level(body)==99 && ClassStats.Defense(body)==body.Prepared.Birth.Defense,"Oak base life/level/defense/regen");
            Require(body.PhysicalResistance==25 && body.FireResistance==25 && body.ColdResistance==25 && body.EnergyResistance==25
                && body.PoisonResistance==170 && NamedElements.MagicResistance(body)==25,"Oak raw difficulty resistance/passive totals");
            Require(SourceAuraTargets.Eligible(body) && CreatureProfiles.For(body).Traits is{Size:2,Undead:false,Demon:false,ChillEffectiveness:50}
                && !CreatureProfiles.For(body).HitRecovery.Enabled,"Oak source traits/recipient eligibility");
            SkillHealth.SetBalance256(body,6407);body.Advance(at.AddMilliseconds(40),true);
            Require(SkillHealth.Balance256(body)==6418 && DruidOakAuras.Percent(body)==0,"Oak first frame regen or premature aura");
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2+7);NecromancerEffects.SetMana256(p,7*256+13);BattleOrders.SetStamina256(p,11*256+7);
            var playerBefore=BattleOrders.Capture(p);var petBefore=BattleOrders.Capture(body);
            Require(DruidOakAuras.Pulse(body,at),"Oak pulse refused");
            Require(body.MaximumLife256==19968 && body.HitsMax==78 && BattleOrders.Regeneration256(body)==14
                && SkillHealth.Balance256(body)==BattleOrders.Rescale256(petBefore.Life256,15360,19968),"Oak self aura exact maximum/current/regen");
            Require(DruidOakAuras.Percent(p)==30 && ClassResources.Parts(p,0).SkillPercent==30
                && SkillHealth.Balance256(p)==BattleOrders.Rescale256(playerBefore.Life256,playerBefore.ExactLifeMaximum256,SkillHealth.Maximum256(p))
                && p.ManaMax==playerBefore.ManaMax && p.StamMax==playerBefore.StaminaMax && NecromancerEffects.ManaBalance256(p)==playerBefore.Mana256
                && BattleOrders.Stamina256(p)==playerBefore.Stamina256,"Oak affected wrong resource, paid mana or lost life fraction");
            int life=SkillHealth.Balance256(p);DruidOakAuras.Pulse(body,at.AddMilliseconds(40));Require(SkillHealth.Balance256(p)==life,"Equal refresh healed");
            BattleOrders.Apply(supplier,body,1,at);BattleOrders.Apply(supplier,p,1,at);
            Require(body.MaximumLife256==25344 && body.HitsMax==99 && ClassResources.Parts(p,0).SkillPercent==65
                && ClassResources.Parts(p,1).SkillPercent==35 && ClassResources.Parts(p,2).SkillPercent==35,"Oak multiplied BO or leaked into other resource pools");
            BattleOrders.Clear(body);BattleOrders.Clear(p);Require(body.MaximumLife256==19968,"BO removal erased Oak");
            var old=DruidOakAuras.State(p);var before=BattleOrders.Capture(p);DruidOakAuras.Expire(old.UntilUtc.AddTicks(-1));
            Require(DruidOakAuras.State(p)!=null&&SkillHealth.Balance256(p)==before.Life256,"Premature lease expiry");
            DruidOakAuras.Expire(old.UntilUtc);Require(DruidOakAuras.State(p)==null&&body.MaximumLife256==15360
                && SkillHealth.Balance256(p)==BattleOrders.Rescale256(before.Life256,before.ExactLifeMaximum256,SkillHealth.Maximum256(p)),"Expiry dropped maximum before callback");
            life=SkillHealth.Balance256(p);DruidOakAuras.Expire(old.UntilUtc);Require(SkillHealth.Balance256(p)==life,"Duplicate expiry changed life");
            var birth=body.Prepared.Birth;var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
            Require(p.EquipItem(helm),"Oak Tarnhelm");ClassGear.Refresh(p);
            Require(body.Prepared.Birth==birth && DruidOakStats.Capture(p) is{Rank:2,Life256:19968,AuraRank:2},"Oak birth/aura rank changed with owner gear");
            var enemy=new GraveyardZombie{CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();
            life=SkillHealth.Balance256(enemy);body.Combatant=enemy;body.BeginMelee(enemy);body.OnThink();Require(body.Combatant==null&&SkillHealth.Balance256(enemy)==life,"Oak leaked skeleton attack");enemy.Delete();Malformed(body);
        }
        finally{End(p);End(supplier);}
    }
    private static void RecipientsAndOverlap()
    {
        var p=Fresh();var other=Fresh();var at=TimedEffects.Now.AddHours(1);other.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);
        var first=DruidOakBody.CreatePrepared(p,Point(p),at);var second=DruidOakBody.CreatePrepared(other,Point(other),at);
        try
        {
            DruidOakAuras.Pulse(first,at);DruidOakAuras.Pulse(second,at);
            Require(DruidOakAuras.Percent(first)==60&&DruidOakAuras.Percent(second)==60&&first.MaximumLife256==24576&&second.MaximumLife256==24576
                && DruidOakAuras.Percent(p)==30,"Distinct control/recipient state composition");
            var lease=DruidOakAuras.State(p);Require(lease.Creator==first.Serial.Value&&lease.Supplier==second.Serial.Value,"Equal rank creator versus refresher");
            DruidOakAuras.Apply(first,p,20,at);var high=DruidOakAuras.State(p);int life=SkillHealth.Balance256(p);
            Require(high.Percent==125&&!DruidOakAuras.Apply(second,p,1,at.AddMilliseconds(40))&&DruidOakAuras.State(p)==high&&SkillHealth.Balance256(p)==life,"Weaker rank replaced live aura");
            other.MoveToWorld(new(first.X+15,first.Y,first.Z),first.Map);Require(DruidOakAuras.Recipient(first,p,other,30),"Closed radius boundary");
            other.X++;Require(!DruidOakAuras.Recipient(first,p,other,30),"Outside radius accepted");
            Train(p,"druid.poisoncreeper");var vine=DruidCreeperBody.CreatePrepared(p,Point(p),at);
            Require(!DruidOakAuras.Recipient(first,p,vine,30),"isAtt0vine received ordinary aura");vine.Delete();
            Train(p,"druid.summonspiritwolf");var wolf=DruidSpiritWolfBody.CreatePrepared(p,Point(p),at);int maximum=wolf.MaximumLife256;
            DruidOakAuras.Apply(first,wolf,1,at);Require(wolf.MaximumLife256==maximum+ClassResources.Percentage(maximum,30),"Exact-life wolf missed Oak consumer");
            var snapshot=DruidOakAuras.Snapshot();DruidOakAuras.Restore(JsonSerializer.Deserialize<OakAuraSave>(JsonSerializer.Serialize(snapshot)));
            Require(DruidOakAuras.State(p)==high,"Round-trip aura state changed rank/source");
            p.MoveToWorld(new(1420,1698,0),Map.Trammel);p.Z=p.Map.GetAverageZ(p.X,p.Y);first.MoveToWorld(Point(p),p.Map);
            Require(SpellHelper.IsTown(first.Location,p)&&!DruidOakAuras.Recipient(first,p,p,30),"Source0x100town target filter");
            Require(DruidOakAuras.Pulse(first,at)&&DruidOakAuras.State(first,true)!=null,"Town self control aura was incorrectly target-filtered");
        }
        finally{End(p);End(other);}
    }
    private static void Lifecycle()
    {
        var p=Fresh();var at=TimedEffects.Now.AddHours(1);var body=DruidOakBody.CreatePrepared(p,Point(p),at);var facets=SummonDifficulties.Facets();
        try
        {
            SkillHealth.SetBalance256(body,6007);body.Advance(at.AddMinutes(5),false);
            Require(body.Map==Map.Internal&&SkillHealth.Balance256(body)==6007,"Offline Oak healed or remained active");
            body.Advance(body.Prepared.NextRecoveryUtc,true);Require(body.Map==p.Map&&SkillHealth.Balance256(body)==6018,"Oak resumed with catch-up heal");
            Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _),"Oak wardrobe park");int life=SkillHealth.Balance256(body);
            body.Advance(body.Prepared.NextRecoveryUtc.AddMinutes(5),true);Require(body.Map==Map.Internal&&body.Permitted(p)&&SkillHealth.Balance256(body)==life,"Inactive history healed or lost Oak");
            Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Druid,out _,out _),"Oak wardrobe restore");body.Advance(body.Prepared.NextRecoveryUtc,true);
            var orphan=DruidOakBody.CreatePrepared(p,Point(p),at);orphan.SetControlMaster(null);orphan.Advance(at.AddMilliseconds(40),true);Require(orphan.Deleted,"Orphan Oak survived");
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
            life=SkillHealth.Balance256(body);p.MoveToWorld(p.Location,Map.Felucca);
            Require(body.Prepared.Difficulty==MonsterDifficulty.Hell&&body.PoisonResistance==200&&body.MaximumLife256==15360&&SkillHealth.Balance256(body)==life,"Oak facet stats changed injury or invented noRatio HP");
            p.Kill();Require(body.Deleted,"Owner death retained Oak");p.Corpse?.Delete();
        }
        finally{SummonDifficulties.SetFacets(facets);End(p);}
    }
    private static void ClockAndSource()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"oak-aura-source-probe.json")));int contexts=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();var aura=DruidOakStats.Aura(N(0));var first=row[6][0];
            Require(N(4)-N(1)==DruidOakAuras.PeriodFrames&&first[4].GetInt32()==DruidOakAuras.LeaseFrames
                && first[1].GetInt32()==161&&first[3].GetInt32()==aura.LifePercent&&first[2].GetInt32()==aura.Rank,"Complete do65source timing/values");
            if(row[6].GetArrayLength()>1){Require(row[6][1][1].GetInt32()==149,"Separate source recipient state");}contexts++;
        }
        Require(contexts==440,"Incomplete do65source contexts");
        var p=Fresh();var at=TimedEffects.Now.AddHours(1);var body=DruidOakBody.CreatePrepared(p,Point(p),at);
        try
        {
            body.Advance(at.AddMilliseconds(199),true);Require(DruidOakAuras.Percent(body)==0&&DruidOakAuras.Percent(p)==0,"Aura fired before five frames");
            body.Advance(at.AddMilliseconds(200),true);Require(DruidOakAuras.Percent(body)==30&&DruidOakAuras.Percent(p)==30,"Fifth-frame aura did not fire");
            var first=DruidOakAuras.State(p);p.X=body.X+20;
            body.Advance(at.AddMilliseconds(399),true);Require(DruidOakAuras.State(p)==first,"Early pulse changed lease");
            body.Advance(at.AddMilliseconds(400),true);Require(DruidOakAuras.State(p)==first&&DruidOakAuras.State(body,true).UntilUtc==at.AddMilliseconds(640),"Pulse did not renew self independently");
            DruidOakAuras.Expire(at.AddMilliseconds(439));Require(DruidOakAuras.Percent(p)==30,"Six-frame lease ended early");
            DruidOakAuras.Expire(at.AddMilliseconds(440));Require(DruidOakAuras.Percent(p)==0&&DruidOakAuras.Percent(body)==30,"Leaver lost wrong state/expiry");
            body.Advance(at.AddMinutes(5),false);int life=SkillHealth.Balance256(body);
            DruidOakAuras.Expire(at.AddMinutes(5));int after=SkillHealth.Balance256(body);
            Require(body.Map==Map.Internal&&after<=life&&DruidOakAuras.Percent(body)==0,"Offline emitter kept aura or healed on expiry");
        }
        finally{End(p);}
    }
    private static void Check()
    {
        Require(!DruidCombat.Implemented(CombatAbility.OakSage),"Prepared actor exposed learned Oak");ClockAndSource();LifeAndStacking();RecipientsAndOverlap();Lifecycle();
        var p=Fresh();var at=TimedEffects.Now.AddDays(2);var body=DruidOakBody.CreatePrepared(p,Point(p),at);DruidOakAuras.Pulse(body,at);
        SkillHealth.SetBalance256(body,6407);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2+7);PersistentMinions.Suspend(p);Malformed(body);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,body.Prepared,SkillHealth.Balance256(body),body.MaximumLife256,BattleOrders.Regeneration256(body),SkillHealth.Balance256(p),SkillHealth.Maximum256(p),DruidOakAuras.State(body,true),DruidOakAuras.State(p),p.Map.MapID,p.Location)));
        Log.Information("Prepared Oak: saved vulnerable60life body, raw170/200poison and25magic, exact recovery, do65self/recipient states, additive BO, proportional life, expiry/rank/creator, range/town/vine gates and offline/wardrobe/facet/orphan/death controls pass; Totem movement and learned casting pending");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=World.FindMobile((Serial)saved.Oak) as DruidOakBody;
        Require(body is{Alive:true,Deleted:false}&&body.ControlMaster==p&&body.Map==Map.Internal&&body.Permitted(p),"Oak saved owner/history/parking");
        Require(body.SavedText==JsonSerializer.Serialize(saved.Body)&&SkillHealth.Balance256(body)==saved.Life&&body.MaximumLife256==saved.Maximum
            && BattleOrders.Regeneration256(body)==saved.Regeneration&&SkillHealth.Balance256(p)==saved.OwnerLife&&SkillHealth.Maximum256(p)==saved.OwnerMaximum
            && DruidOakAuras.State(body,true)==saved.Self&&DruidOakAuras.State(p)==saved.Recipient,
            $"Reload Oak: bytes={body.SavedText==JsonSerializer.Serialize(saved.Body)}, life={SkillHealth.Balance256(body)}/{saved.Life}, max={body.MaximumLife256}/{saved.Maximum}, regen={BattleOrders.Regeneration256(body)}/{saved.Regeneration}, ownerLife={SkillHealth.Balance256(p)}/{saved.OwnerLife}, ownerMax={SkillHealth.Maximum256(p)}/{saved.OwnerMaximum}, self={DruidOakAuras.State(body,true)==saved.Self}, recipient={DruidOakAuras.State(p)==saved.Recipient}");
        Malformed(body);if(p.Map==Map.Internal){p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);}
        body.Advance(body.Prepared.NextRecoveryUtc,false);Require(SkillHealth.Balance256(body)==saved.Life,"Oak reload replayed offline healing");
        body.Advance(body.Prepared.NextRecoveryUtc,true);Require(body.Map==p.Map&&SkillHealth.Balance256(body)==Math.Min(saved.Maximum,saved.Life+saved.Regeneration),"Oak reload active recovery");
        PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Body=body.Prepared,Life=SkillHealth.Balance256(body)}));
    }
}
