// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class IronGolemStrongChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(IronGolemStrongChecks));
    private sealed record Saved(uint Owner,uint Body,uint Legacy,string Prepared,int Life,int LegacyLife,int Maximum,int Rating,int LegacyRating,NpcPhysicalStats Packet,long Order,ulong Seed,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"iron-golem-strong-fixtures.json");
    public static void Configure()
    {
        Register("iron-golem-strong-check",Check);Register("iron-golem-strong-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("iron-golem-strong-live","Connected Iron hard10 during G4 windup",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("iron-golem-strong-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Iron existing direct Extra Strong ED/AR and wild grade controls",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,int rank)=>MagePetBuffChecks.Train(p,"necromancer.irongolem",rank);
    private static int[] Res(Mobile b)=>[b.PhysicalResistance,b.FireResistance,b.ColdResistance,b.EnergyResistance,b.PoisonResistance];
    private static void Wild()
    {
        int count=0;
        foreach(var difficulty in Enum.GetValues<MonsterDifficulty>())foreach(var grade in new[]{EncounterGrade.Ordinary,EncounterGrade.Unique,EncounterGrade.SuperUnique,EncounterGrade.Minion})
        {
            var profile=EncounterProfiles.Profile("crypt-sentinel-60");var a=new EncounterCreature();var b=new EncounterCreature();
            try
            {
                a.Assign(profile,60,difficulty,grade,ChampionKind.Champion,[],EliteAura.None,null,new Random(836));b.Assign(profile,60,difficulty,grade,ChampionKind.Champion,[EliteModifier.ExtraStrong],EliteAura.None,null,new Random(836));
                int index=(int)difficulty,damage=(grade==EncounterGrade.Minion?new[]{67,56,49}:new[]{135,112,99})[index],rating=(grade==EncounterGrade.Minion?new[]{45,37,33}:new[]{90,75,66})[index];
                Require(EncounterProfiles.ExtraStrong(difficulty,grade==EncounterGrade.Minion)==(damage,rating),"Shared direct/inherited constants");
                Require(b.CombatProfile.MinDamage==a.CombatProfile.MinDamage*(100+damage)/100&&b.CombatProfile.MaxDamage==a.CombatProfile.MaxDamage*(100+damage)/100&&b.CombatProfile.AttackRating==a.CombatProfile.AttackRating*(100+rating)/100,"Wild damage/AR branch changed");
                Require(b.NormalStats.Attack1==(a.NormalStats.Attack1 with{MinDamage=b.CombatProfile.MinDamage,MaxDamage=b.CombatProfile.MaxDamage})&&b.NormalStats.Attack2==(a.NormalStats.Attack2 with{MinDamage=b.CombatProfile.MinDamage,MaxDamage=b.CombatProfile.MaxDamage}),"Wild attack descriptor omitted damage or changed frame clock");
                Require(b.CombatProfile with{MinDamage=a.CombatProfile.MinDamage,MaxDamage=a.CombatProfile.MaxDamage,AttackRating=a.CombatProfile.AttackRating}==a.CombatProfile&&(b.NormalStats with{Attack1=a.NormalStats.Attack1,Attack2=a.NormalStats.Attack2})==a.NormalStats,"Extra Strong changed wild life/defense/resistance/clock/reward");count++;
            }
            finally{a.Delete();b.Delete();}
        }
        Require(count==12,"Missing wild affix controls");
    }
    private static void Match(IronGolemBody b,NpcPhysicalStats basis,int baseRating,int itemRating)
    {
        int index=(int)b.SummonDifficulty,damage=new[]{135,112,99}[index],rating=new[]{90,75,66}[index];
        Require(IronGolemUpgrades.DamagePercent(b)==damage&&IronGolemUpgrades.AttackPercent(b)==rating,"Pet inherited weaker boss-minion branch");
        Require(NpcEquipmentDamage.Capture(b)==(basis with{DamagePercent=basis.DamagePercent+damage}),"Extra Strong changed innate/item endpoints or duplicated local ED");
        Require(ClassStats.AttackRating(b)==baseRating+(int)((long)baseRating*(itemRating+rating)/100),"Extra Strong AR did not share item percent pool over full flat basis");
    }
    private static void Check()
    {
        Wild();var p=IronGolemDamageChecks.Owner(9,3,3280);PoisonDaggerChecks.Named(p,"Wormskull");var item=IronGolemDamageChecks.Original(p,"Messerschmidt's Reaver");var b=IronGolemDamageChecks.Learned(p,item);
        var legacy=new NecromancerMinion{Name="Historical Iron Extra Strong [fixture]",Frozen=true,CantWalk=true};legacy.Prepare(p,CombatAbility.IronGolem);legacy.SetControlMaster(p);legacy.MoveToWorld(p.Location,p.Map);legacy.AIObject.AITimer.Stop();PersistentMinions.Profiles.Track(legacy);legacy.Internalize();
        var basis=NpcEquipmentDamage.Capture(b).Value;int ratingBase=CreatureProfiles.AttackRating(b)+NpcEquipmentCombat.AttackFlat(b),itemPercent=NpcEquipmentCombat.AttackPercent(b);
        Require(b.Rank==10&&IronGolemUpgrades.HardRank(b)==9&&IronGolemUpgrades.DamagePercent(b)==0&&NpcEquipmentCombat.AttackFlat(b)>0&&itemPercent>0,"Real soft boundary/flat Dexterity/item percentage setup");
        var res=Res(b);int max=SkillHealth.Maximum256(b);SkillHealth.SetBalance256(b,max-257);SkillHealth.SetBalance256(legacy,SkillHealth.Maximum256(legacy)-257);TimedEffects.PreventHealing(b,TimedEffects.Now);TimedEffects.PreventHealing(legacy,TimedEffects.Now);
        int hp=SkillHealth.Balance256(b);long order=BattleCommand.PetOrder(b);ulong seed=NpcEquipmentRandom.State(b).Seed;string prepared=JsonSerializer.Serialize(b.Prepared);
        Train(p,10);Match(b,basis,ratingBase,itemPercent);
        var raw=legacy.SourceWeaponBase;Require(legacy.SkillWeaponDamage==(raw.Min*(100+raw.Percent+135)/100,raw.Max*(100+raw.Percent+135)/100),"Historical Iron fallback omitted direct damage affix");
        Require(Res(b).SequenceEqual(res)&&SkillHealth.Balance256(b)==hp&&SkillHealth.Maximum256(b)==max&&BattleCommand.PetOrder(b)==order&&NpcEquipmentRandom.State(b).Seed==seed&&JsonSerializer.Serialize(b.Prepared)==prepared,"Hard10 healed/recaptured/reordered/rerolled or changed resistances/controller");
        Train(p,20);Match(b,basis,ratingBase,itemPercent);Require(Res(b).SequenceEqual(res),"Unfinished Spectral resistance activated");
        ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);
        Require(!b.Deleted&&!legacy.Deleted&&NpcEquipmentDamage.Capture(b)==basis&&ClassStats.AttackRating(b)==ratingBase+(int)((long)ratingBase*itemPercent/100)&&legacy.SkillWeaponDamage==(raw.Min*(100+raw.Percent)/100,raw.Max*(100+raw.Percent)/100),"Respec retained Extra Strong or recaptured curve");Train(p,10);
        var facets=SummonDifficulties.Facets();PersistentMinions.Suspend(p);
        try
        {
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Nightmare),new(Map.Ilshenar.MapID,MonsterDifficulty.Hell)]);
            foreach(var map in new[]{Map.Felucca,Map.Ilshenar,Map.Trammel})
            {
                hp=SkillHealth.Balance256(b);max=SkillHealth.Maximum256(b);p.MoveToWorld(new(5450,1160,0),map);
                // This real original and the source curve retain their endpoint base in
                // every facet; captured summon ED is also difficulty-neutral.
                Match(b,basis,CreatureProfiles.AttackRating(b)+NpcEquipmentCombat.AttackFlat(b),itemPercent);Require(item.Parent==b&&SkillHealth.Balance256(b)==(long)hp*SkillHealth.Maximum256(b)/max,"Facet affix healed or lost original item");
            }
        }
        finally{SummonDifficulties.SetFacets(facets);}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,legacy.Serial.Value,JsonSerializer.Serialize(b.Prepared),SkillHealth.Balance256(b),SkillHealth.Balance256(legacy),SkillHealth.Maximum256(b),ClassStats.AttackRating(b),ClassStats.AttackRating(legacy),NpcEquipmentDamage.Capture(b).Value,order,seed,0)));
        Log.Information("Iron Extra Strong:12wild direct/inherited pairs,hard9+soft1refusal/live10/respec/relearn,actual original flat/Dexterity/percent AR and local ED,legacy fallback,three facets/no health/controller/recipe/seed change pass;Spectral Hit remains pending");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(IronGolemBody)World.FindMobile((Serial)s.Body);var legacy=(NecromancerMinion)World.FindMobile((Serial)s.Legacy);
        Require(s.Generation<2&&b.ControlMaster==p&&legacy.ControlMaster==p&&IronGolemUpgrades.HardRank(b)==10&&IronGolemUpgrades.DamagePercent(b)==135&&JsonSerializer.Serialize(b.Prepared)==s.Prepared&&NpcEquipmentDamage.Capture(b)==s.Packet&&ClassStats.AttackRating(b)==s.Rating&&ClassStats.AttackRating(legacy)==s.LegacyRating&&SkillHealth.Balance256(b)==s.Life&&SkillHealth.Balance256(legacy)==s.LegacyLife&&SkillHealth.Maximum256(b)==s.Maximum&&BattleCommand.PetOrder(b)==s.Order&&NpcEquipmentRandom.State(b).Seed==s.Seed,"Saved live affix/recipe/resources/item packet/order/stream changed");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=IronGolemDamageChecks.Owner(9,3,3740);FixtureNetwork.Authenticate(network,p,"iron-strong");var b=IronGolemDamageChecks.Learned(p,IronGolemDamageChecks.Original(p));var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(b.Location,p.Map);t.SetHits(20000);t.SetResistance(ResistanceType.Physical,0);TimedEffects.PreventHealing(t,TimedEffects.Now);
        var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;NpcEquipmentRandom.Set(b,IronGolemWorldChecks.AttackSeed(b,t));b.RestoreController(b.Prepared.Controller with{NextUtc=TimedEffects.Now.AddMilliseconds(40)});int life=SkillHealth.Balance256(t),damage=0;bool captured=false;long deadline=Core.TickCount+12000;
        void End(){SourceBlocking.BlockRoll=block;t.Delete();b.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(network.Running&&Core.TickCount<deadline&&b.Prepared.Controller.Fault==null,"Natural Extra Strong timeout/fault");
                if(!captured&&b.Prepared.Controller is{Mode:4} state)
                {
                    Require(IronGolemUpgrades.DamagePercent(b)==0&&SkillHealth.Balance256(t)==life,"Perk active before live training");Train(p,10);Require(IronGolemUpgrades.DamagePercent(b)==135,"Windup training did not activate direct affix");
                    ulong seed=NpcEquipmentRandom.State(b).Seed;var packet=IronGolemContact.Prepare(b,t,IronGolemContact.Require(b,p,t),()=>IronGolemWorld.Result(b,p,state,t));damage=packet.Impact.Packet.Physical256;packet.Cancel();NpcEquipmentRandom.Set(b,seed);captured=true;
                    Require(packet.Impact.Packet.Total256==damage&&damage>0&&SkillHealth.Balance256(t)==life,"Extra Strong prediction mutated HP");
                }
                if(SkillHealth.Balance256(t)==life){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(captured&&life-SkillHealth.Balance256(t)==damage&&b.Prepared.Controller.Attempts==1,"Actual release lost live hard10 damage");End();Log.Information("iron-golem-strong-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"iron-golem-strong-live-result: failed");}
        }
        Log.Information("iron-golem-strong-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
}
