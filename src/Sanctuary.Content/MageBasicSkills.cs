// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

internal static class MageBasicSkills
{
    internal static CombatAbility Ability(int element)=>NecromancerSummons.CanonicalMageElement(element) switch
    {1=>CombatAbility.IceBolt,2=>CombatAbility.FireBolt,3=>CombatAbility.ChargedBolt,_=>throw new NotSupportedException("Unknown Mage basic element")};
    internal static bool Basic(CombatAbility ability)=>ability is CombatAbility.FireBolt or CombatAbility.IceBolt or CombatAbility.ChargedBolt;
    internal static MageMissilePacket Packet(NecromancerMinion actor)
    {
        var ability=Ability(actor.MageElement);var values=MagePetBuffs.Values(ability,actor.Rank,actor.Level);
        var row=SkillMechanics.For(CombatAbility.RaiseSkeletalMage);
        var without=new SkillValues(row,k=>k==row.SourceKey?actor.Rank:0,_=>0,actor.Level);
        int element=NecromancerSummons.CanonicalMageElement(actor.MageElement);
        var normal=NecromancerMageMissiles.Packet(element,checked((int)without.Eval("sumsk1calc")));
        var mastered=NecromancerMageMissiles.Packet(element,actor.MageMissileRank);
        // Approved flat old-Mastery increase,not old base damage or a change
        // to Sorceress spell rank. Each Charged Bolt receives the same bonus.
        int min=checked(values.Damage256(false)+mastered.Minimum256-normal.Minimum256),
            max=checked(values.Damage256(true)+mastered.Maximum256-normal.Maximum256);
        return new(ability==CombatAbility.FireBolt?"firebolt":ability==CombatAbility.IceBolt?"icebolt":"chargedbolt",
            ability==CombatAbility.FireBolt?"fire":ability==CombatAbility.IceBolt?"cold":"ltng",actor.Rank,min,max,
            ability==CombatAbility.IceBolt?values.DurationFrames:0){Spell=ability};
    }
    internal static int Frames(MageMissilePacket p)=>p.Spell switch
    {CombatAbility.GlacialSpike=>40,CombatAbility.FireBolt or CombatAbility.IceBolt=>50,CombatAbility.ChargedBolt=>ChargedBolts.Frames,_=>30};
    internal static int Velocity(MageMissilePacket p)=>p.Spell switch
    {CombatAbility.GlacialSpike=>16,CombatAbility.FireBolt=>20,CombatAbility.IceBolt or CombatAbility.ChargedBolt=>12,_=>14};
    internal static int Copies(NecromancerMinion actor)=>MagePetBuffs.HardRank(actor)>=5?3:1;
    internal static int Count(NecromancerMinion actor)=>Ability(actor.MageElement)==CombatAbility.ChargedBolt
        ?checked((int)MagePetBuffs.Values(CombatAbility.ChargedBolt,actor.Rank,actor.Level).Eval("calc1")):1;
    internal static long Launch(NecromancerMinion actor,Mobile target,DateTime at)
    {
        if(actor is not{IsMage:true,Alive:true,Deleted:false}||target is not{Alive:true,Deleted:false}){return 0;}
        var packet=Packet(actor);int copies=Copies(actor),count=Count(actor),ordinal=0;long first=0;
        var rays=copies==1?new[]{new FanRay((target.X-actor.X)*2,(target.Y-actor.Y)*2,2080,0)}
            :FanGeometry.Create((target.X-actor.X)*2,(target.Y-actor.Y)*2,3);
        foreach(var ray in rays)for(int i=0;i<count;i++,ordinal++)
        {
            var aim=new Point2D(actor.X*2+ray.X,actor.Y*2+ray.Y);
            MageChargedMotion motion=null;
            if(packet.Spell==CombatAbility.ChargedBolt)
            {
                // One concatenated ordinal space keeps additional volleys'
                // deterministic paths distinct,using the existing initializer.
                uint seed=ChargedBolts.SeedSubtile(aim.X,ordinal);
                motion=new(actor.X,actor.Y,ChargedBolts.Path(seed,ChargedBolts.Heading(ray.X,ray.Y),ChargedBolts.Frames),1);
            }
            long id=NecromancerMageMissiles.LaunchPacket(actor,target,at,packet,ray,motion);if(first==0){first=id;}
        }
        return first;
    }
}
