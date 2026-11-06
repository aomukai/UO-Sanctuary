// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// Shared paired melee under separately approved Double Swing/Frenzy startup
// contracts. Current SQ11,ordinary contact,hand identity and GUID search are
// reused;neither current start68 nor start67 is claimed as executed.
internal static class BarbarianPairedMelee
{
    internal sealed record Series(CombatAbility Ability,Serial Primary,Serial Secondary,Map Map,Point3D Origin,Serial Target,long[] Events,int Released,bool Basic);
    public static void Configure()
    {
        void Disconnected(Mobile mobile){if(mobile is PlayerMobile p&&ClassCombat.Existing(p)?.PairedMelee!=null){ClassCombat.Stop(p);}}
        EventSink.Disconnected+=Disconnected;EventSink.Logout+=Disconnected;
    }
    public static void Initialize()=>ClassMapHooks.LocationChanged+=mobile=>
    {
        if(mobile is PlayerMobile p&&ClassCombat.Existing(p)?.PairedMelee is{} series&&(p.Location!=series.Origin||p.Map!=series.Map)){Cancel(p);}
    };
    private sealed record Rolls(Func<double> Hit,Func<double> Element,Func<int,int> Physical,Func<int,int> Poison,Random Effects);
    private static Rolls Inputs(CombatAbility ability)=>ability==CombatAbility.DoubleSwing
        ?new(BarbarianDoubleSwing.HitRoll,BarbarianDoubleSwing.ElementRoll,BarbarianDoubleSwing.PhysicalRoll,BarbarianDoubleSwing.PoisonRoll,BarbarianDoubleSwing.Effects)
        :new(BarbarianFrenzy.HitRoll,BarbarianFrenzy.ElementRoll,BarbarianFrenzy.PhysicalRoll,BarbarianFrenzy.PoisonRoll,BarbarianFrenzy.Effects);
    internal static WeaponPair? Pair(PlayerMobile owner)=>WeaponHands.Pair(owner) is{} pair&&Melee(pair.Primary)&&Melee(pair.Secondary)?pair:null;
    private static bool Melee(BaseWeapon weapon)=>WeaponHands.Eligible(weapon)&&ClassGear.Base(weapon) is{} basis&&ClassItemTypes.Is(basis.Type,"mele");
    private static IDisposable MeleeContext(PlayerMobile owner,BaseWeapon weapon)=>ClassGear.Definition(weapon)?.Category=="Throwing"?WeaponHands.UseMelee(owner,weapon):null;
    private static bool Recipient(PlayerMobile owner,Mobile target)=>target is BaseCreature{Alive:true,Deleted:false}&&ClassCombat.IsHostile(owner,target)
        &&target.Map==owner.Map&&Math.Abs(owner.Z-target.Z)<16&&owner.InLOS(target)
        &&SpellHelper.CheckTown(owner.Location,owner)&&SpellHelper.CheckTown(target.Location,owner);
    internal static bool InRange(PlayerMobile owner,Mobile target)=>target is BaseCreature recipient&&Recipient(owner,target)&&PlayerMeleeGeometry.Within(owner,recipient,1);
    private static SourceAnimationState Plan(PlayerMobile owner,CombatAbility ability)=>PlayerWeaponSequences.Schedule(11,
        PaladinZeal.FamilyCode(CombatTiming.Weapon(CombatTiming.Equipped(owner)).Family),CombatTiming.WeaponSequenceAdvance(owner,ability),0,0);
    internal static ActionTiming Timing(PlayerMobile owner,CombatAbility ability)
    {
        var events=Plan(owner,ability).Events;return new(events.Single(e=>e.Type==SourceAnimationEvents.EndAnimation).Frame,events.First(e=>e.Type==SourceAnimationEvents.ModeChange).Frame);
    }
    public static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)
    {
        if(ability is not(CombatAbility.DoubleSwing or CombatAbility.Frenzy)||!BarbarianCombat.Available(owner,ability)||Pair(owner) is not{} pair||World.FindMobile(selected) is not BaseCreature target){return false;}
        using var hand=WeaponHands.Use(owner,pair.Primary);using var melee=MeleeContext(owner,pair.Primary);
        try{if(!InRange(owner,target)){return false;}PlayerWeaponContact.RequireOrdinary(owner,target,true);}
        catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}
        bool basic=!NecromancerEffects.CanPay(owner,SkillMechanics.Values(owner,ability).Mana256);
        ActionTiming timing;int[] actions;
        try
        {
            if(basic){timing=CombatTiming.For(owner,CombatAbility.BasicAttack);actions=[timing.EffectFrame];}
            else
            {
                var plan=Plan(owner,ability);timing=new(plan.Events.Single(e=>e.Type==SourceAnimationEvents.EndAnimation).Frame,plan.Events.First(e=>e.Type==SourceAnimationEvents.ModeChange).Frame);
                actions=plan.Events.Where(e=>e.Type==SourceAnimationEvents.ModeChange).Select(e=>e.Frame).ToArray();
            }
        }
        catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}
        if(!SkillCombat.Begin(owner,ability,target.Serial,target.Location,now,(_,_,id,_)=>InRange(owner,World.FindMobile(id)),requireMana:false,timing:timing)){return false;}
        var session=ClassCombat.For(owner);session.PairedMelee=new(ability,pair.Primary.Serial,pair.Secondary.Serial,owner.Map,owner.Location,target.Serial,actions.Select(frame=>now+frame*40L).ToArray(),0,basic);
        return true;
    }
    internal static void Cancel(PlayerMobile owner)
    {
        if(ClassCombat.Existing(owner) is not{} session){return;}session.PairedMelee=null;
        if(session.PendingAbility is CombatAbility.DoubleSwing or CombatAbility.Frenzy){session.PendingAbility=CombatAbility.None;}
    }
    private static BaseCreature Next(PlayerMobile owner,Serial anchor)
    {
        int rawRange=PlayerMeleeGeometry.Reach(owner)+4;double radius=SkillGeometry.Tiles(rawRange);
        var candidates=SkillCombat.NearbySnapshot<BaseCreature>(owner.Map,owner.Location,(int)Math.Ceiling(radius))
            .Where(t=>Recipient(owner,t)&&(long)(t.X-owner.X)*(t.X-owner.X)+(long)(t.Y-owner.Y)*(t.Y-owner.Y)<=radius*radius).ToArray();
        int count=0;var found=SourceGuidTargets.Find(new(0,0,rawRange,0x20003,anchor.Value),
            (_,_,_,_)=>candidates.Select(t=>new SourceGuidTarget(unchecked((int)t.Serial.Value),t.Serial.Value)),ref count);
        return found==null?null:candidates.First(t=>t.Serial.Value==found.Id);
    }
    internal static bool Resolve(PlayerMobile owner,long now)
    {
        var session=ClassCombat.For(owner);var series=session.PairedMelee;
        if(series==null){return false;}var ability=series.Ability;
        if(session.PendingAbility!=ability||owner is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false,Spell:null}
            ||!ClassCombat.Enabled(owner)||!BarbarianCombat.Available(owner,ability)||!AlliedOverlap.Allowed(owner)||HitRecovery.Active(owner,now)
            ||owner.Map!=series.Map||owner.Location!=series.Origin||Pair(owner) is not{} pair||pair.Primary.Serial!=series.Primary||pair.Secondary.Serial!=series.Secondary)
        {Cancel(owner);return false;}
        if(now<series.Events[series.Released]){return false;}
        var weapon=WeaponHands.SecondaryForSequence(series.Released,true)?pair.Secondary:pair.Primary;
        using var hand=WeaponHands.Use(owner,weapon);using var melee=MeleeContext(owner,weapon);
        BaseCreature target;
        try
        {
            target=series.Released==0?World.FindMobile(series.Target)as BaseCreature:Next(owner,series.Target);
            if(!Recipient(owner,target)){Cancel(owner);return false;}
            PlayerWeaponContact.RequireOrdinary(owner,target,true);_=PlayerMeleeGeometry.Within(owner,target,1);
        }
        catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);Cancel(owner);return false;}
        var values=SkillMechanics.Values(owner,ability);bool powered=!series.Basic&&NecromancerEffects.CanPay(owner,values.Mana256);
        // Reserve this ordinal before native callbacks; cancellation/death during
        // a contact must not reinstall a finished series or replay a paid hit.
        var current=series with{Released=series.Released+1,Target=target.Serial};session.PairedMelee=current;
        if(powered&&!NecromancerEffects.Pay(owner,values.Mana256)){Cancel(owner);return false;}
        try
        {
            var rolls=Inputs(ability);
            var packet=PlayerWeaponContact.PrepareOrdinary(owner,target,powered?(int)values.Formula("toht"):0,powered?(int)values.Eval("calc1"):0,
                ()=>InRange(owner,target),rolls.Hit,rolls.Element,rolls.Physical,rolls.Poison,rolls.Effects,true,
                physicalToMagicPercent:powered&&ability==CombatAbility.Frenzy?checked((int)values.Eval("calc4")):0);
            owner.Direction=owner.GetDirectionTo(target);owner.DoHarmful(target,true);owner.PlaySound(0x238); // Identified native weapon placeholder.
            packet=packet with{RangeCheck=bonus=>Recipient(owner,target)&&PlayerMeleeGeometry.Within(owner,target,bonus)};
            var contact=PlayerWeaponContact.Release(owner,target,packet,TimedEffects.Now,true);
            if(powered&&ability==CombatAbility.Frenzy&&contact.Exit==MonsterContactExit.Finished&&contact.Commit!=null
                &&(contact.Packet.ResultFlags&SourceMeleeResult.Successful)!=0)
            {BarbarianFrenzyEffects.Gain(owner,values,TimedEffects.Now);}
        }
        catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);Cancel(owner);return false;}
        if(ReferenceEquals(session.PairedMelee,current))
        {
            if(current.Released==current.Events.Length){session.PairedMelee=null;session.PendingAbility=CombatAbility.None;}
            else{session.EffectAt=current.Events[current.Released];session.PendingTarget=current.Target;}
        }
        ClassCombat.SendState(owner,session);return true;
    }
}
