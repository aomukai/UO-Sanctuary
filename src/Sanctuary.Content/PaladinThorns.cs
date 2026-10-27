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
    public static bool Active(Mobile actor)=>actor!=null && PaladinAuras.Effect(actor,CombatAbility.Thorns)!=null;
    public static int ReturnedPercent(Mobile attacker,int percent)=>attacker is PlayerMobile or BaseHire ? (percent+4)/8 : percent;
    public static IDisposable Attempt(Mobile attacker,Mobile defender,bool ranged)
    {
        if(ranged || !Eligible(attacker,defender) || PaladinAuras.Effect(defender,CombatAbility.Thorns) is not {} lease){return null;}
        return _current=new(attacker,defender,lease,_current);
    }
    private static bool Eligible(Mobile attacker,Mobile defender)=>attacker is{Alive:true,Deleted:false,Blessed:false}
        && defender is{Deleted:false} && attacker!=defender && attacker.Map==defender.Map && attacker.Map is not null
        && attacker.Map!=Map.Internal && !ClassCombat.Friendly(attacker,defender);
    public static void Prepared(Mobile attacker,Mobile defender,int physical256,bool ranged,int? clippedPhysical256=null)
    {
        if(ranged || !Eligible(attacker,defender)){return;}
        var scope=_current!=null && _current.Attacker==attacker && _current.Defender==defender ? _current : null;
        var lease=PaladinAuras.Effect(defender,CombatAbility.Thorns) ?? (defender.Alive?null:scope?.Lease);
        if(lease==null){return;}
        // ExecuteEvents caps the copied physical component before the attacked-in-melee events and Thorns call.
        long eligiblePhysical=clippedPhysical256 is{} clipped?Math.Clamp(clipped,0,Math.Max(0,physical256)):Math.Min(Math.Max(0,physical256),Math.Max(0,SkillHealth.Balance256(defender)));
        if(scope==null || !scope.FlatApplied)
        {
            if(scope!=null){scope.FlatApplied=true;}
            Retaliate(defender,attacker,(long)Math.Max(0,lease.Percent)*256);
        }
        if(eligiblePhysical>0 && attacker is{Alive:true,Deleted:false})
        {Retaliate(defender,attacker,eligiblePhysical*Math.Max(0,ReturnedPercent(attacker,lease.DamagePercent))/100);}
    }
    private static void Retaliate(Mobile defender,Mobile attacker,long physical256)
    {
        if(physical256<=0 || !Eligible(attacker,defender)){return;}
        // Return damage is a separate physical packet, not an enhanced weapon strike or another melee attempt.
        var absorbed=SorceressEffects.Absorb(attacker,new(Math.Min(physical256,int.MaxValue)/256.0,0,0,0,0));
        double raw=NecromancerEffects.AbsorbPhysical(attacker,absorbed.Physical);
        double value=NamedElements.Damage(defender,attacker,raw,"physical",enhance:false);
        defender.DoHarmful(attacker,true);
        SkillHealth.Damage(defender,attacker,(int)Math.Clamp(Math.Floor(value*256),0,int.MaxValue));
        attacker.FixedEffect(0x374A,5,5,0x481,0); // Placeholder retaliation flash; independent effect register.
    }
    private sealed class AttemptScope:IDisposable
    {
        public Mobile Attacker{get;private set;}
        public Mobile Defender{get;private set;}
        public PaladinAuraLease Lease{get;private set;}
        public bool FlatApplied{get;set;}
        private AttemptScope _previous;
        private bool _disposed;
        public AttemptScope(Mobile attacker,Mobile defender,PaladinAuraLease lease,AttemptScope previous)
        {Attacker=attacker;Defender=defender;Lease=lease;_previous=previous;}
        public void Dispose()
        {
            if(_disposed){return;}
            try
            {
                if(!FlatApplied)
                {
                    FlatApplied=true;
                    var lease=PaladinAuras.Effect(Defender,CombatAbility.Thorns) ?? (Defender.Alive?null:Lease);
                    if(lease!=null){Retaliate(Defender,Attacker,(long)Math.Max(0,lease.Percent)*256);}
                }
            }
            finally{_disposed=true;_current=_previous;_previous=null;Attacker=Defender=null;Lease=null;}
        }
    }
}
