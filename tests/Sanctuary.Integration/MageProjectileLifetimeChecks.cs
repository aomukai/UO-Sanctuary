// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;
public static class MageProjectileLifetimeChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(MageProjectileLifetimeChecks));
    private sealed record Saved(int Generation,uint Owner,uint Actor,uint Target,Point3D Origin,int Map,MageMissileState Shot,MageCriticalSeed Seed,int TargetLife,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"mage-projectile-lifetime-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"mage-projectile-lifetime-reload-check":"mage-projectile-lifetime-check";
            ConsoleInputHandler.RegisterCommand(name,"Approved original dead-Mage emitter/projectile lifetime",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>{try{if(reload){Reload(n);}Scene(n,!reload);Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static MageMissileState Shot(long id)=>NecromancerMageMissiles.Snapshot().Bolts.SingleOrDefault(b=>b.Id==id);
    private static ulong NoCritical(){for(ulong i=1;i<10000;i++){ulong n=i;if(SourceRandom.Roll(ref n)%100>=5){return i;}}throw new InvalidOperationException("Critical fixture seed");}
    private static BaseCreature Target(PlayerMobile p,NecromancerMinion mage,int dx=2)
    {
        var t=new MageMissileWitness{Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(mage.X+dx,mage.Y,mage.Z),p.Map);t.AIObject?.AITimer.Stop();t.SetHits(30000);t.Hits=t.HitsMax;
        foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static long Fire(NecromancerMinion m,Mobile t)=>NecromancerMageMissiles.LaunchPacket(m,t,TimedEffects.Now,new("firebolt","fire",1,4096,4096,0){Spell=CombatAbility.FireBolt});
    private static void Finish(long id,bool? active=null){for(int i=0;i<300&&Shot(id)is{} f;i++){NecromancerMageMissiles.Tick(f.NextUtc,active,id);}Require(Shot(id)==null,"Released Mage bolt did not expire");}
    private static void Clear(NecromancerMinion m)
    {
        FireGround.Cancel(m);ChainLightning.Cancel(m);var all=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(all with{Bolts=all.Bolts.Where(b=>b.Actor!=m.Serial.Value).ToArray()});MageProjectileRetirement.Cleanup();m.Delete();
    }
    private static void Scene(NetState network,bool retain)
    {
        var p=MagePetBuffChecks.Owner(20);var account=FixtureNetwork.Authenticate(network,p,"mage-lifetime-");bool kept=false;int cases=0;
        try
        {
            foreach(string route in new[]{"basic-fire","basic-cold","basic-lightning","legacy-poison","legacy-cold","legacy-fire","legacy-lightning","glacial","chain","meteor-warning","meteor-field"}){Exercise(route);}
            foreach(string mode in new[]{"logout","facet","owner-dead","suppressed"}){Restrict(mode);}
            Require(cases==15,"Incomplete Mage post-death matrix");
            var blank=NecromancerMageMissiles.Snapshot();string before=JsonSerializer.Serialize(blank);bool refused=false;
            try{NecromancerMageMissiles.Restore(blank with{RetiredEmitters=[new(1,1,p.Map.MapID,TimedEffects.Now)]});}catch(InvalidDataException){refused=true;}
            Require(refused&&JsonSerializer.Serialize(NecromancerMageMissiles.Snapshot())==before,"Invalid retirement partially restored");
            if(retain){Persist(p);kept=true;}
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){p.Delete();}}
        void Exercise(string route)
        {
            int element=route.Contains("cold")||route=="glacial"?1:route.Contains("lightning")||route=="chain"?3:2;
            var mage=MagePetBuffChecks.Pet(p,element);var target=Target(p,mage);long id=0;int before=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(p),events=0;string trace="";bool original=false;uint actor=mage.Serial.Value;
            try
            {
                NecromancerMageMissiles.SetActorSeed(mage,NoCritical());
                if(route.StartsWith("basic-")){id=NecromancerMageMissiles.Launch(mage,target,TimedEffects.Now);}
                else if(route.StartsWith("legacy-")){int e=route=="legacy-poison"?0:element;id=NecromancerMageMissiles.LaunchPacket(mage,target,TimedEffects.Now,NecromancerMageMissiles.Packet(e,20));}
                else if(route=="glacial"){id=NecromancerMageMissiles.LaunchGlacial(mage,target,TimedEffects.Now);}
                else if(route=="chain"){id=ChainLightning.CastNpc(mage,target.Location,MagePetBuffs.Values(CombatAbility.ChainLightning,10,mage.Level),TimedEffects.Now);}
                else
                {
                    id=FireGround.CastNpcMeteor(mage,target,TimedEffects.Now);Require(id>0,"Mage Meteor release");
                    if(route=="meteor-field"){var warning=FireGround.Snapshot().Meteors.Single(x=>x.Id==id);FireGround.Tick(warning.ImpactUtc,null,actor);before=SkillHealth.Balance256(target);Require(FireGround.Snapshot().Patches.Any(x=>x.NpcCaster==actor),"Meteor created no existing fire");}
                }
                Require(id>0,"Mage emission refused:"+route);var captured=NecromancerMageMissiles.Snapshot().Bolts.Where(x=>x.Actor==actor).ToArray();mage.Kill();
                Require(!mage.Alive&&!mage.Deleted&&SkillHealth.Balance256(mage)==0&&MageProjectileRetirement.Retiring(mage)&&World.FindMobile((Serial)actor)==mage,"Original dead Mage not retained:"+route);
                Require(captured.SequenceEqual(NecromancerMageMissiles.Snapshot().Bolts.Where(x=>x.Actor==actor))&&NecromancerMageMissiles.Launch(mage,target,TimedEffects.Now)==0,"Death changed captured shots or emitted a new volley");
                void Observe(SkillDamageContact c){if(c.Target==target){events++;original|=c.Source==mage&&!mage.Alive&&mage.GetDamageMaster(target)==p;}}
                SkillDamageEvents.BeforeDirectDamage+=Observe;
                try
                {
                    if(route=="chain")
                    {
                        Require(MageProjectileRetirement.Fighting(mage,p,mage.Map.MapID,null)&&NecromancerMageMissiles.ActorSeed(mage)?.Owner==p.Serial.Value,"Retired Chain admission/seed");
                        for(int n=0;n<200&&ChainLightning.Snapshot().SingleOrDefault(x=>x.Cast==id)is{} f;n++)
                        {if(n<8){trace+=$"f={f.Frame},xy={f.X},{f.Y},target={target.Location},range={f.Min256}-{f.Max256};";}ChainLightning.Tick(f.NextUtc,null,id);}
                    }
                    else if(route.StartsWith("meteor-"))
                    {
                        if(route=="meteor-warning"){var warning=FireGround.Snapshot().Meteors.Single(x=>x.Id==id);FireGround.Tick(warning.ImpactUtc,null,actor);}
                        else{var patch=FireGround.Snapshot().Patches.First(x=>x.NpcCaster==actor);FireGround.Tick(patch.NextUtc,null,actor);}
                    }
                    else{for(int n=0;n<200&&NecromancerMageMissiles.Snapshot().Bolts.Any(x=>x.Actor==actor);n++){var f=NecromancerMageMissiles.Snapshot().Bolts.First(x=>x.Actor==actor);NecromancerMageMissiles.Tick(f.NextUtc,null);}}
                }
                finally{SkillDamageEvents.BeforeDirectDamage-=Observe;}
                Require(before>SkillHealth.Balance256(target)&&NecromancerEffects.ManaBalance256(p)==mana&&(route=="legacy-poison"||events>0&&original)
                    &&(before-SkillHealth.Balance256(target)<256||Credited(target,mage,p)),$"Released {route} lost damage/credit:loss={before-SkillHealth.Balance256(target)},events={events},source={original},trace={trace}");cases++;
            }
            finally{Clear(mage);GearEffects.Clear(target);target.Delete();}
        }
        void Restrict(string mode)
        {
            var mage=MagePetBuffChecks.Pet(p,2);var target=Target(p,mage);var origin=p.Location;var map=p.Map;long id=Fire(mage,target);mage.Kill();int hp=SkillHealth.Balance256(target);var net=p.NetState;
            try
            {
                if(mode=="logout"){p.NetState=null;network.Mobile=null;}if(mode=="facet"){p.MoveToWorld(origin,Map.Felucca);}if(mode=="owner-dead"){p.Kill();}
                Finish(id,mode=="suppressed"?false:null);Require(SkillHealth.Balance256(target)==hp&&mage.Deleted,"Restricted retired Mage fought or kept an expired source:"+mode);cases++;
            }
            finally{if(!p.Alive){p.Corpse?.Delete();p.Resurrect();}p.MoveToWorld(origin,map);p.NetState=net;network.Mobile=p;Clear(mage);target.Delete();}
        }
    }
    private static bool Credited(BaseCreature target,Mobile source,PlayerMobile owner)
        =>target.FindDamageEntryFor(source)?.Responsible?.Any(r=>r.Damager==owner&&r.DamageGiven>0)==true;
    private static void Persist(PlayerMobile p)
    {
        var mage=MagePetBuffChecks.Pet(p,2);var target=Target(p,mage,8);NecromancerMageMissiles.SetActorSeed(mage,NoCritical());long id=Fire(mage,target);mage.Kill();Hold(id);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,mage.Serial.Value,target.Serial.Value,p.Location,p.Map.MapID,Shot(id),NecromancerMageMissiles.ActorSeed(mage),SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(p))));
    }
    private static void Hold(long id)
    {var all=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(all with{Bolts=all.Bolts.Select(x=>x.Id==id?x with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}:x).ToArray()});}
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var mage=(NecromancerMinion)World.FindMobile((Serial)s.Actor);var target=(BaseCreature)World.FindMobile((Serial)s.Target);var account=FixtureNetwork.Authenticate(network,p,"mage-lifetime-reload-");
        try
        {
            Require(mage!=null&&!mage.Alive&&!mage.Deleted&&SkillHealth.Balance256(mage)==0&&Shot(s.Shot.Id)==s.Shot&&NecromancerMageMissiles.ActorSeed(mage)==s.Seed&&SkillHealth.Balance256(target)==s.TargetLife&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Saved original dead Mage/shot/seed/life/resources changed");p.MoveToWorld(s.Origin,Map.Maps[s.Map]);
            if(s.Generation==0)
            {for(int n=0;n<2;n++){var f=Shot(s.Shot.Id);NecromancerMageMissiles.Tick(f.NextUtc,null,f.Id);}Require(Shot(s.Shot.Id)?.Frame==s.Shot.Frame+2&&SkillHealth.Balance256(target)==s.TargetLife,"Reload advanced/replayed impact");Hold(s.Shot.Id);File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=1,Shot=Shot(s.Shot.Id)}));}
            else
            {Finish(s.Shot.Id);Require(s.TargetLife-SkillHealth.Balance256(target)==4096&&mage.Deleted,"Original retained Mage did not land once after two reloads");File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=2}));target.Delete();}
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
