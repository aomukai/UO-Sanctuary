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

public static class SacrificeChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(SacrificeChecks));
    private sealed record Saved(uint Owner,uint Enemy,uint Weapon,Point3D Origin,int Map,int OwnerLife,int EnemyLife,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sacrifice-checks.json");
    public static void Configure()
    {
        EventSink.WorldLoad+=()=>
        {
            if(File.Exists(Manifest))
            {
                var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
                if(World.FindMobile((Serial)saved.Enemy) is BaseCreature enemy){Stop(enemy);}
            }
        };
        Register("sacrifice-check",Check);Register("sacrifice-reload-check",Reload);
    }
    private static void Stop(BaseCreature enemy)
    {enemy.Frozen=enemy.CantWalk=true;enemy.Combatant=null;enemy.AIObject?.AITimer.Stop();}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Sacrifice prepared physical, self-cost, melee input and reload",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static int Life(Mobile actor)=>actor.Alive?actor.Hits*256-SkillHealth.Remainder256(actor):0;
    private static void Freeze(Mobile actor)
    {
        TimedEffects.Poison(actor,actor,.000001,60000,TimedEffects.Now);
        TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==actor.Serial.Value?x with{UpdatedUtc=TimedEffects.Now.AddMinutes(5)}:x).ToArray());
    }
    private static void Ready(PlayerMobile actor)
    {ClassCombat.Stop(actor);var state=ClassCombat.For(actor);state.NextAction=state.RecoveryUntil=Core.TickCount;}
    private static void Resist(BaseCreature actor,int physical)
    {
        actor.SetResistance(ResistanceType.Physical,physical);actor.SetResistance(ResistanceType.Fire,100);
        actor.SetResistance(ResistanceType.Cold,100);actor.SetResistance(ResistanceType.Energy,100);
    }
    private static void Cast(PlayerMobile owner,Mobile target,int input=0)
    {
        Ready(owner);bool ok=input==0?ClassCombat.ContinueAttack(owner,CombatAbility.Sacrifice,target,target.Location,Core.TickCount)
            :input==1?ClassCombat.ContinueCast(owner,CombatAbility.Sacrifice,target,target.Location,Core.TickCount)
            :ClassCombat.Perform(owner,CombatAbility.Sacrifice,target,target.Location,Core.TickCount);
        Require(ok,"Sacrifice melee input rejected");ClassCombat.ReleaseCast(owner);
        Require(ClassCombat.ResolveAction(owner,ClassCombat.For(owner).EffectAt),"Sacrifice melee event rejected");
    }
    private static void Controlled(Action action)
    {
        var roll=PaladinSacrifice.Roll;var hit=PaladinSacrifice.HitRoll;var element=PaladinSacrifice.ElementRoll;
        try{PaladinSacrifice.Roll=_=>0;PaladinSacrifice.HitRoll=()=>0;PaladinSacrifice.ElementRoll=()=>0;action();}
        finally{PaladinSacrifice.Roll=roll;PaladinSacrifice.HitRoll=hit;PaladinSacrifice.ElementRoll=element;}
    }
    private static void Probe()
    {
        string root=Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT");
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(root,"tools/reference/sacrifice-engine-evidence.json")));
        foreach(var row in json.RootElement.GetProperty("probe").GetProperty("data").GetProperty("costCases").EnumerateArray())
        {Require(PaladinSacrifice.Cost256(row[1].GetInt32(),row[2].GetInt32(),row[0].GetInt32())==row[4].GetInt32(),"Self-cost differs from original Do64");}
        foreach(var row in json.RootElement.GetProperty("physicalProbe").GetProperty("data").EnumerateArray())
        {
            int stat=row[7].GetInt32()==1?ClassStats.WeaponStatPercent(row[2].GetInt32(),row[8].GetInt32(),75,75):row[2].GetInt32();
            var range=ClassStats.PhysicalEndpoints256(row[0].GetInt32(),row[1].GetInt32(),stat+row[3].GetInt32()+row[4].GetInt32());
            Require(range.Min+Math.Min(row[5].GetInt32(),range.Max-range.Min-1)==row[6].GetInt32(),"Physical endpoints differ from original ApplyDamageBonuses");
        }
        var definition=SkillMechanics.For(CombatAbility.Sacrifice);
        foreach(var (rank,cost,damage,attack) in new[]{(1,8,180,20),(20,2,465,153),(21,1,480,160),(99,1,1650,706)})
        {
            var values=new SkillValues(definition,_=>rank,_=>0,99);
            Require(values.Eval("calc2")==cost && values.Eval("calc1")==damage && values.Formula("toht")==attack && values.Mana256==0,"Pinned Sacrifice rank arithmetic");
        }
        Require(new SkillValues(definition,_=>20,_=>20,99).Eval("calc1")==865,"Hard Redemption/Fanaticism synergies");
    }
    private static void Strike(PlayerMobile owner,Ogre target,int input=0)
    {
        var values=SkillMechanics.Values(owner,CombatAbility.Sacrifice);var range=ClassStats.SkillPhysicalRange256(owner,(int)values.Eval("calc1"),0);
        int raw=range.Min,prepared=raw*50/100;Resist(target,50);
        int hp=Life(owner),enemy=Life(target);owner.Mana=0;NecromancerEffects.Profiles.Store(owner,new());
        Cast(owner,target,input);
        Require(enemy-Life(target)==prepared && hp-Life(owner)==PaladinSacrifice.Cost256(prepared,enemy,(int)values.Eval("calc2")),"Actual prepared damage/self cost or repeated mitigation");
        Require(NecromancerEffects.ManaBalance256(owner)==0,"Zero-mana Sacrifice charged resources");
        hp=Life(owner);enemy=Life(target);Resist(target,100);Cast(owner,target);
        Require(Life(owner)==hp && Life(target)==enemy,"Physical immunity still charged self cost");Resist(target,50);
    }
    private static void Miss(PlayerMobile owner,Ogre target)
    {
        int hp=Life(owner),enemy=Life(target),rolls=0;var hit=PaladinSacrifice.HitRoll;var roll=PaladinSacrifice.Roll;
        try{PaladinSacrifice.HitRoll=()=>1;PaladinSacrifice.Roll=n=>{rolls++;return 0;};Cast(owner,target);}
        finally{PaladinSacrifice.HitRoll=hit;PaladinSacrifice.Roll=roll;}
        Require(Life(owner)==hp && Life(target)==enemy && rolls==0,"Miss spent life or rolled physical damage");
    }
    private static void CapAndDeath(PlayerMobile owner,Ogre target)
    {
        var values=SkillMechanics.Values(owner,CombatAbility.Sacrifice);Resist(target,0);target.Hits=2;SkillHealth.Clear(target);
        SkillHealth.Damage(owner,target,17);int life=Life(target),hp=Life(owner);Cast(owner,target);
        Require(!target.Alive && hp-Life(owner)==PaladinSacrifice.Cost256(int.MaxValue,life,(int)values.Eval("calc2")),"Target FP life cap was ignored");
        var dying=ClassSkillChecks.Player(DiabloClass.Paladin);dying.MoveToWorld(new(owner.X,owner.Y+1,owner.Z),owner.Map);dying.Hits=1;SkillHealth.Clear(dying);Freeze(dying);
        Require(SkillBindings.Instance.Assign(dying,true,CombatAbility.Sacrifice),"Self-cost death attack binding");
        var enemy=new Ogre{NoKillAwards=true,Frozen=true,CantWalk=true};enemy.SetHits(20000);enemy.MoveToWorld(new(dying.X+1,dying.Y,dying.Z),owner.Map);enemy.AIObject.AITimer.Stop();Freeze(enemy);Resist(enemy,0);
        int before=Life(enemy);Cast(dying,enemy);
        Require(!dying.Alive && dying.Corpse is{Deleted:false} && Life(enemy)==before,"Self death did not abort target commitment");
        dying.Corpse?.Delete();dying.Delete();enemy.Delete();
    }
    private static void SoftRank(PlayerMobile owner,Ogre target)
    {
        while(ClassCreation.Profiles.GetRank(owner.Serial,"paladin.sacrifice")<20)
        {Require(ClassCreation.Profiles.TryTrain(owner.Serial,"paladin.sacrifice",out _),"Sacrifice rank20 training");}
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);
        Require(owner.EquipItem(helm),"Actual Sacrifice soft rank gear");
        Require(SkillMechanics.Values(owner,CombatAbility.Sacrifice).Rank==21 && SkillMechanics.Values(owner,CombatAbility.Sacrifice).Eval("calc2")==1,"Soft rank did not lower self-cost");
        Strike(owner,target,1);helm.Delete();
    }
    private static void AbsorbOrder(PlayerMobile owner)
    {
        var originalMap=owner.Map;var originalLocation=owner.Location;owner.MoveToWorld(originalLocation,Map.Felucca);
        var defender=ClassSkillChecks.Player(DiabloClass.Paladin);defender.MoveToWorld(new(owner.X+1,owner.Y+1,owner.Z),owner.Map);Freeze(defender);
        var definition=NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Raven Frost");
        var sourceRing=NamedGear.Create(definition.Id,99,new Random(173),true);
        var targetRing=NamedGear.Create(definition.Id,99,new Random(173),true);
        Require(owner.EquipItem(sourceRing) && defender.EquipItem(targetRing),"Actual cold/absorb order gear");
        defender.Hits=2;SkillHealth.Clear(defender);owner.Hits=owner.HitsMax-20;SkillHealth.Clear(owner);
        int hp=Life(owner),cap=Life(defender);var values=SkillMechanics.Values(owner,CombatAbility.Sacrifice);
        Require(GearEffects.Damage(owner,0,defender).Cold>0 && ClassGear.Bonus(defender,"abs-cold%")>0,"Absorb-order fixture lacks cold or absorb");
        // Delivery-only fixture, independent of friendly/PvP input authorization.
        var friendly=ClassCombatHooks.Friendly;
        try
        {
            // Permit only this delivery-only pair in the disposable fixture; gameplay remains cooperative.
            ClassCombatHooks.Friendly=(source,target)=>source==owner && target==defender?false:friendly?.Invoke(source,target)??false;
            PaladinSacrifice.Strike(owner,defender,values);
        }
        finally{ClassCombatHooks.Friendly=friendly;}
        Require(!defender.Alive && hp-Life(owner)==PaladinSacrifice.Cost256(int.MaxValue,cap,(int)values.Eval("calc2")),
            $"Absorb-order result: targetAlive={defender.Alive}, targetLife={Life(defender)}, selfLoss={hp-Life(owner)}, cap={cap}, pct={values.Eval("calc2")}");
        Require(ClassCombat.Friendly(owner,owner) && ClassCombat.Friendly(owner,defender),"Self-cost bypass escaped its internal scope");
        sourceRing.Delete();targetRing.Delete();defender.Corpse?.Delete();defender.Delete();
        owner.MoveToWorld(originalLocation,originalMap);
    }
    private static void Block(PlayerMobile owner)
    {
        var defender=ClassSkillChecks.Player(DiabloClass.Paladin);defender.RawStr=1000;defender.RawDex=10000;
        defender.MoveToWorld(new(owner.X+1,owner.Y+1,owner.Z),owner.Map);Freeze(defender);
        var shield=new WoodenKiteShield();ClassGear.Profiles.Convert(shield,1,GearRarity.Normal,new Random(173));Require(defender.EquipItem(shield),"Actual block shield");
        Require(ClassGear.BlockChance(defender)>0,"Block fixture has no block chance");
        int hp=Life(owner),enemy=Life(defender),rolls=0;var roll=PaladinSacrifice.Roll;
        try{PaladinSacrifice.Roll=n=>{rolls++;return 0;};PaladinSacrifice.Strike(owner,defender,SkillMechanics.Values(owner,CombatAbility.Sacrifice));}
        finally{PaladinSacrifice.Roll=roll;}
        Require(Life(owner)==hp && Life(defender)==enemy && rolls==0,"Blocked Sacrifice rolled damage or charged life");
        defender.Delete();
    }
    private static void Shield(PlayerMobile owner)
    {
        var mage=ClassSkillChecks.Player(DiabloClass.Sorceress);mage.MoveToWorld(new(owner.X+1,owner.Y+1,owner.Z),owner.Map);Freeze(mage);
        SorceressEffects.CastShield(mage,SkillMechanics.Values(mage,CombatAbility.EnergyShield));
        int hp=Life(owner),enemy=Life(mage),mana=NecromancerEffects.ManaBalance256(mage);
        var values=SkillMechanics.Values(owner,CombatAbility.Sacrifice);
        int raw=ClassStats.SkillPhysicalRange256(owner,(int)values.Eval("calc1"),0).Min;
        var friendly=ClassCombatHooks.Friendly;
        try
        {
            ClassCombatHooks.Friendly=(source,target)=>source==owner && target==mage?false:friendly?.Invoke(source,target)??false;
            PaladinSacrifice.Strike(owner,mage,values);
        }
        finally{ClassCombatHooks.Friendly=friendly;}
        int prepared=enemy-Life(mage);
        Require(prepared>0 && prepared<raw && NecromancerEffects.ManaBalance256(mage)<mana
            && hp-Life(owner)==PaladinSacrifice.Cost256(prepared,enemy,(int)values.Eval("calc2")),"Self cost used damage before Energy Shield");
        mage.Delete();
    }
    private static void UpperWeapon(PlayerMobile owner,Ogre enemy)
    {
        Resist(enemy,0);var values=SkillMechanics.Values(owner,CombatAbility.Sacrifice);
        int raw=ClassStats.SkillPhysicalRange256(owner,(int)values.Eval("calc1"),0).Max-1,hp=Life(owner),before=Life(enemy);
        var roll=PaladinSacrifice.Roll;
        try{PaladinSacrifice.Roll=n=>n-1;Cast(owner,enemy);}
        finally{PaladinSacrifice.Roll=roll;}
        int cost=PaladinSacrifice.Cost256(raw,before,(int)values.Eval("calc2"));
        Require(cost>=256 && before-Life(enemy)==raw && hp-Life(owner)==cost && ClassCombat.Friendly(owner,owner),
            "Upper endpoint included maximum, whole-point self cost was blocked, or bypass leaked");
        Resist(enemy,50);
    }
    private static void Gates(PlayerMobile owner,Ogre target)
    {
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.Sacrifice) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Sacrifice)
            && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.Sacrifice),"Sacrifice mouse/quick bindings");
        var timing=CombatTiming.For(owner,CombatAbility.Sacrifice);
        Require(timing==CombatTiming.For(owner,CombatAbility.BasicAttack) && CombatTiming.Range(owner,CombatAbility.Sacrifice)==CombatTiming.Range(owner,CombatAbility.BasicAttack),"Sacrifice used casting timing/range");
        var where=target.Location;target.MoveToWorld(new(owner.X+8,owner.Y,owner.Z),owner.Map);Ready(owner);
        Require(!ClassCombat.Perform(owner,CombatAbility.Sacrifice,target,target.Location,Core.TickCount),"Out-of-range Sacrifice started");target.MoveToWorld(where,owner.Map);
        var bow=new Bow();ClassGear.Profiles.Convert(bow,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(bow),"Bow gate fixture");Ready(owner);
        Require(!ClassCombat.Perform(owner,CombatAbility.Sacrifice,target,target.Location,Core.TickCount),"Sacrifice accepted a bow");bow.Delete();
        Ready(owner);int hp=Life(owner),enemy=Life(target);
        Require(ClassCombat.ContinueAttack(owner,CombatAbility.Sacrifice,target,target.Location,Core.TickCount),"Sacrifice windup");ClassCombat.ReleaseCast(owner);
        Require(!ClassCombat.ResolveAction(owner,ClassCombat.For(owner).EffectAt-1) && Life(owner)==hp && Life(target)==enemy,"Sacrifice resolved before its melee event");
        var sword=new Longsword();ClassGear.Profiles.Convert(sword,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(sword),"Weapon-swap fixture");
        Require(!ClassCombat.ResolveAction(owner,ClassCombat.For(owner).EffectAt) && Life(owner)==hp && Life(target)==enemy,"Weapon swap committed Sacrifice");sword.Delete();
    }
    private static void Check()=>Controlled(()=>
    {
        Probe();var owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.AddItem(new Backpack());
        for(int i=0;i<50;i++){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength) && ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Legitimate melee stat allocation");}
        owner.MoveToWorld(ClassSkillChecks.Corridor(owner,5750,1800),owner.Map);Freeze(owner);
        var target=new Ogre{NoKillAwards=true,Frozen=true,CantWalk=true};target.SetHits(30000);target.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);target.AIObject.AITimer.Stop();Freeze(target);
        Gates(owner,target);Strike(owner,target);Miss(owner,target);SoftRank(owner,target);Block(owner);Shield(owner);AbsorbOrder(owner);CapAndDeath(owner,target);target.Corpse?.Delete();target.Delete();
        target=new Ogre{NoKillAwards=true,Frozen=true,CantWalk=true};target.SetHits(30000);target.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);target.AIObject.AITimer.Stop();Freeze(target);Resist(target,50);
        var weapon=new Longsword();ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(weapon),"Actual Sacrifice melee weapon");Strike(owner,target,2);UpperWeapon(owner,target);
        owner.Hits=owner.HitsMax-20;SkillHealth.Clear(owner);SkillHealth.Damage(owner,owner,17);owner.Mana=owner.ManaMax;NecromancerEffects.Profiles.Store(owner,new());
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,target.Serial.Value,weapon.Serial.Value,owner.Location,owner.Map.MapID,Life(owner),Life(target),NecromancerEffects.ManaBalance256(owner))));
    });
    private static void Reload()=>Controlled(()=>
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);Freeze(owner);
        var enemy=(Ogre)World.FindMobile((Serial)saved.Enemy);enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Stop(enemy);Freeze(enemy);Resist(enemy,50);
        Require(Life(owner)==saved.OwnerLife && Life(enemy)==saved.EnemyLife && NecromancerEffects.ManaBalance256(owner)==saved.Mana && CombatTiming.Equipped(owner)?.Serial.Value==saved.Weapon,
            $"Sacrifice persistence: owner={Life(owner)}/{saved.OwnerLife}, enemy={Life(enemy)}/{saved.EnemyLife}, mana={NecromancerEffects.ManaBalance256(owner)}/{saved.Mana}, weapon={CombatTiming.Equipped(owner)?.Serial.Value}/{saved.Weapon}");
        Require(ClassCreation.Profiles.GetRank(owner.Serial,"paladin.sacrifice")==20 && SkillBindings.Instance.Get(owner).Left==CombatAbility.Sacrifice
            && SkillBindings.Instance.Get(owner).Right==CombatAbility.Sacrifice,"Sacrifice ranks/bindings persistence");
        Strike(owner,enemy);UpperWeapon(owner,enemy);Miss(owner,enemy);owner.Hits=owner.HitsMax-20;SkillHealth.Clear(owner);SkillHealth.Damage(owner,owner,17);owner.Mana=owner.ManaMax;NecromancerEffects.Profiles.Store(owner,new());
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{OwnerLife=Life(owner),EnemyLife=Life(enemy),Mana=NecromancerEffects.ManaBalance256(owner)}));
    });
}
