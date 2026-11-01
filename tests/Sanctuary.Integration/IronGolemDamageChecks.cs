// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class IronGolemDamageChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(IronGolemDamageChecks));
    private sealed record Saved(uint Owner,uint Body,uint Legacy,uint Item,string Recipe,int Life,int LegacyLife,int Rank,int Mastery,NpcPhysicalStats Packet,long Order,ulong Seed,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"iron-golem-damage-fixtures.json");
    public static void Configure()
    {
        Register("iron-golem-damage-check",Check);Register("iron-golem-damage-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("iron-golem-damage-live","Connected learned Iron shared curve",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("iron-golem-damage-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Iron captured Clay curve/original item composition",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,string name,int rank)=>MagePetBuffChecks.Train(p,"necromancer."+name,rank);
    private static PlayerMobile Owner(int rank=9,int mastery=3,int row=3220)
    {var p=PoisonDaggerChecks.Owner();FixtureSpace.Corridor(p,6100,40,row);Train(p,"irongolem",rank);if(mastery>0){Train(p,"golemmastery",mastery);}return p;}
    private static Item Original(PlayerMobile p,string name=null)
    {Item item=name==null?new Broadsword():NpcEquipmentRegenerationChecks.Named(NamedGear.Catalog.Items.Single(x=>x.SourceKey==name||x.SourceKey=="The "+name).Id);if(name==null){ClassGear.Profiles.Ensure(item);}item.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);return item;}
    private static IronGolemBody Learned(PlayerMobile p,Item item)
    {
        NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p);
        Require(IronGolemCasting.Release(p,item,TimedEffects.Now,out var b)&&NecromancerEffects.ManaBalance256(p)==mana-35*256&&item.Parent==b,"Learned Iron payment/original identity");
        b.RestoreController(b.Prepared.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});return b;
    }
    private static void Match(IronGolemBody b,int mastery)
    {Require(b.SourceWeaponBase==SkeletonStats.WeaponBase(b.Rank,mastery,b.SummonDifficulty),"Prepared Iron captured Clay curve");}
    private static void Goldens()
    {
        foreach(var x in new[]{(Rank:1,Mastery:0,Min:1,Max:2,Percent:0),(Rank:4,Mastery:0,Min:1,Max:2,Percent:7),(Rank:20,Mastery:3,Min:23,Max:24,Percent:119)})
        {
            var v=new SkillValues(SkillMechanics.For(CombatAbility.IronGolem),key=>key=="IronGolem"?x.Rank:key=="Golem Mastery"?x.Mastery:0,_=>0,99);
            Require(GolemPetDamage.Weapon(v,MonsterDifficulty.Normal)==(x.Min,x.Max,x.Percent),"Independent Iron curve golden");
        }
    }
    private static void ItemControls()
    {
        foreach(string name in new string[]{null,"Battlebranch","Boneflesh"})
        {
            var p=Owner();var item=Original(p,name);var b=Learned(p,item);
            try
            {
                Match(b,3);var raw=b.SourceWeaponBase;var capture=NpcEquipmentDamage.Capture(b).Value;bool weapon=ClassGear.Slot(item)==GearSlot.Weapon;
                bool two=weapon&&(ClassGear.Base(item).TwoHanded||ClassGear.Base(item).OneOrTwoHanded);var local=weapon?ClassGear.Damage(item,b.Level,two,true):(ClassGear.Bonus(item,"dmg-min",b.Level),ClassGear.Bonus(item,"dmg-max",b.Level));
                Require(capture.Minimum+capture.NormalDamage==(two?0:raw.Min)+local.Item1&&capture.Maximum+capture.NormalDamage==(two?0:raw.Max)+local.Item2,"Original item replaced/duplicated wrong intrinsic endpoint");
                Require(capture.DamagePercent==raw.Percent+(weapon?0:ClassGear.Bonus(item,"dmg%",b.Level))&&item.Parent==b,"Old Fire synergy or duplicate weapon ED/original identity");
            }
            finally{b.Delete();p.Delete();}
        }
    }
    private static void Check()
    {
        Goldens();ItemControls();var p=Owner();Train(p,"skeletonmastery",20);Train(p,"firegolem",4);PoisonDaggerChecks.Named(p,"Wormskull");var item=Original(p);var b=Learned(p,item);
        Match(b,4);Require(b.Rank==10,"Real effective Iron rank");var packet=NpcEquipmentDamage.Capture(b).Value;int max=SkillHealth.Maximum256(b);SkillHealth.SetBalance256(b,max-257);TimedEffects.PreventHealing(b,TimedEffects.Now);int life=SkillHealth.Balance256(b);long order=BattleCommand.PetOrder(b);ulong seed=NpcEquipmentRandom.State(b).Seed;string recipe=JsonSerializer.Serialize(b.Prepared.Recipe);
        Train(p,"irongolem",20);Train(p,"golemmastery",20);Train(p,"firegolem",20);Match(b,4);Require(NpcEquipmentDamage.Capture(b)==packet&&SkillHealth.Balance256(b)==life&&SkillHealth.Maximum256(b)==max&&BattleCommand.PetOrder(b)==order&&NpcEquipmentRandom.State(b).Seed==seed&&JsonSerializer.Serialize(b.Prepared.Recipe)==recipe,"Training recaptured Iron damage/resources/item/stream");
        var facets=SummonDifficulties.Facets();PersistentMinions.Suspend(p);
        try
        {
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Nightmare),new(Map.Ilshenar.MapID,MonsterDifficulty.Hell)]);
            foreach(var map in new[]{Map.Felucca,Map.Ilshenar,Map.Trammel})
            {life=SkillHealth.Balance256(b);max=SkillHealth.Maximum256(b);p.MoveToWorld(new(5450,1160,0),map);Match(b,4);Require(b.SummonDifficulty==SummonDifficulties.For(map)&&SkillHealth.Balance256(b)==(long)life*SkillHealth.Maximum256(b)/max&&item.Parent==b,"Facet rescale lost captured damage/life ratio/original");}
        }
        finally{SummonDifficulties.SetFacets(facets);}
        var legacy=new NecromancerMinion{Name="Original Iron curve [fixture]",Frozen=true,CantWalk=true};legacy.Prepare(p,CombatAbility.IronGolem);legacy.SetControlMaster(p);legacy.MoveToWorld(p.Location,p.Map);legacy.AIObject.AITimer.Stop();PersistentMinions.Profiles.Track(legacy);legacy.Internalize();
        Require(legacy.SourceWeaponBase==SkeletonStats.WeaponBase(legacy.Rank,21,legacy.SummonDifficulty),"Historical Iron curve did not use its captured Golem Mastery");
        SkillHealth.SetBalance256(legacy,SkillHealth.Maximum256(legacy)-257);TimedEffects.PreventHealing(legacy,TimedEffects.Now);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,legacy.Serial.Value,item.Serial.Value,recipe,SkillHealth.Balance256(b),SkillHealth.Balance256(legacy),b.Rank,4,NpcEquipmentDamage.Capture(b).Value,order,seed,0)));
        Log.Information("Iron damage:three independent curve goldens;real learned sword/two-hand/armor originals,captured Golem Mastery/no Skeleton Mastery or Fire synergy,no live recapture,three actual facet transitions and historical curve pass;Extra Strong/Spectral Hit remain pending");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(IronGolemBody)World.FindMobile((Serial)s.Body);var legacy=(NecromancerMinion)World.FindMobile((Serial)s.Legacy);var item=World.FindItem((Serial)s.Item);Match(b,s.Mastery);
        Require(s.Generation<2&&b.ControlMaster==p&&legacy.ControlMaster==p&&legacy.GetType()==typeof(NecromancerMinion)&&legacy.Kind==CombatAbility.IronGolem&&item.Parent==b&&JsonSerializer.Serialize(b.Prepared.Recipe)==s.Recipe&&b.Rank==s.Rank&&NpcEquipmentDamage.Capture(b)==s.Packet&&SkillHealth.Balance256(b)==s.Life&&SkillHealth.Balance256(legacy)==s.LegacyLife&&BattleCommand.PetOrder(b)==s.Order&&NpcEquipmentRandom.State(b).Seed==s.Seed,"Saved Iron curve/item/resources/capture/order/stream changed");
        Require(legacy.SourceWeaponBase==SkeletonStats.WeaponBase(legacy.Rank,21,legacy.SummonDifficulty),"Saved historical Iron curve");File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner(20,20,3700);FixtureNetwork.Authenticate(network,p,"iron-damage");var b=Learned(p,Original(p));var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(b.Location,p.Map);t.SetHits(20000);t.SetResistance(ResistanceType.Physical,0);TimedEffects.PreventHealing(t,TimedEffects.Now);
        var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;NpcEquipmentRandom.Set(b,IronGolemWorldChecks.AttackSeed(b,t));b.RestoreController(b.Prepared.Controller with{NextUtc=TimedEffects.Now.AddMilliseconds(40)});int life=SkillHealth.Balance256(t),damage=0;bool captured=false;long deadline=Core.TickCount+12000;
        void End(){SourceBlocking.BlockRoll=block;t.Delete();b.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline&&network.Running&&b.Prepared.Controller.Fault==null,"Natural Iron curve timeout/fault");
                if(!captured&&b.Prepared.Controller is{Mode:4} state)
                {
                    // Iron allocates at release, unlike the unarmed bodies.
                    // Predict this unchanged-target/no-absorption packet from
                    // its current stream, cancel it, then restore that stream.
                    ulong seed=NpcEquipmentRandom.State(b).Seed;var packet=IronGolemContact.Prepare(b,t,IronGolemContact.Require(b,p,t),()=>IronGolemWorld.Result(b,p,state,t));damage=packet.Impact.Packet.Physical256;packet.Cancel();NpcEquipmentRandom.Set(b,seed);captured=true;
                    Require(damage>40*256&&packet.Impact.Packet.Total256==damage&&SkillHealth.Balance256(t)==life,"Natural Iron curve prediction or early HP");
                }
                if(SkillHealth.Balance256(t)==life){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(captured&&life-SkillHealth.Balance256(t)==damage&&b.Prepared.Controller.Attempts==1,"Natural learned Iron curve contact");End();Log.Information("iron-golem-damage-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"iron-golem-damage-live-result: failed");}
        }
        Log.Information("iron-golem-damage-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
}
