// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowBodyChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowBodyChecks));
    private sealed record Held(uint Actor,string State,uint[] Items,string[] Payloads,string Resources,NpcEquipmentSeed Random,
        int Life,int Maximum,int Mana,int ManaMaximum,int Stamina,int StaminaMaximum,EquipmentStatValue[] Numeric);
    private sealed record Saved(Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-body-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Reject(Action run)
    {try{run();}catch(Exception e)when(e is ArgumentException or InvalidDataException or NotSupportedException or InvalidOperationException){return;}throw new Exception("Unsupported Shadow body context accepted");}
    public static void Configure(){Register("shadow-body-check",false);Register("shadow-body-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original initialized Shadow bodies/resources;controller/cast remain gated",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Held Capture(AssassinShadowBody b)
    {
        var items=ShadowLoadout.Originals(b);var graph=b.EquipmentNumbers;var resources=GeneratedNpcLiveResources.State(b);
        return new(b.Serial.Value,b.SavedText,items.Select(i=>i.Serial.Value).ToArray(),items.Select(i=>i.SavedText).ToArray(),Json(resources),NpcEquipmentRandom.Require(b),
            SkillHealth.Balance256(b),SkillHealth.Maximum256(b),NecromancerEffects.RawManaBalance256(b),NecromancerEffects.ManaMaximum256(b),BattleOrders.RawStamina256(b),BattleOrders.StaminaMaximum256(b),graph.Stats);
    }
    private static void Validate(AssassinShadowBody b)
    {
        var state=b.Prepared;AssassinShadowBody.Validate(state);ShadowInitializer.Validate(state.Initialization);var items=ShadowLoadout.Originals(b);
        Require(b.Map==Map.Internal&&b.CantWalk&&!((IPersistentMinion)b).CanActivate&&!PersistentMinions.CanThink(b)&&MinionSafeAreas.Parked(b),"Resource-only Shadow escaped parking/controller admission");
        var resources=GeneratedNpcLiveResources.State(b);var graph=b.EquipmentNumbers;
        Require(resources is{Shadow:not null,Valkyrie:null,ItemEvents:null}&&resources.Shadow.Capture==state.Capture&&resources.Shadow.Initialization==state.Initialization,
            "Shadow live resources lost their original source context");
        Require(graph.ActiveItems.Length==items.Length&&graph.ActiveItems.All(id=>items.Any(i=>i.Serial.Value==id)),"Shadow birth list confused original item attachment order");
        Require(graph.Read(7)==SkillHealth.Maximum256(b)&&graph.Read(9)==NecromancerEffects.ManaMaximum256(b)&&graph.Read(11)==BattleOrders.StaminaMaximum256(b),"Shadow FP256 maxima do not own native resource reads");
        Require(resources.DamageRegeneration==NecromancerSummons.Source(b.SourceMonsterId,"DamageRegen")&&resources.Basis.Any(v=>v.Stat==74&&v.Value==state.Initialization.RegenerationBase),"Shadow source raw regeneration provenance changed");
        Require(NpcEquipmentRandom.Require(b).ShadowBinding==state.Weapons.Identity&&NpcEquipmentRandom.Require(b).ValkyrieBinding==null,"Shadow attack RNG borrowed another species binding");
        Require(CreatureProfiles.Definition(b).Reference==b.SourceMonsterId&&CreatureProfiles.Variant(b).Traits is{Family:"Shadow",Undead:false,Demon:false},"Shadow inherited skeleton/appearance traits");
        Require(b.InitialSkillRequests.All(s=>s.Rank==1+new[]{0,3,7}[(int)state.Difficulty]),"Shadow initial skill request difficulty did not rescale");
        Reject(()=>b.BeginMelee(b.ControlMaster));
        if(state.Skills!=null)
        {
            var book=b.OwnedSkills;Require(book.Left==251&&book.Right==266&&book.Used==251&&book.Find(251).Bonus==2,"Original NPC skill entries/selections/bonus did not survive save");
            Require(book.Find(266).Mode==14&&book.Find(0).Mode==5,"Reload reset NPC custom modes");
        }
        if(resources.Passives is{} passives)
        {
            Require(graph.Passives!=null&&BattleCommand.Bonus(b)==1&&Json(graph.Passives.Capture())==Json(passives),"Reload lost active Command or exact source passive cache/order");
            foreach(var list in passives.Lists){Require(list.Level==b.OwnedSkillRank(list.Skill),"Loaded passive did not retain its Command-adjusted own skill rank");}
        }
    }
    private static void CallbackBirth(AssassinShadowBody b)
    {
        var state=b.Prepared;var raw=ShadowInitializer.Basis(state.Initialization,state.Capture.Level);
        var graph=new GeneratedNpcStatGraph(b,raw,_=>null,(_,_,_,_,_)=>{},false,_=>{},_=>{},
            sourceDamageRegeneration:NecromancerSummons.Source(b.SourceMonsterId,"DamageRegen"),activeOrder:[],
            beforeReplay:g=>ShadowEquipmentResources.ApplyBirth(g,new(state.Capture,state.Initialization,state.Difficulty)));
        int maximum=state.Capture.Plan(state.Initialization.SpawnLife256).MaximumLifeWrite256;
        int expected=maximum==state.Initialization.SpawnLife256?state.Initialization.RegenerationBase
            :(NecromancerSummons.Source(b.SourceMonsterId,"DamageRegen")*(maximum>>8))>>4;
        Require(graph.Read(7)==maximum&&graph.Read(6)==maximum&&graph.Read(74)==expected,"Birth maximum-life write missed the already-registered server resource callback");
    }
    private static void Pools(AssassinShadowBody b)
    {
        int maximum=SkillHealth.Maximum256(b);SkillHealth.SetBalance256(b,Math.Max(1,maximum*2/3-17));
        var items=ShadowLoadout.Originals(b);var before=Capture(b);uint item=items[0].Serial.Value;
        GeneratedNpcLiveResources.SetItemActive(b,item,false);int next=SkillHealth.Maximum256(b);
        Require(SkillHealth.Balance256(b)==BattleOrders.Rescale256(before.Life,before.Maximum,next),"Original item removal lost FP256 wounds");
        int wounded=SkillHealth.Balance256(b);GeneratedNpcLiveResources.SetItemActive(b,item,true);int restored=SkillHealth.Maximum256(b);
        Require(SkillHealth.Balance256(b)==BattleOrders.Rescale256(wounded,next,restored),"Original item reattachment filled or rounded wounds");
        Require(ShadowLoadout.Originals(b).Select(i=>i.SavedText).SequenceEqual(before.Payloads),"Resource transition changed an original item");
        int life=SkillHealth.Balance256(b);NpcEquipmentRegeneration.Apply(b,100000);b.AdvanceRecovery(TimedEffects.Now.AddMinutes(2),true);
        Require(SkillHealth.Balance256(b)==life&&b.Map==Map.Internal,"Parked Shadow recovered or activated during elapsed time");
        Require(SkillHealth.Heal(b,111)==111&&SkillHealth.Balance256(b)==life+111,"Explicit parked-roster healing did not reach Shadow FP256 pools");
    }
    private static void Random(AssassinShadowBody b)
    {
        var before=NpcEquipmentRandom.Require(b);ulong seed=before.Seed;uint expected=SourceRandom.Roll(ref seed);var world=NpcWorldAllocation.Capture();
        Require(NpcEquipmentRandom.Raw(b)==expected&&NpcEquipmentRandom.Require(b).Seed==seed&&NpcWorldAllocation.Capture()==world,"Shadow actor RNG consumed or substituted world allocation");
        NpcEquipmentRandom.Forget(b);Reject(()=>NpcEquipmentRandom.Raw(b));Require(NpcWorldAllocation.Capture()==world,"Missing Shadow stream used world RNG");NpcEquipmentRandom.Set(b,seed);
        var all=NpcEquipmentRandom.Snapshot();Reject(()=>NpcEquipmentRandom.Restore(all.Select(s=>s.Actor==b.Serial.Value?s with{ValkyrieBinding=s.ShadowBinding}:s).ToArray()));
        Require(NpcEquipmentRandom.Require(b).Seed==seed,"Invalid mixed species RNG restore changed the live stream");
    }
    private static void Rescale(AssassinShadowBody b,PlayerMobile owner,MonsterDifficulty original)
    {
        var facets=SummonDifficulties.Facets();var prior=Capture(b);var world=NpcWorldAllocation.Capture();
        try
        {
            foreach(var d in Enum.GetValues<MonsterDifficulty>())
            {
                SummonDifficulties.SetFacets(facets.Where(f=>f.Map!=owner.Map.MapID).Append(new(owner.Map.MapID,d)).ToArray());SummonDifficulties.Sync(owner);
                Require(b.Prepared.Difficulty==d&&b.InitialSkillRequests.All(s=>s.Rank==1+new[]{0,3,7}[(int)d]),"Parked Shadow missed designated facet difficulty");
                Require(SkillHealth.Balance256(b)==prior.Life&&SkillHealth.Maximum256(b)==prior.Maximum&&NpcEquipmentRandom.Require(b)==prior.Random
                    &&ShadowLoadout.Originals(b).Select(i=>i.SavedText).SequenceEqual(prior.Payloads)&&NpcWorldAllocation.Capture()==world,"Difficulty rescale refilled wounds,regenerated equipment or consumed RNG");
            }
            SummonDifficulties.SetFacets(facets.Where(f=>f.Map!=owner.Map.MapID).Append(new(owner.Map.MapID,original)).ToArray());SummonDifficulties.Sync(owner);
        }
        finally{SummonDifficulties.SetFacets(facets);}
    }
    private static void Refusals(AssassinShadowBody b,PlayerMobile owner)
    {
        var world=NpcWorldAllocation.Capture();int count=World.Mobiles.Count;string saved=b.SavedText;
        Reject(()=>AssassinShadowBody.CreateInitialized(owner,b.Prepared.Capture.Ability,DateTime.SpecifyKind(TimedEffects.Now,DateTimeKind.Unspecified)));
        Reject(()=>AssassinShadowBody.CreateInitialized(owner,CombatAbility.BasicAttack,TimedEffects.Now));
        Reject(()=>b.RestorePrepared(b.Prepared with{Initialization=b.Prepared.Initialization with{AfterInitialization=b.Prepared.Initialization.AfterInitialization^1}}));
        Require(World.Mobiles.Count==count&&NpcWorldAllocation.Capture()==world&&b.SavedText==saved,"Unsupported Shadow request changed state or consumed allocation");
        Reject(()=>GeneratedNpcLiveResources.ChangeBase(b,7,1234));
        PersistentMinions.Resume(owner);Require(b.Map==Map.Internal&&MinionSafeAreas.Parked(b),"Partial Shadow resumed through generic roster code");
    }
    private static void Check(bool reload)
    {
        ShadowSkillBookChecks.SourceControls();
        Require(NpcWorldAllocation.Capture()==NpcWorldAllocationChecks.CheckedContinuation,"Unexpected shared world draws before Shadow bodies");
        var held=new List<Held>();int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;Require(generation<=2,"Shadow body reload generation");
            foreach(var old in saved.Bodies)
            {
                var b=World.FindMobile((Serial)old.Actor)as AssassinShadowBody;Require(b!=null,"Original dedicated Shadow actor did not load");Validate(b);
                Require(Json(Capture(b))==Json(old),"Shadow original body/items/resources/numeric lists/FP256 wounds/RNG changed on reload");held.Add(Capture(b));
            }
        }
        else
        {
            var facets=SummonDifficulties.Facets();
            try
            {
                foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster})foreach(var difficulty in Enum.GetValues<MonsterDifficulty>())for(int n=0;n<4;n++)
                {
                    var p=new PlayerMobile{Name="ShadowBodyOwnerFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Assassin);ClassCreation.Profiles.AdvanceLevel(p,99);
                    int hard=new[]{1,1,9,19}[n];ClassSkillChecks.Train(p,SkillMechanics.For(ability).TalentId,hard);FixtureSpace.Corridor(p,6240,32,3380);
                    ClassSkillChecks.Train(p,SkillMechanics.Source("Fire Trauma").TalentId,3);
                    Item helm=null,armor=null;if(n>0){helm=PoisonDaggerChecks.Named(p,"Tarnhelm");armor=PoisonDaggerChecks.Named(p,"Greyform");ClassGear.Refresh(p);}
                    SummonDifficulties.SetFacets(facets.Where(f=>f.Map!=p.Map.MapID).Append(new(p.Map.MapID,difficulty)).ToArray());
                    var snapshot=ShadowOwnerSnapshot.Capture(p,ability);var before=NpcWorldAllocation.Capture();var b=AssassinShadowBody.CreateInitialized(p,ability,TimedEffects.Now);
                    Require(b.Prepared.Capture==snapshot&&b.Prepared.Capture.Rank==new[]{1,2,10,20}[n]&&b.Prepared.Initialization.WorldBefore==before.Seed,"Actual trained/item owner capture or unit allocation changed");
                    Require(b.Prepared.Equipment.GameAfter==NpcWorldAllocation.Capture().Seed&&b.Prepared.Equipment.PetAfter==NpcEquipmentRandom.Require(b).Seed,"Original equipment did not hand off its actual continuations");
                    Require(helm==null||helm.Parent==p&&armor.Parent==p&&b.Prepared.Capture.Head==ClassGear.Base(helm).Code&&b.Prepared.Capture.Armor==ClassGear.Base(armor).Code,"Shadow borrowed owner items or guessed owner bases");
                    ShadowSkillBookChecks.Body(b,p);ShadowPassiveChecks.Body(b);Validate(b);CallbackBirth(b);Pools(b);Random(b);Rescale(b,p,difficulty);Refusals(b,p);
                    if(helm!=null){var capture=b.Prepared.Capture;p.AddToBackpack(helm);ClassGear.Refresh(p);Require(ShadowOwnerSnapshot.Capture(p,ability).Rank==capture.Rank-1&&b.Prepared.Capture==capture,"Existing Shadow recaptured changed owner gear");Require(p.EquipItem(helm),"Owner helm return");ClassGear.Refresh(p);}
                    held.Add(Capture(b));p.Internalize();
                }
            }
            finally{SummonDifficulties.SetFacets(facets);}
            Require(held.Count==24,"Missing actual Shadow rank/difficulty births");
        }
        Require(!AssassinCombat.Implemented(CombatAbility.ShadowWarrior)&&!AssassinCombat.Implemented(CombatAbility.ShadowMaster),"Resource backend enabled incomplete learned Shadows");
        ShadowPassiveChecks.SourceControls();
        NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(held.ToArray(),generation)));
        Log.Information("Shadow bodies:24actual initialized species/owners and unfiltered original inventories;source callback before birth scaling,item/actor/world continuations,FP256 life/mana/stamina,logical item changes,owner snapshots,parked no-recovery/healer support and designated-facet rescale;actual body/items/resources/wounds/RNG through two reloads;NPC skills/events,AI105/106,UMod death and learned casts remain gated");
    }
}
