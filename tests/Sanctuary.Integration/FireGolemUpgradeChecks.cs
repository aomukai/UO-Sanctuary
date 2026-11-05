// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class FireGolemUpgradeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FireGolemUpgradeChecks));
    private sealed record Actor(uint Serial,string Type,int Rank,int Life,int Maximum,NpcPhysicalStats? Packet,ulong? Seed,string Controller);
    private sealed record Saved(uint Owner,Actor[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"fire-upgrades-fixtures.json");
    public static void Configure()
    {
        Register("fire-upgrades-check",Check);Register("fire-upgrades-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("fire-upgrades-live","Actual Fire pulse then upgraded melee",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("fire-upgrades-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Fire doubled Clay/Holy Fire/CBF/cold immunity and saved actors",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,string id,int rank)=>MagePetBuffChecks.Train(p,"necromancer."+id,rank);
    private static PlayerMobile Owner(int rank,int mastery,int row)
    {var p=PoisonDaggerChecks.Owner();FixtureSpace.Corridor(p,6740,40,row);Train(p,"firegolem",rank);if(mastery>0){Train(p,"golemmastery",mastery);}return p;}
    private static FireGolemWorldBody Learned(PlayerMobile p)
    {
        NecromancerEffects.SetMana256(p,p.ManaMax*256);int before=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.FireGolem).Mana256;
        Require(NecromancerCombat.Release(p,CombatAbility.FireGolem,Serial.Zero,new(p.X+1,p.Y,p.Z))&&NecromancerEffects.ManaBalance256(p)==before-cost,"Fire learned once-only payment");
        var b=NecromancerGolems.For(p).OfType<FireGolemWorldBody>().Single();b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});return b;
    }
    private static GraveyardSkeleton Target(PlayerMobile p,Mobile b)
    {var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);t.SetResistance(ResistanceType.Physical,0);t.SetResistance(ResistanceType.Fire,0);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static void Match(NecromancerMinion b,int mastery)
    {
        var raw=SkeletonStats.WeaponBase(b.Rank,mastery,b.SummonDifficulty);int low=raw.Min*512,high=raw.Max*512;
        Require(b.SourceWeaponBase==(low/256,high/256,raw.Percent)&&b.SkillWeaponDamage==((low+SourcePercentage.Calculate(low,raw.Percent,100))/256,(high+SourcePercentage.Calculate(high,raw.Percent,100))/256),"Fire did not use captured doubled Clay/Skeleton curve");
        if(b is FireGolemBody typed){Require(typed.UnarmedPhysical==new NpcPhysicalStats(raw.Min*2,raw.Max*2,0,raw.Percent,0,0,false)&&typed.InnateFire256==(0,0),"Fire duplicated old physical or skill-fire packet");}
        var holy=FireGolemUpgrades.HolyFire(b);Require(holy.Rank==b.Rank&&holy.Formula("skill('Resist Fire'.blvl)")==0&&holy.Formula("skill('Salvation'.blvl)")==0,"Fire aura rank/context inherited owner synergy");
    }
    private static void Controls()
    {
        var p=Owner(9,3,3020);PoisonDaggerChecks.Named(p,"Wormskull");var b=Learned(p);NecroSnapshotChecks.MissingMetadata(b);var enemy=Target(p,b);var caster=new PlayerMobile{Player=true,Body=0x190};ClassCreation.Profiles.Assign(caster,DiabloClass.Paladin);
        var conviction=PaladinConviction.Snapshot();var holy=PaladinHolyFreeze.Snapshot();
        try
        {
            Require(b.Rank==10&&FireGolemUpgrades.HardRank(b)==9&&!ItemDurationDefenses.CannotFreeze(b),"Soft rank unlocked CBF");Match(b,4);
            Require(GearEffects.ApplyChill(b,TimedEffects.TickAt(TimedEffects.Now),1600,enemy),"Pre-unlock Fire was not chillable");TimedEffects.Clear(b);
            Require(FreezeStates.Apply(enemy,b,40,TimedEffects.TickAt(TimedEffects.Now)),"Pre-unlock Fire was not freezable");TimedEffects.Clear(b);
            int original=b.ColdResistance,max=SkillHealth.Maximum256(b);Require(original<100,"Cold immunity boundary needs nonimmune birth");SkillHealth.SetBalance256(b,max-257);TimedEffects.PreventHealing(b,TimedEffects.Now);
            int hp=SkillHealth.Balance256(b);long order=BattleCommand.PetOrder(b);ulong seed=NpcEquipmentRandom.State(b).Seed;var controller=b.WorldState.Controller;var packet=b.UnarmedPhysical;
            Train(p,"firegolem",10);Require(ItemDurationDefenses.CannotFreeze(b)&&!GearEffects.ApplyChill(b,TimedEffects.TickAt(TimedEffects.Now),1600,enemy)&&!FreezeStates.Apply(enemy,b,40,TimedEffects.TickAt(TimedEffects.Now))&&NamedElements.Damage(enemy,b,100,"cold",enhance:false)>0,"CBF did not block cold states independently of cold damage");
            Train(p,"firegolem",19);Require(b.ColdResistance==original,"Hard19plus soft rank unlocked cold immunity");Train(p,"firegolem",20);Require(b.ColdResistance==100&&NamedElements.Damage(enemy,b,100,"cold",enhance:false)==0,"Hard20cold immunity missing");
            Require(PaladinConviction.Apply(caster,b,SkillMechanics.Values(caster,CombatAbility.Conviction,12),TimedEffects.Now.AddMinutes(1))&&NamedElements.Resistance(enemy,b,"cold",b.ColdResistance)==83&&NamedElements.Damage(enemy,b,100,"cold",enhance:false)==17,"Cold immunity became unbreakable or bypassed shared one-fifth Conviction");PaladinConviction.Restore(conviction);
            Require(PaladinHolyFreeze.Eligible(b,out _),"Fire cold-effect mapping missing");PaladinHolyFreeze.Slow(caster,b,SkillMechanics.Values(caster,CombatAbility.HolyFreeze,5),TimedEffects.Now);Require(PaladinHolyFreeze.VelocitySlow(b)>0,"CBF/cold immunity incorrectly blocked Holy Freeze");PaladinHolyFreeze.Restore(holy);
            b.SetResistance(ResistanceType.Cold,160);Require(b.ColdResistance==160,"Immunity floor lowered existing stronger resistance");b.SetResistance(ResistanceType.Cold,original);
            Train(p,"golemmastery",20);Train(p,"skeletonmastery",20);Match(b,4);Require(b.UnarmedPhysical==packet&&FireGolemUpgrades.HolyFire(b).Rank==10&&b.WorldState.Controller==controller&&NpcEquipmentRandom.State(b).Seed==seed&&SkillHealth.Balance256(b)==hp&&SkillHealth.Maximum256(b)==max&&BattleCommand.PetOrder(b)==order,"Live upgrades recaptured rank/curve/aura,healed,changed controller/order or rolled RNG");
            ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(!ItemDurationDefenses.CannotFreeze(b)&&b.ColdResistance==original&&b.UnarmedPhysical==packet,"Respec retained unlock or recaptured curve");Train(p,"firegolem",20);Require(ItemDurationDefenses.CannotFreeze(b)&&b.ColdResistance==100,"Relearn lost unlocks");
            var facets=SummonDifficulties.Facets();PersistentMinions.Suspend(p);
            try
            {
                SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Nightmare),new(Map.Ilshenar.MapID,MonsterDifficulty.Hell)]);
                foreach(var map in new[]{Map.Felucca,Map.Ilshenar,Map.Trammel})
                {hp=SkillHealth.Balance256(b);max=SkillHealth.Maximum256(b);p.MoveToWorld(new(5450,1160,0),map);Match(b,4);Require(SkillHealth.Balance256(b)==(long)hp*SkillHealth.Maximum256(b)/max&&b.ColdResistance>=100&&ItemDurationDefenses.CannotFreeze(b),"Facet rescale lost wounds/upgrade/captured curve");}
            }
            finally{SummonDifficulties.SetFacets(facets);}
        }
        finally{PaladinConviction.Restore(conviction);PaladinHolyFreeze.Restore(holy);enemy.Delete();b.Delete();p.Delete();caster.Delete();}
        p=Owner(4,0,3060);b=Learned(p);enemy=Target(p,b);
        try
        {
            Require(b.UnarmedPhysical==new NpcPhysicalStats(2,4,0,7,0,0,false)&&NpcEquipmentDamage.Roll(b,enemy,_=>0,()=>99).Damage.Physical256==547&&NpcEquipmentDamage.Roll(b,enemy,n=>n-1,()=>99).Damage.Physical256==1094,"Independent rank4double curve goldens");
            Require(NpcEquipmentElements.Capture(b).Stats.FireMin==0&&NpcEquipmentDamage.Roll(b,enemy,_=>0,()=>99).Damage.Elements.Fire==0,"Old innate fire remains before aura activation");
            foreach(var (rank,min,max) in new[]{(1,6,18),(4,15,36),(10,51,90),(20,195,264),(30,405,504)})
            {var v=new SkillValues(SkillMechanics.Source("Holy Fire"),k=>k=="Holy Fire"?rank:0,_=>0,99);Require(PaladinElements.WeaponRange(b,CombatAbility.HolyFire,v)==(min,max),"Shared Paladin progression golden");}
        }
        finally{enemy.Delete();b.Delete();p.Delete();}
    }
    private static Actor Stored(NecromancerMinion b)=>new(b.Serial.Value,b.GetType().FullName,b.Rank,SkillHealth.Balance256(b),SkillHealth.Maximum256(b),b is ISourceUnarmedNpc?NpcEquipmentDamage.Capture(b):null,b is ISourceUnarmedNpc?NpcEquipmentRandom.State(b).Seed:null,b is FireGolemWorldBody world?JsonSerializer.Serialize(world.WorldState):null);
    private static void Check()
    {
        Controls();var p=Owner(20,20,3100);var learned=Learned(p);var actors=new NecromancerMinion[]{learned,new FireGolemBody(),new FireGolemDeathBody(),new NecromancerMinion()};
        foreach(var b in actors)
        {
            if(b!=learned){b.Prepare(p,CombatAbility.FireGolem);Require(b.SetControlMaster(p),"Historical Fire owner");b.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);b.AIObject.AITimer.Stop();PersistentMinions.Profiles.Track(b);BattleCommand.Track(b);if(b is ISourceUnarmedNpc){NpcEquipmentRandom.Set(b,173);}}
            Match(b,20);Require(ItemDurationDefenses.CannotFreeze(b)&&b.ColdResistance>=100,"Historical Fire did not receive live defenses");
            if(b is not FireGolemBody)
            {
                var target=Target(p,b);try{int hp=SkillHealth.Balance256(target);typeof(NecromancerMinion).GetMethod("Strike",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(b,[target]);var range=b.SkillWeaponDamage;Require(hp-SkillHealth.Balance256(target)>=range.Min*256&&hp-SkillHealth.Balance256(target)<=range.Max*256,"Historical fallback retained separate old skill-fire hit");}finally{target.Delete();}
            }
            SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-257);TimedEffects.PreventHealing(b,TimedEffects.Now);
        }
        PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,actors.Select(Stored).ToArray(),0)));
        Log.Information("Fire upgrades:shared200percent Clay curve/no old fire,five Paladin Holy Fire goldens,captured ranks/no owner synergy,live hard9+soft1/10/19+soft1/20,respec/relearn,CBF versus cold damage/Holy Freeze,breakable100cold floor,unchanged wounds/order/controller/stream,three facets and four saved actor types pass");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        Require(s.Generation<2,"Extra Fire reload");foreach(var stored in s.Actors)
        {
            var b=(NecromancerMinion)World.FindMobile((Serial)stored.Serial);Match(b,20);Require(b.ControlMaster==p&&b.Map==Map.Internal&&JsonSerializer.Serialize(Stored(b))==JsonSerializer.Serialize(stored)&&ItemDurationDefenses.CannotFreeze(b)&&b.ColdResistance>=100,"Saved Fire identity/resources/ranks/controller/stream/unlocks changed");
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner(9,3,3140);PoisonDaggerChecks.Named(p,"Wormskull");FixtureNetwork.Authenticate(network,p,"fire-upgrades");var b=Learned(p);NecroSnapshotChecks.MissingMetadata(b);b.Frozen=true;b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddMilliseconds(40)});var t=Target(p,b);NecromancerEffects.SetMana256(p,0);NpcEquipmentRandom.Set(b,17);
        ulong expectedSeed=17;int min=b.HolyFireValues.Damage256(false),max=b.HolyFireValues.Damage256(true),pulse=2*(min+(int)SourceRandom.Limited(ref expectedSeed,max-min));int hp=SkillHealth.Balance256(t),damage=0;bool trained=false,pulsed=false,captured=false;long deadline=Core.TickCount+12000;var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        void End(){SourceBlocking.BlockRoll=block;t.Delete();b.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(network.Running&&Core.TickCount<deadline&&b.WorldState.Controller.Fault==null,"Natural Fire upgrades timeout/fault");
                if(!trained&&b.WorldState.Controller.Frame>=25){Require(!ItemDurationDefenses.CannotFreeze(b),"Soft rank unlocked live CBF");Train(p,"firegolem",10);Require(ItemDurationDefenses.CannotFreeze(b),"Live hard10 did not activate CBF");trained=true;}
                if(!pulsed)
                {
                    if(b.WorldState.Controller.Frame<50){Require(SkillHealth.Balance256(t)==hp&&PaladinAuras.Effect(b,CombatAbility.HolyFire)==null,"Guessed early aura activation");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                    Require(trained&&hp-SkillHealth.Balance256(t)==pulse&&NpcEquipmentRandom.State(b).Seed==expectedSeed&&PaladinAuras.Effect(b,CombatAbility.HolyFire) is{Rank:10,WeaponMin:51,WeaponMax:90}&&NecromancerEffects.ManaBalance256(p)==0,"Natural exact-rank/no-mana Holy Fire pulse and own weapon state");
                    Train(p,"firegolem",20);Require(b.ColdResistance>=100&&b.HolyFireValues.Rank==10,"Live immunity changed captured aura");hp=SkillHealth.Balance256(t);NpcEquipmentRandom.Set(b,FireGolemWorldChecks.Seed(b,t));b.Frozen=false;pulsed=true;
                }
                if(!captured&&b.WorldContact is{} packet){Require(packet.Impact.Packet.Physical256>0&&packet.Impact.Packet.Fire256>0&&SkillHealth.Balance256(t)==hp,"Natural active-aura mixed contact/early HP");damage=packet.Impact.Packet.Total256;captured=true;}
                if(SkillHealth.Balance256(t)==hp){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(captured&&hp-SkillHealth.Balance256(t)==damage&&b.WorldState.Controller.Attempts==1,"Natural approved Fire combined release");End();Log.Information("fire-upgrades-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"fire-upgrades-live-result: failed");}
        }
        Log.Information("fire-upgrades-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
}
