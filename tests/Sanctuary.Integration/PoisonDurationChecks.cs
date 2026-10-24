// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class PoisonDurationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PoisonDurationChecks));
    private sealed record Target(uint Serial,int Rank,int Health,int Balance256,TimedEffectState State);
    private sealed record Saved(uint Source,int Generation,Target[] Targets);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"poison-duration.json");
    public static void Configure(){Register("poison-duration-check",Check);Register("poison-duration-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Long poison serialization and source scalar inspection",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Roundtrip(){TimedEffects.Profiles.Capture();var writer=new BufferWriter(new byte[1024*1024],true);TimedEffects.Profiles.Serialize(writer);TimedEffects.Profiles.Deserialize(new BufferReader(writer.Buffer[..(int)writer.Position]));}
    private static TimedEffectState State(Mobile target)=>TimedEffects.Snapshot().Single(x=>x.Owner==target.Serial.Value);
    private static void Hold(uint[] targets,DateTime at)=>TimedEffects.Load(TimedEffects.Snapshot().Select(x=>targets.Contains(x.Owner)?x with{UpdatedUtc=at}:x).ToArray());
    private static void Differential()
    {
        var row=SkillMechanics.For(CombatAbility.PoisonJavelin);string plague=SkillMechanics.For(CombatAbility.PlagueJavelin).SourceKey;
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"poison-javelin-damage-source-probe.json")));var names=doc.RootElement.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();int count=0;
        foreach(var r in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            bool cloud=r[0].GetInt32()!=0;int rank=r[1].GetInt32(),hard=r[2].GetInt32(),mastery=r[3].GetInt32(),item=r[4].GetInt32(),length=r[5].GetInt32();var values=new SkillValues(row,k=>k==row.SourceKey?rank:0,k=>k==plague?hard:0,99);
            int Expected(string key)=>r[8][Array.IndexOf(names,key)].GetInt32();
            Require(AmazonMelee.EnhanceEndpoint(values.Damage256(false),mastery)+(cloud?0:item)==Expected("nPoisonMinDamage")
                && AmazonMelee.EnhanceEndpoint(values.Damage256(true),mastery)+(cloud?0:item+19)==Expected("nPoisonMaxDamage")
                && values.DurationFrames+(cloud?0:length)==Expected("nPoisonLength"),"Complete source poison scalar/length differential");count++;
        }
        Require(count==3456,"Source poison scalar case count");Log.Information("Poison Javelin:3456 source/native scalar comparisons passed; no Poison Javelin handler enabled; shared regeneration checked separately");
    }
    private static void Check()
    {
        Differential();var source=MagicArrowChecks.Fresh("LongPoisonSource");var saved=new Target[2];int[] ranks=[28,255];DateTime start=TimedEffects.Now;
        for(int i=0;i<ranks.Length;i++)
        {
            int rank=ranks[i];var row=SkillMechanics.For(CombatAbility.PoisonJavelin);var values=new SkillValues(row,k=>k==row.SourceKey?rank:0,_=>0,99);int duration=values.DurationFrames*40;
            Require(duration==(rank==28?62000:516000),"Current skill long-duration golden");var target=new TimedEffectDummy{Frozen=true,RawStr=2000,Hits=2000,Name="LongPoisonTarget"};target.MoveToWorld(new(source.X+i+2,source.Y,source.Z),source.Map);
            // This is a duration/persistence test at declared1HP/sec, not a
            // claim that the unfinished Jav packet/regen consumer is integrated.
            Require(TimedEffects.Poison(source,target,duration/1000.0,duration,start),"Long duration rejected by runtime");TimedEffects.Settle(target,start.AddSeconds(1));var state=State(target);Require(state.PoisonMs==duration-1000 && state.PoisonMs>60000 && target.Hits==1999,"Initial long poison elapsed state");saved[i]=new(target.Serial.Value,rank,target.Hits,SkillHealth.Balance256(target),state);
        }
        uint[] ids=saved.Select(x=>x.Serial).ToArray();Hold(ids,TimedEffects.Now.AddMinutes(10));Roundtrip();
        for(int i=0;i<saved.Length;i++)
        {
            var target=World.FindMobile((Serial)saved[i].Serial);var row=State(target);Require(row.PoisonMs==saved[i].State.PoisonMs && row.PoisonRate==saved[i].State.PoisonRate,"Long poison real serializer roundtrip");
            foreach(int duration in new[]{60001,62000,516000,int.MaxValue}){TimedEffectProfiles.Validate(row with{PoisonMs=duration});}
            foreach(var invalid in new[]{row with{PoisonMs=-1},row with{PoisonRate=double.NaN},row with{PoisonFraction=1.1}})
            {bool rejected=false;try{TimedEffectProfiles.Validate(invalid);}catch(InvalidDataException){rejected=true;}Require(rejected,"Long-duration fix weakened another validation boundary");}
            saved[i]=saved[i] with{State=row};
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(source.Serial.Value,0,saved)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(World.FindMobile((Serial)saved.Source) is PlayerMobile,"Saved poison source missing");var rows=new Target[saved.Targets.Length];DateTime now=TimedEffects.Now;
        for(int i=0;i<rows.Length;i++)
        {
            var old=saved.Targets[i];var target=World.FindMobile((Serial)old.Serial);Require(target is{Alive:true,Deleted:false} && target.Hits==old.Health && SkillHealth.Balance256(target)==old.Balance256 && JsonSerializer.Serialize(State(target))==JsonSerializer.Serialize(old.State),$"Long poison duration/source/fraction reset or world load failed: health{target?.Hits}/{old.Health}; actual{JsonSerializer.Serialize(State(target))}; expected{JsonSerializer.Serialize(old.State)}");
            int elapsed=saved.Generation==0?400:600;var resumed=old.State with{UpdatedUtc=now};TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==old.Serial?resumed:x).ToArray());TimedEffects.Settle(target,now.AddMilliseconds(elapsed));var state=State(target);double accrued=old.State.PoisonFraction+elapsed*old.State.PoisonRate;int raw=(int)Math.Floor(accrued*256+1e-9);
            Require(SkillHealth.Balance256(target)==old.Balance256-raw && state.PoisonMs==old.State.PoisonMs-elapsed && state.PoisonSource==saved.Source && Math.Abs(state.PoisonFraction-(accrued-raw/256.0))<1e-9,"Resumed long poison elapsed fraction/repayment");rows[i]=old with{Health=target.Hits,Balance256=SkillHealth.Balance256(target),State=state};
        }
        Hold(rows.Select(x=>x.Serial).ToArray(),now.AddMinutes(10));for(int i=0;i<rows.Length;i++){rows[i]=rows[i] with{State=State(World.FindMobile((Serial)rows[i].Serial))};}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1,Targets=rows}));
    }
}
