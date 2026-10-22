// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Collections;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class PaladinSanctuary
{
    internal static Func<int,int> Roll=Utility.Random;
    public static bool Active(Mobile owner)=>PaladinAuras.Effect(owner,CombatAbility.Sanctuary)!=null;
    public static int DamagePercent(Mobile owner,Mobile target)=>GemCombat.Undead(target)?PaladinAuras.Effect(owner,CombatAbility.Sanctuary)?.Percent ?? 0:0;
    public static int AttackFlat(Mobile owner,Mobile target)=>GemCombat.Undead(target)?PaladinAuras.Effect(owner,CombatAbility.Sanctuary)?.AttackPercent ?? 0:0;
    public static int PhysicalResistance(Mobile owner,Mobile target,int resistance)=>Active(owner) && GemCombat.Undead(target)?Math.Min(resistance,0):resistance;
    internal static void Pulse(PlayerMobile owner,SkillValues values,DateTime now)
    {
        int frames=Math.Max(5,(int)values.Eval("perdelay"));
        // Preserve the inspected supplier-stat threshold without charging idle/push
        // pulses. Pinned-build user checks confirm knockback still works at zero mana.
        bool stats=NecromancerEffects.CanPay(owner,values.Mana256);
        PaladinAuras.ElementalWeapon(new(owner.Serial.Value,owner.Serial.Value,CombatAbility.Sanctuary,values.Rank,
            stats?(int)values.Eval("passivecalc1"):0,now.AddMilliseconds((frames+1)*40),AttackPercent:stats?(int)values.Eval("passivecalc2"):0));
        if(SpellHelper.IsTown(owner.Location,owner)){return;}
        int min=(int)Math.Clamp(NamedElements.Enhance(owner,values.Damage256(false),"mag"),0,int.MaxValue),max=(int)Math.Clamp(NamedElements.Enhance(owner,values.Damage256(true),"mag"),0,int.MaxValue);
        int damage=min+(max>min?Roll(max-min):0);double radius=SkillGeometry.Tiles(values.Eval("aurarangecalc"));
        using var targets=PooledRefList<BaseCreature>.Create();
        foreach(var target in owner.Map.GetMobilesInRange<BaseCreature>(owner.Location,(int)Math.Ceiling(radius)))
        {
            double dx=target.X-owner.X,dy=target.Y-owner.Y;
            if(GemCombat.Undead(target) && ClassCombat.IsHostile(owner,target) && Math.Abs(target.Z-owner.Z)<16
                && dx*dx+dy*dy<=radius*radius && !SpellHelper.IsTown(target.Location,target)){targets.Add(target);}
        }
        foreach(var target in targets)
        {
            if(owner is not{Alive:true,Deleted:false}){break;}if(target is not{Alive:true,Deleted:false}){continue;}
            double dx=target.X-owner.X,dy=target.Y-owner.Y;
            int loss=SkillCombat.AuraElement(owner,target,(int)Math.Clamp(Math.Floor(damage*PaladinElements.Proximity(Math.Sqrt(dx*dx+dy*dy),radius)),0,int.MaxValue),"mag");
            if(target is{Alive:true,Deleted:false} && !SourceKnockbacks.Begin(owner,target) && SourceKnockbacks.Rule(target) is{Knockback:false,GetHit:true})
            {CreatureHitRecovery.Apply(target,owner,loss);}
        }
        owner.FixedEffect(0x375A,10,10,0x481,0); // Placeholder Sanctuary pulse; final ring/bolts/impact art tracked separately.
    }
}
