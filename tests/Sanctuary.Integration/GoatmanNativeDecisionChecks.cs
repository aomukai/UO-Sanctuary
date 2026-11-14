// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GoatmanNativeDecisionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GoatmanNativeDecisionChecks));
    private sealed record Saved(int Generation,uint Owner,uint Body,string Birth,DeathMarkSavedActor Controller,NpcEquipmentSeed Random,int Life);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"goatman-native-decision-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"goatman-native-decision-reload-check":"goatman-native-decision-check";
            ConsoleInputHandler.RegisterCommand(name,"Bound current Goatman decision root with native stats/contacts",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}else{Start();}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static GenericPetBirthPath FixturePath(SkeletonGuardian b)
    {
        // Explicit qualified-input fixture. It is NOT the unresolved native
        // birth constructor:unused counters/masks are fixture inputs only.
        int cls=WarlockDemonPlacement.ClassId(b);var path=new GenericPetCommitPath
        {X=((uint)(b.X*2)<<16)|0x8000,Y=((uint)(b.Y*2)<<16)|0x8000,Room=GenericPetNativePlacement.RoomHandle(b.Map,b.X*2,b.Y*2),
            Flags=0x20,Size5c=2,Mask60=0x100,Mask64=0x1c09,Actor=new(checked((int)b.Serial.Value),1)};
        return new(new(new(cls,0),true,1,cls,2,2,0),path,1,1,TimedEffects.Now);
    }
    private static void Start()
    {
        var p=new PlayerMobile{Name="Goatman decision root fixture",Player=true};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,47);
        ClassSkillChecks.Train(p,"warlock.summongoatman",6);ClassSkillChecks.Train(p,"warlock.demonicmastery",16);
        FixtureSpace.Corridor(p,6880,24,3790);WarlockGoatmanBody b=null;GoatmanNpcContactChecks.Recipient t=null;bool kept=false;
        try
        {
            b=WarlockGoatmanBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now,new(1,FixturePath));
            t=new(){CantWalk=true,Frozen=true,NoKillAwards=true};t.SetHits(50000);t.Hits=t.HitsMax;t.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);t.AIObject?.AITimer.Stop();
            int actor=checked((int)b.Serial.Value),owner=checked((int)p.Serial.Value),target=checked((int)t.Serial.Value);var meta=GenericPetMetadata.Table.Bind(1);
            Mobile Unit(int id)=>World.FindMobile((Serial)(uint)id)??throw new NotSupportedException("Missing native decision fixture unit");
            GenericPetDecisionUnit Position(int id){var m=Unit(id);return new(m is PlayerMobile?0:1,true,m.X*2,m.Y*2,m is PlayerMobile,p.X*2,p.Y*2);}
            GenericPetSearchWrapperUnit Wrapper(int id)
            {
                var m=Unit(id);int cls=m==b?742:m is BaseCreature c?GenericPetSearchTables.SourceId(CreatureProfiles.Definition(c).Reference)??-1:-1;
                return new(new(Position(id),m.Alive?1:12,m.Alive?4u:0x10000u,cls,1,m is BaseCreature,false,m.X*2*65536,m.Y*2*65536),0,0);
            }
            bool Valid(int id)=>Unit(id)is BaseCreature c&&c.Map==b.Map&&c.Alive&&ClassCombat.IsHostile(p,c);
            var callback=new GenericPetSearchServices(id=>Wrapper(id).Unit,(_,id,_)=>Valid(id)?1:0,
                (id,x,y)=>GenericPetSearch.Squared(Position(id),x,y),_=>new(0,0,2),(a,z,_)=>Unit(a).InLOS(Unit(z))?0:1,
                (_,_)=>0,(_,_)=>throw new NotSupportedException("Unused long-range search service"),_=>0,_=>0,
                (selector,cls)=>GenericPetSearchTables.Bind(1).Monster(selector,cls)?.Threat137,(_,_)=>false);
            var search=new GenericPetSearchWrapperServices(Wrapper,_=>{},meta.SearchServices(),callback,(_,_,_,visit)=>
            {
                // Declared native traversal leaf. The full source room/owner
                // iterator adapter remains a separate host requirement.
                foreach(var mob in SkillCombat.NearbySnapshot<BaseCreature>(b.Map,b.Location,16).OrderBy(m=>m.Serial.Value)){visit(checked((int)mob.Serial.Value));}
            });
            var init=new GenericPetInitializationServices(new GenericPetWorldIdentity(1).Resolve,GenericPetLifePercent.For,id=>Unit(id)is PlayerMobile?0:1,
                (_,_)=>throw new InvalidOperationException("Binding must use actual owner census"),
                new(_=>throw new InvalidOperationException("Swarm0 should not inspect units"),(_,_)=>false,_=>0,(_,_,_)=>0,_=>{}));
            var selected=new HashSet<int>();int executed=0,followed=0,aiCalls=0;ulong beforeContact=0,afterContact=0;
            bool Deliver(int skill,int id)
            {
                beforeContact=NpcEquipmentRandom.State(b).Seed;executed++;selected.Add(skill);var enemy=(BaseCreature)Unit(id);var at=TimedEffects.Now;
                var prepared=skill==0?GoatmanNpcContact.PrepareBasic(b,p,enemy,at):GoatmanNpcContact.Prepare(b,p,enemy,skill,at);
                if(skill==0){_=GoatmanNpcContact.ReleaseBasic(b,p,enemy,prepared,at);}else{_=GoatmanNpcContact.Release(b,p,enemy,skill,prepared,at);}
                afterContact=NpcEquipmentRandom.State(b).Seed;return true;
            }
            var action=new GenericPetActionServices((_,_,_)=>throw new NotSupportedException("No type6 skills"),Position,
                (x,y)=>GenericPetSearch.Squared(Position(actor),x,y),(_,_)=>false,id=>ReviveMeleeGeometry.Within(b,(BaseCreature)Unit(id),0),
                ()=>throw new InvalidOperationException("Close-target fixture moved"),(_,_,_,_,_)=>throw new InvalidOperationException("Close-target fixture retreated"),()=>{},
                _=>throw new InvalidOperationException("Close target queried a far movement hint"),(_,_)=>throw new InvalidOperationException("Close-target fixture approached"),
                (mode,skill,id,_,_,_)=>{Require(mode==(skill==417?5:4),"Approved NPC skill mode/rank binding differs");return Deliver(skill,id);},
                mode=>new(0,0,0,mode),request=>{Require((int)request.Opaque==4,"Wrong base mode");return Deliver(0,request.Target);},
                (_,_)=>throw new InvalidOperationException("Binding must use owned flags37"),Valid,_=>0);
            var follow=new GenericPetFollowServices(GenericPetSearch.Squared,_=>false,(_,_,_)=>new(true,true,1),_=>throw new InvalidOperationException("Unbound follow executor"));
            var followExec=new GenericPetFollowExecutionServices(()=>(p.X*2,p.Y*2),()=>1,(_,_,_)=>1,(_,_,_)=>1,(_,_)=>0,()=>0,
                (_,_,_)=>{},()=>{},(_,_)=>throw new InvalidOperationException("Unexpected point follow"),(_,_,_,_,_)=>throw new InvalidOperationException("Unexpected random follow"),
                (_,_,_,radius,_,_,_)=>{Require(radius==7,"Native owner census did not supply source follow radius");followed++;return 1;},
                (_,_,_,_,_,_)=>throw new InvalidOperationException("Unexpected alternative follow"),(_,_,_,_)=>0,()=>throw new InvalidOperationException("Unexpected warp"));
            var leaves=new GenericPetCombatServices(()=>throw new InvalidOperationException(),_=>0,_=>0,(_,_,_)=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),
                ()=>GenericPetLifePercent.For(actor),()=>1,(_,_,_,_,_)=>false,()=>{},(_,_,_,_,_)=>false,()=>{},_=>{},(_,_)=>throw new InvalidOperationException(),
                _=>throw new InvalidOperationException("Weighted source profile should not sort"),(_,_,_)=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException());
            for(int low=0;low<256&&selected.Count<5;low++)
            {
                SkillHealth.SetBalance256(t,20000*256);NpcEquipmentRandom.Set(b,SourceRandom.InitLow(low));ulong expected=NpcEquipmentRandom.State(b).Seed;_=SourceRandom.Roll(ref expected);
                var random=new GenericPetActionRandom(NpcEquipmentRandom.State(b).Seed);int prior=executed;
                var binding=GoatmanNativeDecision.Bind(b,p,random,Position,init,search,leaves,follow,followExec,action,(_,_)=>0,_=>2,_=>throw new InvalidOperationException("Combat should execute"));
                var events=new GenericPetTimedEvents();events.Schedule(actor,2,2,()=>new(0,1,1,GenericPetMetadata.Table.StateCount,true,-1,0,4),()=>{});
                var root=events.Bind(binding.Context,binding.Services,actor);GenericPetDecisions.Think(root.Context,root.Services);aiCalls++;
                Require(executed==prior+1&&beforeContact==expected&&NpcEquipmentRandom.State(b).Seed==afterContact&&random.Seed==afterContact
                    &&events.Unit(actor).Length==0,$"Source root differs:low={low},executed={executed},prior={prior},before={beforeContact},expectedAI={expected},stored={NpcEquipmentRandom.State(b).Seed},after={afterContact},local={random.Seed},events={events.Unit(actor).Length}");
            }
            Require(selected.SetEquals(new[]{0,416,417,418,419})&&aiCalls>=5,"Actual root did not select all base/unlocked skills");
            t.Internalize();NpcEquipmentRandom.Set(b,SourceRandom.InitLow(173));var quietRandom=new GenericPetActionRandom(NpcEquipmentRandom.State(b).Seed);
            var quiet=GoatmanNativeDecision.Bind(b,p,quietRandom,Position,init,search,leaves,follow,followExec,action,(_,_)=>0,_=>2,_=>throw new InvalidOperationException("Follow should handle idle"));
            GenericPetDecisions.Think(quiet.Context,quiet.Services);Require(followed==1&&NpcEquipmentRandom.State(b).Seed==quietRandom.Seed,"Empty combat did not use full follow executor/census");
            SkillHealth.SetBalance256(b,b.MaximumLife256-257);PersistentMinions.Suspend(p);b.RestorePrepared(b.Prepared with{NextRecoveryUtc=TimedEffects.Now.AddDays(2)});
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,b.Serial.Value,b.SavedText,DeathMarkWorld.State(b),NpcEquipmentRandom.State(b),SkillHealth.Balance256(b))));kept=true;
            Log.Information("Goatman decision root:{Calls} weighted native decisions select all5actions;real source search/scoring/HP,shared combat RNG,type2removal,follow census and persistent controller words;entry/movement/timed execution leaves remain explicit",aiCalls);
        }
        finally{t?.Delete();if(!kept){b?.Delete();p.Delete();}}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var b=World.FindMobile((Serial)s.Body)as WarlockGoatmanBody;
        Require(b!=null&&b.ControlMaster?.Serial.Value==s.Owner&&b.SavedText==s.Birth&&DeathMarkWorld.State(b)==s.Controller
            &&NpcEquipmentRandom.State(b)==s.Random&&SkillHealth.Balance256(b)==s.Life,"Original decision/controller/RNG/wounds changed on load");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
