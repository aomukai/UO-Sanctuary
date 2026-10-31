// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal abstract class BloodHealingServices
{
    internal abstract bool Attacker{get;}internal abstract bool Target{get;}internal abstract bool Damage{get;}
    internal abstract int TargetKind{get;}internal abstract int Drain{get;}internal abstract int Alignment{get;}
    internal abstract bool Owner{get;}internal abstract bool OwnerDead{get;}internal abstract bool AttackerDead{get;}
    internal abstract int Rank{get;}internal abstract int Share{get;}
    internal abstract int Life(bool owner);internal abstract int Maximum(bool owner);
    internal abstract void SetLife(bool owner,int value);internal abstract void Overlay(bool owner);
}
internal static class BloodGolemHealing
{
    // Event23 uses11034/11033, whose multiply-before-divide rounding differs
    // from Fog's dm expression at some ranks. Current-executable parity is open.
    internal static int Conversion(int rank)=>rank<=0?0:Math.Min(150,75+75*110*rank/(rank+6)/100);
    private static int Heal(BloodHealingServices s,bool owner,int amount)
    {
        if(amount<=0){return 0;}int life=s.Life(owner),max=s.Maximum(owner),after=checked(life+amount);
        if(after>max){amount+=max-after;after=max;}s.SetLife(owner,after);return amount;
    }
    internal static int Execute(BloodHealingServices s,int physical256,int targetLife256)
    {
        if(!s.Attacker||!s.Target||!s.Damage||s.TargetKind is not(0 or 1)){return 0;}
        int drain=s.TargetKind==1?s.Drain:100;if(drain==0||s.Alignment!=0){return 0;}
        int amount=drain==100?physical256:SourcePercentage.Calculate(physical256,drain,100);
        amount=Math.Min(amount,targetLife256);
        if(s.Owner&&s.OwnerDead||s.AttackerDead){return 0;}
        int percent=Conversion(s.Rank);if(percent<=0){return 0;}
        amount=SourcePercentage.Calculate(amount,percent,100);if(amount<=0){return 0;}
        if(s.Owner&&s.Share>0){amount-=Heal(s,true,SourcePercentage.Calculate(amount,s.Share,100));}
        int overflow=amount-Heal(s,false,amount);if(s.Owner&&overflow>0){Heal(s,true,overflow);}
        s.Overlay(false);if(s.Share>0&&s.Owner){s.Overlay(true);}return 1;
    }
    internal static (int Result,int OwnerLife,int Damage,bool Written) Incoming(bool owner,bool dead,int life,int damage,int kind,bool monster,bool skillId,bool skillRow,int percent)
    {
        if(kind!=1||damage<=0||!owner||dead||life<256||!monster||!skillId){return(0,life,damage,false);}
        int reduced=SourcePercentage.Calculate(damage,skillRow?percent:0,100);
        return(1,Math.Max(256,life-reduced),Math.Max(0,damage-reduced),true);
    }
    internal static void RequireCurrent()
    {
        var row=SkillMechanics.For(CombatAbility.BloodGolem);
        if(row.Parameter(1)!=75||row.Parameter(2)!=150||row.Parameter(3)!=30||row.Parameter(5)!=0)
        {throw new NotSupportedException("Blood Golem event parameters changed; nonzero incoming sharing is not connected");}
    }
    internal static int Alignment(BaseCreature target)
    {
        if(!CreatureProfiles.For(target).SourceStats.TryGetValue("Align",out string value))
        {throw new NotSupportedException("Blood Golem healing recipient alignment needs its source/native mapping");}
        if(string.IsNullOrEmpty(value)){return 0;}
        return int.TryParse(value,out int align)&&align is >=0 and <=2?align:throw new NotSupportedException("Unmapped Blood Golem recipient alignment");
    }
    private sealed class Native(BloodGolemBody body,BaseCreature target,PlayerMobile owner):BloodHealingServices
    {
        internal override bool Attacker=>!body.Deleted;internal override bool Target=>!target.Deleted;internal override bool Damage=>true;
        internal override int TargetKind=>1;internal override int Drain=>GemCombat.Drain(target);internal override int Alignment=>BloodGolemHealing.Alignment(target);
        internal override bool Owner=>owner!=null&&!owner.Deleted;internal override bool OwnerDead=>owner is not{Alive:true,Deleted:false};internal override bool AttackerDead=>!body.Alive;
        internal override int Rank=>body.Rank;internal override int Share=>30;
        private Mobile Unit(bool useOwner)=>useOwner?owner:body;
        internal override int Life(bool useOwner)=>SkillHealth.Balance256(Unit(useOwner));internal override int Maximum(bool useOwner)=>SkillHealth.Maximum256(Unit(useOwner));
        internal override void SetLife(bool useOwner,int value)=>SkillHealth.SetBalance256(Unit(useOwner),value);
        internal override void Overlay(bool useOwner)=>Unit(useOwner).FixedEffect(0x376A,9,20,0x21,0); // Identified native placeholder for source151.
    }
    internal static void Dealt(BloodGolemBody body,BaseCreature target,PreparedMonsterDamagePacket packet)
    {
        RequireCurrent();Execute(new Native(body,target,body.ControlMaster as PlayerMobile),packet.Physical256,SkillHealth.Balance256(target));
    }
}
