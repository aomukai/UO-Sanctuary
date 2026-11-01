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

public static class BloodGolemDamageChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BloodGolemDamageChecks));
    private sealed record Saved(uint Owner,uint Body,uint Legacy,int Life,int LegacyLife,int Maximum,int Rank,int Mastery,NpcPhysicalStats Packet,long Order,ulong Seed,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blood-damage-fixtures.json");
    public static void Configure()
    {
        Register("blood-damage-check",Check);Register("blood-damage-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("blood-damage-live","Connected approved Blood replacement damage",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("blood-damage-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Blood shared captured curve and150percent fractional endpoints",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,string id,int rank)=>MagePetBuffChecks.Train(p,"necromancer."+id,rank);
    private static PlayerMobile Owner(int rank=9,int mastery=3,int row=3150)
    {var p=PoisonDaggerChecks.Owner();FixtureSpace.Corridor(p,6100,40,row);Train(p,"bloodgolem",rank);if(mastery>0){Train(p,"golemmastery",mastery);}return p;}
    private static BloodGolemWorldBody Learned(PlayerMobile p)
    {
        NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.BloodGolem).Mana256;
        Require(NecromancerCombat.Release(p,CombatAbility.BloodGolem,Serial.Zero,new(p.X+1,p.Y,p.Z))&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Learned Blood once-only payment");
        var b=NecromancerGolems.For(p).OfType<BloodGolemWorldBody>().Single();b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});return b;
    }
    private static void Match(NecromancerMinion b,int mastery)
    {
        var clay=SkeletonStats.WeaponBase(b.Rank,mastery,b.SummonDifficulty);int low=checked(clay.Min*384),high=checked(clay.Max*384);
        Require(b.SourceWeaponBase==(low/256,high/256,clay.Percent),"Blood native base did not use150percent of captured Clay/Skeleton curve");
        Require(b.SkillWeaponDamage==((low+SourcePercentage.Calculate(low,clay.Percent,100))/256,(high+SourcePercentage.Calculate(high,clay.Percent,100))/256),"Blood native whole-number projection lost fractional endpoint before percent");
        if(b is BloodGolemBody exact){Require(exact.UnarmedPhysical==new NpcPhysicalStats(low/256,high/256,0,clay.Percent,0,0,false,MinimumFraction256:low%256,MaximumFraction256:high%256),"Blood packet retained old innate/Fire synergy or discarded half damage");}
    }
    private static void Precision()
    {
        var stats=new NpcPhysicalStats(1,3,0,7,0,0,false,MinimumFraction256:128);
        Require(NpcEquipmentDamage.Roll256(stats,_=>0)==410&&NpcEquipmentDamage.Roll256(stats,n=>n-1)==820&&NpcEquipmentDamage.Roll256(stats,_=>0,64)==205,"Fractional base endpoints or SrcDam order");
        var old=JsonSerializer.Deserialize<NpcPhysicalStats>("{\"Minimum\":1,\"Maximum\":2,\"NormalDamage\":0,\"DamagePercent\":7,\"Strength\":0,\"Dexterity\":0,\"Weapon\":false}");
        Require(old==new NpcPhysicalStats(1,2,0,7,0,0,false)&&NpcEquipmentDamage.Roll256(old,_=>0)==273,"Older physical JSON/default zero fractions changed Clay");
        int draws=0;foreach(int fraction in new[]{-1,256}){bool bad=false;try{NpcEquipmentDamage.Roll256(stats with{MinimumFraction256=fraction},_=>{draws++;return 0;});}catch(ArgumentOutOfRangeException){bad=true;}Require(bad&&draws==0,"Invalid fraction consumed RNG or was accepted");}
        Require(NpcEquipmentDamage.Roll256(new(int.MinValue,int.MinValue,0,0,0,0,false),_=>0)==256,"Unarmed negative native floor now overflows");
        var p=Owner(4,0);var b=Learned(p);
        try
        {
            Match(b,0);Require(b.UnarmedPhysical==stats,"Real no-Mastery rank4 precision fixture");
            foreach(var name in new[]{"Steelclash","Lance of Yaggai"})
            {
                var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==name).Id,99,new Random(836),true);item.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
                Require(IronGolemItems.TryAttach(p,b,item,out var why),"Blood original-item fraction control: "+why);var captured=NpcEquipmentDamage.Capture(b).Value;
                Require(captured.MinimumFraction256==(name=="Steelclash"?128:0)&&captured.MaximumFraction256==0,"Shield lost intrinsic half or two-hand item retained displaced base fraction");IronGolemItems.DestroyBoundItem(b);
            }
        }
        finally{b.Delete();p.Delete();}
    }
    private static void Facets(PlayerMobile p,BloodGolemWorldBody b)
    {
        var old=SummonDifficulties.Facets();PersistentMinions.Suspend(p);
        try
        {
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Nightmare),new(Map.Ilshenar.MapID,MonsterDifficulty.Hell)]);
            foreach(var map in new[]{Map.Felucca,Map.Ilshenar,Map.Trammel})
            {int life=SkillHealth.Balance256(b),max=SkillHealth.Maximum256(b);p.MoveToWorld(new(5450,1160,0),map);Match(b,4);Require(b.SummonDifficulty==SummonDifficulties.For(map)&&SkillHealth.Balance256(b)==(long)life*SkillHealth.Maximum256(b)/max,"Blood facet damage supplier or wound ratio changed");}
        }
        finally{SummonDifficulties.SetFacets(old);}
    }
    private static void Direct()
    {
        Precision();var p=Owner();Train(p,"skeletonmastery",20);Train(p,"firegolem",4);PoisonDaggerChecks.Named(p,"Wormskull");var b=Learned(p);
        int maximum=SkillHealth.Maximum256(b);SkillHealth.SetBalance256(b,maximum-257);TimedEffects.PreventHealing(b,TimedEffects.Now);int life=SkillHealth.Balance256(b);var source=b.UnarmedPhysical;long order=BattleCommand.PetOrder(b);ulong seed=NpcEquipmentRandom.State(b).Seed;
        try
        {
            Require(b.Rank==10&&b.Mastery==21,"Real soft rank or distinct Skeleton Mastery setup");Match(b,4);
            Train(p,"bloodgolem",20);Train(p,"golemmastery",20);Match(b,4);
            Require(b.UnarmedPhysical==source&&b.SourceAttackClock==(10,16)&&SkillHealth.Maximum256(b)==maximum&&SkillHealth.Balance256(b)==life&&BattleCommand.PetOrder(b)==order&&NpcEquipmentRandom.State(b).Seed==seed,"Training recaptured damage/resources,healed,reordered or changed stream/clock");
            ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(!b.Deleted&&b.UnarmedPhysical==source,"Respec recaptured original Blood damage");Train(p,"bloodgolem",20);Train(p,"golemmastery",20);Match(b,4);Facets(p,b);
        }
        finally{b.Delete();p.Delete();}
        Log.Information("Blood damage:shared captured Skeleton/Golem Mastery150percent endpoints,rank4FP410..820,half-endpoint/source-percent/SrcDam order,old JSON zero fractions,invalid fractions before RNG,real shield/two-hand item composition,no Skeleton Mastery/old Fire synergy/owner recapture,unchanged wounds/order/seed/clock and three facet transitions pass;Open Wounds/Life Tap remain pending");
    }
    private static void Check()
    {
        Direct();var p=Owner(20,20);var b=Learned(p);var old=new NecromancerMinion();old.Prepare(p,CombatAbility.BloodGolem);old.SetControlMaster(p);old.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);old.AIObject.AITimer.Stop();PersistentMinions.Profiles.Track(old);BattleCommand.Track(old);
        foreach(var pet in new NecromancerMinion[]{b,old}){Match(pet,20);SkillHealth.SetBalance256(pet,SkillHealth.Maximum256(pet)-257);TimedEffects.PreventHealing(pet,TimedEffects.Now);}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,old.Serial.Value,SkillHealth.Balance256(b),SkillHealth.Balance256(old),SkillHealth.Maximum256(b),b.Rank,b.Mastery,b.UnarmedPhysical,BattleCommand.PetOrder(b),NpcEquipmentRandom.State(b).Seed,0)));
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(BloodGolemWorldBody)World.FindMobile((Serial)s.Body);var old=(NecromancerMinion)World.FindMobile((Serial)s.Legacy);
        Match(b,20);Match(old,20);Require(b.ControlMaster==p&&old.ControlMaster==p&&old.GetType()==typeof(NecromancerMinion)&&old.Kind==CombatAbility.BloodGolem&&SkillHealth.Balance256(b)==s.Life&&SkillHealth.Balance256(old)==s.LegacyLife&&SkillHealth.Maximum256(b)==s.Maximum&&b.Rank==s.Rank&&b.Mastery==s.Mastery&&b.UnarmedPhysical==s.Packet&&BattleCommand.PetOrder(b)==s.Order&&NpcEquipmentRandom.State(b).Seed==s.Seed&&b.SourceAttackClock==(10,16),"Saved Blood type/owner/wound/resources/fractional packet/rank/order/seed/clock changed");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner(20,20,3600);FixtureNetwork.Authenticate(network,p,"blood-damage");var b=Learned(p);var t=PoisonDaggerChecks.Target(p);t.SetHits(20000);t.MoveToWorld(b.Location,p.Map);t.SetResistance(ResistanceType.Physical,0);TimedEffects.PreventHealing(t,TimedEffects.Now);
        var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;NpcEquipmentRandom.Set(b,BloodGolemWorldChecks.Seed(b,t));b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddMilliseconds(40)});
        int hp=SkillHealth.Balance256(t),damage=0;bool captured=false,observedHit=false;long deadline=Core.TickCount+10000;
        void End(){SourceBlocking.BlockRoll=block;t.Delete();b.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline&&b.WorldState.Controller.Fault==null,"Natural Blood replacement timeout/fault");
                if(!captured&&b.WorldContact is{} packet){captured=true;damage=packet.Impact.Packet.Physical256;Require(damage>40*256&&packet.Impact.Packet.Total256==damage&&SkillHealth.Balance256(t)==hp,"Natural Blood fractional capture/early HP");}
                if(SkillHealth.Balance256(t)==hp){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                // Hard20 also has Open Wounds. Observe beyond its first
                // frames deliberately; host polling is not a no-bleed window.
                if(!observedHit){observedHit=true;Timer.StartTimer(TimeSpan.FromMilliseconds(80),Wait);return;}
                int ticks=0,rate=0;if(TimedEffects.TryActor(t,out var state)&&state.OpenWounds is{} wound){ticks=(8000-state.BleedMs)/40;rate=wound.Rate256;}
                int loss=hp-SkillHealth.Balance256(t);
                Require(network.Running&&captured&&ticks>=2&&loss==damage+ticks*rate&&b.WorldState.Controller.Attempts==1&&b.SourceAttackClock==(10,16),
                    $"Natural Blood replacement actual contact/clock:captured={captured},loss={loss},direct={damage},woundTicks={ticks},rate={rate},attempts={b.WorldState.Controller.Attempts},clock={b.SourceAttackClock}");End();Log.Information("blood-damage-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"blood-damage-live-result: failed");}
        }
        Log.Information("blood-damage-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
}
