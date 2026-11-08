// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowIceChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowIceChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,MartialCharge[] Charges,IceCubeSave Cubes,uint Target,int TargetLife,string Status);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-ice-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static IceCubeSave Own(AssassinShadowBody b)
    {var cubes=MartialCold.Snapshot().Cubes.Where(c=>c.Npc?.Actor==b.Serial.Value).ToArray();return new(cubes,cubes.Select(c=>c.Id).DefaultIfEmpty(0).Max());}
    private static MartialCharge[] Charges(AssassinShadowBody b)=>AssassinCharges.Snapshot().Where(c=>c.Owner==b.Serial.Value).ToArray();
    private static string Status(BaseCreature t)=>TimedEffects.TryActor(t,out var s)?Json(new{s.Cold,s.ChillUntil,s.Control}):null;
    private static Held Capture(AssassinShadowBody b,BaseCreature t)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),Charges(b),Own(b),t.Serial.Value,SkillHealth.Balance256(t),Status(t));
    public static void Configure(){Register("shadow-ice-check",false);Register("shadow-ice-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own Shadow Ice statuses/cubes/mixed state order and original saves",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Clear(AssassinShadowBody b)
    {
        ShadowActions.Cancel(b);AssassinCharges.Clear(b);var cubes=MartialCold.Snapshot();MartialCold.Restore(cubes with{Cubes=cubes.Cubes.Where(c=>c.Npc?.Actor!=b.Serial.Value).ToArray()});
        var fire=FireGround.Snapshot();FireGround.Restore(fire with{Patches=fire.Patches.Where(p=>p.PhoenixNpc?.Actor!=b.Serial.Value).ToArray()});
        var t=MartialLightning.Snapshot();var casts=t.Flights.Where(f=>f.Npc?.Actor==b.Serial.Value).Select(f=>f.Cast).ToHashSet();MartialLightning.Restore(t with{Flights=t.Flights.Where(f=>f.Npc?.Actor!=b.Serial.Value).ToArray(),Leases=t.Leases.Where(l=>!casts.Contains(l.Cast)).ToArray()});
        var p=MartialPhoenix.Snapshot();var pc=p.Flights.Where(f=>f.Npc?.Actor==b.Serial.Value).Select(f=>f.Cast).ToHashSet();MartialPhoenix.Restore(p with{Drops=p.Drops.Where(d=>d.Npc?.Actor!=b.Serial.Value).ToArray(),Flights=p.Flights.Where(f=>f.Npc?.Actor!=b.Serial.Value).ToArray(),Leases=p.Leases.Where(l=>!pc.Contains(l.Cast)).ToArray()});
    }
    private static ulong Seed(bool hit)
    {for(int i=1;i<10000;i++){ulong seed=SourceRandom.InitLow(i),copy=seed;uint n=SourceRandom.Limited(ref copy,100);if(hit?n<5:n>=95){return seed;}}throw new InvalidOperationException("Missing bounded hit seed");}
    private static SkillValues Values(AssassinShadowBody b,int id)=>ShadowSkillCasting.OwnValues(b,b.OwnedSkillRank(id),id);
    private static MartialCharge Gain(AssassinShadowBody b,int id,int count,DateTime at)
    {for(int i=0;i<count;i++){AssassinCharges.Gain(b,Values(b,id),at);}return AssassinCharges.Finisher(b).Single(s=>SkillMechanics.For(s.Ability).SourceId==id);}
    private static ShadowPsychicChecks.Witness Target(AssassinShadowBody b,int dx=1,int dy=0,int life=65000)
    {var t=new ShadowPsychicChecks.Witness{CasterId=b.Serial.Value};t.SetHits(life);foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}t.MoveToWorld(new(b.X+dx,b.Y+dy,b.Z),b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static void Contact(AssassinShadowBody b,PlayerMobile owner,int count,DateTime at,bool lethal=false)
    {
        Clear(b);var target=Target(b,life:lethal?1:65000);var side=Target(b,1,1);try
        {
            NpcEquipmentRandom.Set(b,Seed(true));var ordinary=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at);var normal=ordinary.Impact.Packet;ulong seed=NpcEquipmentRandom.Require(b).Seed;ordinary.Cancel();Gain(b,274,count,at);
            var packet=MartialCold.Capture(Values(b,274),count,b.EquipmentNumbers.Read(331),n=>(int)SourceRandom.Limited(ref seed,n));
            var replace=MartialCold.InlineShadow(new(ColdFrames:777,FreezeFrames:7),packet);
            Require(replace.ColdFrames==packet.Frames&&replace.FreezeFrames==7+(count==3?packet.Frames:0),"Ice added weapon cold duration or lost initial freeze lane");
            int hp=SkillHealth.Balance256(target),near=SkillHealth.Balance256(side);NpcEquipmentRandom.Set(b,Seed(true));var prepared=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at);var p=prepared.Impact.Packet;
            Require(p.Physical256==normal.Physical256&&p.Cold256==normal.Cold256+packet.Inline256&&p.ColdFrames==packet.Frames
                &&p.FreezeFrames==normal.FreezeFrames+(count==3?packet.Frames:0)&&(p.ResultFlags&16384)!=0&&NpcEquipmentRandom.Require(b).Seed==seed
                &&Own(b).Cubes.Length==0&&SkillHealth.Balance256(target)==hp&&SkillHealth.Balance256(side)==near
                &&(AssassinCharges.State(b,CombatAbility.BladesOfIce)?.Count??0)==count-1,"Own Ice inline/duration/phase/consume-one failed");
            _=NpcWorldAllocation.RequireStream();ulong world=NpcWorldAllocation.Capture().Seed;Point2D[] offsets=[];
            if(count>=2){_=WeaponMissileDamage.RollValue(packet.Minimum256,packet.Maximum256,0,n=>(int)SourceRandom.Limited(ref seed,n));}
            if(count==3){int low=unchecked((int)SourceRandom.Roll(ref seed));offsets=FistsFireRelease.Scatter(packet.ScatterRadius,low);foreach(var _ in offsets){SourceRandom.Roll(ref world);}}
            _=IronGolemContact.Release(b,owner,target,prepared,_=>true,at);
            Require(lethal?!target.Alive:SkillHealth.Balance256(target)<hp,"Ice parent did not deliver");
            if(!lethal){Require(TimedEffects.TryActor(target,out var status)&&status.Cold?.Source==b.Serial.Value&&(count==3?status.Control?.Freeze?.Source==b.Serial.Value:status.Control?.Freeze==null),"Ice inline lost separate cold/freeze source");}
            Require(count==1?SkillHealth.Balance256(side)==near:SkillHealth.Balance256(side)<near&&TimedEffects.TryActor(side,out var area)&&area.Cold?.Source==b.Serial.Value&&area.Control?.Freeze==null,"Ice copied area missing or incorrectly freezing");
            Require(NpcEquipmentRandom.Require(b).Seed==seed&&NpcWorldAllocation.Capture().Seed==world,"Ice area/scatter caster/world RNG differs");
            var cubes=Own(b).Cubes;int mastery=b.EquipmentNumbers.Read(331),damage=256*Math.Max(0,100+mastery)/100;
            Require(cubes.Length==offsets.Length&&cubes.All(c=>c.Minimum256==damage&&c.Maximum256==damage&&c.ColdFrames==100&&c.Frames==30
                &&c.Npc.Actor==b.Serial.Value&&c.Owner==owner.Serial.Value&&c.LastCollision==b.Serial.Value&&c.Npc.Binding==OriginalNpcEquipment.Binding(b)
                &&c.Seed==c.Npc.UnitSeed&&c.Pierce==c.Npc.Pierce),"Ice cube packet/source/lifetime differs");
            Require(cubes.Select(c=>new Point2D((int)Math.Round((c.X-target.X)*2),(int)Math.Round((c.Y-target.Y)*2))).SequenceEqual(offsets),"Ice scatter order/duplicates lost");
        }
        finally{Clear(b);target.Corpse?.Delete();target.Delete();side.Delete();}
    }
    private static void Cubes(AssassinShadowBody b,PlayerMobile owner,DateTime at)
    {
        Clear(b);var target=Target(b);try
        {
            var source=new PhoenixNpcSource(b.Serial.Value,OriginalNpcEquipment.Binding(b),NpcWorldAllocation.UnitSeed(),0);
            MartialCold.AddShadowCube(b,3,target.X,target.Y,target.Z,256,256,source,at);var rng=NpcEquipmentRandom.Require(b);var world=NpcWorldAllocation.Capture();int hp=SkillHealth.Balance256(target);
            MartialCold.Tick(at.AddMilliseconds(40),true,b.Serial.Value);var next=Own(b).Cubes.Single();
            Require(next.LastCollision==target.Serial.Value&&hp-SkillHealth.Balance256(target)==256&&next.Seed==source.UnitSeed&&next.Npc.Fault==null
                &&NpcEquipmentRandom.Require(b)==rng&&NpcWorldAllocation.Capture()==world&&TimedEffects.TryActor(target,out var status)
                &&status.Cold?.Source==b.Serial.Value&&status.Control?.Freeze?.Source==b.Serial.Value,"Cube damage10 context "+Json(new{next,Loss=hp-SkillHealth.Balance256(target),ExpectedTarget=target.Serial.Value,RngBefore=rng,RngAfter=NpcEquipmentRandom.Require(b),WorldBefore=world,WorldAfter=NpcWorldAllocation.Capture(),Status=Status(target)}));
            hp=SkillHealth.Balance256(target);MartialCold.Tick(at.AddMilliseconds(80),true,b.Serial.Value);Require(SkillHealth.Balance256(target)==hp,"Stationary cube hit LastCollision again");
            MartialCold.Tick(at.AddMilliseconds(30*40),true,b.Serial.Value);Require(Own(b).Cubes.Length==0,"Cube did not expire at30frames");
            TimedEffects.Clear(target);target.SetResistance(ResistanceType.Cold,100);MartialCold.AddShadowCube(b,3,target.X,target.Y,target.Z,256,256,source,at);
            MartialCold.Tick(at.AddMilliseconds(40),true,b.Serial.Value);Require(SkillHealth.Balance256(target)==hp&&(!TimedEffects.TryActor(target,out status)||status.Cold==null&&status.Control?.Freeze==null),"Cold-immune cube target damaged/chilled/frozen");target.SetResistance(ResistanceType.Cold,0);Clear(b);
            var state=Gain(b,274,3,at);NpcEquipmentRandom.Set(b,Seed(false));var miss=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at);
            Require((miss.Impact.Packet.ResultFlags&1)==0&&AssassinCharges.State(b,CombatAbility.BladesOfIce)==state&&Own(b).Cubes.Length==0,"Miss emitted/consumed Ice");miss.Cancel();
            NpcEquipmentRandom.Set(b,Seed(true));var edge=OriginalNpcMelee.Prepare(b,owner,target,bonus=>bonus>0,at);Require(AssassinCharges.State(b,CombatAbility.BladesOfIce)==state&&edge.AfterContact==null,"Loose result range consumed/emitted Ice");edge.Cancel();
            NpcEquipmentRandom.Set(b,Seed(true));var canceled=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at);canceled.Cancel();Require(canceled.AfterContact==null&&Own(b).Cubes.Length==0,"Canceled Ice callback retained work");
            MartialCold.AddShadowCube(b,3,target.X,target.Y,target.Z,256,256,source,at);hp=SkillHealth.Balance256(target);MartialCold.Tick(at.AddSeconds(2),false,b.Serial.Value);
            Require(Own(b).Cubes.Length==0&&SkillHealth.Balance256(target)==hp,"Offline cube fought or failed to expire");
            MartialCold.AddShadowCube(b,3,target.X,target.Y,target.Z,256,256,source,at);var good=MartialCold.Snapshot();bool bad=false;
            try{MartialCold.Restore(good with{Cubes=good.Cubes.Select(c=>c.Npc?.Actor==b.Serial.Value?c with{Npc=c.Npc with{Actor=owner.Serial.Value}}:c).ToArray()});}catch(InvalidDataException){bad=true;}
            Require(bad&&Json(MartialCold.Snapshot())==Json(good),"Malformed NPC cube restore replaced live pool");
        }
        finally{Clear(b);target.Delete();}
    }
    private static void Mixed(AssassinShadowBody b,PlayerMobile owner,DateTime at)
    {
        Clear(b);var target=Target(b);try
        {
            foreach(int id in new[]{254,265,280,259,274,269}){b.ChangeSkills(book=>book.Assign(id,3));}
            NpcEquipmentRandom.Set(b,Seed(true));var baseline=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at);ulong seed=NpcEquipmentRandom.Require(b).Seed;var normal=baseline.Impact.Packet;baseline.Cancel();
            foreach(int id in new[]{254,265,280,259,274,269}){Gain(b,id,3,at);}
            var fire=FistsFireRelease.Capture(Values(b,259),3,b.EquipmentNumbers.Read(329),n=>(int)SourceRandom.Limited(ref seed,n));
            var cold=MartialCold.Capture(Values(b,274),3,b.EquipmentNumbers.Read(331),n=>(int)SourceRandom.Limited(ref seed,n));
            var lightning=MartialLightning.Capture(Values(b,269),3,b.EquipmentNumbers.Read(330),b.EquipmentNumbers.Read(334),n=>(int)SourceRandom.Limited(ref seed,n));
            SourceRandom.Roll(ref seed); // Phoenix124chaos callback before parent.
            NpcEquipmentRandom.Set(b,Seed(true));_=NpcWorldAllocation.RequireStream();ulong world=NpcWorldAllocation.Capture().Seed;for(int i=0;i<16;i++){SourceRandom.Roll(ref world);}
            var prepared=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at);
            Require(NpcEquipmentRandom.Require(b).Seed==seed&&NpcWorldAllocation.Capture().Seed==world&&prepared.AfterContact!=null&&Charges(b).Length==6&&Charges(b).All(c=>c.Count==2)
                &&prepared.Impact.Packet.Cold256==normal.Cold256+cold.Inline256&&prepared.Impact.Packet.Lightning256==normal.Lightning256+lightning.Inline256
                &&Own(b).Cubes.Length==0&&!FireGround.Snapshot().Patches.Any(p=>p.PhoenixNpc?.Actor==b.Serial.Value)
                &&!MartialLightning.Snapshot().Flights.Any(f=>f.Npc?.Actor==b.Serial.Value)
                &&MartialPhoenix.Snapshot().Flights.Count(f=>f.Npc?.Actor==b.Serial.Value)==16,"Mixed preparation lost state/inline RNG order or emitted later families before parent");
            _=WeaponMissileDamage.RollValue(fire.Min256,fire.Max256,0,n=>(int)SourceRandom.Limited(ref seed,n));int low=unchecked((int)SourceRandom.Roll(ref seed));int flames=FistsFireRelease.Scatter(fire.ScatterRadius,low).Length;
            _=WeaponMissileDamage.RollValue(cold.Minimum256,cold.Maximum256,0,n=>(int)SourceRandom.Limited(ref seed,n));low=unchecked((int)SourceRandom.Roll(ref seed));int cubes=FistsFireRelease.Scatter(cold.ScatterRadius,low).Length;
            SourceRandom.Roll(ref seed);for(int i=0;i<flames+cubes+80;i++){SourceRandom.Roll(ref world);}
            _=IronGolemContact.Release(b,owner,target,prepared,_=>true,at);
            Require(NpcEquipmentRandom.Require(b).Seed==seed&&NpcWorldAllocation.Capture().Seed==world&&Own(b).Cubes.Length==cubes
                &&FireGround.Snapshot().Patches.Count(p=>p.PhoenixNpc?.Actor==b.Serial.Value)==flames&&MartialLightning.Snapshot().Flights.Count(f=>f.Npc?.Actor==b.Serial.Value)==80,"Mixed Fire/Ice/Thunder after-parent source order/streams differ");
        }
        finally{Clear(b);target.Delete();}
    }
    private static void Ai(AssassinShadowBody b,PlayerMobile owner,DateTime at)
    {
        Clear(b);var t=Target(b);try
        {
            int frames=((INecromancerGroundBrain)b).GroundSkill(owner,14,274,t,at);Require(frames>0,"Ice own world mode14 refused");
            while(ShadowActions.State(b)is{} action){NpcEquipmentRandom.Set(b,Seed(true));ShadowActions.Tick(action.NextUtc,true,b.Serial.Value);}
            Require(AssassinCharges.State(b,CombatAbility.BladesOfIce)?.Count>=1,"Ice sequence did not build");Clear(b);Gain(b,274,3,at);NpcEquipmentRandom.Set(b,Seed(true));Require(ShadowActions.BeginMelee(b,t,at.AddSeconds(2)),"Ice own basic entry refused");
            frames=ShadowActions.State(b).Plan.Events.Single(e=>e.Type==2).Frame;ShadowActions.Tick(at.AddMilliseconds(2000+frames*40),true,b.Serial.Value);Require(Own(b).Cubes.Length>0&&AssassinCharges.State(b,CombatAbility.BladesOfIce)?.Count==2,"Ice own basic did not release");Clear(b);
            if(b.Prepared.Capture.Ability==CombatAbility.ShadowWarrior)
            {
                if(!ShadowGroundMotion.Active(b)){ShadowGroundMotion.Activate(b,owner,at.AddMinutes(1));}
                Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.BladesOfIce)&&SkillBindings.Instance.Assign(owner,false,CombatAbility.BladesOfIce),"Ice Warrior binding refused");
                NpcEquipmentRandom.Set(b,SourceRandom.InitLow(1234));bool builder=false,emitted=false;
                for(int i=0;i<800&&!(builder&&emitted);i++){IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);Require(b.WorldGround.State.Controller.Fault==null,"Ice Warrior fault: "+b.WorldGround.State.Controller.Fault);builder|=ShadowActions.State(b)?.Cast.Skill==274;emitted=Own(b).Cubes.Length>0;}
                Require(builder&&emitted,"Natural Warrior AI did not build/release Ice");Clear(b);
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
                var h=saved.Bodies[i];var target=(BaseCreature)World.FindMobile((Serial)h.Target);var expected=h with{Cubes=h.Cubes with{Cubes=h.Cubes.Cubes.Select(c=>c with{Npc=c.Npc with{Dormant=true}}).ToArray()}};
                Require(Json(Capture(bodies[i],target))==Json(expected),"Reload lost original body/items/resources/charges/cube source/clock/LastCollision or duration lanes");
                var cube=Own(bodies[i]).Cubes.Single();int hp=SkillHealth.Balance256(target);var rng=NpcEquipmentRandom.Require(bodies[i]);var world=NpcWorldAllocation.Capture();MartialCold.Tick(cube.NextUtc,true,bodies[i].Serial.Value);
                Require(Own(bodies[i]).Cubes.Single().Frame==cube.Frame+1&&SkillHealth.Balance256(target)==hp&&NpcEquipmentRandom.Require(bodies[i])==rng&&NpcWorldAllocation.Capture()==world,"Loaded cube replayed damage/resources or reset frame");Clear(bodies[i]);target.Delete();
            }
            owner.MoveToWorld(new(6260,3600,0),Map.Trammel);
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3600);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);ClassSkillChecks.Train(owner,"assassin.bladesofice",1);var world=NpcWorldAllocation.Capture();
            try{bodies=new AssassinShadowBody[2];int i=0;foreach(var a in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}){NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));bodies[i]=AssassinShadowBody.CreateInitialized(owner,a,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(bodies[i]);ShadowEquipmentDefense.Activate(bodies[i]);i++;}}finally{NpcWorldAllocation.Restore(world);}
        }
        var held=new System.Collections.Generic.List<Held>();
        foreach(var b in bodies)
        {
            b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);b.ChangeSkills(book=>book.Assign(274,3));var at=TimedEffects.Now.AddMinutes(15+generation);int mana=NecromancerEffects.ManaBalance256(owner);var items=ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray();
            for(int count=1;count<=3;count++){Contact(b,owner,count,at);}Contact(b,owner,3,at.AddSeconds(1),true);Cubes(b,owner,at.AddSeconds(2));Mixed(b,owner,at.AddSeconds(4));Ai(b,owner,at.AddSeconds(6));
            Require(NecromancerEffects.ManaBalance256(owner)==mana&&ShadowLoadout.Originals(b).Select(i=>i.SavedText).SequenceEqual(items),"Own Ice borrowed mana or altered original items");
            var t=Target(b,b.Prepared.Capture.Ability==CombatAbility.ShadowWarrior?12:18);var source=new PhoenixNpcSource(b.Serial.Value,OriginalNpcEquipment.Binding(b),NpcWorldAllocation.UnitSeed(),0);MartialCold.AddShadowCube(b,3,t.X,t.Y,t.Z,256,256,source,at);MartialCold.Tick(at.AddMilliseconds(40),true,b.Serial.Value);
            Gain(b,274,3,at);SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);PersistentMinions.Park(b);GeneratedNpcLiveResources.Snapshot();held.Add(Capture(b,t));
        }
        PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,held.ToArray(),generation)));
        Require(!AssassinCombat.Implemented(CombatAbility.ShadowWarrior)&&!AssassinCombat.Implemented(CombatAbility.ShadowMaster),"Ice enabled unfinished learned Shadows");
        Log.Information("Own Ice:inline cold length replacement/three-charge freeze,parent-before-chill area,source scatter/cube damage10 cold+freeze/LastCollision/30frame expiry,all-six state-order streams,immunity/miss/cancel/atomic restore,world/basic/natural Warrior AI and exact original body/cube/status saves through two reloads;other intrinsic/modern/learned gates remain.");
    }
}
