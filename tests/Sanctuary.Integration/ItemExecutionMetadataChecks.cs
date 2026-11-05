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

public static class ItemExecutionMetadataChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ItemExecutionMetadataChecks));
    private static readonly CombatAbility[] Curses=[CombatAbility.AmplifyDamage,CombatAbility.DimVision,CombatAbility.Weaken,CombatAbility.IronMaiden,CombatAbility.Terror,CombatAbility.LifeTap,CombatAbility.Decrepify,CombatAbility.LowerResist];
    private static readonly int[] Durations=[500,375,590,540,300,640,160,700];
    private sealed record SavedTarget(uint Actor,NecromancerCurse Curse,BlindAiState Blind);
    private sealed record Saved(uint Owner,uint Source,SavedTarget[] Targets,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"item-execution-metadata-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("item-execution-metadata-check",false);Register("item-execution-metadata-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Current item execution metadata and actual NPC curse bodies",_=>Core.LoopContext.Post(()=>
    {
        try{Metadata();if(reload){Reload();}else{Cast();}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}
    }));
    private static void Refuses(Action action,string why)
    {try{action();}catch(NotSupportedException){return;}throw new InvalidOperationException(why);}
    private static void Metadata()
    {
        using var skills=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-execution-raw-skills.json")));
        using var missiles=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-execution-raw-missiles.json")));
        int count=0,links=0,unknown=0;
        foreach(var r in ItemSkillExecution.All)
        {
            var raw=skills.RootElement.GetProperty(r.Id.ToString());
            Require(raw.EnumerateObject().Count()==r.Fields.Count&&raw.EnumerateObject().All(p=>r.Fields.TryGetValue(p.Name,out var v)&&JsonElement.DeepEquals(p.Value,v)),"Dropped/changed raw current skill field");
            if(raw.TryGetProperty("srvmissile",out var key))
            {Require(r.Missile==missiles.RootElement.GetProperty(key.GetString()).GetProperty("*ID").GetInt32(),"Direct server missile link");links++;}
            else{Require(r.Missile==-1,"Missing server missile became missile0");}
            if(!raw.TryGetProperty("anim",out _)){Require(r.Animation==null,"Unknown animation became mode0");unknown++;}
            count++;
        }
        Require(count==429&&unknown==43&&ItemSkillExecution.MissileCount==742&&links>0,"Incomplete current metadata domain");
        Require(ItemSkillExecution.Skill(362) is{Name:"",ItemEffect:0}&&ItemSkillExecution.Skill(-1)==null&&ItemSkillExecution.Skill(429)==null,"Blank/address bounds changed");
        Require(ItemSkillExecution.Skill(54) is{ItemTarget:2,ItemTargetDo:true}&&ItemSkillExecution.Skill(88).ItemCheckStart,"Teleport/Bone Prison special cast metadata lost");
        Require(ItemSkillExecution.Skill(56).Number("localdelay")==30&&ItemSkillExecution.Skill(59).Number("localdelay")==45,"Current separate delay fields lost");
        Refuses(()=>ItemSkillExecution.Skill(56).LegacyRecord(true,0),"Modern player delay silently projected to legacy zero");
        Refuses(()=>ItemSkillExecution.All.First(r=>r.Animation==null).LegacyRecord(true,0),"Unknown player animation silently projected");
        Require(Curses.All(a=>ItemSkillExecution.RequireCurse(SkillMechanics.For(a).SourceId).Do==30),"Incomplete admitted curse metadata");
        Refuses(()=>ItemSkillExecution.RequireCurse(SkillMechanics.For(CombatAbility.Confuse).SourceId),"Confuse do61 misclassified as do30");
        Refuses(()=>ItemSkillExecution.RequireCurse(SkillMechanics.For(CombatAbility.Attract).SourceId),"Attract misclassified as do30");
        Log.Information("Item execution metadata:429raw current rows/742missile links/43explicit missing animations;special flags,current delays and unsupported projections preserved");
    }
    private static GraveyardSkeleton Target(PlayerMobile p,BloodGolemBody b)
    {
        var t=NpcOpenWoundsChecks.Target(p,b);
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(type,0);}
        return t;
    }
    private static void Effect(CombatAbility ability,BaseCreature t,BloodGolemBody b)
    {
        if(ability==CombatAbility.AmplifyDamage){Require(NecromancerEffects.Resistance(t,"physical",0)==-100,"NPC Amplify physical consumer");}
        if(ability==CombatAbility.Weaken){Require(NecromancerEffects.DamageStatPercent(t)==-37,"NPC Weaken damage consumer");}
        if(ability==CombatAbility.Decrepify){Require(NecromancerEffects.Resistance(t,"physical",0)==-50&&NecromancerEffects.DamageStatPercent(t)==-50&&NecromancerEffects.Slow(t)==50,"NPC Decrepify consumers");}
        if(ability==CombatAbility.LowerResist){Require(new[]{"fire","cold","ltng","pois"}.All(e=>NecromancerEffects.Resistance(t,e,0)==-47),"NPC Lower Resist elemental consumers");}
        if(ability==CombatAbility.LifeTap){SkillHealth.SetBalance256(b,1001);Require(NecromancerEffects.LifeTap(b,t,512)==256&&SkillHealth.Balance256(b)==1257,"NPC-origin Life Tap actual healing");}
        if(ability==CombatAbility.IronMaiden)
        {
            int before=SkillHealth.Balance256(t);IronMaiden.MeleeDamage(t,b,256,false);
            Require(SkillHealth.Balance256(t)==before-768,"NPC-origin Iron Maiden actual physical return");
        }
        if(ability==CombatAbility.DimVision){Require(NecromancerEffects.Profiles.Find(t).BlindAi!=null&&SourceBlind.Think(t,NecromancerEffects.Curse(t)),"NPC blind AI activation");}
        if(ability==CombatAbility.Terror){Require(NecromancerEffects.Curse(t).Terror!=null&&SourceTerror.Think(t,NecromancerEffects.Curse(t)),"NPC fear AI activation");}
    }
    private static void Cast()
    {
        var p=NpcOpenWoundsChecks.Owner(20,3160);var b=NpcOpenWoundsChecks.Body(p);var original=Target(p,b);
        original.MoveToWorld(new(b.X+30,b.Y,b.Z),b.Map);b.Combatant=original;
        Require(b.Combatant==original,"NPC curse original target setup");
        var saved=new List<SavedTarget>();var facets=SummonDifficulties.Facets();
        try
        {
            SummonDifficulties.SetFacets(facets.Where(x=>x.Map!=p.Map.MapID).Append(new(p.Map.MapID,MonsterDifficulty.Normal)).ToArray());
            NecromancerEffects.SetMana256(p,0);NecromancerEffects.SetMana256(b,0);
            for(int i=0;i<Curses.Length;i++)
            {
                var t=Target(p,b);var controller=b.WorldState.Controller;ulong seed=NpcEquipmentRandom.State(b).Seed;DateTime at=TimedEffects.Now;
                Require(NpcItemSpellCasts.CastCurse(b,t,Curses[i],5,at),"Actual NPC curse cast refused:"+Curses[i]);
                var curse=NecromancerEffects.Curse(t);
                Require(curse?.Ability==Curses[i]&&curse.Source==b.Serial.Value&&curse.Rank==5&&curse.UntilUtc==at.AddMilliseconds(Durations[i]*40L),"NPC curse source/rank/duration:"+Curses[i]);
                Require(b.Combatant==original&&b.WorldState.Controller==controller&&NpcEquipmentRandom.State(b).Seed==seed&&!NpcItemSpellCasts.KeepsCombatTarget(b),"NPC curse altered original targeting/controller/RNG or leaked scope");
                Require(t.Aggressors.Any(a=>a.Attacker==b)&&NecromancerEffects.ManaBalance256(p)==0&&NecromancerEffects.ManaBalance256(b)==0,"NPC curse lost aggression or paid mana");
                Effect(Curses[i],t,b);b.Combatant=original;
                var state=NecromancerEffects.Profiles.Find(t);DateTime held=TimedEffects.Now.AddDays(1);
                curse=state.Curse with{UntilUtc=held,Terror=state.Curse.Terror is{} fear?fear with{NextUtc=held}:null};
                var blind=state.BlindAi is{} ai?ai with{NextUtc=held,NextStepUtc=held}:null;
                NecromancerEffects.Profiles.Store(t,state with{Curse=curse,BlindAi=blind});t.Internalize();
                saved.Add(new(t.Serial.Value,curse,blind));
            }
            var unsupported=Target(p,b);try
            {
                ulong seed=NpcEquipmentRandom.State(b).Seed;
                Refuses(()=>NpcItemSpellCasts.CastCurse(b,unsupported,CombatAbility.Confuse,5,TimedEffects.Now),"Unsupported curse silently accepted");
                Require(NecromancerEffects.Curse(unsupported)==null&&NpcEquipmentRandom.State(b).Seed==seed&&!NpcItemSpellCasts.KeepsCombatTarget(b),"Unsupported curse changed victim/RNG/scope");
            }
            finally{unsupported.Delete();}
            uint source=b.Serial.Value;b.Combatant=null;b.Delete();original.Delete();p.Internalize();
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,source,saved.ToArray(),0)));
            Log.Information("NPC item curses:8actual do30 spells,rank5/source durations/physical/elemental/weakening/slow/healing/return/blind/fear consumers,no mana/extra RNG,target/controller/aggression and explicit unsupported refusal;held saved source removed");
        }
        finally{SummonDifficulties.SetFacets(facets);if(!b.Deleted){b.Delete();}if(!original.Deleted){original.Delete();}}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(s.Generation<2&&s.Targets.Length==8&&World.FindMobile((Serial)s.Source)==null,"Original NPC curse source/roster");
        foreach(var row in s.Targets)
        {
            var t=(BaseCreature)World.FindMobile((Serial)row.Actor);var state=NecromancerEffects.Profiles.Find(t);
            Require(state?.Curse==row.Curse&&state.BlindAi==row.Blind&&state.Curse.Source==s.Source&&state.Curse.Rank==5,"Original NPC curse/AI/source metadata changed across reload");
            if(state.Curse.Ability is not(CombatAbility.IronMaiden or CombatAbility.LifeTap or CombatAbility.DimVision or CombatAbility.Terror))
            {Effect(state.Curse.Ability,t,null);}
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
