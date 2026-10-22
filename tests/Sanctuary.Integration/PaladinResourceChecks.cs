using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class PaladinResourceChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(PaladinResourceChecks));
    private sealed record Saved(uint[] Owners,uint Ally,Point3D Origin,PaladinAuraSave State,TimedEffectState Poison,
        NecromancerState Curse,DateTime EliteUntil,int HealthFraction,int ManaFraction);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"paladin-resource-checks.json");
    public static void Configure(){Register("paladin-resource-check",Check);Register("paladin-resource-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prayer, Cleansing and Meditation resource/status consumers",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int milliseconds,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(milliseconds),()=>
    {try{action();}catch(Exception e){_log.Error(e,"paladin-resource-result: failed");}});
    private static void Select(PlayerMobile actor,CombatAbility ability)=>Require(SkillBindings.Instance.Assign(actor,false,ability),"Resource aura selection failed");
    private static void Due(params PlayerMobile[] owners)
    {
        var at=TimedEffects.Now;var ids=owners.Select(x=>x.Serial.Value).ToArray();var snapshot=PaladinAuras.Snapshot();
        PaladinAuras.Restore(snapshot with{Emitters=snapshot.Emitters.Select(x=>ids.Contains(x.Owner)?x with{NextUtc=at}:x).ToArray()});
    }
    private static void FreezeNaturalHealing(PlayerMobile actor)=>TimedEffects.Poison(actor,actor,.000001,60000,TimedEffects.Now);
    private static void Mana(PlayerMobile actor,int balance)
    {
        actor.Mana=balance/256;NecromancerEffects.Profiles.Store(actor,(NecromancerEffects.Profiles.Find(actor)??new()) with{ManaFraction=balance%256});
    }
    private static void Check()
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.MoveToWorld(ClassSkillChecks.Corridor(owner,5720,1550),owner.Map);
        var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);
        var necro=ClassSkillChecks.Player();necro.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);
        var pet=new SkeletonGuardian();pet.Initialize(necro,1,1);Require(pet.SetControlMaster(necro),"Prayer pet owner");pet.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);pet.Frozen=true;pet.AIObject.AITimer.Stop();
        var prayer=SkillMechanics.Values(owner,CombatAbility.Prayer);var row=SkillMechanics.For(CombatAbility.Prayer);
        var high=new SkillValues(row,k=>k==row.SourceKey?20:0,_=>0,99);
        Require(prayer.Eval("aurastatcalc1")==512 && prayer.Mana256==256 && high.Eval("aurastatcalc1")==6400 && high.Mana256==1168,"Prayer fixed-point heal/mana tiers");
        var cleanse=SkillMechanics.Values(owner,CombatAbility.Cleansing);var med=SkillMechanics.Values(owner,CombatAbility.Meditation);
        Require(cleanse.Eval("aurastatcalc1")==61 && cleanse.Eval("aurastatcalc2")==512 && cleanse.Mana256==0
            && med.Eval("aurastatcalc1")==300 && med.Eval("aurastatcalc2")==512,"Resource aura source values");
        var soft=new SkillValues(SkillMechanics.For(CombatAbility.Meditation),k=>k=="Prayer"?20:1,_=>0,99);
        var absent=new SkillValues(SkillMechanics.For(CombatAbility.Meditation),k=>k=="Meditation"?1:0,_=>0,99);
        Require(soft.Eval("aurastatcalc2")==6400 && absent.Eval("aurastatcalc2")==0,"Prayer synergy ignored effective rank or invented an unlearned heal");
        owner.Hits=owner.HitsMax;SkillHealth.Damage(owner,owner,128);
        Require(SkillHealth.Heal(owner,64)==64 && SkillHealth.Remainder256(owner)==64
            && SkillHealth.Heal(owner,256)==64 && SkillHealth.Remainder256(owner)==0,"Fractional healing debt or maximum cap");
        FreezeNaturalHealing(owner);FreezeNaturalHealing(ally);owner.Hits-=20;ally.Hits-=20;pet.Hits-=10;
        int a=owner.Hits,b=ally.Hits,c=pet.Hits,mana=owner.Mana*256;
        Select(owner,CombatAbility.Prayer);
        Require(owner.Hits==a && ally.Hits==b && owner.Mana*256==mana && PaladinAuras.Effect(owner,CombatAbility.Prayer)==null,"Prayer incorrectly applied immediately");
        Later(1400,()=>Require(owner.Hits==a && ally.Hits==b && pet.Hits==c,"Healing happened before first periodic pulse"));
        Later(2150,()=>
        {
            Require(owner.Hits==a+2 && ally.Hits==b+2 && pet.Hits==c+2 && NecromancerEffects.ManaBalance256(owner)==mana-256
                && PaladinAuras.ManaSuppressed(owner) && !owner.CanRegenMana,"Prayer did not heal poisoned allies/pets once or debit once per useful pulse");
            owner.Hits=owner.HitsMax;ally.Hits=ally.HitsMax;pet.Hits=pet.HitsMax;Mana(owner,owner.ManaMax*256);Due(owner);
            Later(80,()=>
            {
                Require(owner.Mana==owner.ManaMax && !PaladinAuras.ManaSuppressed(owner) && owner.CanRegenMana,"Idle Prayer spent mana or retained recovery suppression");
                Require(ClassCreation.Profiles.TryTrain(owner.Serial,"paladin.prayer",out _),"Prayer rank2 fixture");
                owner.Hits-=10;ally.Hits-=10;pet.Hits-=10;a=owner.Hits;b=ally.Hits;c=pet.Hits;Mana(owner,303);Due(owner);
                Later(80,()=>
                {
                    Require(owner.Hits==a && ally.Hits==b && pet.Hits==c && NecromancerEffects.ManaBalance256(owner)==303,"Insufficient fractional mana healed or paid");
                    Mana(owner,304);Due(owner);
                    Later(80,()=>
                    {
                        Require(owner.Hits==a+3 && ally.Hits==b+3 && pet.Hits==c+3 && NecromancerEffects.ManaBalance256(owner)==0,"Exact fractional cost or rank2 healing failed");
                        Flat(owner,ally,pet,necro);
                    });
                });
            });
        });
    }
    private static void Flat(PlayerMobile owner,PlayerMobile ally,SkeletonGuardian pet,PlayerMobile necro)
    {
        var dagger=new Dagger{Resource=CraftResource.Verite};var state=ClassGear.Profiles.Convert(dagger,99,GearRarity.Normal,new Random(1));
        ClassGear.Profiles.Store(dagger,state with{CraftedEquipment=true,MaterialRolls=[new("mana-per-five",1)]});Require(owner.EquipItem(dagger),"Prayer flat recovery fixture");
        var at=TimedEffects.Now;var snapshot=PaladinAuras.Snapshot();
        PaladinAuras.Restore(snapshot with{Emitters=snapshot.Emitters.Select(x=>x.Owner==owner.Serial.Value?x with{NextUtc=at.AddSeconds(10),FlatManaNumerator=0}:x).ToArray(),
            Leases=snapshot.Leases.Select(x=>x.Target==owner.Serial.Value && x.Ability==CombatAbility.Prayer?x with{UntilUtc=at.AddSeconds(10)}:x).ToArray()});
        Require(NecromancerEffects.ManaBalance256(owner)==0,"Flat recovery granted an equip tick");
        Later(400,()=>
        {
            int gain=NecromancerEffects.ManaBalance256(owner);
            Require(gain is >0 and <128 && !owner.CanRegenMana,"Prayer did not preserve fractional flat recovery independently of suppressed natural recovery");
            dagger.Delete();Select(owner,CombatAbility.None);Require(owner.CanRegenMana,"Leaving Prayer did not restore native eligibility");
            Cleansing(owner,ally,pet,necro);
        });
    }
    private static void Status(PlayerMobile source,PlayerMobile target,DateTime at,int milliseconds)
    {
        TimedEffects.Poison(source,target,.000001,milliseconds,at);
        NecromancerEffects.Profiles.Store(target,(NecromancerEffects.Profiles.Find(target)??new()) with
        {Curse=new(CombatAbility.Attract,source.Serial.Value,1,at.AddMilliseconds(milliseconds),target.X,target.Y)});
        EliteAuras.Curses.Apply(target.Serial,at.AddMilliseconds(milliseconds));EliteAuras.Refresh(target);
    }
    private static void Cleansing(PlayerMobile owner,PlayerMobile ally,SkeletonGuardian pet,PlayerMobile necro)
    {
        var at=TimedEffects.Now;Status(owner,ally,at,10000);GearEffects.ApplyChill(ally,Core.TickCount,10000,owner);
        var chill=TimedEffects.Snapshot().Single(x=>x.Owner==ally.Serial.Value).ChillUntilUtc;ally.Hits-=10;int before=ally.Hits;
        Select(owner,CombatAbility.Cleansing);Require(ally.Hits==before,"Cleansing healed on selection instead of pulse");Due(owner);
        Later(80,()=>
        {
            var lease=PaladinAuras.Effect(ally,CombatAbility.Cleansing);var pulse=lease.UntilUtc.AddMilliseconds(-2040);
            var poison=TimedEffects.Snapshot().Single(x=>x.Owner==ally.Serial.Value);
            Require(poison.PoisonMs is >5700 and <=6100 && poison.PoisonRate==.000001/10000 && poison.ChillUntilUtc==chill
                && NecromancerEffects.Curse(ally).UntilUtc==PaladinCleansing.Shorten(at.AddSeconds(10),pulse,61)
                && EliteAuras.Curses.Expires(ally.Serial)==PaladinCleansing.Shorten(at.AddSeconds(10),pulse,61)
                && ally.Hits==before+3,"Cleansing changed rate/chill, failed curable Attract/elite curse shortening or missed Prayer healing");
            Require(PaladinCleansing.Curable("attract") && !PaladinCleansing.Curable("shrine_skill"),"Pinned state curability flags changed");
            Meditation(owner,ally,pet,necro);
        });
    }
    private static void Meditation(PlayerMobile owner,PlayerMobile ally,SkeletonGuardian pet,PlayerMobile necro)
    {
        owner.Hits-=10;ally.Hits-=10;pet.Hits-=10;int a=owner.Hits,b=ally.Hits,c=pet.Hits;
        Select(owner,CombatAbility.Meditation);Require(PaladinAuras.ManaRecoveryPercent(ally)==0,"Meditation applied before its pulse");Due(owner);
        Later(80,()=>
        {
            Require(owner.Hits==a+3 && ally.Hits==b+3 && pet.Hits==c && PaladinAuras.ManaRecoveryPercent(ally)==300
                && PaladinAuras.ManaRecoveryPercent(pet)==0 && Math.Abs(GearEffects.RegenerationRate(ally,TimeSpan.FromSeconds(2),"mana-per-five").TotalSeconds-2/4.3)<.0001,
                "Meditation player-only filter, effective Prayer synergy or additive Warmth/recovery bonus failed");
            var other=ClassSkillChecks.Player(DiabloClass.Paladin);other.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);
            var third=ClassSkillChecks.Player(DiabloClass.Paladin);third.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);
            PaladinAuras.Clear(ally);ally.Hits-=10;b=ally.Hits;int mp=other.Mana,mp2=third.Mana;
            Select(other,CombatAbility.Prayer);Select(third,CombatAbility.Prayer);Due(owner,other,third);
            Later(80,()=>
            {
                Require(ally.Hits==b+7 && other.Mana==mp-1 && third.Mana==mp2-1,"Equal Prayer sources or separate Meditation healing did not stack as direct pulses");
                pet.Delete();necro.Delete();
                Require(PaladinAuras.ManaSuppressed(third),"Prayer death gate fixture");third.AddItem(new Backpack());third.Kill();third.Resurrect();
                Require(PaladinAuras.Selected(third)==CombatAbility.None && third.CanRegenMana,"Death/resurrection retained paid Prayer suppression");third.Corpse?.Delete();
                var reset=ClassSkillChecks.Player(DiabloClass.Paladin);reset.AddItem(new Backpack());reset.Hits-=10;Select(reset,CombatAbility.Prayer);Due(reset);
                Later(80,()=>
                {
                    Require(PaladinAuras.ManaSuppressed(reset),"Prayer respec gate fixture");ClassCreation.Profiles.TryGet(reset.Serial,out var profile);
                    ClassCreation.Profiles.ResetAllocatedPoints(reset,profile);
                    Require(PaladinAuras.Selected(reset)==CombatAbility.None && reset.CanRegenMana,"Respec retained paid Prayer suppression");reset.Delete();
                    Seed(owner,other,third,ally);_log.Information("paladin-resource-result: passed");
                });
            });
        });
    }
    private static PaladinAuraSave Scope(PaladinAuraSave state,uint[] owners)=>new(state.Emitters.Where(x=>owners.Contains(x.Owner)).ToArray(),state.Leases.Where(x=>owners.Contains(x.Source)).ToArray());
    private static void Seed(PlayerMobile owner,PlayerMobile prayer,PlayerMobile meditation,PlayerMobile ally)
    {
        Select(owner,CombatAbility.Cleansing);Select(meditation,CombatAbility.Meditation);
        var at=TimedEffects.Now.AddMinutes(5);uint[] owners=[owner.Serial.Value,prayer.Serial.Value,meditation.Serial.Value];
        var snapshot=PaladinAuras.Snapshot();snapshot=snapshot with
        {Emitters=snapshot.Emitters.Select(x=>owners.Contains(x.Owner)?x with{NextUtc=at,FlatManaNumerator=x.Owner==prayer.Serial.Value?123:0}:x).ToArray(),
         Leases=snapshot.Leases.Select(x=>owners.Contains(x.Source)?x with{UntilUtc=at.AddMilliseconds(40)}:x).ToArray()};
        PaladinAuras.Restore(snapshot);Status(owner,ally,at,20000);ally.Hits=ally.HitsMax-30;SkillHealth.Damage(owner,ally,128);
        var timed=TimedEffects.Snapshot();var poison=timed.Single(x=>x.Owner==ally.Serial.Value) with{UpdatedUtc=at,PoisonMs=20000};
        TimedEffects.Load(timed.Select(x=>x.Owner==ally.Serial.Value?poison:x).ToArray());
        var manaState=NecromancerEffects.Profiles.Find(prayer)??new();NecromancerEffects.Profiles.Store(prayer,manaState with{ManaFraction=17});
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owners,ally.Serial.Value,owner.Location,Scope(snapshot,owners),poison,
            NecromancerEffects.Profiles.Find(ally),EliteAuras.Curses.Expires(ally.Serial),128,17)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var snapshot=PaladinAuras.Snapshot();
        Require(JsonSerializer.Serialize(Scope(snapshot,saved.Owners))==JsonSerializer.Serialize(saved.State),"Saved resource aura emitter/lease/suppression/flat fraction changed");
        var actors=saved.Owners.Select(x=>World.FindMobile((Serial)x) as PlayerMobile).ToArray();var ally=World.FindMobile((Serial)saved.Ally) as PlayerMobile;
        Require(actors.All(x=>x is{Alive:true}) && ally is{Alive:true} && !actors[1].CanRegenMana
            && SkillHealth.Remainder256(ally)==saved.HealthFraction && NecromancerEffects.Profiles.Find(actors[1]).ManaFraction==saved.ManaFraction,
            "Saved resource actors, recovery gate or fractional resources missing");
        for(int i=0;i<actors.Length;i++){actors[i].MoveToWorld(new(saved.Origin.X+i*2,saved.Origin.Y,saved.Origin.Z),Map.Trammel);}
        ally.MoveToWorld(new(saved.Origin.X+1,saved.Origin.Y,saved.Origin.Z),Map.Trammel);
        var oldTimed=TimedEffects.Snapshot().Single(x=>x.Owner==ally.Serial.Value);var oldCurse=NecromancerEffects.Profiles.Find(ally);
        var oldElite=EliteAuras.Curses.Expires(ally.Serial);int oldHits=ally.Hits,oldMana=actors[1].Mana;var oldManaState=NecromancerEffects.Profiles.Find(actors[1]);
        var oldFraction=SkillHealth.Profiles.Remainders[ally.Serial];var at=TimedEffects.Now;
        Status(actors[0],ally,at,20000);int before=ally.Hits;int mana=actors[1].Mana;Due(actors);
        LaterReload(()=>
        {
            Require(ally.Hits==before+7 && actors[1].Mana==mana-1 && PaladinAuras.ManaRecoveryPercent(ally)==300
                && NecromancerEffects.Curse(ally).UntilUtc<at.AddSeconds(13)
                && TimedEffects.Snapshot().Single(x=>x.Owner==ally.Serial.Value).PoisonMs<13000,"Restored resource auras did not resume healing, payment, status shortening and recovery effects");
            PaladinAuras.Restore(snapshot);TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==ally.Serial.Value?oldTimed:x).ToArray());
            NecromancerEffects.Profiles.Store(ally,oldCurse);EliteAuras.Curses.Apply(ally.Serial,oldElite);EliteAuras.Refresh(ally);
            ally.Hits=oldHits;SkillHealth.Profiles.Remainders[ally.Serial]=oldFraction;
            actors[1].Mana=oldMana;NecromancerEffects.Profiles.Store(actors[1],oldManaState);
            _log.Information("paladin-resource-reload-result: passed");
        });
    }
    private static void LaterReload(Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(120),()=>
    {try{action();}catch(Exception e){_log.Error(e,"paladin-resource-reload-result: failed");}});
}
