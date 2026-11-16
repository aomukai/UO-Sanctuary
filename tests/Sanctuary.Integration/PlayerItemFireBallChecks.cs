// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class PlayerItemFireBallChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(PlayerItemFireBallChecks));
    private sealed record Saved(uint Owner,uint Item,uint Target,Point3D Point,string Gear,ItemPropertyCreationState Carrier,int Life,int Mana,int TargetLife,SorceressSpellFlight Flight,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"player-item-fireball-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static EquipmentStatValues Values()=>new(_=>4,_=>null,_=>null,(_,_,_,_,_)=>{});
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>m.Name!="PlayerItemFireBallFixture"&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>m.Name!="PlayerItemFireBallFixture"&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"player-item-fireball-reload-check":"player-item-fireball-check";
            ConsoleInputHandler.RegisterCommand(name,"Prepared eight-class Fire Ball item producer,original carrier and actual saves",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static PlayerMobile Owner(DiabloClass type,int index)
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="PlayerItemFireBallFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,type);ClassCreation.Profiles.AdvanceLevel(p,99);
        foreach(var stat in new[]{CharacterStat.Strength,CharacterStat.Dexterity,CharacterStat.Energy})
        {while((stat==CharacterStat.Strength?p.RawStr:stat==CharacterStat.Dexterity?p.RawDex:p.RawInt)<100){Require(ClassCreation.Profiles.TrySpendStat(p,stat),"Allocate item carrier stats");}}
        FixtureSpace.Corridor(p,6200,48,3200+index*35);
        if(type==DiabloClass.Sorceress)
        {
            for(int n=0;n<3;n++){Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.firebolt",out _),"Train hard Fire Bolt synergy");}
            var gear=NamedGear.Create("unique/185",99,new Random(173),true);p.AddToBackpack(gear);Require(p.EquipItem(gear),"Equip complete Chromatic Ire");ClassGear.Refresh(p);
        }
        p.Hits=p.HitsMax;SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-513);NecromancerEffects.SetMana256(p,0);return p;
    }
    private static IronGolemFireBallChecks.Recipient Target(PlayerMobile p)
    {
        var t=new IronGolemFireBallChecks.Recipient();t.SetHits(50000);t.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();
        foreach(var resistance in Enum.GetValues<ResistanceType>()){t.SetResistance(resistance,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static SorceressSpellFlight Flight(long id)=>SorceressSpellMissiles.Snapshot().Flights.SingleOrDefault(f=>f.Id==id);
    private static EquipmentStatValue Encoded(ItemPropertyCreationState carrier)
    {
        var value=carrier.Properties.Single();Require(value.Stat==195&&(ushort)value.Packed==(47<<6|20)&&value.Value==10,"Hellslayer source level/chance/layer changed");return value;
    }
    private static SorceressSpellFlight Emit(PlayerMobile p,Mobile target,int rank,int outgoing,bool position,DateTime at,bool allocated=true)
    {
        var state=ClassCombat.For(p);var pending=(state.PendingAbility,state.PendingTarget,state.PendingPoint,state.NextAction);string binding=Json(SkillBindings.Instance.Get(p));
        int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(p);int draws=0;
        var aim=target.Location;
        var beforePool=SorceressSpellMissiles.Snapshot();var combatant=p.Combatant;int rolls=0,notifications=0;ulong random=ChanceSeed(true),expected=random;SourceRandom.Roll(ref expected);
        int stat=outgoing==1?195:201;uint packed=EquipmentStatKey.Pack(stat,(47<<6)|rank);
        var emitted=PlayerItemProcDispatch.Run(outgoing==1?20:21,packed,p,position?null:NpcItemTarget.Capture((BaseCreature)target),aim,new(0,0,0,0,4,HitFlags:32),
            (s,layer)=>{Require(s==stat&&layer==((47<<6)|rank),"Proc read a different stat/layer");return 10;},()=>{rolls++;return SourceRandom.Roll(ref random);},false,at,r=>
        {
            draws++;Require(r.Skill==47&&r.Rank==rank&&r.X==(outgoing==1?target.X:p.X)*2&&r.Y==p.Y*2&&r.TargetX==(outgoing==1?2*target.X-p.X:target.X)*2,"Player/source item origin request differs");
            var frame=PlayerItemSpellCasts.Context(p);Require(frame!=null&&frame.ServerDo&&frame.Target==(position?0:checked((int)target.Serial.Value))&&NpcItemSpellCasts.KeepsCombatTarget(p),"Missing protected player item frame");
            var alternate=World.FindMobile(state.PendingTarget)as BaseCreature;Require(alternate!=null&&alternate!=target,"Missing distinct ordinary target");
            p.Combatant=p.Combatant==target?alternate:target;Require(p.Combatant==combatant,"Item frame replaced native ordinary combatant");
            bool nested=false;try
            {
                PlayerItemSpellCasts.Target(p,NpcItemTarget.Capture(alternate),rank,1,false,at,_=>throw new ArithmeticException("Nested item allocation probe"));
            }
            catch(ArithmeticException){nested=true;}
            Require(nested&&PlayerItemSpellCasts.Context(p)==frame&&p.Combatant==combatant&&NpcItemSpellCasts.KeepsCombatTarget(p),"Nested failure lost outer target/path/server-do/guard");
            return new(allocated,SourceRandom.InitLow(73));
        },_=>{notifications++;Require(NpcItemSpellCasts.KeepsCombatTarget(p),"Notification escaped target guard");});
        var created=SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value&&f.Id>beforePool.NextId).ToArray();
        Require(emitted==new PlayerItemProcOutcome(1,1,1)&&draws==1&&rolls==1&&random==expected&&notifications==1&&created.Length==(allocated?1:0),"Source callback/cast/allocation/refusal distinction or RNG count");
        Require(PlayerItemSpellCasts.Context(p)==null&&!NpcItemSpellCasts.KeepsCombatTarget(p)&&p.Combatant==combatant,"Outer item frame/guard leaked");
        Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(p)==life&&(state.PendingAbility,state.PendingTarget,state.PendingPoint,state.NextAction)==pending&&Json(SkillBindings.Instance.Get(p))==binding,"Item emission changed player mana/life/ordinary action or bindings");
        Require(ClassCreation.Profiles.GetRank(p.Serial,"sorceress.fireball")==0&&SkillMechanics.Rank(p,CombatAbility.Fireball)==0&&!ClassCombat.Available(p,CombatAbility.Fireball),"Item emission granted learned/cross-class Fire Ball");
        if(!allocated){return null;}
        var f=created.Single();ClassCreation.Profiles.TryGet(p.Serial,out var profile);
        Require(f.PlayerItemClass==profile.Class&&f.Npc==null&&f.Rank==rank&&f.Seed==SourceRandom.InitLow(73)&&f.Minimum256>0,"Player proc lost own class/rank/seed/positive packet");
        if(profile.Class==DiabloClass.Sorceress){Require(f.Modifiers.FireMastery==51,"Item Fire Ball omitted player's own mastery");}
        else{Require(f.Modifiers.FireMastery==0,"Foreign item Fire Ball borrowed Sorceress mastery");}
        return f;
    }
    private static ulong ChanceSeed(bool success)
    {
        for(int n=1;n<10000;n++){ulong seed=SourceRandom.InitLow(n),copy=seed;if((SourceRandom.Roll(ref copy)%100<10)==success){return seed;}}
        throw new InvalidOperationException("No callback chance seed");
    }
    private static void Gates(PlayerMobile p,BaseCreature target)
    {
        uint packed=EquipmentStatKey.Pack(195,(47<<6)|20);var pool=SorceressSpellMissiles.Snapshot();var combatant=p.Combatant;
        foreach(int test in Enumerable.Range(0,7))
        {
            int rolls=0,allocations=0,notifications=0;ulong random=ChanceSeed(test!=2);bool incoming=test is 3 or 4;int function=incoming?21:20;
            var packet=test==4?null:new PreparedMonsterDamagePacket(0,0,0,0,incoming?0:4,HitFlags:test==1?0u:32u);
            var outcome=PlayerItemProcDispatch.Run(function,incoming?EquipmentStatKey.Pack(201,(47<<6)|20):packed,test==6?null:p,NpcItemTarget.Capture(target),target.Location,packet,
                (_,_)=>test==0?0:10,()=>{rolls++;return SourceRandom.Roll(ref random);},test==5,TimedEffects.Now,_=>{allocations++;throw new ArithmeticException("Unexpected gate allocation");},_=>notifications++);
            if(test==5){Require(outcome==new PlayerItemProcOutcome(1,1,0)&&rolls==1,"Uninterruptible player cast/result gate");}
            else{Require(outcome==new PlayerItemProcOutcome(0,0,0)&&rolls==(test==2?1:0),"Callback chance/hit/source/get-hit gate "+test);}
            Require(allocations==0&&notifications==0&&PlayerItemSpellCasts.Context(p)==null&&!NpcItemSpellCasts.KeepsCombatTarget(p)&&p.Combatant==combatant,"Rejected callback mutated world/frame");
        }
        bool failed=false;int chanceRolls=0;
        try
        {
            PlayerItemProcDispatch.Run(20,packed,p,NpcItemTarget.Capture(target),target.Location,new(0,0,0,0,0,HitFlags:32),(_,_)=>10,()=>{chanceRolls++;return 0;},false,TimedEffects.Now,_=>throw new ArithmeticException("Outer allocation failure"));
        }
        catch(ArithmeticException){failed=true;}
        Require(failed&&chanceRolls==1&&PlayerItemSpellCasts.Context(p)==null&&!NpcItemSpellCasts.KeepsCombatTarget(p)&&p.Combatant==combatant,"Throwing callback failed restoration or rolled back chance");
        Require(Json(SorceressSpellMissiles.Snapshot())==Json(pool),"Rejected callback allocated a flight");
    }
    private static void Finish(PlayerMobile p,IronGolemFireBallChecks.Recipient target,SorceressSpellFlight f)
    {
        int before=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(p);int raw=SorceressSpellMissiles.Roll(f,out _);
        for(int n=0;n<60&&Flight(f.Id)is{} current;n++){SorceressSpellMissiles.Tick(current.NextUtc,true,current.Id);}
        Require(Flight(f.Id)==null&&before-SkillHealth.Balance256(target)==raw&&target.Source==p.Serial.Value&&target.Before==before,"Player item travel/one captured Fire Ball/source/notification-before-life differs: loss="+(before-SkillHealth.Balance256(target))+" raw="+raw+" source="+target.Source+"/"+p.Serial.Value+" before="+target.Before+"/"+before);
        Require(NecromancerEffects.ManaBalance256(p)==mana,"Item Fire Ball spent player mana on contact");
    }
    private static void RestoreFlight(SorceressSpellFlight f)
    {var pool=SorceressSpellMissiles.Snapshot();SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Id==f.Id?f:x).ToArray()});}
    private static void Check(bool reload)
    {
        Require(!NamedGear.Available("unique/254"),"Prepared producer enabled incomplete Hellslayer acquisition");var types=Enum.GetValues<DiabloClass>();Require(types.Length==8,"Class roster changed");
        var saved=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):new Saved[8];var originalSeed=SorceressSpellMissiles.SeedLow;
        SorceressSpellMissiles.SeedLow=()=>throw new InvalidOperationException("Item allocation borrowed ordinary spell RNG");
        try
        {
            for(int index=0;index<8;index++)
            {
                PlayerMobile p;Item item;IronGolemFireBallChecks.Recipient t;ItemPropertyCreationState carrier;int generation=0;
                if(!reload)
                {
                    p=Owner(types[index],index);t=Target(p);item=new DoubleAxe();ClassGear.Profiles.Convert(item,99,GearRarity.Normal,new Random(173));p.AddToBackpack(item);
                    var graph=new ItemPropertyCreationContext(item,Values(),default,true,false,false,SourceRandom.InitLow(1),[]);
                    var property=NamedGear.Catalog.Item("unique/254").Properties.Single(x=>x.Code=="att-skill");
                    graph.ApplySkill(EquipmentPropertyWriter.Plan("att-skill"),new(property.SourceMin.Value,property.SourceMax.Value,NamedPropertyRuntime.Parameter(property)),ItemSkillProperties.ForLevel(ClassGear.State(item).Roll.ItemLevel));carrier=graph.Capture();Require(graph.Draws==0,"Proc encoding rerolled its source arguments");
                }
                else
                {
                    var old=saved[index];generation=old.Generation+1;Require(generation<=2,"Extra producer reload");p=(PlayerMobile)World.FindMobile((Serial)old.Owner);item=World.FindItem((Serial)old.Item);t=(IronGolemFireBallChecks.Recipient)World.FindMobile((Serial)old.Target);
                    Require(item?.RootParent==p&&Json(ClassGear.State(item))==old.Gear&&SkillHealth.Balance256(p)==old.Life&&NecromancerEffects.ManaBalance256(p)==old.Mana&&SkillHealth.Balance256(t)==old.TargetLife&&Json(Flight(old.Flight.Id))==Json(old.Flight),"Saved carrier/actor/flight/wounds/resources changed");
                    carrier=ItemPropertyCreationContext.Restore(item,Values(),default,true,false,false,old.Carrier).Capture();Require(Json(carrier)==Json(old.Carrier),"Restoration rerolled encoded proc");
                    p.MoveToWorld(old.Point,Map.Trammel);var held=Flight(old.Flight.Id);SorceressSpellMissiles.Tick(held.NextUtc,true,held.Id);var aged=Flight(held.Id);
                    Require(aged.Frame==held.Frame+1&&aged.X==held.X&&aged.Y==held.Y&&SkillHealth.Balance256(t)==old.TargetLife,"Loaded item projectile replayed offline movement/damage");Finish(p,t,aged);
                }
                var ordinary=FireBoltChecks.Target(p,1);ordinary.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=0;p.Combatant=ordinary;
                if(ClassCombat.For(p).PendingAbility==CombatAbility.None){Require(ClassCombat.Perform(p,CombatAbility.BasicAttack,ordinary,ordinary.Location,Core.TickCount),"Begin distinct ordinary attack");}
                Require(ClassCombat.For(p).PendingTarget==ordinary.Serial,"Ordinary target not retained");Gates(p,t);
                int rank=(ushort)Encoded(carrier).Packed&63;
                if(!reload)
                {
                    for(int outgoing=0;outgoing<=1;outgoing++)foreach(bool position in new[]{false,true})
                    {var launched=Emit(p,t,rank,outgoing,position,TimedEffects.Now);Finish(p,t,launched);}
                    _=Emit(p,t,rank,1,false,TimedEffects.Now,false);
                    var f=Emit(p,t,rank,0,false,TimedEffects.Now);int hp=SkillHealth.Balance256(t);SorceressSpellMissiles.Tick(f.NextUtc,false,f.Id);var offline=Flight(f.Id);
                    Require(offline.Frame==1&&offline.X==f.X&&offline.Y==f.Y&&SkillHealth.Balance256(t)==hp,"Offline item projectile moved/fought");Finish(p,t,offline);
                }
                var pending=Emit(p,t,rank,0,false,TimedEffects.Now.Add(FixtureSpace.Retention));
                if(index==0)
                {
                    var pool=SorceressSpellMissiles.Snapshot();bool refused=false;try{SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(f=>f.Id==pending.Id?f with{PlayerItemClass=(DiabloClass)0}:f).ToArray()});}catch(InvalidDataException){refused=true;}
                    Require(refused&&Flight(pending.Id)==pending,"Invalid saved item class partly restored");
                }
                ClassCombat.Stop(p);p.Combatant=null;ordinary.Delete();
                var point=p.Location;p.Internalize();saved[index]=new(p.Serial.Value,item.Serial.Value,t.Serial.Value,point,Json(ClassGear.State(item)),carrier,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(t),pending,generation);
            }
            File.WriteAllText(Manifest,Json(saved));
        }
        finally{SorceressSpellMissiles.SeedLow=originalSeed;}
    }
}
