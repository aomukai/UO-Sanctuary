// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record MiasmaBoltBirth(int Rank,int HardChain,int HardAbyss,EntropyCloudShape Shape,MiasmaDamage Numbers,
    int MagicMastery,FirestormModifiers Modifiers,SlowMissilePayload Slow);

// Whole-candidate release descriptor. Numeric suppliers retain their own
// acceptance;modern magic capture/pierce and hit41/do41 remain explicit policy.
internal static class MiasmaBoltBirths
{
    private static readonly Lazy<EchoingStrikeInputProfile> Data=new(()=>BalanceCatalog.ReadObject<EchoingStrikeInputProfile>("miasma-bolt-profile"));
    internal static void Route()
    {
        var d=Data.Value;
        if(d.ReferenceVersion!="3.3.93847"||d.MissileSourceSha256!=ItemSkillExecution.MissileSourceHash||d.Missiles?.Length!=3
            ||d.Missiles[0].Key!="miasmabolt"||d.Missiles[1].Key!="miasmaboltcloudmaker"||d.Missiles[2].Key!="miasmaboltcloud")
        {throw new NotSupportedException("Unqualified owned Miasma graph");}
        int N(int row,string key)=>d.Missiles[row].Fields.TryGetValue(key,out var v)&&int.TryParse(v.ToString(),out int n)?n:0;
        if(N(0,"*ID")!=697||N(0,"pSrvDoFunc")!=1||N(0,"pSrvHitFunc")!=41||N(0,"Vel")!=20||N(0,"Range")!=50
            ||N(0,"Size")!=1||N(0,"AlwaysExplode")!=1||N(0,"CollideKill")!=1||N(0,"LastCollide")!=1
            ||N(0,"sHitPar1")!=0||N(0,"sHitPar2")!=14||N(0,"sHitPar3")!=1
            ||N(1,"*ID")!=736||N(1,"pSrvDoFunc")!=37||N(1,"Param1")!=1||N(1,"InitSteps")!=1
            ||N(2,"*ID")!=705||N(2,"pSrvDoFunc")!=41||N(2,"Explosion")!=1||N(2,"NextDelay")!=25||N(2,"InitSteps")!=25
            ||N(2,"CanSlow")!=0||N(2,"GetHit")!=0||N(2,"SrcDamage")!=0)
        {throw new NotSupportedException("Pinned Miasma graph changed");}
    }
    private static SkillValues Values(int rank,int chain,int abyss,int entropy,int hardEntropy)=>new(SkillMechanics.For(CombatAbility.MiasmaBolt),
        k=>k=="Miasma Bolt"?rank:k=="Enhanced Entropy"?entropy:0,
        k=>k=="Miasma Chains"?chain:k=="Abyss"?abyss:k=="Enhanced Entropy"?hardEntropy:0,99);
    internal static MiasmaBoltBirth Capture(PlayerMobile p)
    {
        Route();var values=SkillMechanics.Values(p,CombatAbility.MiasmaBolt);
        int Hard(string key)=>checked((int)values.Reference("skill",key,"blvl"));
        var birth=new MiasmaBoltBirth(values.Rank,Hard("Miasma Chains"),Hard("Abyss"),WarlockEntropyClouds.Capture(values),MiasmaPackets.Capture(values),
            NamedElements.MasteryPercent(p,"mag"),DruidFirestorm.Modifiers(p),AmazonSlowMissiles.Capture(p,true));Validate(birth);return birth;
    }
    internal static void Validate(MiasmaBoltBirth b)
    {
        if(b==null||b.Rank is <1 or >255||b.HardChain is <0 or >20||b.HardAbyss is <0 or >20||b.Shape is not{} s
            ||s.EffectiveRank is <0 or >255||s.HardRank is <0 or >20||b.MagicMastery is <0 or >1000000||b.Modifiers is not{} m
            ||m.Potion is <0 or >1000000||m.FireMastery is <0 or >1000000||m.ColdMastery is <0 or >1000000
            ||m.LightningMastery is <0 or >1000000||m.PoisonMastery is <0 or >1000000
            ||b.Slow.VelocityPercent is <0 or >100||b.Slow.DamagePercent is <0 or >100)
        {throw new InvalidDataException("Invalid Miasma birth");}
        var v=Values(b.Rank,b.HardChain,b.HardAbyss,s.EffectiveRank,s.HardRank);
        if(b.Shape!=WarlockEntropyClouds.Capture(v)||b.Numbers!=MiasmaPackets.Capture(v))
        {throw new InvalidDataException("Miasma saved suppliers changed");}
        ValidatePacket(b.Numbers.BoltMinimum256,b.Numbers.BoltMaximum256,b.MagicMastery,m);
        ValidatePacket(b.Numbers.CloudMinimum256,b.Numbers.CloudMaximum256,b.MagicMastery,m);
    }
    internal static void ValidatePacket(int minimum,int maximum,int magicMastery,FirestormModifiers m)
    {
        if(minimum<0||maximum<minimum||magicMastery is <0 or >1000000||m==null
            ||m.Potion is <0 or >1000000||m.FireMastery is <0 or >1000000||m.ColdMastery is <0 or >1000000
            ||m.LightningMastery is <0 or >1000000||m.PoisonMastery is <0 or >1000000)
        {throw new InvalidDataException("Invalid Miasma magic/potion packet");}
        Int128 raw=Enhanced(maximum,magicMastery),total=raw;
        foreach(int mastery in new[]{m.FireMastery,m.ColdMastery,m.LightningMastery,m.PoisonMastery})
        {total+=(raw*m.Potion*(100L+mastery)+9999)/10000;}
        if(total>int.MaxValue){throw new NotSupportedException("Miasma combined potion packet exceeds signed FP256 domain");}
    }
    internal static int Enhanced(int n,int mastery)
    {
        Int128 result=(Int128)n*(100L+mastery)/100;
        if(n<0||mastery<0||result>int.MaxValue){throw new NotSupportedException("Miasma magic packet exceeds signed FP256 domain");}return(int)result;
    }
    internal static GearDamage Roll(MiasmaBoltBirth b,bool cloud,ref ulong seed)
    {
        int min=cloud?b.Numbers.CloudMinimum256:b.Numbers.BoltMinimum256,max=cloud?b.Numbers.CloudMaximum256:b.Numbers.BoltMaximum256;
        int magic=SourceSpellPacketRoll.Range(ref seed,Enhanced(min,b.MagicMastery),Enhanced(max,b.MagicMastery));
        if(!cloud){magic=b.Slow.Damage256(magic);} // Owned stationary705is not CanSlow.
        return DruidMoltenBoulder.Potion(b.Modifiers,new(0,0,0,0,0,Magic:magic/256.0));
    }
    internal static FanRay[] Makers()=>PoisonCloudGeometry.Pattern(14,0,1);
    // Complete legacy do37composition:emit before ordinary remaining-frame
    // update. For this owned maker,Range=Param1+2=3and emission threshold1.
    internal static int MakerUpdate(int remaining,Action emit)
    {
        if(remaining is <1 or >3||emit==null){throw new ArgumentException("Invalid Miasma maker update");}
        if(remaining==1){emit();}
        return BasicMissileUpdate.Run(new(true,true,0,remaining,0,0,false),new(()=>0,_=>{},()=>true,()=>true,()=>0,()=>[],_=>0,_=>0,(_,_)=>2)).Remaining;
    }
}
