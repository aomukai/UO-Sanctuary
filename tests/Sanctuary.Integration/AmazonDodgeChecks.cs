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

public static class AmazonDodgeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AmazonDodgeChecks));
    private sealed record Saved(uint Owner,uint Helm,uint Amulet,uint Weapon,Point3D Origin,int Mana);
    private static bool _creating;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"amazon-dodge-fixtures.json");
    public static void Configure(){Freeze();Register("amazon-dodge-check",Check);Register("amazon-dodge-reload-check",Reload);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Freeze();
    private static void Freeze()
    {
        var mp=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=a=>_creating || a.Name=="AmazonDodgeFixture"?TimeSpan.FromHours(1):mp?.Invoke(a)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Dodge source dispatch, real gear, paid attack continuity and persistence",_=>Core.LoopContext.Post(()=>
    {
        var roll=AmazonAvoidance.Roll;
        try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}
        finally{AmazonAvoidance.Roll=roll;}
    }));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Name="AmazonDodgeFixture",Player=true};}finally{_creating=false;}
        owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);
        while(owner.RawStr<110){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Strength");}
        while(owner.RawDex<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Dexterity");}
        FixtureSpace.Place(owner,new(2450,1250,0),Map.Trammel);owner.Hits=owner.HitsMax;owner.Mana=0;return owner;
    }
    private static void Train(PlayerMobile owner,string id,int rank=1)
    {
        while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank)
        {
            Require(TalentCatalog.TryGet(DiabloClass.Amazon,id,out var talent),"Talent "+id);
            foreach(var prerequisite in talent.Prerequisites){Train(owner,prerequisite);}
            Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Training "+id);
        }
    }
    private static Item Named(PlayerMobile owner,string source)
    {
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);
        Require(owner.EquipItem(item),"Named equip "+source);ClassGear.Refresh(owner);return item;
    }
    private static void Stationary(PlayerMobile owner)
    {var s=ClassCombat.For(owner);s.RunningUntil=Core.TickCount-1;s.RunningStep=false;}
    private static BaseCreature Enemy(PlayerMobile owner)
    {
        var enemy=new Ogre{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.AIObject.AITimer.Stop();
        enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);return enemy;
    }
    private static void Goldens()
    {
        var source=SkillMechanics.For(CombatAbility.Dodge);
        Require(source.Passive && source.Text("passivestat1")=="passive_dodge" && source.Parameter(8)==2
            && !source.SourceFields.ContainsKey("passiveitype"),"Pinned Dodge metadata/interruptible animation");
        int[] expected=[18,24,29,34,37,40,42,44,46,47,49,50,51,52,52,54,54,55,55,56];
        for(int i=0;i<expected.Length;i++)
        {int rank=i+1;Require(new SkillValues(source,_=>rank,_=>0,99).Eval("passivecalc1")==expected[i],"Official Dodge chance rank "+rank);}
    }
    private static void Differential(PlayerMobile owner,Mobile enemy)
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"amazon-dodge-source-probe.json")));
        int count=0;
        foreach(int rank in new[]{0,1,2,20})
        {
            if(rank>0){Train(owner,"amazon.dodge",rank);}
            int chance=rank==0?0:rank==1?18:rank==2?24:56;
            Require(AmazonAvoidance.DodgeChance(owner)==chance && ClassGear.BlockChance(owner)==0,"Source chance/shield contrast");
            foreach(var row in data.RootElement.EnumerateArray())
            {
                if(row[2].GetInt32()!=chance || row[4].GetInt32()!=0 || row[5].GetInt32()!=1){continue;}
                int moving=row[0].GetInt32(),draw=row[3].GetInt32(),calls=0;bool projectile=row[1].GetInt32()!=0;
                var s=ClassCombat.For(owner);s.RunningStep=moving==2;s.RunningUntil=Core.TickCount+(moving>0?1000:-1);s.StepPosition=owner.Location;s.StepMap=owner.Map;
                AmazonAvoidance.Roll=maximum=>{Require(maximum==100,"Dodge RNG range");calls++;return draw;};
                bool result=ClassStats.CheckHit(enemy,owner,0,()=>0,projectile)==true;
                Require(result==(row[6].GetInt32()==0) && calls==row[7].GetInt32(),"Compiled Dodge dispatch/strict threshold differential");count++;
            }
        }
        Stationary(owner);Require(count==240,"Incomplete native/source Dodge matrix");
        Log.Information("Dodge:240 native dispatch/source comparisons and20 official rank goldens passed");
    }
    private static void Consumers(PlayerMobile owner,BaseWeapon spear)
    {
        Stationary(owner);var enemy=Enemy(owner);var s=ClassCombat.For(owner);int calls=0;
        try
        {
            AmazonAvoidance.Roll=_=>{calls++;return 0;};
            Require(ClassStats.CheckHit(enemy,owner,0,()=>.999)==false && calls==0,"Ordinary AR miss rolled Dodge");
            Require(ClassStats.CheckHit(enemy,owner,0,()=>0,projectile:true)==true && calls==0,"Projectile used melee Dodge");
            int life=SkillHealth.Balance256(owner);var fists=(BaseWeapon)enemy.Weapon;
            enemy.Frozen=false;
            for(int i=0;i<1000 && calls<4;i++){fists.OnSwing(enemy,owner);}
            enemy.Frozen=true;
            Require(calls>0 && SkillHealth.Balance256(owner)==life,"Native ordinary weapon bypassed Dodge");
            // A real ranged native weapon enters the public hook, not the
            // explicit projectile overload used by the saved missile controller.
            enemy.AddItem(new Bow());calls=0;
            for(int i=0;i<100;i++){ClassStats.CheckHit(enemy,owner);}
            Require(calls==0,"Native ranged weapon used Dodge");
            enemy.FindItemOnLayer(Layer.TwoHanded)?.Delete();
            owner.AddToBackpack(spear);ClassGear.Refresh(owner);
            var shield=new WoodenShield();ClassGear.Profiles.Convert(shield,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(shield),"Shield equip");ClassGear.Refresh(owner);
            Require(ClassGear.BlockChance(owner)>0,"Real shield block chance");calls=0;s.RecoveryUntil=Core.TickCount-1;
            Require(ClassStats.CheckHit(enemy,owner,0,()=>0)==false && calls==0,"Successful shield block also rolled Dodge");
            s.RecoveryUntil=Core.TickCount-1;int draws=0;
            Require(ClassStats.CheckHit(enemy,owner,0,()=>draws++==0?0:.999)==false && calls==1,"Failed shield block did not reach Dodge");
            shield.Delete();Require(owner.EquipItem(spear),"Restore spear after shield");ClassGear.Refresh(owner);s.RecoveryUntil=s.NextAction=Core.TickCount-1;
            Train(owner,"amazon.fend");NecromancerEffects.SetMana256(owner,30*256+17);
            Require(ClassCombat.Perform(owner,CombatAbility.Fend,enemy,enemy.Location,Core.TickCount),"Prepare paid Fend");
            var series=s.Fend;long end=s.NextAction,effect=s.EffectAt,recovery=s.RecoveryUntil;int mana=NecromancerEffects.ManaBalance256(owner);calls=0;
            Require(series is{Basic:false} && ClassStats.CheckHit(enemy,owner,0,()=>0)==false && calls==1,"Dodge during paid Fend");
            Require(ReferenceEquals(s.Fend,series) && s.PendingAbility==CombatAbility.Fend && s.NextAction==end && s.EffectAt==effect
                && s.RecoveryUntil==recovery && NecromancerEffects.ManaBalance256(owner)==mana,"Dodge cancelled/repaid/delayed Fend");
            Require(ClassCombat.ResolveAction(owner,series.EffectAt),"Fend release after Dodge");AmazonFend.Cancel(owner);ClassCombat.Stop(owner);
            // Moving selection/failed steps alone do not activate a movement
            // lease. Accepted walk and run steps do, until their deadline.
            s.NextAction=s.RecoveryUntil=Core.TickCount-1;Stationary(owner);calls=0;
            Require(ClassStats.CheckHit(enemy,owner,0,()=>0)==false && calls==1,"Standing Run selection suppressed Dodge");
            foreach(var direction in new[]{Direction.East,Direction.East|Direction.Running})
            {
                ClassMovement.Moved(owner,direction);calls=0;
                Require(ClassMovement.IsMoving(owner,Core.TickCount) && ClassStats.CheckHit(enemy,owner,0,()=>0)==true && calls==0,"Accepted movement used standing Dodge");
                Require(!AmazonAvoidance.Dodge(owner,false,s.RunningUntil-1),"Moving lease ended early");
                Require(AmazonAvoidance.Dodge(owner,false,s.RunningUntil),"Moving lease failed to expire");
            }
            Stationary(owner);s.NextAction=s.RecoveryUntil=Core.TickCount-1;owner.Mana=0;
        }
        finally{enemy.Delete();AmazonFend.Cancel(owner);ClassCombat.Stop(owner);}
    }
    private static void Check()
    {
        Goldens();var owner=Fresh();var enemy=Enemy(owner);var helm=Named(owner,"Tarnhelm");
        Require(AmazonAvoidance.DodgeChance(owner)==0,"Allskills granted unlearned Dodge");
        owner.AddToBackpack(helm);ClassGear.Refresh(owner);Differential(owner,enemy);enemy.Delete();
        Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.Dodge) && !SkillBindings.Instance.Assign(owner,false,CombatAbility.Dodge)
            && !SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.Dodge),"Passive accepted active binding");
        Require(!ClassCreation.Profiles.TryTrain(owner.Serial,"amazon.dodge",out _),"Hard rank cap");
        var spear=new Spear();ClassGear.Profiles.Convert(spear,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(spear),"Spear equip");
        Require(owner.EquipItem(helm),"Helm restore");var amulet=Named(owner,"Highlord's Wrath");Require(AmazonAvoidance.DodgeChance(owner)==57,"Real hard20/soft22 Dodge");
        Consumers(owner,spear);Require(AmazonAvoidance.DodgeChance(owner)==57 && owner.Mana==0,"Consumers changed passive/mana");
        var other=Fresh();Train(other,"amazon.dodge");Require(AmazonAvoidance.DodgeChance(other)==18,"Rank1 native chance");
        Require(ClassCreation.Profiles.TryActivateHistory(other,DiabloClass.Paladin) && AmazonAvoidance.DodgeChance(other)==0,"Wrong-class Dodge");
        Require(ClassCreation.Profiles.TryActivateHistory(other,DiabloClass.Amazon) && AmazonAvoidance.DodgeChance(other)==18,"History restored Dodge");
        ClassCreation.Profiles.TryGet(other.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(other,profile);Require(AmazonAvoidance.DodgeChance(other)==0,"Respec retained Dodge");other.Delete();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,helm.Serial.Value,amulet.Serial.Value,spear.Serial.Value,owner.Location,NecromancerEffects.ManaBalance256(owner))));
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        Require(owner!=null && World.FindItem((Serial)saved.Helm)?.Parent==owner && World.FindItem((Serial)saved.Amulet)?.Parent==owner
            && World.FindItem((Serial)saved.Weapon) is BaseWeapon spear && spear.Parent==owner && ClassCreation.Profiles.GetRank(owner.Serial,"amazon.dodge")==20,"Saved Dodge rank/gear");
        if(owner.Map==Map.Internal){owner.MoveToWorld(saved.Origin,Map.Trammel);}
        Require(AmazonAvoidance.DodgeChance(owner)==57 && NecromancerEffects.ManaBalance256(owner)==saved.Mana,"Saved passive/resource");
        Require(ClassCombat.For(owner).Fend==null && ClassCombat.For(owner).PendingAbility==CombatAbility.None,"Transient attack survived restart");
        Consumers(owner,(BaseWeapon)World.FindItem((Serial)saved.Weapon));
    }
}
