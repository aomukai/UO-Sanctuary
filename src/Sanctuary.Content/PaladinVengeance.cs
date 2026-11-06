// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.CompilerServices;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public readonly record struct VengeanceElements(int Base256,int Fire256,int Cold256,int Lightning256,int ColdFrames)
{
    public GearDamage Damage=>new(0,Fire256/256.0,Cold256/256.0,Lightning256/256.0,0,ColdMs:ColdFrames*40);
}

public static class PaladinVengeance
{
    internal sealed record Prepared(Serial Target,PlayerWeaponContactPrepared Contact,long EffectAt);
    private sealed class State{internal int NextLayer;}
    private static readonly ConditionalWeakTable<PlayerMobile,State> States=new();
    internal static Func<int,int> Roll=Utility.Random;
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> PhysicalRoll=Utility.RandomDouble;
    internal static Func<int,int> WeaponRoll=Utility.Random;
    internal static Func<int,int> PoisonRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    public static void Initialize()=>ClassMapHooks.LocationChanged+=mobile=>
    {
        if(mobile is PlayerMobile owner&&ClassCombat.Existing(owner) is{} s&&s.VengeanceAttack!=null
            &&(owner.Map!=s.ActionMap||owner.Location!=s.ActionOrigin)){Cancel(owner);}
    };
    internal static void Cancel(PlayerMobile owner)
    {
        if(ClassCombat.Existing(owner) is not{} s){return;}s.VengeanceAttack=null;
        if(s.PendingAbility==CombatAbility.Vengeance){s.PendingAbility=CombatAbility.None;}
    }
    internal static void EquipmentChanged(PlayerMobile owner)
    {
        if(ClassCombat.Existing(owner) is{} s&&s.VengeanceAttack!=null&&(CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=s.PendingWeapon){Cancel(owner);}
    }
    public static bool WeaponAllowed(PlayerMobile owner)
    {
        var weapon=CombatTiming.Equipped(owner);
        if(weapon==null){return true;}
        if(CombatTiming.Weapon(weapon).Family is AttackFamily.Bow or AttackFamily.Crossbow or AttackFamily.Throw){return false;}
        // Converted throwing bases currently expose missile damage; do not reuse it for melee.
        return ClassGear.Base(weapon) is not{} basis || ClassGear.Definition(weapon).Category!="Throwing" && EquipmentBalance.IsType(basis.Type,"mele");
    }
    public static bool ValidTarget(PlayerMobile owner,Serial selected)
    {
        var target=World.FindMobile(selected);
        return owner.Map!=null && owner.Map!=Map.Internal && WeaponAllowed(owner) && ClassCombat.IsHostile(owner,target)
            && owner.InRange(target,CombatTiming.Range(owner,CombatAbility.Vengeance)) && owner.InLOS(target)
            && Math.Abs(owner.Z-target.Z)<16 && SpellHelper.CheckTown(owner.Location,owner) && SpellHelper.CheckTown(target.Location,owner);
    }
    private static void Require(PlayerMobile owner,BaseCreature target)
    {
        PlayerWeaponContact.Require(owner,target,true);CreatureHitRecovery.PlayerHitClass(owner);BowLaunch.RawPoison(owner,CombatTiming.Equipped(owner));
        if(PlayerWeaponContact.Metadata(target,true)!=null&&!SourceBlocking.TryRate(target,out _,out _))
        {throw new NotSupportedException("Vengeance recipient shield rule is unavailable");}
    }
    private sealed class Result(PlayerMobile owner,BaseCreature target,bool native):SourceMeleeResultServices
    {
        internal override int? AttackerKind=>0;
        internal override int? DefenderKind=>1;
        internal override bool Hostile()=>ClassCombat.IsHostile(owner,target);
        internal override bool DefenderRunning()=>throw new InvalidOperationException("NPC is not a running player");
        internal override int MeleeReach()=>throw new NotSupportedException("Vengeance uses zero range offset");
        internal override bool InRange(int bonus)=>bonus==1&&ValidTarget(owner,target.Serial);
        internal override bool HitSuccessful(int percent,bool missile)
        {
            if(missile){throw new InvalidOperationException("Vengeance is melee");}
            bool hit=ClassStats.CheckHit(owner,target,percent,HitRoll,notifyMelee:false,npcDefense:false)==true;
            if(hit){WeaponStartEffects.SuccessfulHitRoll(owner,target);}return hit;
        }
        internal override int BlockOrDodge(bool avoid,bool block)
        {
            if(native){return 0;} // Existing unclassified UO actor policy;no source shield component is invented.
            if(!NpcBlockOrDodge.Try(target,avoid,block,out int flags,out _))
            {throw new NotSupportedException("Vengeance recipient shield rule is unavailable");}
            return flags;
        }
        internal override bool Uninterruptible()=>MonsterUninterruptibles.Active(target);
    }
    private static Prepared Allocate(PlayerMobile owner,BaseCreature target,SkillValues values,long effectAt)
    {
        int flags=SourceMeleeResult.Resolve(new Result(owner,target,PlayerWeaponContact.Metadata(target,true)==null),(int)values.Formula("toht"));
        PreparedWeaponImpact impact=default;int frames=0,layer=0;
        if((flags&1)!=0)
        {
            flags|=SkillMechanics.For(CombatAbility.Vengeance).Number("ResultFlags");
            // St35 rolls physical first,then one independent weapon base for
            // all three skill elements. AllocCombat fills ordinary item lanes.
            var range=GearEffects.PhysicalRange256(owner,target,0);int span=Math.Max(1,range.Max-range.Min);
            int physical=Math.Clamp(WeaponRoll(span),0,span-1);
            var bonus=Elements(owner,values);var state=States.GetOrCreateValue(owner);
            layer=32+state.NextLayer*16;state.NextLayer=(state.NextLayer+1)%3;
            WeaponStartEffects.PrepareDamage(owner,target);double itemRoll=PhysicalRoll();
            var damage=GearEffects.Damage(owner,itemRoll,target,skillPhysicalPercent:0,
                physicalRoll:n=>n==span?physical:throw new NotSupportedException("Vengeance physical range changed during allocation"),
                poisonFactory:()=>WeaponPoisonPackets.Melee(WeaponPoisonPackets.AddVenom(BowLaunch.RawPoison(owner,CombatTiming.Equipped(owner)),Venoms.State(owner)),
                    NamedElements.MasteryPercent(owner,"pois"),128,PoisonRoll,overrideFrames:Venoms.State(owner)?.Frames??0));
            impact=GearEffects.PrepareWeaponImpact(owner,target,itemRoll,Effects,damage,skillElements:bonus.Damage);
            var poison=PoisonJavelins.PreparePoison(owner,target,damage.SourcePoison.Rate256,damage.SourcePoison.Frames);
            impact=impact with{LifeDamage256=checked(NamedElements.To256(impact.Parts.Total)+poison.Rate256),AllocatedPoison=poison};
            if(impact.Damage.Cold>0)
            {
                if(impact.Damage.ColdMs%40!=0){throw new NotSupportedException("Vengeance cold duration has no exact source-frame projection");}
                frames=ColdStates.LengthFrames(impact.Damage.ColdMs/40,NamedElements.Resistance(owner,target,"cold",target.ColdResistance),
                    ItemDurationDefenses.CannotFreeze(target),ItemDurationDefenses.HalfFreeze(target));
            }
        }
        return new(target.Serial,new(flags,impact,WeaponLeech.Capture(owner),frames,CreatureHitRecovery.PlayerHitClass(owner),Effects,layer),effectAt);
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!PaladinCombat.Available(owner,CombatAbility.Vengeance)||!ValidTarget(owner,selected)||World.FindMobile(selected) is not BaseCreature target){return false;}
        try
        {
            Require(owner,target);
            if(!SkillCombat.Begin(owner,CombatAbility.Vengeance,selected,point,now,(_,_,id,_)=>ValidTarget(owner,id))){return false;}
            var session=ClassCombat.For(owner);session.VengeanceAttack=Allocate(owner,target,SkillMechanics.Values(owner,CombatAbility.Vengeance),session.EffectAt);return true;
        }
        catch(NotSupportedException e){Cancel(owner);owner.SendMessage(0x22,e.Message);return false;}
    }
    public static bool Release(PlayerMobile owner,Serial selected,long now)
    {
        var session=ClassCombat.For(owner);var prepared=session.VengeanceAttack;
        if(prepared==null||now<prepared.EffectAt){return false;}session.VengeanceAttack=null;
        if(prepared.Target!=selected||!PaladinCombat.Available(owner,CombatAbility.Vengeance)||!ValidTarget(owner,selected)
            ||World.FindMobile(selected) is not BaseCreature target||!FireGround.Ready(owner,CombatAbility.Vengeance)){return false;}
        try
        {
            Require(owner,target);
            if(!NecromancerEffects.Pay(owner,SkillMechanics.Values(owner,CombatAbility.Vengeance).Mana256)){return false;}
            owner.DoHarmful(target,true);owner.PlaySound(0x238); // Existing weapon/contact placeholders.
            var result=PlayerWeaponContact.Release(owner,target,prepared.Contact,TimedEffects.Now,true);
            if(target is{Alive:true,Deleted:false}){target.FixedEffect(0x374A,10,10,0x480,0);}
            return result.Exit==MonsterContactExit.Finished;
        }
        catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}
    }
    internal static int Scale(int basis256,int percent,int mastery)
    {
        long enhanced=(long)percent+(long)percent*mastery/100;
        return(int)Math.Clamp((long)basis256*enhanced/100,0,int.MaxValue);
    }
    public static VengeanceElements Elements(PlayerMobile owner,SkillValues values)
    {
        var raw=ClassStats.WeaponBaseDamage(owner);
        // Ordinary weapon endpoints project item_normaldamage for physical attacks.
        // Do35 reads min/max without that separate stat for its elemental roll.
        int normal=ClassGear.Bonus(owner,"normal-damage");
        int min=Math.Max(1,raw.Min-normal)*256,max=Math.Max(2,raw.Max-normal)*256;
        if(max<=min){max=min+256;}
        int basis=min+Math.Clamp(Roll(max-min),0,max-min-1);
        return new(basis,Scale(basis,(int)values.Eval("calc1"),NamedElements.MasteryPercent(owner,"fire")),
            Scale(basis,(int)values.Eval("calc2"),NamedElements.MasteryPercent(owner,"cold")),
            Scale(basis,(int)values.Eval("calc3"),NamedElements.MasteryPercent(owner,"ltng")),values.DurationFrames);
    }
    public static bool Strike(PlayerMobile owner,Mobile target,SkillValues values)
    {
        owner.DoHarmful(target,true);owner.PlaySound(0x238); // Existing weapon sound placeholder.
        using var attempted=PaladinThorns.Attempt(owner,target,false);
        if(ClassStats.CheckHit(owner,target,(int)values.Formula("toht"),HitRoll)!=true){return true;}
        var bonus=Elements(owner,values);
        GearEffects.Strike(owner,target,PhysicalRoll(),skillElements:bonus.Damage);
        if(target is{Alive:true,Deleted:false}){target.FixedEffect(0x374A,10,10,0x480,0);} // Placeholder elemental strike.
        return true;
    }
}
