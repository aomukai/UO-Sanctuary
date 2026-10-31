// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class PaladinThorns
{
    private static AttemptScope _current;
    public static void Configure()
    {
        ClassCombatHooks.MeleeAttempt=Attempt;
        ClassCombatHooks.PreparedWeaponDamage=NecromancerEffects.PreparedNativeWeaponHit;
    }
    // Legacy public entry point now consumes the shared physical ATD stat.
    private readonly record struct ReturnStats(int Flat,int Percent);
    private static ReturnStats Values(Mobile actor)
    {
        if(actor==null){return default;}var thorns=PaladinAuras.Effect(actor,CombatAbility.Thorns);
        long flat=(thorns?.Percent??0)+(long)DruidWolverineAuras.FlatDamage(actor);
        if(actor is PlayerMobile p && ClassCombat.Enabled(p)){flat+=ClassGear.Bonus(p,"thorns");}
        else{flat+=NpcEquipmentRetaliation.Flat(actor);}
        return new((int)Math.Clamp(flat,0,int.MaxValue),Math.Max(0,thorns?.DamagePercent??0));
    }
    internal static int FlatDamage(Mobile actor)=>Values(actor).Flat;
    public static bool Active(Mobile actor)=>Values(actor) is{Flat:>0} or{Percent:>0};
    public static int ReturnedPercent(Mobile attacker,int percent)=>attacker is PlayerMobile or BaseHire ? (percent+4)/8 : percent;
    public static IDisposable Attempt(Mobile attacker,Mobile defender,bool ranged)
    {
        if(ranged || !Eligible(attacker,defender)){return null;}var stats=Values(defender);
        if(stats==default){return null;}return _current=new(attacker,defender,stats,_current);
    }
    private static bool Eligible(Mobile attacker,Mobile defender)=>attacker is{Alive:true,Deleted:false,Blessed:false}
        && defender is{Deleted:false} && attacker!=defender && attacker.Map==defender.Map && attacker.Map is not null
        && attacker.Map!=Map.Internal && !ClassCombat.Friendly(attacker,defender);
    public static void Prepared(Mobile attacker,Mobile defender,int physical256,bool ranged,int? clippedPhysical256=null)
    {
        if(ranged || !Eligible(attacker,defender)){return;}
        var scope=_current!=null && _current.Attacker==attacker && _current.Defender==defender ? _current : null;
        var stats=defender.Alive?Values(defender):scope?.Stats??Values(defender);
        if(stats==default){return;}
        // ExecuteEvents caps the copied physical component before the attacked-in-melee events and Thorns call.
        long eligiblePhysical=clippedPhysical256 is{} clipped?Math.Clamp(clipped,0,Math.Max(0,physical256)):Math.Min(Math.Max(0,physical256),Math.Max(0,SkillHealth.Balance256(defender)));
        Flat(attacker,defender,stats,scope);
        if(eligiblePhysical>0 && attacker is{Alive:true,Deleted:false})
        {Retaliate(defender,attacker,eligiblePhysical*Math.Max(0,ReturnedPercent(attacker,stats.Percent))/100);}
    }
    private static void Flat(Mobile attacker,Mobile defender,ReturnStats stats,AttemptScope scope)
    {
        if(scope!=null&&scope.FlatApplied){return;}
        if(scope!=null){scope.FlatApplied=true;}
        Retaliate(defender,attacker,(long)stats.Flat*256);
    }
    // Ordered NPC consumers dispatch the attacked-in-melee event (including
    // misses) separately from the later successful-hit percentage return.
    internal static void PreparedFlatAttempt(Mobile attacker,Mobile defender)
    {
        if(!Eligible(attacker,defender)){return;}
        var scope=_current!=null&&_current.Attacker==attacker&&_current.Defender==defender?_current:null;
        var stats=defender.Alive?Values(defender):scope?.Stats??Values(defender);
        Flat(attacker,defender,stats,scope);
    }
    internal static void PreparedPercentage(Mobile attacker,Mobile defender,int clippedPhysical256)
    {
        if(clippedPhysical256<=0||!Eligible(attacker,defender)){return;}
        var scope=_current!=null&&_current.Attacker==attacker&&_current.Defender==defender?_current:null;
        var stats=defender.Alive?Values(defender):scope?.Stats??Values(defender);
        Retaliate(defender,attacker,(long)clippedPhysical256*Math.Max(0,ReturnedPercent(attacker,stats.Percent))/100);
    }
    internal static void Retaliate(Mobile defender,Mobile attacker,long physical256)
    {
        if(physical256<=0 || !Eligible(attacker,defender)){return;}
        // Return damage is a separate physical packet, not an enhanced weapon strike or another melee attempt.
        // Share the physical absorption/resistance pipeline, including the
        // prepared Psychic Ward pool, without weapon/mastery/potion additions.
        double value=SkillCombat.MitigatePrepared(defender,attacker,
            new(Math.Min(physical256,int.MaxValue)/256.0,0,0,0,0)).Physical;
        defender.DoHarmful(attacker,true);
        SkillHealth.Damage(defender,attacker,(int)Math.Clamp(Math.Floor(value*256),0,int.MaxValue));
        attacker.FixedEffect(0x374A,5,5,0x481,0); // Placeholder retaliation flash; independent effect register.
    }
    private sealed class AttemptScope:IDisposable
    {
        public Mobile Attacker{get;private set;}
        public Mobile Defender{get;private set;}
        public ReturnStats Stats{get;private set;}
        public bool FlatApplied{get;set;}
        private AttemptScope _previous;
        private bool _disposed;
        public AttemptScope(Mobile attacker,Mobile defender,ReturnStats stats,AttemptScope previous)
        {Attacker=attacker;Defender=defender;Stats=stats;_previous=previous;}
        public void Dispose()
        {
            if(_disposed){return;}
            try
            {
                if(!FlatApplied)
                {
                    FlatApplied=true;
                    var stats=Defender.Alive?Values(Defender):Stats;
                    Retaliate(Defender,Attacker,(long)stats.Flat*256);
                }
            }
            finally{_disposed=true;_current=_previous;_previous=null;Attacker=Defender=null;Stats=default;}
        }
    }
}
