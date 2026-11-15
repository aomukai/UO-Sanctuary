// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record FlameWaveNumbers(int Rank,int Minimum256,int Maximum256,int GroundMinimum256,int GroundMaximum256,int Mana256,
    int HalfWidth,int DelayFrames,int WaveFrames,int TailFrames,int LingerFrames,int CooldownFrames);
internal sealed record FlameWaveBirth(FlameWaveNumbers Numbers,int HardRing,int HardApocalypse,FirestormModifiers Modifiers,SlowMissilePayload Slow);

// Isolated owned graph;the common profile DTO avoids expanding the unrelated
// summon/active-footprint catalogue. Modern156/38/42world composition is separate.
internal static class FlameWaveBirths
{
    private static readonly Lazy<MiasmaChainRoute> Data=new(()=>BalanceCatalog.ReadObject<MiasmaChainRoute>("flame-wave-route"));
    internal static MissileValues Missile(string key,SkillValues values)=>new(Data.Value.Missiles.Single(m=>m.Key==key).Fields,values);
    internal static void Route(SkillValues v)
    {
        var s=SkillMechanics.For(CombatAbility.FlameWave);var d=Data.Value;
        if(v?.Ability!=CombatAbility.FlameWave||v.Rank is <1 or >255||s.SourceId!=398||s.Number("srvdofunc")!=156||s.Text("srvmissilea")!="flamewaveunveiling"
            ||s.Text("anim")!="SC"||s.Number("UseAttackRate")!=0||s.Number("localdelay")!=15||s.Text("EType")!="fire"
            ||d.ReferenceVersion!="3.3.93847"||d.SourceSha256!=ItemSkillExecution.MissileSourceHash||d.MissileCount!=742||d.Missiles?.Length!=6)
        {throw new NotSupportedException("Unqualified pinned Flame Wave graph");}
        string[] keys=["flamewaveunveiling","flamewave","flamewavefire","flamewavelingerfire","meteorfire","fireexplode"];
        int[] ids=[708,690,695,716,240,29],dos=[42,38,6,6,5,0];
        for(int i=0;i<6;i++){var m=Missile(keys[i],v);if(m.Number("*ID")!=ids[i]||m.Number("pSrvDoFunc")!=dos[i]){throw new InvalidDataException("Flame Wave stage changed");}}
        var reveal=Missile(keys[0],v);var wave=Missile(keys[1],v);var fire=Missile(keys[2],v);var linger=Missile(keys[3],v);var child=Missile(keys[4],v);
        if(reveal.Number("Param1")!=1||reveal.Number("Param2")!=2||reveal.Number("InitSteps")!=2||reveal.Text("SubMissile1")!=keys[1]
            ||wave.Number("Vel")!=24||wave.Number("Range")!=40||wave.Number("NextDelay")!=15||wave.Number("NextHit")!=1||wave.Number("Size")!=1
            ||wave.Number("Param1")!=1||wave.Number("Param2")!=68||wave.Text("SubMissile1")!=keys[2]||wave.Text("SubMissile2")!=keys[3]
            ||fire.Number("Range")!=7||linger.Number("Range")!=7||fire.Text("SubMissile1")!="meteorfire"||linger.Text("SubMissile1")!="meteorfire"
            ||child.Number("DamageRate")!=41||child.Number("pSrvDmgFunc")!=3||child.Number("GetHit")!=0)
        {throw new InvalidDataException("Flame Wave graph dependencies changed");}
    }
    internal static FlameWaveNumbers Numbers(SkillValues v)
    {
        Route(v);var wave=Missile("flamewave",v);var fire=Missile("flamewavefire",v);var linger=Missile("flamewavelingerfire",v);
        int min=fire.ElementDamage256(false),max=fire.ElementDamage256(true);
        if(min!=linger.ElementDamage256(false)||max!=linger.ElementDamage256(true)){throw new NotSupportedException("Flame Wave child endpoints diverged");}
        return new(v.Rank,v.Damage256(false),v.Damage256(true),min,max,v.Mana256,checked((int)v.Eval("calc1")),checked((int)v.Eval("calc2")),
            wave.Number("Range"),fire.Number("Range")+wave.Number("Param1"),linger.Number("Range")+wave.Number("Param2"),15);
    }
    private static SkillValues Values(int rank,int ring,int apocalypse)=>new(SkillMechanics.For(CombatAbility.FlameWave),k=>k=="Flame Wave"?rank:0,
        k=>k=="Ring of Fire"?ring:k=="Apocalypse"?apocalypse:0,99);
    internal static FlameWaveBirth Capture(PlayerMobile p)
    {
        if(p is not{Alive:true,Deleted:false}||ClassCreation.Profiles.TryGet(p.Serial,out var c)!=true||c.Class!=DiabloClass.Warlock)
        {throw new NotSupportedException("Flame Wave requires a living Warlock/Witch");}
        var v=SkillMechanics.Values(p,CombatAbility.FlameWave);int Hard(string k)=>checked((int)v.Reference("skill",k,"blvl"));
        var b=new FlameWaveBirth(Numbers(v),Hard("Ring of Fire"),Hard("Apocalypse"),DruidFirestorm.Modifiers(p),AmazonSlowMissiles.Capture(p,true));Validate(b);return b;
    }
    internal static void Validate(FlameWaveBirth b)
    {
        if(b?.Numbers is not{} n||n.Rank is <1 or >255||b.HardRing is <0 or >20||b.HardApocalypse is <0 or >20
            ||b.Slow.VelocityPercent is <0 or >100||b.Slow.DamagePercent is <0 or >100||n!=Numbers(Values(n.Rank,b.HardRing,b.HardApocalypse))
            ||n.HalfWidth is <4 or >7||n.DelayFrames!=17||n.WaveFrames!=40||n.TailFrames!=8||n.LingerFrames!=75||n.CooldownFrames!=15)
        {throw new InvalidDataException("Invalid Flame Wave birth");}
        MiasmaBoltBirths.ValidatePacket(n.Minimum256,n.Maximum256,b.Modifiers?.FireMastery??-1,b.Modifiers);
        MiasmaBoltBirths.ValidatePacket(n.GroundMinimum256,n.GroundMaximum256,b.Modifiers.FireMastery,b.Modifiers);
    }
    internal static (int Min,int Max) Endpoints(FlameWaveBirth b,bool ground)
        =>DruidFirestorm.Endpoints(ground?b.Numbers.GroundMinimum256:b.Numbers.Minimum256,ground?b.Numbers.GroundMaximum256:b.Numbers.Maximum256,
            b.Modifiers.FireMastery,ground?SlowMissilePayload.Normal:b.Slow);
}
