// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record EchoingStrikePacket(EchoingStrikeInput Input,WeaponMissileDamage BaseDamage,bool PreventHeal);
// Proposed complete packet policy;not recovered do166/damage16. Keep damage
// rates distinct from poison lengths/count/drain and from physical-only ED.
internal static class EchoingStrikePackets
{
    internal static int Scale(int n,int percent,int divisor=100)
    {
        if(n<0||percent<0||divisor<=0){throw new NotSupportedException("Unsupported Echoing damage scalar");}
        Int128 result=(Int128)n*percent/divisor;
        if(result>int.MaxValue){throw new NotSupportedException("Echoing packet exceeds signed FP256 domain");}
        return(int)result;
    }
    internal static void ValidateInput(EchoingStrikeInput c)
    {
        if(c==null||!((Serial)c.Owner).IsMobile||!((Serial)c.Weapon).IsItem||c.Skill is not{} s||s.Rank is <1 or >255
            ||s.HardMirroredBlades is <0 or >20||s.HardBladeWarp is <0 or >20||c.WeaponInputs is not{} w
            ||w.Minimum256<0||w.Maximum256<w.Minimum256||w.NormalDamage256<0||w.Elements is not{} e
            ||c.Critical is not{} critical||critical.Passive is <0 or >100||critical.Deadly is <0 or >100||critical.Mastery is <0 or >100
            ||c.Slow.VelocityPercent is <0 or >100||c.Slow.DamagePercent is <0 or >100)
        {throw new InvalidDataException("Invalid Echoing captured inputs");}
        var expected=EchoingStrikePrepared.Capture(new(SkillMechanics.For(CombatAbility.EchoingStrike),k=>k=="Echoing Strike"?s.Rank:0,
            k=>k=="Mirrored Blades"?s.HardMirroredBlades:k=="Blade Warp"?s.HardBladeWarp:0,99));
        if(s!=expected||e.FireMin<0||e.FireMax<e.FireMin||e.ColdMin<0||e.ColdMax<e.ColdMin||e.LightningMin<0||e.LightningMax<e.LightningMin
            ||e.MagicMin<0||e.MagicMax<e.MagicMin||e.PoisonMin<0||e.PoisonMax<e.PoisonMin||e.PoisonFrames<0||e.PoisonCount<0
            ||e.ColdFrames<0||e.LifeDrain<0||e.ManaDrain<0||e.BurnMin!=0||e.BurnMax!=0||e.BurnFrames!=0
            ||w.PhysicalSupplement is{} x&&(x.Min256<0||x.Max256<x.Min256))
        {throw new InvalidDataException("Invalid Echoing supplier/element metadata");}
    }
    internal static EchoingStrikePacket Prepare(EchoingStrikeInput c,bool critical,bool preventHeal)
    {
        ValidateInput(c);var s=c.Skill;var w=c.WeaponInputs;var e=w.Elements;
        int Source(int n)=>Scale(n,s.WeaponSource128,128);
        var elements=e with
        {
            FireMin=Source(e.FireMin),FireMax=Source(e.FireMax),ColdMin=Source(e.ColdMin),ColdMax=Source(e.ColdMax),
            LightningMin=Source(e.LightningMin),LightningMax=Source(e.LightningMax),MagicMin=Source(e.MagicMin),MagicMax=Source(e.MagicMax),
            PoisonMin=Source(e.PoisonMin),PoisonMax=Source(e.PoisonMax)
        }; // Length/count/drain survive unchanged.
        var damage=new WeaponMissileDamage(checked(Source(Math.Max(0,w.Minimum256-w.NormalDamage256))+s.MinimumPhysical256),
            checked(Source(Math.Max(0,w.Maximum256-w.NormalDamage256))+s.MaximumPhysical256),w.OwnerPercent,w.DemonPercent,w.UndeadPercent,critical,elements)
            {SourcePoisonFrames=true,PhysicalSupplement=w.PhysicalSupplement is{} extra?new(Source(extra.Min256),Source(extra.Max256)):null};
        Int128 maximum=(Int128)damage.Max256*Math.Max(10,100L+w.OwnerPercent+Math.Max(0,w.DemonPercent)+Math.Max(0,w.UndeadPercent))/100;
        maximum+=damage.PhysicalSupplement?.Max256??0;if(critical){maximum*=2;}
        // Shared WeaponMissileDamage rolls use signed FP256 intermediates.
        if(maximum>int.MaxValue||maximum*(100L+s.PhysicalPercent)/100>int.MaxValue)
        {throw new NotSupportedException("Echoing enhanced physical intermediate exceeds signed FP256 domain");}
        return new(c,damage,preventHeal);
    }
    internal static void Validate(EchoingStrikePacket packet)
    {if(packet==null||packet!=Prepare(packet.Input,packet.BaseDamage?.Critical??false,packet.PreventHeal)){throw new InvalidDataException("Invalid Echoing packet");}}
    internal static GearDamage Roll(EchoingStrikePacket packet,Mobile target,ref ulong seed,out int poisonRate)
    {
        var c=packet.Input;ulong local=seed;int Random(int n)=>(int)SourceRandom.Limited(ref local,n);
        var damage=packet.BaseDamage.Roll(target,Random,out int rate);seed=local;
        int Shared(int n)=>c.Slow.Damage256(Scale(n,c.Skill.SuppliedAttackPercent));
        int physical=Shared(Scale(NamedElements.To256(damage.Physical),checked(100+c.Skill.PhysicalPercent)));
        poisonRate=Shared(rate);
        return damage with{Physical=physical/256.0,Fire=Shared(NamedElements.To256(damage.Fire))/256.0,
            Cold=Shared(NamedElements.To256(damage.Cold))/256.0,Lightning=Shared(NamedElements.To256(damage.Lightning))/256.0,
            Magic=Shared(NamedElements.To256(damage.Magic))/256.0,Poison=0};
    }
}
