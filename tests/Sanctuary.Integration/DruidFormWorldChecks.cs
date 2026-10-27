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

public static class DruidFormWorldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidFormWorldChecks));
    private sealed record Saved(uint Owner,DruidPlayerForm Form,int Life256,int Mana256,int Stamina256,int MaximumLife,int MaximumStamina,int Body);
    private sealed record SavedExpiry(uint Owner,DruidPlayerForm Form,BattleOrders.Pools BeforeRemoval,int Body);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"druid-form-world-fixtures.json");
    private static string ExpiredManifest=>Path.Join(Core.BaseDirectory,"druid-form-expired-fixtures.json");
    private static bool _creating;private static int _scene;
    public static void Configure()
    {Rates();Register("druid-form-world-check",Check);Register("druid-form-world-reload-check",Reload);Register("druid-form-world-snapshot-check",Snapshot);
        Register("druid-form-live-check",Live);Register("druid-form-expiry-stage-check",StageExpiry);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Rates()
    {
        bool Held(Mobile p)=>_creating||p.Name?.StartsWith("DruidFormFixture",StringComparison.Ordinal)==true;
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;var sp=Mobile.StamRegenRateHandler;
        Mobile.HitsRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):hp?.Invoke(p)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):mp?.Invoke(p)??Mobile.DefaultManaRate;
        Mobile.StamRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):sp?.Invoke(p)??Mobile.DefaultStamRate;
    }
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Prepared saved Druid form stat/resource consumers",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {
        var definition=SkillMechanics.For(ability);
        TalentCatalog.TryGet(DiabloClass.Druid,definition.TalentId,out var talent);
        foreach(var prior in talent.Prerequisites)
        {if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(p,SkillMechanics.All.Single(x=>x.TalentId==prior).Ability);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,definition.TalentId)<rank)
        {Require(ClassCreation.Profiles.TryTrain(p.Serial,definition.TalentId,out _),"Train "+definition.TalentId);}
    }
    private static PlayerMobile Fresh(DiabloClass characterClass=DiabloClass.Druid,bool trained=true)
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Player=true,Name="DruidFormFixture"};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,characterClass);ClassCreation.Profiles.AdvanceLevel(p,99);
        if(characterClass==DiabloClass.Druid&&trained){Train(p,CombatAbility.Werewolf);Train(p,CombatAbility.Lycanthropy);Train(p,CombatAbility.Werebear);}
        FixtureSpace.Corridor(p,6100,20,1000+32*_scene++);
        p.Hits=p.HitsMax;p.Mana=p.ManaMax;p.Stam=p.StamMax;return p;
    }
    private static void Drop(PlayerMobile p)
    {if(p==null){return;}DruidPlayerForms.Clear(p);BattleOrders.Clear(p);p.Corpse?.Delete();p.Delete();}
    private static void Ratio(PlayerMobile p,BattleOrders.Pools before,string why,bool ordersChanged=false)
    {
        // Independent ratio invariant, bounded to one FP256 quantum of the
        // reused native player projection; not an exact-engine rounding claim.
        long lifeError=Math.Abs((long)SkillHealth.Balance256(p)*before.LifeMax*256-(long)before.Life256*p.HitsMax*256);
        Require(lifeError<=(long)before.LifeMax*256,why+" life percentage");
        long stamError=Math.Abs((long)BattleOrders.Stamina256(p)*before.StaminaMax*256-(long)before.Stamina256*p.StamMax*256);
        Require(stamError<=(long)before.StaminaMax*256,why+" stamina percentage");
        if(ordersChanged)
        {
            long manaError=Math.Abs((long)NecromancerEffects.ManaBalance256(p)*before.ManaMax*256-(long)before.Mana256*p.ManaMax*256);
            Require(manaError<=(long)before.ManaMax*256,why+" mana percentage");
        }
        else{Require(NecromancerEffects.ManaBalance256(p)==before.Mana256,why+" changed mana");}
    }
    private static void StatsAndResources()
    {
        var p=Fresh();var supplier=Fresh(DiabloClass.Barbarian,false);
        try
        {
            SkillHealth.SetBalance256(p,p.HitsMax*128-17);BattleOrders.SetStamina256(p,p.StamMax*128-31);
            NecromancerEffects.SetMana256(p,p.ManaMax*256-71);
            var plain=ClassResources.Parts(p,0);var stamina=ClassResources.Parts(p,1);
            int ar=ClassStats.PlayerItemAttackPercent(p),damage=ClassStats.DamageBonus(p,null),defense=ClassStats.Defense(p),body=p.Body;
            var before=BattleOrders.Capture(p);
            Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werewolf,TimedEffects.Now),"Prepared wolf entry");
            var state=DruidPlayerForms.State(p);var m=state.Form.Modifiers;
            Require(m.LifePercent==45&&m.StaminaPercent==25&&m.AttackPercent==50&&m.DurationFrames==2000,"Rank1wolf/Lycan payload");
            Require(ClassResources.Parts(p,0).SkillPercent==plain.SkillPercent+45&&ClassResources.Parts(p,0).Base256==plain.Base256
                &&ClassResources.Parts(p,1).SkillPercent==stamina.SkillPercent+25&&ClassResources.Parts(p,2).SkillPercent==0,"Form op1percentage pools");
            Require(ClassStats.PlayerItemAttackPercent(p)==ar+50&&ClassStats.AttackRating(p)==ClassStats.PlayerAttackPercent(ClassStats.PlayerAttackBase(p),ar+50)
                &&ClassStats.DamageBonus(p,null)==damage&&ClassStats.Defense(p)==defense,"Wolf AR-only weapon/armor channels");
            Ratio(p,before,"Wolf entry");Require(p.Body==body&&!p.Blessed&&!p.Hidden,"Stat preparation changed body/protection");
            var same=DruidPlayerForms.State(p);before=BattleOrders.Capture(p);
            Require(!DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werebear,TimedEffects.Now)&&DruidPlayerForms.State(p)==same
                &&BattleOrders.Capture(p)==before,"Prepared factory invented current cross-form behavior");
            Require(DruidPlayerForms.Clear(p)&&DruidPlayerForms.State(p)==null,"Wolf removal");Ratio(p,before,"Wolf removal");
            BattleOrders.Apply(supplier,p,1,TimedEffects.Now);
            var orders=ClassResources.Parts(p,0);before=BattleOrders.Capture(p);double basePhysical=GearEffects.Damage(p,1).Physical;
            Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werebear,TimedEffects.Now),"Prepared bear entry");
            m=DruidPlayerForms.State(p).Form.Modifiers;
            Require(m.LifePercent==95&&m.DamagePercent==55&&m.ArmorPercent==40&&m.Concentration==100,"Rank1bear/Lycan payload");
            Require(ClassResources.Parts(p,0).SkillPercent==orders.SkillPercent+95
                &&ClassResources.Parts(p,0).Maximum256==(orders with{SkillPercent=orders.SkillPercent+95}).Maximum256,"BO and form life must add in same source bucket");
            Require(ClassStats.DamageBonus(p,null)==damage+55&&GearEffects.Damage(p,1).Physical>basePhysical
                &&ClassStats.PlayerItemAttackPercent(p)==ar&&ClassStats.Defense(p)==ClassStats.ScaleDefense(defense,40),"Bear physical/armor consumers");
            Ratio(p,before,"Bear with BO");before=BattleOrders.Capture(p);BattleOrders.Clear(p);Ratio(p,before,"BO removal inside bear",true);
            Require(DruidPlayerForms.State(p)!=null&&ClassResources.Parts(p,0).SkillPercent==plain.SkillPercent+95,"BO removal erased form life");
            before=BattleOrders.Capture(p);DruidPlayerForms.Prune(DruidPlayerForms.State(p).UntilUtc);
            Require(DruidPlayerForms.State(p)==null,"Elapsed form did not expire");Ratio(p,before,"Bear expiry");
        }
        finally{Drop(p);Drop(supplier);}
    }
    private static void GearAndValidation()
    {
        var p=Fresh();var untrained=Fresh(trained:false);var other=Fresh(DiabloClass.Barbarian,false);
        try
        {
            bool refused=false;try{DruidPlayerForms.ApplyPrepared(untrained,CombatAbility.Werewolf,TimedEffects.Now);}catch(InvalidOperationException){refused=true;}
            Require(refused&&DruidPlayerForms.State(untrained)==null,"Untrained form");
            refused=false;try{DruidPlayerForms.ApplyPrepared(other,CombatAbility.Werebear,TimedEffects.Now);}catch(InvalidOperationException){refused=true;}
            Require(refused&&DruidPlayerForms.State(other)==null,"Wrong-class form");
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(321),true);
            Require(p.EquipItem(helm),"Real skill gear equip");ClassGear.Refresh(p);
            Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werewolf,TimedEffects.Now),"Geared form");
            var state=DruidPlayerForms.State(p);
            Require(state.Form.Rank==2&&state.Form.LycanthropyRank==2&&state.Form.Modifiers.LifePercent==50&&state.Form.Modifiers.AttackPercent==65,"Effective own/Lycan gear capture");
            p.RemoveItem(helm);helm.Delete();ClassGear.Refresh(p);
            Require(DruidPlayerForms.State(p)==state&&SkillMechanics.Rank(p,CombatAbility.Werewolf)==1,"Captured form unexpectedly refreshed from gear loss");
            var rows=DruidPlayerForms.Snapshot();string json=JsonSerializer.Serialize(rows);
            DruidPlayerForms.Restore(JsonSerializer.Deserialize<DruidPlayerForm[]>(json));Require(DruidPlayerForms.State(p)==state,"Prepared JSON roundtrip");
            foreach(var bad in new[]{rows.Append(state).ToArray(),rows.Select(x=>x.Owner==p.Serial.Value?x with{UntilUtc=x.UntilUtc.AddMilliseconds(40)}:x).ToArray(),
                rows.Select(x=>x.Owner==p.Serial.Value?x with{Form=x.Form with{Modifiers=x.Form.Modifiers with{LifePercent=999}}}:x).ToArray()})
            {
                refused=false;try{DruidPlayerForms.Restore(bad);}catch(InvalidDataException){refused=true;}
                Require(refused&&JsonSerializer.Serialize(DruidPlayerForms.Snapshot())==json,"Malformed restore partially replaced forms");
            }
            ClassCombat.Stop(p);p.Frozen=true;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);
            Require(DruidPlayerForms.State(p)==state,"Stop/freeze/movement removed established form");p.Frozen=false;
            p.Kill();DruidPlayerForms.Prune(TimedEffects.Now);
            Require(DruidPlayerForms.State(p)==null&&!DruidPlayerForms.Snapshot().Any(x=>x.Owner==p.Serial.Value),"Death retained form");
            Drop(untrained);untrained=Fresh();
            Require(DruidPlayerForms.ApplyPrepared(untrained,CombatAbility.Werebear,TimedEffects.Now),"Deleted owner form");
            uint deleted=untrained.Serial.Value;untrained.Delete();DruidPlayerForms.Prune(TimedEffects.Now);
            Require(!DruidPlayerForms.Snapshot().Any(x=>x.Owner==deleted),"Deleted owner retained form");
        }
        finally{Drop(p);Drop(untrained);Drop(other);}
    }
    private static void Write(PlayerMobile p)
    {
        var state=DruidPlayerForms.State(p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,state,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),BattleOrders.Stamina256(p),p.HitsMax,p.StamMax,p.Body)));
    }
    private static void Live()
    {
        var p=Fresh();SkillHealth.SetBalance256(p,p.HitsMax*128-17);
        // A consistent old cast,near its ordinary80second endpoint. Runtime
        // Prune must remove it; this check never calls Prune itself.
        Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werewolf,TimedEffects.Now.AddMilliseconds(-79800)),"Real timer form");
        var before=BattleOrders.Capture(p);int body=p.Body;
        Timer.StartTimer(TimeSpan.FromMilliseconds(500),()=>
        {
            try
            {
                Require(DruidPlayerForms.State(p)==null&&p.Body==body,"Real40ms timer did not remove elapsed form");
                Ratio(p,before,"Natural expiry");Log.Information("druid-form-live-result: passed");
            }
            catch(Exception error){Log.Error(error,"druid-form-live-result: failed");}
            finally{Drop(p);}
        });
    }
    private static void StageExpiry()
    {
        var p=Fresh();SkillHealth.SetBalance256(p,p.HitsMax*128-17);BattleOrders.SetStamina256(p,p.StamMax*128-31);
        NecromancerEffects.SetMana256(p,p.ManaMax*256-71);
        Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werewolf,TimedEffects.Now.AddSeconds(-77)),"Offline expiry stage");
        var form=DruidPlayerForms.State(p);Require(form.UntilUtc>TimedEffects.Now,"Offline fixture expired before save");
        File.WriteAllText(ExpiredManifest,JsonSerializer.Serialize(new SavedExpiry(p.Serial.Value,form,BattleOrders.Capture(p),p.Body)));
    }
    private static void Check()
    {
        Require(DruidCombat.Handles(CombatAbility.Werewolf)&&DruidCombat.Handles(CombatAbility.Werebear)
            &&DruidCombat.Implemented(CombatAbility.Lycanthropy)&&!DruidCombat.Handles(CombatAbility.Lycanthropy),"Learned form routes and passive-only Lycanthropy composition");
        StatsAndResources();GearAndValidation();
        var p=Fresh();Train(p,CombatAbility.Werewolf,20);Train(p,CombatAbility.Lycanthropy,20);Train(p,CombatAbility.Werebear,20);
        Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werebear,TimedEffects.Now),"Saved bear");
        SkillHealth.SetBalance256(p,p.HitsMax*256-20007);NecromancerEffects.SetMana256(p,p.ManaMax*256-71);BattleOrders.SetStamina256(p,p.StamMax*256-31);
        Write(p);Snapshot();
        Log.Information("Druid form saved world:typed life/stamina/AR/physical/armor pools,BO stacking,proportional entry/removal/expiry,effective real-gear capture,atomic malformed restore,death/deletion and strict fractional injury guards pass; no learned casting,modern rates/restrictions or body art claimed");
    }
    private static void Snapshot()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        Require(World.FindMobile((Serial)saved.Owner) is PlayerMobile,"Saved form owner missing");var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(DruidPlayerForms.State(p)==saved.Form&&p.HitsMax==saved.MaximumLife&&p.StamMax==saved.MaximumStamina
            &&SkillHealth.Balance256(p)==saved.Life256&&NecromancerEffects.ManaBalance256(p)==saved.Mana256&&BattleOrders.Stamina256(p)==saved.Stamina256
            &&p.Body==saved.Body,"Saved form/birth/deadline/FP pools changed");
    }
    private static void Reload()
    {
        Snapshot();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(saved.Form.Form.Rank==20&&saved.Form.Form.LycanthropyRank==20&&saved.Form.UntilUtc>TimedEffects.Now
            &&DruidPlayerForms.State(p).Form.Modifiers.DamagePercent==340,"Captured saved form rank/lifetime");
        var expired=JsonSerializer.Deserialize<SavedExpiry>(File.ReadAllText(ExpiredManifest));
        Require(World.FindMobile((Serial)expired.Owner) is PlayerMobile,"Offline expiry owner missing");
        var prior=(PlayerMobile)World.FindMobile((Serial)expired.Owner);
        Require(expired.Form.UntilUtc<TimedEffects.Now&&DruidPlayerForms.State(prior)==null
            &&!DruidPlayerForms.Snapshot().Any(x=>x.Owner==expired.Owner)&&prior.Body==expired.Body,"Elapsed saved form retained or restored presentation");
        Ratio(prior,expired.BeforeRemoval,"Offline saved expiry");Drop(prior);
        Log.Information("Druid form expired reload:elapsed original deadline removed before gameplay with proportional projected pools,no healing or renewal");
        Log.Information("Druid form reload:original captured ranks/start/deadline,typed pools and20007FPinjury preserved without refresh,healing or duplicate scaling; no new cast occurred");
    }
}
