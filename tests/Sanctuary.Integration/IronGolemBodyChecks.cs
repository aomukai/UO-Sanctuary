// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class IronGolemBodyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(IronGolemBodyChecks));
    private sealed record Saved(uint Owner,uint Pet,uint Item,string Gear,int Life,int Maximum,int Regeneration,int Attack,int Defense,string State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"iron-golem-body-fixtures.json");
    public static void Configure(){Register("iron-golem-body-check",Check);Register("iron-golem-body-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Iron Golem saved stat body;AI/cast disabled",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile owner,string talent,int rank=1)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Necromancer,talent,out var row),"Missing Necromancer talent");
        foreach(string prior in row.Prerequisites){Train(owner,prior);}
        while(ClassCreation.Profiles.GetRank(owner.Serial,talent)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,talent,out _),"Train "+talent);}
    }
    private static Item Named(string key)=>NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(1107),true);
    private static void SourceRecipes()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"iron-golem-body-source-cases.json")));int count=0;IronGolemBodyRecipe last=null;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int N(int i)=>row[0][i].GetInt32();
            if(N(3)==0){continue;} // Sanctuary uses the expansion monlvl columns.
            var effective=SkillMechanics.All.Where(x=>x.CharacterClass==DiabloClass.Necromancer).ToDictionary(x=>x.SourceKey,_=>0);
            var hard=new Dictionary<string,int>(effective);effective["IronGolem"]=N(1);effective["Golem Mastery"]=N(4);
            hard["Clay Golem"]=hard["BloodGolem"]=hard["FireGolem"]=20;
            var baseStats=new List<(int,int)>();var properties=new List<(int,int)>();
            if(N(5) is 1 or 4){properties.Add((7,60*256));properties.Add((16,100));baseStats.Add((31,150));}
            if(N(5) is 2 or 4){properties.Add((76,40));}
            if(N(5) is 3 or 4){properties.Add((216,2560));}
            if(N(5)==4){properties.AddRange([(19,80),(119,30)]);}
            if(N(5)==5){properties.AddRange([(142,10),(144,10),(148,10)]);}
            if(N(5)==6){properties.AddRange([(76,100),(216,768)]);}
            var recipe=new IronGolemBodyRecipe(1,0x40000001,"crn",N(0),N(1),effective,hard,52,
                IronGolemBodyRecipes.Fields(baseStats.ToArray()),IronGolemBodyRecipes.Fields(properties.ToArray()));
            last=recipe;
            var actual=IronGolemBodyRecipes.Build(recipe,(MonsterDifficulty)N(2));
            int S(int array,int stat)=>row[1][1][array].EnumerateArray().Where(x=>x[0].GetUInt32()==EquipmentStatKey.Pack(stat)).Select(x=>x[1].GetInt32()).SingleOrDefault();
            Require(actual.Level==S(0,12)&&actual.LifeBase256==S(0,7)&&actual.CurrentLifeWrite256==S(1,6)&&actual.BirthMaximum256==S(1,7)
                &&actual.ArmorBase==S(0,31)&&actual.AttackBase==S(0,19)&&actual.Regeneration256==S(1,74)
                &&actual.Fire==S(1,39)&&actual.Cold==S(1,43)&&actual.Lightning==50+S(1,41)&&actual.Poison==100+S(1,45),"Source body recipe differs at "+count);
            count++;
        }
        Require(count==1008,"Missing applicable body recipe source contexts");
        var oversized=new Dictionary<string,int>(last.Effective);oversized["Golem Mastery"]=255;bool rejected=false;
        try{IronGolemBodyRecipes.Build(last with{Effective=oversized},MonsterDifficulty.Hell);}
        catch(Exception e)when(e is NotSupportedException or OverflowException){rejected=true;}
        Require(rejected,"Unverified overflow/native-ceiling context silently clamped");
        Log.Information("Iron Golem body recipes:1008 applicable expansion source compositions match supplied noRatio spawn/item contexts");
    }
    private static Saved Capture(IronGolemBody pet)
    {
        var binding=IronGolemItems.State(pet);var item=World.FindItem((Serial)binding.Item);
        return new(binding.Owner,pet.Serial.Value,binding.Item,JsonSerializer.Serialize(ClassGear.State(item)),SkillHealth.Balance256(pet),
            SkillHealth.Maximum256(pet),BattleOrders.Regeneration256(pet),ClassStats.AttackRating(pet),ClassStats.Defense(pet),JsonSerializer.Serialize(pet.Prepared));
    }
    private static void Verify(IronGolemBody body)
    {
        var state=body.Prepared;var n=state.Numbers;var item=IronGolemItems.Equipment(body,n.Level);
        Require(n==IronGolemBodyRecipes.Build(state.Recipe,state.Difficulty),"Saved recipe no longer reproduces source numbers");
        Require(SkillHealth.Maximum256(body)==n.BirthMaximum256&&body.HitsMax==(n.BirthMaximum256+255)/256,"Live exact/display maximum double counted or dropped original item");
        int flat=checked(n.AttackBase+item.Bonus("att")+5*item.Bonus("dex"));
        Require(ClassStats.AttackRating(body)==flat+(int)((long)flat*item.Bonus("att%")/100),"Live AR repeated the item's flat/Dexterity/percent contribution");
        Require(ClassStats.Defense(body)==n.ArmorBase+item.Defense+item.Bonus("dex")/4,"Live defense repeated original armor or Dexterity");
        Require(body.FireResistance==n.Fire+NpcEquipmentResistances.Bonus(item,ResistanceType.Fire)
            &&body.ColdResistance==n.Cold+NpcEquipmentResistances.Bonus(item,ResistanceType.Cold)
            &&body.EnergyResistance==n.Lightning+NpcEquipmentResistances.Bonus(item,ResistanceType.Energy)
            &&body.PoisonResistance==n.Poison+NpcEquipmentResistances.Bonus(item,ResistanceType.Poison),"Raw source resistance pool lost or repeated an item contribution");
        Require(BattleOrders.Regeneration256(body)==n.Regeneration256,"Live birth regeneration differs from numeric source graph");
        Require(body.SkillWeaponDamage==(0,0)&&body.CantWalk&&body.Name.Contains("placeholder",StringComparison.Ordinal)
            &&!NecromancerCombat.Implemented(CombatAbility.IronGolem),"Unfinished AI/attack/cast activated");
        Require(CreatureProfiles.Definition(body).Reference=="irongolem","Body inherited skeleton identity");
    }
    private static void Check()
    {
        SourceRecipes();
        var owner=new PlayerMobile{Name="IronBodyOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,99);
        Train(owner,"necromancer.irongolem");Train(owner,"necromancer.golemmastery");Train(owner,"necromancer.summonresist");
        FixtureSpace.Place(owner,new(6100,1860,0),Map.Trammel);var saved=new List<Saved>();var bodies=new List<IronGolemBody>();
        foreach(string key in new[]{"Crown of Thieves","Shaftstop"})
        {
            var original=Named(key);original.MoveToWorld(new(owner.X+bodies.Count+1,owner.Y,owner.Z),owner.Map);uint serial=original.Serial.Value;
            string gear=JsonSerializer.Serialize(ClassGear.State(original));var body=IronGolemBody.CreatePrepared(owner,original,original.Location);bodies.Add(body);
            Require(original.Serial.Value==serial&&original.Parent==body&&gear==JsonSerializer.Serialize(ClassGear.State(original)),"Prepared body changed original identity/rolls");
            Verify(body);Require(SkillHealth.Balance256(body)==body.Prepared.Numbers.CurrentLifeWrite256,"Birth invented a final accrued refill");
            var before=Capture(body);SkillHealth.SetBalance256(body,before.Maximum/3+17);int injured=SkillHealth.Balance256(body);
            SkillHealth.Damage(null,body,37);Require(SkillHealth.Balance256(body)==injured-37,"Prepared body does not take exact fractional damage");
            SkillHealth.Heal(body,11);Require(SkillHealth.Balance256(body)==injured-26,"Prepared body does not heal exact fractional life");
            int level=body.Prepared.Numbers.Level;Require(level==75&&ClassStats.Level(body)==75,"Default pet level incorrectly uses full owner level");
        }
        // The same original's live activation changes, but the birth imprint
        // remains fixed; no second mastery application occurs on repair.
        var selected=bodies[0];var held=World.FindItem((Serial)IronGolemItems.State(selected).Item);var raw=ClassGear.State(held);
        int full=SkillHealth.Maximum256(selected),life=SkillHealth.Balance256(selected);
        ClassGear.Profiles.Store(held,raw with{Identified=false});
        Require(SkillHealth.Maximum256(selected)==selected.Prepared.Numbers.LifeBase256&&SkillHealth.Balance256(selected)==BattleOrders.Rescale256(life,full,selected.Prepared.Numbers.LifeBase256),"Inactive original altered frozen birth imprint or lost current ratio");
        ClassGear.Profiles.Store(held,raw);Verify(selected);
        // Approved facet rescaling preserves the same damaged life fraction.
        var facets=SummonDifficulties.Facets();
        try
        {
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
            int oldMaximum=SkillHealth.Maximum256(selected),oldLife=SkillHealth.Balance256(selected);
            owner.MoveToWorld(new(6100,1860,0),Map.Felucca);SummonDifficulties.Sync(owner);
            Require(selected.Prepared.Difficulty==MonsterDifficulty.Hell&&SkillHealth.Balance256(selected)==SummonDifficulties.Life256(oldLife,oldMaximum,SkillHealth.Maximum256(selected)),"Hell rescale lost relative life");
            Verify(selected);owner.MoveToWorld(new(6100,1860,0),Map.Trammel);SummonDifficulties.Sync(owner);Verify(selected);
        }
        finally{SummonDifficulties.SetFacets(facets);}
        foreach(var body in bodies){saved.Add(Capture(body));body.Internalize();}
        var helm=Named("Tarnhelm");Require(owner.EquipItem(helm),"Tarnhelm equip");ClassGear.Refresh(owner);
        Require(SkillMechanics.Rank(owner,CombatAbility.IronGolem)==2&&bodies.All(x=>x.Prepared.Recipe.Rank==1),"Existing birth snapshot changed with owner gear");
        var next=Named("Crown of Thieves");next.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);
        var fresh=IronGolemBody.CreatePrepared(owner,next,next.Location);Require(fresh.Prepared.Recipe.Rank==2&&fresh.Prepared.Numbers.Level==76,"Fresh body missed effective ranks/default level");
        fresh.Delete();Require(next.Deleted&&IronGolemItems.State(fresh)==null,"Deleting prepared body left consumed original");
        owner.Backpack.DropItem(helm);ClassGear.Refresh(owner);
        var failed=Named("Shaftstop");failed.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);var failedAt=failed.Location;
        string failedGear=JsonSerializer.Serialize(ClassGear.State(failed));bool rollback=false;
        try{IronGolemBody.CreatePrepared(owner,failed,failedAt,_=>throw new NotSupportedException("Injected controller refusal"));}
        catch(NotSupportedException e){rollback=e.Message=="Injected controller refusal";}
        Require(rollback&&!failed.Deleted&&failed.Parent==null&&failed.Location==failedAt&&failed.Visible&&failed.Movable
            &&failedGear==JsonSerializer.Serialize(ClassGear.State(failed)),"Post-transfer rollback lost/rerolled original");failed.Delete();
        var sword=new Broadsword();ClassGear.Profiles.Ensure(sword);sword.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);
        var dying=IronGolemBody.CreatePrepared(owner,sword,sword.Location);Require(dying.Prepared.Numbers.LifeBase256%256!=0,"Fractional bare-weapon birth control lost");
        dying.Kill();Require(!dying.Alive&&sword.Deleted&&IronGolemItems.State(dying)==null,"Combat death left bound original");
        // Rejection happens before a ground item is consumed.
        var unsupported=Named("Spiritforge");unsupported.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        var point=unsupported.Location;bool refused=false;try{IronGolemBody.CreatePrepared(owner,unsupported,point);}catch(NotSupportedException e){refused=e.Message.Contains("sock",StringComparison.Ordinal);}
        Require(refused&&!unsupported.Deleted&&unsupported.Parent==null&&unsupported.Location==point&&unsupported.Visible&&unsupported.Movable,"Unsupported socket recipe consumed/changed original");unsupported.Delete();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));owner.Internalize();
    }
    private static void Reload()
    {
        SourceRecipes();
        foreach(var saved in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var body=World.FindMobile((Serial)saved.Pet) as IronGolemBody;Require(body is{Alive:true,Deleted:false},"Saved Iron Golem body missing");
            Verify(body);Require(Capture(body)==saved,"Prepared body/resource/original state changed on reload");body.AIObject?.AITimer.Stop();
        }
    }
}
