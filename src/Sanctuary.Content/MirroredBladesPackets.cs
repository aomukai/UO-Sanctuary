// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

internal enum MirroredWeaponKind{Melee,Arrow,Bolt,Throw}
internal sealed record MirroredWeaponInput(uint Owner,uint Weapon,MirroredWeaponKind Kind,MirroredBladeValues Skill,
    WeaponMissileDamage Damage,int AttackBase,int ItemAttackPercent,int MasteryAttackPercent,EchoingCriticalInputs Critical);
internal sealed record MirroredWeaponPacket(MirroredWeaponInput Input,MirroredBladeAttack Attack,WeaponMissileDamage Damage);
internal readonly record struct MirroredWeaponRoll(GearDamage Damage,int PoisonRate256,int PoisonFrames,int ColdFrames);

// Prepared complete damage packets,not a start68/do166 substitute. Collection
// and rolls have no mana,ammo,durability,hit,proc or world-allocation effects.
// The proposed transform scales final rolled channels but preserves lengths,
// source counts and drain percentages. Admission awaits the whole action rule.
internal static class MirroredBladesPackets
{
    internal static bool Approved=>MirroredBladesAction.Approved;
    internal static MirroredWeaponKind Kind(PlayerMobile owner,BaseWeapon weapon)
    {
        if(weapon==null||!ClassGear.Active(owner,weapon)||ClassGear.Base(weapon)is not{} basis
            ||!ClassItemTypes.Is(basis.Type,"weap")||ClassItemTypes.Is(basis.Type,"tpot"))
        {throw new NotSupportedException("Mirrored Blades needs an active non-potion source weapon");}
        if(ClassItemTypes.Is(basis.Type,"bow")){return MirroredWeaponKind.Arrow;}
        if(ClassItemTypes.Is(basis.Type,"xbow")){return MirroredWeaponKind.Bolt;}
        if(ClassGear.Definition(weapon)?.Category=="Throwing")
        {
            if(NamedAmmunition.Maximum(weapon)<=0){throw new NotSupportedException("Unmapped Mirrored throwing quantity");}
            return MirroredWeaponKind.Throw;
        }
        if(!PlayerMeleeGeometry.WarlockMelee(owner)){throw new NotSupportedException("Unmapped Mirrored weapon kind");}
        return MirroredWeaponKind.Melee;
    }
    internal static MirroredWeaponInput Capture(PlayerMobile owner)
    {
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Warlock)
        {throw new NotSupportedException("Mirrored input needs a living Warlock/Witch");}
        var weapon=CombatTiming.Equipped(owner);var kind=Kind(owner,weapon);
        var skill=MirroredBladesPrepared.Capture(SkillMechanics.Values(owner,CombatAbility.MirroredBlades));
        _=MirroredBladesPrepared.Attacks(skill);
        var raw=ClassStats.WeaponBaseDamage(owner);int undead=ClassGear.Bonus(owner,"dmg-undead%");
        if(ClassItemTypes.Is(ClassGear.Base(weapon).Type,"blun")){undead=checked(undead+50);}
        // Source128 keeps Damage+ and ordinary min/max affixes. Unlike Echoing,
        // no ledger is subtracted and no116/128 reduction is introduced.
        var damage=new WeaponMissileDamage(checked(raw.Min*256),checked(raw.Max*256),ClassStats.PhysicalDamageBonus(owner,weapon),
            ClassGear.Bonus(owner,"dmg-demon%"),undead,false,BowLaunch.WeaponElements(owner,weapon))
            {SourcePoisonFrames=true,PhysicalSupplement=BowLaunch.PhysicalSupplement(owner,weapon,128),SkillAttackPercent=skill.AttackPercent};
        var input=new MirroredWeaponInput(owner.Serial.Value,weapon.Serial.Value,kind,skill,damage,
            ClassStats.PlayerAttackBase(owner),ClassStats.PlayerItemAttackPercent(owner),WeaponMasteries.Bonus(owner,weapon,1),
            new(Math.Clamp(AmazonCombat.CriticalChance(owner),0,100),Math.Clamp(ClassGear.Bonus(owner,"deadly"),0,100),Math.Clamp(WeaponMasteries.Bonus(owner,weapon,3),0,100)));
        Validate(input);return input;
    }
    internal static void Validate(MirroredWeaponInput input)
    {
        if(input==null||!((Serial)input.Owner).IsMobile||!((Serial)input.Weapon).IsItem||!Enum.IsDefined(input.Kind)
            ||input.Damage is not{} d||d.Critical||d.Min256<0||d.Max256<d.Min256||d.Elements is not{} e
            ||d.FireMastery!=0||d.LightningMastery!=0||d.MagicMastery!=0||d.ColdMastery!=0||d.PoisonMastery!=0
            ||d.Conversion!=null||d.PhysicalToMagicPercent!=0||d.LightningJavelinPercent!=null||d.FreezeColdLength||!d.SourcePoisonFrames
            ||input.Critical is not{} c||c.Passive is <0 or >100||c.Deadly is <0 or >100||c.Mastery is <0 or >100)
        {throw new InvalidDataException("Invalid Mirrored weapon input");}
        _=MirroredBladesPrepared.Attacks(input.Skill);
        if(d.SkillAttackPercent!=input.Skill.AttackPercent||e.FireMin<0||e.FireMax<e.FireMin||e.ColdMin<0||e.ColdMax<e.ColdMin
            ||e.LightningMin<0||e.LightningMax<e.LightningMin||e.MagicMin<0||e.MagicMax<e.MagicMin||e.PoisonMin<0||e.PoisonMax<e.PoisonMin
            ||e.ColdFrames<0||e.PoisonFrames<0||e.PoisonCount<0||e.LifeDrain<0||e.ManaDrain<0||e.BurnMin!=0||e.BurnMax!=0||e.BurnFrames!=0
            ||d.PhysicalSupplement is{} extra&&(extra.Min256<0||extra.Max256<extra.Min256))
        {throw new InvalidDataException("Invalid Mirrored damage metadata");}
        Int128 top=(Int128)d.Max256*Math.Max(10,100L+d.Percent+Math.Max(0,d.DemonPercent)+Math.Max(0,d.UndeadPercent))/100;
        top+=d.PhysicalSupplement?.Max256??0;top*=2; // Reject unsafe critical intermediates before a future allocation.
        if(top>int.MaxValue||top*120/100>int.MaxValue){throw new NotSupportedException("Mirrored physical packet exceeds signed FP256 domain");}
    }
    internal static MirroredWeaponPacket Prepare(MirroredWeaponInput input,int ordinal,bool critical)
    {
        Validate(input);var attacks=MirroredBladesPrepared.Attacks(input.Skill);
        if(ordinal<0||ordinal>=attacks.Length){throw new ArgumentOutOfRangeException(nameof(ordinal));}
        return new(input,attacks[ordinal],input.Damage with{Critical=critical});
    }
    internal static bool Critical(MirroredWeaponInput input,ref ulong seed)
    {
        Validate(input);ulong local=seed;var c=input.Critical;
        bool result=NpcMissileCritical.Unsigned(c.Passive,c.Deadly,c.Mastery,()=>SourceRandom.Roll(ref local));seed=local;return result;
    }
    internal static MirroredWeaponRoll Roll(MirroredWeaponPacket packet,Mobile target,ref ulong seed)
    {
        if(packet==null||packet!=Prepare(packet.Input,packet.Attack.Ordinal,packet.Damage.Critical))
        {throw new InvalidDataException("Invalid Mirrored weapon packet");}
        return RollDamage(packet.Damage,packet.Attack,target,ref seed);
    }
    internal static MirroredWeaponRoll RollDamage(WeaponMissileDamage source,MirroredBladeAttack attack,Mobile target,ref ulong seed)
    {
        ulong local=seed;var rolled=source.Roll(target,n=>(int)SourceRandom.Limited(ref local,n),out int poison);seed=local;
        int Scale(int n)=>MirroredBladesPrepared.ScaleSuppliedDirect256(n,attack);
        double Channel(double n)=>Scale(NamedElements.To256(n))/256.0;
        var damage=rolled with{Physical=Channel(rolled.Physical),Fire=Channel(rolled.Fire),Cold=Channel(rolled.Cold),
            Lightning=Channel(rolled.Lightning),Magic=Channel(rolled.Magic),Poison=0};
        // No packet length/count/drain scaling and no spell-only potion lane.
        return new(damage,Scale(poison),source.Elements.PoisonImpactFrames,source.Elements.ColdFrames);
    }
}
