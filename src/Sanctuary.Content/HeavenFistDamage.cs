// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public sealed record HeavenCapture(FirestormModifiers Modifiers,SlowMissilePayload Slow,ulong Seed,bool TargetDead=false,bool TargetLost=false);
internal sealed record PreparedHeavenDamage(int Minimum256,int Maximum256,HeavenCapture Capture);
internal static class HeavenFistDamage
{
    internal static Func<int> SeedLow=()=>SorceressSpellMissiles.SeedLow();
    internal static void Route(SkillValues v)
    {
        var r=SkillMechanics.For(CombatAbility.FistOfTheHeavens);int M(string k)=>NecromancerSummons.Source("fistoftheheavensdelay",k);
        if(v.Ability!=CombatAbility.FistOfTheHeavens||v.Rank is <1 or >255||r.SourceId!=121||r.Number("srvdofunc")!=80||r.Text("srvmissilea")!="fistoftheheavensdelay"||r.Text("EType")!="ltng"||r.Number("SrcDam")!=0
            ||M("pSrvDoFunc")!=1||M("pSrvHitFunc")!=22||M("pSrvDmgFunc")!=0||M("Vel")!=0||M("Range")!=10||M("InitSteps")!=0||M("Holy")!=0||M("ResultFlags")!=4||M("GetHit")!=1||M("HitFlags")!=0||M("HitClass")!=0||M("sHitPar1")!=0||M("sHitPar2")!=0)
        {throw new NotSupportedException("Unsupported pinned Fist of the Heavens delay route");}
    }
    internal static bool Supported(PlayerMobile p,Mobile target,out string reason)=>InstantSpellNpcDamage.Supported(p,target,"Fist of the Heavens",out reason);
    internal static PreparedHeavenDamage Prepare(PlayerMobile p,Mobile target,SkillValues values)
    {
        Route(values);if(!Supported(p,target,out string reason)){throw new NotSupportedException(reason);}
        var mods=DruidFirestorm.Modifiers(p) with{Potion=0};var slow=AmazonSlowMissiles.Capture(p,false);var ends=DruidFirestorm.Endpoints(values,mods.LightningMastery,slow);
        return new(ends.Min,ends.Max,new(mods,slow,SourceRandom.InitLow(SeedLow())));
    }
    internal static bool Release(PlayerMobile p,Mobile target,SkillValues values)
    {
        var f=HeavenFists.Prepare(p,target,values,Prepare(p,target,values));if(!NecromancerEffects.Pay(p,values.Mana256)){return false;}
        HeavenFists.Commit(f);FireGround.Delay(p,CombatAbility.FistOfTheHeavens);p.PlaySound(0x1E5);return true; // Identified cast placeholder.
    }
    internal static int Roll(int lo,int hi,ref ulong seed)
    {if(lo<=0||hi<=0){return 0;}if(lo>hi){(lo,hi)=(hi,lo);}return lo+(hi>lo?(int)SourceRandom.Limited(ref seed,hi-lo):0);}
    internal static PreparedMonsterDamageResult Deliver(PlayerMobile p,BaseCreature t,int lightning256,HeavenCapture capture,DateTime at)
    {
        if(!Supported(p,t,out string reason)||ClassCombat.Friendly(p,t)||lightning256<0||capture?.Modifiers==null||at.Kind!=DateTimeKind.Utc)
        {throw new NotSupportedException(reason??"Unsupported Fist of the Heavens receiver");}
        var metadata=PlayerWeaponContact.Metadata(t,true);NativeMonsterReturn.Session(t);
        var services=new InstantSpellNpcDamage(p,t,metadata,capture.Modifiers,at,"Fist of the Heavens",()=>TelekinesisCombat.DefenderRoll());
        return PreparedMonsterDamage.ReleaseMissile(p,t,new(0,lightning256,0,0,5,Lightning256:lightning256),
            new(NamedCombat.Elite(t),()=>false,()=>metadata?.Velocity??0,()=>false),_=>throw new InvalidOperationException("Fist of the Heavens has no timed stun"),services,at);
    }
}
