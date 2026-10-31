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

public static class PoisonNovaChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PoisonNovaChecks));
    private sealed record Saved(uint Owner,uint Target,Point3D Point,int Map,NecromancerSpellFlight[] Flights,int Balance,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"poison-nova-fixtures.json");
    public static void Configure()
    {
        Register("poison-nova-check",Check);Register("poison-nova-reload-check",Reload);
        foreach(bool quick in new[]{false,true}){bool q=quick;Register(q?"poison-nova-quick-check":"poison-nova-right-check",()=>FixtureNetwork.Connected(q?"poison-nova-quick-result":"poison-nova-right-result",n=>Input(n,q)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Poison Nova saved ring and pure poison tick delivery",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureNova";
        void Train(string id){if(ClassCreation.Profiles.GetRank(p.Serial,id)>0){return;}TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string pre in row.Prerequisites){Train(pre);}Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
        Train("necromancer.poisonnova");Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.PoisonNova)&&SkillBindings.Instance.Assign(p,false,CombatAbility.PoisonNova)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.PoisonNova),"Nova bindings");return p;
    }
    private static GraveyardSkeleton Target(PlayerMobile p,int dx,int dy=0,int resistance=0)
    {
        var t=PoisonDaggerChecks.Target(p,resistance);t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);
        foreach(var type in new[]{ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy}){t.SetResistance(type,0);}
        TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static NecromancerSpellFlight[] Flights(PlayerMobile p)=>NecromancerSpellMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).OrderBy(x=>x.Id).ToArray();
    private static TimedEffectState Effect(Mobile t)=>TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value);
    private static void Through(PlayerMobile p,int frames,bool actions=true)
    {for(int i=0;i<frames&&Flights(p).Length>0;i++){NecromancerSpellMissiles.Tick(Flights(p).Max(x=>x.NextUtc),actions);}}
    private static void Clean(PlayerMobile p,params Mobile[] targets)
    {NecromancerSpellMissiles.Cancel(p);AmazonSlowMissiles.Clear(p);foreach(var t in targets){t.Corpse?.Delete();t.Delete();}p.Corpse?.Delete();p.Delete();}
    private static void Cast(PlayerMobile p)
    {
        PoisonDaggerChecks.Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p);
        Require(ClassCombat.Perform(p,CombatAbility.PoisonNova,null,new(p.X+3,p.Y,p.Z),Core.TickCount),"Nova windup");var session=ClassCombat.For(p);
        Require(session.PendingPoint==p.Location&&session.PendingTarget==Serial.Zero&&NecromancerEffects.ManaBalance256(p)==mana&&!ClassCombat.ResolveAction(p,session.EffectAt-1),"Nova self origin/early payment");
        Require(ClassCombat.ResolveAction(p,session.EffectAt)&&!ClassCombat.ResolveAction(p,session.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-20*256&&Flights(p).Length==64,"Nova once-only20mana/ring release");
    }
    private static void Source()
    {
        using var damage=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"poison-nova-damage-source-cases.json")));int count=0;
        foreach(var r in damage.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();var values=new SkillValues(SkillMechanics.For(CombatAbility.PoisonNova),k=>k=="Poison Nova"?V(0):255,k=>k=="Poison Dagger"?Math.Min(20,V(1)):k=="Poison Explosion"?Math.Max(0,V(1)-20):0,99);
            var ends=NecromancerSpellMissiles.Endpoints(values,V(3),SlowMissilePayload.Normal);int calls=0;int roll=WeaponMissileDamage.RollValue(ends.Min,ends.Max,0,n=>{calls++;return n-1;});
            Require(ends==(V(7),V(8))&&values.DurationFrames==V(9)&&roll==V(13)&&calls==V(14)&&V(5)==0&&V(6)==0&&V(10)==0&&V(11)==0&&V(12)==0,"Nova packet/source context "+count);count++;
        }
        Require(count==12240,"Missing Nova packet contexts");
        using var ring=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"poison-nova-emitter-source-cases.json")));int directions=0;
        foreach(var r in ring.RootElement.EnumerateArray())
        {
            int i=0;foreach(var shot in r[8].EnumerateArray())
            {var d=FrozenOrbs.Direction(i++);Require(d.X==shot[3].GetInt32()-400&&d.Y==shot[4].GetInt32()-700&&shot[5].GetInt32()==2304&&shot[6].GetInt32()==30,"Nova shared integer direction/factory");directions++;}
            if(i>0){Require(i==64,"Incomplete ring");}
        }
        Require(directions==384,"Missing emitter source projections");
    }
    private static void Ring()
    {
        var p=Owner();try
        {
            Cast(p);var ring=Flights(p);Require(ring.Select(x=>x.Cast).Distinct().Count()==1&&ring.Select(x=>x.RadialIndex).SequenceEqual(Enumerable.Range(0,64)),"Nova cast/radial identities");
            foreach(var s in ring)
            {
                var d=FrozenOrbs.Direction(s.RadialIndex);var vector=MissileVectors.VelocityFp8(d.X,d.Y,2304);
                Require(s is{Frames:30,Frame:0,ElementFrames:50,Flags:7,VelocityFp8:2304}&&s.X==p.X&&s.Y==p.Y&&s.Vx==vector.X&&s.Vy==vector.Y&&s.LastCollider==p.Serial.Value,"Nova saved source ray");
            }
            Through(p,4);Require(Flights(p).Length==64,"Initial radial rays collided in cleared origin");Through(p,25,false);Require(Flights(p).Length==64,"Nova expired before30frames");Through(p,1,false);Require(Flights(p).Length==0,"Nova lifetime exceeds30frames");
            AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,p.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());
            var values=SkillMechanics.Values(p,CombatAbility.PoisonNova);var full=NecromancerSpellMissiles.Endpoints(values,0,SlowMissilePayload.Normal);
            NecromancerSpellMissiles.CreateRing(p,values,TimedEffects.Now);Require(Flights(p).All(x=>x.VelocityFp8==2304&&x.Slow.VelocityPercent==100&&x.Slow.DamagePercent==50&&x.Minimum256==full.Min/2),"Nova speed immunity and independent captured damage slow");
        }finally{Clean(p);}
    }
    private static void HitAndTicks()
    {
        foreach(bool immune in new[]{false,true})
        {
            var p=Owner();var t=Target(p,4,resistance:immune?100:0);try
            {
                Cast(p);int before=SkillHealth.Balance256(t);
                while(Flights(p).Length>0&&!TimedEffects.Snapshot().Any(x=>x.Owner==t.Serial.Value&&x.SpellPoison!=null)){Through(p,1);}
                var state=Effect(t);var exposure=state.SpellPoison;Require(exposure!=null&&exposure.RawRate256>=Flights(p).First().Minimum256&&state.PoisonMs==(immune?0:2000),"Nova contact omitted exposure/length");
                Require(before-SkillHealth.Balance256(t)==exposure.Rate256&&CreatureHitRecovery.Remaining(t)==0,"Nova initial-rate bridge/get-hit exclusion");
                NecromancerSpellMissiles.Cancel(p);var at=state.UpdatedUtc;int life=SkillHealth.Balance256(t);
                Require(CombatPotions.Apply(p,3,at),"Late Nova potion");TimedEffects.Settle(t,at.AddMilliseconds(40));
                int extra=exposure.RawRate256/10;
                int expected=immune?3*extra:exposure.RawRate256+4*extra;
                Require(life-SkillHealth.Balance256(t)==expected,"Nova actual exposure tick/potion/immunity");
                Require(TimedEffects.CleansePoison(t,0,at.AddMilliseconds(40)),"Nova cure");life=SkillHealth.Balance256(t);TimedEffects.Settle(t,at.AddSeconds(3));Require(life==SkillHealth.Balance256(t)&&Effect(t).SpellPoison==null,"Nova bonus survived cure");
            }finally{Clean(p,t);}
        }
        var owner=Owner();var near=Target(owner,3);var far=Target(owner,6);try
        {
            Cast(owner);
            // Isolate the central ray. Other ring children may correctly skip
            // a NextHit-delayed near target and reach the farther enemy.
            var pool=NecromancerSpellMissiles.Snapshot();NecromancerSpellMissiles.Restore(pool with{Flights=pool.Flights.Where(x=>x.Owner!=owner.Serial.Value||x.RadialIndex==0).ToArray()});
            Through(owner,30);Require(Effect(near).SpellPoison!=null&&Effect(far).SpellPoison==null,"One Nova ray pierced instead of contact removal");
        }finally{Clean(owner,near,far);}
    }
    private static void Gear()
    {
        var p=Owner();try
        {
            var helm=PoisonDaggerChecks.Named(p,"Wormskull");var values=SkillMechanics.Values(p,CombatAbility.PoisonNova);
            Require(values.Rank==2&&NamedGear.PoisonSources(p).Sum(x=>x.Minimum)==102,"Nova real soft-rank/item poison control");
            var ends=NecromancerSpellMissiles.Endpoints(values,0,SlowMissilePayload.Normal);Cast(p);var captured=Flights(p);
            Require(captured.All(x=>x.Rank==2&&x.Minimum256==ends.Min&&x.Maximum256==ends.Max&&x.ElementFrames==50),"Item poison leaked into SrcDam0/rank capture");
            p.AddToBackpack(helm);ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,CombatAbility.PoisonNova)==1&&Flights(p).SequenceEqual(captured),"Live gear changed an already emitted ring");
            NecromancerSpellMissiles.Cancel(p);p.MoveToWorld(new(1497,1628,10),Map.Trammel);NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p);
            Require(Server.Spells.SpellHelper.IsTown(p.Location,p)&&!NecromancerCombat.Release(p,CombatAbility.PoisonNova,Serial.Zero,p.Location)&&mana==NecromancerEffects.ManaBalance256(p)&&Flights(p).Length==0,"Town ring spent mana/created rays");
        }finally{Clean(p);}
    }
    private static void Controls()
    {
        foreach(string mode in new[]{"offline","facet","death","respec","wardrobe","wall"})
        {
            var p=Owner();var t=Target(p,5);Item wall=null;try
            {
                Cast(p);if(mode=="offline"){Through(p,30,false);}
                if(mode=="facet"){p.MoveToWorld(p.Location,Map.Felucca);Through(p,1);}
                if(mode=="death"){p.Kill();}
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);}
                if(mode=="wardrobe"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _),"Nova wardrobe");}
                if(mode=="wall"){wall=new Static(0x80);wall.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);Through(p,30);}
                Require(Flights(p).Length==0&&Effect(t).SpellPoison==null,"Nova lifecycle "+mode);
            }finally{wall?.Delete();Clean(p,t);}
        }
        var owner=Owner();try
        {
            NecromancerEffects.SetMana256(owner,0);Require(!NecromancerCombat.Release(owner,CombatAbility.PoisonNova,Serial.Zero,owner.Location)&&Flights(owner).Length==0,"Unpaid Nova ring");
            NecromancerSpellMissiles.CreateRing(owner,SkillMechanics.Values(owner,CombatAbility.PoisonNova),TimedEffects.Now);var pool=NecromancerSpellMissiles.Snapshot();var first=Flights(owner).First();
            foreach(var bad in new[]{first with{ElementFrames=0},first with{RadialIndex=64},first with{Flags=33},first with{VelocityFp8=1152}})
            {
                try{NecromancerSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Id==first.Id?bad:x).ToArray()});throw new InvalidOperationException("Invalid Nova save accepted");}catch(InvalidDataException){}
                Require(NecromancerSpellMissiles.State(first.Id)==first,"Failed Nova restore partly replaced pool");
            }
            var legacy=JsonSerializer.Serialize(first with{Ability=CombatAbility.BoneSpear,Frames=40,Cast=0,Flags=33,LastCollider=0,ElementFrames=0,RadialIndex=-1});
            legacy=legacy.Replace(",\"ElementFrames\":0","").Replace(",\"RadialIndex\":-1","");
            Require(JsonSerializer.Deserialize<NecromancerSpellFlight>(legacy) is{ElementFrames:0,RadialIndex:-1},"Legacy flight defaults");
        }finally{Clean(owner);}
    }
    private static void Hold(PlayerMobile p)
    {
        var pool=NecromancerSpellMissiles.Snapshot();var shift=TimedEffects.Now.Add(FixtureSpace.Retention)-Flights(p).Min(x=>x.NextUtc);
        NecromancerSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Owner==p.Serial.Value?x with{NextUtc=x.NextUtc+shift}:x).ToArray()});
    }
    private static void Check()
    {
        Source();Ring();HitAndTicks();Gear();Controls();var p=Owner();var t=Target(p,6);Cast(p);Through(p,2);Require(Flights(p).Length==64&&Effect(t).SpellPoison==null,"Saved ring already hit");Hold(p);
        var saved=new Saved(p.Serial.Value,t.Serial.Value,p.Location,p.Map.MapID,Flights(p),SkillHealth.Balance256(t),0);p.Internalize();t.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
        Log.Information("Poison Nova:12240source poison packets/384source direction projections;64saved rays,30frames,once-only20mana,collision,cure/potion/immune ticks,6lifecycle/terrain controls and malformed/legacy saves pass");
    }
    private static void Reload()
    {
        Source();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var t=World.FindMobile((Serial)saved.Target);
        Require(Flights(p).SequenceEqual(saved.Flights)&&SkillHealth.Balance256(t)==saved.Balance,"Saved Nova ring changed");p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);
        t.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);Through(p,1);Require(Flights(p).Length==64&&Flights(p).Zip(saved.Flights).All(x=>x.First.Frame==x.Second.Frame+1&&x.First.NextUtc==x.Second.NextUtc.AddMilliseconds(40)),"Saved rays did not resume once");
        Require(Effect(t).SpellPoison==null,"Resumed ring hit too early");Hold(p);p.Internalize();t.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Flights=Flights(p),Generation=saved.Generation+1}));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Target(p,4);PoisonDaggerChecks.Ready(p);int mana=NecromancerEffects.ManaBalance256(p);string label=quick?"poison-nova-quick":"poison-nova-right";
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false;long deadline=Core.TickCount+6000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Nova input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.PoisonNova&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana,"Input paid before release");began=true;Log.Information("{Label}: windup",label);}
                var state=TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value&&x.SpellPoison!=null);
                if(state==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&state.PoisonMs>0&&state.SpellPoison.Source==p.Serial.Value&&Flights(p).Length>0&&NecromancerEffects.ManaBalance256(p)==mana-20*256,"Actual Nova learned input/payment/ring");Clean(p,t);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Clean(p,t);Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.PoisonNova,(int)DiabloClass.Necromancer,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
