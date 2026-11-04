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

public static class ReviveDefenseChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveDefenseChecks));
    private static readonly string[] Codes=["res-physical","res-fire","res-cold","res-pois","res-ltng","res-all","res-mag","magic-reduction","physical-reduction",
        "abs-fire","abs-fire%","abs-cold","abs-cold%","abs-ltng","abs-ltng%","res-pois-len","nofreeze","half-freeze","ac-miss","ac-hth"];
    private sealed record Fixture(uint Actor,uint Corpse,bool Enabled,int[][] Bonuses);
    private sealed record Saved(uint Owner,Fixture[] Actors,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-defense-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("revive-defense-check",Check);Register("revive-defense-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Frozen original Revive recipient defenses and native pools",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Item Named(string name)=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==name).Id,99,new Random(173),true);
    private static Fixture Create(PlayerMobile owner,int kind,bool enabled)
    {
        var b=new Skeleton{Name="PoisonDaggerFixtureReviveDefense",NoKillAwards=true};
        foreach(var item in b.Items.Where(i=>i is not Backpack&&i.Layer!=Layer.Bank).ToArray()){item.Delete();}
        string[][] names=[["Duriel's Shell","Raven Frost","Snakecord","Nokozan Relic","Coif of Glory","Goblin Toe","Baranar's Star"],
            ["Venomsward","Undead Crown","Dwarf Star","Goblin Toe"],["Skin of the Vipermagi","Raven Frost","Snakecord","Waterwalk","Blackhorn's Face"]];
        var gear=names[kind].Select(Named).ToArray();foreach(var item in gear){b.AddItem(item);}
        Require(gear.All(i=>i.Parent==b),"Fixture gear slots collided");
        var bonuses=Enumerable.Range(1,99).Select(level=>Codes.Select(code=>gear.Sum(i=>ClassGear.Bonus(i,code,level))).ToArray()).ToArray();
        b.SetResistance(ResistanceType.Physical,10);b.SetResistance(ResistanceType.Fire,70);b.SetResistance(ResistanceType.Cold,20);
        b.SetResistance(ResistanceType.Poison,25);b.SetResistance(ResistanceType.Energy,45);
        b.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);b.AIObject?.AITimer.Stop();Corpse corpse=null;var died=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(who,item)=>{died?.Invoke(who,item);if(who==b){corpse=item as Corpse;corpse?.DropItem(new Gold(1));}};
        try{owner.DoHarmful(b,true);b.Damage(b.Hits+1,owner);CombatDamage.Finish(b,owner);}finally{ClassLootHooks.CreatureDied=died;}
        Require(corpse is{Deleted:false}&&ReviveOriginals.Find(corpse)==b,"Defense original was not retained");corpse.BeginDecay(FixtureSpace.Retention);
        Require(!ReviveCasts.Plan(owner,corpse,out _,out _,out _),"Public equipped cast gate was lifted");
        var original=ReviveOriginals.State(corpse);var before=ReviveBeforeDeath.Decode(original.BeforeDeathJson);
        var setup=ReviveLife.Setup(owner,corpse,before.Mana,before.Stamina)with{RegenerationRate=ReviveRegeneration.Rate("skeleton1")};
        setup=enabled?ReviveEquipmentDefenses.Prepare(setup,original):ReviveEquipmentCombat.Prepare(setup,original);
        Require(ReviveNativeActivation.CommitPrepared(owner,corpse,corpse.Location,before.AiStatus.Value,setup,out var actual,out var issue)&&actual==b,"Defense activation failed: "+issue);
        foreach(var item in gear){Require(item.Parent==corpse,"Defense supplier did not remain independent loot");owner.AddToBackpack(item);item.Delete();}
        return new(b.Serial.Value,corpse.Serial.Value,enabled,bonuses);
    }
    private static void Expect(PlayerMobile owner,Fixture fixture)
    {
        var actor=World.FindMobile((Serial)fixture.Actor)as BaseCreature;var state=ReviveNativeActivation.State(actor);
        Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster==owner,"Original defense body/owner lost");
        int level=(state.FacetPlan?.Combat??state.Setup.CombatPlan).Level;var bonuses=fixture.Bonuses[level-1];
        int Bonus(string code)=>fixture.Enabled?bonuses[Array.IndexOf(Codes,code)]:0;
        Require(ReviveEquipmentDefenses.Handles(actor)==fixture.Enabled&&!ItemDurationDefenses.HasEquipment(actor),"Recipient opt-in changed regeneration ownership");
        var types=new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy};
        var seeds=new[]{actor.BasePhysicalResistance,actor.BaseFireResistance,actor.BaseColdResistance,actor.BasePoisonResistance,actor.BaseEnergyResistance};
        int count=actor.ResistanceMods?.Count??0;
        for(int n=0;n<3;n++){ReviveEquipmentDefenses.Sync(actor);}
        Require((actor.ResistanceMods?.Count??0)==count,"Repeated recipient synchronization duplicated mods");
        for(int i=0;i<5;i++)
        {Require(actor.GetResistance(types[i])==seeds[i]+Bonus(Codes[i])+(i==0?0:Bonus("res-all")),"Native NPC resistance pool lost source seed or doubled frozen item/appearance");}
        Require(GemCombat.Reduction(actor)==Math.Max(0,Bonus("magic-reduction"))&&GemCombat.PhysicalReduction(actor)==Math.Max(0,Bonus("physical-reduction")),"Frozen MDR/PDR supplier differs from original");
        Require(ItemDurationDefenses.PoisonLength(actor)==Bonus("res-pois-len")&&ItemDurationDefenses.CannotFreeze(actor)==(Bonus("nofreeze")>0)
            &&ItemDurationDefenses.HalfFreeze(actor)==(Bonus("half-freeze")>0),"Frozen duration supplier differs from original");
        Require(NamedCombat.DefenseFor(null,actor,100,true)==100+Bonus("ac-miss")&&NamedCombat.DefenseFor(null,actor,100,false)==100+Bonus("ac-hth"),"Conditional recipient armor lost its attack lane");
        foreach(string element in new[]{"fire","cold","ltng","mag","physical"})
        {
            int resist=element switch{"fire"=>actor.FireResistance,"cold"=>actor.ColdResistance,"ltng"=>actor.EnergyResistance,"mag"=>NamedElements.MagicResistance(actor),_=>actor.PhysicalResistance};
            int dr=element=="physical"?Bonus("physical-reduction"):Bonus("magic-reduction");
            bool absorb=element is "fire" or "cold" or "ltng";
            var expected=NamedElements.Reduce256(500*256,resist,Math.Max(0,dr)*256,absorb?Bonus("abs-"+element+"%"):0,absorb?Bonus("abs-"+element)*256:0);
            Require(NamedElements.Resolve256(null,actor,500*256,element,enhance:false)==expected,"Frozen resistance/reduction/absorb ordering differs");
        }
        int frames=ColdStates.LengthFrames(100,actor.ColdResistance,Bonus("nofreeze")>0,Bonus("half-freeze")>0);
        long tick=TimedEffects.TickAt(TimedEffects.Now);bool chilled=ColdStates.Apply(null,actor,100,tick);
        Require(chilled==(frames>0),"Cold state bypassed frozen duration protection");
        if(chilled)
        {Require(TimedEffects.TryActor(actor,out var cold)&&cold.ChillUntil==TimedEffects.TimeAt(tick).AddMilliseconds(40L*ColdStates.StateFrames(frames,true,ColdStates.Effect(actor),NecromancerEffects.DifficultyDivisor(actor))),"Frozen half-freeze duration or difficulty divisor changed");}
        TimedEffects.ClearGear(actor);
        var source=new NpcPoisonRecipient();source.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);source.AIObject?.AITimer.Stop();
        try
        {
            var poison=NpcEquipmentPoison.Mitigate(source,actor,new(source.Serial.Value,owner.Serial.Value,new(),0,0),new(256,100));
            Require(poison==PoisonPackets.Mitigate(256,100,false,actor.PoisonResistance,Bonus("res-pois-len"),0,0,0),"Frozen poison length was omitted or applied twice");
        }
        finally{source.Delete();}
        // Unrelated live modifiers remain outside the frozen item pool.
        actor.AddResistanceMod(new(ResistanceType.Fire,"ReviveDefenseFixtureOther",-17));
        Require(actor.FireResistance==seeds[1]+Bonus("res-fire")+Bonus("res-all")-17,"Native unrelated resistance modifier lost");actor.RemoveResistanceMod("ReviveDefenseFixtureOther");
    }
    private static void Exercise(PlayerMobile owner,Fixture[] fixtures)
    {
        var prior=SummonDifficulties.Facets();SummonDifficulties.SetFacets([new(Map.Trammel.MapIndex,MonsterDifficulty.Normal),new(Map.Felucca.MapIndex,MonsterDifficulty.Hell)]);
        try
        {
            foreach(var map in new[]{Map.Trammel,Map.Felucca,Map.Trammel})
            {owner.MoveToWorld(new(6450,3450,0),map);ReviveFacets.Sync(owner);foreach(var fixture in fixtures){Expect(owner,fixture);}}
            var source=World.FindMobile((Serial)fixtures[0].Actor)as BaseCreature;var recipient=World.FindMobile((Serial)fixtures[1].Actor)as BaseCreature;
            int life=SkillHealth.Balance256(recipient);var stream=NpcEquipmentRandom.State(source);
            var impact=ReviveEquipmentImpact.Prepare(source,recipient,1,_=>0,()=>99);
            Require(impact.Packet.Total256>0&&SkillHealth.Balance256(recipient)==life&&NpcEquipmentRandom.State(source)==stream,"Frozen recipient allocation changed HP or the supplied stream");
            var custom=new SkeletonGuardian();bool customRefused=false;
            try{ReviveEquipmentDefenses.RequireBody(custom);}catch(NotSupportedException){customRefused=true;}finally{custom.Delete();}
            Require(customRefused,"Custom resistance body silently acquired native mods");
            var rows=ReviveNativeActivation.Snapshot();var first=rows.Single(r=>r.Actor==fixtures[0].Actor);bool refused=false;
            try{ReviveNativeActivation.Restore(rows.Select(r=>r==first?r with{Setup=r.Setup with{EquipmentDefenseProfile="unknown"}}:r).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&ReviveNativeActivation.Snapshot().SequenceEqual(rows),"Invalid defensive profile restore was not atomic");
            var before=ReviveBeforeDeath.Decode(first.Original.BeforeDeathJson);
            var items=ReviveBeforeDeath.Equipment(before).Select((item,i)=>i<3?item with{FixedJson=JsonSerializer.Serialize(new[]{new FrozenBonus("res-fire",1000000000)})}:item).ToArray();
            refused=false;
            try{ReviveNativeActivation.Restore(rows.Select(r=>r==first?r with{Original=r.Original with{BeforeDeathJson=JsonSerializer.Serialize(before with{EquipmentJson=JsonSerializer.Serialize(items)})}}:r).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&ReviveNativeActivation.Snapshot().SequenceEqual(rows),"Overflowing combined recipient defenses replaced the roster");
            ReviveNativeActivation.Restore(rows.Select(r=>r==first?r with{Setup=r.Setup with{EquipmentDefenseProfile=null}}:r).ToArray());
            Expect(owner,fixtures[0]with{Enabled=false});ReviveNativeActivation.Restore(rows);
            foreach(var fixture in fixtures){Expect(owner,fixture);}
            Require(source.FireResistance>100,"NPC resistance acquired a player cap or lost the positive uncapped control");
        }
        finally{SummonDifficulties.SetFacets(prior);}
    }
    private static void Check()
    {
        var owner=PoisonDaggerChecks.Owner();owner.Name="PoisonDaggerFixtureReviveDefenseOwner";MagePetBuffChecks.Train(owner,"necromancer.revive",20);MagePetBuffChecks.Train(owner,"necromancer.skeletonmastery",20);FixtureSpace.Corridor(owner,6450,24,3450);
        var fixtures=new[]{Create(owner,0,true),Create(owner,1,true),Create(owner,2,true),Create(owner,0,false)};
        Require(fixtures[0].Bonuses.Any(row=>row[1]>=70&&row[12]>0&&row[16]>0)&&fixtures[1].Bonuses.Any(row=>row[10]>0&&row[15]>0&&row[17]>0)
            &&fixtures[2].Bonuses.Any(row=>row[5]>0&&row[7]>0&&row[13]>0&&row[18]>0),"Positive defense controls missing");
        Exercise(owner,fixtures);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,fixtures)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;
        Exercise(owner,saved.Actors);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
}
