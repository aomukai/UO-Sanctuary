// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
namespace Sanctuary.Integration;

public static class ParkingBoundaryChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ParkingBoundaryChecks));
    private sealed record Pet(uint Serial,string Type,int Life,int Maximum,int Rank,int Mastery,int Slots,long Order);
    private sealed record Saved(uint Owner,Point3D Point,int Map,Pet[] Pets,ReviveNativeState[] Revives,int[] ReviveLife,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"parking-boundary-fixtures.json");
    public static void Configure()
    {
        Register("parking-boundary-check",Check);Register("parking-boundary-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("parking-boundary-live","Connected parked army facet transitions",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("parking-boundary-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Actual safe-area facet/legacy/captured-resource boundaries",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=MagePetBuffChecks.Owner(1,6600,3000);p.RawInt=200;
        foreach(string id in new[]{"claygolem","bloodgolem","firegolem","revive"}){MagePetBuffChecks.Train(p,"necromancer."+id,id=="revive"?2:1);}
        return p;
    }
    private static GuardedRegion Town(Map map,Point3D at)
    {var r=new GuardedRegion("Parking boundary "+Guid.NewGuid(),map,1000,new Rectangle2D(at.X-2,at.Y-2,5,5));r.Register();return r;}
    private static SkeletonGuardian[] Pets(PlayerMobile p)
    {
        var skeleton=new SkeletonGuardian();skeleton.Initialize(p,7,2);skeleton.ControlSlots=2;Require(skeleton.SetControlMaster(p),"Legacy skeleton owner");skeleton.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);PersistentMinions.Profiles.Track(skeleton);BattleCommand.Track(skeleton);skeleton.AIObject.AITimer.Stop();
        var pets=new[]{CombatAbility.RaiseSkeletalMage,CombatAbility.ClayGolem,CombatAbility.BloodGolem,CombatAbility.FireGolem}.Select(kind=>
        {var m=new NecromancerMinion();m.Prepare(p,kind);Require(m.SetControlMaster(p),"Legacy minion owner");m.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);PersistentMinions.Profiles.Track(m);BattleCommand.Track(m);m.AIObject.AITimer.Stop();return(SkeletonGuardian)m;}).Prepend(skeleton).ToArray();
        PersistentMinions.Resume(p);foreach(var m in pets.OfType<NecromancerMinion>().Where(x=>x.IsMage)){m.LegacyMageWorld.World.Store(m.LegacyMageWorld.World.State.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});}
        foreach(var pet in pets){pet.RecoverLife(Core.TickCount);SkillHealth.SetBalance256(pet,SkillHealth.Maximum256(pet)/2+17);}
        return pets;
    }
    private static Pet Snapshot(SkeletonGuardian p)=>new(p.Serial.Value,p.GetType().FullName,SkillHealth.Balance256(p),SkillHealth.Maximum256(p),p.Rank,p.Mastery,PersistentMinions.Profiles.Track(p).Slots,BattleCommand.PetOrder(p));
    private static void Parked(PlayerMobile p,SkeletonGuardian[] pets)
    {foreach(var pet in pets){Require(pet.Map==Map.Internal&&pet.ControlMaster==p&&pet.ControlSlots==0&&PersistentMinions.Profiles.Find(pet).Parked,"Legacy army escaped safe-area parking");}}
    private static void Move(PlayerMobile p,SkeletonGuardian[] pets,BaseCreature[] revives,ReviveNativeState[] births,Point3D at,Map map,MonsterDifficulty difficulty)
    {
        var nativeBefore=revives.Select(ReviveNativeActivation.State).ToArray();var before=pets.Select(Snapshot).ToArray();var life=revives.Select(SkillHealth.Balance256).ToArray();var maximum=revives.Select(SkillHealth.Maximum256).ToArray();int mana=NecromancerEffects.ManaBalance256(p);var deadlines=(p.NextSkillTime,p.NextSpellTime,ClassCombat.For(p).NextAction);
        p.MoveToWorld(at,map);Parked(p,pets);
        for(int i=0;i<pets.Length;i++)
        {
            var current=Snapshot(pets[i]);var old=before[i];int expected=old.Life==0?0:(int)Math.Max(1,(long)old.Life*current.Maximum/old.Maximum);
            Require(pets[i].SummonDifficulty==difficulty&&current.Serial==old.Serial&&current.Type==old.Type&&current.Life==expected&&current.Rank==old.Rank&&current.Mastery==old.Mastery&&current.Slots==old.Slots&&current.Order==old.Order,"Parked facet transition reset identity/capture/slots or injured life ratio");
        }
        for(int i=0;i<revives.Length;i++)
        {
            if(nativeBefore[i].Difficulty==difficulty){Require(ReviveNativeActivation.State(revives[i])==nativeBefore[i]&&SkillHealth.Balance256(revives[i])==life[i]&&SkillHealth.Maximum256(revives[i])==maximum[i],"Same-difficulty safe entry rewrote original Revive state/resources");}
            else{ReviveFacetChecks.Expect(revives[i],births[i],life[i],maximum[i],difficulty);}
            Require(revives[i].Map==Map.Internal&&MinionSafeAreas.Parked(revives[i]),"Original Revive escaped safe facet");
        }
        var stable=pets.Select(Snapshot).ToArray();SummonDifficulties.Sync(p);Require(stable.SequenceEqual(pets.Select(Snapshot)),"Repeated parked difficulty synchronization rounded/healed again");
        Require(mana==NecromancerEffects.ManaBalance256(p)&&deadlines==(p.NextSkillTime,p.NextSpellTime,ClassCombat.For(p).NextAction),"Parked transition recast or changed owner resources/deadlines");
    }
    private static void Check()
    {
        var old=SummonDifficulties.Facets();var p=Owner();SkeletonGuardian[] pets=[];var origin=p.Location;GuardedRegion townA=null,townB=null;BaseCreature[] actors=[];
        try
        {
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Nightmare)]);
            var first=ReviveFacetChecks.Create(p);var second=ReviveFacetChecks.Create(p,true);actors=[first.Actor,second.Actor];pets=Pets(p);var births=actors.Select(ReviveNativeActivation.State).ToArray();foreach(var a in actors){SkillHealth.SetBalance256(a,SkillHealth.Maximum256(a)/2+17);}townA=Town(Map.Trammel,origin);townB=Town(Map.Felucca,origin);
            // Creation can suspend an offline owner's existing army. Safety
            // entry must still record/release each original slot cost once.
            Move(p,pets,actors,births,new(origin.X+1,origin.Y,origin.Z),Map.Trammel,MonsterDifficulty.Normal);
            Move(p,pets,actors,births,origin,Map.Felucca,MonsterDifficulty.Nightmare);
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
            Move(p,pets,actors,births,new(origin.X+1,origin.Y,origin.Z),Map.Felucca,MonsterDifficulty.Hell);
            Move(p,pets,actors,births,origin,Map.Trammel,MonsterDifficulty.Normal);
            Move(p,pets,actors,births,origin,Map.Felucca,MonsterDifficulty.Hell);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,p.Location,p.Map.MapID,pets.Select(Snapshot).ToArray(),actors.Select(ReviveNativeActivation.State).ToArray(),actors.Select(SkillHealth.Balance256).ToArray(),0)));p.Internalize();
            Log.Information("Parking boundaries:five original legacy kinds and two actual learned original Revives,Normal/Nightmare/Hell safe facets,independent FP256 ratio,captured ranks/identity/order/slots,no repeated rounding or owner recast/deadline changes pass");
        }
        finally{townA?.Unregister();townB?.Unregister();SummonDifficulties.SetFacets(old);}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var pets=s.Pets.Select(x=>(SkeletonGuardian)World.FindMobile((Serial)x.Serial)).ToArray();Parked(p,pets);Require(s.Generation<2&&s.Pets.SequenceEqual(pets.Select(Snapshot)),"Saved parked legacy capture/wounds/slots changed");
        for(int i=0;i<s.Revives.Length;i++){var row=s.Revives[i];var actor=(BaseCreature)World.FindMobile((Serial)row.Actor);Require(actor.ControlMaster==p&&actor.Map==Map.Internal&&ReviveNativeActivation.State(actor)==row&&SkillHealth.Balance256(actor)==s.ReviveLife[i],"Saved original parked Revive changed");}
        var old=SummonDifficulties.Facets();
        try
        {
            SummonDifficulties.SetFacets([new(s.Map,MonsterDifficulty.Hell)]);p.MoveToWorld(s.Point,Map.Maps[s.Map]);var town=Town(p.Map,s.Point);
            try{PersistentMinions.Resume(p);Parked(p,pets);Require(s.Pets.SequenceEqual(pets.Select(Snapshot)),"Login/resume in safe Hell changed parked actors");}finally{town.Unregister();}
            p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
        }finally{SummonDifficulties.SetFacets(old);}
    }
    private static void Live(NetState network)
    {
        var old=SummonDifficulties.Facets();var p=Owner();FixtureNetwork.Authenticate(network,p,"park-facet");var pets=Pets(p);var origin=p.Location;var a=Town(Map.Trammel,origin);var b=Town(Map.Felucca,origin);
        void End(){a.Unregister();b.Unregister();foreach(var pet in pets){pet.Delete();}p.Delete();SummonDifficulties.SetFacets(old);}
        try
        {
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
            Move(p,pets,[],[],new(origin.X+1,origin.Y,origin.Z),Map.Trammel,MonsterDifficulty.Normal);
            Move(p,pets,[],[],origin,Map.Felucca,MonsterDifficulty.Hell);var held=pets.Select(Snapshot).ToArray();
            Timer.StartTimer(TimeSpan.FromMilliseconds(250),()=>
            {try{Require(network.Running&&held.SequenceEqual(pets.Select(Snapshot)),"Parked legacy army regenerated while idle");Parked(p,pets);p.MoveToWorld(new(origin.X+8,origin.Y,origin.Z),Map.Trammel);Require(pets.All(x=>x.Map==p.Map&&!MinionSafeAreas.Parked(x)),"Actual cross-facet safe exit did not return legacy roster");End();Log.Information("parking-boundary-live-result: passed");}catch(Exception e){End();Log.Error(e,"parking-boundary-live-result: failed");}});
            Log.Information("parking-boundary-live: ready");
        }catch{End();throw;}
    }
}
