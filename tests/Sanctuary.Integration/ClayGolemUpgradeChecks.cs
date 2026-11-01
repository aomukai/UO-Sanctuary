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

public static class ClayGolemUpgradeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ClayGolemUpgradeChecks));
    private sealed record Saved(uint Owner,uint Body,uint Legacy,int Life,int LegacyLife,int Maximum,int Rank,int Mastery,NpcPhysicalStats Packet,int Defense,int[] Res,long Order,ulong Seed,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"clay-upgrades-fixtures.json");
    public static void Configure()
    {
        Register("clay-upgrades-check",Check);Register("clay-upgrades-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("clay-upgrades-live","Connected approved Clay damage and defensive unlocks",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("clay-upgrades-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Clay shared skeleton curve and existing defensive affixes",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,string id,int rank)=>MagePetBuffChecks.Train(p,"necromancer."+id,rank);
    private static PlayerMobile Owner(int rank=9,int mastery=3,int row=3200)
    {var p=PoisonDaggerChecks.Owner();FixtureSpace.Corridor(p,6100,40,row);Train(p,"claygolem",rank);Train(p,"golemmastery",mastery);return p;}
    private static ClayGolemWorldBody Learned(PlayerMobile p)
    {
        NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.ClayGolem).Mana256;
        Require(NecromancerCombat.Release(p,CombatAbility.ClayGolem,Serial.Zero,new(p.X+1,p.Y,p.Z))&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Learned upgraded Clay once-only payment");
        var b=NecromancerGolems.For(p).OfType<ClayGolemWorldBody>().Single();Hold(b);return b;
    }
    private static void Hold(ClayGolemWorldBody b)=>b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});
    private static int[] Res(NecromancerMinion b)=>[b.PhysicalResistance,NamedElements.MagicResistance(b),b.FireResistance,b.EnergyResistance,b.ColdResistance,b.PoisonResistance];
    private static int[] Affixes(int[] basis,bool magic)=>EncounterProfiles.Resistances(new(basis[0],basis[1],basis[2],basis[3],basis[4],basis[5]),null,
        magic?[EliteModifier.StoneSkin,EliteModifier.MagicResistant]:[EliteModifier.StoneSkin]);
    private static void Match(NecromancerMinion b,int mastery)
    {
        var w=SkeletonStats.WeaponBase(b.Rank,mastery,b.SummonDifficulty);Require(b.SourceWeaponBase==w,"Clay did not share Skeleton rank/mastery/difficulty curve");
        Require(b.SkillWeaponDamage==(w.Min*(100+w.Percent)/100,w.Max*(100+w.Percent)/100),"Clay native damage display/fallback differs from shared curve");
        if(b is ClayGolemBody exact){Require(exact.UnarmedPhysical==new NpcPhysicalStats(w.Min,w.Max,0,w.Percent,0,0,false),"Clay FP packet kept former innate damage/synergy");}
    }
    private static void Incoming(PlayerMobile p,ClayGolemBody b,ResistanceType type)
    {
        int life=SkillHealth.Balance256(b),res=b.GetResistance(type);var parts=type==ResistanceType.Physical?new GearDamage(100,0,0,0,0):new GearDamage(0,100,0,0,0);
        var enemy=PoisonDaggerChecks.Target(p);
        try{SkillCombat.ResolveHitParts(enemy,b,parts,false,false,enhanceBase:false,addWeaponElements:false);
            Require(life-SkillHealth.Balance256(b)==100*(100-Math.Clamp(res,-100,100))*256/100,"Actual incoming packet ignored live defensive resistance");SkillHealth.SetBalance256(b,life);}
        finally{enemy.Delete();}
    }
    private static void Facets(PlayerMobile p,ClayGolemWorldBody b)
    {
        var old=SummonDifficulties.Facets();PersistentMinions.Suspend(p);
        try
        {
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Nightmare),new(Map.Ilshenar.MapID,MonsterDifficulty.Hell)]);
            foreach(var map in new[]{Map.Felucca,Map.Ilshenar,Map.Trammel})
            {
                int life=SkillHealth.Balance256(b),max=SkillHealth.Maximum256(b);p.MoveToWorld(new(5450,1160,0),map);Match(b,4);
                Require(b.SummonDifficulty==SummonDifficulties.For(map)&&SkillHealth.Balance256(b)==(long)life*SkillHealth.Maximum256(b)/max,"Facet upgrade changed wound ratio or damage difficulty supplier");
            }
        }
        finally{SummonDifficulties.SetFacets(old);}
    }
    private static void Wild()
    {
        foreach(var difficulty in Enum.GetValues<MonsterDifficulty>())foreach(var grade in new[]{EncounterGrade.Unique,EncounterGrade.Minion})
        {
            var a=new EncounterCreature();var b=new EncounterCreature();
            try
            {
                var profile=EncounterProfiles.Profile("crypt-sentinel-60");a.Assign(profile,60,difficulty,grade,ChampionKind.Champion,[],EliteAura.None,null,new Random(836));
                b.Assign(profile,60,difficulty,grade,ChampionKind.Champion,[EliteModifier.StoneSkin],EliteAura.None,null,new Random(836));
                Require(b.CombatProfile.Defense==a.CombatProfile.Defense*(grade==EncounterGrade.Minion?1:2)&&b.NormalStats==a.NormalStats&&b.CombatProfile.AttackMs==a.CombatProfile.AttackMs&&b.CombatProfile.MinDamage==a.CombatProfile.MinDamage&&b.CombatProfile.MaxDamage==a.CombatProfile.MaxDamage,"Extracted Stone Skin changed wild defense inheritance or other stats");
            }
            finally{a.Delete();b.Delete();}
        }
    }
    private static void Direct()
    {
        Wild();var p=Owner();Train(p,"skeletonmastery",20);Train(p,"firegolem",4);var helm=PoisonDaggerChecks.Named(p,"Wormskull");var b=Learned(p);
        int basis=b.CombatProfile.Defense;var res=Res(b);int maximum=SkillHealth.Maximum256(b);SkillHealth.SetBalance256(b,maximum-257);TimedEffects.PreventHealing(b,TimedEffects.Now);
        int life=SkillHealth.Balance256(b);long order=BattleCommand.PetOrder(b);ulong seed=NpcEquipmentRandom.State(b).Seed;var source=b.UnarmedPhysical;
        try
        {
            Require(b.Rank==10&&b.Mastery==21&&ClayGolemUpgrades.HardRank(b)==9,"Clay real soft-point boundary setup");Match(b,4);
            Train(p,"claygolem",10);Require(b.CombatProfile.Defense==2*basis&&Res(b).SequenceEqual(Affixes(res,false)),"Hard10 Stone Skin defense/resistance");Incoming(p,b,ResistanceType.Physical);
            p.Backpack.DropItem(helm);ClassGear.Refresh(p);Train(p,"claygolem",19);Require(p.EquipItem(helm),"Reequip actual soft point");ClassGear.Refresh(p);
            Require(SkillMechanics.Rank(p,CombatAbility.ClayGolem)==20&&Res(b).SequenceEqual(Affixes(res,false)),"Soft point unlocked Magic Resistant");
            Train(p,"claygolem",20);Require(Res(b).SequenceEqual(Affixes(res,true))&&b.CombatProfile.Defense==2*basis,"Hard20 Magic Resistant or doubled Stone Skin");Incoming(p,b,ResistanceType.Fire);
            Require(b.UnarmedPhysical==source&&b.SourceAttackClock==(10,18)&&SkillHealth.Balance256(b)==life&&SkillHealth.Maximum256(b)==maximum&&BattleCommand.PetOrder(b)==order&&NpcEquipmentRandom.State(b).Seed==seed,"Affixes recaptured birth,healed,rerolled,reordered or changed attack clock");
            // Same two-immunity restriction and already-immune skip as wild affixes.
            Require(ClayGolemUpgrades.Resistance(b,ResistanceType.Cold,t=>t==ResistanceType.Cold?75:t is ResistanceType.Fire or ResistanceType.Energy?100:0)==75,"Shared two-immunity guard lost");
            ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);
            Require(b.CombatProfile.Defense==basis&&Res(b).SequenceEqual(res)&&b.UnarmedPhysical==source&&!b.Deleted,"Respec retained affixes or recaptured damage");Train(p,"claygolem",20);Train(p,"golemmastery",20);Match(b,4);
            Require(b.CombatProfile.Defense==2*basis&&Res(b).SequenceEqual(Affixes(res,true)),"Relearn did not update original Clay");Facets(p,b);
        }
        finally{b.Delete();p.Delete();}
        Log.Information("Clay upgrades:real hard9+soft1/19+soft1 gates,live Stone Skin10/Magic Resistant20,shared two-immunity order,actual physical/fire incoming packets,captured Skeleton curve with Golem Mastery not Skeleton Mastery,unchanged resources/order/seed/clock,respec/relearn and three actual facet transitions pass");
    }
    private static void Check()
    {
        Direct();var p=Owner(20,20);var b=Learned(p);var legacy=new NecromancerMinion();legacy.Prepare(p,CombatAbility.ClayGolem);legacy.SetControlMaster(p);legacy.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);legacy.AIObject.AITimer.Stop();PersistentMinions.Profiles.Track(legacy);BattleCommand.Track(legacy);
        foreach(var pet in new NecromancerMinion[]{b,legacy}){Match(pet,20);SkillHealth.SetBalance256(pet,SkillHealth.Maximum256(pet)-257);TimedEffects.PreventHealing(pet,TimedEffects.Now);}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,legacy.Serial.Value,SkillHealth.Balance256(b),SkillHealth.Balance256(legacy),SkillHealth.Maximum256(b),b.Rank,b.Mastery,b.UnarmedPhysical,b.CombatProfile.Defense,Res(b),BattleCommand.PetOrder(b),NpcEquipmentRandom.State(b).Seed,0)));
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(ClayGolemWorldBody)World.FindMobile((Serial)s.Body);var old=(NecromancerMinion)World.FindMobile((Serial)s.Legacy);
        Match(b,20);Match(old,20);Require(b.ControlMaster==p&&old.ControlMaster==p&&old.GetType()==typeof(NecromancerMinion)&&old.Kind==CombatAbility.ClayGolem&&SkillHealth.Balance256(b)==s.Life&&SkillHealth.Balance256(old)==s.LegacyLife&&SkillHealth.Maximum256(b)==s.Maximum&&b.Rank==s.Rank&&b.Mastery==s.Mastery&&b.UnarmedPhysical==s.Packet&&b.CombatProfile.Defense==s.Defense&&Res(b).SequenceEqual(s.Res)&&BattleCommand.PetOrder(b)==s.Order&&NpcEquipmentRandom.State(b).Seed==s.Seed&&b.SourceAttackClock==(10,18),"Saved upgraded Clay changed type/owner/wounds/resources/packet/defense/resists/order/seed/clock");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner(20,20,3600);FixtureNetwork.Authenticate(network,p,"clay-upgrades");var b=Learned(p);var t=PoisonDaggerChecks.Target(p);t.SetHits(20000);t.MoveToWorld(b.Location,p.Map);t.SetResistance(ResistanceType.Physical,0);TimedEffects.PreventHealing(t,TimedEffects.Now);
        var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;NpcEquipmentRandom.Set(b,ClayGolemWorldChecks.Seed(b,t));b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddMilliseconds(40)});
        int hp=SkillHealth.Balance256(t),damage=0;bool captured=false;long deadline=Core.TickCount+10000;
        void End(){SourceBlocking.BlockRoll=block;t.Delete();b.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline&&b.WorldState.Controller.Fault==null,"Natural upgraded Clay timeout/fault");
                if(!captured&&b.WorldContact is{} packet){captured=true;damage=packet.Impact.Packet.Physical256;Require(damage>40*256&&packet.Impact.Packet.Total256==damage&&SkillHealth.Balance256(t)==hp,"Natural captured upgraded Clay packet/early HP");}
                if(SkillHealth.Balance256(t)==hp){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(network.Running&&captured&&hp-SkillHealth.Balance256(t)==damage&&b.WorldState.Controller.Attempts==1&&b.SourceAttackClock==(10,18)&&ClayGolemUpgrades.HardRank(b)==20,"Natural upgraded Clay actual release/clock");End();Log.Information("clay-upgrades-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"clay-upgrades-live-result: failed");}
        }
        Log.Information("clay-upgrades-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
}
