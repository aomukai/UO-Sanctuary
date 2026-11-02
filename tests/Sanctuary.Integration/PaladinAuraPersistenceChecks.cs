// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class PaladinAuraPersistenceChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PaladinAuraPersistenceChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("paladin-aura-persistence-check","Holy Freeze,Conviction and knockback atomic restore;original retained mechanics",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("paladin-aura-persistence-check: passed");}catch(Exception e){Log.Error(e,"paladin-aura-persistence-check: failed");}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static bool Same<T>(T[] a,T[] b)=>JsonSerializer.Serialize(a)==JsonSerializer.Serialize(b);
    private static void Atomic<T>(string name,Func<T[]> snapshot,Action<T[]> restore,T invalid) where T:class
    {
        var all=snapshot();Require(all.Length>=2,name+" two live states required");
        var cases=new (string Name,T[] State)[]{("malformed-tail",all.Take(all.Length-1).Append(invalid).ToArray()),("duplicate-head",new[]{all[0],all[0]}),("null-head",new T[]{null}),("null-array",null)};
        foreach(var test in cases)
        {
            bool refused=false;try{restore(test.State);}catch(InvalidDataException){refused=true;}
            Require(refused&&Same(all,snapshot()),name+" atomic "+test.Name);
        }
        restore(JsonSerializer.Deserialize<T[]>(JsonSerializer.Serialize(all)));Require(Same(all,snapshot()),name+" JSON field roundtrip");
    }
    private static void Check()
    {
        var beforeFreeze=PaladinHolyFreeze.Snapshot();var beforeConviction=PaladinConviction.Snapshot();var beforeKnockback=SourceKnockbacks.Snapshot();
        var p=BlessedHammerDamageChecks.Owner();var a=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};var b=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};
        try
        {
            foreach(var pair in new[]{(Target:a,Offset:3),(Target:b,Offset:5)})
            {
                var target=pair.Target;target.MoveToWorld(new(p.X+pair.Offset,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();
                PaladinHolyFreeze.Slow(p,target,SkillMechanics.Values(p,CombatAbility.HolyFreeze),TimedEffects.Now);
                Require(PaladinHolyFreeze.Effect(target)!=null,"Actual mapped Holy Freeze state");
                Require(PaladinConviction.Apply(p,target,SkillMechanics.Values(p,CombatAbility.Conviction),TimedEffects.Now.AddSeconds(2)),"Actual Conviction state");
                Require(SourceKnockbacks.BeginPrepared(p,target),"Actual paused source knockback state");
            }
            var freeze=PaladinHolyFreeze.Effect(a);Atomic("Holy Freeze",PaladinHolyFreeze.Snapshot,PaladinHolyFreeze.Restore,freeze with{Rank=0});
            var conviction=PaladinConviction.Effect(a);Atomic("Conviction",PaladinConviction.Snapshot,PaladinConviction.Restore,conviction with{DefensePercent=101});
            var knockback=SourceKnockbacks.Snapshot().Single(x=>x.Target==a.Serial.Value);Atomic("Source knockback",SourceKnockbacks.Snapshot,SourceKnockbacks.Restore,knockback with{Length16=0});
            Require(SourceKnockbacks.Snapshot().Single(x=>x.Target==a.Serial.Value).PauseFrozen,"Version0 optional PauseFrozen field lost");
            Log.Information("Aura persistence:Holy Freeze/Conviction/knockback malformed tails,duplicate/null heads,null arrays refuse atomically;actual mapped states and JSON fields/PauseFrozen retain values;existing mechanics and original reload fixtures reused");
        }
        finally
        {
            PaladinHolyFreeze.Restore(beforeFreeze);PaladinConviction.Restore(beforeConviction);SourceKnockbacks.Restore(beforeKnockback);
            foreach(var actor in new Mobile[]{p,a,b}){actor.Corpse?.Delete();actor.Delete();}
        }
    }
}
