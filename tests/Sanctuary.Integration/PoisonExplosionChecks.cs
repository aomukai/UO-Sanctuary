// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class PoisonExplosionChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PoisonExplosionChecks));
    private sealed record Saved(uint Owner,uint Corpse,uint Gold,Point3D Point,int Map,NecromancerSpellFlight[] Clouds,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"poison-explosion-fixtures.json");
    public static void Configure()
    {
        Register("poison-explosion-check",Check);Register("poison-explosion-reload-check",Reload);
        foreach(bool quick in new[]{false,true}){bool q=quick;Register(q?"poison-explosion-quick-check":"poison-explosion-right-check",()=>FixtureNetwork.Connected(q?"poison-explosion-quick-result":"poison-explosion-right-result",n=>Input(n,q)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Poison Explosion moving clouds and corpse transaction",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureExplosion";
        void Train(string id){if(ClassCreation.Profiles.GetRank(p.Serial,id)>0){return;}TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string pre in row.Prerequisites){Train(pre);}Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
        Train("necromancer.poisonexplosion");Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.PoisonExplosion)&&SkillBindings.Instance.Assign(p,false,CombatAbility.PoisonExplosion)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.PoisonExplosion),"Explosion right/quick bindings");return p;
    }
    private static Corpse Corpse(PlayerMobile p,out Gold gold)
    {
        var mob=new GraveyardZombie{NoKillAwards=true};mob.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);mob.Damage(10000,p);if(mob.Alive){mob.Kill();}
        var corpse=mob.Corpse as Corpse;Require(SkeletonPets.Usable(p,corpse),"Explosion actual corpse");gold=new Gold(273);corpse.DropItem(gold);return corpse;
    }
    private static GraveyardSkeleton Target(PlayerMobile p,int x,int y=0,int resistance=0)
    {
        var t=PoisonDaggerChecks.Target(p,resistance);t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);
        foreach(var type in new[]{ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy}){t.SetResistance(type,0);}
        TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static NecromancerSpellFlight[] Clouds(PlayerMobile p)=>NecromancerSpellMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).OrderBy(x=>x.Id).ToArray();
    private static TimedEffectState Effect(Mobile t)=>TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value);
    private static void Through(PlayerMobile p,int frames,bool actions=true)
    {for(int i=0;i<frames&&Clouds(p).Length>0;i++){NecromancerSpellMissiles.Tick(Clouds(p).Max(x=>x.NextUtc),actions);}}
    private static void Clean(PlayerMobile p,Corpse corpse=null,params Mobile[] targets)
    {NecromancerSpellMissiles.Cancel(p);AmazonSlowMissiles.Clear(p);foreach(var t in targets){t.Corpse?.Delete();t.Delete();}corpse?.Delete();p.Corpse?.Delete();p.Delete();}
    private static void Cast(PlayerMobile p,Corpse corpse)
    {
        PoisonDaggerChecks.Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p);
        Require(NecromancerCombat.Begin(p,CombatAbility.PoisonExplosion,corpse.Serial,corpse.Location,Core.TickCount),"Explosion corpse windup");var session=ClassCombat.For(p);
        Require(!corpse.SanctuaryConsumed&&NecromancerEffects.ManaBalance256(p)==mana&&!ClassCombat.ResolveAction(p,session.EffectAt-1),"Explosion consumed/paid early");
        Require(ClassCombat.ResolveAction(p,session.EffectAt)&&!ClassCombat.ResolveAction(p,session.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-8*256&&Clouds(p).Length==8,"Explosion once-only8mana/eight clouds");
        Require(corpse.SanctuaryConsumed&&corpse.Animated&&CorpseSkillRules.NoSelect(corpse),"Consumed corpse metadata/state");
    }
    private static void Source()
    {
        using var damage=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"poison-explosion-damage-source-cases.json")));int count=0;
        foreach(var r in damage.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();var values=new SkillValues(SkillMechanics.For(CombatAbility.PoisonExplosion),k=>k=="Poison Explosion"?V(0):255,k=>k=="Poison Dagger"?Math.Min(20,V(1)):k=="Poison Nova"?Math.Max(0,V(1)-20):0,99);
            var ends=NecromancerSpellMissiles.Endpoints(values,V(3),SlowMissilePayload.Normal);int calls=0;int roll=WeaponMissileDamage.RollValue(ends.Min,ends.Max,0,n=>{calls++;return n-1;});
            Require(ends==(V(7),V(8))&&values.DurationFrames==V(9)&&roll==V(13)&&calls==V(14)&&V(5)==0&&V(6)==0&&V(10)==0&&V(11)==0&&V(12)==0,"Explosion packet/source context "+count);count++;
        }
        Require(count==12240,"Missing Explosion packet contexts");
        using var emitter=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"poison-explosion-emitter-source-cases.json")));int directions=0;
        foreach(var r in emitter.RootElement.GetProperty("emissions").EnumerateArray())
        {
            var pattern=PoisonCloudGeometry.Pattern(2,0,0);int i=0;foreach(var shot in r[10].EnumerateArray())
            {var ray=pattern[i++];Require(ray.X==shot[3].GetInt32()&&ray.Y==shot[4].GetInt32()&&ray.Flags==shot[0].GetInt32()&&shot[5].GetInt32()==256&&shot[6].GetInt32()==0,"Explosion shared cloud geometry/factory");directions++;}
            if(i>0){Require(i==8,"Incomplete cloud emission");}
        }
        Require(directions==144,"Missing source allocation projections");
    }
    private static void Emission()
    {
        var p=Owner();var corpse=Corpse(p,out var gold);try
        {
            var point=corpse.Location;Cast(p,corpse);var clouds=Clouds(p);var pattern=PoisonCloudGeometry.Pattern(2,0,0);
            Require(clouds.Select(x=>x.RadialIndex).SequenceEqual(Enumerable.Range(0,8))&&gold.Parent==corpse&&gold.Amount==273,"Cloud order/loot retention");
            foreach(var s in clouds)
            {
                var d=pattern[s.RadialIndex];var v=MissileVectors.VelocityFp8(d.X,d.Y,192);
                Require(s is{Frame:0,Frames:60,ElementFrames:50,VelocityFp8:192,Flags:23,Cast:0}&&s.X==point.X&&s.Y==point.Y&&s.Z==point.Z&&s.Vx==v.X&&s.Vy==v.Y,"Actual corpse-origin cloud factory");
            }
            var projections=MissileFootprints.Snapshot().Where(x=>x.Source=="poisonexplosioncloud"&&x.Map==p.Map.MapID&&x.X==point.X*2&&x.Y==point.Y*2).ToArray();Require(projections.Length==8,"Saved clouds missing Collision1 projection");
            int mana=NecromancerEffects.ManaBalance256(p);Require(!NecromancerCombat.Release(p,CombatAbility.PoisonExplosion,corpse.Serial,corpse.Location)&&mana==NecromancerEffects.ManaBalance256(p)&&Clouds(p).SequenceEqual(clouds),"Consumed corpse reused/paid");
            Through(p,20);Require(Clouds(p).Length==8&&Clouds(p).Any(x=>x.X!=point.X||x.Y!=point.Y),"Clouds are stationary");
            Through(p,39,false);Require(Clouds(p).Length==8&&Clouds(p).All(x=>x.Frame==59),"Cloud expired before60frames");Through(p,1,false);Require(Clouds(p).Length==0,"Cloud survived60frames");
        }finally{Clean(p,corpse);}
    }
    private static void Single(PlayerMobile p,int index=2)
    {var all=NecromancerSpellMissiles.Snapshot();NecromancerSpellMissiles.Restore(all with{Flights=all.Flights.Where(x=>x.Owner!=p.Serial.Value||x.RadialIndex==index).ToArray()});}
    private static void Contact()
    {
        foreach(bool immune in new[]{false,true})
        {
            var p=Owner();var corpse=Corpse(p,out _);var t=Target(p,5,resistance:immune?100:0);try
            {
                Cast(p,corpse);Single(p);Through(p,20);Require(Effect(t).SpellPoison==null,"Cloud applied old fixed-radius poison before travel");
                while(Clouds(p).Length>0&&Effect(t).SpellPoison==null){Through(p,1);}var state=Effect(t);var exposure=state.SpellPoison;
                Require(exposure!=null&&Clouds(p).Single().LastCollider==t.Serial.Value&&Clouds(p).Single().Frame<60,"Moving cloud missed eligible enemy");
                var original=Clouds(p).Single();Through(p,1);Require(Clouds(p).Single().LastCollider==t.Serial.Value&&Effect(t).PoisonMs==state.PoisonMs&&Clouds(p).Single().Seed==original.Seed,"Cloud repeatedly hit LastCollide");
                NecromancerSpellMissiles.Cancel(p);int before=SkillHealth.Balance256(t);var at=state.UpdatedUtc;Require(CombatPotions.Apply(p,3,at),"Explosion late potion");TimedEffects.Settle(t,at.AddMilliseconds(40));
                int extra=exposure.RawRate256/10,expected=immune?extra*3:exposure.RawRate256+extra*4;
                Require(before-SkillHealth.Balance256(t)==expected&&CreatureHitRecovery.Remaining(t)==0,"Explosion tick/potion/immunity/GetHit");
                Require(TimedEffects.CleansePoison(t,0,at.AddMilliseconds(40)),"Explosion cure");before=SkillHealth.Balance256(t);TimedEffects.Settle(t,at.AddSeconds(5));Require(before==SkillHealth.Balance256(t)&&Effect(t).SpellPoison==null,"Cure left Explosion tick additions");
            }finally{Clean(p,corpse,t);}
        }
        var owner=Owner();var c=Corpse(owner,out _);var a=Target(owner,4);var b=Target(owner,4);try
        {
            Cast(owner,c);Single(owner);Through(owner,1);Require(Clouds(owner).Single().LastCollider==a.Serial.Value,"Cloud candidate order");
            Through(owner,1);Require(Clouds(owner).Single().LastCollider==b.Serial.Value,"Latest-collider exclusion became a permanent target set");
            Through(owner,1);Require(Clouds(owner).Single().LastCollider==a.Serial.Value,"Cloud failed prior-recipient rehit after another collider");
        }finally{Clean(owner,c,a,b);}
    }
    private static void Controls()
    {
        foreach(string mode in new[]{"offline","facet","death","respec","wardrobe","wall","slow","zero"})
        {
            var p=Owner();var corpse=Corpse(p,out _);var t=Target(p,5);Item wall=null;try
            {
                if(mode is "slow" or "zero"){int percent=mode=="zero"?0:50;AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,t.Serial.Value,1,percent,percent,TimedEffects.Now.AddHours(1))).ToArray());}
                Cast(p,corpse);Single(p);var first=Clouds(p).Single();
                if(mode is "slow" or "zero")
                {
                    Require(first.VelocityFp8==(mode=="zero"?0:96),"Cloud captured movement slow");AmazonSlowMissiles.Clear(p);Through(p,59);Require(Effect(t).SpellPoison==null&&Clouds(p).Length==1,"Slow cloud unexpectedly reached distant target");Through(p,1);
                }
                if(mode=="offline"){Through(p,60,false);}
                if(mode=="facet"){p.MoveToWorld(p.Location,Map.Felucca);Through(p,1);}
                if(mode=="death"){p.Kill();}
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);}
                if(mode=="wardrobe"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _),"Explosion wardrobe");}
                if(mode=="wall"){wall=new Static(0x80);wall.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);Through(p,60);}
                Require(Clouds(p).Length==0&&Effect(t).SpellPoison==null,"Cloud lifecycle/terrain/slow "+mode);
            }finally{wall?.Delete();Clean(p,corpse,t);}
        }
        var owner=Owner();var body=Corpse(owner,out var loot);try
        {
            NecromancerEffects.SetMana256(owner,0);Require(!NecromancerCombat.Release(owner,CombatAbility.PoisonExplosion,body.Serial,body.Location)&&!body.SanctuaryConsumed&&Clouds(owner).Length==0&&loot.Parent==body,"Unpaid Explosion consumed corpse");
            body.SanctuaryRestInPeace=true;NecromancerEffects.SetMana256(owner,owner.ManaMax*256);Require(!NecromancerCombat.Release(owner,CombatAbility.PoisonExplosion,body.Serial,body.Location)&&!body.SanctuaryConsumed,"RIP bypass");body.SanctuaryRestInPeace=false;
            var helmet=PoisonDaggerChecks.Named(owner,"Wormskull");Cast(owner,body);var captured=Clouds(owner);Require(captured.All(x=>x.Rank==2&&x.ElementFrames==60),"Captured soft rank/poison duration");
            owner.AddToBackpack(helmet);ClassGear.Refresh(owner);Require(Clouds(owner).SequenceEqual(captured),"Cloud packet reread live gear");
            var all=NecromancerSpellMissiles.Snapshot();var first=captured.First();
            foreach(var bad in new[]{first with{Flags=31},first with{Frames=87},first with{ElementFrames=50},first with{RadialIndex=8},first with{Cast=first.Id}})
            {
                try{NecromancerSpellMissiles.Restore(all with{Flights=all.Flights.Select(x=>x.Id==first.Id?bad:x).ToArray()});throw new InvalidOperationException("Invalid cloud save accepted");}catch(InvalidDataException){}
                Require(NecromancerSpellMissiles.State(first.Id)==first,"Malformed restore replaced cloud pool");
            }
        }finally{Clean(owner,body);}
    }
    private static void Hold(PlayerMobile p)
    {var all=NecromancerSpellMissiles.Snapshot();var shift=TimedEffects.Now.Add(FixtureSpace.Retention)-Clouds(p).Min(x=>x.NextUtc);NecromancerSpellMissiles.Restore(all with{Flights=all.Flights.Select(x=>x.Owner==p.Serial.Value?x with{NextUtc=x.NextUtc+shift}:x).ToArray()});}
    private static void Check()
    {
        Source();Emission();Contact();Controls();var p=Owner();var corpse=Corpse(p,out var gold);Cast(p,corpse);Through(p,17);Hold(p);
        var saved=new Saved(p.Serial.Value,corpse.Serial.Value,gold.Serial.Value,p.Location,p.Map.MapID,Clouds(p),0);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
        Log.Information("Poison Explosion:12240source rate/length packets,432emitter/factory contexts/144allocation projections;8moving60frame clouds,8mana/corpse/retained loot,collision/latest identity,immune/cure/potion ticks,8lifecycle/slow controls and atomic saves pass");
    }
    private static void Reload()
    {
        Source();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var corpse=(Corpse)World.FindItem((Serial)saved.Corpse);var gold=World.FindItem((Serial)saved.Gold);
        Require(Clouds(p).SequenceEqual(saved.Clouds)&&corpse is{SanctuaryConsumed:true,Animated:true}&&CorpseSkillRules.NoSelect(corpse)&&gold is Gold{Amount:273}&&gold.Parent==corpse,"Saved moving clouds/corpse reservation/loot");
        Require(SkillBindings.Instance.Get(p).Right==CombatAbility.PoisonExplosion&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Necromancer,7)==CombatAbility.PoisonExplosion,"Saved Explosion bindings");
        p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);Through(p,1);Require(Clouds(p).Zip(saved.Clouds).All(x=>x.First.Frame==x.Second.Frame+1&&x.First.X==x.Second.X+x.Second.Vx&&x.First.Y==x.Second.Y+x.Second.Vy),"Loaded cloud motion did not resume once");
        Hold(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Clouds=Clouds(p),Generation=saved.Generation+1}));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var corpse=Corpse(p,out var gold);var t=Target(p,4);PoisonDaggerChecks.Ready(p);int mana=NecromancerEffects.ManaBalance256(p);string label=quick?"poison-explosion-quick":"poison-explosion-right";
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false;long deadline=Core.TickCount+6000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Explosion input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.PoisonExplosion&&!began){Require(!corpse.SanctuaryConsumed&&NecromancerEffects.ManaBalance256(p)==mana,"Input consumed/paid before release");began=true;Log.Information("{Label}: windup",label);}
                if(Effect(t).SpellPoison==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&corpse.SanctuaryConsumed&&gold.Parent==corpse&&Clouds(p).Length==8&&NecromancerEffects.ManaBalance256(p)==mana-8*256,"Real corpse input/payment/cloud contact");Clean(p,corpse,t);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Clean(p,corpse,t);Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.PoisonExplosion,(int)DiabloClass.Necromancer,corpse.Serial.Value,corpse.X,corpse.Y,corpse.Z);Inspect();});
    }
}
