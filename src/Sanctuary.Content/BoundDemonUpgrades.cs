// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Captured effective-rank modifiers reuse existing native encounter affixes.
// Source ID30's aura selection/delivery is a separate unfinished component.
internal static class BoundDemonUpgrades
{
    private static Attempt Current;
    internal static Func<int,int> Roll=Utility.Random;
    internal static bool Has(Mobile actor,EliteModifier modifier)=>actor is BaseCreature b&&BoundDemonHosts.State(b)is{} s&&modifier switch
    {EliteModifier.ExtraStrong=>s.Birth.Modifier5==5,EliteModifier.ExtraFast=>s.Birth.Modifier10==6,
        EliteModifier.SpectralHit=>s.Birth.Modifier15==27,EliteModifier.AuraEnchanted=>s.Birth.Modifier20==30,_=>false};
    private static (int DamagePercent,int AttackPercent) Strength(Mobile actor)=>Has(actor,EliteModifier.ExtraStrong)
        ?EncounterProfiles.ExtraStrong(BoundDemonHosts.State((BaseCreature)actor).Difficulty,false):default;
    internal static int DamagePercent(Mobile actor)=>Strength(actor).DamagePercent;
    internal static int AttackPercent(Mobile actor)=>Strength(actor).AttackPercent;
    internal static int Velocity(Mobile actor)=>Has(actor,EliteModifier.ExtraFast)?EncounterProfiles.ModifierMovementPercent(EliteModifier.ExtraFast):0;
    public static void Initialize()
    {
        var attempt=ClassCombatHooks.MeleeAttempt;
        ClassCombatHooks.MeleeAttempt=(source,target,ranged)=>
        {
            var inner=attempt?.Invoke(source,target,ranged);
            return Has(source,EliteModifier.SpectralHit)?Current=new((BaseCreature)source,target,ranged,inner,Current):inner;
        };
        var hit=ClassGearHooks.WeaponHit;
        ClassGearHooks.WeaponHit=(source,target,loss,ranged)=>
        {
            var scope=Find(source,target,ranged);var payload=scope?.Take();
            hit?.Invoke(source,target,loss,ranged);
            if(payload!=null){NpcSpectralHit.After((BaseCreature)source,target,payload,TimedEffects.Now);}
        };
    }
    private static Attempt Find(Mobile source,Mobile target,bool ranged)
    {
        for(var s=Current;s!=null;s=s.Previous){if(s.Source==source&&s.Target==target&&s.Ranged==ranged){return s;}}return null;
    }
    internal static NpcSpectralPayload Capture(Mobile source,Mobile target,bool weapon,bool ranged,int damage)
    {
        if(!weapon||damage<=0||!Has(source,EliteModifier.SpectralHit)){return null;}
        var scope=Find(source,target,ranged)??throw new NotSupportedException("Bound Spectral Hit needs its native weapon-attempt scope");
        var body=(BaseCreature)source;
        var payload=NpcSpectralHit.CapturePrepared(body,body.DamageMin,body.DamageMax,Roll);scope.Pending.Push(payload);return payload;
    }
    internal static void RequireDelivery(NpcSpectralPayload payload,GearDamage parts)
    {
        // Native AOS returns before its post-hit hook on an entirely absorbed
        // packet. Do not silently invent a different poison/chill event order.
        if(payload?.Roll is{Channel:3 or 4}&&NamedElements.To256(parts.Total)<=0)
        {throw new NotSupportedException("Fully absorbed bound Spectral chill/poison needs a qualified native post-hit bridge");}
    }
    internal static void ApplyNative(BaseCreature body,BindDemonSuppliers birth)
    {
        if(birth.Modifier15!=27){return;}
        var basis=new CreatureResistances(body.BasePhysicalResistance,NamedElements.MagicResistance(body),body.BaseFireResistance,body.BaseEnergyResistance,body.BaseColdResistance,body.BasePoisonResistance);
        var values=EncounterProfiles.Resistances(basis,null,[EliteModifier.SpectralHit]);
        body.SetResistance(ResistanceType.Fire,values[2]);body.SetResistance(ResistanceType.Energy,values[3]);body.SetResistance(ResistanceType.Cold,values[4]);
    }
    private sealed class Attempt(BaseCreature source,Mobile target,bool ranged,IDisposable inner,Attempt previous):IDisposable
    {
        internal readonly BaseCreature Source=source;
        internal readonly Mobile Target=target;
        internal readonly bool Ranged=ranged;
        internal readonly Attempt Previous=previous;
        internal readonly Stack<NpcSpectralPayload> Pending=new();
        private bool Done;
        internal NpcSpectralPayload Take()=>Pending.TryPop(out var p)?p:null;
        public void Dispose()
        {if(Done){return;}Done=true;try{inner?.Dispose();}finally{Current=Previous;Pending.Clear();}}
    }
}
