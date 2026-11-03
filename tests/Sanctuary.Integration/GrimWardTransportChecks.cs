// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GrimWardTransportChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GrimWardTransportChecks));
    private sealed record Saved(uint Owner,uint Target,uint Creator,NecromancerCurse Curse,int Life,int Mana,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"grim-ward-transport-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("grim-ward-transport-check",()=>Check(false));Register("grim-ward-transport-reload-check",()=>Check(true));
        ConsoleInputHandler.RegisterCommand("grim-ward-transport-live","Actual Ward escape and expiry",_=>Core.LoopContext.Post(()=>
        {try{Live();}catch(Exception e){Log.Error(e,"grim-ward-transport-live-result: failed");}}));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Ward actual recipient and saved curse",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static SkillValues Values(int rank,int hard)=>new(SkillMechanics.For(CombatAbility.GrimWard),
        k=>k=="Grim Ward"?rank:k=="Find Potion"?hard+7:0,k=>k=="Find Potion"?hard:0,80);
    private static Item Marker(PlayerMobile owner)
    {var item=new Static(0x1B7A){Name="Grim Ward missile identity fixture (placeholder)",Movable=false};item.MoveToWorld(owner.Location,owner.Map);return item;}
    private static void Check(bool reload)
    {
        Saved saved;PlayerMobile owner;BaseCreature target;
        if(reload)
        {
            saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
            owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;target=World.FindMobile((Serial)saved.Target) as BaseCreature;
            Require(owner!=null&&target!=null&&NecromancerEffects.Curse(target)==saved.Curse,"Actual world reload changed Ward payload/creator/AI");
            Require(World.FindItem((Serial)saved.Creator)==null&&GrimWardCurseTransport.Valid(saved.Curse),"Deleted missile identity must remain valid without resolving a live item");
            Require(target.Hits==saved.Life&&NecromancerEffects.ManaBalance256(owner)==saved.Mana,"Reload changed original wounds/resources");
            saved=saved with{Generation=saved.Generation+1};
        }
        else
        {
            owner=PoisonDaggerChecks.Owner();FixtureSpace.Corridor(owner,6200,26,3700);
            target=PoisonDaggerChecks.Target(owner);target.SetResistance(ResistanceType.Physical,100);
            var first=Marker(owner);var second=Marker(owner);
            try
            {
                int mana=NecromancerEffects.ManaBalance256(owner),life=target.Hits;
                target.Combatant=owner;
                Require(GrimWardCurseTransport.Apply(owner,first,target,Values(3,0),TimedEffects.Now),"Initial prepared Ward refused");
                var curse=NecromancerEffects.Curse(target);
                Require(curse.Source==first.Serial.Value&&curse.Terror.Skill==CombatAbility.GrimWard&&curse.Terror.Override==owner.Serial.Value
                    &&curse.PhysicalDelta==-4&&curse.Ward is{Velocity:-27,Attack:-27,Other:-27,FullPhysical:-20},"Missile creator/formula/initial threat or immunity penalty changed");
                Require(NecromancerEffects.Resistance(target,"physical",100)==96,"Ward physical immunity penalty applied twice");
                Require(NecromancerEffects.Resistance(target,"fire",80)==80,"Ward changed elemental resistance");
                Require(Math.Abs(ClassGearHooks.MovementDelay(target,1)-100.0/73)<1e-9
                    &&GearEffects.AttackSlow(target)==27&&GrimWardCurseTransport.Other(target)==-27,"Separate movement/attack/retained other lanes");
                var running=curse with{UntilUtc=TimedEffects.Now.AddMinutes(1),Ward=curse.Ward with{Velocity=-11},
                    Terror=curse.Terror with{Started=true,Goal=new(target.X+5,target.Y,target.Z),VelocityPercent=100}};
                NecromancerEffects.Profiles.Store(target,new(Curse:running));
                Require(Math.Abs(ClassGearHooks.MovementDelay(target,1)-100.0/189)<1e-9,"Fear running and Ward slow must add before division");
                target.Combatant=owner;
                Require(GrimWardCurseTransport.Apply(owner,second,target,Values(3,20),TimedEffects.Now),"Equal-rank Ward refresh refused");
                curse=NecromancerEffects.Curse(target);
                Require(curse.Source==first.Serial.Value&&curse.Ward is{Velocity:-11,Attack:-27,Other:-27,FullPhysical:-120}
                    &&curse.PhysicalDelta==-24&&curse.Terror is{Started:false,Combat:false,Goal:null}
                    &&curse.Terror.Override==0&&target.Combatant==null,"Equal refresh lost primary/creator or failed secondary writes/AI restart");
                Require(NecromancerEffects.Resistance(target,"physical",100)==76,"Refreshed secondary physical consumer");
                Require(!GrimWardCurseTransport.Apply(owner,second,target,Values(1,0),TimedEffects.Now)&&NecromancerEffects.Curse(target)==curse,"Weaker rank mutated active Ward");
                target.Combatant=owner;
                Require(GrimWardCurseTransport.Apply(owner,second,target,Values(4,0),TimedEffects.Now)&&NecromancerEffects.Curse(target).Source==second.Serial.Value,"Higher rank retained wrong creator");
                var attract=new NecromancerCurse(CombatAbility.Attract,owner.Serial.Value,1,TimedEffects.Now.AddMinutes(1),owner.X,owner.Y);
                NecromancerEffects.Profiles.Store(target,new(Curse:attract));
                Require(!GrimWardCurseTransport.Apply(owner,first,target,Values(20,20),TimedEffects.Now)&&NecromancerEffects.Curse(target)==attract,"Ward replaced Attract");
                NecromancerEffects.Profiles.Clear(target);target.Combatant=owner;
                Require(GrimWardCurseTransport.Apply(owner,first,target,Values(3,20),TimedEffects.Now),"Final retained cohort refused");
                curse=NecromancerEffects.Curse(target) with{UntilUtc=TimedEffects.Now.AddDays(1)};
                NecromancerEffects.Profiles.Store(target,new(Curse:curse));
                Require(!GrimWardCurseTransport.Valid(curse with{Source=owner.Serial.Value})
                    &&!GrimWardCurseTransport.Valid(curse with{PhysicalDelta=-17})
                    &&!GrimWardCurseTransport.Valid(curse with{Ward=curse.Ward with{Attack=-200}})
                    &&!GrimWardCurseTransport.Valid(curse with{Ability=CombatAbility.Terror}),"Malformed Ward metadata accepted");
                Require(!JsonSerializer.Serialize(attract).Contains("\"Ward\""),"Older curse schema gained a mandatory field");
                Require(!BarbarianCombat.Implemented(CombatAbility.GrimWard)&&!NecromancerEffects.IsCurse(CombatAbility.GrimWard),"Recipient adapter enabled incomplete learned handler");
                Require(target.Hits==life&&NecromancerEffects.ManaBalance256(owner)==mana,"Prepared curse caused damage or paid cast mana");
                saved=new(owner.Serial.Value,target.Serial.Value,first.Serial.Value,curse,life,mana,0);
            }
            finally{first.Delete();second.Delete();}
        }
        Require(GrimWardCurseTransport.Attack(target)==-27&&GrimWardCurseTransport.Other(target)==-27
            &&NecromancerEffects.Resistance(target,"physical",100)==76,"Retained actual effect consumers changed");
        target.AIObject?.AITimer.Stop();target.Internalize();owner.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
        Log.Information("Ward transport:generation{Generation},same saved monster/missile creator/four deltas/AI;deleted creator survives reload;no learned cast or nonattack animation claim",saved.Generation);
    }
    private static void Live()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        FixtureSpace.Corridor(owner,6200,26,3600);var target=PoisonDaggerChecks.Target(owner);var missile=Marker(owner);
        target.Frozen=target.CantWalk=false;target.Combatant=owner;int distance=SourceTerror.Distance(target,owner);int hits=target.Hits;
        void Cleanup(){NecromancerEffects.Profiles.Clear(target);target.Delete();missile.Delete();owner.Internalize();}
        try
        {
            Require(GrimWardCurseTransport.Apply(owner,missile,target,Values(1,0),TimedEffects.Now),"Live Ward application refused");
            var end=NecromancerEffects.Curse(target).UntilUtc;
            Require(NecromancerEffects.Think(target)&&SourceTerror.State(target) is{Started:true,Goal:not null},"Shared Terror AI did not request an escape");
            Log.Information("grim-ward-transport-live: ready");
            Timer.StartTimer(TimeSpan.FromMilliseconds(1200),()=>Core.LoopContext.Post(()=>
            {
                try
                {
                    Require(SourceTerror.Distance(target,owner)>distance&&target.Hits==hits,"Actual monster did not flee without taking damage");
                    Timer.StartTimer(end-TimedEffects.Now+TimeSpan.FromMilliseconds(200),()=>Core.LoopContext.Post(()=>
                    {
                        try
                        {
                            NecromancerEffects.Think(target);
                            Require(NecromancerEffects.Curse(target)==null&&GrimWardCurseTransport.Attack(target)==0
                                &&GrimWardCurseTransport.Velocity(target)==0&&GrimWardCurseTransport.Other(target)==0,"Natural expiry retained Ward effects");
                            Cleanup();Log.Information("grim-ward-transport-live-result: passed");
                        }
                        catch(Exception e){Cleanup();Log.Error(e,"grim-ward-transport-live-result: failed");}
                    }));
                }
                catch(Exception e){Cleanup();Log.Error(e,"grim-ward-transport-live-result: failed");}
            }));
        }
        catch{Cleanup();throw;}
    }
}
