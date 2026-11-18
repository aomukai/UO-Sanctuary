// SPDX-License-Identifier: GPL-3.0-or-later
namespace Sanctuary;
public static class WarlockCombat
{
    public static bool Implemented(CombatAbility ability) => ability is CombatAbility.Consume or CombatAbility.BloodBoil or CombatAbility.Engorge or CombatAbility.BloodOath or CombatAbility.EnhancedEntropy or CombatAbility.DemonicMastery or CombatAbility.DeathMark or CombatAbility.SummonDefiler or CombatAbility.SummonTainted or CombatAbility.SummonGoatman or CombatAbility.LevitationMastery or CombatAbility.SigilLethargy or CombatAbility.SigilRancor or CombatAbility.SigilDeath or CombatAbility.Apocalypse or CombatAbility.RingOfFire or CombatAbility.PsychicWard or CombatAbility.BladeWarp or CombatAbility.HexSiphon or CombatAbility.HexBane or CombatAbility.HexPurge or CombatAbility.Cleave or CombatAbility.BindDemon or CombatAbility.EldritchBlast or CombatAbility.EchoingStrike or CombatAbility.MirroredBlades or CombatAbility.MiasmaBolt or CombatAbility.MiasmaChain or CombatAbility.FlameWave or CombatAbility.Abyss;
    public static bool Handles(CombatAbility ability)=>ability is CombatAbility.Consume or CombatAbility.BloodBoil or CombatAbility.Engorge or CombatAbility.DeathMark or CombatAbility.SummonDefiler or CombatAbility.SummonTainted or CombatAbility.SummonGoatman or CombatAbility.SigilLethargy or CombatAbility.SigilRancor or CombatAbility.SigilDeath or CombatAbility.Apocalypse or CombatAbility.RingOfFire or CombatAbility.PsychicWard or CombatAbility.BladeWarp or CombatAbility.HexSiphon or CombatAbility.HexBane or CombatAbility.HexPurge or CombatAbility.Cleave or CombatAbility.BindDemon or CombatAbility.EldritchBlast or CombatAbility.EchoingStrike or CombatAbility.MirroredBlades or CombatAbility.MiasmaBolt or CombatAbility.MiasmaChain or CombatAbility.FlameWave or CombatAbility.Abyss;
    public static bool Available(Server.Mobiles.PlayerMobile owner,CombatAbility ability)=>Handles(ability)
        &&ClassCreation.Profiles.TryGet(owner.Serial,out var profile)&&profile.Class==DiabloClass.Warlock&&SkillMechanics.Rank(owner,ability)>0
        &&(ability!=CombatAbility.BindDemon||ClassCreation.Profiles.GetRank(owner.Serial,"warlock.binddemon")>0);
    private static bool Point(Server.Mobiles.PlayerMobile owner,Server.Serial selected,Server.Point3D requested,out Server.Point3D point)
    {
        point=requested;var entity=Server.World.FindEntity(selected);
        if(entity is{Deleted:false}){if(entity.Map!=owner.Map){return false;}point=entity.Location;}
        return SigilActors.TargetPoint(owner,point,out point);
    }
    private static bool BuffSelf(Server.Mobiles.PlayerMobile owner)=>owner is{Alive:true,Deleted:false}&&owner.Map!=null&&owner.Map!=Server.Map.Internal&&SorceressEffects.Shield(owner)==null;
    private static bool Self(Server.Mobiles.PlayerMobile owner)=>owner is{Alive:true,Deleted:false}&&owner.Map!=null&&owner.Map!=Server.Map.Internal&&!MinionSafeAreas.Contains(owner);
    public static bool Begin(Server.Mobiles.PlayerMobile owner,CombatAbility ability,Server.Serial selected,Server.Point3D point,long now)
        =>ability==CombatAbility.BindDemon?false // Explicit press/aim/release owns this action.
            :ability is CombatAbility.SummonGoatman or CombatAbility.SummonTainted or CombatAbility.SummonDefiler
            ?WarlockGoatmanCasting.Begin(owner,ability,selected,point,now)
            :ability==CombatAbility.Consume
            ?ConsumeEffects.Approved&&Available(owner,ability)&&(selected==Server.Serial.Zero?ConsumeCasting.OpenPrepared(owner):ConsumeCasting.BeginPrepared(owner,selected,now)!=null)
            :ability==CombatAbility.BloodBoil
            ?BloodBoilPrepared.Approved&&Available(owner,ability)&&BloodBoilCasting.BeginPrepared(owner,now)!=null
            :ability==CombatAbility.Engorge
            ?EngorgeEffects.Approved&&Available(owner,ability)&&EngorgeCasting.BeginPrepared(owner,selected,point,now)!=null
            :ability==CombatAbility.MirroredBlades
            ?MirroredBladesAction.BeginLearned(owner,selected,point,now)
            :ability==CombatAbility.Abyss
            ?AbyssCasting.BeginLearned(owner,selected,point,now)
            :ability==CombatAbility.FlameWave
            ?FlameWaveCasting.BeginLearned(owner,selected,point,now)
            :ability==CombatAbility.MiasmaChain
            ?MiasmaChainCasting.BeginLearned(owner,selected,point,now)
            :ability==CombatAbility.MiasmaBolt
            ?MiasmaBoltCasting.BeginLearned(owner,selected,point,now)
            :ability==CombatAbility.EchoingStrike
            ?EchoingStrikeWorld.Approved&&Available(owner,ability)&&EchoingStrikeCasting.BeginPrepared(owner,selected,point,now)!=null
            :ability==CombatAbility.DeathMark
            ?DeathMarkCasting.BeginPrepared(owner,selected,point,now,0)!=null
            :ability==CombatAbility.Cleave
            ?CleaveActionProposal.BeginPrepared(owner,selected,point,now)!=null
            :ability is CombatAbility.HexSiphon or CombatAbility.HexBane or CombatAbility.HexPurge
            ?WeaponHexes.CanCast(owner)&&SkillCombat.Begin(owner,ability,Server.Serial.Zero,owner.Location,now,(p,a,id,at)=>WeaponHexes.CanCast(p))
            :ability==CombatAbility.BladeWarp
            ?BladeWarp.Begin(owner,selected,point,now)
            :ability==CombatAbility.PsychicWard
            ?BuffSelf(owner)&&SkillCombat.Begin(owner,ability,Server.Serial.Zero,owner.Location,now,(p,a,id,at)=>BuffSelf(p))
            :ability is CombatAbility.RingOfFire or CombatAbility.EldritchBlast
            ?Self(owner)&&SkillCombat.Begin(owner,ability,Server.Serial.Zero,owner.Location,now,(p,a,id,at)=>Self(p))
            :Handles(ability)&&Point(owner,selected,point,out var ground)
                &&SkillCombat.Begin(owner,ability,selected,ground,now,(p,a,id,at)=>Point(p,id,at,out _));
    public static bool Release(Server.Mobiles.PlayerMobile owner,CombatAbility ability,Server.Serial selected)
    {
        if(!Available(owner,ability)){return false;}
        if(ability is CombatAbility.SummonGoatman or CombatAbility.SummonTainted or CombatAbility.SummonDefiler){return WarlockGoatmanCasting.Release(owner,ability);}
        if(ability is CombatAbility.Consume or CombatAbility.BloodBoil or CombatAbility.Engorge or CombatAbility.Cleave or CombatAbility.DeathMark or CombatAbility.BindDemon or CombatAbility.EchoingStrike or CombatAbility.MirroredBlades or CombatAbility.MiasmaBolt or CombatAbility.MiasmaChain or CombatAbility.FlameWave or CombatAbility.Abyss){return false;} // Its transient sequence owns release/end dispatch.
        if(ability==CombatAbility.EldritchBlast){return EldritchBlastWorld.ReleasePrepared(owner,TimedEffects.Now)!=null;}
        if(ability==CombatAbility.HexPurge){return HexPurge.CastPrepared(owner,replaceGroup:WeaponHexGroups.Approved);}
        if(ability==CombatAbility.HexBane){return HexBane.CastPrepared(owner,replaceGroup:WeaponHexGroups.Approved);}
        if(ability==CombatAbility.HexSiphon){return HexSiphon.CastPrepared(owner,replaceGroup:WeaponHexGroups.Approved);}
        if(ability==CombatAbility.BladeWarp){return BladeWarp.Release(owner,selected,ClassCombat.For(owner).PendingPoint);}
        if(ability==CombatAbility.PsychicWard){return BuffSelf(owner)&&PsychicWard.CastPrepared(owner);}
        var point=owner.Location;
        if(ability==CombatAbility.RingOfFire){if(!Self(owner)){return false;}}
        else if(!Point(owner,selected,ClassCombat.For(owner).PendingPoint,out point)){return false;}
        var values=SkillMechanics.Values(owner,ability);
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        if(ability==CombatAbility.RingOfFire)
        {if(RingOfFireWorld.CreatePaid(owner,values,TimedEffects.Now)==0){return false;}}
        else if(ability==CombatAbility.Apocalypse)
        {if(ApocalypseGround.CreatePaid(owner,values,point,TimedEffects.Now)==null){return false;}}
        else if(!NecromancerEffects.CanPay(owner,values.Mana256)||SigilActors.CreatePaid(owner,values,point,TimedEffects.Now,values.Mana256)==null){return false;}
        FireGround.Delay(owner,ability);
        if(ability==CombatAbility.RingOfFire)
        {
            owner.FixedEffect(SpellEffectGraphics.RingOfFireCast,10,8);
            owner.PlaySound(SpellEffectSounds.RingOfFireCast);
        }
        else if(ability!=CombatAbility.Apocalypse){owner.PlaySound(0x1E5);} // Identified shared Sigil cast placeholder.
        return true;
    }
}
