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

public static class DruidHumanCastClockChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidHumanCastClockChecks));
    private static bool _creating;
    private static void Freeze()
    {var mana=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>_creating||p.Name=="DruidHumanClockFixture"?TimeSpan.FromHours(1):mana?.Invoke(p)??Mobile.DefaultManaRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Freeze();
    public static void Configure()
    {
        Freeze();Register("druid-human-cast-clock-check",Check);
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;string label=quick?"druid-human-cast-clock-quick":"druid-human-cast-clock-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,captured)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Druid human SC routes reuse the existing human/form clock",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        PlayerMobile p;_creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Druid);}finally{_creating=false;}p.Name="DruidHumanClockFixture";
        if(p.Backpack==null){p.AddItem(new Backpack());}
        while(p.RawStr<50){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Actual Magefist Strength");}
        FixtureSpace.Corridor(p,6200,26,3600);Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,100*256+17);}
    private static void Magefist(PlayerMobile p)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Magefist").Id,99,new Random(173),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Actual Magefist equip");ClassGear.Refresh(p);}
    private static Point3D Aim(PlayerMobile p)=>new(p.X+4,p.Y,p.Z);
    private static void Clear(PlayerMobile p)
    {DruidFirestorm.Cancel(p);DruidCycloneArmor.Clear(p);foreach(var bird in DruidRavens.For(p)){bird.Delete();}}
    private static void Clean(PlayerMobile p)
    {ClassCombat.Stop(p);Clear(p);DruidPlayerForms.Clear(p);p.Delete();}
    private static bool Effect(PlayerMobile p,CombatAbility ability)=>ability switch
    {
        CombatAbility.CycloneArmor=>DruidCycloneArmor.State(p)!=null,
        CombatAbility.Firestorm=>DruidFirestorm.Snapshot().Emitters.Any(f=>f.Owner==p.Serial.Value),
        CombatAbility.Raven=>DruidRavens.For(p).Length==1,
        _=>false
    };
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-form-cast-clock-source-probe.json")));
        int comparisons=0,human=0,routes=0,starts=0,casts=0,formRoutes=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            int index=r[0].GetInt32(),fcr=r[1].GetInt32();var t=index==0?DruidFormCastClock.Calculate(CombatAbility.Werewolf,fcr):index==9?DruidFormCastClock.Calculate(CombatAbility.Werebear,fcr):DruidHumanCastClock.Calculate(fcr);
            Require(t.EffectFrame==r[3][0][1].GetInt32()&&t.Frames==r[3][1][1].GetInt32(),"Existing complete-source human/form differential");comparisons++;if(index is>0 and<9){human++;}
        }
        var skills=SkillMechanics.All.Where(r=>DruidHumanCastClock.Handles(r.Ability)).ToArray();
        Require(comparisons==2590&&human==2072&&skills.Length==18,"Human SC/source scope");
        foreach(var ability in new[]{CombatAbility.ShockWave,CombatAbility.PoisonCreeper,CombatAbility.CarrionVine,CombatAbility.SolarCreeper,CombatAbility.ArcticBlast,CombatAbility.Maul,CombatAbility.Lycanthropy})
        {Require(!DruidHumanCastClock.Handles(ability),"Human clock captured a separate/gated route");}
        foreach(var skill in skills)for(int i=0;i<259;i++)
        {
            int fcr=i<256?i:i==256?500:i==257?1000:10000;
            Require(CombatTiming.Calculate(DiabloClass.Druid,skill.Ability,new(AttackFamily.Bow,-20),90,fcr,85,70)==DruidHumanCastClock.Calculate(fcr),"Human SC leaked IAS/weapon/chill/attack rate");routes++;
        }
        foreach(bool boosted in new[]{false,true})
        {
            var p=Owner();try
            {
                var weapon=CombatTiming.For(p,CombatAbility.BasicAttack);if(boosted){Magefist(p);}int fcr=boosted?20:0;var expected=DruidHumanCastClock.Calculate(fcr);
                Require(CombatTiming.Fcr(p)==fcr&&CombatTiming.For(p,CombatAbility.BasicAttack)==weapon,"Actual FCR/weapon clock");
                foreach(var ability in new[]{CombatAbility.PoisonCreeper,CombatAbility.CarrionVine,CombatAbility.SolarCreeper,CombatAbility.ArcticBlast,CombatAbility.ShockWave})
                {Require(!ClassCombat.Available(p,ability),"Human timing enabled a gated/form-only skill");}
                foreach(var skill in skills)
                {
                    Ready(p);int mana=NecromancerEffects.ManaBalance256(p);long at=Core.TickCount;
                    Require(CombatTiming.For(p,skill.Ability)==expected&&ClassCombat.Perform(p,skill.Ability,null,Aim(p),at),"Actual human SC start: "+skill.Ability);var s=ClassCombat.For(p);
                    Require(s.Timing==expected&&s.EffectAt==at+expected.EffectMs&&s.NextAction==at+expected.DurationMs&&NecromancerEffects.ManaBalance256(p)==mana,"Human start clock/payment");
                    long effect=s.EffectAt;ClassCombat.Stop(p);
                    Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana&&DruidPlayerForms.State(p)==null&&s.Transform==null,"Cancelled human cast paid/transformed/replayed");starts++;
                }
                foreach(var ability in new[]{CombatAbility.CycloneArmor,CombatAbility.Firestorm,CombatAbility.Raven})
                {
                    Clear(p);Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
                    Require(ClassCombat.Perform(p,ability,null,Aim(p),Core.TickCount),"Human effect start");var s=ClassCombat.For(p);
                    Require(!Effect(p,ability)&&!ClassCombat.ResolveAction(p,s.EffectAt-1)&&ClassCombat.ResolveAction(p,s.EffectAt)&&!ClassCombat.ResolveAction(p,s.EffectAt),"Early/duplicate human SC effect");
                    Require(Effect(p,ability)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Actual human SC effect/payment");
                    Require(!ClassCombat.Perform(p,ability,null,Aim(p),s.NextAction-1),"Human SC tail reentry");casts++;
                }
                Clear(p);
                foreach(var form in new[]{CombatAbility.Werewolf,CombatAbility.Werebear})
                {
                    Require(DruidPlayerForms.ApplyPrepared(p,form,TimedEffects.Now),"Prepared retained form setup");
                    Require(CombatTiming.For(p,CombatAbility.Armageddon)==DruidFormCastClock.Calculate(form,fcr),"Human override captured an active form clock");formRoutes++;DruidPlayerForms.Clear(p);
                }
            }
            finally{Clean(p);}
        }
        Require(routes==4662&&starts==36&&casts==6&&formRoutes==4,"Missing human clock coverage");
        Log.Information("Druid human SC:2590retained human/form source comparisons,4662static clocks,36actual start/cancel routes,6gear/effect casts,4active form exclusions;prepared vines/Shock Wave/Arctic/weapon boundaries retain");
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();if(quick){Magefist(p);}var ability=quick?CombatAbility.Raven:CombatAbility.CycloneArmor;
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Actual Druid bindings");
        var account=FixtureNetwork.Authenticate(network,p,"druid-human-clock-");Ready(p);var aim=Aim(p);
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;bool began=false,finished=false;
        string label=quick?"druid-human-cast-clock-quick":"druid-human-cast-clock-right";long deadline=Core.TickCount+10000;
        void Cleanup(){Clean(p);account.Delete();}
        void Inspect()
        {
            if(finished){return;}
            try
            {
                Require(Core.TickCount<deadline,"Druid human SC input timeout");var s=ClassCombat.For(p);
                if(s.PendingAbility==ability&&!began)
                {Require(s.Timing==(quick?new ActionTiming(16,9):new ActionTiming(19,10))&&!Effect(p,ability)&&NecromancerEffects.ManaBalance256(p)==mana,"Wire human SC/windup payment");began=true;Log.Information("{Label}: windup",label);}
                if(!Effect(p,ability)){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Wire human SC once-only effect/payment");finished=true;Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){finished=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Druid,0,aim.X,aim.Y,aim.Z);Inspect();}));
    }
}
