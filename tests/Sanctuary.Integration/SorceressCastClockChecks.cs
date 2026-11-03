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

public static class SorceressCastClockChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SorceressCastClockChecks));
    private static bool _creating;
    private static void Freeze()
    {var mana=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>_creating||p.Name=="SorceressCastClockFixture"?TimeSpan.FromHours(1):mana?.Invoke(p)??Mobile.DefaultManaRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Freeze();
    public static void Configure()
    {
        Freeze();Register("sorceress-cast-clock-check",Check);
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;string label=quick?"sorceress-cast-clock-quick":"sorceress-cast-clock-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,captured)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Sorceress SC clocks, ordinary starts and separate Teleport controller",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        PlayerMobile p;_creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Sorceress);}finally{_creating=false;}p.Name="SorceressCastClockFixture";
        if(p.Backpack==null){p.AddItem(new Backpack());}
        while(p.RawStr<50){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Real Magefist Strength");}
        FixtureSpace.Corridor(p,6200,26,3600);Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,100*256+17);}
    private static void Magefist(PlayerMobile p)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Magefist").Id,99,new Random(173),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Actual Magefist equip");ClassGear.Refresh(p);}
    private static Point3D Aim(PlayerMobile p)=>new(p.X+4,p.Y,p.Z);
    private static bool Effect(PlayerMobile p,CombatAbility ability,Point3D origin)=>ability switch
    {
        CombatAbility.FrozenArmor=>SorceressEffects.Armor(p)?.Ability==ability,
        CombatAbility.FireBolt=>SorceressSpellMissiles.Snapshot().Flights.Any(f=>f.Owner==p.Serial.Value&&f.Ability==ability),
        CombatAbility.Teleport=>p.Location!=origin,
        _=>false
    };
    private static void Clean(PlayerMobile p,Mobile target=null)
    {ClassCombat.Stop(p);SorceressEffects.Profiles.Clear(p);target?.Delete();p.Delete();}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"sorceress-cast-clock-source-probe.json")));
        int comparisons=0,routes=0,ownerRoutes=0,starts=0,casts=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {var t=SorceressCastClock.Calculate(r[1].GetInt32());Require(t.EffectFrame==r[3][0][1].GetInt32()&&t.Frames==r[3][1][1].GetInt32(),"Complete source Sorceress SC differential");comparisons++;}
        var skills=SkillMechanics.All.Where(r=>SorceressCastClock.Handles(r.Ability)).ToArray();
        Require(comparisons==2072&&skills.Length==23&&skills.Any(r=>r.Ability==CombatAbility.Teleport),"SC route coverage or missing separate Teleport");
        foreach(var ability in new[]{CombatAbility.Lightning,CombatAbility.ChainLightning,CombatAbility.Inferno,CombatAbility.Warmth})
        {Require(!SorceressCastClock.Handles(ability),"Sequence/passive was redirected to ordinary SC");}
        foreach(var skill in skills)for(int i=0;i<259;i++)
        {
            int fcr=i<256?i:i==256?500:i==257?1000:10000;
            Require(CombatTiming.Calculate(DiabloClass.Sorceress,skill.Ability,new(AttackFamily.Bow,-20),90,fcr,85,70)==SorceressCastClock.Calculate(fcr),"SC leaked weapon/IAS/chill/attack rate");routes++;
        }
        foreach(bool boosted in new[]{false,true})
        {
            var p=Owner();var enemy=PoisonDaggerChecks.Target(p);
            try
            {
                var weaponClock=CombatTiming.For(p,CombatAbility.BasicAttack);if(boosted){Magefist(p);}var expected=SorceressCastClock.Calculate(boosted?20:0);
                Require(CombatTiming.Fcr(p)==(boosted?20:0)&&CombatTiming.For(p,CombatAbility.BasicAttack)==weaponClock&&!ClassCombat.Available(p,CombatAbility.Inferno),"Actual FCR/weapon clock/Inferno gate");
                foreach(var ability in new[]{CombatAbility.Lightning,CombatAbility.ChainLightning})
                {Require(CombatTiming.For(p,ability)==(boosted?new ActionTiming(17,6):new ActionTiming(19,7)),"Separate SQ12 clock changed");}
                foreach(var skill in skills)
                {
                    Ready(p);int mana=NecromancerEffects.ManaBalance256(p);var origin=p.Location;
                    Require(CombatTiming.For(p,skill.Ability)==expected,"Actual owner SC route");ownerRoutes++;
                    long at=Core.TickCount;
                    Require(ClassCombat.Perform(p,skill.Ability,skill.Ability==CombatAbility.Telekinesis?enemy:null,Aim(p),at),"Learned SC start: "+skill.Ability);var s=ClassCombat.For(p);
                    Require(s.Timing==expected&&s.EffectAt==at+expected.EffectMs&&s.NextAction==at+expected.DurationMs&&NecromancerEffects.ManaBalance256(p)==mana,"SC windup clock/payment: "+skill.Ability);
                    long effect=s.EffectAt;ClassCombat.Stop(p);
                    Require(!ClassCombat.ResolveAction(p,effect)&&p.Location==origin&&NecromancerEffects.ManaBalance256(p)==mana&&s.PendingTeleport==null,"Cancelled SC start moved/paid/replayed");starts++;
                }
                foreach(var ability in new[]{CombatAbility.FireBolt,CombatAbility.FrozenArmor,CombatAbility.Teleport})
                {
                    SorceressEffects.Profiles.Clear(p);Ready(p);var origin=p.Location;var aim=Aim(p);
                    int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;long at=Core.TickCount;
                    Require(ClassCombat.Perform(p,ability,null,aim,at),"Actual SC effect start");var s=ClassCombat.For(p);
                    Require(s.Timing==expected&&!Effect(p,ability,origin)&&NecromancerEffects.ManaBalance256(p)==mana,"Early SC effect/payment");
                    Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&ClassCombat.ResolveAction(p,s.EffectAt)&&!ClassCombat.ResolveAction(p,s.EffectAt),"Early/duplicate SC release");
                    Require(Effect(p,ability,origin)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&(ability!=CombatAbility.Teleport||p.Location==aim),"SC actual effect/payment");
                    Require(!ClassCombat.Perform(p,ability,null,Aim(p),s.NextAction-1),"SC animation tail reentry");casts++;
                }
            }
            finally{Clean(p,enemy);}
        }
        Require(routes==5957&&ownerRoutes==46&&starts==46&&casts==6,"Missing Sorceress clock coverage");
        Log.Information("Sorceress SC clock:2072source comparisons,5957static clocks,46actual owner/start/cancel routes,6gear/effect casts;separate Teleport,unpaid windup,once release and SQ12/Inferno/weapon exclusions pass");
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();if(quick){Magefist(p);}var ability=quick?CombatAbility.Teleport:CombatAbility.FrozenArmor;
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Actual Sorceress bindings");
        var account=FixtureNetwork.Authenticate(network,p,"sorceress-clock-");Ready(p);var origin=p.Location;var aim=Aim(p);
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;bool began=false,finished=false;
        string label=quick?"sorceress-cast-clock-quick":"sorceress-cast-clock-right";long deadline=Core.TickCount+10000;
        void Cleanup(){Clean(p);account.Delete();}
        void Inspect()
        {
            if(finished){return;}
            try
            {
                Require(Core.TickCount<deadline,"Sorceress SC input timeout");var s=ClassCombat.For(p);
                if(s.PendingAbility==ability&&!began)
                {Require(s.Timing==(quick?new ActionTiming(12,6):new ActionTiming(14,7))&&!Effect(p,ability,origin)&&NecromancerEffects.ManaBalance256(p)==mana,"Wire SC/windup payment");began=true;Log.Information("{Label}: windup",label);}
                if(!Effect(p,ability,origin)){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-cost&&(!quick||p.Location==aim),"Wire once-only effect/payment");finished=true;Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){finished=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Sorceress,0,aim.X,aim.Y,aim.Z);Inspect();}));
    }
}
