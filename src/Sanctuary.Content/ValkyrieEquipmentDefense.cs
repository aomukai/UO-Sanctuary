// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class ValkyrieEquipmentDefense
{
    internal const string Profile="valkyrie-inherited-defense-v1";
    internal static bool Active(Mobile actor)=>actor is AmazonValkyrieBody{Alive:true,Deleted:false} b&&b.Prepared?.EquipmentDefenseProfile==Profile;
    internal static void Require(AmazonValkyrieBody body,bool world=true)
    {
        if(body?.Prepared?.EquipmentDefenseProfile!=Profile){throw new NotSupportedException("Valkyrie inherited defense is not activated");}
        _=ValkyrieEquipmentWeapons.Frame(body);_=ValkyrieEquipmentDamage.Passives(body);_=NpcEquipmentRandom.Require(body);
        // The prepared immobile body cannot enter source walk/run. The future
        // moving controller must supply its actual source mode,not a guessed
        // lease inferred from native location changes or owner movement.
        if(world&&!body.CantWalk){throw new NotSupportedException("Moving Valkyrie defense needs the actual source controller mode");}
        if(!SourceBlocking.TryRate(body,out int shield,out _)||shield!=0)
        {throw new NotSupportedException("Valkyrie original no-shield component context changed");}
        if(body.EquipmentNumbers.Stats.Any(v=>v.Stat==348&&v.Value!=0))
        {throw new NotSupportedException("Valkyrie weapon-block skill/composite materialization is unfinished");}
    }
    internal static void Activate(AmazonValkyrieBody body)
    {
        if(body?.Prepared is not{EquipmentDefenseProfile:null,EquipmentWeapons:not null} state||!body.CantWalk)
        {throw new NotSupportedException("Valkyrie defense requires its original immobile weapon/RNG context");}
        _=ValkyrieEquipmentWeapons.Frame(body);_=ValkyrieEquipmentDamage.Passives(body);_=NpcEquipmentRandom.Require(body);
        if(!SourceBlocking.TryRate(body,out int shield,out _)||shield!=0||body.EquipmentNumbers.Stats.Any(v=>v.Stat==348&&v.Value!=0))
        {throw new NotSupportedException("Valkyrie shield/weapon-block context requires another source service");}
        body.RestorePrepared(state with{EquipmentDefenseProfile=Profile});
    }
    private sealed class Services(AmazonValkyrieBody body,int mode,ValkyrieInheritedPassive[] passive):SourceAvoidanceServices
    {
        internal override bool Present=>true;
        internal override int Kind=>1;
        internal override int Mode=>mode;
        internal override bool Moving=>mode is 2 or 15;
        internal override int BlockRate=>0;
        internal override int Stat(int stat)=>checked(body.EquipmentNumbers.Read(stat)+passive.Where(p=>p.Stat==stat).Sum(p=>p.Value));
        internal override IEnumerable<EquipmentStatValue> WeaponBlockStats=>body.EquipmentNumbers.Stats.Where(v=>v.Stat==348);
        internal override bool HandType(int type)=>ValkyrieEquipment.Originals(body).Where(i=>i.Snapshot.Equipment.Body is 4 or 5)
            .Any(i=>CurrentItemInitialization.TypeCode(type)is{} code&&NpcItemAffixes.Pool.IsType(i.Snapshot.Metadata.Code,code));
        internal override int WeaponClass=>throw new NotSupportedException("Unadmitted Valkyrie weapon-block composite");
        internal override uint Raw()=>NpcEquipmentRandom.Raw(body);
    }
    internal static int SuppliedMode(AmazonValkyrieBody body,int mode,bool avoid,bool block)
    {
        if(mode is<0 or>15){throw new ArgumentOutOfRangeException(nameof(mode));}
        Require(body,false);return SourceAvoidance.Resolve(new Services(body,mode,ValkyrieEquipmentDamage.Passives(body)),avoid,block);
    }
    internal static int World(AmazonValkyrieBody body,bool avoid,bool block)
    {
        Require(body);int flags=SuppliedMode(body,1,avoid,block);
        if(flags!=0){body.FixedEffect(0x375A,5,5,0x481,0);body.PlaySound(0x1A4);} // Identified defense presentation placeholder.
        return flags;
    }
}

// Existing melee result callers retain shield-only behavior for other NPCs.
// Valkyrie returns the full source action flags,without folding Dodge into Block.
internal static class NpcBlockOrDodge
{
    internal static bool Try(BaseCreature target,bool avoid,bool block,out int flags,out bool animation)
    {
        flags=0;animation=false;
        if(ValkyrieEquipmentDefense.Active(target))
        {flags=ValkyrieEquipmentDefense.World((AmazonValkyrieBody)target,avoid,block);return true;}
        if(avoid||!block||!SourceBlocking.TryRoll(target,true,out bool shield,out animation)){return false;}
        flags=shield?SourceAvoidance.Block:0;return true;
    }
}
