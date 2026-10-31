// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// UO bookkeeping adapter for a source HP write. This registers the same whole
// display-HP loss as SkillHealth.Damage, including its lethal cap, without
// replaying Damage or triggering incoming native death before source return.
internal static class PreparedMonsterCredit
{
    private sealed class OwnerScope(Mobile attacker,BaseCreature target,PlayerMobile owner,OwnerScope previous):IDisposable
    {
        internal readonly Mobile Attacker=attacker;
        internal readonly BaseCreature Target=target;
        internal readonly PlayerMobile Owner=owner;
        public void Dispose(){_current=previous;}
    }
    private static OwnerScope _current;
    // Damage events such as Iron Maiden can retire the attacking pet before
    // the original packet commits HP. Keep only this synchronous packet's
    // responsible owner; never recover it from unrelated old damage history.
    internal static IDisposable CaptureOwner(Mobile attacker,BaseCreature target)
    {
        if((attacker as BaseCreature)?.GetDamageMaster(target) is not PlayerMobile{Deleted:false} owner){return null;}
        return _current=new(attacker,target,owner,_current);
    }
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
    internal static void Register(Mobile attacker,BaseCreature target,int after256)
    {
        if(attacker==null||target is not{Alive:true,Deleted:false}||after256<0)
        {throw new ArgumentException("Prepared damage credit requires an existing source and living recipient");}
        int before256=SkillHealth.Balance256(target);
        int whole=Amount(target,after256);
        if(whole>0)
        {
            bool captured=_current?.Attacker==attacker&&_current.Target==target;
            var master=(attacker as BaseCreature)?.GetDamageMaster(target) as PlayerMobile;
            var owner=captured?_current.Owner:master;
            if(captured&&master!=null&&master!=owner){throw new NotSupportedException("Prepared packet owner changed during damage events");}
            var entry=target.RegisterDamage(whole,attacker);
            if(captured&&master==null&&owner is{Deleted:false})
            {
                // Mirror the native responsible sub-entry when retirement
                // cleared the pet's master before this packet could register.
                var list=entry.Responsible??=new List<DamageEntry>();
                var responsible=list.FirstOrDefault(e=>e.Damager==owner);
                if(responsible==null){responsible=new(owner);list.Add(responsible);}
                responsible.DamageGiven+=whole;responsible.LastDamage=Core.Now;
            }
            Owners.Remove(target);
            // Keep the exact current packet's responsible owner through its
            // later retaliation/return, even if that removes the attacking pet.
            // This is transient like the native ledger, never inferred from a
            // different damager's history or replayed across world reloads.
            if(owner is{Deleted:false}&&entry.Responsible?.Any(e=>e.Damager==owner&&e.DamageGiven>0)==true)
            {Owners.Add(target,new(attacker.Serial.Value,owner));}
        }
        if(target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)!=before256)
        {throw new NotSupportedException("Reentrant damage-credit mutation/death before prepared HP commitment is unsupported");}
    }
}
