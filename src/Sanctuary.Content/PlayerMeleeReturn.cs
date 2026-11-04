// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Fixed-point ordinary NPC melee writes HP before attacked-in-melee reactions,
// then enters the player's death return. Scope identity keeps nested spells,
// ranged/PvP/owned actors and raw native Damage outside this admission.
internal static class PlayerMeleeReturn
{
    private static AttemptScope _current;
    private static HealthScope _health;
    internal static IDisposable Attempt(Mobile attacker,Mobile defender,IDisposable reactions)
    {
        if(attacker is not BaseCreature{Controlled:false,Summoned:false,ControlMaster:null,SummonMaster:null}||attacker is BaseHire
            ||defender is not PlayerMobile{Alive:true,Deleted:false} owner||!ClassCombat.Enabled(owner)){return reactions;}
        return _current=new(attacker,owner,reactions,_current);
    }
    internal static IDisposable Health(Mobile attacker,Mobile defender)
    {
        for(var scope=_current;scope!=null;scope=scope.Previous)
        {
            if(!scope.Done&&scope.Attacker==attacker&&scope.Owner==defender)
            {return _health=new(scope,_health);}
        }
        return null;
    }
    internal static bool Defer(Mobile target,Mobile attacker)
    {
        var scope=_health?.Attempt;
        if(scope is not{Done:false}||scope.Owner!=target||scope.Attacker!=attacker){return false;}
        scope.Lethal=true;return true;
    }
    private sealed class HealthScope(AttemptScope attempt,HealthScope previous):IDisposable
    {
        internal AttemptScope Attempt=attempt;
        private HealthScope _previous=previous;
        private bool _done;
        public void Dispose()
        {if(_done){return;}_done=true;_health=_previous;_previous=null;Attempt=null;}
    }
    private sealed class AttemptScope(Mobile attacker,PlayerMobile owner,IDisposable reactions,AttemptScope previous):IDisposable
    {
        internal Mobile Attacker=attacker;
        internal PlayerMobile Owner=owner;
        internal AttemptScope Previous=previous;
        internal bool Done,Lethal;
        private IDisposable _reactions=reactions;
        public void Dispose()
        {
            if(Done){return;}Done=true;
            try{_reactions?.Dispose();}
            finally
            {
                try
                {
                    // The source WILLDIE result is retained through late
                    // reactions; a return kill must not cancel the main hit.
                    if(Lethal&&Owner is{Alive:true,Deleted:false}){Owner.LastKiller=Attacker;Owner.Kill();}
                }
                finally{_current=Previous;Previous=null;Attacker=null;Owner=null;_reactions=null;}
            }
        }
    }
}
