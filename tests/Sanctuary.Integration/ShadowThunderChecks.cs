// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowThunderChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowThunderChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,MartialCharge[] Charges,ThunderSave Projectiles);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-thunder-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static ThunderSave Own(AssassinShadowBody b)
    {
        var all=MartialLightning.Snapshot();var flights=all.Flights.Where(f=>f.Npc?.Actor==b.Serial.Value).ToArray();var casts=flights.Select(f=>f.Cast).ToHashSet();
        return new(flights,all.Leases.Where(l=>casts.Contains(l.Cast)).ToArray(),flights.Select(f=>f.Id).DefaultIfEmpty(0).Max(),flights.Select(f=>f.Cast).DefaultIfEmpty(0).Max());
    }
    private static MartialCharge[] Charges(AssassinShadowBody b)=>AssassinCharges.Snapshot().Where(c=>c.Owner==b.Serial.Value).ToArray();
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),Charges(b),Own(b));
    public static void Configure(){Register("shadow-thunder-check",false);Register("shadow-thunder-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own Shadow Thunder original contact/shared flights and persistence",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Clear(AssassinShadowBody b)
    {
        ShadowActions.Cancel(b);AssassinCharges.Clear(b);var s=MartialLightning.Snapshot();var ids=s.Flights.Where(f=>f.Npc?.Actor==b.Serial.Value).Select(f=>f.Cast).ToHashSet();
        MartialLightning.Restore(s with{Flights=s.Flights.Where(f=>f.Npc?.Actor!=b.Serial.Value).ToArray(),Leases=s.Leases.Where(l=>!ids.Contains(l.Cast)).ToArray()});
    }
    private static ulong Seed(bool hit)
    {for(int i=1;i<10000;i++){ulong seed=SourceRandom.InitLow(i),copy=seed;uint n=SourceRandom.Limited(ref copy,100);if(hit?n<5:n>=95){return seed;}}throw new InvalidOperationException("Missing bounded hit seed");}
    private static MartialCharge Charge(AssassinShadowBody b,int count,DateTime at)
    {
        AssassinCharges.Clear(b);var values=ShadowSkillCasting.OwnValues(b,b.OwnedSkillRank(269),269);
        for(int i=0;i<count;i++){AssassinCharges.Gain(b,values,at);}return AssassinCharges.State(b,CombatAbility.ClawsOfThunder);
    }
    private static ThunderPacket Packet(AssassinShadowBody b,int count)
        =>MartialLightning.Capture(ShadowSkillCasting.OwnValues(b,b.OwnedSkillRank(269),269),count,b.EquipmentNumbers.Read(330),b.EquipmentNumbers.Read(334),_=>0);
    private static ShadowPsychicChecks.Witness Target(AssassinShadowBody b,int dx=1,int dy=0)
    {
        var t=new ShadowPsychicChecks.Witness{CasterId=b.Serial.Value};t.SetHits(65000);foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}
        t.MoveToWorld(new(b.X+dx,b.Y+dy,b.Z),b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static long Emit(AssassinShadowBody b,int count,DateTime at)
    {
        var packet=Packet(b,count);var before=NpcEquipmentRandom.Require(b);ulong caster=before.Seed;_=NpcWorldAllocation.RequireStream();var world=NpcWorldAllocation.Capture();ulong game=world.Seed;
        if(count==3){SourceRandom.Roll(ref caster);}var pattern=MartialLightning.Pattern(count,count==3?unchecked((int)(uint)caster):0);
        long cast=MartialLightning.AfterShadow(b,new(b.X+1,b.Y,b.Z),packet,at);var flights=Own(b).Flights.Where(f=>f.Cast==cast).ToArray();
        Require(count==1?cast==0&&flights.Length==0:cast>0&&flights.Length==(count==2?64:80),"Wrong Thunder tier count");
        for(int i=0;i<flights.Length;i++)
        {
            var f=flights[i];var spawn=pattern[i];ulong unit=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref game)));
            ulong seed=spawn.Bolt?SourceRandom.InitLow(unchecked((int)spawn.Seed)):unit;
            var path=spawn.Bolt?ChargedBolts.Path(spawn.Seed,ChargedBolts.Heading(spawn.X,spawn.Y),77):Array.Empty<Point2D>();for(int j=1;j<path.Length;j++){SourceRandom.Roll(ref seed);}
            Require(f.Npc.Actor==b.Serial.Value&&f.Npc.Binding==OriginalNpcEquipment.Binding(b)&&f.Npc.UnitSeed==unit&&f.Owner==b.ControlMaster.Serial.Value
                &&f.LastCollision==b.Serial.Value&&f.Npc.Pierce==packet.Pierce&&f.Seed==seed&&f.Bolt==spawn.Bolt&&f.Path.SequenceEqual(path)
                &&f.Frames==spawn.Frames&&f.TargetX==spawn.TargetX&&f.Min256==(spawn.Bolt?packet.BoltMin256:packet.NovaMin256)
                &&f.Max256==(spawn.Bolt?packet.BoltMax256:packet.NovaMax256)&&f.Direction==MissileVectors.Direction(spawn.X,spawn.Y),"Thunder child factory/initializer/path/source/damage differs");
        }
        Require(NpcEquipmentRandom.Require(b).Seed==caster&&NpcWorldAllocation.Capture().Seed==game,"Thunder caster/world allocation stream differs");return cast;
    }
    private static void Contact(AssassinShadowBody b,PlayerMobile owner,int count,DateTime at)
    {
        Clear(b);var target=Target(b);try
        {
            NpcEquipmentRandom.Set(b,Seed(true));var baseline=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at);var normal=baseline.Impact.Packet;var afterFill=NpcEquipmentRandom.Require(b);baseline.Cancel();
            Charge(b,count,at);ulong seed=afterFill.Seed;var expected=MartialLightning.Capture(ShadowSkillCasting.OwnValues(b,b.OwnedSkillRank(269),269),count,b.EquipmentNumbers.Read(330),b.EquipmentNumbers.Read(334),n=>(int)SourceRandom.Limited(ref seed,n));if(count==3){SourceRandom.Roll(ref seed);}
            NpcEquipmentRandom.Set(b,Seed(true));int hp=SkillHealth.Balance256(target);var prepared=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at);var packet=prepared.Impact.Packet;
            Require(packet.Physical256==normal.Physical256&&packet.Lightning256==normal.Lightning256+expected.Inline256&&(packet.ResultFlags&16384)!=0
                &&NpcEquipmentRandom.Require(b).Seed==seed&&SkillHealth.Balance256(target)==hp&&(AssassinCharges.State(b,CombatAbility.ClawsOfThunder)?.Count??0)==count-1
                &&Own(b).Flights.Length==(count==1?0:count==2?64:80),"Own Thunder inline/unchanged physical/pre-parent emission/consume-one differs");
            _=IronGolemContact.Release(b,owner,target,prepared,_=>true,at);Require(SkillHealth.Balance256(target)<hp,"Own Thunder parent failed");
        }
        finally{Clear(b);target.Delete();}
    }
    private static void Flight(AssassinShadowBody b,PlayerMobile owner,DateTime at)
    {
        Clear(b);var target=Target(b);try
        {
            for(int count=1;count<=3;count++){Clear(b);Emit(b,count,at);}
            var f=Own(b).Flights.First(x=>!x.Bolt);var caster=NpcEquipmentRandom.Require(b);var game=NpcWorldAllocation.Capture();ulong seed=f.Seed;
            int raw=WeaponMissileDamage.RollValue(f.Min256,f.Max256,0,n=>(int)SourceRandom.Limited(ref seed,n));int hp=SkillHealth.Balance256(target);
            var hit=MartialLightning.Impact(owner,target,f);
            Require(hit.Loss==raw&&hp-SkillHealth.Balance256(target)==raw&&hit.Seed==seed&&NpcEquipmentRandom.Require(b)==caster&&NpcWorldAllocation.Capture()==game
                &&!MartialLightning.Ready(f.Cast,target.Serial.Value,f.NextUtc.AddMilliseconds(159))&&MartialLightning.Ready(f.Cast,target.Serial.Value,f.NextUtc.AddMilliseconds(160)),"NPC Thunder typed damage/private RNG/lease failed");
            target.SetResistance(ResistanceType.Energy,100);hit=MartialLightning.Impact(owner,target,f with{Seed=hit.Seed});Require(hit.Loss==0,"NPC Thunder ignored lightning immunity");target.SetResistance(ResistanceType.Energy,0);
            long another=Emit(b,2,at);Require(MartialLightning.Ready(another,target.Serial.Value,f.NextUtc),"Distinct Thunder casts shared suppression");
            Clear(b);Emit(b,3,at);hp=SkillHealth.Balance256(target);MartialLightning.Tick(at.AddMilliseconds(120),true,b.Serial.Value);
            Require(SkillHealth.Balance256(target)<hp&&Own(b).Flights.All(f=>f.Fault==null&&f.Npc.Fault==null),"NPC Thunder moving collision caused no damage or faulted");
            Clear(b);Emit(b,3,at);hp=SkillHealth.Balance256(target);caster=NpcEquipmentRandom.Require(b);MartialLightning.Tick(at.AddSeconds(4),false,b.Serial.Value);
            Require(Own(b).Flights.Length==0&&SkillHealth.Balance256(target)==hp&&NpcEquipmentRandom.Require(b)==caster,"Offline Thunder fought or failed to expire");
            Emit(b,3,at);var good=MartialLightning.Snapshot();bool bad=false;
            try{MartialLightning.Restore(good with{Flights=good.Flights.Select(f=>f.Npc?.Actor==b.Serial.Value?f with{Npc=f.Npc with{Actor=owner.Serial.Value}}:f).ToArray()});}catch(InvalidDataException){bad=true;}
            Require(bad&&Json(MartialLightning.Snapshot())==Json(good),"Malformed NPC Thunder restore replaced pools");
            Clear(b);var state=Charge(b,3,at);NpcEquipmentRandom.Set(b,Seed(false));var miss=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at);
            Require((miss.Impact.Packet.ResultFlags&1)==0&&Own(b).Flights.Length==0&&AssassinCharges.State(b,CombatAbility.ClawsOfThunder)==state,"Miss emitted/consumed Thunder");miss.Cancel();
            NpcEquipmentRandom.Set(b,Seed(true));var edge=OriginalNpcMelee.Prepare(b,owner,target,bonus=>bonus>0,at);Require(Own(b).Flights.Length==0&&AssassinCharges.State(b,CombatAbility.ClawsOfThunder)==state,"Loose-range hit emitted/consumed Thunder");edge.Cancel();
        }
        finally{Clear(b);target.Delete();}
    }
    private static void Ai(AssassinShadowBody b,PlayerMobile owner,DateTime at)
    {
        Clear(b);var t=Target(b);try
        {
            int frames=((INecromancerGroundBrain)b).GroundSkill(owner,14,269,t,at);Require(frames>0,"Thunder world mode14 refused");
            while(ShadowActions.State(b)is{} action){NpcEquipmentRandom.Set(b,Seed(true));ShadowActions.Tick(action.NextUtc,true,b.Serial.Value);}
            Require(AssassinCharges.State(b,CombatAbility.ClawsOfThunder)?.Count>=1,"Thunder world action failed to build");Charge(b,3,at);NpcEquipmentRandom.Set(b,Seed(true));
            Require(ShadowActions.BeginMelee(b,t,at.AddSeconds(2)),"Thunder own basic entry refused");frames=ShadowActions.State(b).Plan.Events.Single(e=>e.Type==2).Frame;ShadowActions.Tick(at.AddMilliseconds(2000+frames*40),true,b.Serial.Value);
            Require(Own(b).Flights.Length==80&&AssassinCharges.State(b,CombatAbility.ClawsOfThunder)?.Count==2,"Thunder basic clock did not emit/consume");Clear(b);
            if(b.Prepared.Capture.Ability==CombatAbility.ShadowWarrior)
            {
                if(!ShadowGroundMotion.Active(b)){ShadowGroundMotion.Activate(b,owner,at.AddMinutes(1));}
                Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.ClawsOfThunder)&&SkillBindings.Instance.Assign(owner,false,CombatAbility.ClawsOfThunder),"Thunder Warrior owner binding refused");
                NpcEquipmentRandom.Set(b,SourceRandom.InitLow(1234));bool builder=false,emitted=false;
                for(int i=0;i<800&&!(builder&&emitted);i++)
                {IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);Require(b.WorldGround.State.Controller.Fault==null,"Thunder Warrior fault: "+b.WorldGround.State.Controller.Fault);builder|=ShadowActions.State(b)?.Cast.Skill==269;emitted=Own(b).Flights.Length>0;}
                Require(builder&&emitted,"Natural Warrior AI did not build/release Thunder");Clear(b);
            }
        }
        finally{Clear(b);t.Delete();}
    }
    private static void Check(bool reload)
    {
        PlayerMobile owner;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++)
            {
                var h=saved.Bodies[i];var expected=h with{Projectiles=h.Projectiles with{Flights=h.Projectiles.Flights.Select(f=>f with{Npc=f.Npc with{Dormant=true}}).ToArray()}};
                Require(Json(Capture(bodies[i]))==Json(expected),"Reload lost original Thunder source/body/items/resources/RNG/partial flights");
                var f=Own(bodies[i]).Flights.First(x=>!x.Bolt);var old=NpcEquipmentRandom.Require(bodies[i]);var game=NpcWorldAllocation.Capture();var expectedVelocity=MartialLightning.VelocityStep(f.VelocityFp8,f.Acceleration,f.AccelerationCounter,f.MaximumVelocityFp8,0,f.Direction.X,f.Direction.Y);
                MartialLightning.Tick(f.NextUtc,true,bodies[i].Serial.Value);var next=Own(bodies[i]).Flights.Single(x=>x.Id==f.Id);
                Require(next.Frame==f.Frame+1&&next.VelocityFp8==expectedVelocity.Velocity&&next.AccelerationCounter==expectedVelocity.Counter&&next.Npc.Dormant
                    &&NpcEquipmentRandom.Require(bodies[i])==old&&NpcWorldAllocation.Capture()==game,"Loaded partial Thunder reset motion or replayed actor/world RNG");Clear(bodies[i]);
            }
            owner.MoveToWorld(new(6260,3720,0),Map.Trammel);
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3720);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);ClassSkillChecks.Train(owner,"assassin.clawsofthunder",1);var world=NpcWorldAllocation.Capture();
            try{bodies=new AssassinShadowBody[2];int i=0;foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}){NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));bodies[i]=AssassinShadowBody.CreateInitialized(owner,ability,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(bodies[i]);ShadowEquipmentDefense.Activate(bodies[i]);i++;}}finally{NpcWorldAllocation.Restore(world);}
        }
        foreach(var b in bodies)
        {
            b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);b.ChangeSkills(book=>book.Assign(269,3));var at=TimedEffects.Now.AddMinutes(15+generation);int mana=NecromancerEffects.ManaBalance256(owner);var items=ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray();
            for(int count=1;count<=3;count++){Contact(b,owner,count,at);}Flight(b,owner,at.AddSeconds(5));Ai(b,owner,at.AddSeconds(10));
            Require(NecromancerEffects.ManaBalance256(owner)==mana&&ShadowLoadout.Originals(b).Select(i=>i.SavedText).SequenceEqual(items),"Thunder borrowed owner mana or changed original items");
            Emit(b,3,at);MartialLightning.Tick(at.AddMilliseconds(160),true,b.Serial.Value);Require(Own(b).Flights.Any(f=>!f.Bolt&&f.Frame==4),"Missing partial frame4 nova fixture");
            Charge(b,3,at);SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);PersistentMinions.Park(b);
        }
        GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,bodies.Select(Capture).ToArray(),generation)));

        Log.Information("Own Thunder:source64nova/16charged initializer streams,post-fill inline lightning/pre-parent emission,typed moving impacts/shared per-cast four-frame leases,immunity/miss/range/offline/expiry/atomic restore,world/basic/natural Warrior AI and exact original partial-motion saves through two reloads;remaining intrinsic/modern/learned gates retained.");
    }
}
