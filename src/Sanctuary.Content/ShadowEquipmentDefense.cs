// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Original Shadow recipient, using the shared source avoidance and ordered
// item/damage services. No intrinsic attack, movement or learned cast admission.
internal static class ShadowEquipmentDefense
{
    internal const string Profile="shadow-original-defense-v1";
    internal static bool Active(Mobile actor)=>!Server.World.Loading&&actor is AssassinShadowBody{Alive:true,Deleted:false} body
        &&body.Prepared?.DefenseProfile==Profile;
    internal static bool NumericWorld(AssassinShadowBody body)=>Active(body)&&(ShadowGroundMotion.Active(body)||body.CantWalk&&!((IPersistentMinion)body).CanActivate);
    internal static GeneratedNpcStatGraph Require(AssassinShadowBody body,bool world=true)
    {
        if(!Active(body)||!body.CantWalk&&!ShadowGroundMotion.Active(body)||world&&body.Map==Map.Internal)
        {throw new NotSupportedException("Shadow defense needs its qualified stationary original recipient");}
        var graph=OriginalNpcEquipment.Graph(body);_=NpcEquipmentRandom.Require(body);
        if(!SourceBlocking.TryRate(body,out int rate,out _)||rate!=0)
        {throw new NotSupportedException("Shadow original no-shield component context changed");}
        return graph;
    }
    internal static void Activate(AssassinShadowBody body)
    {
        if(body?.Prepared is not{DefenseProfile:null} state||body.Map!=Map.Internal||!body.CantWalk)
        {throw new NotSupportedException("Shadow defense must be qualified on its original parked body");}
        // All callbacks must have real consumers; unsupported original items
        // refuse intact rather than being removed or cloned into a carrier.
        _=NpcOriginalItemProcs.RequireRegistrationHost(body);
        _=NpcCombatEvents.FromOriginalStats(body.EquipmentNumbers.Stats);
        if(!SourceBlocking.TryRate(body,out int rate,out _)||rate!=0)
        {throw new NotSupportedException("Shadow has no qualified original shield recipient");}
        body.RestorePrepared(state with{DefenseProfile=Profile});
    }
    internal static int? Bonus(BaseCreature actor,string code)=>Active(actor)
        ?ValkyrieEquipmentContact.Number(Require((AssassinShadowBody)actor,false),code):null;
    internal static NpcCombatEvents Registry(AssassinShadowBody body)
    {_=Require(body,false);return NpcCombatEvents.FromOriginalStats(NpcOriginalItemProcs.RequireRegistrationHost(body).Stats);}
    internal static int Composite(AssassinShadowBody body)
    {
        var graph=OriginalNpcEquipment.Graph(body);var active=graph.ActiveItems.ToHashSet();
        var hands=ShadowLoadout.Originals(body).Where(i=>i.Snapshot.Equipment.Body is 4 or 5&&active.Contains(i.Serial.Value)).ToArray();
        // Approved Sanctuary adaptation: original active dual-claw equipment
        // supplies HT2; the older monster BaseW=hth path cannot establish it.
        return hands.Length==2&&hands.All(i=>NpcItemAffixes.Pool.IsType(i.Snapshot.Metadata.Code,"h2h"))?13:0;
    }
    private sealed class Services(AssassinShadowBody body,GeneratedNpcStatGraph graph,int mode):SourceAvoidanceServices
    {
        internal override bool Present=>true;
        internal override int Kind=>1;
        internal override int Mode=>mode;
        internal override bool Moving=>mode is 2 or 15;
        internal override int BlockRate=>0;
        internal override int Stat(int stat)=>graph.Read(stat);
        internal override IEnumerable<EquipmentStatValue> WeaponBlockStats=>graph.Stats.Where(s=>s.Stat==348);
        internal override bool HandType(int type)=>ShadowLoadout.Originals(body).Where(i=>i.Snapshot.Equipment.Body is 4 or 5&&graph.ActiveItems.Contains(i.Serial.Value))
            .Any(i=>NpcSkillItemTypes.Code(type)is{} code&&NpcItemAffixes.Pool.IsType(i.Snapshot.Metadata.Code,code));
        internal override int WeaponClass=>Composite(body);
        internal override uint Raw()=>NpcEquipmentRandom.Raw(body);
    }
    internal static int SuppliedMode(AssassinShadowBody body,int mode,bool avoid,bool block)
    {
        if(mode is<0 or>15){throw new ArgumentOutOfRangeException(nameof(mode));}
        return SourceAvoidance.Resolve(new Services(body,Require(body,false),mode),avoid,block);
    }
    internal static int World(AssassinShadowBody body,bool avoid,bool block)
    {
        _=Require(body);int flags=SuppliedMode(body,ShadowGroundMotion.Active(body)?ShadowGroundMotion.Mode(body):1,avoid,block);
        if(flags!=0){body.FixedEffect(0x375A,5,5,0x481,0);body.PlaySound(0x1A4);} // Shared identified defense placeholders.
        return flags;
    }
}
