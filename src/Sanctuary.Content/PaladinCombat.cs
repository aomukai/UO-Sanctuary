// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class PaladinCombat
{
    public static bool Implemented(CombatAbility ability) => ability is CombatAbility.Might or CombatAbility.Defiance or CombatAbility.BlessedAim or CombatAbility.Prayer or CombatAbility.Cleansing or CombatAbility.Meditation
        or CombatAbility.ResistFire or CombatAbility.ResistCold or CombatAbility.ResistLightning or CombatAbility.Salvation or CombatAbility.Vigor or CombatAbility.Redemption or CombatAbility.Concentration or CombatAbility.Fanaticism
        or CombatAbility.HolyFire or CombatAbility.HolyShock or CombatAbility.HolyFreeze or CombatAbility.Sanctuary or CombatAbility.HolyBolt or CombatAbility.FistOfTheHeavens or CombatAbility.BlessedHammer or CombatAbility.Thorns or CombatAbility.Conviction or CombatAbility.HolyShield or CombatAbility.Vengeance or CombatAbility.Sacrifice or CombatAbility.Smite or CombatAbility.Zeal or CombatAbility.Charge or CombatAbility.Conversion;
    public static bool IsAura(CombatAbility ability) => ability is CombatAbility.Might or CombatAbility.Defiance or CombatAbility.BlessedAim
        or CombatAbility.Prayer or CombatAbility.Cleansing or CombatAbility.Meditation or CombatAbility.ResistFire or CombatAbility.ResistCold
        or CombatAbility.ResistLightning or CombatAbility.Salvation or CombatAbility.Vigor or CombatAbility.Redemption
        or CombatAbility.Concentration or CombatAbility.Fanaticism or CombatAbility.HolyFire or CombatAbility.HolyShock or CombatAbility.HolyFreeze or CombatAbility.Sanctuary or CombatAbility.Thorns or CombatAbility.Conviction;
    public static bool Handles(CombatAbility ability) => Implemented(ability);
    public static bool Available(PlayerMobile owner, CombatAbility ability) => Handles(ability)
        && ClassCreation.Profiles.TryGet(owner.Serial,out var profile) && profile.Class==DiabloClass.Paladin && SkillMechanics.Rank(owner,ability)>0;
    public static bool Begin(PlayerMobile owner, CombatAbility ability)
    {
        if (!IsAura(ability) || !Available(owner,ability) || !owner.Alive || !AlliedOverlap.Allowed(owner)) { return false; }
        return SkillBindings.Instance.Get(owner).Right==ability && PaladinAuras.Selected(owner)==ability
            || SkillBindings.Instance.Assign(owner,false,ability);
    }
    internal static bool ValidCastPoint(PlayerMobile owner,Point3D point)=>owner.Map!=null && owner.Map!=Map.Internal
        && point.X>=0 && point.Y>=0 && point.X<owner.Map.Width && point.Y<owner.Map.Height
        && owner.InRange(point,CombatProtocol.TeethRange) && owner.InLOS(point)
        && SpellHelper.CheckTown(owner.Location,owner) && SpellHelper.CheckTown(point,owner);
    public static bool ValidTarget(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point)
    {
        if(ability==CombatAbility.Vengeance){return PaladinVengeance.ValidTarget(owner,selected);}
        if(ability==CombatAbility.Sacrifice){return PaladinSacrifice.ValidTarget(owner,selected);}
        if(ability==CombatAbility.Smite){return PaladinSmite.ValidTarget(owner,selected);}
        if(ability==CombatAbility.Zeal){return PaladinZeal.ValidTarget(owner,selected);}
        if(ability==CombatAbility.Charge){return PaladinCharge.ValidTarget(owner,selected,point);}
        if(ability==CombatAbility.Conversion){return PaladinConversion.ValidTarget(owner,selected);}
        if(ability==CombatAbility.HolyShield){return owner.Map!=null && owner.Map!=Map.Internal && HolyShields.Shield(owner)!=null;}
        if(ability==CombatAbility.BlessedHammer){return owner.Map!=null && owner.Map!=Map.Internal && SpellHelper.CheckTown(owner.Location,owner);}
        if(ability is not (CombatAbility.HolyBolt or CombatAbility.FistOfTheHeavens) || !ValidCastPoint(owner,point)){return false;}
        var target=World.FindMobile(selected);
        if(ability==CombatAbility.FistOfTheHeavens){return selected==Serial.Zero?HeavenFists.Acquire(owner,point)!=null:target!=null && ClassCombat.IsHostile(owner,target);}
        return selected==Serial.Zero || HolyBolts.Candidate(owner,target);
    }
    public static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)
        =>IsAura(ability)?Begin(owner,ability):ability==CombatAbility.FistOfTheHeavens?HeavenFists.Begin(owner,selected,point,now):ability==CombatAbility.Conversion?PaladinConversion.Begin(owner,selected,point,now):ability==CombatAbility.Charge?PaladinCharge.Begin(owner,selected,point,now):ability==CombatAbility.Zeal?PaladinZeal.Begin(owner,selected,point,now):SkillCombat.Begin(owner,ability,selected,point,now,ValidTarget);
    public static bool Release(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long? now=null)
    {
        if(ability==CombatAbility.Zeal){return PaladinZeal.Release(owner,selected,now??Core.TickCount);}
        if(ability==CombatAbility.Conversion){return PaladinConversion.Release(owner,selected,now??Core.TickCount);}
        if(!Available(owner,ability) || !ValidTarget(owner,ability,selected,point) || !FireGround.Ready(owner,ability)){return false;}
        var values=SkillMechanics.Values(owner,ability);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        if(ability==CombatAbility.Vengeance){return PaladinVengeance.Strike(owner,World.FindMobile(selected),values);}
        if(ability==CombatAbility.Sacrifice){return PaladinSacrifice.Strike(owner,World.FindMobile(selected),values);}
        if(ability==CombatAbility.Smite){return PaladinSmite.Strike(owner,World.FindMobile(selected) as BaseCreature,values);}
        if(ability==CombatAbility.HolyShield){HolyShields.Cast(owner,values);}
        else if(ability==CombatAbility.BlessedHammer){BlessedHammers.Cast(owner,values);}
        else if(ability==CombatAbility.FistOfTheHeavens){HeavenFists.Cast(owner,World.FindMobile(selected),values);FireGround.Delay(owner,ability);}
        else{HolyBolts.Cast(owner,point,values);}
        owner.PlaySound(0x1E5);return true; // Placeholder cast sound.
    }
}

