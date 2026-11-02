// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public sealed record HammerCapture(FirestormModifiers Modifiers,SlowMissilePayload Slow,int Concentration);
internal sealed record PreparedHammerDamage(int Min256,int Max256,HammerCapture Capture);

// Damage5 keeps its double magic sampling/species percentages;ordinary Holy0
// magic mitigation shares the typed receiver without Holy Bolt's bypass.
internal static class BlessedHammerDamage
{
    internal static void Route(SkillValues values)
    {
        int M(string field)=>NecromancerSummons.Source("blessedhammer",field);var row=SkillMechanics.For(CombatAbility.BlessedHammer);
        if(values.Ability!=CombatAbility.BlessedHammer||values.Rank is <1 or >255||row.SourceId!=112||row.Number("srvdofunc")!=73||row.Text("srvmissilea")!="blessedhammer"||row.Text("EType")!="mag"||row.Number("SrcDam")!=0
            ||M("pSrvDoFunc")!=1||M("pSrvHitFunc")!=0||M("pSrvDmgFunc")!=5||M("Vel")!=18||M("MaxVel")!=30||M("Accel")!=250||M("Range")!=120||M("InitSteps")!=6
            ||M("Holy")!=0||M("CanSlow")!=0||M("ResultFlags")!=4||M("HitFlags")!=0||M("HitClass")!=0||M("GetHit")!=1||M("dParam1")!=50||M("dParam2")!=50)
        {throw new NotSupportedException("Unsupported pinned Blessed Hammer route");}
    }
    internal static PreparedHammerDamage Prepare(PlayerMobile owner,SkillValues values)
    {
        Route(values);var slow=AmazonSlowMissiles.Capture(owner,false);var mods=DruidFirestorm.Modifiers(owner) with{Potion=0};
        var raw=DruidFirestorm.Endpoints(values,NamedElements.MasteryPercent(owner,"mag"),slow);int concentration=PaladinOffense.HammerBonus(owner);
        int Scale(int value)
        {
            int result=SourcePercentage.Calculate(value,checked(100+concentration),100);
            if(result<0){throw new NotSupportedException("Blessed Hammer Concentration exceeds the supported source signed result domain");}return result;
        }
        return new(Scale(raw.Min),Scale(raw.Max),new(mods,slow,concentration));
    }
    internal static bool Release(PlayerMobile owner,SkillValues values)
    {
        var prepared=Prepare(owner,values);if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        BlessedHammers.Commit(owner,values,prepared);owner.PlaySound(0x1E5);return true; // Identified cast placeholder.
    }
    internal static int Roll(int minimum,int maximum,bool undead,bool demon,Func<int,int> limited)
    {
        // Complete older FillDamageParams/GetDamageValue behavior. Endpoints
        // already contain factory mastery/slow/Concentration for this actor.
        if(minimum<=0||maximum<=0){return 0;}
        if(minimum>maximum){(minimum,maximum)=(maximum,minimum);}
        int range=maximum-minimum;if(range>0){limited(range);} // Discarded fill.
        int raw=minimum+(range>0?limited(range):0);long total=raw;
        if(undead){total+=SourcePercentage.Calculate(raw,50,100);}
        if(demon){total+=SourcePercentage.Calculate(raw,50,100);}
        if(total>int.MaxValue){throw new NotSupportedException("Blessed Hammer species damage exceeds the source packet domain");}return (int)total;
    }
    internal static bool Supported(PlayerMobile owner,Mobile actor,out string reason)
        =>InstantSpellNpcDamage.Supported(owner,actor,"Blessed Hammer",out reason);
    internal static PreparedMonsterDamageResult Deliver(PlayerMobile owner,BaseCreature target,int magic256,HammerCapture captured,DateTime at)
    {
        if(!Supported(owner,target,out string reason)||ClassCombat.Friendly(owner,target)||magic256<0||captured?.Modifiers==null||at.Kind!=DateTimeKind.Utc)
        {throw new NotSupportedException(reason??"Unsupported Blessed Hammer receiver/capture");}
        var metadata=PlayerWeaponContact.Metadata(target,true);NativeMonsterReturn.Session(target);
        var services=new InstantSpellNpcDamage(owner,target,metadata,captured.Modifiers,at,"Blessed Hammer",()=>TelekinesisCombat.DefenderRoll());
        return PreparedMonsterDamage.ReleaseMissile(owner,target,new(0,magic256,0,0,5,Magic256:magic256),
            new(NamedCombat.Elite(target),()=>false,()=>metadata?.Velocity??0,()=>false),
            _=>throw new InvalidOperationException("Blessed Hammer has no timed stun"),services,at);
    }
}
