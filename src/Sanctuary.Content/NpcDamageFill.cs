// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
namespace Sanctuary;

// Bound independently to the pinned D2C_ItemStats enum by the source probe.
internal enum NpcFillStat
{
    ARMORCLASS=31,
    ITEM_DAMAGETARGETAC=120,
    ITEM_DEMONDAMAGE_PERCENT=121,
    ITEM_UNDEADDAMAGE_PERCENT=122,
    DAMAGE_VS_MONTYPE=180,
    PASSIVE_CRITICAL_STRIKE=337,
    ITEM_DEADLYSTRIKE=141,
    FIREMINDAM=48,
    FIREMAXDAM=49,
    LIGHTMINDAM=50,
    LIGHTMAXDAM=51,
    COLDMINDAM=54,
    COLDMAXDAM=55,
    MAGICMINDAM=52,
    MAGICMAXDAM=53,
    PASSIVE_FIRE_MASTERY=329,
    PASSIVE_LTNG_MASTERY=330,
    PASSIVE_COLD_MASTERY=331,
    PASSIVE_MAG_MASTERY=357,
    LIFEDRAINMINDAM=60,
    LIFEDRAINMAXDAM=61,
    MANADRAINMINDAM=62,
    MANADRAINMAXDAM=63,
    STAMDRAINMINDAM=64,
    STAMDRAINMAXDAM=65,
    SKILL_BYPASS_UNDEAD=103,
    SKILL_BYPASS_DEMONS=104,
    SKILL_BYPASS_BEASTS=106,
    POISONMINDAM=57,
    POISONMAXDAM=58,
    PASSIVE_POIS_MASTERY=332,
    SKILL_POISON_OVERRIDE_LENGTH=101,
    POISONLENGTH=59,
    POISON_COUNT=326,
    COLDLENGTH=56,
    STUNLENGTH=66,
}

// Raw allocation fields. Positive drain fields and status lengths remain
// distinct from eventual resource/HP/status/event delivery.
internal sealed record NpcFillPacket(int Physical=0,int Fire=0,int Lightning=0,int Cold=0,int Magic=0,int Poison=0,
    int ColdFrames=0,int PoisonFrames=0,int StunFrames=0,int LifeDrain=0,int ManaDrain=0,int StaminaDrain=0,
    int AdditionalPercent=0,uint HitFlags=0,int ResultFlags=0,int ConversionType=0,int ConversionPercent=0,
    int FreezeFrames=0,int BurnFrames=0,int HitClass=0,int Overlay=0);
internal abstract class NpcDamageFillServices
{
    internal abstract bool AttackerPresent{get;}
    internal abstract bool DefenderPresent{get;}
    internal abstract int ActorKind{get;}
    internal abstract bool GoodAlignment{get;}
    internal abstract bool TargetMonster{get;}
    internal abstract bool Demon{get;}
    internal abstract bool Undead{get;}
    internal abstract int MonsterType{get;}
    internal abstract int Armor{get;set;}
    internal abstract bool Blunt{get;}
    internal abstract int Hireling{get;}
    internal abstract int IntrinsicCritical{get;}
    internal abstract int Stat(NpcFillStat stat);
    internal abstract IEnumerable<EquipmentStatValue> TypeDamage{get;}
    internal abstract bool TypeMatches(int layer,int monsterType);
    internal abstract int WeaponCritical();
    internal abstract void Enter(bool ranged);
    internal abstract void Restore(bool ranged);
    internal virtual void Overlay(int value){}
    internal abstract int Physical(int additional,int initial,int sourceDamage);
    internal abstract int Limited(int bound);
    internal abstract uint Raw();
    internal abstract NpcFillPacket AttackedInMelee(NpcFillPacket packet);
}

