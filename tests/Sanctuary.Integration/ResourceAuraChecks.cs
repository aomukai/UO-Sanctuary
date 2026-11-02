// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ResourceAuraChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ResourceAuraChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Resource aura rank/heal/payment and delayed shared input",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("resource-aura-check",Check);
        foreach(var ability in new[]{CombatAbility.Prayer,CombatAbility.Cleansing,CombatAbility.Meditation})foreach(string mode in new[]{"right","quick"})
        {var captured=ability;string label="resource-aura-"+ability.ToString().ToLowerInvariant()+"-"+mode;
            Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>MightAuraChecks.Input(n,label,captured,true)));}
    }
    private static void Select(PlayerMobile p)=>Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Prayer),"Prayer selection");
    private static void Due(PlayerMobile chosen,PlayerMobile other)
    {
        // Isolate one forced-due supplier for rank/payment composition; natural cadence is checked elsewhere.
        var all=PaladinAuras.Snapshot();var now=TimedEffects.Now;
        PaladinAuras.Restore(all with{Emitters=all.Emitters.Select(e=>e.Owner==chosen.Serial.Value?e with{NextUtc=now}:e.Owner==other.Serial.Value?e with{NextUtc=now.AddMinutes(1)}:e).ToArray()});
    }
    private static void Check()
    {
        var weak=BlessedHammerDamageChecks.Owner();var strong=BlessedHammerDamageChecks.Owner();strong.MoveToWorld(new(weak.X+2,weak.Y,weak.Z),weak.Map);
        var ally=BlessedHammerDamageChecks.Owner();ally.MoveToWorld(new(weak.X+4,weak.Y,weak.Z),weak.Map);
        void Clean(){foreach(var p in new[]{weak,strong,ally}){PaladinAuras.Clear(p);p.Corpse?.Delete();p.Delete();}}
        void Later(Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(80),()=>{try{action();}catch(Exception e){Clean();Log.Error(e,"resource-aura-result: failed");}});
        try
        {
            Require(ClassCreation.Profiles.TryTrain(strong.Serial,"paladin.prayer",out _),"Stronger Prayer hard rank");
            var at=TimedEffects.Now;Require(PaladinCleansing.Shorten(at.AddMilliseconds(199*40),at,61)==at.AddMilliseconds(121*40)
                &&PaladinCleansing.Shorten(at.AddMilliseconds(299*40),at,61)==at.AddMilliseconds(182*40)
                &&PaladinCleansing.Curable("amplifydamage")&&!PaladinCleansing.Curable("battlecry"),"Compiled Cleansing frame/curability controls");
            SkillHealth.SetBalance256(ally,SkillHealth.Maximum256(ally)-20*256-17);int life=SkillHealth.Balance256(ally);
            NecromancerEffects.SetMana256(weak,256);NecromancerEffects.SetMana256(strong,304);Select(strong);Due(strong,weak);
            Later(()=>
            {
                var lease=PaladinAuras.Effect(ally,CombatAbility.Prayer);
                Require(lease is{Rank:2,Percent:768}&&SkillHealth.Balance256(ally)==life+768&&NecromancerEffects.ManaBalance256(strong)==0,"Strong useful Prayer heal/payment");
                Select(weak);Due(weak,strong);
                Later(()=>
                {
                    Require(PaladinAuras.Effect(ally,CombatAbility.Prayer)==lease&&SkillHealth.Balance256(ally)==life+768
                        &&NecromancerEffects.ManaBalance256(weak)==256&&!PaladinAuras.ManaSuppressed(weak),"Weaker Prayer healed,paid or refreshed stronger state");
                    Require(ClassCreation.Profiles.TryTrain(weak.Serial,"paladin.prayer",out _),"Equal Prayer hard rank");NecromancerEffects.SetMana256(weak,304);Due(weak,strong);
                    Later(()=>
                    {
                        Require(PaladinAuras.Effect(ally,CombatAbility.Prayer) is{Rank:2,Percent:768} equal&&equal.Source==strong.Serial.Value
                            &&SkillHealth.Balance256(ally)==life+1536&&NecromancerEffects.ManaBalance256(weak)==0&&PaladinAuras.ManaSuppressed(weak),"Equal Prayer creator/heal/once fractional payment");
                        SkillHealth.SetBalance256(ally,SkillHealth.Maximum256(ally));NecromancerEffects.SetMana256(weak,304);Due(weak,strong);
                        Later(()=>
                        {
                            Require(NecromancerEffects.ManaBalance256(weak)==304&&!PaladinAuras.ManaSuppressed(weak),"Full-health Prayer paid or retained suppression");
                            SkillHealth.SetBalance256(weak,SkillHealth.Maximum256(weak)-1);Due(weak,strong);
                            Later(()=>
                            {
                                Require(SkillHealth.Balance256(weak)==SkillHealth.Maximum256(weak)&&NecromancerEffects.ManaBalance256(weak)==0&&PaladinAuras.ManaSuppressed(weak),"One-FP useful heal/cap/fractional payment");
                                Clean();Log.Information("Resource auras:source frame/curability goldens;forced-due strong/refused-weak/equal/full/one-FP Prayer heal/payment;equal original creator;natural delayed right/quick input and original resource reloads retained");Log.Information("resource-aura-result: passed");
                            });
                        });
                    });
                });
            });
        }
        catch{Clean();throw;}
    }
}
