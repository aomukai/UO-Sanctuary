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

public static class PaladinCastClockChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PaladinCastClockChecks));
    private static bool _creating;
    private static void Freeze()
    {
        var mana=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>_creating||p.Name=="PaladinCastClockFixture"?TimeSpan.FromHours(1):mana?.Invoke(p)??Mobile.DefaultManaRate;
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Freeze();
    public static void Configure()
    {
        Freeze();Register("paladin-cast-clock-check",Check);
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;string label=quick?"paladin-cast-clock-quick":"paladin-cast-clock-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,captured)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Paladin SC clocks with real gear, payment phases and input",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        PlayerMobile p;_creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Paladin);}finally{_creating=false;}p.Name="PaladinCastClockFixture";
        if(p.Backpack==null){p.AddItem(new Backpack());}
        while(p.RawStr<50){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Real cast gear Strength");}
        var shield=new WoodenKiteShield();ClassGear.Profiles.Convert(shield,99,GearRarity.Normal,new Random(173));
        Require(p.EquipItem(shield)&&HolyShields.Shield(p)==shield,"Real Holy Shield equipment");
        FixtureSpace.Corridor(p,6200,26,3600);Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,100*256+17);}
    private static void Magefist(PlayerMobile p)
    {
        var gloves=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Magefist").Id,99,new Random(173),true);
        p.AddToBackpack(gloves);Require(p.EquipItem(gloves),"Real Magefist equip");ClassGear.Refresh(p);
    }
    private static void ClearEffects(PlayerMobile p)
    {HolyShields.Clear(p);HolyBoltFlights.Cancel(p);BlessedHammerFlights.Cancel(p);HeavenFists.Cancel(p);}
    private static void Clean(PlayerMobile p,Mobile target=null)
    {ClassCombat.Stop(p);ClearEffects(p);target?.Delete();p.Delete();}
    private static bool Effect(PlayerMobile p,CombatAbility ability)=>ability switch
    {
        CombatAbility.HolyShield=>HolyShields.State(p)!=null,
        CombatAbility.HolyBolt=>HolyBolts.Snapshot().Any(f=>f.Owner==p.Serial.Value),
        CombatAbility.BlessedHammer=>BlessedHammers.Snapshot().Any(f=>f.Owner==p.Serial.Value),
        CombatAbility.FistOfTheHeavens=>HeavenFists.Snapshot().Any(f=>f.Owner==p.Serial.Value),
        _=>false
    };
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"paladin-cast-clock-source-probe.json")));
        int comparisons=0,routes=0,ownerRoutes=0,casts=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            var t=PaladinCastClock.Calculate(r[1].GetInt32());
            Require(t.EffectFrame==r[3][0][1].GetInt32()&&t.Frames==r[3][1][1].GetInt32(),"Full source Paladin SC differential");comparisons++;
        }
        var skills=SkillMechanics.All.Where(r=>PaladinCastClock.Handles(r.Ability)).ToArray();
        Require(comparisons==2072&&skills.Length==4&&!PaladinCastClock.Handles(CombatAbility.Smite)&&!PaladinCastClock.Handles(CombatAbility.Prayer),"SC source/routes/exclusions");
        foreach(var skill in skills)for(int i=0;i<259;i++)
        {
            int fcr=i<256?i:i==256?500:i==257?1000:10000;
            Require(CombatTiming.Calculate(DiabloClass.Paladin,skill.Ability,new(AttackFamily.Bow,-20),90,fcr,85,70)==PaladinCastClock.Calculate(fcr),"SC leaked weapon/IAS/chill/attack rate");routes++;
        }
        foreach(bool boosted in new[]{false,true})
        {
            var p=Owner();var enemy=PoisonDaggerChecks.Target(p);
            try
            {
                var weaponClock=CombatTiming.For(p,CombatAbility.BasicAttack);
                if(boosted){Magefist(p);}int fcr=boosted?20:0;var expected=PaladinCastClock.Calculate(fcr);
                Require(CombatTiming.Fcr(p)==fcr&&CombatTiming.For(p,CombatAbility.BasicAttack)==weaponClock,"Real FCR or unrelated weapon clock changed");
                foreach(var skill in skills)
                {
                    var ability=skill.Ability;bool paidAtStart=ability==CombatAbility.HolyShield;
                    ClearEffects(p);Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
                    Require(CombatTiming.For(p,ability)==expected,"Actual owner SC route");ownerRoutes++;
                    bool targeted=ability is CombatAbility.HolyBolt or CombatAbility.FistOfTheHeavens;
                    var point=targeted?enemy.Location:p.Location;long at=Core.TickCount;
                    Require(ClassCombat.Perform(p,ability,targeted?enemy:null,point,at),"Actual learned SC start: "+ability);
                    var s=ClassCombat.For(p);
                    Require(s.Timing==expected&&s.EffectAt==at+expected.EffectMs&&s.NextAction==at+expected.DurationMs
                        &&!Effect(p,ability)&&NecromancerEffects.ManaBalance256(p)==mana-(paidAtStart?cost:0),"SC timing/start payment: "+ability);
                    Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&ClassCombat.ResolveAction(p,s.EffectAt)&&!ClassCombat.ResolveAction(p,s.EffectAt),"Early/duplicate SC release: "+ability);
                    Require(Effect(p,ability)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Actual SC effect/once payment: "+ability);
                    Require(!ClassCombat.Perform(p,ability,targeted?enemy:null,point,s.NextAction-1)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"SC tail admitted/refunded cast");casts++;
                }
                foreach(var ability in new[]{CombatAbility.HolyShield,CombatAbility.HolyBolt})
                {
                    ClearEffects(p);Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
                    Require(ClassCombat.Perform(p,ability,ability==CombatAbility.HolyBolt?enemy:null,enemy.Location,Core.TickCount),"Cancel start");long effect=ClassCombat.For(p).EffectAt;
                    ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,effect)&&!Effect(p,ability)
                        &&NecromancerEffects.ManaBalance256(p)==mana-(ability==CombatAbility.HolyShield?cost:0),"Cancellation changed original payment phase");
                }
            }
            finally{Clean(p,enemy);}
        }
        Require(routes==1036&&ownerRoutes==8&&casts==8,"Missing Paladin clock coverage");
        Log.Information("Paladin SC clock:2072source comparisons,1036static clocks,8actual owner routes/gear/effect casts;Holy Shield pays on start,other3on release;4cancellation controls and weapon/aura exclusions pass");
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();if(quick){Magefist(p);}var ability=quick?CombatAbility.BlessedHammer:CombatAbility.HolyShield;
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Actual Paladin bindings");
        var account=FixtureNetwork.Authenticate(network,p,"paladin-clock-");Ready(p);
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;bool began=false,finished=false;
        string label=quick?"paladin-cast-clock-quick":"paladin-cast-clock-right";long deadline=Core.TickCount+10000;
        void Cleanup(){Clean(p);account.Delete();}
        void Inspect()
        {
            if(finished){return;}
            try
            {
                Require(Core.TickCount<deadline,"Paladin SC input timeout");var s=ClassCombat.For(p);
                if(s.PendingAbility==ability&&!began)
                {
                    Require(s.Timing==(quick?new ActionTiming(14,8):new ActionTiming(16,9))&&!Effect(p,ability)
                        &&NecromancerEffects.ManaBalance256(p)==mana-(quick?0:cost),"Wire timing/start payment");
                    began=true;Log.Information("{Label}: windup",label);
                }
                if(!Effect(p,ability)){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Wire once-only release/payment");
                finished=true;ClassCombat.Stop(p);Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){finished=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Paladin,0,p.X,p.Y,p.Z);Inspect();}));
    }
}
