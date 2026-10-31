// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// UO bookkeeping adapter for a source HP write. This registers the same whole
// display-HP loss as SkillHealth.Damage, including its lethal cap, without
// replaying Damage or triggering incoming native death before source return.
internal static class PreparedMonsterCredit
{
    private sealed record CommittedOwner(uint Attacker,PlayerMobile Owner);
    private static readonly ConditionalWeakTable<BaseCreature,CommittedOwner> Owners=new();
    internal static PlayerMobile Owner(BaseCreature target,Mobile attacker)
        =>attacker!=null&&Owners.TryGetValue(target,out var state)&&state.Attacker==attacker.Serial.Value&&state.Owner is{Deleted:false}
            ?state.Owner:null;
    internal static int Amount(BaseCreature target,int after256)
    {
        if(target is not{Alive:true,Deleted:false}||after256<0)
        {throw new ArgumentException("Prepared damage credit requires a living recipient and nonnegative remaining life");}
        if(after256>SkillHealth.Balance256(target)){throw new ArgumentOutOfRangeException(nameof(after256));}
        return Math.Max(0,target.Hits-(int)(((long)after256+255)/256));
    }
    internal static void Register(BaseCreature attacker,BaseCreature target,int after256)
    {
        if(attacker==null||target is not{Alive:true,Deleted:false}||after256<0)
        {throw new ArgumentException("Prepared damage credit requires an existing source and living recipient");}
        int before256=SkillHealth.Balance256(target);
        int whole=Amount(target,after256);
        if(whole>0)
        {
            var owner=attacker.GetMaster() as PlayerMobile;
            target.RegisterDamage(whole,attacker);
            Owners.Remove(target);
            // Keep the exact current packet's responsible owner through its
            // later retaliation/return, even if that removes the attacking pet.
            // This is transient like the native ledger, never inferred from a
            // different damager's history or replayed across world reloads.
            if(owner is{Deleted:false}&&target.FindDamageEntryFor(attacker)?.Responsible?.Any(e=>e.Damager==owner&&e.DamageGiven>0)==true)
            {Owners.Add(target,new(attacker.Serial.Value,owner));}
        }
        if(target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)!=before256)
        {throw new NotSupportedException("Reentrant damage-credit mutation/death before prepared HP commitment is unsupported");}
    }
}
