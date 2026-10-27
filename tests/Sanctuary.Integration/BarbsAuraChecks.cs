// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class BarbsAuraChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BarbsAuraChecks));
    private sealed record Saved(uint Owner,uint Body,OakBodyState State,int Life,int Maximum,WolverineLease Self,WolverineLease Recipient);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"barbs-aura-fixtures.json");
    private static int _scene;
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("barbs-aura-check",Check);Register("barbs-aura-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Shared flat ATD, distinct combat-spirit leases and saved Barbs",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Train(PlayerMobile p,string id)
    {
        ClassCreation.Profiles.TryGet(p.Serial,out var profile);Require(TalentCatalog.TryGet(profile.Class,id,out var talent),"Talent");
        foreach(var prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(p,prior);}}
        if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    private static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="OakSageBodyFixtureBarbsAura"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);Train(p,"druid.spiritofbarbs");
        FixtureSpace.Corridor(p,6400,24,2100+32*_scene++);return p;
    }
    private static DruidBarbsBody Body(PlayerMobile p,DateTime at,int dx=2)=>(DruidBarbsBody)DruidOakBody.CreatePrepared(p,new(p.X+dx,p.Y,p.Z),at,CombatAbility.SpiritOfBarbs);
    private static WolverineLease State(Mobile p,bool control=false)=>DruidWolverineAuras.State(p,control,CombatAbility.SpiritOfBarbs);
    private static void End(PlayerMobile p)
    {
        foreach(var pet in p.AllFollowers??[]){DruidWolverineAuras.Clear(pet);DruidOakAuras.Clear(pet);}
        DruidWolverineAuras.Clear(p);PersistentMinions.Dismiss(p);p.Delete();
    }
    private static void Source()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"barbs-aura-source-probe.json")));int count=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            int rank=row[0].GetInt32();var at=DateTime.UnixEpoch.AddMilliseconds(row[1].GetInt32()*40);
            foreach(var call in row[6].EnumerateArray())
            {
                bool control=call[1].GetInt32()==163;var lease=DruidWolverineAuras.Replacement(null,200,100,rank,at,control,CombatAbility.SpiritOfBarbs);
                Require(call[1].GetInt32()==(control?163:147)&&call[3].GetInt32()==78&&lease.FlatDamage==call[4].GetInt32()
                    &&lease.AttackPercent==0&&lease.DamagePercent==0&&lease.Ability==CombatAbility.SpiritOfBarbs
                    &&lease.UntilUtc==at.AddMilliseconds(call[7].GetInt32()*40),"Complete source flat callback transport");
            }
            count++;
        }
        Require(count==440,"Incomplete Barbs aura matrix");
    }
    private static void StateRules()
    {
        var p=Fresh();var at=TimedEffects.Now.AddHours(1);var body=Body(p,at);var second=Body(p,at,3);
        try
        {
            int life=SkillHealth.Balance256(p),maximum=SkillHealth.Maximum256(p),mana=NecromancerEffects.ManaBalance256(p),stamina=p.Stam;
            Require(DruidWolverineAuras.Pulse(body,at)&&State(body,true) is{FlatDamage:32,Rank:1,Control:true}&&State(p) is{FlatDamage:32,Control:false},"Barbs self/recipient pulse");
            Require(DruidWolverineAuras.AttackPercent(p)==0&&DruidWolverineAuras.DamagePercent(p)==0
                &&SkillHealth.Balance256(p)==life&&SkillHealth.Maximum256(p)==maximum&&NecromancerEffects.ManaBalance256(p)==mana&&p.Stam==stamina,"Flat aura changed combat percentages or resources");
            Require(DruidWolverineAuras.Apply(body,p,2,at),"Higher rank");var high=State(p);
            Require(!DruidWolverineAuras.Apply(second,p,1,at.AddMilliseconds(40))&&State(p)==high,"Lower rank bypassed stronger lease");
            Require(DruidWolverineAuras.Apply(second,p,2,at.AddMilliseconds(40))&&State(p).Creator==body.Serial.Value&&State(p).Supplier==second.Serial.Value,"Equal rank lost creator/current supplier");
            var current=State(p);DruidWolverineAuras.Expire(current.UntilUtc.AddTicks(-1));Require(State(p)==current,"Early expiry");
            DruidWolverineAuras.Expire(current.UntilUtc);Require(State(p)==null,"Exact expiry");
            DruidWolverineAuras.Apply(body,p,1,current.UntilUtc);
            var heart=(DruidWolverineBody)DruidOakBody.CreatePrepared(p,new(p.X+4,p.Y,p.Z),at,CombatAbility.HeartOfWolverine);
            Require(DruidWolverineAuras.Apply(heart,p,1,current.UntilUtc)&&State(p).FlatDamage==32&&DruidWolverineAuras.State(p) is{AttackPercent:25,DamagePercent:20},"Distinct Heart/Barbs states overwrote each other");
            var all=DruidWolverineAuras.Snapshot();string bytes=JsonSerializer.Serialize(all);bool rejected=false;
            try{DruidWolverineAuras.Restore(all with{Leases=[State(p) with{FlatDamage=99}]});}catch(InvalidDataException){rejected=true;}
            Require(rejected&&JsonSerializer.Serialize(DruidWolverineAuras.Snapshot())==bytes,"Malformed flat state not atomic");
            // Actual old Heart JSON omits the two new optional fields.
            var old=new WolverineSave([DruidWolverineAuras.State(p)],[]);var json=JsonNode.Parse(JsonSerializer.Serialize(old));
            foreach(var entry in json["Leases"].AsArray()){entry.AsObject().Remove("FlatDamage");entry.AsObject().Remove("Ability");}
            DruidWolverineAuras.Restore(JsonSerializer.Deserialize<WolverineSave>(json.ToJsonString()));
            Require(DruidWolverineAuras.State(p)==old.Leases[0]&&State(p)==null,"Legacy Heart lease did not default to its old identity");DruidWolverineAuras.Restore(all);
            NecromancerEffects.SetMana256(p,0);Require(DruidWolverineAuras.Pulse(body,at.AddSeconds(1))&&NecromancerEffects.ManaBalance256(p)==0,"Barbs required aura mana");
            var singer=new PlayerMobile{Body=0x190,Player=true,Name="BarbsCommandFixture"};singer.AddItem(new Backpack());
            try
            {
                ClassCreation.Profiles.Assign(singer,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(singer,99);Train(singer,"barbarian.battlecommand");
                var birth=body.Prepared.Birth;var commandAt=at.AddSeconds(2);BattleCommand.Apply(singer,body,1,commandAt);DruidWolverineAuras.Pulse(body,commandAt);
                Require(State(body,true) is{Rank:2,FlatDamage:41}&&body.Prepared.Birth==birth,"NPC Command changed birth or failed to raise Barbs aura");
                var boosted=State(body,true);BattleCommand.Clear(body);DruidWolverineAuras.Pulse(body,commandAt.AddMilliseconds(40));Require(State(body,true)==boosted,"Lower Barbs rank overwrote stronger lease");
                DruidWolverineAuras.Expire(boosted.UntilUtc);DruidWolverineAuras.Pulse(body,boosted.UntilUtc);Require(State(body,true) is{Rank:1,FlatDamage:32},"Barbs lower rank did not resume after expiry");
            }
            finally{BattleCommand.Clear(body);singer.Delete();}
        }
        finally{End(p);}
    }
    private static void Combat()
    {
        var p=Fresh();var at=TimedEffects.Now.AddHours(2);var body=Body(p,at);var prior=PaladinAuras.Snapshot();
        var enemy=new Ogre{CantWalk=true,Frozen=true,NoKillAwards=true};enemy.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();enemy.SetHits(10000);enemy.SetResistance(ResistanceType.Physical,33);
        var helm=RuneSocketChecks.Host(p,typeof(PlateHelm));GemActivationChecks.Install(p,GemActivationChecks.Gem(p,"Amber"),helm);
        try
        {
            p.AddItem(helm);Require(helm.Parent==p,"Actual Amber helm");ClassGear.Refresh(p);Require(ClassGear.Bonus(p,"thorns")==20,"Actual item ATD");
            Require(DruidWolverineAuras.Pulse(body,at),"Actual self/owner Barbs pulse");
            PaladinAuras.ElementalWeapon(new(p.Serial.Value,body.Serial.Value,CombatAbility.Thorns,1,4,at.AddDays(1),DamagePercent:250));
            Require(PaladinThorns.FlatDamage(p)==56,"Barbs/item/Thorns flat aggregation");
            int flat=56*256*67/100;long before=SkillHealth.Balance256(enemy);
            using(PaladinThorns.Attempt(enemy,p,false)){}
            Require(before-SkillHealth.Balance256(enemy)==flat,"Miss split the flat sum before fractional mitigation");
            before=SkillHealth.Balance256(enemy);int hp=SkillHealth.Balance256(p);
            AOS.Damage(p,enemy,10,false,100,0,0,0,0,weaponDamage:true);
            Require(before-SkillHealth.Balance256(enemy)==flat+10*256*250/100*67/100&&hp-SkillHealth.Balance256(p)==10*256,"Actual native combined flat/percentage packets or duplicate late gem reflection");
            before=SkillHealth.Balance256(enemy);AOS.Damage(p,enemy,10,false,100,0,0,0,0,archer:true,weaponDamage:true);AOS.Damage(p,enemy,10,false,100,0,0,0,0,spellDamage:true);
            Require(SkillHealth.Balance256(enemy)==before,"Ranged/spell triggered ATD");
            var hit=ClassCombatHooks.ClassHitCheck;
            try
            {
                ClassCombatHooks.ClassHitCheck=(_,_)=>false;enemy.Frozen=false;before=SkillHealth.Balance256(enemy);hp=SkillHealth.Balance256(p);
                ((BaseWeapon)enemy.Weapon).OnSwing(enemy,p);
                Require(before-SkillHealth.Balance256(enemy)==flat&&SkillHealth.Balance256(p)==hp,"Actual native missed swing lost/doubled ATD");
            }
            finally{ClassCombatHooks.ClassHitCheck=hit;enemy.Frozen=true;}
            DruidWolverineAuras.Apply(body,enemy,1,at);before=SkillHealth.Balance256(enemy);hp=SkillHealth.Balance256(p);
            AOS.Damage(p,enemy,10,false,100,0,0,0,0,weaponDamage:true);
            Require(before-SkillHealth.Balance256(enemy)==flat+10*256*250/100*67/100&&hp-SkillHealth.Balance256(p)==10*256,"Return recursively generated ATD");
            enemy.SetResistance(ResistanceType.Physical,100);before=SkillHealth.Balance256(enemy);AOS.Damage(p,enemy,10,false,100,0,0,0,0,weaponDamage:true);
            Require(SkillHealth.Balance256(enemy)==before,"ATD bypassed physical immunity");
            enemy.SetResistance(ResistanceType.Physical,33);before=SkillHealth.Balance256(enemy);int bodyLife=SkillHealth.Balance256(body);
            AOS.Damage(body,enemy,10,false,100,0,0,0,0,weaponDamage:true);
            Require(before-SkillHealth.Balance256(enemy)==32*256*67/100&&bodyLife-SkillHealth.Balance256(body)==10*256*75/100,"Actual vulnerable Barbs self ATD/physical resistance or recursion");
            before=SkillHealth.Balance256(enemy);bodyLife=SkillHealth.Balance256(body);AOS.Damage(body,enemy,10,false,0,100,0,0,0,spellDamage:true);
            Require(SkillHealth.Balance256(enemy)==before&&bodyLife-SkillHealth.Balance256(body)==10*256*75/100,"Barbs elemental damage/spell exclusion");
            bodyLife=SkillHealth.Balance256(body);AOS.Damage(body,enemy,10,false,0,0,0,100,0,spellDamage:true);
            Require(SkillHealth.Balance256(body)==bodyLife&&body.PoisonResistance>=100,"Barbs poison passive/resistance immunity");
        }
        finally{PaladinAuras.Restore(prior);DruidWolverineAuras.Clear(enemy);enemy.Delete();End(p);}
    }
    private static void Check()
    {
        Source();StateRules();Combat();var p=Fresh();var at=TimedEffects.Now.AddDays(30);var body=Body(p,at);
        DruidWolverineAuras.Pulse(body,at);SkillHealth.SetBalance256(body,6407);PersistentMinions.Suspend(p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,body.Prepared,SkillHealth.Balance256(body),body.MaximumLife256,State(body,true),State(p))));
        Log.Information("Barbs:440source callback transports,distinct states/legacy Heart JSON,rank/creator/expiry/zero-mana/NPC Command, vulnerable self/elemental/poison and actual Amber+Thorns aggregation/native miss/hit/ranged/spell/immunity/nonrecursion pass; full HP/death order remains separate");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var body=World.FindMobile((Serial)saved.Body) as DruidBarbsBody;
        Require(body is{Alive:true,Deleted:false}&&body.ControlMaster==p&&body.Map==Map.Internal&&body.SavedText==JsonSerializer.Serialize(saved.State)
            &&SkillHealth.Balance256(body)==saved.Life&&body.MaximumLife256==saved.Maximum&&body.SourceMonsterId=="spiritofbarbs"
            &&State(body,true)==saved.Self&&State(p)==saved.Recipient,"Saved Barbs type/birth/injury/aura identity changed");
        Combat();
    }
}
