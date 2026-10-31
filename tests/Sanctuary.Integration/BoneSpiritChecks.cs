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
public static class BoneSpiritChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BoneSpiritChecks));
    private sealed record Saved(uint Owner,uint Target,Point3D Point,int Map,NecromancerSpellFlight[] Flights,int Life,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bone-spirit-fixtures.json");
    public static void Configure()
    {
        Register("bone-spirit-check",Check);Register("bone-spirit-reload-check",Reload);
        foreach(bool quick in new[]{false,true}){bool selected=quick;Register(selected?"bone-spirit-quick-check":"bone-spirit-right-check",()=>FixtureNetwork.Connected(selected?"bone-spirit-quick-result":"bone-spirit-right-result",n=>Input(n,selected)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Bone Spirit saved guidance and source-zero spell packet",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureBoneSpirit";
        void Train(string id){if(ClassCreation.Profiles.GetRank(p.Serial,id)>0){return;}TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string pre in row.Prerequisites){Train(pre);}Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
        Train("necromancer.bonespirit");Require(SkillBindings.Instance.Assign(p,true,CombatAbility.BoneSpirit)&&SkillBindings.Instance.Assign(p,false,CombatAbility.BoneSpirit)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.BoneSpirit),"Spirit bindings");return p;
    }
    private static GraveyardSkeleton Target(PlayerMobile p,int dx,int dy=0)
    {var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static NecromancerSpellFlight[] Flights(PlayerMobile p)=>NecromancerSpellMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).OrderBy(x=>x.Id).ToArray();
    private static long Create(PlayerMobile p,Mobile target=null,int dx=3)=>NecromancerSpellMissiles.Create(p,target?.Location??new(p.X+dx,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.BoneSpirit),TimedEffects.Now,target);
    private static void Through(PlayerMobile p,int frames,bool actions=true)
    {for(int i=0;i<frames&&Flights(p).Length>0;i++){NecromancerSpellMissiles.Tick(Flights(p).Max(x=>x.NextUtc),actions);}}
    private static void Clean(PlayerMobile p,params Mobile[] targets)
    {NecromancerSpellMissiles.Cancel(p);foreach(var t in targets){t.Corpse?.Delete();t.Delete();}p.Corpse?.Delete();p.Delete();}
    private static long Cast(PlayerMobile p,Mobile target)
    {
        PoisonDaggerChecks.Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.BoneSpirit).Mana256;
        Require(ClassCombat.Perform(p,CombatAbility.BoneSpirit,target,target.Location,Core.TickCount),"Learned Spirit begin");var s=ClassCombat.For(p);
        Require(NecromancerEffects.ManaBalance256(p)==mana&&!ClassCombat.ResolveAction(p,s.EffectAt-1),"Spirit paid early");
        Require(ClassCombat.ResolveAction(p,s.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&!ClassCombat.ResolveAction(p,s.EffectAt),"Spirit payment/replay");return Flights(p).Single().Id;
    }
    private static void Source()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"bone-spirit-damage-source-cases.json")));int count=0;var definition=SkillMechanics.For(CombatAbility.BoneSpirit);string[] synergies=["Bone Wall","Bone Prison","Teeth","Bone Spear"];
        foreach(var row in json.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();var values=new SkillValues(definition,key=>key==definition.SourceKey?V(0):255,key=>Math.Clamp(V(1)-20*Array.IndexOf(synergies,key),0,20),99);
            var ends=NecromancerSpellMissiles.Endpoints(values,0,SlowMissilePayload.Normal);int calls=0;int roll=WeaponMissileDamage.RollValue(ends.Min,ends.Max,0,n=>{calls++;return n-1;});
            Require(ends==(V(6),V(7))&&V(4)==0&&V(5)==0&&V(8)==0&&V(9)==0&&roll==V(10)&&calls==V(11),"Spirit source packet/roll "+count);count++;
        }
        Require(count==4080,"Spirit packet fixture count");
        using var guide=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"bone-spirit-guidance-source-cases.json")));var root=guide.RootElement;
        foreach(var row in root.GetProperty("steer").EnumerateArray())
        {int V(int i)=>row[i].GetInt32();Require(GuidedArrows.Turn(V(0),V(1),V(2),V(3)==1,V(4))==(V(6)!=0),"Source Spirit turn predicate");}
        foreach(var row in root.GetProperty("hit").EnumerateArray())
        {int V(int i)=>row[i].GetInt32();Require(GuidedArrows.Hit(V(0),V(1),V(2),V(3))==V(5),"Source Spirit hit result");}
        var p=Owner();var t=Target(p,8);try
        {
            int projected=0;
            foreach(var row in root.GetProperty("launch").EnumerateArray())
            {
                int V(int i)=>row[i].GetInt32();if(V(1)%2!=0||V(2)%2!=0){continue;}
                t.MoveToWorld(new(p.X+V(1)/2,p.Y+V(2)/2,p.Z),p.Map);
                var g=BoneSpiritGuidance.Capture(p,V(0)!=0?t:null,t.Location,V(12));
                Require(g.Mode==V(8)&&g.Remaining==V(10)&&g.VelocityFp8==V(12)&&g.OriginalX==V(1)&&g.OriginalY==V(2),"Spirit launch/source factory projection");projected++;
            }
            Require(projected==144,"Incomplete exact two-subtile launch projection");
        }finally{Clean(p,t);}
        Require(root.GetProperty("launch").GetArrayLength()==360&&root.GetProperty("steer").GetArrayLength()==2016&&root.GetProperty("hit").GetArrayLength()==288&&root.GetProperty("select").GetArrayLength()==40,"Incomplete guidance matrix");
    }
    private static void Locked()
    {
        var p=Owner();var blocker=Target(p,3);var t=Target(p,8);try
        {
            int before=SkillHealth.Balance256(t),ignored=SkillHealth.Balance256(blocker);long id=Cast(p,t);var s=NecromancerSpellMissiles.State(id);
            Require(s is{Frame:0,Frames:128,VelocityFp8:2304,Flags:0x20}&&s.Guided is{Mode:1,Remaining:128}&&s.Guided.Target==t.Serial.Value&&before==SkillHealth.Balance256(t),"Selected Spirit launch");
            Through(p,128);int loss=before-SkillHealth.Balance256(t);Require(Flights(p).Length==0&&loss>=s.Minimum256&&loss<s.Maximum256&&SkillHealth.Balance256(blocker)==ignored,"Locked Spirit target/interception/contact removal");
        }finally{Clean(p,t,blocker);}
        p=Owner();t=Target(p,8);try
        {
            Create(p,t);Through(p,2);t.MoveToWorld(new(t.X,p.Y+3,t.Z),t.Map);Through(p,1);Require(Flights(p).Single().Vy==0,"Turned before remaining125 cadence");
            Through(p,1);Require(Flights(p).Single().Vy>0,"Did not turn on remaining125 cadence");Through(p,128);Require(SkillHealth.Balance256(t)<50000*256,"Steered Spirit missed moving target");
        }finally{Clean(p,t);}
        p=Owner();t=Target(p,8);var other=Target(p,6);try
        {
            Create(p,t);t.Kill();Through(p,128);Require(Flights(p).Length==0&&SkillHealth.Balance256(other)==50000*256,"Dead locked target triggered invented retargeting");
        }finally{Clean(p,t,other);}
    }
    private static void Ground()
    {
        var p=Owner();var lower=Target(p,8);var closer=Target(p,4);try
        {
            long id=Create(p);var launch=NecromancerSpellMissiles.State(id);Require(launch.Guided is{Mode:2,Remaining:10}&&launch.Frames==10&&launch.Flags==0x420,"Ground Spirit initial remaining");
            Through(p,9);Require(Flights(p).Single().Guided.Mode==2&&SkillHealth.Balance256(closer)==50000*256,"Ground Spirit hit before acquisition");
            Through(p,1);var acquired=Flights(p).Single();Require(acquired.Guided is{Mode:5,Remaining:128}&&acquired.Guided.Target==lower.Serial.Value&&acquired.Frames==138,"Ground acquisition not lowest ID/source extension");
            Through(p,128);Require(Flights(p).Length==0&&SkillHealth.Balance256(lower)<50000*256&&SkillHealth.Balance256(closer)==50000*256,"Acquired Spirit ignored selected target/collision policy");
        }finally{Clean(p,lower,closer);}
        p=Owner();try
        {
            Create(p);Through(p,10);Require(Flights(p).Single().Guided is{Mode:6,Target:0,Remaining:128},"No-target ground extension");Through(p,128);Require(Flights(p).Length==0,"Mode6 reacquired/failed final expiry");
        }finally{Clean(p);}
    }
    private static void Controls()
    {
        foreach(string mode in new[]{"wall","offline","facet","death","respec","wardrobe","slow"})
        {
            var p=Owner();var t=Target(p,8);Item wall=null;try
            {
                if(mode=="slow"){AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,t.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());}
                Create(p,t);var first=Flights(p).Single();if(mode=="slow"){Require(first.VelocityFp8==1152&&first.Guided.VelocityFp8==1152,"Spirit captured slow");AmazonSlowMissiles.Clear(p);}
                if(mode=="wall"){wall=new Static(0x80);wall.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);}
                if(mode=="facet"){p.MoveToWorld(p.Location,Map.Felucca);}
                if(mode=="death"){p.Kill();Require(Flights(p).Length==0,"Spirit death clear");}
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(Flights(p).Length==0,"Spirit respec clear");}
                if(mode=="wardrobe"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _)&&Flights(p).Length==0,"Spirit wardrobe clear");}
                Through(p,128,mode!="offline");Require(Flights(p).Length==0&&(SkillHealth.Balance256(t)<50000*256)==(mode=="slow"),"Spirit control "+mode);
            }finally{wall?.Delete();AmazonSlowMissiles.Clear(p);Clean(p,t);}
        }
        var owner=Owner();try
        {
            NecromancerEffects.SetMana256(owner,0);Require(!NecromancerCombat.Release(owner,CombatAbility.BoneSpirit,Serial.Zero,new(owner.X+3,owner.Y,owner.Z))&&Flights(owner).Length==0,"Unpaid Spirit emitted");
            foreach(int fraction in new[]{17,255})
            {
                int maximum=owner.ManaMax*256;NecromancerEffects.SetMana256(owner,maximum-256+fraction);
                owner.Mana=owner.ManaMax; // Actual native whole-mana setter, as used by regeneration.
                Require(NecromancerEffects.RawManaBalance256(owner)==maximum+fraction&&NecromancerEffects.ManaBalance256(owner)==maximum,"Native regeneration granted fractional over-cap mana");
                Require(NecromancerEffects.Pay(owner,128)&&NecromancerEffects.ManaBalance256(owner)==maximum-128
                    &&NecromancerEffects.Profiles.Find(owner).ManaFraction==128,"Fractional payment spent latent over-cap credit");
            }
            Create(owner);var pool=NecromancerSpellMissiles.Snapshot();var first=Flights(owner).Single();
            try{NecromancerSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Id==first.Id?x with{Guided=x.Guided with{Mode=3}}:x).ToArray()});throw new InvalidOperationException("Invalid guidance accepted");}catch(InvalidDataException){}
            Require(NecromancerSpellMissiles.State(first.Id)==first,"Invalid Spirit restore partially replaced state");
        }finally{Clean(owner);}
    }
    private static void Hold(PlayerMobile p)
    {var pool=NecromancerSpellMissiles.Snapshot();TimeSpan shift=TimedEffects.Now.Add(FixtureSpace.Retention)-Flights(p).Min(x=>x.NextUtc);NecromancerSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Owner==p.Serial.Value?x with{NextUtc=x.NextUtc+shift}:x).ToArray()});}
    private static void Check()
    {
        Source();Locked();Ground();Controls();var p=Owner();var target=Target(p,8);Create(p,target);Through(p,2);target.MoveToWorld(new(target.X,p.Y+3,target.Z),target.Map);Create(p);Through(p,10);
        Require(Flights(p).Length==2&&Flights(p).Any(x=>x.Guided.Mode==1&&x.Vy>0)&&Flights(p).Any(x=>x.Guided.Mode==5),"Missing midflight locked/acquired pair");Hold(p);
        var saved=new Saved(p.Serial.Value,target.Serial.Value,p.Location,p.Map.MapID,Flights(p),SkillHealth.Balance256(target),0);Require(saved.Life==50000*256,"Save pair hit too early");p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
        Log.Information("Bone Spirit:4080source packets,360launch/2016turn/288hit/40selector controls;learned payment,locked interception/turn/death,ground lowest-ID acquisition/mode6 expiry,7lifecycle controls and atomic restore pass");
    }
    private static void Reload()
    {
        Source();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=World.FindMobile((Serial)saved.Target);
        Require(Flights(p).SequenceEqual(saved.Flights)&&SkillHealth.Balance256(target)==saved.Life,"Spirit reload changed flight/guidance/packet/target");p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);Through(p,1);
        var next=Flights(p);Require(next.Length==2&&next.Zip(saved.Flights).All(x=>x.First.Frame==x.Second.Frame+1&&x.First.Guided.Remaining==x.Second.Guided.Remaining-1)&&SkillHealth.Balance256(target)==saved.Life,"Loaded guidance did not resume once");
        Hold(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Flights=Flights(p),Generation=saved.Generation+1}));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Target(p,8);PoisonDaggerChecks.Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.BoneSpirit).Mana256,life=SkillHealth.Balance256(t);
        string label=quick?"bone-spirit-quick":"bone-spirit-right";network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false,launched=false;long deadline=Core.TickCount+6000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Bone Spirit input timeout");var s=ClassCombat.For(p);
                if(s.PendingAbility==CombatAbility.BoneSpirit&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana,"Input paid during windup");began=true;Log.Information("{Label}: windup",label);}
                if(NecromancerSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value)){Require(SkillHealth.Balance256(t)==life||launched,"Input hit before observing travel");launched=true;}
                if(SkillHealth.Balance256(t)==life){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&launched&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Real right/quick Spear travel/payment");Clean(p,t);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Clean(p,t);Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.BoneSpirit,(int)DiabloClass.Necromancer,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
