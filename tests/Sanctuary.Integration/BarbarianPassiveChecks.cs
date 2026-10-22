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

public static class BarbarianPassiveChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BarbarianPassiveChecks));
    private static readonly CombatAbility[] Skills=[CombatAbility.IncreasedStamina,CombatAbility.IronSkin,CombatAbility.IncreasedSpeed,CombatAbility.NaturalResistance];
    private sealed record Saved(uint Owner,uint Helm,uint Boots,uint Amulet,Point3D Origin,int Map,int Stamina,int Defense,int Fire,int Cold,int Walk,int Run);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"barbarian-passives.json");
    public static void Configure(){Register("barbarian-passives-check",Check);Register("barbarian-passives-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Barbarian passive native stats, movement, damage and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static PlayerMobile Fresh()
    {
        var actor=new PlayerMobile{Body=0x190,Name="BarbarianPassiveFixture",Player=true};actor.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(actor,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(actor,99);
        FixtureSpace.Place(actor,new(6100,1800,0),Map.Trammel);actor.Hits=actor.HitsMax;actor.Stam=actor.StamMax;return actor;
    }
    private static void Train(PlayerMobile owner,CombatAbility ability,int rank)
    {string talent=SkillMechanics.For(ability).TalentId;while(ClassCreation.Profiles.GetRank(owner.Serial,talent)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,talent,out _),"Cannot train "+talent);}}
    private static Item Equip(PlayerMobile owner,string source)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);Require(owner.EquipItem(item),"Cannot equip "+source);ClassGear.Refresh(owner);return item;}
    private static int BaseStamina(PlayerMobile owner)
    {
        var stats=ClassStats.For(DiabloClass.Barbarian);
        return stats.Stamina+(98*stats.StaminaLevel4+(ClassStats.Vitality(owner)-stats.Vitality)*stats.StaminaVitality4)/4+AosAttributes.GetValue(owner,AosAttribute.BonusStam);
    }
    private static int DefenseBase(PlayerMobile owner)=>owner.Dex/4+owner.Items.Where(x=>ClassGear.Active(owner,x)).Sum(ClassGear.Defense)+NamedGear.SetBonus(owner,"ac");
    private static void Goldens()
    {
        string root=Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT");
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(root,"tools/reference/barbarian-passive-preflight.json")));
        foreach(var row in source.RootElement.GetProperty("firstBatch").EnumerateArray())
        {
            var skill=SkillMechanics.All.Single(x=>x.TalentId==row.GetProperty("talentId").GetString());
            foreach(var point in row.GetProperty("projectedTableGoldens").EnumerateArray())
            {
                int rank=point.GetProperty("effectiveRank").GetInt32();var values=new SkillValues(skill,_=>rank,_=>0,99);
                Require(values.Eval("passivecalc1")==point.GetProperty("percent").GetInt32(),"Pinned passive rank formula mismatch");
                if(skill.Ability==CombatAbility.NaturalResistance)
                {for(int i=2;i<=4;i++){Require(values.Eval("passivecalc"+i)==values.Eval("passivecalc1"),"Natural Resistance channel mismatch");}}
            }
        }
    }
    private static void Native(PlayerMobile owner)
    {
        int stamina=BarbarianCombat.Passive(owner,CombatAbility.IncreasedStamina),armor=BarbarianCombat.Passive(owner,CombatAbility.IronSkin),speed=BarbarianCombat.Passive(owner,CombatAbility.IncreasedSpeed),resist=BarbarianCombat.Passive(owner,CombatAbility.NaturalResistance);
        Require(owner.StamMax==BaseStamina(owner)*(100+stamina+PaladinVigor.StaminaPercent(owner))/100,"Passive native stamina or additive Vigor pool");
        Require(ClassStats.Defense(owner)==ClassStats.ScaleDefense(DefenseBase(owner),armor+PaladinAuras.DefensePercent(owner)),"Passive defense percent composition");
        int walk=EquipmentBalance.RunDelay(Server.Movement.Movement.WalkFootDelay,0,speed+PaladinVigor.VelocityPercent(owner));
        int run=EquipmentBalance.RunDelay(Server.Movement.Movement.RunFootDelay,EquipmentBalance.RunPenalty(owner),speed+PaladinVigor.VelocityPercent(owner)+CombatPotions.RunBonus(owner));
        Require(owner.ComputeMovementSpeed(Direction.East,false)==walk && owner.ComputeMovementSpeed(Direction.East|Direction.Running,false)==run,"Native walk/run skill velocity or item FRW composition");
        foreach(var type in new[]{ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison})
        {Require(ClassGear.RawResistance(owner,type)==resist+ClassGear.Bonus(owner,type==ResistanceType.Fire?"res-fire":type==ResistanceType.Cold?"res-cold":type==ResistanceType.Energy?"res-ltng":"res-pois"),"Natural Resistance not in raw elemental pool");}
        Require(BarbarianCombat.Resistance(owner,ResistanceType.Physical)==0 && NamedElements.MagicResistance(owner)==0,"Natural Resistance granted physical/magic defense");
    }
    private static void Damage(PlayerMobile owner)
    {
        var enemy=new Ogre{NoKillAwards=true,Frozen=true,CantWalk=true};enemy.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);enemy.AIObject.AITimer.Stop();
        try
        {
            foreach(var (fire,cold,poison,lightning,resistance) in new[]{(100,0,0,0,owner.FireResistance),(0,100,0,0,owner.ColdResistance),(0,0,100,0,owner.PoisonResistance),(0,0,0,100,owner.EnergyResistance)})
            {
                owner.Hits=owner.HitsMax;SkillHealth.Clear(owner);int hp=owner.Hits;
                AOS.Damage(owner,enemy,100,false,0,fire,cold,poison,lightning);
                Require(owner.Hits==hp-(100-resistance),"Native typed damage ignored Natural Resistance/cap");
            }
            Require(owner.FireResistance==85 && NamedCombat.ResistanceCap(owner,ResistanceType.Cold)==75
                && NamedElements.Resistance(enemy,owner,"fire",owner.FireResistance,50)==68,"Natural Resistance changed maximum or capped before pierce");
        }
        finally{enemy.Delete();}
        owner.Hits=owner.HitsMax;SkillHealth.Clear(owner);ClassCombat.Stop(owner);ClassCombat.For(owner).RecoveryUntil=Core.TickCount;
    }
    private static void Lifecycle()
    {
        var actor=Fresh();foreach(var skill in Skills){Train(actor,skill,1);}int maximum=actor.StamMax;
        Require(ClassCreation.Profiles.TryActivateHistory(actor,DiabloClass.Paladin),"Barbarian history exit");ClassGear.Refresh(actor);
        Require(Skills.All(x=>BarbarianCombat.Passive(actor,x)==0),"Inactive Barbarian history leaked passives");
        Require(ClassCreation.Profiles.TryActivateHistory(actor,DiabloClass.Barbarian),"Barbarian history return");ClassGear.Refresh(actor);
        Require(actor.StamMax==maximum && BarbarianCombat.Passive(actor,CombatAbility.NaturalResistance)==12,"Barbarian history lost passive rank");
        actor.Kill();actor.Resurrect();ClassGear.Refresh(actor);
        Require(actor.Alive && actor.StamMax==maximum && BarbarianCombat.Passive(actor,CombatAbility.IncreasedSpeed)==13,"Death/resurrection lost learned passives");
        ClassCreation.Profiles.TryGet(actor.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(actor,profile);BarbarianCombat.Refresh(actor);
        Require(Skills.All(x=>BarbarianCombat.Passive(actor,x)==0) && actor.Stam<=actor.StamMax && actor.StamMax==190 && actor.FireResistance==0,"Respec retained passive stats or excess stamina");
        actor.Corpse?.Delete();actor.Delete();
    }
    private static void Check()
    {
        Goldens();Lifecycle();var owner=Fresh();owner.MoveToWorld(ClassSkillChecks.Corridor(owner,6100,1800),owner.Map);
        int before=owner.StamMax;Require(before==190 && ClassStats.Defense(owner)==5 && owner.FireResistance==0,"Barbarian baseline fixture");
        foreach(var skill in Skills){Train(owner,skill,1);}
        Require(owner.Stam==before && owner.StamMax==247 && ClassStats.Defense(owner)==6 && owner.FireResistance==12,"Training failed live refresh or granted free stamina");Native(owner);
        foreach(var skill in Skills)
        {
            Require(!SkillBindings.Instance.Assign(owner,true,skill) && !SkillBindings.Instance.Assign(owner,false,skill) && !SkillBindings.Instance.AssignQuick(owner,0,skill),"Passive accepted hotkey/input binding");
            Train(owner,skill,20);Require(!ClassCreation.Profiles.TryTrain(owner.Serial,SkillMechanics.For(skill).TalentId,out _),"Passive allocated cap exceeded20");
        }
        Native(owner);var helm=Equip(owner,"Tarnhelm");var boots=Equip(owner,"Treads of Cthon");var amulet=Equip(owner,"Nokozan Relic");
        Require(Skills.All(x=>SkillMechanics.Rank(owner,x)==21) && BarbarianCombat.Passive(owner,CombatAbility.IncreasedStamina)==330
            && BarbarianCombat.Passive(owner,CombatAbility.IronSkin)==230 && BarbarianCombat.Passive(owner,CombatAbility.NaturalResistance)==68,"Real gear effective passive ranks");Native(owner);
        var paladin=ClassSkillChecks.Player(DiabloClass.Paladin);paladin.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);
        Require(SkillBindings.Instance.Assign(paladin,false,CombatAbility.Vigor) && PaladinVigor.StaminaPercent(owner)==50,"Barbarian Vigor recipient");Native(owner);
        PaladinAuras.Clear(owner);PaladinAuras.Clear(paladin);paladin.Delete();Native(owner);
        owner.Direction=Direction.East;Require(owner.Move(Direction.East),"Passive walking native step");
        owner.Direction=Direction.West;Require(owner.Move(Direction.West|Direction.Running) && ClassMovement.IsRunning(owner,Core.TickCount) && ClassStats.Defense(owner)==0,"Running did not suppress Iron Skin defense");
        ClassCombat.For(owner).RunningUntil=0;Native(owner);Damage(owner);Seed(owner,helm,boots,amulet);
        // A separate probe checks that training from full stamina starts useful native recovery.
        var probe=Fresh();probe.MoveToWorld(new(owner.X+50,owner.Y,owner.Z),owner.Map);int original=probe.Stam;
        Train(probe,CombatAbility.IncreasedStamina,1);Require(probe.Stam==original && probe.StamMax>original,"Stamina training refilled resource");
        Timer.StartTimer(TimeSpan.FromMilliseconds(5500),()=>
        {
            try{Require(probe.Stam>original && probe.Stam<probe.StamMax,"Native stamina recovery did not start after passive max increased");Log.Information("barbarian-passives-result: passed");}
            catch(Exception e){Log.Error(e,"barbarian-passives-result: failed");}finally{probe.Delete();}
        });
    }
    private static void Seed(PlayerMobile owner,Item helm,Item boots,Item amulet)
    {
        owner.Hits=owner.HitsMax;owner.Mana=owner.ManaMax;owner.Stam=owner.StamMax;
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,helm.Serial.Value,boots.Serial.Value,amulet.Serial.Value,owner.Location,owner.Map.MapID,owner.StamMax,ClassStats.Defense(owner),owner.FireResistance,owner.ColdResistance,owner.ComputeMovementSpeed(Direction.East,false),owner.ComputeMovementSpeed(Direction.East|Direction.Running,false))));
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);ClassGear.Refresh(owner);Native(owner);
        Require(owner.Stam==saved.Stamina && owner.StamMax==saved.Stamina && ClassStats.Defense(owner)==saved.Defense && owner.FireResistance==saved.Fire && owner.ColdResistance==saved.Cold
            && owner.ComputeMovementSpeed(Direction.East,false)==saved.Walk && owner.ComputeMovementSpeed(Direction.East|Direction.Running,false)==saved.Run,"Passive native stat payload changed over reload");
        Require(Skills.All(x=>SkillMechanics.Rank(owner,x)==21 && ClassCreation.Profiles.GetRank(owner.Serial,SkillMechanics.For(x).TalentId)==20),"Passive allocated/effective ranks lost");
        var helm=World.FindItem((Serial)saved.Helm);owner.AddToBackpack(helm);ClassGear.Refresh(owner);
        Require(BarbarianCombat.Passive(owner,CombatAbility.NaturalResistance)==67 && owner.ColdResistance==67 && owner.Stam<=owner.StamMax,"Unequipped soft-rank gear retained passive or excess stamina");
        Require(owner.EquipItem(helm),"Passive saved helm re-equip");ClassGear.Refresh(owner);Native(owner);Damage(owner);
        Seed(owner,helm,World.FindItem((Serial)saved.Boots),World.FindItem((Serial)saved.Amulet));
    }
}
