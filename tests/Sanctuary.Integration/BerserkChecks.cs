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

public static class BerserkChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BerserkChecks));
    private sealed record Saved(uint Owner,uint Enemy,uint Weapon,uint Helm,uint Expired,int Mana,int Life,int Defense,BerserkState State,Point3D Origin,int Map);
    private static bool _creating;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"berserk.json");
    private sealed class Rolls(bool critical=false):Random
    {private int _calls;public override int Next(int max)=>critical && ++_calls==1?0:max-1;}
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static int Life(Mobile actor)=>actor.Hits*256-SkillHealth.Remainder256(actor);
    public static void Configure()
    {
        FreezeManaRate();
        Register("berserk-check",()=>Controlled(Check));Register("berserk-reload-check",()=>Controlled(Reload));Register("berserk-disconnect-check",Disconnected);
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest) && World.FindMobile((Serial)JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)).Enemy) is BaseCreature enemy){Stop(enemy);}};
    }
    [CallPriority(int.MaxValue)]
    public static void Initialize()=>FreezeManaRate();
    private static void FreezeManaRate()
    {
        // Configure covers load; Initialize follows the production class recovery handler.
        // Constructor scope holds a native timer created before the fixture name is assigned.
        var prior=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=actor=>actor is PlayerMobile && (_creating || actor.Name=="BerserkFixture")?TimeSpan.FromHours(1):prior?.Invoke(actor)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Berserk full conversion, timed/temporary defense, inputs and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Later(int ms,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(Math.Max(1,ms)),()=>Controlled(()=>{try{action();}catch(Exception e){Log.Error(e,"berserk-result: failed");}}));
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
        TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==actor.Serial.Value?x with{UpdatedUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}:x).ToArray());
    }
    private static void Ready(PlayerMobile owner){BarbarianConcentrate.Cancel(owner);ClassCombat.Stop(owner);ClassCombat.For(owner).NextAction=ClassCombat.For(owner).RecoveryUntil=Core.TickCount;}
    private static void Clean(PlayerMobile owner){Ready(owner);BarbarianBerserk.Clear(owner);}
    private static void Mana(PlayerMobile owner){owner.Mana=owner.ManaMax;NecromancerEffects.Profiles.Store(owner,new());}
    private static void Replace(BerserkState state)=>BarbarianBerserk.Restore(BarbarianBerserk.Snapshot().Where(x=>x.Owner!=state.Owner).Append(state).ToArray());
    private static void TrainId(PlayerMobile owner,string id,int rank)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Barbarian,id,out var talent),"Missing talent "+id);foreach(string prior in talent.Prerequisites){TrainId(owner,prior,1);}
        while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out var why),"Cannot train "+id+": "+why);}
    }
    private static void Train(PlayerMobile owner,CombatAbility ability,int rank)=>TrainId(owner,SkillMechanics.For(ability).TalentId,rank);
    private static Item Equip(PlayerMobile owner,Item item,bool named=false)
    {
        if(item is BaseWeapon && CombatTiming.Equipped(owner) is{} old){owner.AddToBackpack(old);}if(!named){ClassGear.Profiles.Convert(item,1,GearRarity.Normal,new Random(173));}
        Require(owner.EquipItem(item),"Berserk equipment "+item.GetType().Name);ClassGear.Refresh(owner);return item;
    }
    private static Item Named(PlayerMobile owner,string key)=>Equip(owner,NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(173),true),true);
    private static void Attach(PlayerMobile owner)
    {
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=owner;owner.NetState=network;
        // Explicit account/protocol bootstrap; natural class timer and owning TCP connection are real.
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Require(protocol!=null,"Berserk network bootstrap changed");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(owner);
    }
    private static PlayerMobile Fresh(PlayerMobile reference=null)
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Name="BerserkFixture",Player=true};}finally{_creating=false;}Require(Mobile.ManaRegenRateHandler(owner)==TimeSpan.FromHours(1),"Berserk fixture mana handler order");owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(owner,99);
        FixtureSpace.Place(owner,reference==null?new(6070,1750,0):new(reference.X,reference.Y+3,reference.Z),reference?.Map??Map.Felucca);
        if(reference==null){owner.MoveToWorld(ClassSkillChecks.Corridor(owner,6070,1750),owner.Map);}return owner;
    }
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.Berserk);
        foreach(var x in new (int Rank,int Hard,int Damage,int Ar,int Frames)[]{(1,0,150,100,68),(20,20,835,385,33),(21,20,850,400,33)})
        {
            var values=new SkillValues(row,key=>key==row.SourceKey?x.Rank:0,_=>x.Hard,99);
            Require(values.Mana256==1024 && values.Eval("calc1")==x.Damage && values.Formula("toht")==x.Ar && values.Eval("calc2")==x.Frames && values.Eval("calc4")==100 && values.Eval("aurastatcalc1")==0 && values.Eval("aurastatcalc2")==-100,"Pinned Berserk goldens");
        }
        Require(BarbarianBerserk.ApplyOverride(12500,-100)==0 && BarbarianBerserk.ApplyOverride(12501,-50)==6251,"Final override integer order");
    }
    private static void Start(PlayerMobile owner,BaseCreature enemy,int input=2)
    {
        Ready(owner);Mana(owner);Require(input==0?ClassCombat.ContinueAttack(owner,CombatAbility.Berserk,enemy,enemy.Location,Core.TickCount):input==1?ClassCombat.ContinueCast(owner,CombatAbility.Berserk,enemy,enemy.Location,Core.TickCount):ClassCombat.Perform(owner,CombatAbility.Berserk,enemy,enemy.Location,Core.TickCount),"Berserk input start");ClassCombat.ReleaseCast(owner);
    }
    private static void Damage(PlayerMobile owner,Skeleton enemy)
    {
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.Berserk) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Berserk) && SkillBindings.Instance.AssignQuick(owner,1,CombatAbility.Berserk),"Berserk L/R/quick bindings");
        foreach(int input in new[]{0,1,2})foreach(bool critical in new[]{false,true})
        {
            Clean(owner);enemy.SetResistance(ResistanceType.Physical,100);BarbarianMelee.Effects=new Rolls(critical);int hp=Life(enemy);var values=SkillMechanics.Values(owner,CombatAbility.Berserk);var raw=GearEffects.Damage(owner,0,enemy,(int)values.Eval("calc1"),_=>0);Start(owner,enemy,input);
            var state=ClassCombat.For(owner);var packet=state.BarbarianAttack;var debuff=BarbarianBerserk.State(owner);
            Require(packet.Hit && packet.Impact.Parts.Physical==0 && NamedElements.To256(packet.Impact.Parts.Magic)==NamedElements.To256(raw.Physical*(critical?2:1)) && Life(enemy)==hp && ClassStats.Defense(owner)==0,"Full conversion/critical/immunity/start timing");
            Require(owner.ManaMax*256-NecromancerEffects.ManaBalance256(owner)==1024 && !ClassCombat.ResolveAction(owner,state.EffectAt-1) && ClassCombat.ResolveAction(owner,state.EffectAt) && hp-Life(enemy)==packet.Impact.LifeDamage256,"Impact/payment/order");
            Require(BarbarianBerserk.State(owner)==debuff && !ClassCombat.ResolveAction(owner,state.EffectAt) && owner.ManaMax*256-NecromancerEffects.ManaBalance256(owner)==1024,"Do2 extended penalty/replayed/paid again");TimedEffects.ClearGear(enemy);
        }
        Clean(owner);BarbarianMelee.HitRoll=()=>1;int life=Life(enemy);Start(owner,enemy);var miss=ClassCombat.For(owner);
        Require(!miss.BarbarianAttack.Hit && ClassStats.Defense(owner)==0 && ClassCombat.ResolveAction(owner,miss.EffectAt) && Life(enemy)==life && owner.ManaMax*256-NecromancerEffects.ManaBalance256(owner)==1024,"Miss penalty/payment/damage");BarbarianMelee.HitRoll=()=>0;
        Require(NamedElements.Resolve256(owner,enemy,100*256,"mag",100,enhance:false).Damage256==0,"Magic immunity resolver");enemy.SetResistance(ResistanceType.Physical,0);Clean(owner);
    }
    private static void Gates(PlayerMobile owner,Skeleton enemy,Item weapon)
    {
        Clean(owner);owner.Mana=3;NecromancerEffects.Profiles.Store(owner,new());Require(!ClassCombat.Perform(owner,CombatAbility.Berserk,enemy,enemy.Location,Core.TickCount) && BarbarianBerserk.State(owner)==null,"Unpaid Berserk");Mana(owner);
        var origin=enemy.Location;enemy.MoveToWorld(new(owner.X+6,owner.Y,owner.Z),owner.Map);Require(!ClassCombat.Perform(owner,CombatAbility.Berserk,enemy,enemy.Location,Core.TickCount),"Berserk spell reach");enemy.MoveToWorld(origin,owner.Map);
        Require(!ClassCombat.Perform(owner,CombatAbility.Berserk,null,owner.Location,Core.TickCount),"Unverified Berserk ground acquisition");
        var weaponTiming=CombatTiming.Weapon(CombatTiming.Equipped(owner));var timing=CombatTiming.Calculate(DiabloClass.Barbarian,CombatAbility.Berserk,weaponTiming);
        Require(timing==CombatTiming.Calculate(DiabloClass.Barbarian,CombatAbility.BasicAttack,weaponTiming) && timing==CombatTiming.Calculate(DiabloClass.Barbarian,CombatAbility.Berserk,weaponTiming,fcr:100) && CombatTiming.Calculate(DiabloClass.Barbarian,CombatAbility.Berserk,weaponTiming,ias:100).Frames<timing.Frames,"Berserk A1/IAS/FCR");
        foreach(int cancel in new[]{0,1,2,3,4})
        {
            Clean(owner);int hp=Life(enemy);var point=owner.Location;Start(owner,enemy);var action=ClassCombat.For(owner);long end=action.NextAction,at=action.EffectAt;int paid=NecromancerEffects.ManaBalance256(owner);var buff=BarbarianBerserk.State(owner);
            if(cancel==0){ClassCombat.Stop(owner);}else if(cancel==1){Require(HitRecovery.Apply(owner,owner.HitsMax,Core.TickCount),"Berserk FHR interrupt");}else if(cancel==2){owner.Direction=Direction.West;Require(owner.Move(Direction.West),"Interruptible Berserk movement");owner.Direction=Direction.East;Require(owner.Move(Direction.East) && owner.Location==point,"Berserk out/back step");}else if(cancel==3){owner.AddToBackpack(weapon);}else{owner.Paralyzed=true;}
            Require(!ClassCombat.ResolveAction(owner,at) && action.BarbarianAttack==null && action.NextAction==end && Life(enemy)==hp && NecromancerEffects.ManaBalance256(owner)==paid && BarbarianBerserk.State(owner)==buff && ClassStats.Defense(owner)==0,"Interrupt removed paid timed penalty/refunded/delivered");owner.Paralyzed=false;if(weapon.Parent!=owner){Require(owner.EquipItem(weapon),"Interrupt weapon restore");}ClassGear.Refresh(owner);
        }
        Clean(owner);var bow=Equip(owner,new Bow());Require(!ClassCombat.Perform(owner,CombatAbility.Berserk,enemy,enemy.Location,Core.TickCount),"Berserk accepted bow");bow.Delete();Require(owner.EquipItem(weapon),"Bow restore");ClassGear.Refresh(owner);Clean(owner);
    }
    private static void Temporary(PlayerMobile owner,Skeleton enemy)
    {
        foreach(int cancel in new[]{0,1,2})
        {
            Clean(owner);BarbarianMelee.HitRoll=()=>cancel==0?1:0;Start(owner,enemy);BarbarianMelee.HitRoll=()=>0;var timed=BarbarianBerserk.State(owner);Replace(timed with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)});var action=ClassCombat.For(owner);
            Require(ClassCombat.ResolveAction(owner,action.EffectAt) && BarbarianBerserk.State(owner)==null && action.BerserkMode!=null && ClassStats.Defense(owner)==0,"Expired-before-impact Do2 temporary state");
            if(cancel==0){ClassCombat.Stop(owner);}else if(cancel==1){Require(HitRecovery.Apply(owner,owner.HitsMax,Core.TickCount),"Temporary Berserk FHR");}else{var point=owner.Location;owner.Direction=Direction.West;Require(owner.Move(Direction.West),"Temporary Berserk movement");owner.MoveToWorld(point,owner.Map);}
            Require(action.BerserkMode==null && ClassStats.Defense(owner)>0,"Temporary Berserk survived mode interruption");TimedEffects.ClearGear(enemy);
        }
        Clean(owner);
    }
    private static void Lifecycle(PlayerMobile owner,Skeleton enemy)
    {
        var point=enemy.Location;
        var recast=Fresh(owner);Train(recast,CombatAbility.Berserk,1);enemy.MoveToWorld(new(recast.X+1,recast.Y,recast.Z),recast.Map);Start(recast,enemy);var first=BarbarianBerserk.State(recast);Ready(recast);Train(recast,CombatAbility.Berserk,20);Start(recast,enemy);
        Require(BarbarianBerserk.State(recast).UntilUtc<first.UntilUtc && BarbarianBerserk.State(recast).Rank==20,"Higher-rank recast did not replace/shorten timed penalty");Clean(recast);recast.Delete();enemy.MoveToWorld(point,owner.Map);
        foreach(int transition in new[]{0,1,2})
        {
            var actor=Fresh(owner);Train(actor,CombatAbility.Berserk,1);actor.Hits=actor.HitsMax;enemy.MoveToWorld(new(actor.X+1,actor.Y,actor.Z),actor.Map);int hp=Life(enemy);Start(actor,enemy);long effectAt=ClassCombat.For(actor).EffectAt;
            if(transition==0){actor.Kill();actor.Resurrect();}
            else if(transition==1){Ready(actor);Require(ClassCreation.Profiles.TryActivateHistory(actor,DiabloClass.Paladin) && BarbarianBerserk.State(actor)==null && ClassCreation.Profiles.TryActivateHistory(actor,DiabloClass.Barbarian),"Timed penalty class-history cleanup");}
            else{ClassCreation.Profiles.TryGet(actor.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(actor,profile);}
            Require(BarbarianBerserk.State(actor)==null && ClassCombat.For(actor).BerserkMode==null && ClassStats.Defense(actor)>0 && !ClassCombat.ResolveAction(actor,effectAt) && Life(enemy)==hp,"Death/history/respec leaked Berserk defense override or damage");actor.Corpse?.Delete();actor.Delete();enemy.MoveToWorld(point,owner.Map);
        }
    }
    private static void GearAndConsumers(PlayerMobile owner,Skeleton enemy,Item original)
    {
        var sword=Named(owner,"Hellplague");Clean(owner);var values=SkillMechanics.Values(owner,CombatAbility.Berserk);var raw=GearEffects.Damage(owner,0,enemy,(int)values.Eval("calc1"),_=>0);BarbarianMelee.Effects=new Rolls(true);Start(owner,enemy);
        var packet=ClassCombat.For(owner).BarbarianAttack;Require(raw.Fire>0 && packet.Impact.Parts.Fire==raw.Fire && packet.Impact.Parts.Physical==0 && NamedElements.To256(packet.Impact.Parts.Magic)==NamedElements.To256(2*raw.Physical),"Named flat elements converted/doubled or duration added as physical");Ready(owner);
        var ogre=new Ogre{NoKillAwards=true};ogre.SetHits(30000);ogre.SetResistance(ResistanceType.Physical,0);ogre.SetResistance(ResistanceType.Fire,0);ogre.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Stop(ogre);Freeze(ogre);
        try
        {
            Require(GemCombat.Drain(ogre)==100 && ClassGear.Bonus(owner,"life-steal%")>0 && ClassGear.Bonus(owner,"mana-steal%")>0,"Real Hellplague drain fixture");Clean(owner);Start(owner,ogre);owner.Hits=owner.HitsMax-100;int life=Life(owner),mana=NecromancerEffects.ManaBalance256(owner);
            NecromancerEffects.Profiles.Store(ogre,new(new(CombatAbility.LifeTap,owner.Serial.Value,1,TimedEffects.Now.AddMinutes(1),owner.X,owner.Y)));
            var action=ClassCombat.For(owner);Require(ClassCombat.ResolveAction(owner,action.EffectAt) && Life(owner)==life && NecromancerEffects.ManaBalance256(owner)==mana,"Converted magic leeched or triggered Life Tap");TimedEffects.ClearGear(ogre);Ready(owner);
            // Same ED without conversion is an explicit positive physical-packet control, large enough to exceed leech integer rounding.
            GearEffects.Strike(owner,ogre,0,new Rolls(),wearWeapon:false,skillPhysicalPercent:(int)values.Eval("calc1"));Require(Life(owner)>life && NecromancerEffects.ManaBalance256(owner)>mana,"Physical leech/Life Tap positive control");TimedEffects.ClearGear(ogre);NecromancerEffects.Profiles.Clear(ogre);
            var leases=PaladinAuras.Snapshot();var thorns=new PaladinAuraLease(ogre.Serial.Value,owner.Serial.Value,CombatAbility.Thorns,1,0,TimedEffects.Now.AddMinutes(1),DamagePercent:100);
            try{PaladinAuras.Restore(new(leases.Emitters,leases.Leases.Append(thorns).ToArray()));Clean(owner);Start(owner,ogre);owner.Hits=owner.HitsMax;life=Life(owner);action=ClassCombat.For(owner);Require(ClassCombat.ResolveAction(owner,action.EffectAt) && Life(owner)==life,"Converted magic caused proportional Thorns");}
            finally{PaladinAuras.Restore(leases);}
        }
        finally{ogre.Delete();Clean(owner);sword.Delete();Require(owner.EquipItem(original),"Named weapon restore");ClassGear.Refresh(owner);owner.Hits=owner.HitsMax;}
    }
    private static void Seed(PlayerMobile owner,Skeleton enemy,Item weapon,Item helm)
    {
        Clean(owner);Freeze(owner);Freeze(enemy);Stop(enemy);int defense=ClassStats.Defense(owner),hp=Life(enemy);Start(owner,enemy);var action=ClassCombat.For(owner);
        var state=BarbarianBerserk.State(owner) with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};Replace(state);action.EffectAt=Core.TickCount+FixtureSpace.RetentionMilliseconds;action.NextAction=action.EffectAt+1000;action.BarbarianAttack=action.BarbarianAttack with{EffectAt=action.EffectAt}; // Saved inspection holds only, after natural durations pass.
        var expired=Fresh(owner);Train(expired,CombatAbility.Berserk,1);BarbarianBerserk.Begin(expired,SkillMechanics.Values(expired,CombatAbility.Berserk),Core.TickCount);Replace(BarbarianBerserk.State(expired) with{UntilUtc=TimedEffects.Now.AddMilliseconds(500)});
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,enemy.Serial.Value,weapon.Serial.Value,helm.Serial.Value,expired.Serial.Value,NecromancerEffects.ManaBalance256(owner),hp,defense,state,owner.Location,owner.Map.MapID)));
    }
    private static void Natural(PlayerMobile owner,Skeleton enemy,Item weapon,Item helm)
    {
        Clean(owner);int defense=ClassStats.Defense(owner),hp=Life(enemy),resistance=owner.PhysicalResistance;Start(owner,enemy);var action=ClassCombat.For(owner);var packet=action.BarbarianAttack;var state=BarbarianBerserk.State(owner);long end=action.NextAction;
        int frames=(int)SkillMechanics.Values(owner,CombatAbility.Berserk).Eval("calc2");Require(Math.Abs((state.UntilUtc-TimedEffects.Now).TotalMilliseconds-frames*40)<80 && ClassStats.Defense(owner)==0 && owner.PhysicalResistance==resistance,"Natural Berserk start duration/defense/no invented resistance");
        Later((int)(action.EffectAt-Core.TickCount+45),()=>
        {
            Require(action.PendingAbility==CombatAbility.None && action.BarbarianAttack==null && hp-Life(enemy)==packet.Impact.LifeDamage256 && BarbarianBerserk.State(owner)==state && ClassStats.Defense(owner)==0,"Natural impact/penalty deadline");
            Later((int)(end-Core.TickCount+80),()=>
            {
                Require(BarbarianBerserk.State(owner)==state && ClassStats.Defense(owner)==0,"Berserk penalty ended with ordinary animation");
                Later((int)(state.UntilUtc-TimedEffects.Now).TotalMilliseconds+150,()=>
                {
                    Require(BarbarianBerserk.State(owner)==null && ClassStats.Defense(owner)==defense,"Natural timed Berserk expiry");
                    // An early-expiry fixture makes the actual Do2 loop exercise the slow-swing source branch.
                    Start(owner,enemy);action=ClassCombat.For(owner);Replace(BarbarianBerserk.State(owner) with{UntilUtc=TimedEffects.Now.AddMilliseconds(40)});hp=Life(enemy);packet=action.BarbarianAttack;
                    Later((int)(action.EffectAt-Core.TickCount+45),()=>
                    {
                        Require(BarbarianBerserk.State(owner)==null && action.BerserkMode!=null && ClassStats.Defense(owner)==0 && hp-Life(enemy)==packet.Impact.LifeDamage256,"Natural expired-before-impact temporary state");
                        Later((int)(action.NextAction-Core.TickCount+100),()=>
                        {
                            Require(action.BerserkMode==null && ClassStats.Defense(owner)==defense,"Natural temporary Berserk animation expiry");Seed(owner,enemy,weapon,helm);Log.Information("berserk-result: passed");
                        });
                    });
                });
            });
        });
    }
    private static void Check()
    {
        Goldens();var owner=Fresh();foreach(var ability in new[]{CombatAbility.Howl,CombatAbility.BattleOrders,CombatAbility.Berserk,CombatAbility.IronSkin}){Train(owner,ability,20);}Train(owner,CombatAbility.SwordMastery,1);
        while(owner.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Berserk Strength");}while(owner.RawDex<200){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Berserk Dexterity");}owner.Hits=owner.HitsMax;
        var weapon=Equip(owner,new Longsword());var enemy=new Skeleton{NoKillAwards=true};enemy.SetHits(30000);enemy.SetResistance(ResistanceType.Fire,0);enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Stop(enemy);Freeze(owner);Freeze(enemy);
        Gates(owner,enemy,weapon);ConcentrateChecks.Rating(owner,CombatAbility.Berserk);Damage(owner,enemy);Temporary(owner,enemy);Lifecycle(owner,enemy);var helm=Named(owner,"Tarnhelm");Require(SkillMechanics.Values(owner,CombatAbility.Berserk).Eval("calc1")==850,"Soft Berserk/hard synergy split");GearAndConsumers(owner,enemy,weapon);Damage(owner,enemy);
        Clean(owner);Start(owner,enemy);Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.Concentrate,enemy,enemy.Location,Core.TickCount) && BarbarianConcentrate.Defense(owner)>0 && ClassStats.Defense(owner)==0,"Berserk final override lost against Iron Skin/Concentrate");Clean(owner);
        Attach(owner);Natural(owner,enemy,weapon,helm);
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var enemy=(Skeleton)World.FindMobile((Serial)saved.Enemy);owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);Stop(enemy);Freeze(owner);Freeze(enemy);
        Require(BarbarianBerserk.State(owner)==saved.State && ClassStats.Defense(owner)==0 && NecromancerEffects.ManaBalance256(owner)==saved.Mana && Life(enemy)==saved.Life && ClassCombat.For(owner).PendingAbility==CombatAbility.None && ClassCombat.For(owner).BarbarianAttack==null && ClassCombat.For(owner).BerserkMode==null,"Restart timed state/resources or transient action replay");
        var expired=(PlayerMobile)World.FindMobile((Serial)saved.Expired);Require(BarbarianBerserk.State(expired)==null && ClassStats.Defense(expired)>0,"Expired saved Berserk state survived offline deadline");expired.Delete();
        Require(SkillMechanics.Rank(owner,CombatAbility.Berserk)==21 && SkillBindings.Instance.Get(owner).Left==CombatAbility.Berserk && SkillBindings.Instance.Get(owner).Right==CombatAbility.Berserk && CombatTiming.Equipped(owner)?.Serial==(Serial)saved.Weapon,"Berserk saved gear/ranks/bindings");
        Damage(owner,enemy);Attach(owner);Natural(owner,enemy,World.FindItem((Serial)saved.Weapon),World.FindItem((Serial)saved.Helm));
    }
    private static void Disconnected()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(owner.NetState==null && ClassCombat.Existing(owner)?.BerserkMode==null && BarbarianBerserk.State(owner)==saved.State && ClassStats.Defense(owner)==0,"Disconnect removed timed state or retained transient mode");
    }
}
