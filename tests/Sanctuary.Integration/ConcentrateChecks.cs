// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class ConcentrateChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ConcentrateChecks));
    private sealed record Saved(uint Owner,uint Enemy,uint Weapon,uint Helm,int Mana,int Life,int Defense,Point3D Origin,int Map);
    private static bool _creating;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"concentrate.json");
    private sealed class Rolls(bool critical=false):Random
    {private int _calls;public override int Next(int max)=>critical && ++_calls==1?0:max-1;}
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static int Life(Mobile actor)=>actor.Hits*256-SkillHealth.Remainder256(actor);
    public static void Configure()
    {
        FreezeManaRate();
        Register("concentrate-check",()=>Controlled(Check));Register("concentrate-reload-check",()=>Controlled(Reload));Register("concentrate-disconnect-check",Disconnected);
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest) && World.FindMobile((Serial)JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)).Enemy) is BaseCreature enemy){Stop(enemy);}};
    }
    [CallPriority(int.MaxValue)]
    public static void Initialize()=>FreezeManaRate();
    private static void FreezeManaRate()
    {
        // Configure covers load; Initialize follows the production class recovery handler.
        // Constructor scope holds a native timer created before the fixture name is assigned.
        var prior=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=actor=>actor is PlayerMobile && (_creating || actor.Name=="ConcentrateFixture")?TimeSpan.FromHours(1):prior?.Invoke(actor)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Concentrate resource, damage, animation-state and restart checks",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Controlled(Action action)
    {
        var hit=BarbarianMelee.HitRoll;var element=BarbarianMelee.ElementRoll;var physical=BarbarianMelee.PhysicalRoll;var effects=BarbarianMelee.Effects;
        try{BarbarianMelee.HitRoll=()=>0;BarbarianMelee.ElementRoll=()=>0;BarbarianMelee.PhysicalRoll=_=>0;BarbarianMelee.Effects=new Rolls();action();}
        finally{BarbarianMelee.HitRoll=hit;BarbarianMelee.ElementRoll=element;BarbarianMelee.PhysicalRoll=physical;BarbarianMelee.Effects=effects;}
    }
    private static void Stop(BaseCreature target){target.Frozen=target.CantWalk=true;target.Combatant=null;target.AIObject?.AITimer.Stop();}
    private static void Freeze(Mobile actor)
    {
        TimedEffects.Poison(actor,actor,.000001,60000,TimedEffects.Now);
        TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==actor.Serial.Value?x with{UpdatedUtc=TimedEffects.Now.AddMinutes(10)}:x).ToArray());
    }
    private static void Ready(PlayerMobile owner){BarbarianConcentrate.Cancel(owner);ClassCombat.Stop(owner);ClassCombat.For(owner).NextAction=ClassCombat.For(owner).RecoveryUntil=Core.TickCount;}
    private static void Mana(PlayerMobile owner){owner.Mana=owner.ManaMax;NecromancerEffects.Profiles.Store(owner,new());}
    private static void TrainId(PlayerMobile owner,string id,int rank)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Barbarian,id,out var talent),"Missing talent "+id);
        foreach(string prerequisite in talent.Prerequisites){TrainId(owner,prerequisite,1);}
        while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out var why),"Cannot train "+id+": "+why);}
    }
    private static void Train(PlayerMobile owner,CombatAbility ability,int rank)=>TrainId(owner,SkillMechanics.For(ability).TalentId,rank);
    private static Item Equip(PlayerMobile owner,Item item,bool named=false)
    {
        if(item is BaseWeapon && CombatTiming.Equipped(owner) is{} old){owner.AddToBackpack(old);}
        if(!named){ClassGear.Profiles.Convert(item,1,GearRarity.Normal,new Random(173));}
        Require(owner.EquipItem(item),"Concentrate equipment "+item.GetType().Name);ClassGear.Refresh(owner);return item;
    }
    private static Item Named(PlayerMobile owner,string key)=>Equip(owner,NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(173),true),true);
    private static void Attach(PlayerMobile owner)
    {
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));
        network.Seeded=true;network.CompressionEnabled=false;network.Mobile=owner;owner.NetState=network;
        // A real disposable TCP connection keeps the ordinary 40ms class loop active.
        // Only account/character selection is fixture-bootstrapped; no client acceptance is claimed.
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Require(protocol!=null,"Connected Concentrate protocol bootstrap changed");
        protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(owner);
    }
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.Concentrate);
        foreach(var x in new (int Rank,int Hard,int Damage,int Ar,int Defense,int Magic)[]{(1,0,70,60,100,0),(20,20,465,250,290,20),(21,20,470,260,300,20)})
        {
            var values=new SkillValues(row,key=>key==row.SourceKey?x.Rank:0,_=>x.Hard,99);
            Require(values.Mana256==512 && values.Eval("calc1")==x.Damage && values.Formula("toht")==x.Ar && values.Eval("aurastatcalc1")==x.Defense && values.Eval("calc4")==x.Magic,"Pinned Concentrate goldens");
        }
        foreach(int physical in new[]{0,1,255,256,257,99999})
        foreach(int percent in new[]{0,1,20,99,100})
        {var split=BarbarianConcentrate.Split(physical,percent);Require(split.Magic256==(long)physical*percent/100 && split.Physical256+split.Magic256==physical,"FP256 conversion conservation/order");}
    }
    internal static void Rating(PlayerMobile owner,CombatAbility ability)
    {
        var enemy=new Ogre{NoKillAwards=true,RawDex=40000};enemy.SetHits(30000);enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Stop(enemy);
        var previous=BarbarianMelee.HitRoll;
        try
        {
            Ready(owner);Mana(owner);int bonus=(int)SkillMechanics.Values(owner,ability).Formula("toht");int defense=NamedCombat.DefenseFor(owner,enemy,ClassStats.Defense(enemy));
            double plain=ClassStats.HitChance(ClassStats.AttackRatingAgainst(owner,enemy,0),defense,ClassStats.Level(owner),ClassStats.Level(enemy));
            double enhanced=ClassStats.HitChance(ClassStats.AttackRatingAgainst(owner,enemy,bonus),defense,ClassStats.Level(owner),ClassStats.Level(enemy));
            Require(enhanced>plain+.01,"AR boundary fixture did not distinguish skill bonus");double boundary=(plain+enhanced)/2;
            Require(ClassStats.CheckHit(owner,enemy,0,()=>boundary)==false && ClassStats.CheckHit(owner,enemy,bonus,()=>boundary)==true,"Shared actual skill AR boundary");
            BarbarianMelee.HitRoll=()=>boundary;Require(ClassCombat.Perform(owner,ability,enemy,enemy.Location,Core.TickCount) && ClassCombat.For(owner).BarbarianAttack.Hit,"Prepared attack skipped linear source AR");Ready(owner);
        }
        finally{BarbarianMelee.HitRoll=previous;Ready(owner);enemy.Delete();}
    }
    private static void Damage(PlayerMobile owner,Skeleton enemy)
    {
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.Concentrate) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Concentrate) && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.Concentrate),"Concentrate L/R/quick binding");
        foreach(int input in new[]{0,1,2})
        foreach(bool critical in new[]{false,true})
        {
            Ready(owner);Mana(owner);enemy.SetResistance(ResistanceType.Physical,100);BarbarianMelee.Effects=new Rolls(critical);
            int hp=Life(enemy),balance=NecromancerEffects.ManaBalance256(owner);var values=SkillMechanics.Values(owner,CombatAbility.Concentrate);
            var raw=GearEffects.Damage(owner,0,enemy,(int)values.Eval("calc1"),_=>0);
            Require(input==0?ClassCombat.ContinueAttack(owner,CombatAbility.Concentrate,enemy,enemy.Location,Core.TickCount):input==1?ClassCombat.ContinueCast(owner,CombatAbility.Concentrate,enemy,enemy.Location,Core.TickCount):ClassCombat.Perform(owner,CombatAbility.Concentrate,enemy,enemy.Location,Core.TickCount),"Concentrate input begin");
            var state=ClassCombat.For(owner);var packet=state.BarbarianAttack;
            var split=BarbarianConcentrate.Split(NamedElements.To256(raw.Physical*(critical?2:1)),(int)values.Eval("calc4"));
            Require(packet.Hit && packet.Impact.Parts.Physical==0 && NamedElements.To256(packet.Impact.Parts.Magic)==split.Magic256 && split.Magic256>0,"Critical/conversion/physical immunity order");
            Require(Life(enemy)==hp && balance-NecromancerEffects.ManaBalance256(owner)==512 && BarbarianConcentrate.Defense(owner)==values.Eval("aurastatcalc1"),"Start damage/mana/defense");
            ClassCombat.Stop(owner);Require(state.PendingAbility==CombatAbility.Concentrate && state.BarbarianAttack==packet,"Stop cancelled uninterrupted committed attack");
            Require(!ClassCombat.ResolveAction(owner,state.EffectAt-1) && ClassCombat.ResolveAction(owner,state.EffectAt) && hp-Life(enemy)==packet.Impact.LifeDamage256 && NecromancerEffects.ManaBalance256(owner)==balance-512,"Impact timing/damage/one payment");
            Require(BarbarianConcentrate.Defense(owner)>0 && !ClassCombat.ResolveAction(owner,state.EffectAt),"Defense ended at impact or packet replayed");TimedEffects.ClearGear(enemy);
        }
        Ready(owner);Mana(owner);BarbarianMelee.HitRoll=()=>1;int life=Life(enemy),mana=NecromancerEffects.ManaBalance256(owner);
        Require(ClassCombat.Perform(owner,CombatAbility.Concentrate,enemy,enemy.Location,Core.TickCount),"Miss start");var miss=ClassCombat.For(owner);
        Require(!miss.BarbarianAttack.Hit && BarbarianConcentrate.Defense(owner)>0 && ClassCombat.ResolveAction(owner,miss.EffectAt) && Life(enemy)==life && mana-NecromancerEffects.ManaBalance256(owner)==512,"Miss damage/payment/defense");BarbarianMelee.HitRoll=()=>0;
        enemy.SetResistance(ResistanceType.Physical,0);
        // Explicit resolver resistance override: current live creature templates do not offer nonzero magic resistance.
        Require(NamedElements.Resolve256(owner,enemy,100*256,"mag",50,enhance:false).Damage256==50*256,"Converted magic resolver resistance");Ready(owner);
    }
    private static void Gates(PlayerMobile owner,Skeleton enemy,Item weapon)
    {
        Ready(owner);owner.Mana=1;NecromancerEffects.Profiles.Store(owner,new());
        Require(!ClassCombat.Perform(owner,CombatAbility.Concentrate,enemy,enemy.Location,Core.TickCount) && BarbarianConcentrate.Defense(owner)==0,"Unpaid Concentrate state");Mana(owner);
        var point=enemy.Location;enemy.MoveToWorld(new(owner.X+6,owner.Y,owner.Z),owner.Map);
        Require(!ClassCombat.Perform(owner,CombatAbility.Concentrate,enemy,enemy.Location,Core.TickCount),"Concentrate spell reach");enemy.MoveToWorld(point,owner.Map);
        Require(!ClassCombat.Perform(owner,CombatAbility.Concentrate,null,owner.Location,Core.TickCount),"Unverified ground acquisition enabled");
        var timing=CombatTiming.Weapon(CombatTiming.Equipped(owner));var plain=CombatTiming.Calculate(DiabloClass.Barbarian,CombatAbility.Concentrate,timing);
        Require(plain==CombatTiming.Calculate(DiabloClass.Barbarian,CombatAbility.BasicAttack,timing) && plain==CombatTiming.Calculate(DiabloClass.Barbarian,CombatAbility.Concentrate,timing,fcr:100) && CombatTiming.Calculate(DiabloClass.Barbarian,CombatAbility.Concentrate,timing,ias:100).Frames<plain.Frames,"Concentrate A1/IAS/FCR timing");
        foreach(int cancel in new[]{0,1,2})
        {
            Ready(owner);Mana(owner);int life=Life(enemy);var origin=owner.Location;
            Require(ClassCombat.Perform(owner,CombatAbility.Concentrate,enemy,enemy.Location,Core.TickCount),"Cancellation start");var state=ClassCombat.For(owner);long deadline=state.NextAction;int paid=NecromancerEffects.ManaBalance256(owner);
            Require(!HitRecovery.Apply(owner,owner.HitsMax,Core.TickCount) && state.RecoveryUntil<=Core.TickCount,"Concentrate get-hit interruption");
            owner.Direction=Direction.West;Require(!owner.Move(Direction.West) && owner.Location==origin && state.PendingAbility==CombatAbility.Concentrate,"Voluntary movement interrupted Concentrate");
            if(cancel==0){owner.Frozen=true;}else if(cancel==1){owner.MoveToWorld(new(origin.X,origin.Y+1,origin.Z),owner.Map);}else{owner.AddToBackpack(weapon);}
            Require(!ClassCombat.ResolveAction(owner,state.EffectAt) && state.Concentrate==null && BarbarianConcentrate.Defense(owner)==0 && state.BarbarianAttack==null && state.NextAction==deadline && Life(enemy)==life && NecromancerEffects.ManaBalance256(owner)==paid,"Invalid owner retained state/damage/refund");
            owner.Frozen=false;owner.MoveToWorld(origin,owner.Map);if(weapon.Parent!=owner){Require(owner.EquipItem(weapon),"Cancelled weapon restore");}ClassGear.Refresh(owner);
        }
        Ready(owner);var bow=Equip(owner,new Bow());Require(!ClassCombat.Perform(owner,CombatAbility.Concentrate,enemy,enemy.Location,Core.TickCount),"Concentrate accepted bow");bow.Delete();Require(owner.EquipItem(weapon),"Bow restore");ClassGear.Refresh(owner);Ready(owner);
    }
    private static void Lifecycle(PlayerMobile owner,Skeleton enemy,Item helm)
    {
        Ready(owner);Mana(owner);Require(ClassCombat.Perform(owner,CombatAbility.Concentrate,enemy,enemy.Location,Core.TickCount),"Rank snapshot start");var state=ClassCombat.For(owner);
        Require(BarbarianConcentrate.Defense(owner)==300,"Start soft defense snapshot");owner.AddToBackpack(helm);ClassGear.Refresh(owner);
        Require(BarbarianConcentrate.Defense(owner)==300 && ClassCombat.ResolveAction(owner,state.EffectAt) && BarbarianConcentrate.Defense(owner)==290,"Start/impact defense rank refresh");
        Require(owner.EquipItem(helm),"Soft helm restore");ClassGear.Refresh(owner);Require(BarbarianConcentrate.Defense(owner)==290,"Recovery invented another defense refresh");Ready(owner);
        Mana(owner);int hp=Life(enemy);Require(ClassCombat.Perform(owner,CombatAbility.Concentrate,enemy,enemy.Location,Core.TickCount),"Late tick start");state=ClassCombat.For(owner);var packet=state.BarbarianAttack;
        ClassCombat.UpdateCombat(owner,state.NextAction+500);Require(hp-Life(enemy)==packet.Impact.LifeDamage256 && state.Concentrate==null && state.BarbarianAttack==null,"Delayed tick lost due impact or retained defense");Ready(owner);
        var enemyOrigin=enemy.Location;
        foreach(int transition in new[]{0,1,2})
        {
            var actor=new PlayerMobile{Body=0x190,Name="ConcentrateFixture",Player=true};actor.AddItem(new Backpack());ClassCreation.Profiles.Assign(actor,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(actor,99);
            FixtureSpace.Place(actor,new(owner.X,owner.Y+3,owner.Z),owner.Map);Train(actor,CombatAbility.Concentrate,1);actor.Hits=actor.HitsMax;Mana(actor);enemy.MoveToWorld(new(actor.X+1,actor.Y,actor.Z),actor.Map);hp=Life(enemy);
            Require(ClassCombat.Perform(actor,CombatAbility.Concentrate,enemy,enemy.Location,Core.TickCount),"Owner transition start");var active=ClassCombat.For(actor);long at=active.EffectAt;
            if(transition==0){actor.Kill();Require(!actor.Alive,"Concentrate death fixture survived");}
            else if(transition==1){Require(!ClassCreation.Profiles.TryActivateHistory(actor,DiabloClass.Paladin),"Active Concentrate bypassed history gate");ClassCreation.Profiles.SelectHistory(actor,DiabloClass.Paladin);ClassCreation.Profiles.ApplyActive(actor);} // Explicit fixture-forced profile loss after the real transaction correctly rejects an active swing.
            else{ClassCreation.Profiles.TryGet(actor.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(actor,profile);}
            Require(!ClassCombat.ResolveAction(actor,at) && BarbarianConcentrate.Defense(actor)==0 && ClassCombat.For(actor).BarbarianAttack==null && Life(enemy)==hp,"Owner death/history/respec retained active damage or defense");actor.Corpse?.Delete();actor.Delete();enemy.MoveToWorld(enemyOrigin,owner.Map);
        }
    }
    private static void Disconnected()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(owner.NetState==null && ClassCombat.Existing(owner)?.Concentrate==null && BarbarianConcentrate.Defense(owner)==0,"Actual disconnect retained active defense/state");
    }
    private static void Seed(PlayerMobile owner,Skeleton enemy,Item weapon,Item helm)
    {
        Ready(owner);Mana(owner);Stop(enemy);Freeze(owner);Freeze(enemy);int defense=ClassStats.Defense(owner),hp=Life(enemy);
        Require(ClassCombat.Perform(owner,CombatAbility.Concentrate,enemy,enemy.Location,Core.TickCount),"Saved paid Concentrate start");ClassCombat.ReleaseCast(owner);
        var state=ClassCombat.For(owner);state.EffectAt=Core.TickCount+600000;state.NextAction=state.EffectAt+1000;
        state.Concentrate=state.Concentrate with{Until=state.NextAction};state.BarbarianAttack=state.BarbarianAttack with{EffectAt=state.EffectAt}; // Fixture-only save inspection hold, after normal-clock expiry passed.
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,enemy.Serial.Value,weapon.Serial.Value,helm.Serial.Value,NecromancerEffects.ManaBalance256(owner),hp,defense,owner.Location,owner.Map.MapID)));
    }
    private static void Natural(PlayerMobile owner,Skeleton enemy,Item weapon,Item helm)
    {
        Ready(owner);Mana(owner);int before=ClassStats.Defense(owner),hp=Life(enemy),mana=NecromancerEffects.ManaBalance256(owner);
        Require(ClassCombat.Perform(owner,CombatAbility.Concentrate,enemy,enemy.Location,Core.TickCount),"Connected Concentrate start");ClassCombat.ReleaseCast(owner);var state=ClassCombat.For(owner);var prepared=state.BarbarianAttack;long end=state.NextAction;
        Require(ClassStats.Defense(owner)>before && state.Concentrate!=null && state.EffectAt<end,"Connected start defense/deadlines");
        // Rebinding releases held input; a committed noninterruptible swing must finish.
        Require(SkillBindings.Instance.Assign(owner,false,CombatAbility.Bash) && state.PendingAbility==CombatAbility.Concentrate,"Rebinding interrupted committed swing");
        int afterImpact=(int)Math.Max(1,state.EffectAt-Core.TickCount+45);
        Timer.StartTimer(TimeSpan.FromMilliseconds(afterImpact),()=>
        {
            try
            {
                Require(state.PendingAbility==CombatAbility.None && state.BarbarianAttack==null && hp-Life(enemy)==prepared.Impact.LifeDamage256 && mana-NecromancerEffects.ManaBalance256(owner)==512,"Natural loop impact/payment");
                Require(Core.TickCount<end && BarbarianConcentrate.Defense(owner)>0 && ClassStats.Defense(owner)>before,"Defense lost during recovery");
                Timer.StartTimer(TimeSpan.FromMilliseconds(Math.Max(1,end-Core.TickCount+100)),()=>Controlled(()=>
                {
                    try
                    {
                        Require(state.Concentrate==null && BarbarianConcentrate.Defense(owner)==0 && ClassStats.Defense(owner)==before,"Natural animation-end defense removal");
                        Require(SkillBindings.Instance.Assign(owner,false,CombatAbility.Concentrate),"Restore right Concentrate");
                        Seed(owner,enemy,weapon,helm);Log.Information("concentrate-result: passed");
                    }
                    catch(Exception e){Log.Error(e,"concentrate-result: failed");}
                }));
            }
            catch(Exception e){Log.Error(e,"concentrate-result: failed");}
        });
    }
    private static void Check()
    {
        Goldens();PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Name="ConcentrateFixture",Player=true};}finally{_creating=false;}owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(owner,99);Require(Mobile.ManaRegenRateHandler(owner)==TimeSpan.FromHours(1),"Concentrate fixture mana handler order");
        FixtureSpace.Place(owner,new(6060,1720,0),Map.Felucca);owner.MoveToWorld(ClassSkillChecks.Corridor(owner,6060,1720),Map.Felucca); // Fresh actor: allocate only prerequisites and tested skills within98 points.
        foreach(var ability in new[]{CombatAbility.Bash,CombatAbility.BattleOrders,CombatAbility.Berserk,CombatAbility.Concentrate}){Train(owner,ability,20);}Train(owner,CombatAbility.SwordMastery,1);Train(owner,CombatAbility.IronSkin,1);
        while(owner.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Concentrate Strength");}while(owner.RawDex<200){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Concentrate Dexterity");}
        owner.Hits=owner.HitsMax;var weapon=Equip(owner,new Longsword());var enemy=new Skeleton{NoKillAwards=true};enemy.SetHits(30000);enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Stop(enemy);enemy.SetResistance(ResistanceType.Fire,0);Freeze(owner);Freeze(enemy);
        Gates(owner,enemy,weapon);Rating(owner,CombatAbility.Concentrate);Damage(owner,enemy);var helm=Named(owner,"Tarnhelm");
        var values=SkillMechanics.Values(owner,CombatAbility.Concentrate);Require(values.Rank==21 && values.Eval("calc1")==470 && values.Eval("calc4")==20 && values.Eval("aurastatcalc1")==300,"Real soft rank/hard synergies");
        var sword=Named(owner,"Hellplague");Ready(owner);Mana(owner);BarbarianMelee.Effects=new Rolls(true);var raw=GearEffects.Damage(owner,0,enemy,470,_=>0);
        Require(ClassCombat.Perform(owner,CombatAbility.Concentrate,enemy,enemy.Location,Core.TickCount),"Named Concentrate");var packet=ClassCombat.For(owner).BarbarianAttack;var split=BarbarianConcentrate.Split(NamedElements.To256(2*raw.Physical),20);
        Require(raw.Fire>0 && packet.Impact.Parts.Fire==raw.Fire && NamedElements.To256(packet.Impact.Parts.Physical)==split.Physical256 && NamedElements.To256(packet.Impact.Parts.Magic)==split.Magic256,$"Named elements/critical conversion order raw={raw}, parts={packet.Impact.Parts}, split={split}, rank={SkillMechanics.Rank(owner,CombatAbility.Concentrate)}");Ready(owner);sword.Delete();Require(owner.EquipItem(weapon),"Named restore");ClassGear.Refresh(owner);
        Damage(owner,enemy);Lifecycle(owner,enemy,helm);Attach(owner);Natural(owner,enemy,weapon,helm);
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var enemy=(Skeleton)World.FindMobile((Serial)saved.Enemy);owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);Stop(enemy);Freeze(owner);Freeze(enemy);
        Require(ClassCombat.Existing(owner)?.Concentrate==null && ClassCombat.For(owner).BarbarianAttack==null && ClassCombat.For(owner).PendingAbility==CombatAbility.None && ClassStats.Defense(owner)==saved.Defense && Life(enemy)==saved.Life && NecromancerEffects.ManaBalance256(owner)==saved.Mana,"Paid action/defense replayed or changed after restart");
        Require(SkillMechanics.Rank(owner,CombatAbility.Concentrate)==21 && SkillBindings.Instance.Get(owner).Left==CombatAbility.Concentrate && SkillBindings.Instance.Get(owner).Right==CombatAbility.Concentrate && CombatTiming.Equipped(owner)?.Serial==(Serial)saved.Weapon,"Concentrate gear/ranks/bindings restart");Rating(owner,CombatAbility.Concentrate);Damage(owner,enemy);Attach(owner);Natural(owner,enemy,World.FindItem((Serial)saved.Weapon),World.FindItem((Serial)saved.Helm));
    }
}
