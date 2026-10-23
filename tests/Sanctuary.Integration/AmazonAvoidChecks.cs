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

public static class AmazonAvoidChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AmazonAvoidChecks));
    private sealed record Saved(uint Owner,uint Helm,uint Amulet,uint Weapon,Point3D Origin,int Mana);
    private static bool _creating;
    private static Func<int,int> _original;
    private static PlayerMobile _owner;
    private static Item _helm,_amulet;
    private static BaseWeapon _weapon;
    private static BaseCreature _source;
    private static int _phase,_draw,_calls,_before,_mana;
    private static long _phaseUntil;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"amazon-avoid-fixtures.json");
    public static void Configure(){Freeze();Register("amazon-avoid-check",Check);Register("amazon-avoid-reload-check",Reload);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Freeze();
    private static void Freeze()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=a=>_creating || a.Name=="AmazonAvoidFixture"?TimeSpan.FromHours(1):hp?.Invoke(a)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=a=>_creating || a.Name=="AmazonAvoidFixture"?TimeSpan.FromHours(1):mp?.Invoke(a)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Avoid source dispatch, live enemy projectiles and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Cleanup();Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Cleanup()
    {
        if(_source is EncounterCreature encounter){EliteBolts.Cancel(encounter);}
        _source?.Delete();_source=null;if(_original!=null){AmazonAvoidance.Roll=_original;_original=null;}
    }
    private static void Later(Action action)=>Timer.StartTimer(TimeSpan.FromSeconds(1.1),()=>
    {try{action();}catch(Exception e){Cleanup();Log.Error(e,"amazon-avoid-result: failed");}});
    private static PlayerMobile Fresh()
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Name="AmazonAvoidFixture",Player=true};}finally{_creating=false;}
        owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);
        while(owner.RawStr<110){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Strength");}
        while(owner.RawDex<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Dexterity");}
        // Find a legal projectile corridor independent of other saved fixtures.
        for(int x=2450;x<2510;x++)for(int y=1200;y<1260;y++)
        {
            var p=CombatPractice.FindFloor(Map.Trammel,x,y);var q=CombatPractice.FindFloor(Map.Trammel,x+4,y);
            if(p.Z!=q.Z || !Teleportation.Space(Map.Trammel,p,owner) || !Map.Trammel.CanSpawnMobile(q)
                || !Map.Trammel.LineOfSight(new Point3D(x,y,p.Z+12),new Point3D(x+4,y,p.Z+12))){continue;}
            owner.MoveToWorld(p,Map.Trammel);owner.Hits=owner.HitsMax;owner.Mana=0;return owner;
        }
        throw new InvalidOperationException("No Avoid fixture corridor");
    }
    private static void Train(PlayerMobile owner,string id,int rank=1)
    {
        while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank)
        {
            Require(TalentCatalog.TryGet(DiabloClass.Amazon,id,out var talent),"Talent "+id);
            foreach(var prerequisite in talent.Prerequisites){Train(owner,prerequisite);}
            Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);
        }
    }
    private static Item Named(PlayerMobile owner,string name)
    {
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==name).Id,99,new Random(173),true);
        Require(owner.EquipItem(item),"Named equip "+name);ClassGear.Refresh(owner);return item;
    }
    private static void Stationary(PlayerMobile owner)
    {var s=ClassCombat.For(owner);s.RunningUntil=Core.TickCount-1;s.RunningStep=false;s.RecoveryUntil=s.NextAction=Core.TickCount-1;}
    private static void Mode(PlayerMobile owner,int moving)
    {var s=ClassCombat.For(owner);s.RunningStep=moving==2;s.RunningUntil=Core.TickCount+(moving>0?1000:-1);s.StepPosition=owner.Location;s.StepMap=owner.Map;}
    private static BaseCreature Enemy(PlayerMobile owner)
    {
        var enemy=new Ogre{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.AIObject.AITimer.Stop();
        enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);return enemy;
    }
    private static void Goldens()
    {
        var source=SkillMechanics.For(CombatAbility.Avoid);
        Require(source.Passive && source.Text("passivestat1")=="passive_avoid" && source.Parameter(8)==2,"Pinned Avoid/interruptible animation");
        int[] chances=[24,31,36,41,45,48,50,52,54,55,57,58,60,61,61,63,63,64,64,65];
        for(int i=0;i<chances.Length;i++){int rank=i+1;Require(new SkillValues(source,_=>rank,_=>0,99).Eval("passivecalc1")==chances[i],"Official Avoid chance "+rank);}
        Require(chances[2]==36,"Current user effective rank3=36");
    }
    private static void Differential()
    {
        var owner=Fresh();var enemy=Enemy(owner);var prior=AmazonAvoidance.Roll;int dispatch=0,flags=0,areas=0;
        try
        {
            using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"amazon-avoid-source-probe.json")));
            foreach(int rank in new[]{0,1,2,3,20})
            {
                if(rank>0){Train(owner,"amazon.avoid",rank);}
                int chance=rank==0?0:rank==1?24:rank==2?31:rank==3?36:65;
                Require(AmazonAvoidance.AvoidChance(owner)==chance && ClassGear.BlockChance(owner)==0,"Native Avoid rank/no shield");
                foreach(var row in data.RootElement.GetProperty("dispatch").EnumerateArray())
                {
                    if(row[1].GetInt32()!=1 || row[2].GetInt32()!=chance || row[4].GetInt32()!=0 || row[5].GetInt32()!=1){continue;}
                    int calls=0;AmazonAvoidance.Roll=max=>{Require(max==100,"Avoid RNG range");calls++;return row[3].GetInt32();};Mode(owner,row[0].GetInt32());
                    Require((ClassStats.CheckHit(enemy,owner,0,()=>0,projectile:true)==true)==(row[6].GetInt32()==0) && calls==row[7].GetInt32(),"Compiled ranged dispatch differential");dispatch++;
                }
                foreach(var row in data.RootElement.GetProperty("flags").EnumerateArray())
                {
                    if(row[1].GetInt32()!=chance || row[2].GetInt32()!=0 || row[3].GetInt32()!=0 || row[6].GetInt32()!=0 || row[7].GetInt32()!=0){continue;}
                    int calls=0;AmazonAvoidance.Roll=_=>{calls++;return row[8].GetInt32();};Mode(owner,row[0].GetInt32());
                    Require(AmazonAvoidance.Missile(owner)==((row[9].GetInt32()&1)==0) && calls==row[12].GetInt32(),"Compiled spell defense/soft-hit differential");flags++;
                }
                foreach(var row in data.RootElement.GetProperty("area").EnumerateArray())
                {
                    if(row[1].GetInt32()!=chance || row[3].GetInt32()!=5 || row[4].GetInt32()!=0 || row[5].GetInt32()!=0){continue;}
                    int calls=0;AmazonAvoidance.Roll=_=>{calls++;return row[2].GetInt32();};Mode(owner,row[0].GetInt32());
                    Require(AmazonAvoidance.Missile(owner)==((row[6].GetInt32()&1)==0) && calls==row[9].GetInt32() && row[10].GetInt32()==5,"Compiled copied-area defense differential");areas++;
                }
            }
            Require(dispatch==150 && flags==480 && areas==120,"Incomplete native/source Avoid matrix");
            Require(!ClassCreation.Profiles.TryTrain(owner.Serial,"amazon.avoid",out _),"Hard rank cap");
        }
        finally{AmazonAvoidance.Roll=prior;enemy.Delete();owner.Delete();}
        Log.Information("Avoid:150 ranged dispatch,480 spell flags and120 copied-area native/source comparisons;20 rank goldens passed");
    }
    private static void Consumers(PlayerMobile owner,BaseWeapon spear)
    {
        var enemy=Enemy(owner);var prior=AmazonAvoidance.Roll;Stationary(owner);
        try
        {
            owner.AddToBackpack(spear);var shield=new WoodenShield();ClassGear.Profiles.Convert(shield,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(shield),"Shield equip");ClassGear.Refresh(owner);
            Require(ClassGear.BlockChance(owner)>0,"Real shield chance");int calls=0;AmazonAvoidance.Roll=_=>{calls++;return 0;};
            Require(ClassStats.CheckHit(enemy,owner,0,()=>0,projectile:true)==false && calls==0,"Shield success rolled Avoid");
            Stationary(owner);int draws=0;
            Require(ClassStats.CheckHit(enemy,owner,0,()=>draws++==0?0:.999,projectile:true)==false && calls==1,"Failed shield did not roll Avoid exactly once");
            owner.Hits=owner.HitsMax;int hp=SkillHealth.Balance256(owner);calls=0;
            Require(SkillCombat.Hit(enemy,owner,10,"fire",missileDefense:true)==0 && SkillHealth.Balance256(owner)==hp && calls==1,"Spell Avoid failed/no double draw");
            AmazonAvoidance.Roll=_=>{calls++;return 99;};calls=0;
            Require(SkillCombat.Hit(enemy,owner,10,"fire",missileDefense:true)>0 && calls==1,"Pure spell incorrectly used shield block");
            AmazonAvoidance.Roll=_=>{calls++;return 0;};calls=0;hp=SkillHealth.Balance256(owner);
            SkillCombat.HitParts(enemy,owner,new(4,4,0,0,0),true,spell:true);
            Require(SkillHealth.Balance256(owner)<hp && calls==0,"Direct CE-style area damage acquired global Avoid");
            calls=0;hp=SkillHealth.Balance256(owner);SkillHealth.Damage(enemy,owner,256);
            Require(SkillHealth.Balance256(owner)==hp-256 && calls==0,"Direct health/DoT-style commitment acquired Avoid");
            shield.Delete();Require(owner.EquipItem(spear),"Spear restore");ClassGear.Refresh(owner);Train(owner,"amazon.fend");Stationary(owner);NecromancerEffects.SetMana256(owner,30*256+17);
            Require(ClassCombat.Perform(owner,CombatAbility.Fend,enemy,enemy.Location,Core.TickCount),"Paid Fend begin");var s=ClassCombat.For(owner);var series=s.Fend;
            Require(series is{Basic:false},"Fend state");int mana=NecromancerEffects.ManaBalance256(owner);long end=s.NextAction,effect=s.EffectAt,recovery=s.RecoveryUntil;calls=0;
            Require(SkillCombat.Hit(enemy,owner,10,"fire",missileDefense:true)==0 && calls==1 && ReferenceEquals(s.Fend,series)
                && s.PendingAbility==CombatAbility.Fend && s.NextAction==end && s.EffectAt==effect && s.RecoveryUntil==recovery
                && NecromancerEffects.ManaBalance256(owner)==mana,"Avoid interrupted/repaid/delayed paid Fend");
            Require(ClassCombat.ResolveAction(owner,series.EffectAt),"Fend release after Avoid");AmazonFend.Cancel(owner);ClassCombat.Stop(owner);Stationary(owner);
            calls=0;Require(ClassStats.CheckHit(enemy,owner,0,()=>.999,projectile:true)==false && calls==0,"AR miss rolled Avoid");
            foreach(int moving in new[]{1,2})
            {
                Mode(owner,moving);calls=0;Require(!AmazonAvoidance.Missile(owner) && calls==0,"Moving used standing Avoid");
                var state=ClassCombat.For(owner);state.RunningUntil=Core.TickCount-1;
                Require(AmazonAvoidance.Missile(owner) && calls==1,"Expired walking/running lease kept Avoid disabled");
            }
            Stationary(owner);owner.Hits=owner.HitsMax;owner.Mana=0;
        }
        finally{AmazonAvoidance.Roll=prior;AmazonFend.Cancel(owner);ClassCombat.Stop(owner);enemy.Delete();}
    }
    private static void Check()
    {
        Goldens();Differential();var owner=Fresh();var helm=Named(owner,"Tarnhelm");
        Require(AmazonAvoidance.AvoidChance(owner)==0,"Allskills granted unlearned Avoid");Train(owner,"amazon.avoid");
        Require(AmazonAvoidance.AvoidChance(owner)==31,"Real effective rank2");var amulet=Named(owner,"Highlord's Wrath");Require(AmazonAvoidance.AvoidChance(owner)==36,"Current hard1/item2/rank3=36");
        Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.Avoid) && !SkillBindings.Instance.Assign(owner,false,CombatAbility.Avoid)
            && !SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.Avoid),"Passive accepted active binding");
        var spear=new Spear();ClassGear.Profiles.Convert(spear,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(spear),"Spear equip");ClassGear.Refresh(owner);Consumers(owner,spear);
        var other=Fresh();Train(other,"amazon.avoid");Require(AmazonAvoidance.AvoidChance(other)==24,"Base1 Avoid");
        Require(ClassCreation.Profiles.TryActivateHistory(other,DiabloClass.Paladin) && AmazonAvoidance.AvoidChance(other)==0,"Wrong class Avoid");
        Require(ClassCreation.Profiles.TryActivateHistory(other,DiabloClass.Amazon) && AmazonAvoidance.AvoidChance(other)==24,"History Avoid");
        ClassCreation.Profiles.TryGet(other.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(other,profile);Require(AmazonAvoidance.AvoidChance(other)==0,"Respec Avoid");other.Delete();
        Natural(owner,helm,amulet,spear);
    }
    private static void Natural(PlayerMobile owner,Item helm,Item amulet,BaseWeapon weapon)
    {
        _owner=owner;_helm=helm;_amulet=amulet;_weapon=weapon;_phase=0;_original=AmazonAvoidance.Roll;
        AmazonAvoidance.Roll=max=>{Require(max==100,"Natural Avoid RNG range");_calls++;return _draw;};Next();
    }
    private static int WithoutItems()
    {
        int rank=ClassCreation.Profiles.GetRank(_owner.Serial,"amazon.avoid");
        return (int)new SkillValues(SkillMechanics.For(CombatAbility.Avoid),_=>rank,_=>0,99).Eval("passivecalc1");
    }
    private static void Next()
    {
        var owner=_owner;Stationary(owner);owner.Hits=owner.HitsMax;owner.Mana=0;_before=SkillHealth.Balance256(owner);_mana=NecromancerEffects.ManaBalance256(owner);_calls=0;
        _phaseUntil=Core.TickCount+6000;
        var point=new Point3D(owner.X+4,owner.Y,owner.Z);
        if(_phase<2)
        {
            if(_phase==0){Require(_helm.Parent==owner && _amulet.Parent==owner,"Initial named ranks");}
            else{Require(_helm.Parent!=owner && _amulet.Parent!=owner,"Removed named ranks");}
            _draw=WithoutItems();var lich=new GraveyardLich{CantWalk=true,NoKillAwards=true,Name="AvoidFixtureCaster"};lich.AIObject.AITimer.Stop();lich.MoveToWorld(point,owner.Map);_source=lich;
            Require(lich.BeginFirebolt(owner),"Natural hostile firebolt begin");
            // Change actual items after launch scheduling. Defense is sampled
            // when the delayed impact arrives, not captured at cast start.
            if(_phase==0){owner.AddToBackpack(_helm);owner.AddToBackpack(_amulet);}
            else{Require(owner.EquipItem(_helm) && owner.EquipItem(_amulet),"Live named re-equip");}
            ClassGear.Refresh(owner);
        }
        else
        {
            _draw=_phase==2?0:99;var source=new EncounterCreature{CantWalk=true,NoKillAwards=true};source.AIObject.AITimer.Stop();source.MoveToWorld(point,owner.Map);_source=source;
            Require(EliteCombat.Enemy(source,owner,18),"Natural charged-bolt enemy");EliteBolts.Lightning(source);
        }
        Later(Observe);
    }
    private static void Observe()
    {
            if(_calls==0 && Core.TickCount<_phaseUntil){Later(Observe);return;}
            var owner=_owner;
            bool avoided=_phase is 1 or 2;
            Require(_calls==1 && (avoided?SkillHealth.Balance256(owner)==_before:SkillHealth.Balance256(owner)<_before)
                && NecromancerEffects.ManaBalance256(owner)==_mana,"Natural fire/lightning live defense phase "+_phase+" calls="+_calls);
            if(_source is EncounterCreature encounter){EliteBolts.Cancel(encounter);}_source.Delete();_source=null;
            if(++_phase<4){Next();return;}
            Train(owner,"amazon.avoid",20);Require(AmazonAvoidance.AvoidChance(owner)==66 && !ClassCreation.Profiles.TryTrain(owner.Serial,"amazon.avoid",out _),"Saved hard20/soft22/cap");
            Stationary(owner);owner.Hits=owner.HitsMax;owner.Mana=0;
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,_helm.Serial.Value,_amulet.Serial.Value,_weapon.Serial.Value,owner.Location,NecromancerEffects.ManaBalance256(owner))));
            Cleanup();Log.Information("amazon-avoid-result: passed");
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        Require(owner!=null && World.FindItem((Serial)saved.Helm) is{} helm && helm.Parent==owner && World.FindItem((Serial)saved.Amulet) is{} amulet && amulet.Parent==owner
            && World.FindItem((Serial)saved.Weapon) is BaseWeapon spear && spear.Parent==owner && ClassCreation.Profiles.GetRank(owner.Serial,"amazon.avoid")==20,"Saved Avoid rank/gear");
        if(owner.Map==Map.Internal){owner.MoveToWorld(saved.Origin,Map.Trammel);}
        Require(AmazonAvoidance.AvoidChance(owner)==66 && NecromancerEffects.ManaBalance256(owner)==saved.Mana,"Saved effective rank/resource");
        Require(ClassCombat.For(owner).PendingAbility==CombatAbility.None && ClassCombat.For(owner).Fend==null,"Transient paid attack persisted");
        Consumers(owner,(BaseWeapon)World.FindItem((Serial)saved.Weapon));
        Natural(owner,World.FindItem((Serial)saved.Helm),World.FindItem((Serial)saved.Amulet),(BaseWeapon)World.FindItem((Serial)saved.Weapon));
    }
}
