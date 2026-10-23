// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class AmazonImpale
{
    internal sealed record Attack(Serial Weapon,Layer Layer,Serial Target,Map Map,Point3D Origin,int Reach,long Until,long EffectAt,int Rank,
        bool Basic,bool Hit,bool Released,bool OwnDepletion,PreparedWeaponImpact Impact,double Roll);
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    internal static Random SpecialWearRoll=Random.Shared;
    internal static Random WearRoll=Random.Shared;
    internal static ActionTiming Timing(string code,int advance)
    {
        advance=Math.Max(1,advance);int end=code=="2HT"?24:21,point=code=="2HT"?15:13;
        return new((end*256+advance-1)/advance,(point*256+advance-1)/advance);
    }
    internal static ActionTiming Timing(PlayerMobile owner)=>Timing(AmazonJab.Code(owner),CombatTiming.WeaponSequenceAdvance(owner,CombatAbility.Impale));
    internal static bool Durable(BaseWeapon weapon)
    {
        if(weapon==null || weapon.Deleted || weapon.MaxHitPoints<=0 || NamedGear.Indestructible(weapon) || ClassGear.State(weapon)?.BaseCode is not{} code){return false;}
        var source=NamedGear.Catalog.Base(code).SourceFields;
        return (!source.TryGetProperty("nodurability",out var no) || no.GetInt32()==0) && source.TryGetProperty("durability",out var durability) && durability.GetInt32()>0;
    }
    internal static void SpecialWear(BaseWeapon weapon,int chance,int amount,Random random)
    {
        if(!Durable(weapon)){return;}bool stack=NamedAmmunition.Maximum(weapon)>0;
        if(stack && NamedAmmunition.Remaining(weapon)<=0 || random.Next(100)>=chance){return;}
        if(stack)
        {
            if(NamedAmmunition.Consume(weapon,TimedEffects.Now)){weapon.HitPoints=weapon.MaxHitPoints;}
        }
        else{weapon.HitPoints=Math.Max(0,weapon.HitPoints-amount);}
    }
    private static BaseWeapon Weapon(PlayerMobile owner,Attack attack)=>World.FindItem(attack.Weapon) is BaseWeapon{Deleted:false} weapon
        && weapon.Parent==owner && weapon.Layer==attack.Layer && owner.FindItemOnLayer(attack.Layer)==weapon?weapon:null;
    private static bool Valid(PlayerMobile owner,Attack attack,long now)
    {
        if(attack==null || owner is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false,Spell:null} || owner.Map!=attack.Map || owner.Location!=attack.Origin
            || !AlliedOverlap.Allowed(owner) || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Amazon
            || SkillMechanics.Rank(owner,CombatAbility.Impale) is <1 or >255 || Weapon(owner,attack) is not{} weapon
            || ClassGear.State(weapon) is not{Identified:true} || ClassGear.Base(weapon) is not{} basis || !ClassItemTypes.Is(basis.Type,"spea")){return false;}
        return (attack.OwnDepletion || CombatTiming.Equipped(owner)==weapon && NamedAmmunition.CanAttack(weapon)) && (!attack.Basic || !HitRecovery.Active(owner,now));
    }
    public static bool Locked(PlayerMobile owner,long now)=>ClassCombat.Existing(owner)?.Impale is{Basic:false} attack && now<attack.Until && Valid(owner,attack,now);
    internal static void Cancel(PlayerMobile owner)
    {
        var session=ClassCombat.Existing(owner);if(session==null){return;}session.Impale=null;
        if(session.PendingAbility==CombatAbility.Impale){session.PendingAbility=CombatAbility.None;}
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!AmazonCombat.Available(owner,CombatAbility.Impale) || !AmazonJab.ValidTarget(owner,selected)){return false;}
        var weapon=CombatTiming.Equipped(owner);var values=SkillMechanics.Values(owner,CombatAbility.Impale);bool basic=!NecromancerEffects.CanPay(owner,values.Mana256);
        ActionTiming? timing=null;if(basic){using(WeaponHands.UseMelee(owner,weapon)){timing=CombatTiming.For(owner,CombatAbility.BasicAttack);}}
        if(!SkillCombat.Begin(owner,CombatAbility.Impale,selected,point,now,(_,_,id,_)=>AmazonJab.ValidTarget(owner,id),requireMana:!basic,timing:timing)){return false;}
        var session=ClassCombat.For(owner);var target=World.FindMobile(selected);int reach=AmazonJab.Range(owner);double roll=ElementRoll();PreparedWeaponImpact packet=default;
        bool hit;using(WeaponHands.UseMelee(owner,weapon))
        {
            hit=!basic || ClassStats.CheckHit(owner,target,0,HitRoll)==true;
            if(hit)
            {
                if(basic)
                {packet=GearEffects.PrepareWeaponImpact(owner,target,roll,Effects,GearEffects.Damage(owner,roll,target,0,PhysicalRoll));}
                else
                {
                    // Start7 precomputes physical before special wear. Hitflag1
                    // bypasses later species ED and ordinary critical. Weapon
                    // elemental stats are collected after wear, so a broken
                    // weapon cannot retain its old elemental contribution.
                    double physical=GearEffects.Damage(owner,roll,null,(int)values.Eval("calc1"),PhysicalRoll).Physical;
                    SpecialWear(weapon,(int)values.Eval("calc2"),(int)values.Eval("calc3"),SpecialWearRoll);
                    ClassGear.Refresh(owner);
                    var damage=GearEffects.Damage(owner,roll) with{Physical=physical};
                    packet=GearEffects.PrepareWeaponImpact(owner,target,roll,Effects,damage,projectileCritical:false);
                }
                packet=packet with{LifeDamage256=NamedElements.To256(packet.Parts.Total)};
            }
        }
        // The reviewed start dispatcher ignores a post-start debit failure.
        // Rare gear-break max-mana clipping needs modern corroboration; normal
        // starts were already validated as affordable and pay exactly once.
        if(!basic){NecromancerEffects.Pay(owner,values.Mana256);}
        session.Impale=new(weapon.Serial,weapon.Layer,selected,owner.Map,owner.Location,reach,session.NextAction,session.EffectAt,values.Rank,
            basic,hit,false,ClassGear.Broken(weapon) || !NamedAmmunition.CanAttack(weapon),packet,roll);return true;
    }
    public static bool Resolve(PlayerMobile owner,long now)
    {
        var session=ClassCombat.For(owner);var attack=session.Impale;
        if(session.PendingAbility!=CombatAbility.Impale || !Valid(owner,attack,now)){Cancel(owner);return false;}
        bool processed=false;
        if(!attack.Released && now>=attack.EffectAt)
        {
            attack=attack with{Released=true};session.Impale=attack;processed=true;var weapon=Weapon(owner,attack);var target=World.FindMobile(attack.Target);
            int reach=attack.OwnDepletion?Math.Min(attack.Reach,1):attack.Reach;
            bool eligible=ClassCombat.IsHostile(owner,target) && SpellHelper.CheckTown(owner.Location,owner) && SpellHelper.CheckTown(target.Location,owner);
            // Generic do2 writes the successful prepared hit's target state
            // before DrainItemDurability performs its separate range check.
            if(eligible && attack.Hit && !attack.Basic){ImpaleSlow.Apply(owner,target,SkillMechanics.Values(owner,CombatAbility.Impale,attack.Rank));}
            if(eligible && owner.InRange(target,reach) && owner.InLOS(target) && Math.Abs(owner.Z-target.Z)<16)
            {
                using var melee=WeaponHands.UseMelee(owner,weapon);using var attempt=PaladinThorns.Attempt(owner,target,false);
                owner.DoHarmful(target,true);owner.PlaySound(0x238); // Identified native thrust/sound placeholder.
                if(attack.Hit)
                {
                    GearEffects.Strike(owner,target,attack.Roll,Effects,wearWeapon:false,preparedImpact:attack.Impact);
                    AmazonJab.Wear(CombatTiming.Equipped(owner),WearRoll); // Separate ordinary successful-hit wear.
                    if(!attack.Basic){target.FixedEffect(0x374A,10,5,0x480,0);} // Identified impact/slow placeholder.
                }
            }
            if(attack.Basic){Cancel(owner);ClassCombat.SendState(owner,session);return processed;}
            attack=attack with{OwnDepletion=attack.OwnDepletion || ClassGear.Broken(weapon) || !NamedAmmunition.CanAttack(weapon)};session.Impale=attack;
        }
        if(now>=attack.Until){Cancel(owner);processed=true;}if(processed){ClassCombat.SendState(owner,session);}return processed;
    }
}
