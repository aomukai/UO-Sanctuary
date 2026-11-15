// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record EchoingStrikeInputProfile(string ReferenceVersion,string MissileSourceSha256,SummonSource[] Missiles);
internal sealed record EchoingWeaponInputs(int Minimum256,int Maximum256,int NormalDamage256,int OwnerPercent,
    int DemonPercent,int UndeadPercent,MissileSourceStats Elements,WeaponPhysicalSupplement PhysicalSupplement);
internal sealed record EchoingCriticalInputs(int Passive,int Deadly,int Mastery);
internal sealed record EchoingStrikeInput(uint Owner,uint Weapon,EchoingStrikeValues Skill,EchoingWeaponInputs WeaponInputs,
    int AttackBase,int AttackPercent,int MasteryAttackPercent,EchoingCriticalInputs Critical,SlowMissilePayload Slow);

// Immutable inputs only. Keep weapon/skill endpoints, ED, Damage+, critical
// chances and raw poison metadata distinct: current do166/damage16 placement
// and per-echo/return/event rules remain unresolved consumers. No rolls, mana,
// durability, hit state, flight, final packet or learned route are created.
internal static class EchoingStrikeInputs
{
    private static readonly Lazy<EchoingStrikeInputProfile> Data=new(()=>BalanceCatalog.ReadObject<EchoingStrikeInputProfile>("echoing-strike-profile"));
    internal static void Route()
    {
        var p=Data.Value;
        if(p.ReferenceVersion!="3.3.93847"||p.MissileSourceSha256!=ItemSkillExecution.MissileSourceHash
            ||p.Missiles?.Length!=2||p.Missiles[0].Key!="echoingstrike"||p.Missiles[1].Key!="echoingstrike_detonate")
        {throw new NotSupportedException("Unqualified Echoing Strike owned missile profile");}
        var m=p.Missiles[0];int N(string k)=>m.Fields.TryGetValue(k,out var v)&&int.TryParse(v.ToString(),out int n)?n:0;
        if(N("*ID")!=706||N("pSrvDoFunc")!=44||N("pSrvHitFunc")!=34||N("pSrvDmgFunc")!=16
            ||N("Vel")!=24||N("MaxVel")!=24||N("Range")!=20||N("InitSteps")!=5||N("CollideType")!=3||N("Size")!=1
            ||N("NextHit")!=1||N("NextDelay")!=20||N("CanSlow")!=1||N("ToHit")!=1||N("ReturnFire")!=1
            ||N("GetHit")!=1||N("ResultFlags")!=4||N("HitFlags")!=128||N("dParam1")!=0)
        {throw new NotSupportedException("Echoing Strike missile route changed");}
        var visual=p.Missiles[1];
        if(!visual.Fields.TryGetValue("*ID",out var id)||id.GetInt32()!=720||visual.Fields.ContainsKey("pSrvDoFunc")||visual.Fields.ContainsKey("pSrvDmgFunc"))
        {throw new NotSupportedException("Echoing visual is not an additional damage actor");}
    }
    // Same item scope as WeaponBaseDamage. Capture a ledger rather than
    // assuming that its generic damage total has Echoing's final semantics.
    private static int NormalDamage(PlayerMobile p,Server.Items.BaseWeapon weapon)
    {
        int n=NamedGear.SetBonus(p,"normal-damage");
        foreach(var item in p.Items)
        {if((item==weapon||ClassGear.Slot(item)!=GearSlot.Weapon)&&ClassGear.Active(p,item)){n=checked(n+ClassGear.Bonus(item,"normal-damage"));}}
        return checked(n*256);
    }
    internal static EchoingStrikeInput Capture(PlayerMobile owner)
    {
        // Like the shared weapon collectors, this can inspect an original
        // offline/internalized character. Future emitters own world checks.
        if(owner is not{Alive:true,Deleted:false}
            ||!ClassCreation.Profiles.TryGet(owner.Serial,out var c)||c.Class!=DiabloClass.Warlock
            ||!PlayerMeleeGeometry.WarlockMelee(owner)||CombatTiming.Equipped(owner)is not{} weapon)
        {throw new NotSupportedException("Echoing Strike needs a living Warlock and an eligible original melee weapon");}
        Route();var skill=EchoingStrikePrepared.Capture(SkillMechanics.Values(owner,CombatAbility.EchoingStrike));
        var raw=ClassStats.WeaponBaseDamage(owner);int undead=ClassGear.Bonus(owner,"dmg-undead%");
        if(ClassItemTypes.Is(ClassGear.Base(weapon).Type,"blun")){undead=checked(undead+50);}
        var inputs=new EchoingWeaponInputs(checked(raw.Min*256),checked(raw.Max*256),NormalDamage(owner,weapon),
            ClassStats.PhysicalDamageBonus(owner,weapon),ClassGear.Bonus(owner,"dmg-demon%"),undead,
            BowLaunch.WeaponElements(owner,weapon),BowLaunch.PhysicalSupplement(owner,weapon,128));
        return new(owner.Serial.Value,weapon.Serial.Value,skill,inputs,ClassStats.PlayerAttackBase(owner),ClassStats.PlayerItemAttackPercent(owner),
            WeaponMasteries.Bonus(owner,weapon,1),new(Math.Clamp(AmazonCombat.CriticalChance(owner),0,100),
                Math.Clamp(ClassGear.Bonus(owner,"deadly"),0,100),Math.Clamp(WeaponMasteries.Bonus(owner,weapon,3),0,100)),
            AmazonSlowMissiles.Capture(owner,true));
    }
}
