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

public static class ReviveEquipmentLifeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveEquipmentLifeChecks));
    private sealed record Saved(uint Owner,ReviveNativeState[] Actors,int[] Life,uint[] Corpses,uint[] DeletedItems);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-equipment-life-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("revive-equipment-life-check",Check);Register("revive-equipment-life-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Actual original Revive frozen equipment life",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureReviveEquipmentLife";p.RawInt=200;
        MagePetBuffChecks.Train(p,"necromancer.revive",20);MagePetBuffChecks.Train(p,"necromancer.skeletonmastery",20);FixtureSpace.Corridor(p,7000,28,3400);return p;
    }
    private static Item Named(string name)=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==name).Id,99,new Random(173),true);
    private static (BaseCreature Actor,Corpse Corpse,uint[] Items) Create(PlayerMobile p,bool perLevel,bool enabled)
    {
        var b=new Skeleton{NoKillAwards=true};foreach(var item in b.Items.Where(i=>i is not Backpack&&i.Layer!=Layer.Bank).ToArray()){item.Delete();}
        Item[] gear=[Named(perLevel?"Harlequin Crest":"Crown of Thieves"),Named("Blood Crescent")];foreach(var item in gear){b.AddItem(item);}
        b.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);b.AIObject?.AITimer.Stop();Corpse corpse=null;var died=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(who,container)=>{died?.Invoke(who,container);if(who==b){corpse=container as Corpse;corpse?.DropItem(new Gold(1));}};
        try{p.DoHarmful(b,true);b.Damage(b.Hits+1,p);CombatDamage.Finish(b,p);}finally{ClassLootHooks.CreatureDied=died;}
        Require(corpse is{Deleted:false}&&ReviveOriginals.Find(corpse)==b,"Equipped original was not retained");corpse.BeginDecay(FixtureSpace.Retention);
        int mana=NecromancerEffects.ManaBalance256(p);
        Require(ReviveCasts.Plan(p,corpse,out var publicPlan,out _,out var refusal)&&publicPlan.EquipmentSwingProfile==ReviveSwings.Profile
            &&!b.Alive&&ReviveNativeActivation.State(b)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Qualified public equipped preflight failed or activated/charged early: "+refusal);
        var original=ReviveOriginals.State(corpse);var before=ReviveBeforeDeath.Decode(original.BeforeDeathJson);
        var setup=ReviveLife.Setup(p,corpse,before.Mana,before.Stamina)with{RegenerationRate=ReviveRegeneration.Rate("skeleton1")};
        if(enabled){setup=ReviveEquipmentLife.Prepare(setup,original);}
        var status=before.AiStatus.Value;
        Require(ReviveNativeActivation.CommitPrepared(p,corpse,corpse.Location,status,setup,out var actual,out var issue)&&actual==b,"Prepared equipped original activation: "+issue);
        var ids=gear.Select(i=>i.Serial.Value).ToArray();foreach(var item in gear){Require(item.Parent==corpse,"Original gear left independent loot");p.AddToBackpack(item);item.Delete();}
        Expect(b,enabled);b.AIObject?.AITimer.Stop();return(b,corpse,ids);
    }
    private static int Expected(BaseCreature b,bool equipped)
    {
        var s=ReviveNativeActivation.State(b);var life=s.FacetPlan?.Life??s.Setup.LifePlan;int bo=BattleOrders.LifePercent(b);
        if(!equipped){return checked((int)(life.Maximum256+ClassResources.Percentage(life.Maximum256,bo)));}
        int level=(s.FacetPlan?.Combat??s.Setup.CombatPlan).Level,flat=0,coefficient=0,percent=0;
        foreach(var snapshot in ReviveBeforeDeath.Equipment(ReviveBeforeDeath.Decode(s.Original.BeforeDeathJson)))
        {
            var c=FrozenEquipmentEffects.Bind(snapshot);var input=c.ResourceInput("hp",216,level);
            flat+=input.Flat256;coefficient+=input.LevelCoefficientRaw;percent+=c.Bonus("hp%",level);
        }
        Require(flat>0||coefficient>0,"Item life supplier was vacuous");
        return NpcResourceValues.Calculate(new(1,0,level,life.Maximum256,flat,coefficient,percent,bo));
    }
    private static void Expect(BaseCreature b,bool equipped)
    {Require(SkillHealth.Maximum256(b)==Expected(b,equipped),"Actual equipped/legacy Revive maximum differs from source resource consumer");}
    private static void Check()
    {
        var old=SummonDifficulties.Facets();var p=Owner();SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
        try
        {
            var scenes=new[]{Create(p,false,true),Create(p,true,true),Create(p,true,false)};var actors=scenes.Select(s=>s.Actor).ToArray();
            var supplier=new PlayerMobile{Name="EquipmentLifeOrders",Body=0x190,Player=true};supplier.AddItem(new Backpack());ClassCreation.Profiles.Assign(supplier,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(supplier,99);
            foreach(var b in actors){BattleOrders.Apply(supplier,b,3,TimedEffects.Now.Add(FixtureSpace.Retention));Require(BattleOrders.LifePercent(b)>0,"Positive BO composition missing");Expect(b,b!=actors[2]);}
            foreach(var b in actors){SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)*2/3+17);}
            var lives=actors.Select(SkillHealth.Balance256).ToArray();var maximums=actors.Select(SkillHealth.Maximum256).ToArray();
            p.MoveToWorld(new(p.X+1,p.Y,p.Z),Map.Felucca);
            for(int i=0;i<actors.Length;i++)
            {
                Expect(actors[i],i<2);Require(ReviveFacets.Issue(actors[i])==null&&SkillHealth.Balance256(actors[i])==SummonDifficulties.Life256(lives[i],maximums[i],SkillHealth.Maximum256(actors[i])),"Equipped facet transition healed,rerolled or parked unsupported");
            }
            var states=ReviveNativeActivation.Snapshot();var selected=ReviveNativeActivation.State(actors[0]);bool refused=false;
            try{ReviveNativeActivation.Restore(states.Select(s=>s.Actor==selected.Actor?s with{Setup=s.Setup with{EquipmentLifeProfile="unknown"}}:s).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&states.SequenceEqual(ReviveNativeActivation.Snapshot()),"Unknown equipment life profile was not refused atomically");
            p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,actors.Select(ReviveNativeActivation.State).ToArray(),actors.Select(SkillHealth.Balance256).ToArray(),scenes.Select(s=>s.Corpse.Serial.Value).ToArray(),scenes.SelectMany(s=>s.Items).ToArray())));
        }
        finally{SummonDifficulties.SetFacets(old);}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(s.Actors.Length==3,"Missing equipped/legacy saved originals");
        int itemCount=World.Items.Count;
        for(int i=0;i<s.Actors.Length;i++)
        {
            var b=World.FindMobile((Serial)s.Actors[i].Actor)as BaseCreature;
            Require(b is{Alive:true,Deleted:false}&&b.ControlMaster?.Serial.Value==s.Owner&&b.Map==Map.Internal
                &&ReviveNativeActivation.State(b)==s.Actors[i]&&SkillHealth.Balance256(b)==s.Life[i],"Original equipped/legacy life state/wounds changed on reload");Expect(b,i<2);
            Require(World.FindItem((Serial)s.Corpses[i])is Corpse corpse&&corpse.GetAmount(typeof(Gold))==1,"Separate loot changed on reload");
        }
        Require(World.Items.Count==itemCount,"Equipped life access recreated original gear");
    }
}
