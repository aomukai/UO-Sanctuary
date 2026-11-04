// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class CurseAiFamilyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CurseAiFamilyChecks));
    private static readonly string[] Sources=["fallen1","brute1","goatman1"];
    private sealed record Actor(uint Serial,string Profile,Point3D Point,int Life,NecromancerCurse Curse,BlindAiState Blind);
    private sealed record Saved(uint Owner,int Map,Point3D Point,int Life,int Mana,Actor[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"curse-ai-families-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        // This optional fixture file is copied only into the disposable suite.
        // BalanceCatalog's -100 Configure has already loaded real profiles.
        if(File.Exists(Path.Join(Core.BaseDirectory,"curse-ai-transitions-source.json")))
        {
            var profiles=(Dictionary<string,DungeonProfile>)typeof(EncounterProfiles).GetField("_profiles",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            var basis=EncounterProfiles.Profile("crypt-sentinel-60");
            foreach(string source in Sources.Append("sandmaggot1")){string id="curse-ai-fixture-"+source;profiles.Add(id,basis with{Id=id,Reference=source});}
        }
        Register("curse-ai-families-check",Check);Register("curse-ai-families-reload-check",Reload);
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Shared curse AI family casting and original saved recipients",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureCurseAi";
        void Train(string id)
        {
            TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var talent);
            foreach(string prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(prior);}}
            if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
        }
        Train("necromancer.dimvision");Train("necromancer.terror");FixtureSpace.Corridor(p,6900,30,3500);
        SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2-17);NecromancerEffects.SetMana256(p,p.ManaMax*256-97);return p;
    }
    private static EncounterCreature Enemy(PlayerMobile p,string source)
    {
        var profile=EncounterProfiles.Profile("curse-ai-fixture-"+source);var t=new EncounterCreature();
        t.Assign(profile,60,MonsterDifficulty.Normal,EncounterGrade.Ordinary,ChampionKind.Champion,[],EliteAura.None,null,new Random(1701));
        t.NoKillAwards=true;t.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.Frozen=false;t.CantWalk=false;
        SkillHealth.SetBalance256(t,SkillHealth.Maximum256(t)/2-31);return t;
    }
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"curse-ai-transitions-source.json")));
        Require(doc.RootElement.GetArrayLength()==288,"Missing complete source transition contexts");
        foreach(var row in doc.RootElement.EnumerateArray())
        {Require(row[4].GetInt32()==1&&row[5].GetInt32()==row[2].GetInt32()&&row[6].GetInt32()==(row[2].GetInt32()==11?1:0),"Source transition clear/dispatch contract");}
        var families=new HashSet<string>{"Skeleton","Zombie","Fallen","Brute","Goatman","Megademon"};
        var rules=BalanceCatalog.Read<ConversionSourceRule>("conversion-source-rules");var blind=BalanceCatalog.Read<BlindAiRule>("blind-ai-rules");
        Require(blind.Length==50&&blind.Select(r=>r.Source).ToHashSet().SetEquals(rules.Where(r=>families.Contains(r.Ai)).Select(r=>r.Source)),"Current family timing/range catalogue incomplete");
    }
    private static void Cast(PlayerMobile p,EncounterCreature target,CombatAbility ability)
    {
        PoisonDaggerChecks.Ready(p);ClassCombat.For(p).RecoveryUntil=Core.TickCount;
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
        target.Combatant=p;target.AIObject.Action=ActionType.Wander;
        Require(ClassCombat.Perform(p,ability,target,target.Location,Core.TickCount),"Learned cast refused for "+target.Dungeon.Reference);
        ClassCombat.ReleaseCast(p);var session=ClassCombat.For(p);
        Require(NecromancerEffects.Curse(target)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Cast applied/paid before release");
        Require(ClassCombat.ResolveAction(p,session.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Learned once-only release/payment");
        ClassCombat.Stop(p);
        Require(NecromancerEffects.Curse(target)?.Ability==ability,"Learned cast did not deliver family curse");
    }
    private static void Resume(PlayerMobile p,EncounterCreature t,CombatAbility ability,bool initial=false)
    {
        t.NextReacquireTime=Core.TickCount;t.AIObject.NextMove=Core.TickCount;
        if(ability==CombatAbility.DimVision)
        {
            var current=NecromancerEffects.Profiles.Find(t);Require(current.BlindAi!=null&&SourceBlind.Supported(t),"Missing admitted blind controller");
            ulong seed=current.BlindAi.Seed;
            if(initial){for(int low=1;;low++){ulong candidate=SourceRandom.InitLow(low),next=candidate;if(Enumerable.Range(0,3).All(_=>SourceRandom.Roll(ref next)%100>=20)){seed=candidate;break;}}}
            NecromancerEffects.Profiles.Store(t,current with{BlindAi=current.BlindAi with{Seed=seed,NextUtc=TimedEffects.Now,Goal=null,NextStepUtc=null}});
            Require(NecromancerEffects.Think(t),"Shared blind controller did not handle new family");
            var state=NecromancerEffects.Profiles.Find(t).BlindAi;
            Require(state.Fault==null&&state.Thinks>0&&state.Draws>0&&state.Goal==null&&!state.Combat&&state.NextUtc>TimedEffects.Now,"New-family blind idle/draw deadline");
        }
        else
        {
            Require(SourceTerror.Supported(t)&&NecromancerEffects.Think(t),"Shared Terror controller did not handle new family");
            Require(SourceTerror.State(t) is{Started:true,Override:0,Combat:false,Goal:not null},"New-family Terror did not enter escape");
        }
    }
    private static Actor Hold(EncounterCreature t)
    {
        var state=NecromancerEffects.Profiles.Find(t);DateTime until=TimedEffects.Now.AddDays(1);
        var curse=state.Curse with{UntilUtc=until,Terror=state.Curse.Terror is{} terror?terror with{NextUtc=until}:null};
        var blind=state.BlindAi is{} ai?ai with{NextUtc=until}:null;
        NecromancerEffects.Profiles.Store(t,state with{Curse=curse,BlindAi=blind});
        var saved=new Actor(t.Serial.Value,t.Dungeon.Id,t.Location,SkillHealth.Balance256(t),curse,blind);t.Internalize();return saved;
    }
    private static void Check()
    {
        Source();var p=Owner();var saved=new List<Actor>();
        foreach(string source in Sources)foreach(var ability in new[]{CombatAbility.DimVision,CombatAbility.Terror})
        {
            var t=Enemy(p,source);Require(SourceTerror.Supported(t)&&SourceBlind.Supported(t),"New family remains unsupported");
            int life=SkillHealth.Balance256(t);Cast(p,t,ability);Resume(p,t,ability,initial:true);Require(SkillHealth.Balance256(t)==life,"AI handoff changed recipient wounds");
            saved.Add(Hold(t));
        }
        var unsupported=Enemy(p,"sandmaggot1");
        try{Require(!SourceTerror.Supported(unsupported)&&!SourceBlind.Supported(unsupported)&&!NecromancerEffects.Apply(p,unsupported,CombatAbility.Terror,1),"Deferred-exit family gained an immediate transition");}finally{unsupported.Delete();}
        var s=new Saved(p.Serial.Value,p.Map.MapID,p.Location,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),saved.ToArray(),0);
        p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s));
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        Require(SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Reload changed owner wound/mana balances");p.MoveToWorld(s.Point,Map.Maps[s.Map]);
        var next=new List<Actor>();
        foreach(var row in s.Actors)
        {
            var t=(EncounterCreature)World.FindMobile((Serial)row.Serial);var state=NecromancerEffects.Profiles.Find(t);
            Require(t.Dungeon.Id==row.Profile&&SkillHealth.Balance256(t)==row.Life&&state.Curse==row.Curse&&state.BlindAi==row.Blind,"Reload replaced actor/identity/wounds/curse/stream");
            t.MoveToWorld(row.Point,p.Map);t.AIObject.AITimer.Stop();t.Frozen=false;t.CantWalk=false;
            if(row.Curse.Terror is{} terror){NecromancerEffects.Profiles.Store(t,state with{Curse=state.Curse with{Terror=terror with{NextUtc=TimedEffects.Now}}});}
            Resume(p,t,row.Curse.Ability);Require(SkillHealth.Balance256(t)==row.Life,"Resumed AI changed wounds");next.Add(Hold(t));
        }
        p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Actors=next.ToArray(),Generation=s.Generation+1}));
    }
}
