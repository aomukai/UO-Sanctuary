// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record AbyssNumbers(int Rank,int Minimum256,int Maximum256,int BurstMinimum256,int BurstMaximum256,
    int TooltipMinimum256,int TooltipMaximum256,int Mana256,int Radius,int PeriodFrames,int LifetimeFrames,int BurstFrames,int CooldownFrames);
internal sealed record AbyssBirth(AbyssNumbers Numbers,int HardBolt,int HardChain,int EffectiveEntropy,int HardEntropy,int MagicMastery,FirestormModifiers Modifiers);

internal static class AbyssBirths
{
    private static readonly Lazy<MiasmaChainRoute> Data=new(()=>BalanceCatalog.ReadObject<MiasmaChainRoute>("abyss-route"));
    internal static MissileValues Missile(string key,SkillValues v)=>new(Data.Value.Missiles.Single(m=>m.Key==key).Fields,v);
    internal static void Route(SkillValues v)
    {
        var s=SkillMechanics.For(CombatAbility.Abyss);var d=Data.Value;var route=WarlockGroundRoutes.For(CombatAbility.Abyss);
        if(v?.Ability!=CombatAbility.Abyss||v.Rank is <1 or >255||s.SourceId!=402||s.Text("srvmissilea")!="abysscenter"
            ||s.Text("anim")!="SC"||s.Number("UseAttackRate")!=0||s.Number("localdelay")!=30||s.Text("EType")!="mag"
            ||route.StartFunction!=14||route.DoFunction!=28||route.Missile!=709||route.Size!=3||d.ReferenceVersion!="3.3.93847"
            ||d.SourceSha256!=ItemSkillExecution.MissileSourceHash||d.MissileCount!=742||d.Missiles?.Length!=3)
        {throw new NotSupportedException("Unqualified pinned Abyss graph");}
        var c=Missile("abysscenter",v);var b=Missile("abyssexplode",v);var visual=Missile("abyss",v);
        const string radius="\"min((par3+(sksrc('Enhanced Entropy'.lvl)/par5)),11)\"";
        if(c.Number("*ID")!=709||c.Number("pSrvDoFunc")!=39||c.Number("pSrvHitFunc")!=11||c.Number("Param1")!=12||c.Number("Param2")!=50
            ||c.Number("NextHit")!=1||c.Number("NextDelay")!=12||c.Number("Size")!=3||c.Text("HitSubMissile1")!="abyssexplode"
            ||c.Text("CltSubMissile1")!="abyss"||c.Text("Radius")!=radius||b.Text("Radius")!=radius
            ||b.Number("*ID")!=715||b.Number("pSrvDoFunc")!=39||b.Number("Range")!=1||b.Number("pSrvHitFunc")!=0
            ||visual.Number("*ID")!=714||visual.Number("pSrvDoFunc")!=0||visual.Number("Range")!=60
            ||c.Number("CanSlow")!=0||b.Number("CanSlow")!=0||c.Text("EType")!="mag"||b.Text("EType")!="mag")
        {throw new NotSupportedException("Pinned Abyss stage dependencies changed");}
    }
    // Explicit candidate sksrc binding, scoped to these two owned expressions.
    // Do not teach the general missile evaluator that sksrc aliases skill().
    internal static int Radius(MissileValues m,int effective)
    {
        if(effective is <0 or >255){throw new NotSupportedException("Invalid captured Entropy rank");}
        string expression=m.Text("Radius").Replace("sksrc('Enhanced Entropy'.lvl)",effective.ToString(System.Globalization.CultureInfo.InvariantCulture),StringComparison.Ordinal);
        return checked((int)SkillFormula.Evaluate(expression,(name,_,_)=>
            name=="par3"?m.Number("Param3"):name=="par5"?m.Number("Param5"):
            throw new NotSupportedException("Unmapped Abyss radius context")));
    }
    internal static AbyssNumbers Numbers(SkillValues v)
    {
        Route(v);var c=Missile("abysscenter",v);var b=Missile("abyssexplode",v);int effective=checked((int)v.Reference("skill","Enhanced Entropy","lvl"));
        int radius=Radius(c,effective);if(radius!=Radius(b,effective)){throw new NotSupportedException("Abyss stage radii differ");}
        return new(v.Rank,c.ElementDamage256(false),c.ElementDamage256(true),b.ElementDamage256(false),b.ElementDamage256(true),
            v.Damage256(false),v.Damage256(true),v.Mana256,radius,12,checked((int)c.Eval("Range")),1,30);
    }
    private static SkillValues Values(int rank,int bolt,int chain,int entropy,int hard)=>new(SkillMechanics.For(CombatAbility.Abyss),
        k=>k=="Abyss"?rank:k=="Enhanced Entropy"?entropy:0,k=>k=="Miasma Bolt"?bolt:k=="Miasma Chains"?chain:k=="Enhanced Entropy"?hard:0,99);
    internal static AbyssBirth Capture(PlayerMobile p)
    {
        if(p is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(p.Serial,out var c)||c.Class!=DiabloClass.Warlock)
        {throw new NotSupportedException("Abyss requires a living Warlock/Witch");}
        var v=SkillMechanics.Values(p,CombatAbility.Abyss);int Hard(string key)=>checked((int)v.Reference("skill",key,"blvl"));
        var b=new AbyssBirth(Numbers(v),Hard("Miasma Bolt"),Hard("Miasma Chains"),checked((int)v.Reference("skill","Enhanced Entropy","lvl")),
            Hard("Enhanced Entropy"),NamedElements.MasteryPercent(p,"mag"),DruidFirestorm.Modifiers(p));Validate(b);return b;
    }
    internal static void Validate(AbyssBirth b)
    {
        if(b?.Numbers is not{} n||n.Rank is <1 or >255||b.HardBolt is <0 or >20||b.HardChain is <0 or >20||b.EffectiveEntropy is <0 or >255
            ||b.HardEntropy is <0 or >20||n!=Numbers(Values(n.Rank,b.HardBolt,b.HardChain,b.EffectiveEntropy,b.HardEntropy))
            ||n.Radius is <7 or >11||n.PeriodFrames!=12||n.LifetimeFrames!=50||n.BurstFrames!=1||n.CooldownFrames!=30)
        {throw new InvalidDataException("Invalid Abyss birth");}
        MiasmaBoltBirths.ValidatePacket(n.Minimum256,n.Maximum256,b.MagicMastery,b.Modifiers);
        MiasmaBoltBirths.ValidatePacket(n.BurstMinimum256,n.BurstMaximum256,b.MagicMastery,b.Modifiers);
    }
    internal static GearDamage Roll(AbyssBirth b,bool terminal,ref ulong seed)
    {
        int min=terminal?b.Numbers.BurstMinimum256:b.Numbers.Minimum256,max=terminal?b.Numbers.BurstMaximum256:b.Numbers.Maximum256;
        int raw=SourceSpellPacketRoll.Range(ref seed,MiasmaBoltBirths.Enhanced(min,b.MagicMastery),MiasmaBoltBirths.Enhanced(max,b.MagicMastery));
        return DruidMoltenBoulder.Potion(b.Modifiers,new(0,0,0,0,0,Magic:raw/256.0));
    }
}
