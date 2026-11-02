// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class PaladinSacrifice
{
    internal sealed record Prepared(Serial Target,bool Hit,PreparedWeaponImpact Impact,double ElementRoll,long EffectAt,PlayerWeaponContactPrepared Contact=null);
    internal static Random Effects=Random.Shared;
    internal static Func<int,int> PoisonRoll=Utility.Random;
    internal static Func<int,int> Roll=Utility.Random;
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static int Cost256(int physical256,int targetLife256,int percent)
        =>(int)Math.Clamp((long)Math.Min(Math.Max(0,physical256),Math.Max(0,targetLife256))*Math.Max(0,percent)/100,0,int.MaxValue);
    public static bool ValidTarget(PlayerMobile owner,Serial selected)=>PaladinVengeance.ValidTarget(owner,selected);
    public static void Initialize()=>ClassMapHooks.LocationChanged+=mobile=>
    {
        if(mobile is PlayerMobile owner&&ClassCombat.Existing(owner) is{} session&&session.SacrificeAttack!=null
            &&(owner.Location!=session.ActionOrigin||owner.Map!=session.ActionMap)){Cancel(owner);}
    };
    internal static void Cancel(PlayerMobile owner)
    {
        if(ClassCombat.Existing(owner) is not{} session){return;}
        session.SacrificeAttack=null;if(session.PendingAbility==CombatAbility.Sacrifice){session.PendingAbility=CombatAbility.None;}
    }
    internal static void EquipmentChanged(PlayerMobile owner)
    {
        if(ClassCombat.Existing(owner) is{} session&&session.SacrificeAttack!=null
            &&(CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=session.PendingWeapon){Cancel(owner);}
    }
    private sealed class Result(PlayerMobile owner,BaseCreature target,bool native):SourceMeleeResultServices
    {
        internal override int? AttackerKind=>0;
        internal override int? DefenderKind=>1;
        internal override bool Hostile()=>ClassCombat.IsHostile(owner,target);
        internal override bool DefenderRunning()=>throw new InvalidOperationException("NPC is not a running player");
        internal override int MeleeReach()=>throw new NotSupportedException("Sacrifice uses zero range offset");
        internal override bool InRange(int bonus)=>bonus==1&&ValidTarget(owner,target.Serial);
        internal override bool HitSuccessful(int percent,bool missile)
        {
            if(missile){throw new InvalidOperationException("Sacrifice is melee");}
            bool hit=ClassStats.CheckHit(owner,target,percent,HitRoll,notifyMelee:false)==true;
            if(hit){WeaponStartEffects.SuccessfulHitRoll(owner,target);}return hit;
        }
        internal override int BlockOrDodge(bool avoid,bool block)
        {
            // Unclassified UO monsters retain their prior native lack of a
            // D2 shield component. No D2 species or shield roll is invented.
            if(native){return 0;}
            if(avoid||!block||!SourceBlocking.TryRoll(target,true,out bool blocked,out _))
            {throw new NotSupportedException("Sacrifice recipient shield rule is unavailable");}
            return blocked?1:0;
        }
        internal override bool Uninterruptible()=>MonsterUninterruptibles.Active(target);
    }
    private static void Require(PlayerMobile owner,BaseCreature target)
    {
        PlayerWeaponContact.Require(owner,target,true);CreatureHitRecovery.PlayerHitClass(owner);
        BowLaunch.RawPoison(owner,CombatTiming.Equipped(owner));
        if(PlayerWeaponContact.Metadata(target,true)!=null&&!SourceBlocking.TryRate(target,out _,out _))
        {throw new NotSupportedException("Sacrifice recipient shield rule is unavailable");}
    }
    private static Prepared Allocate(PlayerMobile owner,Mobile target,SkillValues values,long effectAt)
    {
        // St29 allocates a record even for a miss. AllocCombat prepares the
        // typed damage before Do64 reads its physical lane for the self-cost.
        BaseCreature recipient=target as BaseCreature;
        if(recipient!=null){Require(owner,recipient);}
        int flags=recipient!=null?SourceMeleeResult.Resolve(new Result(owner,recipient,PlayerWeaponContact.Metadata(recipient,true)==null),(int)values.Formula("toht"))
            :ClassStats.CheckHit(owner,target,(int)values.Formula("toht"),HitRoll)==true?1:0;
        bool hit=(flags&1)!=0;
        double roll=0;PreparedWeaponImpact impact=default;
        if(hit)
        {
            roll=ElementRoll();
            if(recipient!=null){WeaponStartEffects.PrepareDamage(owner,recipient);}
            var damage=GearEffects.Damage(owner,roll,target,(int)values.Eval("calc1"),Roll,PoisonRoll,
                recipient==null?null:()=>WeaponPoisonPackets.Melee(WeaponPoisonPackets.AddVenom(BowLaunch.RawPoison(owner,CombatTiming.Equipped(owner)),Venoms.State(owner)),
                    NamedElements.MasteryPercent(owner,"pois"),128,PoisonRoll,overrideFrames:Venoms.State(owner)?.Frames??0));
            impact=GearEffects.PrepareWeaponImpact(owner,target,roll,Effects,damage);
            if(recipient!=null)
            {
                var poison=PoisonJavelins.PreparePoison(owner,recipient,damage.SourcePoison.Rate256,damage.SourcePoison.Frames);
                impact=impact with{LifeDamage256=checked(NamedElements.To256(impact.Parts.Total)+poison.Rate256),AllocatedPoison=poison};
            }
        }
        int frames=0;
        if(recipient!=null&&hit&&impact.Damage.Cold>0)
        {
            if(impact.Damage.ColdMs%40!=0){throw new NotSupportedException("Weapon cold duration has no exact source-frame projection");}
            frames=ColdStates.LengthFrames(impact.Damage.ColdMs/40,NamedElements.Resistance(owner,recipient,"cold",recipient.ColdResistance),
                ItemDurationDefenses.CannotFreeze(recipient),ItemDurationDefenses.HalfFreeze(recipient));
        }
        return new(target.Serial,hit,impact,roll,effectAt,recipient==null?null:new(flags,impact,WeaponLeech.Capture(owner),frames,CreatureHitRecovery.PlayerHitClass(owner),Effects));
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(World.FindMobile(selected) is BaseCreature recipient)
        {try{Require(owner,recipient);}catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}}
        if(!SkillCombat.Begin(owner,CombatAbility.Sacrifice,selected,point,now,(_,_,id,_)=>ValidTarget(owner,id))){return false;}
        var session=ClassCombat.For(owner);
        session.SacrificeAttack=Allocate(owner,World.FindMobile(selected),SkillMechanics.Values(owner,CombatAbility.Sacrifice),session.EffectAt);
        return true;
    }
    public static bool Release(PlayerMobile owner,Serial selected,long now)
    {
        var session=ClassCombat.For(owner);var prepared=session.SacrificeAttack;
        if(prepared==null||now<prepared.EffectAt){return false;}
        session.SacrificeAttack=null;
        if(prepared.Target!=selected||!PaladinCombat.Available(owner,CombatAbility.Sacrifice)
            ||World.FindMobile(selected) is not{Alive:true,Deleted:false} target){return false;}
        try{return Deliver(owner,target,SkillMechanics.Values(owner,CombatAbility.Sacrifice),prepared);}
        catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}
    }
    public static bool Strike(PlayerMobile owner,Mobile target,SkillValues values)
        =>Deliver(owner,target,values,Allocate(owner,target,values,Core.TickCount),false);
    private static bool Deliver(PlayerMobile owner,Mobile target,SkillValues values,Prepared prepared,bool checkContact=true)
    {
        if(target is BaseCreature recipient&&prepared.Contact!=null)
        {try{Require(owner,recipient);}catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}}
        int physical256=prepared.Hit?NamedElements.To256(prepared.Impact.Parts.Physical):0;
        int cost=Cost256(physical256,SkillHealth.Balance256(target),(int)values.Eval("calc2"));
        // Do64 commits direct self damage before DrainItemDurability rechecks
        // contact. Never reapply self resistance or reroll the stored packet.
        SkillHealth.PayLife(owner,cost);
        if(owner is not{Alive:true,Deleted:false}){return true;}
        if(checkContact&&!ValidTarget(owner,target.Serial)){return true;}
        owner.DoHarmful(target,true);owner.PlaySound(0x238); // Existing weapon sound placeholder.
        if(target is BaseCreature creature&&prepared.Contact!=null)
        {
            var result=PlayerWeaponContact.Release(owner,creature,prepared.Contact,TimedEffects.Now,true);
            if(owner is{Alive:true,Deleted:false}){owner.FixedEffect(0x37B9,5,5,0x21,0);}
            return result.Exit==MonsterContactExit.Finished;
        }
        using var attempted=PaladinThorns.Attempt(owner,target,false);
        if(!prepared.Hit){return true;}
        GearEffects.Strike(owner,target,prepared.ElementRoll,preparedImpact:prepared.Impact);
        if(owner is{Alive:true,Deleted:false}){owner.FixedEffect(0x37B9,5,5,0x21,0);} // Placeholder; source blood1 art is separately registered.
        return true;
    }
}