// Complete ordinary FillDamageValues orchestration with explicit target,
// inventory,skill/event/RNG services. Existing arithmetic consumers are reused.
internal static class NpcDamageFill
{
    internal static NpcFillPacket Fill(NpcDamageFillServices s,NpcFillPacket p,bool ranged,int sourceDamage=128)
    {
        ArgumentNullException.ThrowIfNull(s);ArgumentNullException.ThrowIfNull(p);
        if(sourceDamage is <0 or >255){throw new ArgumentOutOfRangeException(nameof(sourceDamage));}
        if(!s.AttackerPresent||!s.DefenderPresent){return p;}
        int share=sourceDamage==0?128:sourceDamage;
        s.Enter(ranged);p=p with{HitFlags=p.HitFlags|32};int Read(NpcFillStat stat)=>s.Stat(stat);
        if((s.ActorKind==0||s.GoodAlignment)&&s.TargetMonster)
        {
            int ac=Read(NpcFillStat.ITEM_DAMAGETARGETAC);if(ac!=0){s.Armor=Math.Max(checked(ac+s.Armor),0);}
            int extra=p.AdditionalPercent;
            if(s.Demon){int n=Read(NpcFillStat.ITEM_DEMONDAMAGE_PERCENT);if(n>0){extra=checked(extra+n);}}
            if(s.Undead){int n=checked((s.Blunt?50:0)+Read(NpcFillStat.ITEM_UNDEADDAMAGE_PERCENT));if(n>0){extra=checked(extra+n);}}
            if(s.MonsterType>0)
            {foreach(var stat in s.TypeDamage.Take(128)){if(s.TypeMatches((ushort)stat.Packed,s.MonsterType)){extra=checked(extra+stat.Value);}}}
            p=p with{AdditionalPercent=extra};
        }
        if((p.HitFlags&1)==0)
        {
            int physical=s.Physical(p.AdditionalPercent,p.Physical,share);
            bool critical=NamedCombat.MeleeCritical(()=>ranged?0:s.WeaponCritical(),()=>Read(NpcFillStat.PASSIVE_CRITICAL_STRIKE),()=>Read(NpcFillStat.ITEM_DEADLYSTRIKE),s.Raw);
            p=p with{Physical=critical?checked(physical*2):physical,ResultFlags=critical?p.ResultFlags|8192:p.ResultFlags};
        }
        int Fixed(NpcFillStat stat)=>checked(Read(stat)*256);
        int Element(NpcFillStat minimum,NpcFillStat maximum,NpcFillStat mastery,int initial)
        {
            int high=Fixed(maximum),low=0,percent=0;
            if(high>=8){low=Fixed(minimum);percent=Read(mastery);}
            return NpcEquipmentElements.Channel(low,high,percent,initial,s.Limited,share);
        }
        p=p with{Fire=Element(NpcFillStat.FIREMINDAM,NpcFillStat.FIREMAXDAM,NpcFillStat.PASSIVE_FIRE_MASTERY,p.Fire)};
        p=p with{Lightning=Element(NpcFillStat.LIGHTMINDAM,NpcFillStat.LIGHTMAXDAM,NpcFillStat.PASSIVE_LTNG_MASTERY,p.Lightning)};
        p=p with{Cold=Element(NpcFillStat.COLDMINDAM,NpcFillStat.COLDMAXDAM,NpcFillStat.PASSIVE_COLD_MASTERY,p.Cold)};
        p=p with{Magic=Element(NpcFillStat.MAGICMINDAM,NpcFillStat.MAGICMAXDAM,NpcFillStat.PASSIVE_MAG_MASTERY,p.Magic)};
        int hire=s.Hireling;
        int Drain(NpcFillStat minimum,NpcFillStat maximum,int initial)
        {
            int high=Fixed(maximum);int n=high>=8?SourceElementRoll.Roll256(Fixed(minimum),high,0,0,initial,s.Limited):Math.Max(initial,0);
            return checked(n+n*share/128);
        }
        if(s.ActorKind==1&&hire==0)
        {
            if((p.HitFlags&4)==0){p=p with{LifeDrain=Drain(NpcFillStat.LIFEDRAINMINDAM,NpcFillStat.LIFEDRAINMAXDAM,p.LifeDrain)};}
            if((p.HitFlags&8)==0){p=p with{ManaDrain=Drain(NpcFillStat.MANADRAINMINDAM,NpcFillStat.MANADRAINMAXDAM,p.ManaDrain)};}
            if((p.HitFlags&16)==0){p=p with{StaminaDrain=Drain(NpcFillStat.STAMDRAINMINDAM,NpcFillStat.STAMDRAINMAXDAM,p.StaminaDrain)};}
        }
        else if(s.ActorKind==0||hire!=0)
        {
            if((p.HitFlags&4)==0){p=p with{LifeDrain=checked(p.LifeDrain+Read(NpcFillStat.LIFEDRAINMINDAM))};}
            if((p.HitFlags&8)==0){p=p with{ManaDrain=checked(p.ManaDrain+Read(NpcFillStat.MANADRAINMINDAM))};}
            if(Read(NpcFillStat.SKILL_BYPASS_UNDEAD)!=0){p=p with{HitFlags=p.HitFlags|256};}
            if(Read(NpcFillStat.SKILL_BYPASS_DEMONS)!=0){p=p with{HitFlags=p.HitFlags|512};}
            if(Read(NpcFillStat.SKILL_BYPASS_BEASTS)!=0){p=p with{HitFlags=p.HitFlags|1024};}
        }
        int poisonMinimum=Read(NpcFillStat.POISONMINDAM),poisonMaximum=Read(NpcFillStat.POISONMAXDAM);
        var poison=WeaponPoisonPackets.Melee(new(PoisonMin:poisonMinimum,PoisonMax:poisonMaximum),poisonMaximum>0?Read(NpcFillStat.PASSIVE_POIS_MASTERY):0,share,s.Limited,p.Poison);
        int poisonFrames=p.PoisonFrames;
        if(poison.Rate256!=0)
        {
            int over=Read(NpcFillStat.SKILL_POISON_OVERRIDE_LENGTH),length=0,count=0;
            if(over<=0){length=Read(NpcFillStat.POISONLENGTH);count=Read(NpcFillStat.POISON_COUNT);}
            poisonFrames=WeaponPoisonPackets.Length(poisonFrames,length,count,over);
        }
        p=p with{Poison=poison.Rate256,PoisonFrames=poisonFrames};
        if(p.Cold>0){p=p with{ColdFrames=checked(p.ColdFrames+share*Read(NpcFillStat.COLDLENGTH)/128)};}
        if(p.StunFrames==0){p=p with{StunFrames=checked(p.StunFrames+share*Read(NpcFillStat.STUNLENGTH)/128)};}
        // The pinned older body has no implemented burn-stat collection. Keep
        // its initial burn metadata;do not invent the commented-out routine.
        if((s.ActorKind==0||hire!=0)&&(p.ResultFlags&32)==0){p=s.AttackedInMelee(p);}
        if(p.ConversionType!=0)
        {
            int converted=SourcePercentage.Calculate(p.Physical,p.ConversionPercent,100);
            p=p with{Physical=Math.Max(checked(p.Physical-converted),0)};
            int type=p.ConversionType==10?(int)(s.Raw()%5)+1:p.ConversionType;
            p=type switch
            {
                1=>p with{Fire=checked(p.Fire+converted)},2=>p with{Lightning=checked(p.Lightning+converted)},3=>p with{Magic=checked(p.Magic+converted)},
                4=>p with{Cold=checked(p.Cold+converted),ColdFrames=Math.Max(p.ColdFrames,50)},
                5=>p with{Poison=checked(p.Poison+converted/8),PoisonFrames=Math.Max(p.PoisonFrames,50)},
                11=>p with{Fire=checked(p.Fire+converted),BurnFrames=Math.Max(p.BurnFrames,50)},
                12=>p with{Cold=checked(p.Cold+converted),FreezeFrames=Math.Max(p.FreezeFrames,50)},_=>p
            };
        }
        if(s.ActorKind==1&&NpcEquipmentCritical.Intrinsic(new(0,0,0,s.IntrinsicCritical),s.Raw))
        {
            var critical=NpcEquipmentCritical.ApplyIntrinsic(p.Physical,new(0,p.Fire/256.0,p.Cold/256.0,p.Lightning/256.0,0,Magic:p.Magic/256.0),new(p.Poison,p.PoisonFrames),true,p.HitClass);
            if(critical.Overlay!=0){s.Overlay(critical.Overlay);}
            p=p with{Physical=critical.Physical256,Fire=checked((int)(critical.Elements.Fire*256)),Cold=checked((int)(critical.Elements.Cold*256)),Lightning=checked((int)(critical.Elements.Lightning*256)),Magic=checked((int)(critical.Elements.Magic*256)),Poison=critical.Poison.Rate256,HitClass=critical.HitClass,Overlay=critical.Overlay==0?p.Overlay:critical.Overlay};
        }
        s.Restore(ranged);return p;
    }
}
