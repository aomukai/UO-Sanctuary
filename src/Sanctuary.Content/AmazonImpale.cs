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
        bool Basic,bool Hit,bool Released,bool OwnDepletion,PreparedWeaponImpact Impact,double Roll)
    {internal PlayerWeaponContactPrepared Contact{get;init;}}
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Func<int,int> PoisonRoll=Utility.Random;
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
        if (Durable(weapon) && random.Next(100) < chance)
            weapon.HitPoints = Math.Max(0, weapon.HitPoints - amount);
    }
    private static BaseWeapon Weapon(PlayerMobile owner,Attack attack)=>World.FindItem(attack.Weapon) is BaseWeapon{Deleted:false} weapon
        && weapon.Parent==owner && weapon.Layer==attack.Layer && owner.FindItemOnLayer(attack.Layer)==weapon?weapon:null;
    private static bool Valid(PlayerMobile owner,Attack attack,long now)
    {
        if(attack==null || owner is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false,Spell:null} || owner.Map!=attack.Map || owner.Location!=attack.Origin
            || !AlliedOverlap.Allowed(owner) || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Amazon
            || SkillMechanics.Rank(owner,CombatAbility.Impale) is <1 or >255 || Weapon(owner,attack) is not{} weapon
            || ClassGear.State(weapon) is not{Identified:true} || ClassGear.CombatBase(weapon) is not{} basis || !ClassItemTypes.Is(basis.Type,"spea")){return false;}
        return (attack.OwnDepletion || CombatTiming.Equipped(owner)==weapon) && (!attack.Basic || !HitRecovery.Active(owner,now));
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
        bool hit;PlayerWeaponContactPrepared contact;
        using(WeaponHands.UseMelee(owner,weapon))
        {
            if(target is not BaseCreature creature){session.PendingAbility=CombatAbility.None;return false;}
            try
            {
                PlayerWeaponContact.Require(owner,creature,true);
                if(basic)
                {
                    contact=PlayerWeaponContact.PrepareOrdinary(owner,creature,0,0,()=>AmazonJab.ValidTarget(owner,selected),
                        HitRoll,()=>roll,PhysicalRoll,PoisonRoll,Effects,allowNative:true,suppressWear:true);
                }
                else
                {
                    // Retain the current forced-success convention;modern
                    // shield/avoidance behavior remains explicitly unverified.
                    WeaponStartEffects.SuccessfulHitRoll(owner,creature);
                    double physical=GearEffects.Damage(owner,roll,null,(int)values.Eval("calc1"),PhysicalRoll).Physical;
                    SpecialWear(weapon,(int)values.Eval("calc2"),(int)values.Eval("calc3"),SpecialWearRoll);
                    ClassGear.Refresh(owner);WeaponStartEffects.PrepareDamage(owner,creature);
                    // Hitflag1 retains pre-wear physical without later species
                    // ED or critical. Other weapon lanes are filled after wear.
                    var active=CombatTiming.Equipped(owner);
                    var damage=GearEffects.Damage(owner,roll,poisonRoll:PoisonRoll,poisonFactory:()=>
                        WeaponPoisonPackets.Melee(WeaponPoisonPackets.AddVenom(BowLaunch.RawPoison(owner,active),Venoms.State(owner)),
                            NamedElements.MasteryPercent(owner,"pois"),128,PoisonRoll,overrideFrames:Venoms.State(owner)?.Frames??0)) with{Physical=physical};
                    packet=GearEffects.PrepareWeaponImpact(owner,creature,roll,Effects,damage,projectileCritical:false);
                    var poison=PoisonJavelins.PreparePoison(owner,creature,damage.SourcePoison.Rate256,damage.SourcePoison.Frames);
                    packet=packet with{LifeDamage256=checked(NamedElements.To256(packet.Parts.Total)+poison.Rate256),AllocatedPoison=poison};
                    if(damage.Cold>0&&damage.ColdMs%40!=0){throw new NotSupportedException("Impale cold duration has no exact source-frame projection");}
                    int cold=damage.Cold>0?ColdStates.LengthFrames(damage.ColdMs/40,NamedElements.Resistance(owner,creature,"cold",creature.ColdResistance),ItemDurationDefenses.CannotFreeze(creature),ItemDurationDefenses.HalfFreeze(creature)):0;
                    contact=new(SourceMeleeResult.Successful|SourceMeleeResult.GetHit,packet,WeaponLeech.Capture(owner),cold,
                        CreatureHitRecovery.PlayerHitClass(owner),Effects,HitFlags:1,SuppressWear:true);
                }
                hit=(contact.Flags&SourceMeleeResult.Successful)!=0;packet=contact.Impact;
                contact=contact with{AttemptScoped=true,WeaponWear=()=>AmazonJab.Wear(CombatTiming.Equipped(owner),WearRoll)};
            }
            catch(NotSupportedException e){session.PendingAbility=CombatAbility.None;owner.SendMessage(0x22,e.Message);return false;}
        }
        // The reviewed start dispatcher ignores a post-start debit failure.
        // Rare gear-break max-mana clipping needs modern corroboration; normal
        // starts were already validated as affordable and pay exactly once.
        if(!basic){NecromancerEffects.Pay(owner,values.Mana256);}
        session.Impale=new(weapon.Serial,weapon.Layer,selected,owner.Map,owner.Location,reach,session.NextAction,session.EffectAt,values.Rank,
            basic,hit,false,ClassGear.Broken(weapon),packet,roll){Contact=contact};return true;
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
                owner.DoHarmful(target,true);AmazonMeleeEffects.Thrust(owner,CombatAbility.Impale,attack.Basic);
                if(target is BaseCreature creature&&attack.Contact is{} contact)
                {
                    contact=contact with{RangeCheck=_=>owner.Map==creature.Map&&owner.InRange(creature,reach)&&owner.InLOS(creature)&&Math.Abs(owner.Z-creature.Z)<16};
                    try{AmazonMeleeEffects.ReleaseContact(owner,creature,contact,CombatAbility.Impale,attack.Basic);}
                    catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);}
                }
            }
            if(attack.Basic){Cancel(owner);ClassCombat.SendState(owner,session);return processed;}
            attack=attack with{OwnDepletion=attack.OwnDepletion || ClassGear.Broken(weapon)};session.Impale=attack;
        }
        if(now>=attack.Until){Cancel(owner);processed=true;}if(processed){ClassCombat.SendState(owner,session);}return processed;
    }
}
