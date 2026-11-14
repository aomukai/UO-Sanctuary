// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record EngorgeEffect(uint Owner,DateTime AppliedUtc,DateTime UntilUtc,EngorgeSuppliers Suppliers);

// Prepared complete recipient rule,not a recovered do161 binder. Only the
// internal corpse action can install it while learned admission stays gated.
internal static class EngorgeEffects
{
    internal static bool Approved=>false;
    internal static EngorgeEffect Saved(SkeletonGuardian body)=>body switch
    {WarlockGoatmanBody b=>b.Prepared?.Engorge,WarlockTaintedBody b=>b.Prepared?.Engorge,WarlockDefilerBody b=>b.Prepared?.Engorge,_=>null};
    internal static void Validate(EngorgeEffect effect,uint owner)
    {
        if(effect==null){return;}EngorgePrepared.Validate(effect.Suppliers);
        if(effect.Owner!=owner||effect.AppliedUtc.Kind!=DateTimeKind.Utc||effect.UntilUtc.Kind!=DateTimeKind.Utc
            ||effect.AppliedUtc>DateTime.MaxValue.AddMilliseconds(-40L*effect.Suppliers.DurationFrames)
            ||effect.UntilUtc!=effect.AppliedUtc.AddMilliseconds(40L*effect.Suppliers.DurationFrames))
        {throw new InvalidDataException("Invalid saved Engorge recipient state");}
    }
    internal static void Store(SkeletonGuardian body,EngorgeEffect effect)
    {
        if(body is WarlockGoatmanBody g){g.RestorePrepared(g.Prepared with{Engorge=effect});}
        else if(body is WarlockTaintedBody t){t.RestorePrepared(t.Prepared with{Engorge=effect});}
        else if(body is WarlockDefilerBody d){d.RestorePrepared(d.Prepared with{Engorge=effect});}
        else{throw new NotSupportedException("No qualified saved Engorge demon");}
    }
    internal static EngorgeEffect At(Mobile actor,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Engorge needs a UTC clock");}
        return actor is SkeletonGuardian{Alive:true,Deleted:false} body&&Saved(body)is{} s
            &&at>=s.AppliedUtc&&at<s.UntilUtc?s:null;
    }
    internal static int AttackSpeed(Mobile actor)=>At(actor,TimedEffects.Now)?.Suppliers.AttackSpeed??0;
    internal static int Armor(Mobile actor)=>At(actor,TimedEffects.Now)?.Suppliers.FlatArmor??0;
    internal static int PhysicalResistance(Mobile actor)=>At(actor,TimedEffects.Now)?.Suppliers.PhysicalResistance??0;
    internal static void ApplyPrepared(SkeletonGuardian body,PlayerMobile owner,EngorgeSuppliers captured,DateTime at)
    {
        EngorgePrepared.Validate(captured);
        if(!EngorgeCasting.Recipient(owner,body)){throw new NotSupportedException("Engorge recipient lost its original active demon host");}
        var state=new EngorgeEffect(owner.Serial.Value,at,at.AddMilliseconds(40L*captured.DurationFrames),captured);
        Validate(state,owner.Serial.Value);Store(body,state);
        SkillHealth.Heal(body,EngorgePrepared.Healing256(captured,SkillHealth.Maximum256(body),SkillHealth.Balance256(body)));
        body.FixedEffect(0x376A,10,10,0x26,0); // Identified shared heal/buff placeholder.
    }
    // Count only active source-frame instants in a bulk recovery call. No
    // offline interval is paid back and expiry cannot donate extra frames.
    internal static int Recovery256(SkeletonGuardian body,DateTime first,long frames)
    {
        if(first.Kind!=DateTimeKind.Utc||frames<0){throw new ArgumentException("Invalid Engorge recovery interval");}
        var s=Saved(body);if(s==null||frames==0){return 0;}
        long step=SourceFrameTimeline.IntervalTicks;
        long begin=Math.Max(0,(s.AppliedUtc.Ticks-first.Ticks+step-1)/step);
        long end=Math.Min(frames,(s.UntilUtc.Ticks-first.Ticks+step-1)/step);
        return (int)Math.Clamp(Math.Max(0,end-begin)*(long)s.Suppliers.RegenerationStat,0,int.MaxValue);
    }
    internal static int Leech(Mobile attacker,BaseCreature target,PreparedMonsterDamagePacket packet,DateTime at,SkillDamageKind kind=SkillDamageKind.Direct)
    {
        if(attacker is not WarlockGoatmanBody body||At(body,at)is not{} state||!EngorgeCasting.Recipient(body.ControlMaster as PlayerMobile,body)
            ||kind!=SkillDamageKind.Direct||packet.Physical256<=0||(packet.ResultFlags&1)==0||(packet.ResultFlags&32)!=0){return 0;}
        if(NpcFlatDrains.Any(packet)){throw new NotSupportedException("Engorge percentage leech cannot silently replace a positive raw NPC drain packet");}
        int divisor=MonsterVariants.Difficulty(target)switch{MonsterDifficulty.Nightmare=>2,MonsterDifficulty.Hell=>3,_=>1};
        // Explicit proposed adaptation:interpret the tooltip as percentage
        // leech and reuse the player percentage arithmetic,not its item or
        // resource receiver. Only this actual demon receives the life.
        int gain=WeaponLeech.Calculate(packet.Physical256,SkillHealth.Balance256(target),0,state.Suppliers.MinimumDrainStat,0,
            GemCombat.Drain(target),divisor,divisor).Life256;
        int before=SkillHealth.Balance256(body);SkillHealth.Heal(body,gain);
        return SkillHealth.Balance256(body)-before;
    }
}
