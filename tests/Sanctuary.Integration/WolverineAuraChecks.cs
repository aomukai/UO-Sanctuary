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

public static class WolverineAuraChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WolverineAuraChecks));
    private sealed record Saved(uint Owner,uint Body,OakBodyState State,int Life,int Maximum,WolverineLease Self,WolverineLease Recipient);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"wolverine-aura-fixtures.json");private static int _scene;
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("wolverine-aura-check",Check);Register("wolverine-aura-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Two-stat Wolverine aura and shared weapon/summon pools",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Train(PlayerMobile p,string id)
    {
        ClassCreation.Profiles.TryGet(p.Serial,out var profile);Require(TalentCatalog.TryGet(profile.Class,id,out var talent),"Talent");
        foreach(var prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(p,prior);}}
        if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    private static PlayerMobile Fresh(DiabloClass kind=DiabloClass.Druid)
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="OakSageBodyFixtureWolverineAura"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,kind);ClassCreation.Profiles.AdvanceLevel(p,99);
        Train(p,kind==DiabloClass.Druid?"druid.heartofwolverine":"barbarian.battlecommand");
        FixtureSpace.Corridor(p,6750,24,1900+32*_scene++);return p;
    }
    private static DruidWolverineBody Body(PlayerMobile p,DateTime at,int dx=2)=>(DruidWolverineBody)DruidOakBody.CreatePrepared(p,new(p.X+dx,p.Y,p.Z),at,CombatAbility.HeartOfWolverine);
    private static void End(PlayerMobile p)
    {
        foreach(var pet in p.AllFollowers??[]){DruidWolverineAuras.Clear(pet);DruidOakAuras.Clear(pet);BattleCommand.Clear(pet);}
        DruidWolverineAuras.Clear(p);DruidOakAuras.Clear(p);NecromancerEffects.Profiles.Clear(p);PersistentMinions.Dismiss(p);p.Delete();
    }
    private static void Source()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"wolverine-aura-source-probe.json")));int count=0;
        foreach(var row in source.RootElement.EnumerateArray())
        {
            int rank=row[0].GetInt32();var at=DateTime.UnixEpoch.AddMilliseconds(row[1].GetInt32()*40);
            foreach(var call in row[6].EnumerateArray())
            {
                bool control=call[1].GetInt32()==162;var lease=DruidWolverineAuras.Replacement(null,200,100,rank,at,control);
                Require(call[1].GetInt32()==(control?162:148)&&call[2].GetInt32()==rank&&call[3].GetInt32()==119&&call[5].GetInt32()==25
                    &&lease.AttackPercent==call[4].GetInt32()&&lease.DamagePercent==call[6].GetInt32()
                    &&lease.UntilUtc==at.AddMilliseconds(call[7].GetInt32()*40),"Complete source two-stat callback transport");
            }
            Require(row[4].GetInt32()==row[1].GetInt32()+DruidOakAuras.PeriodFrames,"Source periodic rate");count++;
        }
        Require(count==440,"Incomplete source aura matrix");
    }
    private static void Pools()
    {
        var p=Fresh();var at=TimedEffects.Now.AddHours(1);var body=Body(p,at);var prior=PaladinAuras.Snapshot();
        var enemy=new GraveyardZombie{CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();
        p.RawStr=150;p.RawDex=150;
        var weapon=new Longsword{Resource=CraftResource.Valorite};ClassGear.Profiles.Convert(weapon,99,GearRarity.Normal,new Random(173));
        var gear=ClassGear.State(weapon);ClassGear.Profiles.Store(weapon,gear with{CraftedEquipment=true,MaterialRolls=[new("damage-fire",5)]});
        try
        {
            Require(p.EquipItem(weapon),"Wolverine weapon");ClassGear.Refresh(p);
            int life=SkillHealth.Balance256(p),maximum=SkillHealth.Maximum256(p),mana=NecromancerEffects.ManaBalance256(p),stamina=p.Stam;
            int ar=ClassStats.PlayerItemAttackPercent(p),ed=ClassStats.DamageBonus(p,weapon);var raw=ClassStats.WeaponBaseDamage(p);var plain=GearEffects.Damage(p,0,enemy,200,_=>0);
            Require(DruidWolverineAuras.Apply(body,p,1,at)&&ClassStats.PlayerItemAttackPercent(p)==ar+25&&ClassStats.DamageBonus(p,weapon)==ed+20,"Player additive AR/ED consumers");
            var enhanced=GearEffects.Damage(p,0,enemy,200,_=>0);
            Require(plain.Fire>0&&enhanced.Physical>plain.Physical&&enhanced.Fire==plain.Fire&&enhanced.Cold==plain.Cold&&enhanced.Lightning==plain.Lightning&&enhanced.Poison==plain.Poison,"Physical/elemental weapon separation");
            Require(SkillHealth.Balance256(p)==life&&SkillHealth.Maximum256(p)==maximum&&NecromancerEffects.ManaBalance256(p)==mana&&p.Stam==stamina,"Combat aura changed resources");
            var might=new PaladinAuraLease(p.Serial.Value,body.Serial.Value,CombatAbility.Might,1,40,at.AddMinutes(1));
            PaladinAuras.Restore(prior with{Leases=[..prior.Leases,might]});
            NecromancerEffects.Profiles.Store(p,new(new(CombatAbility.Decrepify,enemy.Serial.Value,1,at.AddMinutes(1),enemy.X,enemy.Y)));
            int penalty=NecromancerEffects.DamageStatPercent(p);
            Require(ClassStats.PhysicalDamageBonus(p,weapon)==ed+60+penalty
                &&ClassStats.SkillPhysicalRange256(p,200,0)==ClassStats.PhysicalEndpoints256(raw.Min,raw.Max,ed+60+penalty+200),"Might/Wolverine/curse/skill ED multiplied or applied twice");
            var before=PaladinAuras.NativeWeaponMultiplier(enemy,true);DruidWolverineAuras.Apply(body,enemy,1,at);
            Require(PaladinAuras.NativeWeaponMultiplier(enemy,true)==before+.2&&PaladinAuras.NativeWeaponMultiplier(enemy,false)==1&&CombatAuras.Managed(enemy),"Ordinary NPC physical/weapon gate");
            var pet=new SkeletonGuardian();pet.Initialize(p,10,0);pet.MoveToWorld(new(p.X+3,p.Y+1,p.Z),p.Map);pet.AIObject.AITimer.Stop();
            try
            {
                DruidWolverineAuras.Apply(body,pet,1,at);var expected=SkeletonStats.For(pet.Level,pet.Rank,pet.Mastery,20,pet.SummonDifficulty);
                Require(pet.SkillWeaponDamage==(expected.MinDamage,expected.MaxDamage)&&PaladinAuras.NativeWeaponMultiplier(pet,true)==1,"Skeleton aura missing or double-scaled");
            }
            finally{pet.Delete();}
            Train(p,"druid.summonspiritwolf");var wolf=DruidSpiritWolfWorld.Create(p,new(p.X+4,p.Y+1,p.Z),at,783);
            try
            {
                var birth=wolf.Prepared.Snapshot.Birth;int baseline=ClassStats.AttackRating(wolf);
                DruidWolverineAuras.Apply(body,wolf,1,at);
                Require(ClassStats.AttackRating(wolf)>baseline&&wolf.Prepared.Snapshot.Birth==birth,"Wolf aura AR missing or birth damage mutated");
                var packet=DruidSpiritWolfHit.Packet(birth.ColdMinimum*256,birth.ColdMaximum*256,birth.ColdFrames,0,0,0,0,0,0,_=>0,()=>0);
                Require(packet.Cold256==birth.ColdMinimum*256&&wolf.SkillWeaponDamage==(0,0),"Physical aura invented wolf physical or cold skill scaling");
            }
            finally{wolf.Delete();}
        }
        finally{PaladinAuras.Restore(prior);DruidWolverineAuras.Clear(enemy);enemy.Delete();weapon.Delete();End(p);}
    }
    private static void StateRules()
    {
        var p=Fresh();var at=TimedEffects.Now.AddHours(1);var first=Body(p,at);var second=Body(p,at,3);
        try
        {
            var born=first.Prepared.Birth;Require(first is{PoisonResistance:170}&&first.MaximumLife256==34816&&first.SkillWeaponDamage==(0,0),"Heart body bases/poison/nonattacking behavior");
            Require(CreatureProfiles.Definition(first).Reference=="heartofwolverine"&&CreatureProfiles.For(first).Resistances.Magic==25,"Heart source traits inherited Oak identity");
            Require(!DruidOakAuras.Pulse(first,at)&&DruidWolverineAuras.Pulse(first,at),"Wrong spirit aura route");
            Require(DruidWolverineAuras.State(first,true) is{Rank:1,AttackPercent:25,DamagePercent:20}&&DruidWolverineAuras.State(p)!=null,"Actual self/recipient aura");
            DruidWolverineAuras.Apply(first,p,3,at);var high=DruidWolverineAuras.State(p);
            DruidWolverineAuras.Apply(second,p,3,at.AddMilliseconds(40));var equal=DruidWolverineAuras.State(p);
            Require(equal.Creator==first.Serial.Value&&equal.Supplier==second.Serial.Value&&equal.UntilUtc>high.UntilUtc,"Equal-rank creator/supplier/refresh");
            Require(!DruidWolverineAuras.Apply(first,p,2,at.AddMilliseconds(80))&&DruidWolverineAuras.State(p)==equal,"Lower aura overwrote stronger live lease");
            DruidWolverineAuras.Expire(equal.UntilUtc);Require(DruidWolverineAuras.State(p)==null,"Exact lease expiry");
            var supplier=Fresh(DiabloClass.Barbarian);
            try
            {
                BattleCommand.Apply(supplier,first,1,at);DruidWolverineAuras.Pulse(first,at);
                Require(DruidWolverineAuras.State(first,true) is{Rank:2,AttackPercent:32,DamagePercent:27}&&first.Prepared.Birth==born,"NPC Command changed birth or failed aura rank");
                var boosted=DruidWolverineAuras.State(first,true);BattleCommand.Clear(first);DruidWolverineAuras.Pulse(first,at.AddMilliseconds(40));Require(DruidWolverineAuras.State(first,true)==boosted,"Command removal bypassed stronger lease");
                DruidWolverineAuras.Expire(boosted.UntilUtc);DruidWolverineAuras.Pulse(first,boosted.UntilUtc);Require(DruidWolverineAuras.State(first,true).Rank==1,"Command lower aura did not resume");
            }
            finally{End(supplier);}
            var old=DruidWolverineAuras.Snapshot();string bytes=JsonSerializer.Serialize(old);bool refused=false;
            try{DruidWolverineAuras.Restore(old with{Leases=[old.Leases[0] with{DamagePercent=-1}]});}catch(InvalidDataException){refused=true;}
            Require(refused&&JsonSerializer.Serialize(DruidWolverineAuras.Snapshot())==bytes,"Malformed two-stat state was not atomic");
            var state=first.Prepared;refused=false;try{first.RestorePrepared(state with{Birth=state.Birth with{Life256=15360}});}catch(InvalidDataException){refused=true;}
            Require(refused&&first.Prepared==state,"Oak birth accepted by Heart runtime type");
        }
        finally{End(p);}
    }
    private static void Check()
    {
        Source();Pools();StateRules();var p=Fresh();var at=TimedEffects.Now.AddDays(2);var body=Body(p,at);
        DruidWolverineAuras.Pulse(body,at);SkillHealth.SetBalance256(body,6407);PersistentMinions.Suspend(p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,body.Prepared,SkillHealth.Balance256(body),body.MaximumLife256,DruidWolverineAuras.State(body,true),DruidWolverineAuras.State(p))));
        Log.Information("Wolverine aura:440source contexts, two combat stats, additive player/NPC/skeleton/wolf consumers, Might/curse/skill pools, immutable birth, exact expiry/creator/supplier and NPC Command controls pass");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var body=World.FindMobile((Serial)saved.Body) as DruidWolverineBody;
        Require(body is{Alive:true,Deleted:false}&&body.ControlMaster==p&&body.Map==Map.Internal&&body.SavedText==JsonSerializer.Serialize(saved.State)
            &&SkillHealth.Balance256(body)==saved.Life&&body.MaximumLife256==saved.Maximum&&body.SourceMonsterId=="heartofwolverine"
            &&DruidWolverineAuras.State(body,true)==saved.Self&&DruidWolverineAuras.State(p)==saved.Recipient,"Saved derived spirit/birth/injury/two-stat states changed");
    }
}
