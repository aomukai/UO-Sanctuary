// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

// Result flag32 suppresses the source weapon damage and kill event callbacks.
// Scope the pair so an unrelated nested retaliation/death keeps its own events.
internal static class CombatItemEvents
{
    [ThreadStatic] private static Scope _current;
    [ThreadStatic] private static KillRecoveryScope _killRecovery;
    internal static bool Allowed(Mobile source,Mobile target)
    {for(var scope=_current;scope!=null;scope=scope.Previous){if(scope.Source==source && scope.Target==target){return false;}}return true;}
    internal static IDisposable Suppress(Mobile source,Mobile target)=>_current=new(source,target,_current);
    internal static bool KillRecoveryAllowed(Mobile source,Mobile target)
    {
        if(!Allowed(source,target)){return false;}
        for(var scope=_killRecovery;scope!=null;scope=scope.Previous)
        {if(scope.Source==source&&scope.Target==target){return false;}}
        return true;
    }
    internal static IDisposable SuppressKillRecovery(Mobile source,Mobile target)=>_killRecovery=new(source,target,_killRecovery);
    private sealed class KillRecoveryScope(Mobile source,Mobile target,KillRecoveryScope previous):IDisposable
    {
        internal Mobile Source=>source;internal Mobile Target=>target;internal KillRecoveryScope Previous=>previous;
        public void Dispose()
        {if(_killRecovery!=this){throw new InvalidOperationException("Kill recovery scopes must close in order");}_killRecovery=previous;}
    }
    private sealed class Scope(Mobile source,Mobile target,Scope previous):IDisposable
    {
        internal Mobile Source=>source;internal Mobile Target=>target;internal Scope Previous=>previous;
        private bool _disposed;
        public void Dispose()
        {if(_disposed){return;}if(_current!=this){throw new InvalidOperationException("Combat event scopes must close in order");}_disposed=true;_current=previous;}
    }
}
