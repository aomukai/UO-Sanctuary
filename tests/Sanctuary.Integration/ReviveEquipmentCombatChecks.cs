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

public static class ReviveEquipmentCombatChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveEquipmentCombatChecks));
    private sealed record Saved(uint Owner,ReviveNativeState[] Actors,int[] Attack,int[] Defense,int[] Life,uint[] Corpses);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-equipment-combat-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("revive-equipment-combat-check",Check);Register("revive-equipment-combat-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Actual original Revive frozen attack/defense and hit checks",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Item Named(string name)=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==name).Id,99,new Random(173),true);
    private static (BaseCreature Actor,Corpse Corpse) Create(PlayerMobile p,bool encounter,bool enabled)
    {
        BaseCreature b;
        if(encounter)
        {
            var profile=EncounterProfiles.Profile("crypt-sentinel-90");var e=new EncounterCreature();
            e.Assign(profile,profile.MinimumLevel,MonsterDifficulty.Normal,EncounterGrade.Minion,ChampionKind.Champion,[EliteModifier.ExtraStrong],EliteAura.None,null,new Random(173));b=e;
        }
        else{b=new Skeleton();}
        b.Name="PoisonDaggerFixtureReviveCombat";b.NoKillAwards=true;
        foreach(var item in b.Items.Where(i=>i is not Backpack&&i.Layer!=Layer.Bank).ToArray()){item.Delete();}
        b.RawDex=61;b.RawInt=70;b.ManaMaxSeed=70;b.StamMaxSeed=55;
        Item[] gear=[Named("The Atlantian"),Named("Waterwalk"),Named("Harlequin Crest"),Named("Raven Frost")];foreach(var item in gear){b.AddItem(item);}
        // Exercise native equipment-offset removal explicitly. This injected
        // native stat mod is not claimed to be an extra D2 item property.
        b.AddStatMod(new StatMod(StatType.Dex,gear[0].Serial+"Dex",17,TimeSpan.Zero));
        int nativeOffset=gear.Sum(item=>b.GetStatMod(item.Serial+"Dex")?.Offset??0);
        Require(nativeOffset>0&&b.GetStatOffset(StatType.Dex)==nativeOffset&&b.Dex==61+nativeOffset,
            "Native equipment-offset control missing: "+JsonSerializer.Serialize(new{b.RawDex,b.Dex,nativeOffset,Total=b.GetStatOffset(StatType.Dex)}));
        b.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);b.AIObject?.AITimer.Stop();Corpse corpse=null;var died=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(who,container)=>{died?.Invoke(who,container);if(who==b){corpse=container as Corpse;corpse?.DropItem(new Gold(1));}};
        try{p.DoHarmful(b,true);b.Damage(b.Hits+1,p);CombatDamage.Finish(b,p);}finally{ClassLootHooks.CreatureDied=died;}
        Require(corpse is{Deleted:false}&&ReviveOriginals.Find(corpse)==b,"Equipped combat original was not retained");corpse.BeginDecay(FixtureSpace.Retention);
        b.RemoveStatMod(gear[0].Serial+"Dex");
        Require(!ReviveCasts.Plan(p,corpse,out _,out _,out _),"Public equipped Revive gate was lifted");
        var original=ReviveOriginals.State(corpse);var before=ReviveBeforeDeath.Decode(original.BeforeDeathJson);
        Require(before.AttackDefenseBases is{Issue:null}&&before.AttackDefenseBases.Kind==(encounter?"source-body":"native-attributes"),"Gear-free native combat basis missing");
        if(!encounter){Require(before.AttackDefenseBases.AttackRating==122&&before.AttackDefenseBases.Defense==15,"Native gear Dex was counted in the frozen base");}
        var setup=ReviveLife.Setup(p,corpse,before.Mana,before.Stamina)with{RegenerationRate=ReviveRegeneration.Rate("skeleton1")};
        setup=enabled?ReviveEquipmentCombat.Prepare(setup,original):ReviveEquipmentResources.Prepare(setup,original);
        Require(ReviveNativeActivation.CommitPrepared(p,corpse,corpse.Location,before.AiStatus.Value,setup,out var actual,out var issue)&&actual==b,"Frozen combat activation failed: "+issue);
        foreach(var item in gear){Require(item.Parent==corpse,"Combat gear did not remain independent loot");p.AddToBackpack(item);item.Delete();}
        Expect(b,enabled);return(b,corpse);
    }
    private static void Expect(BaseCreature b,bool enabled)
    {
        if(!enabled)
        {Require(ReviveEquipmentCombat.AttackBasis(b)==null&&NpcEquipmentCombat.AttackFlat(b)==0&&NpcEquipmentCombat.AttackPercent(b)==0&&NpcEquipmentCombat.Defense(b)==0,"Prior resource profile acquired frozen combat");return;}
        var row=ReviveNativeActivation.State(b);var before=ReviveBeforeDeath.Decode(row.Original.BeforeDeathJson);int level=(row.FacetPlan?.Combat??row.Setup.CombatPlan).Level;
        int attack=0,percent=0,dex=0,armor=0,roundedSeparately=0;
        foreach(var snapshot in ReviveBeforeDeath.Equipment(before))
        {var item=FrozenEquipmentEffects.Bind(snapshot);int d=item.Bonus("dex",level);dex+=d;roundedSeparately+=d/4;attack+=item.Bonus("att",level);percent+=item.Bonus("att%",level);armor+=item.At(level).Defense;}
        Require(attack>0&&percent>0&&dex/4!=roundedSeparately&&armor>0,"Flat/percent/pooled-Dex/armor controls were vacuous");
        var profile=CreatureProfiles.For(b);
        int attackBase=profile.ApplyStats&&profile.Attack1.AttackRating is{} rating?rating:before.AttackDefenseBases.AttackRating.Value;
        int defenseBase=profile.ApplyStats&&profile.Defense is{} defense?defense:before.AttackDefenseBases.Defense.Value;
        Require(CreatureProfiles.AttackRating(b)==attackBase&&CreatureProfiles.Defense(b)==defenseBase,"Revive base included frozen equipment or wrong difficulty");
        int flat=attack+5*dex,addedDefense=armor+dex/4;
        Require(NpcEquipmentCombat.AttackFlat(b)==flat&&NpcEquipmentCombat.AttackPercent(b)==percent&&NpcEquipmentCombat.Defense(b)==addedDefense,"Frozen item contributions were lost or rounded per item");
        int expected=attackBase+flat;expected+=(int)((long)expected*percent/100);
        int skill=attackBase+flat;skill+=(int)((long)skill*(percent+37)/100);
        Require(ClassStats.AttackRating(b)==expected&&ClassStats.AttackRating(b,37)==skill
            &&ClassStats.Defense(b)==ClassStats.ScaleDefense(defenseBase+addedDefense,BarbarianShout.Defense(b)),"Actual NPC stat consumers did not apply gear once before active skill percentages");
        var target=new SkeletonGuardian();target.Initialize((PlayerMobile)b.ControlMaster,20,0);
        try
        {
            double chance=ClassStats.HitChance(expected,ClassStats.Defense(target),ClassStats.Level(b),ClassStats.Level(target));
            Require(ClassStats.CheckHit(b,target,0,()=>Math.Max(0,chance-0.000001),notifyMelee:false)==true
                &&ClassStats.CheckHit(b,target,0,()=>Math.Min(1,chance+0.000001),notifyMelee:false)==false,"Actual hit boundary ignored frozen Revive rating");
        }
        finally{target.Delete();}
    }
    private static void Check()
    {
        var old=SummonDifficulties.Facets();var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureReviveCombatOwner";
        MagePetBuffChecks.Train(p,"necromancer.revive",20);MagePetBuffChecks.Train(p,"necromancer.skeletonmastery",20);FixtureSpace.Corridor(p,6920,36,3400);
        SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
        try
        {
            var scenes=new[]{Create(p,false,true),Create(p,true,true),Create(p,false,false)};var actors=scenes.Select(s=>s.Actor).ToArray();
            var supplier=new PlayerMobile{Name="ReviveCombatShout",Body=0x190,Player=true};supplier.AddItem(new Backpack());ClassCreation.Profiles.Assign(supplier,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(supplier,99);
            foreach(var b in actors.Take(2)){BarbarianShout.Apply(supplier,b,3,TimedEffects.Now);Require(BarbarianShout.Defense(b)>0,"Positive Shout control missing");Expect(b,true);BarbarianShout.Clear(b);}supplier.Delete();
            foreach(var difficulty in new[]{MonsterDifficulty.Nightmare,MonsterDifficulty.Hell,MonsterDifficulty.Normal,MonsterDifficulty.Hell})
            {
                SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,difficulty)]);
                p.MoveToWorld(new(p.X+1,p.Y,p.Z),difficulty==MonsterDifficulty.Normal?Map.Trammel:Map.Felucca);ReviveFacets.Sync(p);
                for(int i=0;i<actors.Length;i++){Require(ReviveFacets.Issue(actors[i])==null,"Frozen combat facet projection parked: "+ReviveFacets.Issue(actors[i]));Expect(actors[i],i<2);}
            }
            var state=ReviveNativeActivation.State(actors[0]);var payload=ReviveBeforeDeath.Decode(state.Original.BeforeDeathJson);bool refused=false;
            try{_=ReviveEquipmentCombat.Prepare(state.Setup,state.Original with{BeforeDeathJson=JsonSerializer.Serialize(payload with{AttackDefenseBases=null})});}catch(NotSupportedException){refused=true;}
            Require(refused,"Historical missing gear-free combat bases were invented");
            var historical=state.Original with{BeforeDeathJson=JsonSerializer.Serialize(payload with{AttackDefenseBases=null})};
            ReviveBeforeDeath.Validate(historical.BeforeDeathJson,historical.RuntimeType);
            Require(ReviveEquipmentResources.Prepare(state.Setup,historical).EquipmentLifeProfile==ReviveEquipmentResources.Profile,"Historical resource-only preparation changed");
            var probe=new Skeleton();try
            {
                probe.AddStatMod(new StatMod(StatType.Dex,"OtherCombatModifier",7,TimeSpan.Zero));var unsupported=ReviveEquipmentCombat.Capture(probe,[]);
                Require(unsupported.Issue?.Contains("Non-equipment")==true&&unsupported.Kind==null&&unsupported.AttackRating==null&&unsupported.Defense==null,"Unsupported native modifier invented combat bases");
            }finally{probe.Delete();}
            var rows=ReviveNativeActivation.Snapshot();refused=false;
            try{ReviveNativeActivation.Restore(rows.Select(r=>r.Actor==state.Actor?r with{Original=r.Original with{BeforeDeathJson=JsonSerializer.Serialize(payload with{AttackDefenseBases=payload.AttackDefenseBases with{AttackRating=123}})}}:r).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&rows.SequenceEqual(ReviveNativeActivation.Snapshot()),"Malformed native base was not refused atomically");
            foreach(var b in actors){SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)*2/3+17);}p.Internalize();
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,actors.Select(ReviveNativeActivation.State).ToArray(),actors.Select(b=>ClassStats.AttackRating(b)).ToArray(),actors.Select(ClassStats.Defense).ToArray(),actors.Select(SkillHealth.Balance256).ToArray(),scenes.Select(s=>s.Corpse.Serial.Value).ToArray())));
        }
        finally{SummonDifficulties.SetFacets(old);}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(s.Actors.Length==3,"Missing equipped combat originals");int count=World.Items.Count;
        for(int i=0;i<s.Actors.Length;i++)
        {
            var b=World.FindMobile((Serial)s.Actors[i].Actor)as BaseCreature;
            Require(b is{Alive:true,Deleted:false}&&b.Map==Map.Internal&&b.ControlMaster?.Serial.Value==s.Owner&&ReviveNativeActivation.State(b)==s.Actors[i]
                &&ClassStats.AttackRating(b)==s.Attack[i]&&ClassStats.Defense(b)==s.Defense[i]&&SkillHealth.Balance256(b)==s.Life[i],"Original frozen combat/source/wounds changed on reload");
            Expect(b,i<2);Require(World.FindItem((Serial)s.Corpses[i])is Corpse c&&c.GetAmount(typeof(Gold))==1,"Combat independent loot changed");
        }
        Require(World.Items.Count==count,"Frozen combat recreated equipment");
    }
}
