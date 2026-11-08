// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowSentryChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowSentryChecks));
    private static readonly int[] Skills=[261,262,271,272,276];
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life);
    private sealed record Trap(uint Actor,SentryBodyState State,int Life);
    private sealed record Saved(uint Owner,Held[] Bodies,Trap[] Traps,AssassinTrapEntry[] Roster,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-sentry-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b));
    public static void Configure(){Register("shadow-sentry-check",false);Register("shadow-sentry-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own Shadow do45/S2 casts over the existing player-owned trap services",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static BaseCreature Target(Mobile b,Point3D point)
    {
        var t=new ShadowPsychicChecks.Witness{CasterId=b.Serial.Value};t.SetHits(65000);foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}
        t.MoveToWorld(point,b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static AssassinSentryBody Cast(AssassinShadowBody b,int skill,Point3D point,DateTime at)
    {
        var owner=(PlayerMobile)b.ControlMaster;var before=AssassinTrapRoster.For(owner);var actor=NpcEquipmentRandom.Require(b);var world=NpcWorldAllocation.Capture();ulong expected=world.Seed;uint low=SourceRandom.Roll(ref expected);int mana=NecromancerEffects.ManaBalance256(owner),petMana=b.EquipmentNumbers.Read(8);
        Require(ShadowActions.BeginSentry(b,null,point,at,skill),"Own sentry startup refused: "+skill);var action=ShadowActions.State(b);int release=action.Plan.Events.Single(e=>e.Type==1&&e.Kind==2).Frame;
        ShadowActions.Tick(at.AddMilliseconds((release-1)*40),true,b.Serial.Value);
        Require(AssassinTrapRoster.For(owner).SequenceEqual(before)&&NpcWorldAllocation.Capture()==world&&NpcEquipmentRandom.Require(b)==actor,"Sentry created/allocated before S2 marker");
        ShadowActions.Tick(at.AddMilliseconds(release*40),true,b.Serial.Value);var entries=AssassinTrapRoster.For(owner);var entry=entries.Single(e=>!before.Any(p=>p.Actor==e.Actor));var trap=(AssassinSentryBody)World.FindMobile((Serial)entry.Actor);var state=trap.Prepared;
        Require(state.Owner==owner.Serial.Value&&trap.SummonMaster==owner&&state.Creation.Ability==ShadowSentries.Ability(skill)&&state.Creation.Rank==b.OwnedSkillRank(skill)
            &&state.Seed==SourceRandom.InitLow(unchecked((int)low))&&NpcWorldAllocation.Capture().Seed==expected,"Wrong ultimate owner,own rank or world unit allocation");
        var expectedStats=AssassinSentryCreation.Capture(owner,ShadowSentries.Ability(skill),b.OwnedSkillRank(skill));
        Require(Json(state.Creation)==Json(expectedStats)&&state.Position==point&&state.Creation.CasterLevel==ClassStats.Level(owner),"Shared base/passive helper did not use ultimate player plus own cast rank");
        Require(NecromancerEffects.ManaBalance256(owner)==mana&&b.EquipmentNumbers.Read(8)==petMana&&NpcEquipmentRandom.Require(b)==actor,"NPC trap borrowed owner mana/seed or NPC mana");
        var after=NpcWorldAllocation.Capture();ShadowActions.Tick(at.AddMilliseconds(release*40),true,b.Serial.Value);ShadowActions.Tick(at.AddSeconds(2),true,b.Serial.Value);
        Require(NpcWorldAllocation.Capture()==after&&ShadowActions.State(b)==null&&b.OwnedSkills.Used==null,"Duplicate/end replayed trap or retained used skill");return trap;
    }
    private static void Clear(PlayerMobile owner)
    {foreach(var entry in AssassinTrapRoster.For(owner)){World.FindMobile((Serial)entry.Actor)?.Delete();}AssassinTrapRoster.Prune();}
    private static void Shoot(AssassinSentryBody trap,BaseCreature target)
    {
        int hp=SkillHealth.Balance256(target);var stats=Json(trap.Prepared.Creation);var first=trap.Prepared.NextFrameUtc;
        for(int f=0;f<500&&SkillHealth.Balance256(target)==hp;f++)
        {
            var now=first.AddMilliseconds(f*40);AssassinSentryBolts.TickFrame(now,true);AssassinSentryLightning.TickFrame(now,true);AssassinWakeFireMissiles.TickFrame(now,true);trap.AdvanceController(now,true);
            Require(trap.Prepared.ControllerFault==null,"Own created sentry faulted: "+trap.Prepared.ControllerFault);
        }
        Require(SkillHealth.Balance256(target)<hp,"Own created sentry did not fire/damage: "+trap.Prepared.Creation.Ability+" state="+Json(trap.Prepared));
        Require(Json(trap.Prepared.Creation)==stats,"Firing rewrote captured ranks/passives");
    }
    private static void Combat(AssassinShadowBody b,int skill,DateTime at)
    {
        var owner=(PlayerMobile)b.ControlMaster;var point=new Point3D(b.X+3,b.Y,b.Z);var target=Target(b,new(point.X+2,point.Y,point.Z));var trap=Cast(b,skill,point,at);
        try
        {
            Shoot(trap,target);
            if(skill==261){Require(trap.Prepared.Creation.Skill("Shock Field")==ClassCreation.Profiles.GetRank(owner.Serial,SkillMechanics.For(CombatAbility.ShockWeb).TalentId),"Trap synergy came from Shadow book instead of source ultimate-player passive helper");}
        }
        finally{target.Delete();trap.Delete();AssassinTrapRoster.Prune();}
    }
    private static void Refusals(AssassinShadowBody b,DateTime at)
    {
        var owner=(PlayerMobile)b.ControlMaster;var point=new Point3D(b.X+3,b.Y,b.Z);var before=NpcWorldAllocation.Capture();int count=AssassinTrapRoster.For(owner).Length;
        Require(ShadowActions.BeginSentry(b,null,point,at,261),"Cancellation fixture");ShadowActions.Cancel(b);ShadowActions.Tick(at.AddSeconds(2),true,b.Serial.Value);
        Require(NpcWorldAllocation.Capture()==before&&AssassinTrapRoster.For(owner).Length==count,"Canceled sentry allocated/spawned");
        Require(!ShadowActions.BeginSentry(b,null,new(1438,1696,0),at,261)&&NpcWorldAllocation.Capture()==before,"Town/far destination allocated or cast");
        Require(ShadowActions.BeginSentry(b,null,point,at,261),"Failed release fixture");var wall=new Server.Items.Static(0x6);wall.MoveToWorld(point,b.Map);
        try{ShadowActions.Tick(at.AddSeconds(2),true,b.Serial.Value);Require(NpcWorldAllocation.Capture()==before&&AssassinTrapRoster.For(owner).Length==count,"Blocked release allocated or spawned");}
        finally{wall.Delete();ShadowActions.Cancel(b);}
        var position=b.Location;PersistentMinions.Park(b);Require(!ShadowActions.BeginSentry(b,null,point,at,261)&&NpcWorldAllocation.Capture()==before,"Parked source cast/allocated");b.MoveToWorld(position,owner.Map);
    }
    private static void SharedCap(AssassinShadowBody b,DateTime at)
    {
        var owner=(PlayerMobile)b.ControlMaster;Clear(owner);
        var player=AssassinSentryBody.CreateEmerging(owner,new(b.X+2,b.Y,b.Z),at,SourceRandom.InitLow(3));var all=new[]{player}.Concat(Skills.Take(4).Select((skill,i)=>Cast(b,skill,new(b.X+3+i,b.Y,b.Z),at.AddSeconds(i+1)))).ToArray();
        Require(AssassinTrapRoster.For(owner).Select(e=>e.Actor).SequenceEqual(all.Select(t=>t.Serial.Value)),"NPC traps were registered outside the player's mixed FIFO");
        var sixth=Cast(b,276,new(b.X+7,b.Y,b.Z),at.AddSeconds(6));
        Require(AssassinTrapRoster.For(owner).Select(e=>e.Actor).SequenceEqual(all.Skip(1).Select(t=>t.Serial.Value).Append(sixth.Serial.Value))
            &&!player.Alive&&player.Prepared.Retirement!=null,"Shadow's sixth trap did not retire oldest player trap");player.Delete();Clear(owner);
    }
    private static void Ground(PlayerMobile owner)
    {
        FixtureSpace.Corridor(owner,6260,24,3528);var b=AssassinShadowBody.CreateInitialized(owner,CombatAbility.ShadowWarrior,TimedEffects.Now);BaseCreature target=null;
        try
        {
            b.ChangeSkills(book=>book.Assign(261,3));GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);ShadowGroundMotion.Activate(b,owner,TimedEffects.Now.AddMinutes(20));target=Target(b,new(owner.X+5,owner.Y,owner.Z));
            var before=NpcWorldAllocation.Capture();int count=AssassinTrapRoster.For(owner).Length;
            Require(((INecromancerGroundBrain)b).GroundSkill(owner,9,261,target,TimedEffects.Now.AddMinutes(20))==0
                &&NpcWorldAllocation.Capture()==before&&AssassinTrapRoster.For(owner).Length==count,"Occupied target silently used an unqualified spawn fallback");
        }
        finally{target?.Delete();Clear(owner);PersistentMinions.Park(b);b.Delete();}
        FixtureSpace.Corridor(owner,6260,24,3480);
    }
    private static void Check(bool reload)
    {
        PlayerMobile owner;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(saved.Bodies[i])&&ShadowActions.State(bodies[i])==null,"Reload changed original caster state/items/resources/wounds/RNG or replayed action");}
            Require(AssassinTrapRoster.For(owner).SequenceEqual(saved.Roster),"Reload changed shared trap FIFO");
            foreach(var held in saved.Traps)
            {var trap=(AssassinSentryBody)World.FindMobile((Serial)held.Actor);var state=trap.Prepared;Require(Json(state.Creation)==Json(held.State.Creation)&&state.Owner==held.State.Owner&&state.Map==held.State.Map&&state.Position==held.State.Position&&state.Seed==held.State.Seed&&SkillHealth.Balance256(trap)==held.Life,"Reload lost original trap stats/source/position/seed/life");}
            owner.MoveToWorld(new(6260,3480,0),Map.Trammel);
            foreach(var held in saved.Traps)
            {
                var trap=(AssassinSentryBody)World.FindMobile((Serial)held.Actor);var target=Target(trap,new(trap.X+2,trap.Y,trap.Z));
                try{Shoot(trap,target);Require(trap.Serial.Value==held.Actor&&Json(trap.Prepared.Creation)==Json(held.State.Creation),"Reload recast/replaced original trap or captured stats");}finally{target.Delete();}
            }
            Clear(owner);
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3480);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);ClassSkillChecks.Train(owner,"assassin.chargedboltsentry",1);ClassSkillChecks.Train(owner,"assassin.shockweb",3);
            bodies=new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}.Select(a=>AssassinShadowBody.CreateInitialized(owner,a,TimedEffects.Now)).ToArray();
            foreach(var b in bodies){b.ChangeSkills(book=>{foreach(int id in Skills){book.Assign(id,3);}book.Assign(256,10);});GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);}
            NecromancerEffects.SetMana256(owner,0);
        }
        foreach(int skill in Skills){_=ShadowSentries.Row(skill);}
        foreach(var b in bodies)
        {
            b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);var at=TimedEffects.Now.AddMinutes(10);Refusals(b,at);
            foreach(int skill in Skills){Combat(b,skill,at);}SharedCap(b,at.AddMinutes(1));PersistentMinions.Park(b);
        }
        if(!reload){Ground(owner);}
        var pending=Skills.Select((skill,i)=>{var b=bodies[i%2];b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);var trap=Cast(b,skill,new(owner.X+4+i,owner.Y,owner.Z),TimedEffects.Now.AddMinutes(30));PersistentMinions.Park(b);return trap;}).ToArray();
        GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());
        File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,bodies.Select(Capture).ToArray(),pending.Select(t=>new Trap(t.Serial.Value,t.Prepared,SkillHealth.Balance256(t))).ToArray(),AssassinTrapRoster.For(owner),generation)));
        Require(!AssassinCombat.Implemented(CombatAbility.ShadowWarrior)&&!AssassinCombat.Implemented(CombatAbility.ShadowMaster),"do45 adapter enabled unfinished learned summons");
        Log.Information("Shadow sentries:five intrinsic do45/S2 casts from both original species,own rank/ultimate player inheritance and shared FIFO,no mana/actor RNG,cancel/failed placement/park/duplicate end,actual firing/HP,occupied-target ground rejection and original saves through two reloads;modern source-equivalence and learned summons remain scoped");
    }
}
