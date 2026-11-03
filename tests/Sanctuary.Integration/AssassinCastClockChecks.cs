// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class AssassinCastClockChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AssassinCastClockChecks));
    private static bool _creating;
    private static void Freeze()
    {var mana=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>_creating||p.Name=="AssassinCastClockFixture"?TimeSpan.FromHours(1):mana?.Invoke(p)??Mobile.DefaultManaRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Freeze();
    public static void Configure()
    {
        Freeze();Register("assassin-cast-clock-check",Check);
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;string label=quick?"assassin-cast-clock-quick":"assassin-cast-clock-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,captured)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Assassin SC/FCR clocks and separate Blade Shield start transaction",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        PlayerMobile p;_creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Assassin);}finally{_creating=false;}p.Name="AssassinCastClockFixture";
        if(p.Backpack==null){p.AddItem(new Backpack());}
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Claw Strength");}
        while(p.RawDex<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Claw Dexterity");}
        FixtureSpace.Corridor(p,6200,26,3600);Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,100*256+17);}
    private static void Gear(PlayerMobile p)
    {
        Item Named(string key)
        {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(173),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Actual gear: "+key);ClassGear.Refresh(p);return item;}
        var claw=Named("Cutthroat1");Named("Magefist");Require(ClassGear.Base((BaseWeapon)claw).Type=="h2h2"&&CombatTiming.Equipped(p)==claw,"Actual elite claw class");
    }
    private static void Clear(PlayerMobile p)
    {BladeShields.Clear(p);AssassinBuffs.Clear(p);Venoms.Clear(p);CloakShadows.Clear(p);}
    private static void Clean(PlayerMobile p,Mobile target=null)
    {ClassCombat.Stop(p);Clear(p);target?.Delete();p.Delete();}
    private static bool Effect(PlayerMobile p,CombatAbility ability)=>ability switch
    {
        CombatAbility.BladeShield=>BladeShields.State(p)?.NextPulseUtc!=null,
        CombatAbility.Venom=>Venoms.State(p)!=null,
        CombatAbility.BurstOfSpeed or CombatAbility.Fade=>AssassinBuffs.State(p)?.Ability==ability,
        _=>false
    };
    private static bool Start(PlayerMobile p,CombatAbility ability,BaseCreature target,long now)
        =>ClassCombat.Perform(p,ability,ability is CombatAbility.PsychicHammer or CombatAbility.MindBlast?target:null,target?.Location??p.Location,now);
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"assassin-cast-clock-source-probe.json")));
        int comparisons=0,routes=0,starts=0,casts=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {var t=AssassinCastClock.Calculate(r[1].GetInt32());Require(t.EffectFrame==r[3][0][1].GetInt32()&&t.Frames==r[3][1][1].GetInt32(),"Complete source Assassin SC differential");comparisons++;}
        var skills=SkillMechanics.All.Where(r=>AssassinCastClock.Handles(r.Ability)).ToArray();
        Require(comparisons==2590&&skills.Length==7,"SC source/route coverage");
        foreach(var ability in new[]{CombatAbility.FireBlast,CombatAbility.ShockWeb,CombatAbility.TigerStrike,CombatAbility.BladeFury,CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster})
        {Require(!AssassinCastClock.Handles(ability),"SC captured a separate/gated route");}
        foreach(var skill in skills)for(int i=0;i<259;i++)
        {
            int fcr=i<256?i:i==256?500:i==257?1000:10000;
            Require(CombatTiming.Calculate(DiabloClass.Assassin,skill.Ability,new(AttackFamily.Bow,-20),90,fcr,85,70)==AssassinCastClock.Calculate(fcr),"SC leaked IAS/weapon/chill/attack rate");routes++;
        }
        foreach(bool boosted in new[]{false,true})
        {
            var p=Owner();var target=PoisonDaggerChecks.Target(p);
            try
            {
                if(boosted){Gear(p);}int fcr=boosted?20:0;var expected=AssassinCastClock.Calculate(fcr);
                Require(CombatTiming.Fcr(p)==fcr,"Actual FCR");
                var trap=FireBlasts.Timing(p);Require(CombatTiming.For(p,CombatAbility.FireBlast)==trap,"S2 placement clock changed");
                foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster,CombatAbility.BladeFury,CombatAbility.WakeOfFire})
                {Require(!ClassCombat.Available(p,ability),"Clock enabled an unsupported skill");}
                foreach(var skill in skills)
                {
                    var ability=skill.Ability;Clear(p);Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;long at=Core.TickCount;
                    Require(CombatTiming.For(p,ability)==expected&&Start(p,ability,target,at),"Learned SC start: "+ability);var s=ClassCombat.For(p);
                    Require(s.Timing==expected&&s.EffectAt==at+expected.EffectMs&&s.NextAction==at+expected.DurationMs
                        &&NecromancerEffects.ManaBalance256(p)==mana-(ability==CombatAbility.BladeShield?cost:0)&&!Effect(p,ability),"SC unpaid/paid-start phase");
                    long effect=s.EffectAt;ClassCombat.Stop(p);
                    Require(!ClassCombat.ResolveAction(p,effect)&&!Effect(p,ability)&&NecromancerEffects.ManaBalance256(p)==mana-(ability==CombatAbility.BladeShield?cost:0)
                        &&(ability!=CombatAbility.BladeShield||BladeShields.State(p) is{NextPulseUtc:null}),"Cancellation refunded/activated/replayed SC");starts++;
                }
                foreach(var ability in new[]{CombatAbility.BurstOfSpeed,CombatAbility.Fade,CombatAbility.Venom,CombatAbility.BladeShield})
                {
                    Clear(p);Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
                    Require(Start(p,ability,target,Core.TickCount),"Actual SC effect start");var s=ClassCombat.For(p);
                    Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&ClassCombat.ResolveAction(p,s.EffectAt)&&!ClassCombat.ResolveAction(p,s.EffectAt),"Early/duplicate SC release");
                    Require(Effect(p,ability)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"SC effect/payment");
                    Require(!Start(p,ability,target,s.NextAction-1)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"SC tail reentry/payment");casts++;
                }
            }
            finally{Clean(p,target);}
        }
        Require(routes==1813&&starts==14&&casts==8,"Missing Assassin clock coverage");
        Log.Information("Assassin SC clock:2590source comparisons,1813static clocks,14actual start/cancel routes,8buff/gear/effect casts;Blade Shield paid state/initial pulse,elite claw,S2 and unavailable skills retain");
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();if(quick){Gear(p);}var ability=quick?CombatAbility.BladeShield:CombatAbility.BurstOfSpeed;
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Actual Assassin bindings");
        var account=FixtureNetwork.Authenticate(network,p,"assassin-clock-");Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
        bool began=false,finished=false;string label=quick?"assassin-cast-clock-quick":"assassin-cast-clock-right";long deadline=Core.TickCount+10000;
        void Cleanup(){Clean(p);account.Delete();}
        void Inspect()
        {
            if(finished){return;}
            try
            {
                Require(Core.TickCount<deadline,"Assassin SC input timeout");var s=ClassCombat.For(p);
                if(s.PendingAbility==ability&&!began)
                {Require(s.Timing==(quick?new ActionTiming(15,8):new ActionTiming(17,9))&&!Effect(p,ability)&&NecromancerEffects.ManaBalance256(p)==mana-(quick?cost:0),"Wire SC/start transaction");began=true;Log.Information("{Label}: windup",label);}
                if(!Effect(p,ability)){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Wire once-only release/payment");finished=true;Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){finished=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Assassin,0,p.X,p.Y,p.Z);Inspect();}));
    }
}
