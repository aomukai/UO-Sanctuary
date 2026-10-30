// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Birth-time hard Rabies is an explicitly provisional capture boundary. Current
// tables and released2.4notes establish the synergy, not this modern timing.
// NPC mastery/pierce are zero in the mapped no-equipment/no-sunder scope.
internal sealed record CreeperPoisonContext(int OwnerLevel,int RabiesHardRank)
{public string Profile{get;init;}="birth-owner-rabies-no-player-gear-provisional-v1";}
internal sealed record CreeperMatPoison(CreeperPoison Damage,int SlowDamagePercent,ulong Seed);

internal static class DruidCreeperPoison
{
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    internal static CreeperPoisonContext Capture(CreeperSnapshot snapshot)
    {
        if(snapshot?.Birth==null || snapshot.Birth.Pierce!=0){throw new NotSupportedException("Creeper poison sunder is not connected");}
        var context=new CreeperPoisonContext(snapshot.OwnerLevel,snapshot.RabiesHardRank);Validate(context);return context;
    }
    internal static void Validate(CreeperPoisonContext context)
    {
        if(context==null || context.OwnerLevel is <1 or >99 || context.RabiesHardRank is <0 or >20
            || context.Profile!="birth-owner-rabies-no-player-gear-provisional-v1")
        {throw new InvalidDataException("Invalid Creeper poison context");}
    }
    internal static CreeperPoison Damage(int rank,CreeperPoisonContext context,int slowDamagePercent)
    {
        Validate(context);
        if(rank is <1 or >255 || slowDamagePercent is <0 or >100){throw new ArgumentOutOfRangeException();}
        var values=new SkillValues(SkillMechanics.For(CombatAbility.PoisonCreeper),
            name=>name=="Plague Poppy"?rank:0,name=>name=="Rabies"?context.RabiesHardRank:0,context.OwnerLevel);
        var raw=DruidCreeperStats.Poison(values,0);var slow=new SlowMissilePayload(100,slowDamagePercent);
        return raw with{Minimum256=slow.Damage256(raw.Minimum256),Maximum256=slow.Damage256(raw.Maximum256)};
    }
    internal static CreeperMat Initialize(CreeperMat mat,CreeperPoisonContext context,int slowDamagePercent)
        =>mat with{Poison=new(Damage(mat.Rank,context,slowDamagePercent),slowDamagePercent,SourceRandom.InitLow(SeedLow()))};
    internal static void Validate(CreeperMatPoison packet)
    {
        if(packet==null || packet.SlowDamagePercent is <0 or >100 || packet.Damage.Minimum256<0 || packet.Damage.Maximum256<packet.Damage.Minimum256
            || packet.Damage.Frames!=100 || packet.Damage.Count!=0){throw new InvalidDataException("Invalid Creeper mat poison packet");}
    }
    internal static CreeperMat Deliver(PlayerMobile owner,BaseCreature target,CreeperMat mat,DateTime at)
    {
        if(mat.Poison is not{} packet){return mat;}Validate(packet);
        if(owner is not{Alive:true,Deleted:false} || target is not{Alive:true,Deleted:false,Blessed:false} || ClassCombat.Friendly(owner,target))
        {return mat;}
        ulong seed=packet.Seed;var damage=packet.Damage;
        int rate=WeaponMissileDamage.RollValue(damage.Minimum256,damage.Maximum256,0,bound=>(int)SourceRandom.Limited(ref seed,bound));
        // Read only recipient resistance/curse/aura pools. Original-player gear
        // must not leak into the NPC poison packet or its mitigation.
        int resistance=NamedElements.Resistance(null,target,"pois",target.PoisonResistance,pierceOverride:0);
        var poison=PoisonPackets.Mitigate(rate,damage.Frames,false,resistance,ItemDurationDefenses.PoisonLength(target),0,0,0);
        if(poison.Rate256>0&&poison.Frames>0)
        {
            owner.DoHarmful(target,true);
            // Original player is the explicit durable source-credit adapter;
            // removing the pet cannot erase subsequent poison-tick attribution.
            poison.Commit(owner,target,at);
        }
        return mat with{Poison=packet with{Seed=seed}};
    }
}
