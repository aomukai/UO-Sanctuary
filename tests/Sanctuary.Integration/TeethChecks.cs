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
public static class TeethChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(TeethChecks));
    private sealed record Saved(uint Owner,uint Near,uint Far,Point3D Point,int Map,NecromancerSpellFlight[] Flights,NecromancerSpellLease[] Leases,int NearLife,int FarLife,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"teeth-fixtures.json");
    public static void Configure()
    {
        Register("teeth-check",Check);Register("teeth-reload-check",Reload);
        foreach(bool quick in new[]{false,true}){bool selected=quick;Register(selected?"teeth-quick-check":"teeth-right-check",()=>FixtureNetwork.Connected(selected?"teeth-quick-result":"teeth-right-result",n=>Input(n,selected)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Teeth saved fan, spell damage and collision",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureTeeth";
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.teeth",out _),"Train Teeth");
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Teeth)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Teeth)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Teeth),"Teeth bindings");return p;
    }
    private static GraveyardSkeleton Target(PlayerMobile p,int dx,int dy=0)
    {var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static NecromancerSpellFlight[] Flights(PlayerMobile p)=>NecromancerSpellMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).OrderBy(x=>x.Id).ToArray();
    private static NecromancerSpellLease[] Leases(PlayerMobile p)
    {var casts=Flights(p).Select(x=>x.Cast).ToHashSet();return NecromancerSpellMissiles.Snapshot().Leases.Where(x=>casts.Contains(x.Cast)).ToArray();}
    private static void Through(PlayerMobile p,int frames,bool actions=true)
    {for(int i=0;i<frames&&Flights(p).Length>0;i++){NecromancerSpellMissiles.Tick(Flights(p).Min(x=>x.NextUtc),actions);}}
    private static long[] Fan(PlayerMobile p,Point3D point)=>NecromancerSpellMissiles.CreateFan(p,point,SkillMechanics.Values(p,CombatAbility.Teeth),TimedEffects.Now);
    private static void Clean(PlayerMobile p,params Mobile[] targets)
    {NecromancerSpellMissiles.Cancel(p);foreach(var t in targets){t.Corpse?.Delete();t.Delete();}p.Corpse?.Delete();p.Delete();}
    private static void Cast(PlayerMobile p,Point3D aim)
    {
        PoisonDaggerChecks.Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.Teeth).Mana256;
        Require(ClassCombat.Perform(p,CombatAbility.Teeth,null,aim,Core.TickCount),"Learned Teeth begin");var s=ClassCombat.For(p);
        Require(NecromancerEffects.ManaBalance256(p)==mana&&Flights(p).Length==0&&!ClassCombat.ResolveAction(p,s.EffectAt-1),"Teeth paid/fired before release");
        Require(ClassCombat.ResolveAction(p,s.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&!ClassCombat.ResolveAction(p,s.EffectAt),"Teeth payment/replay");
    }
    private static void Source()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"teeth-damage-source-cases.json")));int count=0;var definition=SkillMechanics.For(CombatAbility.Teeth);
        string[] synergies=["Bone Wall","Bone Prison","Bone Spear","Bone Spirit"];
        foreach(var row in json.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();var values=new SkillValues(definition,key=>key==definition.SourceKey?V(0):255,key=>Math.Clamp(V(1)-20*Array.IndexOf(synergies,key),0,20),99);
            var ends=NecromancerSpellMissiles.Endpoints(values,0,SlowMissilePayload.Normal);int calls=0;int roll=WeaponMissileDamage.RollValue(ends.Min,ends.Max,0,n=>{calls++;return n-1;});
            Require(ends==(V(6),V(7))&&V(4)==0&&V(5)==0&&V(8)==0&&V(9)==0&&roll==V(10)&&calls==V(11),"Teeth source packet/roll "+count);count++;
        }
        Require(count==4080,"Teeth packet source count");
        using var route=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"teeth-route-source-cases.json")));Require(route.RootElement.GetArrayLength()==192,"Teeth complete finder/router fixture");
        // Real saved flight vectors use the same already-differential-tested fan.
        foreach(int rank in new[]{1,2,20,23,24,255})foreach(var aim in new[]{(1,0),(10,0),(-3,5),(0,0)})
        {
            var p=Owner();try
            {
                var values=new SkillValues(definition,_=>rank,_=>0,99);var ids=NecromancerSpellMissiles.CreateFan(p,new(p.X+aim.Item1,p.Y+aim.Item2,p.Z),values,TimedEffects.Now);
                var rays=FanGeometry.Create(aim.Item1*2,aim.Item2*2,(int)values.Eval("calc1"));Require(ids.Length==Math.Min(24,rank+1),"Teeth rank fan count");
                for(int i=0;i<ids.Length;i++)
                {var s=NecromancerSpellMissiles.State(ids[i]);var ray=rays[i];var v=MissileVectors.VelocityFp8(ray.X==0&&ray.Y==0?1:ray.X,ray.X==0&&ray.Y==0?1:ray.Y,3072);
                    Require(s is{Frame:0,Frames:50,Flags:2080,Activation:0,VelocityFp8:3072}&&s.Cast==ids[0]&&s.LastCollider==p.Serial.Value&&(s.Vx,s.Vy)==v,"Teeth saved fan/factory composition");}
            }finally{Clean(p);}
        }
    }
    private static void Travel()
    {
        var p=Owner();var t=Target(p,18);try
        {
            int before=SkillHealth.Balance256(t);Cast(p,new(p.X+8,p.Y,p.Z));var flights=Flights(p);Require(flights.Length==2&&flights.Select(x=>x.Cast).Distinct().Count()==1&&SkillHealth.Balance256(t)==before,"Teeth immediate packet/fan");
            Through(p,40);Require(SkillHealth.Balance256(t)==before,"Teeth18tile target hit too soon");Through(p,10);
            int loss=before-SkillHealth.Balance256(t);Require(loss>=flights[0].Minimum256&&loss<flights[0].Maximum256&&Flights(p).Length==0,"Teeth50frame travel beyond16tiles/expiry");
        }finally{Clean(p,t);}
        p=Owner();var near=Target(p,2,2);var far=Target(p,2,2);try
        {
            int a=SkillHealth.Balance256(near),b=SkillHealth.Balance256(far);Fan(p,p.Location);Through(p,50);
            Require(SkillHealth.Balance256(near)<a&&SkillHealth.Balance256(far)<b&&Flights(p).Length==0,"Delayed first candidate hid second target or contact pierced");
            Require(Leases(p).Length==0,"Completed fan leaked leases");
        }finally{Clean(p,near,far);}
        p=Owner();near=Target(p,2,2);try
        {
            Fan(p,p.Location);for(int i=0;i<20&&Leases(p).Length==0;i++){Through(p,1);}
            Require(Flights(p).Length==1&&Leases(p).Length==1,"Four-frame delay fixture missing first contact");
            int life=SkillHealth.Balance256(near);near.MoveToWorld(new(p.X+3,p.Y+3,p.Z),p.Map);
            Through(p,3);Require(SkillHealth.Balance256(near)==life&&Flights(p).Length==1,"Hit-delay window ended before four frames");
            Through(p,1);Require(SkillHealth.Balance256(near)<life&&Flights(p).Length==0,"Hit-delay did not end on fourth frame or accepted tooth survived");
        }finally{Clean(p,near);}
        // Same-cast delayed candidates survive; independent existing cast scopes
        // remain independent. This is the explicitly retained adapter,not modern proof.
        p=Owner();near=Target(p,2,2);try
        {
            var one=Fan(p,p.Location);var two=Fan(p,p.Location);Require(NecromancerSpellMissiles.State(one[0]).Cast!=NecromancerSpellMissiles.State(two[0]).Cast,"Independent fan shared identity");
            for(int i=0;i<20&&Leases(p).Length<2;i++){Through(p,1);}Require(Leases(p).Length==2,"Separate cast-local leases not recorded");
            int life=SkillHealth.Balance256(near);Through(p,1);Require(SkillHealth.Balance256(near)==life&&Flights(p).Length==2,"NHD contact consumed sibling or damaged early");
        }finally{Clean(p,near);}
    }
    private static void Controls()
    {
        foreach(string mode in new[]{"miss","wall","offline","facet","death","respec","wardrobe","slow"})
        {
            var p=Owner();var t=Target(p,8);Item wall=null;try
            {
                if(mode=="slow"){AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,t.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());}
                Fan(p,new(p.X+8,p.Y,p.Z));var original=Flights(p);
                if(mode=="slow"){var v=SkillMechanics.Values(p,CombatAbility.Teeth);Require(original.All(x=>x.VelocityFp8==1536&&x.Minimum256==v.Damage256(false)/2&&x.Maximum256==v.Damage256(true)/2),"Teeth slow capture");AmazonSlowMissiles.Clear(p);}
                if(mode=="miss"){t.MoveToWorld(new(t.X,t.Y+4,t.Z),t.Map);}
                if(mode=="wall"){wall=new Static(0x80);wall.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);}
                if(mode=="facet"){p.MoveToWorld(p.Location,Map.Felucca);}
                if(mode=="death"){p.Kill();Require(Flights(p).Length==0,"Teeth death");}
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(Flights(p).Length==0,"Teeth respec");}
                if(mode=="wardrobe"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _)&&Flights(p).Length==0,"Teeth wardrobe");}
                Through(p,50,mode!="offline");Require(Flights(p).Length==0&&(SkillHealth.Balance256(t)<50000*256)==(mode=="slow"),"Teeth lifecycle/geometry control "+mode);
            }finally{wall?.Delete();AmazonSlowMissiles.Clear(p);Clean(p,t);}
        }
        var owner=Owner();try
        {
            NecromancerEffects.SetMana256(owner,0);Require(!NecromancerCombat.Release(owner,CombatAbility.Teeth,Serial.Zero,new(owner.X+8,owner.Y,owner.Z))&&Flights(owner).Length==0,"Unpaid Teeth emitted");
            Fan(owner,owner.Location);var pool=NecromancerSpellMissiles.Snapshot();var first=Flights(owner)[0];
            try{NecromancerSpellMissiles.Restore(pool with{Leases=[new(first.Cast+999,owner.Serial.Value,TimedEffects.Now)]});throw new InvalidOperationException("Malformed lease accepted");}catch(InvalidDataException){}
            Require(NecromancerSpellMissiles.State(first.Id)==first,"Invalid lease partially restored pool");
            // Omitted additive fields remain valid for predecessor Bone Spear JSON.
            var spear=SkillMechanics.For(CombatAbility.BoneSpear);long id=NecromancerSpellMissiles.Create(owner,new(owner.X+8,owner.Y,owner.Z),new(spear,_=>1,_=>0,99),TimedEffects.Now);
            var saved=NecromancerSpellMissiles.State(id);string old=JsonSerializer.Serialize(saved).Replace(",\"Cast\":0","").Replace(",\"Flags\":33","").Replace(",\"Activation\":0","");
            Require(JsonSerializer.Deserialize<NecromancerSpellFlight>(old)==saved,"Additive Teeth fields broke predecessor Spear JSON");
        }finally{Clean(owner);}
    }
    private static void Hold(PlayerMobile p)
    {
        var pool=NecromancerSpellMissiles.Snapshot();var own=Flights(p);var casts=own.Select(x=>x.Cast).ToHashSet();TimeSpan shift=TimedEffects.Now.Add(FixtureSpace.Retention)-own.Min(x=>x.NextUtc);
        NecromancerSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Owner==p.Serial.Value?x with{NextUtc=x.NextUtc+shift}:x).ToArray(),Leases=pool.Leases.Select(x=>casts.Contains(x.Cast)?x with{UntilUtc=x.UntilUtc+shift}:x).ToArray()});
    }
    private static void Check()
    {
        Source();Travel();Controls();var p=Owner();var near=Target(p,2,2);var far=Target(p,3,3);Fan(p,p.Location);
        for(int i=0;i<20&&Leases(p).Length==0;i++){Through(p,1);}Require(Leases(p).Length==1&&Flights(p).Length==1,"No midflight injured-target/lease fixture");
        Hold(p);var saved=new Saved(p.Serial.Value,near.Serial.Value,far.Serial.Value,p.Location,p.Map.MapID,Flights(p),Leases(p),SkillHealth.Balance256(near),SkillHealth.Balance256(far),0);
        Require(saved.NearLife<50000*256&&saved.FarLife==50000*256,"Save fixture didn't isolate first impact");p.Internalize();near.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
        Log.Information("Teeth:4080source packets,24rank/aim fans,50frame beyond16tile travel,contact removal,delayed-candidate filtering,8lifecycle controls,atomic lease restore and old Spear JSON pass;cast-local delay scope remains explicit");
    }
    private static void Reload()
    {
        Source();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var near=World.FindMobile((Serial)saved.Near);var far=World.FindMobile((Serial)saved.Far);
        // Compare values via JSON because SourceStepTrace contains array storage.
        Require(JsonSerializer.Serialize(Flights(p))==JsonSerializer.Serialize(saved.Flights)&&Leases(p).SequenceEqual(saved.Leases)&&SkillHealth.Balance256(near)==saved.NearLife&&SkillHealth.Balance256(far)==saved.FarLife,"Reload changed fan/lease or target resources");
        p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);near.MoveToWorld(new(p.X+2,p.Y+2,p.Z),p.Map);Through(p,1);
        Require(Flights(p).Length==1&&Flights(p)[0].Frame==saved.Flights[0].Frame+1&&SkillHealth.Balance256(near)==saved.NearLife&&SkillHealth.Balance256(far)==saved.FarLife,"Reload replayed impact or lost active delay");
        Hold(p);p.Internalize();near.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Flights=Flights(p),Leases=Leases(p),Generation=saved.Generation+1}));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Target(p,8);PoisonDaggerChecks.Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.Teeth).Mana256,life=SkillHealth.Balance256(t);
        string label=quick?"teeth-quick":"teeth-right";network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false,launched=false;long deadline=Core.TickCount+6000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Teeth input timeout");var s=ClassCombat.For(p);
                if(s.PendingAbility==CombatAbility.Teeth&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana,"Teeth input paid windup");began=true;Log.Information("{Label}: windup",label);}
                if(Flights(p).Length>0){launched=true;}
                if(SkillHealth.Balance256(t)==life){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&launched&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Teeth actual input fan/payment");Clean(p,t);Log.Information("{Label}-result: passed",label);
            }catch(Exception e){Clean(p,t);Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Teeth,(int)DiabloClass.Necromancer,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
