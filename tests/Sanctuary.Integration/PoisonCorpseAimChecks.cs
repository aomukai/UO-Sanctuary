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
using Server.Regions;
namespace Sanctuary.Integration;

public static class PoisonCorpseAimChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PoisonCorpseAimChecks));
    private sealed record Saved(uint Owner,uint Corpse,CorpseSkillRecord Row,Point3D Origin,int Map,int Mana,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"poison-corpse-aim-fixtures.json");
    private const CombatAbility Ability=CombatAbility.PoisonExplosion;
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("poison-corpse-aim-check",Check);Register("poison-corpse-aim-reload-check",Reload);
        foreach(bool quick in new[]{false,true})
        {bool q=quick;string label=q?"poison-ground-quick":"poison-ground-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label,q)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Poison Explosion shared corpse picker and conservation",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner(int row=2700)
    {
        var owner=PoisonDaggerChecks.Owner();owner.Name="PoisonDaggerFixtureGroundCloud";ClassSkillChecks.Train(owner,"necromancer.poisonexplosion");
        FixtureSpace.Corridor(owner,7000,24,row);Require(SkillBindings.Instance.Assign(owner,false,Ability)&&SkillBindings.Instance.AssignQuick(owner,7,Ability),"Poison ground bindings");Ready(owner);return owner;
    }
    private static void Ready(PlayerMobile owner)
    {PoisonDaggerChecks.Ready(owner);ClassCombat.For(owner).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(owner,owner.ManaMax*256);}
    private static Corpse Body(PlayerMobile owner,int dx=5,int dy=0)
    {var corpse=DeathSentryExplosionChecks.Body(owner,new(owner.X+dx,owner.Y+dy,owner.Z));corpse.Owner?.Delete();return corpse;}
    private static Corpse Pick(PlayerMobile owner,Point3D cursor)=>CorpseCastTargets.Acquire(owner,Ability,cursor);
    private static NecromancerSpellFlight[] Clouds(PlayerMobile owner)=>NecromancerSpellMissiles.Snapshot().Flights.Where(x=>x.Owner==owner.Serial.Value).ToArray();
    private static void Cast(PlayerMobile owner,Corpse corpse)
    {
        Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner);var row=CorpseSkillRules.Profiles.Find(corpse);
        Require(ClassCombat.BeginCorpse(owner,Ability,Serial.Zero,new(corpse.X+1,corpse.Y,corpse.Z),Core.TickCount),"Poison ground start");var session=ClassCombat.For(owner);long at=session.EffectAt;
        Require(session.PendingTarget==corpse.Serial&&session.PendingPoint==corpse.Location&&!corpse.SanctuaryConsumed&&CorpseSkillRules.Profiles.Find(corpse)==row
            &&NecromancerEffects.ManaBalance256(owner)==mana&&Clouds(owner).Length==0,"Poison acquisition consumed,emitted or paid early");
        Require(!ClassCombat.BeginCorpse(owner,Ability,Serial.Zero,owner.Location,Core.TickCount)&&session.PendingTarget==corpse.Serial&&session.EffectAt==at,"Repeated input replaced acquired corpse");
        Require(ClassCombat.ResolveAction(owner,at)&&!ClassCombat.ResolveAction(owner,at)&&corpse.SanctuaryConsumed&&CorpseSkillRules.NoSelect(corpse)
            &&corpse.GetAmount(typeof(Gold))==1&&CorpseSkillRules.Profiles.Find(corpse)==row with{NoSelect=true}
            &&NecromancerEffects.ManaBalance256(owner)==mana-8*256&&Clouds(owner).Length==8
            &&Clouds(owner).All(x=>x.X==corpse.X&&x.Y==corpse.Y&&x.Ability==Ability),"Poison ground release changed loot,seed,payment or cloud origin");
        NecromancerSpellMissiles.Cancel(owner);
    }
    private static void Save(PlayerMobile owner,int generation)
    {
        Ready(owner);var corpse=Body(owner);corpse.BeginDecay(FixtureSpace.Retention);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,corpse.Serial.Value,CorpseSkillRules.Profiles.Find(corpse),owner.Location,owner.Map.MapID,NecromancerEffects.ManaBalance256(owner),generation)));owner.Internalize();
    }
    private static void Check()
    {
        var owner=Owner();var a=Body(owner);var b=Body(owner,6,1);var tie=new Point3D(owner.X+6,owner.Y,owner.Z);var row=CorpseSkillRules.Profiles.Find(a);
        try
        {
            Require(Pick(owner,new(b.X,b.Y+1,b.Z))==b&&Pick(owner,tie)==(a.Serial.Value<b.Serial.Value?a:b),"Poison cursor-nearest/serial tie differs");
            a.SanctuaryRestInPeace=true;Require(Pick(owner,tie)==b,"Poison RIP corpse blocked next usable corpse");a.SanctuaryRestInPeace=false;
            foreach(var bad in new[]{row with{NoSelect=true},row with{Shattered=true}})
            {CorpseSkillRules.Profiles.Store(bad);Require(Pick(owner,tie)==b,"Poison unusable corpse selected");}CorpseSkillRules.Profiles.Store(row);
            b.MoveToWorld(b.Location,Map.Felucca);Require(Pick(owner,new(a.X+2,a.Y,a.Z))==a&&Pick(owner,new(a.X+3,a.Y,a.Z))==null&&Pick(owner,new(a.X+2,a.Y+2,a.Z))==null,"Poison near-click circle differs");
            // Poison Explosion does not need CE's corpse-life RNG supplier.
            CorpseSkillRules.Profiles.Store(row with{BaseLife=null});Require(Pick(owner,a.Location)==a&&CorpseExplosionCasts.Acquire(owner,a.Location)==null,"Shared picker conflated CE/poison eligibility");CorpseSkillRules.Profiles.Store(row);
            var origin=a.Location;a.MoveToWorld(new(owner.X+9,owner.Y,owner.Z),owner.Map);Require(Pick(owner,a.Location)==null,"Poison picker expanded caster range");a.MoveToWorld(origin,owner.Map);
            var town=new GuardedRegion("PoisonGroundTown-"+owner.Serial.Value,owner.Map,1000,new Rectangle2D(owner.X-1,owner.Y-1,3,3));town.Register();try{Require(Pick(owner,a.Location)==null,"Poison picker bypassed caster town");}finally{town.Unregister();}
            Ready(owner);Require(ClassCombat.BeginCorpse(owner,Ability,Serial.Zero,a.Location,Core.TickCount),"Poison loss start");int mana=NecromancerEffects.ManaBalance256(owner);CorpseSkillRules.Reserve(a);
            Require(!ClassCombat.ResolveAction(owner,ClassCombat.For(owner).EffectAt)&&NecromancerEffects.ManaBalance256(owner)==mana&&Clouds(owner).Length==0&&!a.SanctuaryConsumed,"Lost corpse reacquired,paid or emitted");
            CorpseSkillRules.Profiles.Store(row);Ready(owner);Require(!ClassCombat.BeginCorpse(owner,Ability,Serial.Zero,owner.Location,Core.TickCount),"Distant empty poison aim cast");Cast(owner,a);a.Delete();b.Delete();Save(owner,0);
        }
        catch{NecromancerSpellMissiles.Cancel(owner);a.Delete();b.Delete();owner.Delete();throw;}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var corpse=(Corpse)World.FindItem((Serial)saved.Corpse);owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);
        Require(corpse.Owner==null&&CorpseSkillRules.Profiles.Find(corpse)==saved.Row&&corpse.GetAmount(typeof(Gold))==1&&NecromancerEffects.ManaBalance256(owner)==saved.Mana,"Saved poison ground corpse,loot,seed or mana changed");
        Cast(owner,corpse);corpse.Delete();Save(owner,saved.Generation+1);
    }
    private static void Input(NetState net,string label,bool quick)
    {
        var owner=Owner(quick?2650:2600);var corpse=Body(owner);var unrelated=Body(owner,1);var row=CorpseSkillRules.Profiles.Find(corpse);var other=CorpseSkillRules.Profiles.Find(unrelated);
        var target=DeathSentryExplosionChecks.Target(corpse,1);target.Frozen=target.CantWalk=true;TimedEffects.PreventHealing(target,TimedEffects.Now);int mana=NecromancerEffects.ManaBalance256(owner);
        var account=FixtureNetwork.Authenticate(net,owner,"poison-ground-");bool began=false,finished=false;long deadline=Core.TickCount+10000;
        void End(){ClassCombat.Stop(owner);NecromancerSpellMissiles.Cancel(owner);target.Delete();corpse.Delete();unrelated.Delete();owner.Delete();account.Delete();}
        void Inspect()
        {
            if(finished){return;}
            try
            {
                Require(net.Running&&Core.TickCount<deadline,"Poison ground input timeout");var session=ClassCombat.For(owner);
                if(session.PendingAbility==Ability&&!began)
                {Require(session.PendingTarget==corpse.Serial&&NecromancerEffects.ManaBalance256(owner)==mana&&!corpse.SanctuaryConsumed&&Clouds(owner).Length==0,"Actual poison ground input lost primary or paid early");began=true;Log.Information("{Label}: windup",label);}
                if(TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==target.Serial.Value)?.SpellPoison!=null)
                {
                    Require(began&&corpse.SanctuaryConsumed&&NecromancerEffects.ManaBalance256(owner)==mana-8*256&&Clouds(owner).Length==8&&corpse.GetAmount(typeof(Gold))==1
                        &&CorpseSkillRules.Profiles.Find(corpse)==row with{NoSelect=true}&&!unrelated.SanctuaryConsumed&&CorpseSkillRules.Profiles.Find(unrelated)==other,"Actual poison ground input changed emission,loot,seed,payment or unrelated corpse");
                    finished=true;End();Log.Information("{Label}-result: passed",label);return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){finished=true;End();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Necromancer,corpse.X+1,corpse.Y,corpse.Z);Inspect();});
    }
}
