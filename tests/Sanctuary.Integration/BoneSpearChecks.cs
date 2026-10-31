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

public static class BoneSpearChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BoneSpearChecks));
    private sealed record Saved(uint Owner,uint Target,uint First,Point3D Point,int Map,NecromancerSpellFlight Flight,int Life256,int FirstLife256,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bone-spear-fixtures.json");
    public static void Configure()
    {
        Register("bone-spear-check",Check);Register("bone-spear-reload-check",Reload);
        foreach(bool quick in new[]{false,true})
        {bool selected=quick;Register(selected?"bone-spear-quick-check":"bone-spear-right-check",()=>FixtureNetwork.Connected(selected?"bone-spear-quick-result":"bone-spear-right-result",n=>Input(n,selected)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Learned Bone Spear source flight and piercing",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,CombatAbility ability)
    {
        void Add(string id){if(ClassCreation.Profiles.GetRank(p.Serial,id)>0){return;}TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string pre in row.Prerequisites){Add(pre);}Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
        Add(SkillMechanics.For(ability).TalentId);
    }
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureBoneSpear";Train(p,CombatAbility.BoneSpear);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.BoneSpear)&&SkillBindings.Instance.Assign(p,false,CombatAbility.BoneSpear)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.BoneSpear),"Bone Spear left/right/quick bindings");return p;
    }
    private static GraveyardSkeleton Target(PlayerMobile p,int dx)
    {var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static long Create(PlayerMobile p,int dx=8)=>NecromancerSpellMissiles.Create(p,new(p.X+dx,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.BoneSpear),TimedEffects.Now);
    private static void Through(long id,int count,bool actions=true)
    {for(int i=0;i<count&&NecromancerSpellMissiles.State(id) is{} state;i++){NecromancerSpellMissiles.Tick(state.NextUtc,actions);}}
    private static void Clean(PlayerMobile p,params Mobile[] targets)
    {NecromancerSpellMissiles.Cancel(p);foreach(var t in targets){t.Corpse?.Delete();t.Delete();}p.Corpse?.Delete();p.Delete();}
    private static long Cast(PlayerMobile p,Mobile target)
    {
        PoisonDaggerChecks.Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.BoneSpear).Mana256;
        Require(ClassCombat.Perform(p,CombatAbility.BoneSpear,target,target.Location,Core.TickCount),"Learned Spear begin");var s=ClassCombat.For(p);
        Require(NecromancerEffects.ManaBalance256(p)==mana&&!ClassCombat.ResolveAction(p,s.EffectAt-1),"Spear paid/fired before release");
        Require(ClassCombat.ResolveAction(p,s.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&!ClassCombat.ResolveAction(p,s.EffectAt),"Spear payment/replay");
        return NecromancerSpellMissiles.Snapshot().Flights.Single(x=>x.Owner==p.Serial.Value).Id;
    }
    private static void Source()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"bone-spear-damage-source-cases.json")));int count=0;
        var definition=SkillMechanics.For(CombatAbility.BoneSpear);string[] synergies=["Bone Wall","Bone Prison","Teeth","Bone Spirit"];
        foreach(var row in json.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();var values=new SkillValues(definition,key=>key==definition.SourceKey?V(0):255,key=>Math.Clamp(V(1)-20*Array.IndexOf(synergies,key),0,20),99);
            var ends=NecromancerSpellMissiles.Endpoints(values,0,SlowMissilePayload.Normal);int calls=0;
            int rolled=WeaponMissileDamage.RollValue(ends.Min,ends.Max,0,n=>{calls++;return n>0?n-1:0;});
            Require(ends==(V(6),V(7))&&V(4)==0&&V(5)==0&&V(8)==0&&V(9)==0&&rolled==V(10)&&calls==V(11),"Bone Spear complete source damage/roll case "+count);count++;
        }
        Require(count==4080,"Incomplete Spear damage source cases");
        using var route=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"bone-spear-route-source-cases.json")));Require(route.RootElement.GetArrayLength()==96,"Missing complete hit-router cases");
    }
    private static void Travel()
    {
        var p=Owner();var targets=new[]{Target(p,3),Target(p,6),Target(p,10),Target(p,18)};var hit=WeaponMissiles.HitRoll;var shield=SourceBlocking.BlockRoll;
        try
        {
            WeaponMissiles.HitRoll=()=>throw new InvalidOperationException("Spell used weapon AR");SourceBlocking.BlockRoll=_=>throw new InvalidOperationException("Pure magic used physical shield");
            int[] before=targets.Select(SkillHealth.Balance256).ToArray();long id=Cast(p,targets[1]);var first=NecromancerSpellMissiles.State(id);
            Require(first is{Frame:0,VelocityFp8:4608,Frames:40,Vx:.5625,Vy:0}&&targets.Select(SkillHealth.Balance256).SequenceEqual(before),"Launch pose/velocity or immediate damage");
            Through(id,4);Require(SkillHealth.Balance256(targets[3])==before[3],"Far target hit without travel");Through(id,40);
            Require(NecromancerSpellMissiles.State(id)==null,"Spear survived40frames");
            for(int i=0;i<targets.Length;i++){int loss=before[i]-SkillHealth.Balance256(targets[i]);Require(loss>=first.Minimum256&&loss<first.Maximum256,"Spear lost piercing,hit repeatedly or stopped at generic16tile limit: "+i);}
        }
        finally{WeaponMissiles.HitRoll=hit;SourceBlocking.BlockRoll=shield;Clean(p,targets);}
        p=Owner();var a=Target(p,3);var b=Target(p,6);try
        {
            int initial=SkillHealth.Balance256(a);long id=Create(p);Through(id,7);int once=SkillHealth.Balance256(a);Require(once<initial,"First collider missing");
            Through(id,7);Require(SkillHealth.Balance256(b)<50000*256&&NecromancerSpellMissiles.State(id)?.LastCollider==b.Serial.Value,"Second collider missing");
            a.MoveToWorld(new(p.X+11,p.Y,p.Z),p.Map);Through(id,26);
            Require(SkillHealth.Balance256(a)<once,"Lifetime hit-set incorrectly prevented a later collision after another target");
        }
        finally{Clean(p,a,b);}
    }
    private static void Controls()
    {
        foreach(string mode in new[]{"miss","wall","offline","facet","slow","death","respec","wardrobe"})
        {
            var p=Owner();var t=Target(p,8);Item wall=null;try
            {
                if(mode=="slow"){AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,t.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());}
                int before=SkillHealth.Balance256(t);long id=Create(p);var first=NecromancerSpellMissiles.State(id);
                if(mode=="miss"){t.MoveToWorld(new(t.X,t.Y+3,t.Z),t.Map);}
                if(mode=="wall"){wall=new Static(0x80);wall.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);}
                if(mode=="facet"){p.MoveToWorld(p.Location,Map.Felucca);}
                if(mode=="slow")
                {var values=SkillMechanics.Values(p,CombatAbility.BoneSpear);Require(first.VelocityFp8==2304&&first.Minimum256==values.Damage256(false)/2&&first.Maximum256==values.Damage256(true)/2,"Slow capture changed wrong channel/endpoints");AmazonSlowMissiles.Clear(p);}
                if(mode=="death"){p.Kill();Require(NecromancerSpellMissiles.State(id)==null,"Death retained owned flight");}
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(NecromancerSpellMissiles.State(id)==null,"Respec retained owned flight");}
                if(mode=="wardrobe"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _)&&NecromancerSpellMissiles.State(id)==null,"Wardrobe retained owned flight");}
                Through(id,40,mode!="offline");Require(NecromancerSpellMissiles.State(id)==null,"Control flight did not expire: "+mode);
                Require((SkillHealth.Balance256(t)<before)==(mode=="slow"),"Unexpected control damage: "+mode);
            }
            finally{wall?.Delete();AmazonSlowMissiles.Clear(p);Clean(p,t);}
        }
        var owner=Owner();var immune=new LichLord{Frozen=true,CantWalk=true,NoKillAwards=true};MonsterVariants.Assign(immune,MonsterDifficulty.Hell);immune.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);immune.AIObject.AITimer.Stop();TimedEffects.PreventHealing(immune,TimedEffects.Now);
        var behind=Target(owner,8);try
        {
            Require(NamedElements.MagicResistance(immune)==100&&ClassCombat.IsHostile(owner,immune),"Actual Hell Lich Lord is not the pinned hostile magic-immune control");int before=SkillHealth.Balance256(immune),other=SkillHealth.Balance256(behind);long id=Create(owner);Through(id,7);
            Require(NecromancerSpellMissiles.State(id)?.LastCollider==immune.Serial.Value,"Immune control was not a real collider");Through(id,40);
            Require(SkillHealth.Balance256(immune)==before&&SkillHealth.Balance256(behind)<other,"Immune collider took damage or stopped piercing");
        }
        finally{Clean(owner,immune,behind);}
    }
    private static void Capture()
    {
        var p=Owner();var t=Target(p,8);try
        {
            long id=Create(p);var original=NecromancerSpellMissiles.State(id);NecromancerSpellMissiles.Cancel(p);PoisonDaggerChecks.Dagger(p);PoisonDaggerChecks.Named(p,"Raven Frost");
            id=Create(p);var armed=NecromancerSpellMissiles.State(id);Require(armed.Minimum256==original.Minimum256&&armed.Maximum256==original.Maximum256&&NecromancerSpellMissiles.Packet(armed,armed.Minimum256).Cold==0,"Weapon/item elemental stats leaked into SrcDam0 spell");
            Train(p,CombatAbility.BoneWall);PoisonDaggerChecks.Named(p,"Wormskull");Require(NecromancerSpellMissiles.State(id)==armed,"Existing flight reread gear/talent ranks");
            NecromancerSpellMissiles.Cancel(p);id=Create(p);var fresh=NecromancerSpellMissiles.State(id);Require(fresh.Rank==2&&fresh.Minimum256>armed.Minimum256,"Fresh flight missed effective/hard synergy ranks");
            var old=NecromancerSpellMissiles.Snapshot();bool refused=false;try{NecromancerSpellMissiles.Restore(old with{Flights=old.Flights.Append(fresh with{Frames=0}).ToArray()});}catch(InvalidDataException){refused=true;}
            Require(refused&&NecromancerSpellMissiles.State(id)==fresh,"Malformed restore partly replaced the live pool");
            NecromancerSpellMissiles.Cancel(p);foreach(var resistance in Enum.GetValues<ResistanceType>()){t.SetResistance(resistance,0);}
            foreach(string mode in new[]{"removed","after-launch","expires","active"})
            {
                TimedEffects.Clear(p);
                if(mode!="after-launch")
                {Require(CombatPotions.Apply(p,3,mode=="expires"?TimedEffects.Now.AddMilliseconds(-29900):null),"Actual tier3 spell potion");}
                id=Create(p);var flight=NecromancerSpellMissiles.State(id);
                if(mode=="removed"){TimedEffects.Clear(p);}else if(mode=="after-launch"){Require(CombatPotions.Apply(p,3),"Drink after launch");}
                ulong stream=flight.Seed;int raw=WeaponMissileDamage.RollValue(flight.Minimum256,flight.Maximum256,0,n=>(int)SourceRandom.Limited(ref stream,n));
                int before=SkillHealth.Balance256(t);Through(id,40);
                int expected=raw+(mode is "active" or "after-launch"?4*(raw/10):0);
                Require(before-SkillHealth.Balance256(t)==expected,"Tier3 impact-time eligibility/expiry or once-only additions: "+mode);
            }

        }
        finally{Clean(p,t);}
    }
    private static void Save()
    {
        var p=Owner();var first=Target(p,3);var t=Target(p,10);long id=Create(p);Through(id,7);var state=NecromancerSpellMissiles.State(id);var pool=NecromancerSpellMissiles.Snapshot();
        Require(state.LastCollider==first.Serial.Value&&state.Seed!=SourceRandom.InitLow(1)&&SkillHealth.Balance256(first)<50000*256,"Saved flight did not contain an actual advanced hit/seed");
        state=state with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};NecromancerSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Id==id?state:x).ToArray()});
        var saved=new Saved(p.Serial.Value,t.Serial.Value,first.Serial.Value,p.Location,p.Map.MapID,state,SkillHealth.Balance256(t),SkillHealth.Balance256(first),0);first.Internalize();p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Check()
    {
        Source();var seed=NecromancerSpellMissiles.SeedLow;
        try{NecromancerSpellMissiles.SeedLow=()=>1;Travel();Controls();Capture();Save();}finally{NecromancerSpellMissiles.SeedLow=seed;}
        Log.Information("Bone Spear:4080source packets;paid learned flight,unlimited piercing,last-collider rehit,8lifecycle/geometry/slow controls,actual Hell magic immunity,real item/rank capture and atomic restore pass");
    }
    private static void Reload()
    {
        Source();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=World.FindMobile((Serial)s.Target);
        Require(NecromancerSpellMissiles.State(s.Flight.Id)==s.Flight&&s.Flight.LastCollider==s.First&&SkillHealth.Balance256(t)==s.Life256&&SkillHealth.Balance256(World.FindMobile((Serial)s.First))==s.FirstLife256,"Reload changed flight pose/packet/collider/advanced seed/deadline");p.MoveToWorld(s.Point,Map.Maps[s.Map]);
        Through(s.Flight.Id,1);var next=NecromancerSpellMissiles.State(s.Flight.Id);Require(next is{Fault:null}&&next.Frame==s.Flight.Frame+1&&SkillHealth.Balance256(t)==s.Life256,"Loaded flight did not advance exactly once");
        var pool=NecromancerSpellMissiles.Snapshot();next=next with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};
        NecromancerSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Id==next.Id?next:x).ToArray()});p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Flight=next,Generation=s.Generation+1}));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Target(p,8);PoisonDaggerChecks.Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.BoneSpear).Mana256,life=SkillHealth.Balance256(t);
        string label=quick?"bone-spear-quick":"bone-spear-right";network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false,launched=false;long deadline=Core.TickCount+6000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Bone Spear input timeout");var s=ClassCombat.For(p);
                if(s.PendingAbility==CombatAbility.BoneSpear&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana,"Input paid during windup");began=true;Log.Information("{Label}: windup",label);}
                if(NecromancerSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value)){Require(SkillHealth.Balance256(t)==life||launched,"Input hit before observing travel");launched=true;}
                if(SkillHealth.Balance256(t)==life){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&launched&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Real right/quick Spear travel/payment");Clean(p,t);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Clean(p,t);Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.BoneSpear,(int)DiabloClass.Necromancer,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
