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

public static class ReviveEquipmentPoolChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveEquipmentPoolChecks));
    private sealed record Saved(uint Owner,ReviveNativeState[] Actors,int[] Life,int[] Mana,int[] Stamina,uint[] Corpses);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-equipment-pools-fixtures.json");
    private static bool Creating;
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Rates();Register("revive-equipment-pools-check",Check);Register("revive-equipment-pools-reload-check",Reload);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Rates()
    {
        var mana=Mobile.ManaRegenRateHandler;var stam=Mobile.StamRegenRateHandler;
        bool Fixture(Mobile m)=>Creating||m.Name?.StartsWith("PoisonDaggerFixtureRevivePools",StringComparison.Ordinal)==true;
        Mobile.ManaRegenRateHandler=m=>Fixture(m)?FixtureSpace.Retention:mana?.Invoke(m)??Mobile.DefaultManaRate;
        Mobile.StamRegenRateHandler=m=>Fixture(m)?FixtureSpace.Retention:stam?.Invoke(m)??Mobile.DefaultStamRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Actual frozen NPC mana/stamina and source-zero pools",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Item Named(string name)=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==name).Id,99,new Random(173),true);
    private static (BaseCreature Actor,Corpse Corpse) Create(PlayerMobile p,bool zero,bool full)
    {
        Skeleton b;Creating=true;try{b=new Skeleton{Name="PoisonDaggerFixtureRevivePools",NoKillAwards=true};}finally{Creating=false;}
        foreach(var item in b.Items.Where(i=>i is not Backpack&&i.Layer!=Layer.Bank).ToArray()){item.Delete();}
        b.RawInt=150;b.RawDex=200;b.ManaMaxSeed=zero?0:70;b.StamMaxSeed=55;
        Item[] items=zero?[Named("Frostburn"),Named("Dwarf Star"),Named("Sandstorm Trek")]:[Named("Harlequin Crest"),Named("Frostburn"),Named("Dwarf Star"),Named("Sandstorm Trek")];
        foreach(var item in items){b.AddItem(item);}b.Mana=zero?0:b.ManaMax;b.Stam=b.StamMax;
        b.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);b.AIObject?.AITimer.Stop();Corpse corpse=null;var died=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(who,container)=>{died?.Invoke(who,container);if(who==b){corpse=container as Corpse;corpse?.DropItem(new Gold(1));}};
        try{p.DoHarmful(b,true);b.Damage(b.Hits+1,p);CombatDamage.Finish(b,p);}finally{ClassLootHooks.CreatureDied=died;}
        Require(corpse is{Deleted:false}&&ReviveOriginals.Find(corpse)==b,"Equipped pool original was not retained");corpse.BeginDecay(FixtureSpace.Retention);
        var original=ReviveOriginals.State(corpse);var before=ReviveBeforeDeath.Decode(original.BeforeDeathJson);
        Require(before.ResourceBases is{Issue:null}&&before.ResourceBases.Mana256==(zero?0:70*256)&&before.ResourceBases.Stamina256==55*256,"Native seed bases guessed from raw attributes/item boosts");
        var setup=ReviveLife.Setup(p,corpse,before.Mana,before.Stamina)with{RegenerationRate=ReviveRegeneration.Rate("skeleton1")};
        setup=full?ReviveEquipmentResources.Prepare(setup,original):ReviveEquipmentLife.Prepare(setup,original);
        Require(ReviveNativeActivation.CommitPrepared(p,corpse,corpse.Location,before.AiStatus.Value,setup,out var actual,out var issue)&&actual==b,"Pool activation failed: "+issue);
        foreach(var item in items){Require(item.Parent==corpse,"Pool original gear was not independent loot");p.AddToBackpack(item);item.Delete();}
        Expect(b,full,zero);return(b,corpse);
    }
    private static void Expect(BaseCreature b,bool full,bool zero)
    {
        var row=ReviveNativeActivation.State(b);int level=(row.FacetPlan?.Combat??row.Setup.CombatPlan).Level;
        if(!full)
        {Require(NpcEquipmentResources.Maximum256(b,1)==null&&NpcEquipmentResources.Maximum256(b,2)==null,"Old life profile acquired new resource behavior");return;}
        int flatMana=0,coeffMana=0,percent=0,flatStam=0,coeffStam=0;
        foreach(var s in ReviveBeforeDeath.Equipment(ReviveBeforeDeath.Decode(row.Original.BeforeDeathJson)))
        {
            var c=FrozenEquipmentEffects.Bind(s);var mana=c.ResourceInput("mana",217,level);var stam=c.ResourceInput("stam",242,level);
            flatMana+=mana.Flat256;coeffMana+=mana.LevelCoefficientRaw;percent+=c.Bonus("mana%",level);flatStam+=stam.Flat256;coeffStam+=stam.LevelCoefficientRaw;
        }
        Require(percent>0&&flatStam>0&&coeffStam>0&&(!zero?coeffMana>0:true),"Mana percentage/stamina flat/per-level controls were vacuous");
        int expectedMana=NpcResourceValues.Calculate(new(1,1,level,zero?0:70*256,flatMana,coeffMana,percent,BattleOrders.Percent(b)));
        int expectedStamina=NpcResourceValues.Calculate(new(1,2,level,55*256,flatStam,coeffStam,BattleOrders.Percent(b)+PaladinVigor.StaminaPercent(b),0));
        Require(NecromancerEffects.ManaMaximum256(b)==expectedMana&&BattleOrders.StaminaMaximum256(b)==expectedStamina,"Actual exact equipped mana/stamina maximum diverged");
        Require(b.ManaMax==(expectedMana+255)/256&&b.StamMax==(expectedStamina+255)/256,"Native resource display did not use captured bases");
        if(zero){Require(expectedMana==0,"NPC zero base gained mana from percent/raw Int");}
    }
    private static void Check()
    {
        var old=SummonDifficulties.Facets();var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureRevivePoolsOwner";
        MagePetBuffChecks.Train(p,"necromancer.revive",20);MagePetBuffChecks.Train(p,"necromancer.skeletonmastery",20);FixtureSpace.Corridor(p,6980,28,3500);
        SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
        try
        {
            var scenes=new[]{Create(p,false,true),Create(p,true,true),Create(p,false,false)};var actors=scenes.Select(s=>s.Actor).ToArray();
            var supplier=new PlayerMobile{Name="PoolOrders",Body=0x190,Player=true};supplier.AddItem(new Backpack());ClassCreation.Profiles.Assign(supplier,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(supplier,99);
            foreach(var b in actors){BattleOrders.Apply(supplier,b,3,TimedEffects.Now.Add(FixtureSpace.Retention));Require(BattleOrders.Percent(b)>0,"Pool BO control missing");}
            p.MoveToWorld(new(p.X+1,p.Y,p.Z),Map.Felucca);
            for(int i=0;i<actors.Length;i++)
            {Expect(actors[i],i<2,i==1);SkillHealth.SetBalance256(actors[i],SkillHealth.Maximum256(actors[i])*2/3+17);NecromancerEffects.SetMana256(actors[i],Math.Max(0,NecromancerEffects.ManaMaximum256(actors[i])-257));BattleOrders.SetStamina256(actors[i],Math.Max(0,BattleOrders.StaminaMaximum256(actors[i])-513));}
            var state=ReviveNativeActivation.State(actors[0]);var legacy=ReviveBeforeDeath.Decode(state.Original.BeforeDeathJson)with{ResourceBases=null};bool refused=false;
            try{_=ReviveEquipmentResources.Prepare(state.Setup,state.Original with{BeforeDeathJson=JsonSerializer.Serialize(legacy)});}catch(NotSupportedException){refused=true;}
            Require(refused,"Historical missing resource bases were invented");
            p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,actors.Select(ReviveNativeActivation.State).ToArray(),actors.Select(SkillHealth.Balance256).ToArray(),actors.Select(NecromancerEffects.ManaBalance256).ToArray(),actors.Select(BattleOrders.Stamina256).ToArray(),scenes.Select(s=>s.Corpse.Serial.Value).ToArray())));
        }
        finally{SummonDifficulties.SetFacets(old);}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(s.Actors.Length==3,"Missing equipped pool originals");int count=World.Items.Count;
        for(int i=0;i<s.Actors.Length;i++)
        {
            var b=World.FindMobile((Serial)s.Actors[i].Actor)as BaseCreature;
            Require(b is{Alive:true,Deleted:false}&&b.Map==Map.Internal&&b.ControlMaster?.Serial.Value==s.Owner&&ReviveNativeActivation.State(b)==s.Actors[i]
                &&SkillHealth.Balance256(b)==s.Life[i]&&NecromancerEffects.ManaBalance256(b)==s.Mana[i]&&BattleOrders.Stamina256(b)==s.Stamina[i],"Saved original resource balances/profile changed");
            Expect(b,i<2,i==1);Require(World.FindItem((Serial)s.Corpses[i])is Corpse c&&c.GetAmount(typeof(Gold))==1,"Pool independent loot changed");
        }
        Require(World.Items.Count==count,"Pool getters recreated loot");
    }
}
