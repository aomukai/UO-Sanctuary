// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Items;
using Server.Spells;

namespace Sanctuary;

public sealed record KnockbackSourceRule(string Source,bool Knockback,bool GetHit,int Distance);
public sealed record SourceKnockbackState(uint Target,int Map,Point3D Position,Point3D End,long Length16,long Travel16,long Budget16,DateTime NextUtc,bool Recovering,DateTime UntilUtc);
public sealed class SourceKnockbackProfiles:GenericPersistence
{
    public SourceKnockbackProfiles():base("SanctuarySourceKnockbacks",139){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(SourceKnockbacks.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown source knockback version");}
        SourceKnockbacks.Restore(JsonSerializer.Deserialize<SourceKnockbackState[]>(reader.ReadString()));
    }
}
public static class SourceKnockbacks
{
    private static readonly Dictionary<string,KnockbackSourceRule> _rules=new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<uint,SourceKnockbackState> _states=new();
    public static SourceKnockbackProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Load()
    {
        _rules.Clear();foreach(var row in BalanceCatalog.Read<KnockbackSourceRule>("knockback-source-rules")){_rules.Add(row.Source,row);}
    }
    public static void Initialize()
    {
        var created=CorpseRecoveryHooks.Created;CorpseRecoveryHooks.Created=corpse=>{Clear(corpse.Owner);created?.Invoke(corpse);};
        Timer.StartTimer(TimeSpan.Zero,TimeSpan.FromMilliseconds(40),Tick);
    }
    public static KnockbackSourceRule Rule(BaseCreature target)=>_rules.GetValueOrDefault(CreatureProfiles.Definition(target).Reference ?? "");
    internal static (int X,int Y,long Length16) Offset(int dx,int dy,int distance)
    {
        int maximum=Math.Max(Math.Abs(dx),Math.Abs(dy));if(maximum==0){return default;}
        int half=(distance>>1)+1,x=dx*half/maximum,y=dy*half/maximum;
        // Source endpoint is integer subtiles. The native projection truncates each
        // offset toward zero at two subtiles/tile, retaining the full travel duration.
        return(x/2,y/2,(long)Math.Sqrt((long)x*x*32768*32768+(long)y*y*32768*32768));
    }
    public static bool Begin(Mobile source,BaseCreature target)
    {
        if(target is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false} || source is not{Deleted:false}
            || source.Map!=target.Map || target.Map==null || target.Map==Map.Internal || Rule(target) is not{Knockback:true,GetHit:true,Distance:5} rule){return false;}
        var offset=Offset(target.X-source.X,target.Y-source.Y,rule.Distance);if(offset.Length16==0){return false;}
        var now=TimedEffects.Now;
        _states[target.Serial.Value]=new(target.Serial.Value,target.Map.MapID,target.Location,new(target.X+offset.X,target.Y+offset.Y,target.Z),offset.Length16,0,0,now.AddMilliseconds(40),false,default);
        if(target is GraveyardUndead undead){undead.InterruptForRecovery();}
        else if(target is SkeletonGuardian guardian){guardian.InterruptForRecovery();}
        if(target.Spell is Spell spell){spell.Disturb(DisturbType.Hurt);}
        target.AIObject?.ResetApproachState();
        return true;
    }
    public static void Clear(Mobile target){if(target!=null){_states.Remove(target.Serial.Value);}}
    public static int Remaining(Mobile target)
    {
        if(target is not BaseCreature{Alive:true,Deleted:false} || !_states.TryGetValue(target.Serial.Value,out var state)){return 0;}
        if(target.Map?.MapID!=state.Map || target.Location!=state.Position){Clear(target);return 0;}
        return state.Recovering?(int)Math.Clamp(Math.Ceiling((state.UntilUtc-TimedEffects.Now).TotalMilliseconds),0,int.MaxValue)
            :(int)Math.Clamp(Math.Ceiling((state.NextUtc-TimedEffects.Now).TotalMilliseconds)+40,1,int.MaxValue);
    }
    private static void Recover(BaseCreature target,SourceKnockbackState state,DateTime now)
    {
        // Source post-knockback enters GH independently of damage amount. Use the
        // existing mapped GH/FHR clock; complete modern mode/sand-leaper AI mapping is open.
        int frames=CreatureHitRecovery.Frames(CreatureProfiles.For(target).HitRecovery);
        _states[target.Serial.Value]=state with{Position=target.Location,Recovering=true,UntilUtc=now.AddMilliseconds(frames*40)};
        ClassCombat.SendAnimation(target,20,frames*40); // Identified native get-hit animation placeholder.
    }
    private static void Tick()
    {
        var now=TimedEffects.Now;
        foreach(uint serial in _states.Keys.ToArray())
        {
            if(!_states.TryGetValue(serial,out var state)){continue;}
            if(World.FindMobile((Serial)serial) is not BaseCreature{Alive:true,Deleted:false} target || target.Map?.MapID!=state.Map || target.Location!=state.Position)
            {_states.Remove(serial);continue;}
            if(state.Recovering){if(state.UntilUtc<=now){_states.Remove(serial);}continue;}
            if(state.NextUtc>now){continue;}
            // PATH velocity4096, base1024, >>6 gives one FP16 subtile/frame;
            // two source subtiles per native tile. Process at most one overdue frame.
            state=state with{Travel16=state.Travel16+32768,Budget16=state.Budget16+32768,NextUtc=now.AddMilliseconds(40)};
            if(target.Location.X!=state.End.X || target.Location.Y!=state.End.Y)
            {
                var direction=target.GetDirectionTo(state.End);bool diagonal=(direction&Direction.Mask) is Direction.Right or Direction.Down or Direction.Left or Direction.Up;
                int cost=diagonal?92682:65536;
                if(state.Budget16>=cost)
                {
                    if(!CombatDisplacement.TryStep(target,direction)){Recover(target,state,now);continue;}
                    state=state with{Position=target.Location,Budget16=state.Budget16-cost};
                }
            }
            if(state.Travel16>=state.Length16){Recover(target,state,now);}else{_states[serial]=state;}
        }
    }
    public static SourceKnockbackState[] Snapshot()=>_states.Values.OrderBy(x=>x.Target).ToArray();
    public static void Restore(SourceKnockbackState[] states)
    {
        _states.Clear();foreach(var state in states ?? throw new InvalidDataException("Missing source knockbacks"))
        {
            if(state.Map is <0 or >255 || state.Length16<=0 || state.Travel16<0 || state.Budget16<0 || state.NextUtc.Kind!=DateTimeKind.Utc
                || state.Recovering && state.UntilUtc.Kind!=DateTimeKind.Utc || !_states.TryAdd(state.Target,state)){throw new InvalidDataException("Invalid source knockback state");}
        }
    }
}
