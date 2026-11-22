// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Server;
using Server.Collections;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// Approved army parking uses actual safety regions, never roofs or buildings.
internal static class MinionSafeAreas
{
    private static readonly ConditionalWeakTable<PlayerMobile, HashSet<Serial>> _waiting = new();

    internal static bool Contains(PlayerMobile owner)=>owner is{Deleted:false,Map:not null}&&owner.Map!=Map.Internal
        &&SpellHelper.IsTown(owner.Location,owner);
    internal static bool Candidate(PlayerMobile owner,BaseCreature pet)=>pet is{Deleted:false,Alive:true}
        &&pet.ControlMaster==owner&&PersistentMinions.Behavior(pet)!=null&&!MercenaryCare.IsCompanion(owner,pet);
    internal static bool ShouldPark(PlayerMobile owner,BaseCreature pet)=>Candidate(owner,pet)&&(owner.Mounted||Contains(owner));
    internal static bool Parked(BaseCreature pet)=>pet is{Deleted:false,Map:not null}&&pet.Map==Map.Internal
        &&pet.ControlMaster is PlayerMobile owner&&Candidate(owner,pet)
        &&PersistentMinions.Profiles?.Find(pet) is{Parked:true} state&&state.Owner==owner.Serial.Value;
    internal static bool NoRecovery(Mobile actor)=>actor is BaseCreature pet&&pet.Map==Map.Internal
        &&pet.ControlMaster is PlayerMobile owner&&(Parked(pet)||ShouldPark(owner,pet));
    internal static int RecoveryRate(Mobile actor,int rate)=>NoRecovery(actor)?Math.Min(0,rate):rate;
    internal static bool Enforce(BaseCreature pet)
    {
        if(pet?.ControlMaster is not PlayerMobile owner||!ShouldPark(owner,pet)){return false;}
        PersistentMinions.Park(pet);return true;
    }

    internal static void Stored(PlayerMobile owner, BaseCreature pet, bool newlyParked)
    {
        if (owner.NetState is not { Running: true } || !ShouldPark(owner, pet) || !PersistentMinions.Active(owner, pet))
            return;

        var waiting = _waiting.GetOrCreateValue(owner);
        if (!waiting.Add(pet.Serial))
            return;
        waiting.RemoveWhere(serial => World.FindMobile(serial) is not BaseCreature { Alive: true, Deleted: false });
        if (newlyParked && waiting.Count == 1)
            owner.SendMessage(owner.Mounted ? "Your minions will return when you are on foot outside town."
                : "Your minions await outside town. They will return when you leave the guarded area.");
    }

    internal static void Returned(PlayerMobile owner, BaseCreature pet)
    {
        if (owner.NetState is { Running: true } && _waiting.TryGetValue(owner, out var waiting) && waiting.Contains(pet.Serial))
        {
            _waiting.Remove(owner);
            owner.SendMessage("Your minions have returned.");
        }
    }

    public static void Initialize()=>ClassMapHooks.LocationChanged+=LocationChanged;
    private static void LocationChanged(Mobile mobile)
    {
        if(World.Loading||mobile is not PlayerMobile{Alive:true,Deleted:false} owner){return;}
        if(owner.Mounted||Contains(owner))
        {
            using var pets=PooledRefList<BaseCreature>.Create();
            foreach(var follower in owner.AllFollowers??[]){if(follower is BaseCreature pet){pets.Add(pet);}}
            foreach(var pet in pets){Enforce(pet);}
        }
        else if(owner.NetState!=null){PersistentMinions.Resume(owner);}
    }
}
