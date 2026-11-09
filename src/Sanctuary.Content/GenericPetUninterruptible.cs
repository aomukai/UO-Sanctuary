// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record GenericPetUninterruptibleUnit(int Kind,int Game,byte Selector,bool HasRoot,int Root1c,uint Rootb38,
    uint Flags128,int LastKind,int LastId);
internal sealed record GenericPetUninterruptibleServices(Func<int,GenericPetUninterruptibleUnit> Unit,Action<int,int,int> State,
    Func<byte,int> StateCount,Func<int,int,int,int> Resolve,Action<int,int,int,PreparedMonsterDamagePacket> FreshReturn,
    Action<int,int,int,int> OwnerLoss,GenericPetTimedEvents Events,Action<int> Remove);
internal static class GenericPetUninterruptible
{
    // Current373690 reuses the existing shared mode-completion policy. Native
    // state writes,damage return and death leaves remain mandatory services.
    internal static void Change(int actor,int enabled,GenericPetUninterruptibleServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        MonsterUninterruptible.Change(enabled!=0,new Current(actor,enabled,services));
    }
    internal static GenericPetUninterruptibleServices BindOwnerLoss(GenericPetUninterruptibleServices original,GenericPetOwnerLossServices loss)
        =>original with{OwnerLoss=(_,actor,killer,detach)=>GenericPetOwnerLoss.Run(actor,killer,detach,loss)};
    private sealed class Current(int actor,int enabled,GenericPetUninterruptibleServices services):MonsterUninterruptibleServices
    {
        private int Game,Attacker;
        internal override void Uninterruptible(bool value)=>services.State(actor,54,enabled);
        internal override bool HasGame()
        {
            if(actor==0){throw new NotSupportedException("Current uninterruptible completion requires an actor after the state write");}
            Game=services.Unit(actor).Game;return Game!=0;
        }
        internal override bool DeathDelayed()
        {
            if(services.Unit(actor).Kind is not(0 or 1 or 3)){return false;}
            byte selector=services.Unit(actor).Selector;
            if(selector is <1 or >3){throw new NotSupportedException("Current death-delay lookup requires selector1..3");}
            if(services.StateCount(selector)<=92){return false;}
            var unit=services.Unit(actor);return unit.HasRoot&&unit.Root1c<0&&(unit.Rootb38&0x10000000)!=0;
        }
        internal override void ClearDeathDelay()=>services.State(actor,92,0);
        internal override bool ResolveLastAttacker()
        {
            var unit=services.Unit(actor);if((unit.Flags128&0x20000)==0){return false;}
            Attacker=services.Resolve(Game,unit.LastKind,unit.LastId);return Attacker!=0;
        }
        internal override void ReturnFreshDeath()=>services.FreshReturn(Game,Attacker,actor,new(0,0,0,0,2));
        internal override void KillWithoutAttacker()=>services.OwnerLoss(Game,actor,0,1);
        internal override void DeleteQueuedThink()
        {
            if(services.Unit(actor).Kind!=1){return;}
            foreach(int handle in services.Events.Unit(actor))
            {if(services.Events.Node(handle).Type==2){services.Remove(handle);}}
        }
    }
}
