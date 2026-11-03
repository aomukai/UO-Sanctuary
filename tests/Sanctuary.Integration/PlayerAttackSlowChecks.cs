// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class PlayerAttackSlowChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PlayerAttackSlowChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("player-attack-slow-check","Player aggregate slow, final rate clamp and queued timing",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("player-attack-slow-check: passed");}catch(Exception e){Log.Error(e,"player-attack-slow-check: failed");}}));
    private static void Numeric()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"player-attack-slow-probe.json")));
        int engines=0,claws=0;
        foreach(var row in fixture.RootElement.GetProperty("engine").EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();
            int actual=CombatTiming.FrameAdvance(new(AttackFamily.Thrust,V(2)),V(0),V(3),V(1),V(4)==1?-30:0,V(5));
            Require(actual==V(6)&&actual==V(7),"Unchanged source aggregate attack-rate result: "+row);engines++;
        }
        foreach(var row in fixture.RootElement.GetProperty("modern").EnumerateArray())
        {
            if(row[0].GetString()!="FISTS_OF_FIRE"){continue;}int V(int i)=>row[i].GetInt32();
            Require(AssassinClaws.Advance(V(2),V(4)+V(6),V(8),V(7))==V(12),"Unchanged current-family claw rate: "+row);claws++;
        }
        Require(engines==1296&&claws==9216,"Incomplete player aggregate rate matrix");
        Require(CombatTiming.FrameAdvance(new(AttackFamily.Thrust,0),0,150,100)==128
            &&CombatTiming.FrameAdvance(new(AttackFamily.Thrust,0),0,200,0)==38,"Final-clamp boundary goldens");
    }
    private static void Slows(PlayerMobile p)
    {
        TimedEffects.Chill(p,50,60000,TimedEffects.Now);TimedEffects.Slow(p,50,TimedEffects.Now);
        NecromancerEffects.Profiles.Store(p,new(Curse:new(CombatAbility.Decrepify,p.Serial.Value,1,TimedEffects.Now.AddMinutes(1),p.X,p.Y)));
        Require(GearEffects.ChillPercent(p)==50&&TimedEffects.SlowAttack(p,TimedEffects.Now)==50&&NecromancerEffects.Slow(p)==50,
            "Individual slow suppliers changed");
        Require(GearEffects.AttackSlowTotal(p)==150&&GearEffects.AttackSlow(p)==85&&GearEffects.MovementSlow(p)==75,
            "Raw player aggregate or bounded native projections");
    }
    private static void Check()
    {
        Numeric();var auras=PaladinAuras.Snapshot();
        foreach(var cls in new[]{DiabloClass.Assassin,DiabloClass.Paladin,DiabloClass.Druid})
        {
            var p=ClassSkillChecks.Player(cls);p.Name="PlayerAttackSlowFixture";BaseWeapon weapon=null;BaseCreature target=null;
            try
            {
                FixtureSpace.Corridor(p,7000,10,2900+(int)cls*4);
                PaladinAuras.Restore(auras with{Leases=auras.Leases.Append(new PaladinAuraLease(p.Serial.Value,p.Serial.Value,CombatAbility.Fanaticism,1,50,TimedEffects.Now.AddMinutes(1))).ToArray()});
                if(cls==DiabloClass.Assassin)
                {
                    p.AddItem(new Backpack());while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Fixture strength");}
                    while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Fixture dexterity");}
                    weapon=new Tekagi();ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));p.AddToBackpack(weapon);
                    Require(p.EquipItem(weapon),"Actual converted claw equip");ClassGear.Refresh(p);
                    AssassinBuffs.Cast(p,SkillMechanics.Values(p,CombatAbility.BurstOfSpeed));
                }
                if(cls==DiabloClass.Druid){Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werewolf,TimedEffects.Now),"Prepared wolf form");}
                Slows(p);int haste=CombatTiming.SkillAttackRate(p),ias=CombatTiming.Ias(p),wsm=CombatTiming.Weapon(CombatTiming.Equipped(p)).Modifier;
                int rate=Math.Clamp(100+120*ias/(120+ias)+haste-wsm-150,15,175),advance=256*rate/100;
                Require(CombatTiming.WeaponSequenceAdvance(p)==advance,"Live player aggregate timing");
                Require(CombatTiming.WeaponSequenceAdvance(p,CombatAbility.Jab)==CombatTiming.FrameAdvance(new(AttackFamily.Thrust,wsm),ias,150,haste,-30),"Live player sequence adjustment");
                if(cls==DiabloClass.Assassin)
                {
                    Require(AssassinClaws.Advance(p)==AssassinClaws.Advance(wsm,ias,150,haste),"Claw consumer pre-capped combined slows");
                    Require(LobProjectiles.Timing(p)==LobProjectiles.Timing(128*rate/100),"Lob/Sentry consumer pre-capped combined slows");
                    string code=PaladinZeal.AnimationCode(p);int basis=code is "HT1" or "HT2"?208:256;
                    Require(TigerStrikes.Timing(p)==TigerStrikes.Timing(code,basis*rate/100),"Single-claw consumer pre-capped combined slows");
                    target=new CobraPassiveTarget{Name="PlayerAttackSlowTarget",Frozen=true,CantWalk=true,NoKillAwards=true};
                    target.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);target.AIObject?.AITimer.Stop();target.SetHits(10000);
                    var expected=AssassinClaws.Timing(p);var s=ClassCombat.For(p);long now=Core.TickCount;s.NextAction=s.RecoveryUntil=now;p.Mana=p.ManaMax;
                    Require(ClassCombat.Perform(p,CombatAbility.FistsOfFire,target,target.Location,now),"Slowed learned claw start");
                    Require(s.EffectAt==now+expected.EffectMs&&s.NextAction==now+expected.DurationMs,"Accepted claw action ignored composed speed");
                    long effect=s.EffectAt,end=s.NextAction;TimedEffects.Clear(p);NecromancerEffects.Profiles.Clear(p);
                    Require(s.EffectAt==effect&&s.NextAction==end&&AssassinClaws.Timing(p).Frames<expected.Frames,"Effect removal changed an already-queued deadline or left next-entry timing stale");
                    ClassCombat.Stop(p);
                }
                else if(cls==DiabloClass.Paladin)
                {
                    Require(PaladinZeal.Timing(p)==PaladinZeal.Timing(PaladinZeal.AnimationCode(p),advance),"Live Zeal bypassed shared rate composition");
                    Require(CombatTiming.For(p,CombatAbility.Smite)==CombatTiming.Calculate(cls,CombatAbility.Smite,new(AttackFamily.Unarmed,0),ias,0,150,haste),"Ordinary player timing pre-capped combined slows");
                    var cast=CombatTiming.For(p,CombatAbility.HolyBolt);TimedEffects.Clear(p);NecromancerEffects.Profiles.Clear(p);
                    Require(CombatTiming.For(p,CombatAbility.HolyBolt)==cast,"Attack slow changed an FCR cast");
                }
                else
                {
                    var state=DruidPlayerForms.State(p);int skill=state.Form.Modifiers.AttackRate+DruidAttackCharges.AttackRate(p)+haste;
                    var expected=DruidFormRates.Calculate(new(CombatAbility.Werewolf,16,256,0,0,ias,skill,150));
                    Require(DruidFormAttacks.Progress(p)==expected.Progress256,"Form consumer pre-capped combined slows");
                    Require(CombatTiming.For(p,CombatAbility.BasicAttack)==DruidFormAttacks.Schedule(CombatAbility.Werewolf,expected.Progress256),"Transformed action dispatch ignored aggregate slows");
                }
            }
            finally
            {
                ClassCombat.Stop(p);DruidPlayerForms.Clear(p);AssassinBuffs.Clear(p);TimedEffects.Clear(p);NecromancerEffects.Profiles.Clear(p);
                PaladinAuras.Restore(auras);target?.Delete();weapon?.Delete();p.Delete();
            }
        }
        Log.Information("Player attack slows:1296source and9216current-family native comparisons;actual chill/item slow/Decrepify/Fanaticism/Burst suppliers,claw/ordinary/S2/Zeal/form dispatch,captured queued deadline,next-entry refresh,FCR and bounded NPC projection controls pass");
    }
}
