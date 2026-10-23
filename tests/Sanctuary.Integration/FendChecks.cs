// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using Server.Network;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary.Integration;

public static class FendChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FendChecks));
    private sealed class Rolls(int value):Random{public int Calls;public override int Next(int maximum){Calls++;return Math.Clamp(value,0,Math.Max(0,maximum-1));}}
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,uint Target,Point3D Origin,int Mana,int Life,int Durability,int Quantity,int Rank);
    private static bool _creating;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"fend-fixtures.json");
    public static void Configure(){Freeze();Register("fend-check",Check);Register("fend-reload-check",Reload);Register("fend-loop-check",Natural,false);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Freeze();
    private static void Freeze()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=a=>_creating || a.Name is "FendOwner" or "FendTarget"?TimeSpan.FromHours(1):hp?.Invoke(a)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=a=>_creating || a.Name=="FendOwner"?TimeSpan.FromHours(1):mp?.Invoke(a)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action,bool controlled=true)=>ConsoleInputHandler.RegisterCommand(name,"Fend count, live strike stats, rate-scaled rollback and transient sequence persistence",_=>Core.LoopContext.Post(()=>
    {try{if(controlled){Controlled(action);}else{action();}Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Controlled(Action action)
    {
        var hit=AmazonFend.HitRoll;var element=AmazonFend.ElementRoll;var physical=AmazonFend.PhysicalRoll;var effects=AmazonFend.Effects;var wear=AmazonFend.WearRoll;
        try{AmazonFend.HitRoll=()=>0;AmazonFend.ElementRoll=()=>0;AmazonFend.PhysicalRoll=_=>0;AmazonFend.Effects=new Rolls(99);AmazonFend.WearRoll=new Rolls(99);action();}
        finally{AmazonFend.HitRoll=hit;AmazonFend.ElementRoll=element;AmazonFend.PhysicalRoll=physical;AmazonFend.Effects=effects;AmazonFend.WearRoll=wear;}
    }
    private static PlayerMobile Fresh()
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Name="FendOwner",Player=true};}finally{_creating=false;}
        owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);
        while(owner.RawStr<110){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Strength");}
        while(owner.RawDex<130){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Dexterity");}
        FixtureSpace.Place(owner,new(2300,1100,0),Map.Trammel);Require(!SpellHelper.IsTown(owner.Location,owner),"Fixture town");Mana(owner);return owner;
    }
    private static void Train(PlayerMobile owner,string id,int rank=1)
    {while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(TalentCatalog.TryGet(DiabloClass.Amazon,id,out var d),"Missing talent");foreach(var prerequisite in d.Prerequisites){Train(owner,prerequisite);}Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}}
    private static void Mana(PlayerMobile owner)=>NecromancerEffects.SetMana256(owner,30*256+17);
    private static void Ready(PlayerMobile owner){AmazonMelee.Cancel(owner);AmazonFend.Cancel(owner);ClassCombat.Stop(owner);var s=ClassCombat.For(owner);s.NextAction=s.RecoveryUntil=Core.TickCount;Mana(owner);}
    private static BaseCreature Enemy(PlayerMobile owner,int dx=1,MonsterDifficulty difficulty=MonsterDifficulty.Normal)
    {
        BaseCreature target;_creating=true;try{target=new GraveyardZombie{Name="FendTarget"};}finally{_creating=false;}
        if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(target,difficulty);}
        target.Frozen=target.CantWalk=true;target.NoKillAwards=true;target.AIObject.AITimer.Stop();target.SetHits(50000);target.Hits=target.HitsMax;target.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);return target;
    }
    private static BaseWeapon Equip(PlayerMobile owner,BaseWeapon weapon)
    {if(CombatTiming.Equipped(owner) is{} old){owner.AddToBackpack(old);}ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(weapon),"Equip weapon");ClassGear.Refresh(owner);return weapon;}
    private static Item Named(PlayerMobile owner,string source)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);Require(owner.EquipItem(item),"Named "+source);ClassGear.Refresh(owner);return item;}
    private static AmazonFend.Series Begin(PlayerMobile owner,Mobile target,int input=0)
    {
        long now=Core.TickCount;bool ok=input==1?ClassCombat.ContinueAttack(owner,CombatAbility.Fend,target,target.Location,now):input==2?ClassCombat.ContinueCast(owner,CombatAbility.Fend,target,target.Location,now):ClassCombat.Perform(owner,CombatAbility.Fend,target,target.Location,now);
        Require(ok && ClassCombat.For(owner).Fend!=null,"Fend input");ClassCombat.ReleaseCast(owner);return ClassCombat.For(owner).Fend;
    }
    private static int Event(PlayerMobile owner,Mobile target,AmazonFend.Series series,long? at=null)
    {
        int before=SkillHealth.Balance256(target);long eventAt=at??series.EffectAt;
        Require(!ClassCombat.ResolveAction(owner,eventAt-1),"Early Fend event");Require(ClassCombat.ResolveAction(owner,eventAt),"Fend event missing");Require(!ClassCombat.ResolveAction(owner,eventAt),"Duplicate Fend event");return before-SkillHealth.Balance256(target);
    }
    private static void Finish(PlayerMobile owner)
    {var series=ClassCombat.For(owner).Fend;if(series!=null){Require(series.Remaining<=0 && ClassCombat.ResolveAction(owner,series.Until) && ClassCombat.For(owner).Fend==null,"Fend recovery end");}}
    private static int Expected(PlayerMobile owner,Mobile target,bool basic=false)
    {
        using var mode=WeaponHands.UseMelee(owner,CombatTiming.Equipped(owner));var v=SkillMechanics.Values(owner,CombatAbility.Fend);
        var damage=GearEffects.Damage(owner,0,target,basic?0:(int)v.Eval("calc2"),_=>0);
        return NamedElements.To256(GearEffects.PrepareWeaponImpact(owner,target,0,AmazonFend.Effects,damage).Parts.Total);
    }
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"fend-source-probe.json")));int countRows=0,schedules=0;
        foreach(var row in doc.RootElement.GetProperty("starts").EnumerateArray())
        {if(row[0].GetInt32()!=1 || row[1].GetInt32()!=1 || row[2].GetInt32()!=1){continue;}Require(AmazonFend.Count(row[3].GetInt32(),12)==row[5].GetInt32(),"Compiled selected-target count");countRows++;}
        foreach(var row in doc.RootElement.GetProperty("modern").EnumerateArray())
        {
            string code=row[0].GetInt32()==1?"1HT":"2HT";int advance=row[1].GetInt32();
            foreach(int n in new[]{4,5})
            {
                var pattern=row[n==5?4:5].EnumerateArray().Select(x=>x.GetInt32()).ToArray();var actual=AmazonFend.Schedule(code,advance,n);int elapsed=0;
                Require(actual.Events.SequenceEqual(pattern.Take(n).Select(x=>elapsed+=x)) && actual.End==pattern.Take(n-1).Sum()+pattern[^1],"Original current-family rollback/event/final-cycle pattern");schedules++;
            }
        }
        for(int rank=1;rank<=20;rank++)
        {var v=new SkillValues(SkillMechanics.For(CombatAbility.Fend),_=>rank,_=>0,99);Require(v.Mana256==1280 && v.Eval("calc1")==12 && v.Formula("toht")==40+10*(rank-1) && v.Eval("calc2")==70+10*(rank-1) && v.Param(2)==30,"Pinned Fend rank golden");}
        Require(countRows==7 && schedules==24 && CombatTiming.SequenceRateAdjustment(CombatAbility.Fend)==0 && CombatTiming.Calculate(DiabloClass.Amazon,CombatAbility.Fend,new(AttackFamily.Thrust,0),fcr:1000)==new ActionTiming(12,7) && CombatTiming.Calculate(DiabloClass.Amazon,CombatAbility.Fend,new(AttackFamily.TwoHandedThrust,0))==new ActionTiming(15,9),"Fend ordinary A1/rate or incomplete comparison");
        Log.Information("Fend:7 applicable compiled count cases,24 native modern rollback comparisons and20 rank goldens passed; isolated legacy timing observations remain separate");
    }
    private static void Cases(PlayerMobile owner,BaseWeapon spear)
    {
        Require(!ClassCombat.Available(owner,CombatAbility.Fend),"Unlearned Fend");Train(owner,"amazon.fend");Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.Fend) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Fend) && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.Fend),"Fend bindings");
        var targets=new[]{Enemy(owner),Enemy(owner),Enemy(owner)};var friend=Fresh();friend.MoveToWorld(new(owner.X,owner.Y+1,owner.Z),owner.Map);
        foreach(int input in new[]{1,2,0})
        {
            Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner),hp=SkillHealth.Balance256(targets[1]),rolls=0;AmazonFend.HitRoll=()=>{rolls++;return 0;};
            var series=Begin(owner,targets[1],input);Require(series.Remaining==3 && rolls==0 && SkillHealth.Balance256(targets[1])==hp && NecromancerEffects.ManaBalance256(owner)==mana-1280,"Count, live rather than prepared hit roll or payment");
            Require(AmazonFend.Locked(owner,Core.TickCount) && !ClassCombatHooks.MovementAllowed(owner) && !HitRecovery.Apply(owner,owner.HitsMax,Core.TickCount),"Paid sequence movement/get-hit lock");ClassCombat.Stop(owner);Require(ClassCombat.For(owner).Fend!=null,"Stop cancelled paid Fend");
            uint[] order=[targets[1].Serial.Value,targets[2].Serial.Value,targets[0].Serial.Value];
            foreach(uint id in order)
            {
                series=ClassCombat.For(owner).Fend;var target=World.FindMobile((Serial)id);Require(series.Target.Value==id && Event(owner,target,series)==Expected(owner,target) && NecromancerEffects.ManaBalance256(owner)==mana-1280,"Cyclic strike, damage or duplicate mana");
                Require(ImpaleSlow.Effect(target)==null,"Fend inherited Impale slow");
            }
            Require(rolls==3 && ClassCombat.For(owner).Fend is{Remaining:0,Released:3},"Fend event count");Finish(owner);
        }
        friend.Delete();Ready(owner);AmazonFend.HitRoll=()=>1;int before=targets.Sum(SkillHealth.Balance256),durability=spear.HitPoints;var miss=Begin(owner,targets[0]);
        for(int i=0;i<3;i++){var series=ClassCombat.For(owner).Fend;Require(Event(owner,World.FindMobile(series.Target),series)==0,"Fend miss damage");}Require(targets.Sum(SkillHealth.Balance256)==before && spear.HitPoints==durability,"Miss target life/wear");Finish(owner);
        Ready(owner);NecromancerEffects.SetMana256(owner,17);var basic=Begin(owner,targets[0]);Require(basic.Basic && basic.Remaining==1 && !AmazonFend.Locked(owner,Core.TickCount) && ClassCombatHooks.MovementAllowed(owner) && Event(owner,targets[0],basic)==0 && NecromancerEffects.ManaBalance256(owner)==17,"Ordinary low-mana miss fallback");Finish(owner);
        Ready(owner);NecromancerEffects.SetMana256(owner,17);AmazonFend.HitRoll=()=>0;basic=Begin(owner,targets[0]);Require(basic.Basic && Event(owner,targets[0],basic)==Expected(owner,targets[0],true),"Basic fallback retained Fend ED");Finish(owner);
        foreach(var target in targets){target.Delete();}
        // Count12 is captured at start; later loss can legitimately wrap onto a
        // sole remaining enemy, rather than inventing a once-per-victim rule.
        targets=Enumerable.Range(0,13).Select(_=>Enemy(owner)).ToArray();Ready(owner);var capped=Begin(owner,targets[0]);Require(capped.Remaining==12,"Twelve-strike cap");
        foreach(var target in targets.Skip(1)){target.Delete();}int hit=0;
        while(ClassCombat.For(owner).Fend is{Remaining:>0} current){Require(Event(owner,targets[0],current)>0,"Sole survivor wrap");hit++;}Require(hit==12,"Captured count changed after target loss");Finish(owner);targets[0].Delete();
        targets=new[]{Enemy(owner),Enemy(owner),Enemy(owner)};Ready(owner);var attack=Begin(owner,targets[0]);targets[0].Delete();int life=SkillHealth.Balance256(targets[1]);Event(owner,targets[1],attack);Require(SkillHealth.Balance256(targets[1])<life && ClassCombat.For(owner).Fend.Target==targets[2].Serial,"Dead current target not replaced/cycled");Ready(owner);
        attack=Begin(owner,targets[1]);targets[1].MoveToWorld(new(owner.X+10,owner.Y,owner.Z),owner.Map);Require(Event(owner,targets[2],attack)>0,"Moving target not replaced");Ready(owner);targets[1].Delete();targets[2].Delete();
        var one=Enemy(owner);Ready(owner);attack=Begin(owner,one);one.Delete();Require(ClassCombat.ResolveAction(owner,attack.EffectAt) && ClassCombat.For(owner).Fend is{Remaining:0},"Empty field did not end sequence");Finish(owner);
        one=Enemy(owner);Ready(owner);attack=Begin(owner,one);ClassMovement.Moved(owner,Direction.South);Require(ClassCombat.For(owner).Fend==null && !ClassCombat.ResolveAction(owner,attack.EffectAt),"External movement cancellation");
        Ready(owner);attack=Begin(owner,one);owner.AddToBackpack(spear);Require(!ClassCombat.ResolveAction(owner,attack.EffectAt),"Weapon removal cancellation");Require(owner.EquipItem(spear),"Spear restore");ClassGear.Refresh(owner);
        var sibling=Enemy(owner);Ready(owner);spear.HitPoints=1;AmazonFend.WearRoll=new Rolls(0);attack=Begin(owner,one);int siblingLife=SkillHealth.Balance256(sibling);Require(spear.HitPoints==1 && Event(owner,one,attack)>0 && spear.HitPoints==0 && ClassCombat.For(owner).Fend==null && SkillHealth.Balance256(sibling)==siblingLife && !ClassCombat.Available(owner,CombatAbility.Fend),"Ordinary release break did not cancel later strikes");spear.HitPoints=spear.MaxHitPoints;ClassGear.Refresh(owner);AmazonFend.WearRoll=new Rolls(99);sibling.Delete();
        var origin=owner.Location;Ready(owner);owner.MoveToWorld(new(1432,1697,0),Map.Trammel);one.Delete();one=Enemy(owner);int balance=NecromancerEffects.ManaBalance256(owner);Require(!ClassCombat.Perform(owner,CombatAbility.Fend,one,one.Location,Core.TickCount) && NecromancerEffects.ManaBalance256(owner)==balance,"Town payment gate");one.Delete();owner.MoveToWorld(origin,Map.Trammel);
    }
    private static void ArBoundary(PlayerMobile owner)
    {
        var target=Enemy(owner,difficulty:MonsterDifficulty.Hell);Train(owner,"amazon.fend",20);Ready(owner);
        int defense=NamedCombat.DefenseFor(owner,target,ClassStats.Defense(target),false);double normal=ClassStats.HitChance(ClassStats.AttackRatingAgainst(owner,target,0),defense,ClassStats.Level(owner),ClassStats.Level(target));double enhanced=ClassStats.HitChance(ClassStats.AttackRatingAgainst(owner,target,(int)SkillMechanics.Values(owner,CombatAbility.Fend).Formula("toht")),defense,ClassStats.Level(owner),ClassStats.Level(target));
        Require(enhanced>normal,"Fend AR boundary unavailable");double mid=(normal+enhanced)/2;AmazonFend.HitRoll=()=>mid;Require(ClassStats.CheckHit(owner,target,0,()=>mid)==false,"Ordinary hit control");var series=Begin(owner,target);Require(Event(owner,target,series)>0,"Fend do13 omitted skill AR");Finish(owner);
        Ready(owner);NecromancerEffects.SetMana256(owner,17);series=Begin(owner,target);Require(series.Basic && Event(owner,target,series)==0 && NecromancerEffects.ManaBalance256(owner)==17,"Basic fallback retained skill AR");Finish(owner);AmazonFend.HitRoll=()=>0;target.Delete();
    }
    private static (BaseWeapon Weapon,Item Helm) Gear(PlayerMobile owner,BaseWeapon spear)
    {
        Ready(owner);owner.AddToBackpack(spear);var weapon=(BaseWeapon)Named(owner,"Titan's Revenge");var helm=Named(owner,"Tarnhelm");Named(owner,"Highlord's Wrath");Train(owner,"amazon.fend",20);Train(owner,"amazon.criticalstrike",20);
        var first=Enemy(owner);var second=Enemy(owner);Ready(owner);int qty=NamedAmmunition.Remaining(weapon),hp=weapon.HitPoints;AmazonFend.Effects=new Rolls(0);var series=Begin(owner,first);Require(weapon.HitPoints==hp && NamedAmmunition.Remaining(weapon)==qty,"Fend inherited Impale start wear/ammo");
        int rank=SkillMechanics.Rank(owner,CombatAbility.Fend);owner.AddToBackpack(helm);ClassGear.Refresh(owner);Require(SkillMechanics.Rank(owner,CombatAbility.Fend)<rank,"Actual soft-rank removal");int expected=Expected(owner,first);AmazonFend.WearRoll=new Rolls(0);
        Require(Event(owner,first,series)==expected && weapon.HitPoints==hp-1 && AmazonCombat.CriticalChance(owner)>0 && ClassGear.Bonus(owner,"deadly")>0,"Live strike rank/gear/ordinary critical/wear");
        Require(NamedAmmunition.Remaining(weapon)==qty && !WeaponHands.Melee(weapon),"Javelin melee quantity/scope leak");
        AmazonFend.HitRoll=()=>1;hp=weapon.HitPoints;series=ClassCombat.For(owner).Fend;Require(Event(owner,second,series)==0 && weapon.HitPoints==hp,"Later strike miss reused previous result/wear");Finish(owner);first.Delete();second.Delete();AmazonFend.HitRoll=()=>0;AmazonFend.WearRoll=new Rolls(99);AmazonFend.Effects=new Rolls(99);
        var other=Fresh();Equip(other,new Spear());Train(other,"amazon.fend");var target=Enemy(other);Ready(other);var pending=Begin(other,target);ClassCreation.Profiles.TryGet(other.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(other,profile);Require(ClassCombat.For(other).Fend==null && !ClassCombat.ResolveAction(other,pending.EffectAt),"Respec retained Fend");
        Equip(other,new Spear());Train(other,"amazon.fend");Ready(other);pending=Begin(other,target);other.Kill();Require(ClassCombat.For(other).Fend==null && !ClassCombat.ResolveAction(other,pending.EffectAt),"Death retained Fend");other.Delete();target.Delete();return(weapon,helm);
    }
    private static void Persist(PlayerMobile owner,BaseWeapon weapon,Item helm)
    {
        var target=Enemy(owner);var second=Enemy(owner);Ready(owner);var pending=Begin(owner,target);Require(Event(owner,target,pending)>0 && ClassCombat.For(owner).Fend is{Remaining:1},"Saved in-progress sequence fixture");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,weapon.Serial.Value,helm.Serial.Value,target.Serial.Value,owner.Location,NecromancerEffects.ManaBalance256(owner),SkillHealth.Balance256(target),weapon.HitPoints,NamedAmmunition.Remaining(weapon),SkillMechanics.Rank(owner,CombatAbility.Fend))));
    }
    private static void Check(){Differential();var owner=Fresh();var spear=Equip(owner,new Spear());Cases(owner,spear);ArBoundary(owner);var gear=Gear(owner,spear);Persist(owner,gear.Weapon,gear.Helm);}
    private static void Natural()
    {
        var owner=Fresh();Equip(owner,new Spear());Train(owner,"amazon.fend");Ready(owner);bool placed=false;
        for(int x=2500;x<=2800 && !placed;x+=50)for(int y=1000;y<=1300 && !placed;y+=50)
        {
            Point3D point;try{point=ClassSkillChecks.Corridor(owner,x,y);}catch(InvalidOperationException){continue;}
            if(SpellHelper.IsTown(point,owner)){continue;}bool clear=true;
            foreach(var other in owner.Map.GetMobilesInRange<Mobile>(point,10)){if(other!=owner){clear=false;break;}}
            if(clear){owner.MoveToWorld(point,owner.Map);placed=true;}
        }
        Require(placed,"No isolated natural Fend corridor");
        BaseCreature LoopEnemy(){BaseCreature target;_creating=true;try{target=new Ogre{Name="FendTarget",NoKillAwards=true,Frozen=true,CantWalk=true};}finally{_creating=false;}target.SetHits(50000);target.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);target.AIObject.AITimer.Stop();return target;}
        var targets=new[]{LoopEnemy(),LoopEnemy(),LoopEnemy()};int[] life=targets.Select(SkillHealth.Balance256).ToArray();int mana=NecromancerEffects.ManaBalance256(owner),rolls=0;
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=owner;owner.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Network bootstrap");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(owner);
        var oldHit=AmazonFend.HitRoll;var oldElement=AmazonFend.ElementRoll;var oldPhysical=AmazonFend.PhysicalRoll;var oldEffects=AmazonFend.Effects;var oldWear=AmazonFend.WearRoll;
        AmazonFend.HitRoll=()=>{rolls++;return 0;};AmazonFend.ElementRoll=()=>0;AmazonFend.PhysicalRoll=_=>0;AmazonFend.Effects=new Rolls(99);AmazonFend.WearRoll=new Rolls(99);
        void Cleanup(){AmazonFend.HitRoll=oldHit;AmazonFend.ElementRoll=oldElement;AmazonFend.PhysicalRoll=oldPhysical;AmazonFend.Effects=oldEffects;AmazonFend.WearRoll=oldWear;network.Mobile=null;owner.NetState=null;owner.Delete();foreach(var target in targets){target.Delete();}}
        long began=Core.TickCount,deadline=began+5000;AmazonFend.Series initial;
        try{initial=Begin(owner,targets[0]);Require(initial.Remaining==3,"Natural count");}catch{Cleanup();throw;}
        void Poll()
        {
            try
            {
                if(ClassCombat.For(owner).Fend==null)
                {
                    Require(Core.TickCount>=initial.EffectAt && rolls==3 && NecromancerEffects.ManaBalance256(owner)==mana-1280 && targets.Select((t,i)=>SkillHealth.Balance256(t)<life[i]).All(x=>x),"Natural connected Fend count/targets/mana: rolls="+rolls+",mana="+NecromancerEffects.ManaBalance256(owner)+",expected="+(mana-1280)+",life="+string.Join(",",targets.Select((t,i)=>life[i]+"->"+SkillHealth.Balance256(t))));Cleanup();Log.Information("fend-loop-result: passed");return;
                }
                Require(Core.TickCount<deadline,"Natural Fend timeout");Timer.StartTimer(TimeSpan.FromMilliseconds(50),Poll);
            }
            catch(Exception ex){Cleanup();Log.Error(ex,"fend-loop-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(50),Poll);
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var weapon=World.FindItem((Serial)saved.Weapon) as BaseWeapon;var target=World.FindMobile((Serial)saved.Target) as BaseCreature;
        Require(owner!=null && weapon?.Parent==owner && target!=null && NecromancerEffects.ManaBalance256(owner)==saved.Mana && SkillHealth.Balance256(target)==saved.Life && weapon.HitPoints==saved.Durability && NamedAmmunition.Remaining(weapon)==saved.Quantity && SkillMechanics.Rank(owner,CombatAbility.Fend)==saved.Rank,"Saved Fend resources/gear/rank/life");
        Require(ClassCombat.Existing(owner)?.Fend==null && ClassCombat.Existing(owner)?.PendingAbility is null or CombatAbility.None && SkillBindings.Instance.Get(owner).Left==CombatAbility.Fend && World.FindItem((Serial)saved.Helm)?.Parent==owner.Backpack,"Transient Fend replayed or saved binding/item missing");
        owner.MoveToWorld(saved.Origin,Map.Trammel);target.Frozen=target.CantWalk=true;target.AIObject.AITimer.Stop();Ready(owner);var pending=Begin(owner,target);Require(Event(owner,target,pending)>0,"Post-load Fend failed");AmazonFend.Cancel(owner);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Mana=NecromancerEffects.ManaBalance256(owner),Life=SkillHealth.Balance256(target),Durability=weapon.HitPoints,Quantity=NamedAmmunition.Remaining(weapon)}));
    }
}
