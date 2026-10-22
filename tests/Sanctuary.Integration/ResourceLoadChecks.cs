// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ResourceLoadChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(ResourceLoadChecks));
    private const string Prefix="ResourceLoadFixture-";
    private sealed record Saved(uint Serial,DiabloClass? Class,int Mode,int Hits,int Mana,int Stam,
        int HitsMax,int ManaMax,int StamMax,int HealthFraction,int ManaFraction,StatAllocation Stats,uint[] Gear);
    private sealed record LoadingRead(int Hits,int Mana,int Stam,int HitsMax,int ManaMax,int StamMax,bool Loading);
    private static readonly Dictionary<uint,LoadingRead> _loading=new();
    private static bool _creating;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"resource-load-checks.json");

    [CallPriority(int.MaxValue)]
    public static void Configure()
    {
        FreezeRates();
        EventSink.WorldLoad+=CaptureLoading;
        Register("resource-load-check",Check);Register("resource-load-reload-check",Reload);
    }
    [CallPriority(int.MaxValue)]
    public static void Initialize()=>FreezeRates();
    private static void FreezeRates()
    {
        // Configure covers deserialization; Initialize wraps the final class recovery handlers.
        // The constructor scope covers native timers created before the fixture name is assigned.
        var hits=Mobile.HitsRegenRateHandler;var mana=Mobile.ManaRegenRateHandler;var stam=Mobile.StamRegenRateHandler;
        Mobile.HitsRegenRateHandler=actor=>Fixture(actor)?TimeSpan.FromHours(1):hits?.Invoke(actor)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=actor=>Fixture(actor)?TimeSpan.FromHours(1):mana?.Invoke(actor)??Mobile.DefaultManaRate;
        Mobile.StamRegenRateHandler=actor=>Fixture(actor)?TimeSpan.FromHours(1):stam?.Invoke(actor)??Mobile.DefaultStamRate;
    }
    private static bool Fixture(Mobile actor)=>actor is PlayerMobile && (_creating || actor.Name?.StartsWith(Prefix,StringComparison.Ordinal)==true);
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Class/legacy full and injured resource persistence",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool condition,string why){if(!condition){throw new InvalidOperationException(why);}}
    private static void CaptureLoading()
    {
        if(!File.Exists(Manifest)){return;}
        foreach(var saved in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            if(World.FindMobile((Serial)saved.Serial) is PlayerMobile actor)
            {_loading[saved.Serial]=new(actor.Hits,actor.Mana,actor.Stam,actor.HitsMax,actor.ManaMax,actor.StamMax,World.Loading);}
        }
    }
    private static uint Equip(PlayerMobile actor,string key)
    {
        var definition=NamedGear.Catalog.Items.Single(x=>x.SourceKey==key);
        var item=NamedGear.Create(definition.Id,99,new Random(319),true);
        Require(actor.EquipItem(item),$"Resource fixture cannot equip {key}");ClassGear.Changed(actor);return item.Serial.Value;
    }
    private static Saved Create(DiabloClass? character,int mode,int offset)
    {
        PlayerMobile actor;
        _creating=true;
        try{actor=new PlayerMobile{Body=0x190,Name=$"{Prefix}{character?.ToString()??"Legacy"}-{mode}",Player=true,Frozen=true};}
        finally{_creating=false;}
        actor.MoveToWorld(CombatPractice.FindFloor(Map.Felucca,1382+offset%6,1495+offset/6),Map.Felucca);
        uint[] gear=[];
        if(character is{} playable)
        {
            ClassCreation.Profiles.Assign(actor,playable);Require(ClassCreation.Profiles.AdvanceLevel(actor,99),"Resource fixture level");
            foreach(var (stat,count) in new[]{(CharacterStat.Strength,60),(CharacterStat.Vitality,150),(CharacterStat.Energy,150)})
            {for(int i=0;i<count;i++){Require(ClassCreation.Profiles.TrySpendStat(actor,stat),"Legitimate resource allocation rejected");}}
            gear=[Equip(actor,"Frostburn"),Equip(actor,"Raven Frost")];
            Require(ClassGear.Bonus(actor,"mana%")==40,"Eligible Frostburn mana percentage absent");
            Require(actor.HitsMax>200 && actor.ManaMax>actor.Int+40 && actor.StamMax>actor.Dex,"Fixture does not exceed legacy maxima");
        }
        else{actor.RawStr=actor.RawDex=actor.RawInt=100;}
        actor.Hits=mode==0?actor.HitsMax:mode==1?actor.HitsMax-23:5;
        actor.Mana=mode==0?actor.ManaMax:mode==1?actor.ManaMax-27:5;
        actor.Stam=mode==0?actor.StamMax:mode==1?actor.StamMax-31:5;
        if(mode!=0)
        {
            SkillHealth.Damage(actor,actor,17);
            if(character!=null){NecromancerEffects.Profiles.Store(actor,(NecromancerEffects.Profiles.Find(actor)??new()) with{ManaFraction=91});}
        }
        return new(actor.Serial.Value,character,mode,actor.Hits,actor.Mana,actor.Stam,actor.HitsMax,actor.ManaMax,actor.StamMax,
            SkillHealth.Remainder256(actor),NecromancerEffects.Profiles.Find(actor)?.ManaFraction??0,ClassCreation.Profiles.Stats(actor.Serial),gear);
    }
    private static void Check()
    {
        var saved=new List<Saved>();
        foreach(var character in new[]{DiabloClass.Necromancer,DiabloClass.Sorceress,DiabloClass.Paladin,DiabloClass.Barbarian,
                    DiabloClass.Amazon,DiabloClass.Assassin,DiabloClass.Druid,DiabloClass.Warlock})
        {for(int mode=0;mode<2;mode++){saved.Add(Create(character,mode,saved.Count));}}
        saved.Add(Create(DiabloClass.Paladin,2,saved.Count));
        saved.Add(Create(null,0,saved.Count));saved.Add(Create(null,1,saved.Count));
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
        Require(saved.Count==19 && saved.Count(x=>x.Class!=null)==17,"Resource fixture roster");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest));
        foreach(var expected in saved)
        {
            var actor=World.FindMobile((Serial)expected.Serial) as PlayerMobile;
            Require(actor is{Alive:true,Deleted:false},"Resource fixture owner missing");
            Require(_loading.TryGetValue(expected.Serial,out var loaded) && loaded.Loading,"Loading-phase resource sample missing");
            Require((loaded.Hits,loaded.Mana,loaded.Stam)==(expected.Hits,expected.Mana,expected.Stam),
                $"{actor.Name}: raw pools clamped during load: {loaded.Hits}/{loaded.Mana}/{loaded.Stam}, expected {expected.Hits}/{expected.Mana}/{expected.Stam}");
            Require((actor.Hits,actor.Mana,actor.Stam)==(expected.Hits,expected.Mana,expected.Stam),$"{actor.Name}: resource balance changed after load");
            Require((actor.HitsMax,actor.ManaMax,actor.StamMax)==(expected.HitsMax,expected.ManaMax,expected.StamMax),$"{actor.Name}: actual maxima changed");
            Require(SkillHealth.Remainder256(actor)==expected.HealthFraction && (NecromancerEffects.Profiles.Find(actor)?.ManaFraction??0)==expected.ManaFraction,
                $"{actor.Name}: resource fractions changed");
            Require(ClassCreation.Profiles.Stats(actor.Serial)==expected.Stats && expected.Gear.All(serial=>World.FindItem((Serial)serial)?.Parent==actor),
                $"{actor.Name}: allocated stats or equipped identity changed");
            Require(ClassCreation.Profiles.TryGet(actor.Serial,out var profile)==(expected.Class!=null) && (expected.Class==null || profile.Class==expected.Class),
                $"{actor.Name}: active class changed");
            if(expected.Class is{} character)
            {
                Require(ClassGear.Bonus(actor,"mana%")==40,"Loaded Frostburn mana percentage absent");
                var title=EarnedTitles.Profiles.Find(actor.Serial);
                Require(title.Classes==(byte)(1<<((int)character-101)) && title.Prefix==0,"Earned class bit or unset title changed");
                if(character==DiabloClass.Warlock){Require(EarnedTitles.Prefix(character)=="","Unapproved Warlock achievement title was invented");}
            }
        }
        _log.Information("Resource load retained {Count} full/injured/low class and legacy actors",saved.Length);
    }
}
