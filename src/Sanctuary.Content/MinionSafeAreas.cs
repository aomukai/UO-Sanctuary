// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Collections;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// Approved army parking uses actual safety regions, never roofs or buildings.
internal static class MinionSafeAreas
{
    internal static bool Contains(PlayerMobile owner)=>owner is{Deleted:false,Map:not null}&&owner.Map!=Map.Internal
        &&SpellHelper.IsTown(owner.Location,owner);
    internal static bool Candidate(PlayerMobile owner,BaseCreature pet)=>pet is{Deleted:false,Alive:true}
        &&pet.ControlMaster==owner&&PersistentMinions.Behavior(pet)!=null&&!MercenaryCare.IsCompanion(owner,pet);
    internal static bool ShouldPark(PlayerMobile owner,BaseCreature pet)=>Candidate(owner,pet)&&Contains(owner);
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
    public static void Initialize()=>ClassMapHooks.LocationChanged+=LocationChanged;
    private static void LocationChanged(Mobile mobile)
    {
        if(World.Loading||mobile is not PlayerMobile{Alive:true,Deleted:false} owner){return;}
        if(Contains(owner))
        {
            using var pets=PooledRefList<BaseCreature>.Create();
            foreach(var follower in owner.AllFollowers??[]){if(follower is BaseCreature pet){pets.Add(pet);}}
            foreach(var pet in pets){Enforce(pet);}
        }
        else if(owner.NetState!=null){PersistentMinions.Resume(owner);}
    }
}
