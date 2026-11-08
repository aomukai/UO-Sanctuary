// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowPhoenixChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowPhoenixChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,uint Target,int TargetLife,PhoenixSave Projectiles,FirePatch[] Fields);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-phoenix-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("shadow-phoenix-check",false);Register("shadow-phoenix-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own Shadow Phoenix original contact/three emissions/shared pool and persistence",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PhoenixSave Own(AssassinShadowBody b)
    {var s=MartialPhoenix.Snapshot();var flights=s.Flights.Where(f=>f.Npc?.Actor==b.Serial.Value).ToArray();var casts=flights.Select(f=>f.Cast).ToHashSet();return s with{Drops=s.Drops.Where(d=>d.Npc?.Actor==b.Serial.Value).ToArray(),Flights=flights,Leases=s.Leases.Where(l=>casts.Contains(l.Cast)).ToArray()};}
    private static FirePatch[] Fields(AssassinShadowBody b)=>FireGround.Snapshot().Patches.Where(p=>p.PhoenixNpc?.Actor==b.Serial.Value).OrderBy(p=>p.Id).ToArray();
    private static void Clear(AssassinShadowBody b)
    {
        var s=MartialPhoenix.Snapshot();var ids=s.Flights.Where(f=>f.Npc?.Actor==b.Serial.Value).Select(f=>f.Cast).ToHashSet();
        MartialPhoenix.Restore(s with{Drops=s.Drops.Where(d=>d.Npc?.Actor!=b.Serial.Value).ToArray(),Flights=s.Flights.Where(f=>f.Npc?.Actor!=b.Serial.Value).ToArray(),Leases=s.Leases.Where(l=>!ids.Contains(l.Cast)).ToArray()});
        var fields=FireGround.Snapshot();FireGround.Restore(fields with{Patches=fields.Patches.Where(p=>p.PhoenixNpc?.Actor!=b.Serial.Value).ToArray()});AssassinCharges.Clear(b);ShadowActions.Cancel(b);
    }
    private static MartialCharge Charge(AssassinShadowBody b,int count,DateTime at)=>new(b.Serial.Value,CombatAbility.PhoenixStrike,count,b.OwnedSkillRank(280),count*25,at.AddMinutes(1));
    private static long Emit(AssassinShadowBody b,BaseCreature t,int count,DateTime at)
    {
        var before=NpcEquipmentRandom.Require(b);ulong expected=before.Seed;_=NpcWorldAllocation.RequireStream();var game=NpcWorldAllocation.Capture();ulong world=game.Seed;
        if(count>1){SourceRandom.Roll(ref expected);}int children=count==1?1:count==2?7:16;for(int i=0;i<children;i++){SourceRandom.Roll(ref world);}
        long cast=MartialPhoenix.AfterShadow(b,t,t.Location,Charge(b,count,at),at);
        Require(cast>0&&NpcEquipmentRandom.Require(b).Seed==expected&&NpcWorldAllocation.Capture().Seed==world,"Own Phoenix caster/world seed allocation differs from source order");
        var s=Own(b);Require(count==1?s.Drops.Length==1&&s.Flights.Length==0:s.Drops.Length==0&&s.Flights.Length==children,"Wrong nonstacked Phoenix phase/count");
        Require(s.Flights.All(f=>f.Owner==b.ControlMaster.Serial.Value&&f.Npc.Actor==b.Serial.Value&&f.Npc.Binding==OriginalNpcEquipment.Binding(b)&&f.Npc.Pierce==f.Pierce),"Phoenix source/provenance borrowed player identity or pierce");return cast;
    }
    private static BaseCreature Target(AssassinShadowBody b)
    {var t=new ShadowPsychicChecks.Witness{CasterId=b.Serial.Value};t.SetHits(65000);foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}t.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static ulong HitSeed()
    {for(int i=1;i<10000;i++){ulong seed=SourceRandom.InitLow(i),copy=seed;if(SourceRandom.Limited(ref copy,100)<5){return seed;}}throw new InvalidOperationException("Missing minimum hit seed");}
    private static void Ai(AssassinShadowBody b,PlayerMobile owner,BaseCreature target,DateTime at)
    {
        Clear(b);NpcEquipmentRandom.Set(b,HitSeed());
        int frames=((INecromancerGroundBrain)b).GroundSkill(owner,5,280,target,at);
        Require(frames>0,"Own world Phoenix A2 request refused");ShadowActions.Tick(at.AddMilliseconds(frames*40),true,b.Serial.Value);
        Require(AssassinCharges.State(b,CombatAbility.PhoenixStrike)?.Count==1,"Own A2 world request did not build Phoenix charge");
        var prepared=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at.AddSeconds(2));
        if((prepared.Impact.Packet.ResultFlags&1)==0)
        {prepared.Cancel();NpcEquipmentRandom.Set(b,HitSeed());prepared=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at.AddSeconds(2));}
        Require(Own(b).Drops.Length==1&&AssassinCharges.State(b,CombatAbility.PhoenixStrike)==null,"World A2/basic release did not emit own meteor and consume one");
        _=IronGolemContact.Release(b,owner,target,prepared,_=>true,at.AddSeconds(2));Clear(b);
        if(b.Prepared.Capture.Ability!=CombatAbility.ShadowWarrior){return;}
        if(!ShadowGroundMotion.Active(b)){ShadowGroundMotion.Activate(b,owner,at.AddMinutes(1));}
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.PhoenixStrike)&&SkillBindings.Instance.Assign(owner,false,CombatAbility.PhoenixStrike),"Phoenix Warrior owner selection refused");
        NpcEquipmentRandom.Set(b,SourceRandom.InitLow(1234));bool builder=false,emitted=false;
        for(int i=0;i<800&&!(builder&&emitted);i++)
        {IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);Require(b.WorldGround.State.Controller.Fault==null,"Phoenix Warrior world fault: "+b.WorldGround.State.Controller.Fault);builder|=ShadowActions.State(b)?.Cast.Skill==280;var s=Own(b);emitted=s.Drops.Length+s.Flights.Length>0;}
        Require(builder&&emitted,"Own Warrior AI did not build/release Phoenix");Clear(b);
    }
    private static void Exercise(AssassinShadowBody b,PlayerMobile owner,int generation)
    {
        b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);b.ChangeSkills(book=>book.Assign(280,3));Clear(b);
        var t=Target(b);var at=TimedEffects.Now.AddMinutes(20+generation);int mana=NecromancerEffects.ManaBalance256(owner);var items=ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray();
        try
        {
            int hp=SkillHealth.Balance256(t);Emit(b,t,1,at);var caster=NpcEquipmentRandom.Require(b);MartialPhoenix.Tick(at.AddMilliseconds(2399),true,b.Serial.Value);
            Require(Own(b).Drops.Single().Frame==59&&SkillHealth.Balance256(t)==hp,"Own warning hit before60frames");
            MartialPhoenix.Tick(at.AddMilliseconds(2400),true,b.Serial.Value);var fields=Fields(b);
            Require(Own(b).Drops.Length==0&&fields.Length==18&&SkillHealth.Balance256(t)<hp&&NpcEquipmentRandom.Require(b)==caster,"Own meteor impact/18fields/source RNG failed");
            var first=fields.First(p=>b.Map.CanFit(new((int)p.X,(int)p.Y,p.Z),1,false,false,false));t.MoveToWorld(new((int)first.X,(int)first.Y,first.Z),b.Map);TimedEffects.Clear(t);hp=SkillHealth.Balance256(t);
            FireGround.Tick(first.NextUtc,true,b.Serial.Value);
            Require(SkillHealth.Balance256(t)<hp&&CreatureHitRecovery.Remaining(t)==0&&NpcEquipmentRandom.Require(b)==caster&&Fields(b).All(p=>p.PhoenixNpc.Fault==null),"Own field failed damage or added GETHIT/caster RNG");
            Clear(b);t.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);
            foreach(int count in new[]{2,3})
            {
                TimedEffects.Clear(t);hp=SkillHealth.Balance256(t);long cast=Emit(b,t,count,at);var f=Own(b).Flights[0];caster=NpcEquipmentRandom.Require(b);
                var next=MartialPhoenix.Impact(owner,t,f);
                Require(SkillHealth.Balance256(t)<hp&&next.Seed!=f.Seed&&NpcEquipmentRandom.Require(b)==caster&&!MartialPhoenix.Ready(cast,t.Serial.Value,f.NextUtc.AddMilliseconds(159))&&MartialPhoenix.Ready(cast,t.Serial.Value,f.NextUtc.AddMilliseconds(160)),"Own projectile damage/private RNG/shared four-frame lease failed");
                if(count==3){Require(TimedEffects.TryActor(t,out var status)&&status.Control?.Freeze?.Source==b.Serial.Value&&status.Cold==null,"Own chaos ice lost freeze-only source");}
                // Real movement/collision and damage through the shared pool.
                Clear(b);TimedEffects.Clear(t);hp=SkillHealth.Balance256(t);Emit(b,t,count,at);MartialPhoenix.Tick(at.AddMilliseconds(120),true,b.Serial.Value);
                Require(SkillHealth.Balance256(t)<hp&&Own(b).Flights.All(x=>x.Fault==null),"Own moving Phoenix flight did not collide/damage");Clear(b);
            }
            for(int count=1;count<=3;count++)
            {
                TimedEffects.Clear(t);var values=ShadowSkillCasting.Values(b,b.OwnedSkillRank(280),280);for(int n=0;n<count;n++){AssassinCharges.Gain(b,values,at);}
                NpcEquipmentRandom.Set(b,HitSeed());hp=SkillHealth.Balance256(t);var prepared=OriginalNpcMelee.Prepare(b,owner,t,_=>true,at);var own=Own(b);
                Require((prepared.Impact.Packet.ResultFlags&1)!=0&&SkillHealth.Balance256(t)==hp&&(AssassinCharges.State(b,CombatAbility.PhoenixStrike)?.Count??0)==count-1&&(count==1?own.Drops.Length==1:own.Flights.Length==(count==2?7:16)),"Own finisher did not emit correct tier before parent contact/consume-one");
                _=IronGolemContact.Release(b,owner,t,prepared,_=>true,at);Require(SkillHealth.Balance256(t)<hp,"Own Phoenix parent contact failed");Clear(b);
            }
            // Corruption must reject before replacing either shared pool.
            Emit(b,t,3,at);var good=MartialPhoenix.Snapshot();bool bad=false;
            try{MartialPhoenix.Restore(good with{Flights=good.Flights.Select(f=>f.Npc?.Actor==b.Serial.Value?f with{Npc=f.Npc with{Actor=owner.Serial.Value}}:f).ToArray()});}catch(InvalidDataException){bad=true;}
            Require(bad&&Json(MartialPhoenix.Snapshot())==Json(good),"Malformed NPC Phoenix restore replaced shared state");Clear(b);
            Emit(b,t,1,at);EmitMore(b,t,2,at);EmitMore(b,t,3,at);hp=SkillHealth.Balance256(t);caster=NpcEquipmentRandom.Require(b);
            MartialPhoenix.Tick(at.AddSeconds(3),false,b.Serial.Value);Require(Own(b).Drops.Length+Own(b).Flights.Length==0&&Fields(b).Length==0&&SkillHealth.Balance256(t)==hp&&NpcEquipmentRandom.Require(b)==caster,"Offline Phoenix fought or failed to expire");Clear(b);
            Ai(b,owner,t,at.AddSeconds(5));
            Require(NecromancerEffects.ManaBalance256(owner)==mana&&ShadowLoadout.Originals(b).Select(i=>i.SavedText).SequenceEqual(items),"Own Phoenix borrowed owner mana or changed original inventory");
        }
        finally{Clear(b);t.Delete();}
    }
    private static void EmitMore(AssassinShadowBody b,BaseCreature t,int count,DateTime at)
    {Require(MartialPhoenix.AfterShadow(b,t,t.Location,Charge(b,count,at),at)>0,"Additional own phase emission failed");}
    private static Held Save(AssassinShadowBody b,PlayerMobile owner,int generation)
    {
        b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);var t=Target(b);var at=TimedEffects.Now.AddMinutes(35+generation);EmitMore(b,t,1,at);EmitMore(b,t,2,at);EmitMore(b,t,3,at);
        var center=t.Location;t.MoveToWorld(new(b.X+20,b.Y,b.Z),b.Map);MartialPhoenix.Tick(at.AddMilliseconds(40),true,b.Serial.Value);
        var source=new PhoenixNpcSource(b.Serial.Value,OriginalNpcEquipment.Binding(b),NpcWorldAllocation.UnitSeed(),0);
        FireGround.AddShadowPhoenix(b,b.OwnedSkillRank(280),center.X,center.Y,center.Z,256,256,source,at);
        SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);GeneratedNpcLiveResources.Snapshot();PersistentMinions.Park(b);
        return Capture(b,t);
    }
    private static Held Capture(AssassinShadowBody b,BaseCreature t)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),t.Serial.Value,SkillHealth.Balance256(t),Own(b),Fields(b));
    private static void Check(bool reload)
    {
        PlayerMobile owner;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++)
            {
                var b=bodies[i];var h=saved.Bodies[i];var t=(BaseCreature)World.FindMobile((Serial)h.Target);var current=Capture(b,t);var expected=h.Projectiles with{NextId=current.Projectiles.NextId,NextCast=current.Projectiles.NextCast,Drops=h.Projectiles.Drops.Select(d=>d with{Npc=d.Npc with{Dormant=true}}).ToArray(),Flights=h.Projectiles.Flights.Select(f=>f with{Npc=f.Npc with{Dormant=true}}).ToArray()};
                Require(current.Body==h.Body&&current.Resources==h.Resources&&current.Items.SequenceEqual(h.Items)&&current.Random==h.Random&&current.Life==h.Life&&current.TargetLife==h.TargetLife&&Json(current.Projectiles)==Json(expected)&&Json(current.Fields)==Json(h.Fields.Select(p=>p with{PhoenixNpc=p.PhoenixNpc with{Dormant=true}}).ToArray()),"Reload lost original Phoenix actors/packets/paths/RNG/expiry or replay protection");
                var rng=NpcEquipmentRandom.Require(b);var world=NpcWorldAllocation.Capture();int hp=SkillHealth.Balance256(t);owner.MoveToWorld(new(6260,3760,0),Map.Trammel);b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);t.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);
                MartialPhoenix.Tick(current.Projectiles.Drops.Single().NextUtc.AddSeconds(3),true,b.Serial.Value);FireGround.Tick(current.Fields.Single().UntilUtc,true,b.Serial.Value);
                Require(Own(b).Drops.Length+Own(b).Flights.Length+Fields(b).Length==0&&SkillHealth.Balance256(t)==hp&&NpcEquipmentRandom.Require(b)==rng&&NpcWorldAllocation.Capture()==world,"Loaded Phoenix replayed children/damage/RNG or reset lifetime");t.Delete();
            }
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3760);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);ClassSkillChecks.Train(owner,"assassin.phoenixstrike",1);var world=NpcWorldAllocation.Capture();
            try{bodies=new AssassinShadowBody[2];int i=0;foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}){NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));bodies[i]=AssassinShadowBody.CreateInitialized(owner,ability,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(bodies[i]);ShadowEquipmentDefense.Activate(bodies[i]);i++;}}
            finally{NpcWorldAllocation.Restore(world);}
        }
        foreach(var b in bodies){Exercise(b,owner,generation);PersistentMinions.Park(b);}var held=bodies.Select(b=>Save(b,owner,generation)).ToArray();PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,held,generation)));

        Log.Information("Own Phoenix:three source-seeded emissions,60frame meteor/18fields/periodic no-GETHIT,7chain/16chaos shared moving collisions,four-frame leases/freeze-only,original finisher/A2/world/Warrior AI,no owner proxy,atomic malformed restore,offline/dormant loaded aging and original saved identity through reloads;other intrinsic/death/learned gates remain.");
    }
}
