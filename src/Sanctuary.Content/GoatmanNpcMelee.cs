// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record GoatmanNpcWeapon(int Minimum,int Maximum,int DamagePercent,int FireMinimum256=0,int FireMaximum256=0,
    int LightningMinimum256=0,int LightningMaximum256=0,int ColdMinimum256=0,int ColdMaximum256=0,int ColdFrames=0);
internal readonly record struct GoatmanNpcDamage(int Physical256,int Magic256,int Fire256,int Lightning256,int Cold256,int ColdFrames,int StunFrames);
internal sealed record GoatmanNpcPrepared(int Physical256,int Magic256,int Fire256,int Lightning256,int Cold256,int ColdFrames,int StunFrames,
    int Total256,int ResultFlags,int HitClass,int CriticalOverlay,bool Critical);
internal sealed record GoatmanBerserkState(int Flags,int UntilFrame,int Rank,int PhysicalResist,int ArmorOverride);
internal static class GoatmanNpcMelee
{
    private const int NoFill=128|256|512|32768;
    internal static GoatmanNpcPrepared Prepare(int sourceId,int rank,int resultFlags,int targetLife256,GoatmanNpcWeapon weapon,
        Func<GoatmanNpcDamage,GoatmanNpcDamage> mitigate,Func<int,int> limited,Func<uint> raw)
    {
        if(sourceId is not(416 or 418)){throw new NotSupportedException("Goatman Frenzy/Cleave callbacks remain unverified");}
        ArgumentNullException.ThrowIfNull(weapon);ArgumentNullException.ThrowIfNull(mitigate);ArgumentNullException.ThrowIfNull(limited);ArgumentNullException.ThrowIfNull(raw);
        if(targetLife256<0||(resultFlags&~0xffff)!=0){throw new ArgumentOutOfRangeException();}
        var values=GoatmanNpcSkills.Values(sourceId,rank);bool hit=(resultFlags&1)!=0;int hitClass=0,stun=0;
        if(hit)
        {
            // Both rows have zero-width skill-element damage. Stun supplies
            // length through elemental assignment before ordinary fill.
            if(limited(0)!=0){throw new NotSupportedException("Zero-width Goatman skill-element roll must return0");}
            if(sourceId==416){hitClass=96;stun=values.DurationFrames;}
        }
        GoatmanNpcDamage damage=new(0,0,0,0,0,0,stun);bool critical=false;int overlay=0;
        if(hit&&(resultFlags&NoFill)==0)
        {
            // Reuse the checked unarmed monster roll,with intrinsic critical
            // deferred until after conversion. Minimum/maximum already include
            // supplied flat damage;source ED is aggregated exactly once.
            var packet=DruidDireWolfHit.Packet(weapon.Minimum,weapon.Maximum,checked(weapon.DamagePercent+(int)values.Eval("calc1")),
                weapon.FireMinimum256,weapon.FireMaximum256,weapon.LightningMinimum256,weapon.LightningMaximum256,
                weapon.ColdMinimum256,weapon.ColdMaximum256,weapon.ColdFrames,0,limited,raw);
            // Ordinary added element stats do not choose a hit class here.
            // Source release derives presentation later;only the skill element
            // or intrinsic critical may set it during allocation.
            int converted=sourceId==418?SourcePercentage.Calculate(packet.Physical256,(int)values.Eval("calc4"),100):0;
            int physical=Math.Max(0,checked(packet.Physical256-converted));
            critical=raw()%100<NecromancerSummons.Source("wargoatman","Crit");int factor=critical?2:1;
            if(critical){if((hitClass&0xf0)==0){hitClass|=0x10;}else{overlay=54;}}
            damage=mitigate(new(checked(physical*factor),checked(converted*factor),checked(packet.Fire256*factor),
                checked(packet.Lightning256*factor),checked(packet.Cold256*factor),packet.ColdFrames,stun));
            if(damage is{Physical256:<0}or{Magic256:<0}or{Fire256:<0}or{Lightning256:<0}or{Cold256:<0}or{ColdFrames:<0}or{StunFrames:<0})
            {throw new NotSupportedException("Negative Goatman packet mitigation");}
        }
        int total=checked(damage.Physical256+damage.Magic256+damage.Fire256+damage.Lightning256+damage.Cold256);
        if(hit&&(resultFlags&NoFill)==0&&(total&~255)>(targetLife256&~255)){resultFlags|=2;}
        return new(damage.Physical256,damage.Magic256,damage.Fire256,damage.Lightning256,damage.Cold256,damage.ColdFrames,damage.StunFrames,total,resultFlags,hitClass,overlay,critical);
    }
    // Explicit NPC start adapter;caller owns targeting,mode/mana rules and
    // scheduling. Prepare failure cannot install a self-state.
    internal static GoatmanNpcPrepared BeginFor(BaseCreature actor,int sourceId,int rank,int resultFlags,int targetLife256,
        GoatmanNpcWeapon weapon,Func<GoatmanNpcDamage,GoatmanNpcDamage> mitigate,Func<int,int> limited,Func<uint> raw,DateTime at)
    {
        if(actor is not{Alive:true,Deleted:false}||at.Kind!=DateTimeKind.Utc){throw new ArgumentException("NPC melee start needs a living actor and UTC clock");}
        var packet=Prepare(sourceId,rank,resultFlags,targetLife256,weapon,mitigate,limited,raw);
        if(sourceId==418){GoatmanBerserkEffects.Begin(actor,rank,at);}
        return packet;
    }
    internal static void RefreshForRelease(BaseCreature actor,int sourceId,int rank,DateTime at)
    {
        if(sourceId is not(416 or 418)){throw new NotSupportedException("Unsupported Goatman release state");}
        if(sourceId==418){GoatmanBerserkEffects.Impact(actor,rank,at);}
    }
    internal static PreparedMonsterDamageResult ReleaseQualified(BaseCreature actor,int sourceId,int rank,BaseCreature target,
        GoatmanNpcPrepared captured,PreparedMonsterRecipient recipient,Func<int,int> limited,PreparedMonsterDamageServices services,DateTime at)
    {
        // Source do2 refreshes the self-state before target/contact work. The
        // actual contact and mode gates still belong to the actor consumer.
        RefreshForRelease(actor,sourceId,rank,at);
        ArgumentNullException.ThrowIfNull(captured);
        return PreparedMonsterDamage.Release(actor,target,new(captured.Physical256,captured.Total256,captured.StunFrames,
            captured.ColdFrames,captured.ResultFlags,captured.HitClass,false,captured.CriticalOverlay),recipient,limited,services,at);
    }
    internal static GoatmanBerserkState StartBerserk(int frame,int rank,GoatmanBerserkState existing,bool allocationSucceeds)
    {
        var values=GoatmanNpcSkills.Values(418,rank);
        if(existing==null&&!allocationSucceeds){return null;}
        int duration=(int)values.Eval("calc2");if(duration<=0){duration=10;}
        return new(existing?.Flags??2,checked(frame+duration),rank,(int)values.Eval("aurastatcalc1"),(int)values.Eval("aurastatcalc2"));
    }
    internal static GoatmanBerserkState ImpactBerserk(int rank,GoatmanBerserkState existing,bool allocationSucceeds)
    {
        var values=GoatmanNpcSkills.Values(418,rank);
        if(existing==null&&!allocationSucceeds){return null;}
        return new(existing?.Flags??4,existing?.UntilFrame??0,rank,(int)values.Eval("aurastatcalc1"),(int)values.Eval("aurastatcalc2"));
    }
}
