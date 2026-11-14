// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class GoatmanNativeSearchChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GoatmanNativeSearchChecks));
    private sealed record Saved(int Generation,uint Owner,uint Body,string Birth,DeathMarkSavedActor Controller,NpcEquipmentSeed Random,int Life);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"goatman-native-search-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"goatman-native-search-reload-check":"goatman-native-search-check";
            ConsoleInputHandler.RegisterCommand(name,"Native room traversal through actual Goatman decisions/timed attacks",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>Scene(n,!reload,name+"-result"));}
                catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static GenericPetBirthPath Fixture(SkeletonGuardian b)
    {
        // Declared walking-qualified input,not a production birth constructor.
        var p=new GenericPetCommitPath{X=((uint)(b.X*2)<<16)|32768,Y=((uint)(b.Y*2)<<16)|32768,Flags=1,
            Room=GenericPetNativePlacement.RoomHandle(b.Map,b.X*2,b.Y*2),Size5c=2,Mask60=0x100,Mask64=0x3401,Actor=new(checked((int)b.Serial.Value),1)};
        return new(new(new(742,0),true,1,742,2,2,0),p,1,1,TimedEffects.Now);
    }
    private static GoatmanNpcContactChecks.Recipient Target(PlayerMobile p,Point3D at)
    {
        var t=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(50000);t.Hits=t.HitsMax;
        t.MoveToWorld(at,p.Map);t.AIObject?.AITimer.Stop();return t;
    }
    private static void Scene(NetState network,bool retain,string result)
    {
        var p=new PlayerMobile{Name="Goatman native search fixture",Player=true};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,47);
        ClassSkillChecks.Train(p,"warlock.summongoatman",6);ClassSkillChecks.Train(p,"warlock.demonicmastery",16);
        FixtureSpace.Corridor(p,6800,28,3700);var account=FixtureNetwork.Authenticate(network,p,"goatman-search-");
        WarlockGoatmanBody b=null;GoatmanNpcContactChecks.Recipient t=null,other=null,friend=null,ray=null;Corpse corpse=null;Static wall=null;
        bool kept=false;var stuns=CombatStuns.Snapshot();
        try
        {
            DateTime at=TimedEffects.Now;int bx=(p.X/16+1)*16-1;b=WarlockGoatmanBody.CreatePrepared(p,new(bx,p.Y,p.Z),at,new(1,Fixture));
            t=Target(p,new(b.X+1,b.Y,b.Z));other=Target(p,t.Location);friend=Target(p,b.Location);Require(friend.SetControlMaster(p),"Friendly control fixture");
            int actor=checked((int)b.Serial.Value),target=checked((int)t.Serial.Value),alternative=checked((int)other.Serial.Value);
            var parameters=GoatmanPrepared.RawAiParameters;var world=new GoatmanNativeSearch(b,p);var context=new GenericPetSearchWrapperContext(1,actor,checked((int)p.Serial.Value),parameters,[]);
            Require(world.Room(actor)!=world.Room(target)&&world.Level(world.Position(actor))==null,"Room-boundary/wilderness-level controls not established");
            var choice=GenericPetSearchWrapper.Run(context,[],1,0,world.Search);
            Require(choice.Targets[0]==target&&choice.Word40!=0,"Actual adjacent-room traversal/serial tie order did not select the first enemy");
            Require(GenericPetSearchWrapper.Run(context,[],1,checked((int)friend.Serial.Value),world.Search).Targets.All(x=>x==0),"Controlled ally entered source enemy search");
            t.Blessed=true;world=new(b,p);Require(GenericPetSearchWrapper.Run(context,[],1,0,world.Search).Targets[0]==alternative,"Invulnerable enemy was selected");t.Blessed=false;
            var oldMap=other.Map;var oldPoint=other.Location;other.MoveToWorld(new(100,100,0),Map.Felucca);world=new(b,p);
            Require(GenericPetSearchWrapper.Run(context,[],1,alternative,world.Search).Targets.All(x=>x==0),"Explicit enemy on another facet stayed eligible");other.MoveToWorld(oldPoint,oldMap);
            // Reuse the established actual shrunk-endpoint wall fixture.
            ray=Target(p,new(b.X+6,b.Y,b.Z));world=new(b,p);int rid=checked((int)ray.Serial.Value);
            Require(GenericPetSearchWrapper.Run(context,[],1,rid,world.Search).Targets[0]==rid,"Clear native source-size ray was blocked");
            int tile=Enumerable.Range(1,0x200).First(id=>(TileData.ItemTable[id].Flags&(TileFlag.Window|TileFlag.NoShoot))!=0
                &&(TileData.ItemTable[id].Flags&TileFlag.Door)==0&&TileData.ItemTable[id].CalcHeight>=16);
            wall=new Static(tile);wall.MoveToWorld(new(b.X+3,b.Y,b.Z),b.Map);world=new(b,p);
            Require(GenericPetSearchWrapper.Run(context,[],1,rid,world.Search).Targets.All(x=>x==0),"Actual native wall did not block search LOS");wall.Delete();wall=null;ray.Delete();ray=null;
            corpse=DeathSentryExplosionChecks.Body(p,new(p.X+1,p.Y,p.Z));corpse.Owner?.Delete();int dead=SourceCorpseIdentity.For(corpse).Id;world=new(b,p);
            Require(world.Position(dead).Kind==1&&GenericPetSearchWrapper.Run(context,[],1,dead,world.Search).Targets.All(x=>x==0),"Historical dead target crashed or entered melee search");
            // Complete current decision root -> actual native traversal and
            // scorer -> timed action interface -> ordered native HP receiver.
            int frame=20000000;DateTime Time(int f)=>at.AddTicks((long)(f-20000000)*SourceFrameTimeline.IntervalTicks);
            var combat=new GoatmanNativeCombat(b,p);var selected=new HashSet<int>();int decisions=0;
            var actions=new GenericPetActionServices((_,_,_)=>throw new InvalidOperationException(),id=>world.Position(id),
                (x,y)=>GenericPetSearch.Squared(world.Position(actor),x,y),(_,_)=>false,
                id=>ReviveMeleeGeometry.Within(b,(BaseCreature)Server.World.FindMobile((Serial)(uint)id),0),
                ()=>throw new InvalidOperationException("Close-target fixture moved"),(_,_,_,_,_)=>throw new InvalidOperationException(),()=>{},
                _=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),(_,_,_,_,_,_)=>throw new InvalidOperationException(),
                _=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),
                id=>world.Search.Callback.Valid372ec0(actor,id,1)!=0,_=>0);
            actions=combat.Bind(actions,()=>frame,()=>Time(frame));
            var leaves=new GenericPetCombatServices(()=>throw new InvalidOperationException(),_=>0,_=>0,(_,_,_)=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),
                ()=>GenericPetLifePercent.For(actor),()=>world.Room(actor),(_,_,_,_,_)=>false,()=>throw new InvalidOperationException(),
                (_,_,_,_,_)=>throw new InvalidOperationException(),()=>{},_=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),
                _=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException());
            var follow=new GenericPetFollowServices(GenericPetSearch.Squared,_=>false,(_,_,_)=>throw new InvalidOperationException(),_=>throw new InvalidOperationException());
            var followExec=new GenericPetFollowExecutionServices(()=>throw new InvalidOperationException(),()=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException(),
                (_,_,_)=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),()=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException(),
                ()=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),(_,_,_,_,_)=>throw new InvalidOperationException(),
                (_,_,_,_,_,_,_)=>throw new InvalidOperationException(),(_,_,_,_,_,_)=>throw new InvalidOperationException(),(_,_,_,_)=>throw new InvalidOperationException(),()=>throw new InvalidOperationException());
            for(int low=0;low<256&&selected.Count<5;low++)
            {
                CombatStuns.Restore(stuns);SkillHealth.SetBalance256(t,20000*256);SkillHealth.SetBalance256(other,20000*256);t.Notifications=0;
                world=new(b,p);NpcEquipmentRandom.Set(b,SourceRandom.InitLow(low));var random=new GenericPetActionRandom(NpcEquipmentRandom.State(b).Seed);
                var binding=world.Bind(random,leaves,follow,followExec,actions,(_,_)=>0,_=>throw new InvalidOperationException("Actual enemy should be attacked"));
                GenericPetDecisions.Think(binding.Context,binding.Services);var a=combat.Current;
                Require(a!=null&&a.Target==t&&DeathMarkWorld.State(b).Target48==target,"Actual source decision lost its native target/command");
                selected.Add(a.Skill);int before=SkillHealth.Balance256(t);combat.Advance(a.ReleaseFrame,Time(a.ReleaseFrame));
                Require(a.Released&&a.Contacts>=1&&t.Notifications<=1&&SkillHealth.Balance256(t)<=before,"Actual native decision did not deliver once at contact");
                combat.Advance(a.EndFrame,Time(a.EndFrame));Require(combat.Current==null&&DeathMarkWorld.State(b).Mode==1,"Timed root action did not finish");
                frame=a.EndFrame+2;decisions++;
            }
            Require(selected.SetEquals(new[]{0,416,417,418,419}),"Real room/search/scorer/root did not select every unlocked action");
            // Stale native owner combat pointers are cleared through the same
            // source traversal refresh,not resurrected by a wrapper snapshot.
            var stale=Target(p,new(b.X+2,b.Y+1,b.Z));p.Combatant=stale;ClassCombat.For(p).Target=stale.Serial;world=new(b,p);
            _=world.Search.Unit(checked((int)p.Serial.Value));stale.Delete();world.Search.Refresh371b10(checked((int)p.Serial.Value));
            Require(p.Combatant==null&&ClassCombat.For(p).Target==Serial.Zero&&world.Search.Unit(checked((int)p.Serial.Value)).PathTarget==0,"Native stale owner path target was resurrected");
            Require(WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Learned Goatman route missing after approved host integration");
            if(retain)
            {
                SkillHealth.SetBalance256(b,b.MaximumLife256-257);PersistentMinions.Suspend(p);b.RestorePrepared(b.Prepared with{NextRecoveryUtc=TimedEffects.Now.AddDays(2)});
                File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,b.Serial.Value,b.SavedText,DeathMarkWorld.State(b),NpcEquipmentRandom.State(b),SkillHealth.Balance256(b))));kept=true;
            }
            Log.Information("Goatman native search:{Decisions} actual room/search/scorer/root decisions select all5timed actions;adjacent rooms,serial ties,ally/invulnerable/facet/corpse exclusion,source-sized native LOS and stale owner target refresh;automatic host still gated",decisions);
            Log.Information("{Result}: passed",result);
        }
        catch(Exception e){Log.Error(e,"{Result}: failed",result);}
        finally
        {
            CombatStuns.Restore(stuns);wall?.Delete();corpse?.Owner?.Delete();corpse?.Delete();ray?.Delete();friend?.Delete();other?.Delete();t?.Delete();
            network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){b?.Delete();p.Delete();}
        }
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var b=Server.World.FindMobile((Serial)s.Body)as WarlockGoatmanBody;
        Require(b!=null&&b.ControlMaster?.Serial.Value==s.Owner&&b.SavedText==s.Birth&&DeathMarkWorld.State(b)==s.Controller
            &&NpcEquipmentRandom.State(b)==s.Random&&SkillHealth.Balance256(b)==s.Life,"Original native-search controller/birth/Frenzy/wounds/RNG changed on reload");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
