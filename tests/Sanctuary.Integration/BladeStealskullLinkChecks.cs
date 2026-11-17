// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BladeStealskullLinkChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeStealskullLinkChecks));
    private static readonly List<object> Reports=[];
    private static Scene Pending;
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(string name in new[]{"onehand","twohand","onehand-immune","twohand-immune"})
        {
            string label="sentinel-stealskull-"+name;
            Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>
            {Require(Pending==null,"Previous Sentinel scene still active");Pending=new(label,name.StartsWith("twohand"),name.EndsWith("immune"));Pending.Warlock(n);}));
            Register(label+"-assassin-check",()=>FixtureNetwork.Connected(label+"-result",n=>
            {Require(Pending?.Label==label,"Missing matching Warlock scene");Pending.Assassin(n);}));
        }
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Two actual players:Stealskull learned Sentinel capture and Health Link",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Pending?.Clean();Log.Error(e,"{Name}: failed",name);}}));
    private sealed class Scene(string label,bool twoHanded,bool immune)
    {
        internal string Label=>label;
        private PlayerMobile W,A;
        private Account Wa,Aa;
        private WarlockDefilerBody Defiler;
        private BladeSentinelBody Blade;
        private GoatmanNpcContactChecks.Recipient Primary,Receiver;
        private Item Helm;
        private int Mana,Cost,PrimaryLife,ReceiverLife,BladeLife,VictimMana;
        private bool Began,Wounded,Observed,Done;
        private long Deadline;
        private Func<int,int> Block;
        internal void Warlock(NetState network)
        {
            W=ClassSkillChecks.Player(DiabloClass.Warlock);W.Name="SacrificeAllocationFixture";W.AddItem(new Backpack());
            ClassSkillChecks.Train(W,"warlock.summondefiler",20);ClassSkillChecks.Train(W,"warlock.demonicmastery",10);
            FixtureSpace.Corridor(W,6560,25,3100);Wa=FixtureNetwork.Authenticate(network,W,"sentinel-link-warlock-");
            Defiler=WarlockDefilerBody.CreatePrepared(W,new(W.X+7,W.Y+2,W.Z),TimedEffects.Now.AddHours(1));
            Primary=Target(W,4);Receiver=Target(W,16);Primary.SetResistance(ResistanceType.Physical,immune?100:0);Receiver.SetResistance(ResistanceType.Physical,40);
            Require(DefilerHealthLink.Cast(Defiler,Primary,TimedEffects.Now)&&Defiler.Prepared.Links.Marks.Length==2,"Actual Defiler two-recipient group");
            Log.Information("{Label}: warlock-ready",label);
        }
        private static GoatmanNpcContactChecks.Recipient Target(PlayerMobile owner,int dx)
        {
            var t=new GoatmanNpcContactChecks.Recipient{NoKillAwards=true,Frozen=true,CantWalk=true};t.SetHits(50000);t.Hits=t.HitsMax;t.SetMana(100);t.Mana=t.ManaMax;
            foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}
            t.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
        }
        internal void Assassin(NetState network)
        {
            A=ClassSkillChecks.Player(DiabloClass.Assassin);A.Name="SacrificeAllocationFixture";A.AddItem(new Backpack());
            while(A.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(A,CharacterStat.Strength),"Stealskull Strength");}
            while(A.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(A,CharacterStat.Dexterity),"Blade Dexterity");}
            A.MoveToWorld(new(W.X+3,W.Y,W.Z),W.Map);
            BaseWeapon weapon=twoHanded?new Spear():new Tekagi();ClassGear.Profiles.Convert(weapon,99,GearRarity.Normal,new Random(173));A.AddToBackpack(weapon);
            Require(A.EquipItem(weapon),"Actual blade weapon equip");Helm=NamedGear.Create("unique/203",99,new Random(173),true);A.AddToBackpack(Helm);Require(A.EquipItem(Helm),"Actual admitted Stealskull equip");ClassGear.Refresh(A);
            Require(WeaponHands.TwoHanded(weapon)==twoHanded&&ClassGear.Bonus(A,"life-steal%")==5&&ClassGear.Bonus(A,"mana-steal%")==5,"Original Stealskull/weapon source stats");
            Require(SkillBindings.Instance.Assign(A,false,CombatAbility.BladeSentinel)&&SkillBindings.Instance.AssignQuick(A,7,CombatAbility.BladeSentinel),"Actual learned Sentinel bindings");
            Aa=FixtureNetwork.Authenticate(network,A,"sentinel-link-assassin-");ClassCombat.Stop(A);ClassCombat.For(A).NextAction=ClassCombat.For(A).RecoveryUntil=Core.TickCount;
            Mana=NecromancerEffects.ManaBalance256(A);Cost=SkillMechanics.Values(A,CombatAbility.BladeSentinel).Mana256;PrimaryLife=SkillHealth.Balance256(Primary);ReceiverLife=SkillHealth.Balance256(Receiver);VictimMana=NecromancerEffects.ManaBalance256(Primary);
            Block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=n=>n-1;SkillDamageEvents.BeforeDirectDamage+=Hit;Deadline=Core.TickCount+12000;
            Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>
            {Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)CombatAbility.BladeSentinel,(int)DiabloClass.Assassin,A.X+4,A.Y,A.Z);Observe();});
        }
        private void Observe()
        {
            if(Done||Observed){return;}
            try
            {
                Require(Core.TickCount<Deadline,"Natural Stealskull Sentinel impact timeout");
                if(!Began&&ClassCombat.For(A).PendingAbility==CombatAbility.BladeSentinel){Began=true;Log.Information("{Label}: windup",label);}
                Blade??=AssassinTrapRoster.For(A).Select(r=>World.FindMobile((Serial)r.Actor)).OfType<BladeSentinelBody>().SingleOrDefault();
                if(Blade!=null&&!Wounded){SkillHealth.SetBalance256(Blade,SkillHealth.Maximum256(Blade)-17);BladeLife=SkillHealth.Balance256(Blade);Wounded=true;}
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);
            }
            catch(Exception e){Clean();Log.Error(e,"{Label}-result: failed",label);}
        }
        private void Hit(SkillDamageContact contact)
        {
            if(Done||Observed||contact.Source!=Blade||contact.Target!=Primary){return;}Observed=true;
            try
            {
                Require(Began&&Wounded&&W.NetState is{Running:true}&&A.NetState is{Running:true}&&W.NetState!=A.NetState,"Two real players and natural paid body required");
                var missile=BladeSentinelMissiles.Snapshot().Missiles.Single(m=>m.Owner==Blade.Serial.Value);int drain=twoHanded?1:3;
                Require(missile.Damage.Elements.LifeDrain==drain&&missile.Damage.Elements.ManaDrain==drain,"Actual Stealskull first-missile capture/scaling");
                ulong seed=missile.Seed;int Roll(int n)=>(int)SourceRandom.Limited(ref seed,n);
                var raw=missile.Damage.Roll(Primary,Roll,out _);var settled=SkillCombat.MitigatePrepared(Blade,Primary,raw with{Poison=0});var known=DirectDamageParts.From(settled);
                int direct=checked(NamedElements.To256(settled.Total)+drain),percent=Defiler.Prepared.Links.Marks.Single(m=>m.Target==Primary.Serial.Value).Percent;
                int shared=NamedElements.To256(SkillCombat.MitigatePrepared(Defiler,Receiver,known.Fraction(percent).Gear).Total);
                var recovery=WeaponLeech.Nonplayer(NamedElements.To256(settled.Physical),PrimaryLife,VictimMana,SkillHealth.Maximum256(Blade)-BladeLife,drain,drain,GemCombat.Drain(Primary));
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>
                {
                    try
                    {
                        var report=new{Label=label,TwoHanded=twoHanded,Immune=immune,LifeDrain=drain,ManaDrain=drain,Direct=direct,PrimaryLoss=PrimaryLife-SkillHealth.Balance256(Primary),Known=known,Shared=shared,SecondaryLoss=ReceiverLife-SkillHealth.Balance256(Receiver),Typed=contact.Parts!=null,Fault=Defiler.Prepared.Links.Fault};
                        Reports.Add(report);File.WriteAllText(Path.Join(Core.BaseDirectory,"sentinel-stealskull-results.json"),JsonSerializer.Serialize(Reports));
                        Log.Information("{Label}: impact drain={Drain},primary={Primary}/{Expected},shared={Secondary}/{Shared},typed={Typed},fault={Fault}",label,drain,report.PrimaryLoss,direct,report.SecondaryLoss,shared,report.Typed,report.Fault);
                        Require(report.Fault==null&&contact.Parts==known&&contact.Damage256==direct&&report.PrimaryLoss==direct&&report.SecondaryLoss==shared,"Stealskull Sentinel lost typed sharing or changed flat-drain primary damage");
                        Require(SkillHealth.Balance256(Blade)==BladeLife+recovery.Life256&&NecromancerEffects.ManaBalance256(Primary)==recovery.VictimMana256&&NecromancerEffects.ManaBalance256(A)==Mana-Cost,"Sentinel NPC recovery/target mana/once-only cast cost");
                        Clean();Log.Information("{Label}-result: passed",label);
                    }
                    catch(Exception e){Clean();Log.Error(e,"{Label}-result: failed",label);}
                });
            }
            catch(Exception e){Clean();Log.Error(e,"{Label}-result: failed",label);}
        }
        internal void Clean()
        {
            if(Done){return;}Done=true;SkillDamageEvents.BeforeDirectDamage-=Hit;if(Block!=null){SourceBlocking.BlockRoll=Block;}
            Blade?.Delete();Defiler?.Delete();Primary?.Delete();Receiver?.Delete();A?.Delete();W?.Delete();Aa?.Delete();Wa?.Delete();Pending=null;
        }
    }
}
