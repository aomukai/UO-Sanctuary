// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class IronGolemRetaliationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(IronGolemRetaliationChecks));
    private sealed record Saved(uint Owner,uint Body,uint Item,string Gear,int Flat,int Life,int Maximum,bool Replaced,string State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"iron-golem-retaliation-fixtures.json");
    public static void Configure()
    {
        Register("iron-golem-retaliation-check",Check);Register("iron-golem-retaliation-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("iron-golem-retaliation-expiry","Natural aura expiry keeps replaced innate state removed",_=>Core.LoopContext.Post(Expiry));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared flat NPC/golem ATD and same-state replacement;HP/death chronology remains separate",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile owner,string id)
    {
        ClassCreation.Profiles.TryGet(owner.Serial,out var profile);Require(TalentCatalog.TryGet(profile.Class,id,out var talent),"Missing talent "+id);
        foreach(var prior in talent.Prerequisites){Train(owner,prior);}
        if(ClassCreation.Profiles.GetRank(owner.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}
    }
    private static PlayerMobile Fresh(DiabloClass kind=DiabloClass.Necromancer,int dx=0)
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="IronRetaliationOwner"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,kind);ClassCreation.Profiles.AdvanceLevel(p,99);
        Train(p,kind==DiabloClass.Necromancer?"necromancer.irongolem":kind==DiabloClass.Paladin?"paladin.thorns":"druid.spiritofbarbs");
        if(kind==DiabloClass.Necromancer){Train(p,"necromancer.raiseskeleton");}
        FixtureSpace.Corridor(p,6100,24,2900+dx);return p;
    }
    private static void End(PlayerMobile owner)
    {foreach(var pet in(owner.AllFollowers??[]).OfType<BaseCreature>().ToArray()){DruidWolverineAuras.Clear(pet);pet.Delete();}owner.Delete();}
    private static Item Lance(PlayerMobile owner,int dx=1)
    {
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Lance of Yaggai").Id,99,new Random(836),true);
        item.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);return item;
    }
    private static IronGolemBody Body(PlayerMobile owner,int dx=1)
    {var item=Lance(owner,dx);return IronGolemBody.CreatePrepared(owner,item,item.Location);}
    private static void Source()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"iron-golem-retaliation-source-probe.json")));int count=0;
        foreach(var row in source.RootElement.GetProperty("ironFlat").EnumerateArray())
        {
            int rank=row[0].GetInt32();var values=new SkillValues(SkillMechanics.For(CombatAbility.IronGolem),key=>key=="IronGolem"?rank:0,_=>0,99);
            Require(NpcEquipmentRetaliation.IronAmount(values)==row[1].GetInt32(),"Pinned Iron flat tier at "+rank);count++;
        }
        Require(count==255&&source.RootElement.GetProperty("events").GetArrayLength()==80&&source.RootElement.GetProperty("replacement").GetArrayLength()==12,"Incomplete source controls");
    }
    private static void Combat()
    {
        var owner=Fresh();var body=Body(owner);var item=World.FindItem((Serial)IronGolemItems.State(body).Item);
        var enemy=new Ogre{NoKillAwards=true,CantWalk=true,Frozen=true};enemy.SetHits(20000);enemy.MoveToWorld(body.Location,owner.Map);enemy.AIObject.AITimer.Stop();enemy.SetResistance(ResistanceType.Physical,33);TimedEffects.PreventHealing(enemy,TimedEffects.Now);
        var paladinState=PaladinAuras.Snapshot();PlayerMobile paladin=null,druid=null;
        try
        {
            Require(body.Prepared.Recipe.Rank==1&&NpcEquipmentCombat.Bonus(body,"thorns")==8&&PaladinThorns.FlatDamage(body)==23,"Birth/item flat stat78 not combined");
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(836),true);
            Require(owner.EquipItem(helm),"Owner Tarnhelm equip");ClassGear.Refresh(owner);
            Require(SkillMechanics.Rank(owner,CombatAbility.IronGolem)==2&&PaladinThorns.FlatDamage(body)==23,"Owner soft rank rewrote captured innate thorns");
            owner.Backpack.DropItem(helm);ClassGear.Refresh(owner);
            var gear=ClassGear.State(item);ClassGear.Profiles.Store(item,gear with{Identified=false});
            Require(PaladinThorns.FlatDamage(body)==15,"Inactive original suppressed innate thorns or stayed active");ClassGear.Profiles.Store(item,gear);
            long before=SkillHealth.Balance256(enemy);int life=SkillHealth.Balance256(body);
            using(PaladinThorns.Attempt(enemy,body,false)){}
            Require(before-SkillHealth.Balance256(enemy)==23*256*67/100&&life==SkillHealth.Balance256(body),"Miss lost aggregate flat return");
            before=SkillHealth.Balance256(enemy);life=SkillHealth.Balance256(body);AOS.Damage(body,enemy,10,false,100,0,0,0,0,weaponDamage:true);
            Require(before-SkillHealth.Balance256(enemy)==23*256*67/100&&life-SkillHealth.Balance256(body)==10*256,"Native hit converted flat damage into percent or duplicated it");
            before=SkillHealth.Balance256(enemy);AOS.Damage(body,enemy,1,false,0,0,0,0,0,weaponDamage:true);
            Require(before-SkillHealth.Balance256(enemy)==23*256*67/100,"Zero-damage weapon attempt lost flat stat");
            before=SkillHealth.Balance256(enemy);AOS.Damage(body,enemy,1,false,100,0,0,0,0,archer:true,weaponDamage:true);AOS.Damage(body,enemy,1,false,100,0,0,0,0,spellDamage:true);
            Require(before==SkillHealth.Balance256(enemy),"Ranged/spell triggered flat ATD");
            var old=ClassCombatHooks.ClassHitCheck;
            try
            {
                ClassCombatHooks.ClassHitCheck=(_,_)=>false;enemy.Frozen=false;before=SkillHealth.Balance256(enemy);life=SkillHealth.Balance256(body);
                ((BaseWeapon)enemy.Weapon).OnSwing(enemy,body);
                Require(before-SkillHealth.Balance256(enemy)==23*256*67/100&&SkillHealth.Balance256(body)==life,"Actual missed swing lost/doubled return");
            }
            finally{ClassCombatHooks.ClassHitCheck=old;enemy.Frozen=true;}
            var skeleton=new SkeletonGuardian();skeleton.Initialize(owner,1,0);Require(skeleton.SetControlMaster(owner),"Item-only NPC control");
            skeleton.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);skeleton.AIObject.AITimer.Stop();var ordinary=Lance(owner,5);
            try
            {
                Require(IronGolemItems.TryAttach(owner,skeleton,ordinary,out var issue),"Item-only NPC binding: "+issue);
                Require(PaladinThorns.FlatDamage(skeleton)==8,"Ordinary NPC inherited Iron Golem's innate thorns");
                before=SkillHealth.Balance256(enemy);using(PaladinThorns.Attempt(enemy,skeleton,false)){}
                Require(before-SkillHealth.Balance256(enemy)==8*256*67/100,"Ordinary original-item flat ATD consumer");
            }
            finally{skeleton.Delete();if(!ordinary.Deleted){ordinary.Delete();}}
            druid=Fresh(DiabloClass.Druid,64);druid.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);
            var spirit=DruidOakBody.CreatePrepared(druid,new(owner.X+3,owner.Y,owner.Z),TimedEffects.Now.AddHours(1),CombatAbility.SpiritOfBarbs);
            Require(DruidWolverineAuras.Pulse(spirit,TimedEffects.Now.AddHours(1))&&DruidWolverineAuras.FlatDamage(body)==32&&PaladinThorns.FlatDamage(body)==55,"Distinct Barbs state did not join item/birth stat");
            paladin=Fresh(DiabloClass.Paladin,128);paladin.MoveToWorld(new(owner.X+2,owner.Y+1,owner.Z),owner.Map);paladin.Mana=0;
            var pulse=typeof(PaladinAuras).GetMethod("Pulse",BindingFlags.NonPublic|BindingFlags.Static);Require(pulse!=null,"Actual aura pulse");
            pulse.Invoke(null,[paladin,CombatAbility.Thorns,TimedEffects.Now.AddHours(1)]);
            Require(PaladinAuras.Effect(body,CombatAbility.Thorns) is{Percent:4,DamagePercent:250}&&body.Prepared.IntrinsicThornsReplaced
                &&PaladinThorns.FlatDamage(body)==44,"Real Paladin pulse added innate same-state thorns instead of replacing it");
            before=SkillHealth.Balance256(enemy);life=SkillHealth.Balance256(body);AOS.Damage(body,enemy,10,false,100,0,0,0,0,weaponDamage:true);
            Require(before-SkillHealth.Balance256(enemy)==44*256*67/100+10*256*250/100*67/100&&life-SkillHealth.Balance256(body)==10*256,"Separate aggregate flat/percentage packets");
            PaladinAuras.Clear(body);Require(PaladinThorns.FlatDamage(body)==40&&body.Prepared.IntrinsicThornsReplaced,"Removing external aura recreated the old birth list");
            DruidWolverineAuras.Clear(body);Require(PaladinThorns.FlatDamage(body)==8,"Distinct Barbs expiry removed item or recreated innate thorns");
            PaladinAuras.ElementalWeapon(new(enemy.Serial.Value,paladin.Serial.Value,CombatAbility.Thorns,1,4,TimedEffects.Now.AddHours(1),DamagePercent:250));
            before=SkillHealth.Balance256(enemy);life=SkillHealth.Balance256(body);AOS.Damage(body,enemy,10,false,100,0,0,0,0,weaponDamage:true);
            Require(before-SkillHealth.Balance256(enemy)==8*256*67/100&&life-SkillHealth.Balance256(body)==10*256,"Return packet recursed");
            enemy.SetResistance(ResistanceType.Physical,100);before=SkillHealth.Balance256(enemy);using(PaladinThorns.Attempt(enemy,body,false)){}
            Require(SkillHealth.Balance256(enemy)==before,"Flat return bypassed physical immunity");
        }
        finally{PaladinAuras.Restore(paladinState);DruidWolverineAuras.Clear(body);enemy.Delete();if(paladin!=null){End(paladin);}if(druid!=null){End(druid);}End(owner);}
    }
    private static void Stage()
    {
        var owner=Fresh();var rows=new System.Collections.Generic.List<Saved>();
        for(int i=0;i<2;i++)
        {
            var body=Body(owner,i+1);if(i==1){body.ReplaceIntrinsicThorns();}
            var item=World.FindItem((Serial)IronGolemItems.State(body).Item);SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)/3+17);
            rows.Add(new(owner.Serial.Value,body.Serial.Value,item.Serial.Value,JsonSerializer.Serialize(ClassGear.State(item)),PaladinThorns.FlatDamage(body),
                SkillHealth.Balance256(body),SkillHealth.Maximum256(body),body.Prepared.IntrinsicThornsReplaced,JsonSerializer.Serialize(body.Prepared)));body.Internalize();
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(rows));owner.Internalize();
    }
    private static void Expiry()
    {
        PlayerMobile owner=null;
        try
        {
            owner=Fresh();var body=Body(owner);int life=SkillHealth.Balance256(body);
            // Supplied short lease isolates expiry; Combat exercises a real
            // zero-mana player pulse and recipient eligibility separately.
            PaladinAuras.ElementalWeapon(new(body.Serial.Value,owner.Serial.Value,CombatAbility.Thorns,1,4,TimedEffects.Now.AddMilliseconds(80),DamagePercent:250));
            Require(body.Prepared.IntrinsicThornsReplaced&&PaladinThorns.FlatDamage(body)==12,"Expiry fixture did not replace birth state");
            Timer.StartTimer(TimeSpan.FromMilliseconds(200),()=>
            {
                try
                {
                    Require(PaladinAuras.Effect(body,CombatAbility.Thorns)==null&&body.Prepared.IntrinsicThornsReplaced&&PaladinThorns.FlatDamage(body)==8
                        &&SkillHealth.Balance256(body)==life,"Natural aura expiry restored old innate thorns or changed life");
                    PaladinAuras.Clear(body);End(owner);Log.Information("iron-golem-retaliation-expiry: passed");
                }
                catch(Exception e){PaladinAuras.Clear(body);End(owner);Log.Error(e,"iron-golem-retaliation-expiry: failed");}
            });
        }
        catch(Exception e){if(owner!=null){End(owner);}Log.Error(e,"iron-golem-retaliation-expiry: failed");}
    }
    private static void Check()
    {
        Source();Combat();Stage();
        Log.Information("Iron Golem retaliation:255pinned tier controls,native item/birth/Barbs aggregate,actual zero-mana Paladin same-state replacement,miss/hit/zero/ranged/spell,physical immunity/nonrecursion and saved state pass;full HP/death/event chronology and current replacement behavior remain separate");
    }
    private static void Reload()
    {
        Source();foreach(var saved in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var body=World.FindMobile((Serial)saved.Body) as IronGolemBody;var item=World.FindItem((Serial)saved.Item);
            Require(body is{Alive:true,Deleted:false}&&body.ControlMaster?.Serial.Value==saved.Owner&&item?.Parent==body&&body.Prepared.IntrinsicThornsReplaced==saved.Replaced
                &&PaladinThorns.FlatDamage(body)==saved.Flat&&SkillHealth.Balance256(body)==saved.Life&&SkillHealth.Maximum256(body)==saved.Maximum
                &&JsonSerializer.Serialize(ClassGear.State(item))==saved.Gear&&JsonSerializer.Serialize(body.Prepared)==saved.State,"Saved original/birth/replaced-state changed");
        }
        Combat();
    }
}
