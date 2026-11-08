// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DeathMarkNativeChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DeathMarkNativeChecks));
    private sealed record Pet(uint Serial,Point3D Point,int Life,string Birth,long Order);
    private sealed record Saved(uint Owner,uint Target,Pet[] Pets,DeathMarkDebuff Mark,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"death-mark-native-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true})
        {
            string name=reload?"death-mark-native-reload-check":"death-mark-native-check";bool again=reload;
            ConsoleInputHandler.RegisterCommand(name,"Native Death Mark world proposal:real targeting and saved follower roster",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",network=>
            {try{if(again){Reload(network);}else{Check(network);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
    }
    private static DeathMarkCastContext Context(PlayerMobile owner)=>new(owner.Serial.Value,new(0,true,owner.X*2,owner.Y*2),true,375,255,1,98765,20,21,212,
        checked((int)GenericPetStateMasks.Bind(1).StateCount(1))); // Test supplies a qualified pettype20bound;no new global count claimed.
    private static string Birth(SkeletonGuardian pet)=>pet switch{WarlockGoatmanBody g=>g.SavedText,WarlockTaintedBody t=>t.SavedText,WarlockDefilerBody d=>d.SavedText,_=>throw new NotSupportedException()};
    private static Pet Capture(SkeletonGuardian pet)=>new(pet.Serial.Value,pet.Location,SkillHealth.Balance256(pet),Birth(pet),BattleCommand.PetOrder(pet));
    private static void Point(PlayerMobile owner,Serial selected,Point3D requested,Point3D expected)
        =>Require(DeathMarkNativeRelease.TargetPoint(owner,selected,requested,out var ground,out _)&&ground==expected,"Native shared current target/fallback destination");
    private static void Check(NetState network)
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Warlock);FixtureSpace.Corridor(owner,6400,40,2900);FixtureNetwork.Authenticate(network,owner,"DeathMarkNative");
        var target=PoisonDaggerChecks.Target(owner);target.MoveToWorld(new(owner.X+12,owner.Y,owner.Z),owner.Map);
        var ground=new Point3D(owner.X+5,owner.Y+1,owner.Z);Point(owner,Serial.Zero,ground,ground);Point(owner,owner.Serial,ground,ground);
        Require(!DeathMarkNativeRelease.TargetPoint(owner,Serial.Zero,new(0,owner.Y,owner.Z),out _,out _),"Native zero coordinate escaped current destination validity");
        Point(owner,target.Serial,ground,target.Location);target.MoveToWorld(new(owner.X+11,owner.Y,owner.Z),owner.Map);Point(owner,target.Serial,ground,target.Location);
        var gone=PoisonDaggerChecks.Target(owner);var stale=gone.Serial;gone.Delete();Point(owner,stale,ground,ground);
        Require(DeathMarkNativeRelease.RunPrepared(owner,Context(owner),target.Serial,ground,TimedEffects.Now)is{Result:0,ServerDoRan:false,EligibleDemons:0},"Required demon absence entered cast");
        SkeletonGuardian[] pets=[WarlockGoatmanBody.CreatePrepared(owner,new(owner.X+1,owner.Y,owner.Z),TimedEffects.Now),
            WarlockTaintedBody.CreatePrepared(owner,new(owner.X+3,owner.Y,owner.Z),TimedEffects.Now),WarlockDefilerBody.CreatePrepared(owner,new(owner.X+5,owner.Y,owner.Z),TimedEffects.Now)];
        foreach(var pet in pets){DeathMarkWorldChecks.Register(pet);SkillHealth.SetBalance256(pet,SkillHealth.Maximum256(pet)-333);}
        Require(DeathMarkNativeRelease.Roster(owner).Select(p=>p.Serial).SequenceEqual(pets.Select(p=>p.Serial)),"Native follower order did not use original saved creation ledger");
        // An unregistered live original demon must be flagged,not skipped.
        var unsupported=WarlockGoatmanBody.CreatePrepared(owner,new(owner.X+7,owner.Y,owner.Z),TimedEffects.Now);bool refused=false;
        try{DeathMarkNativeRelease.Roster(owner);}catch(NotSupportedException){refused=true;}Require(refused,"Unregistered original controller was silently skipped");unsupported.Delete();
        var unfinished=new WarlockGoatmanBody();Require(unfinished.SetControlMaster(owner),"Uninitialized original ownership fixture");unfinished.MoveToWorld(new(owner.X+7,owner.Y,owner.Z),owner.Map);refused=false;
        try{DeathMarkNativeRelease.Roster(owner);}catch(NotSupportedException){refused=true;}Require(refused,"Uninitialized original demon was silently skipped");unfinished.Delete();
        var at=TimedEffects.Now;var before=pets.Select(Capture).ToArray();int mana=NecromancerEffects.ManaBalance256(owner);
        var result=DeathMarkNativeRelease.RunPrepared(owner,Context(owner),target.Serial,ground,at);
        Require(result is{Result:1,ServerDoRan:true,EligibleDemons:3}&&result.Destination==target.Location&&DeathMarkDebuffs.State(target)is{Rank:255,CastId:98765},"Native target/roster/world cast did not reach approved recipient");
        for(int i=0;i<pets.Length;i++){Require(Birth(pets[i])==before[i].Birth&&SkillHealth.Balance256(pets[i])==before[i].Life&&pets[i].Location!=before[i].Point
            &&DeathMarkWorld.State(pets[i]).Target48==checked((int)target.Serial.Value),"Native bridge replaced/healed original demon or lost command");}
        var mark=DeathMarkDebuffs.State(target);
        Require(DeathMarkNativeRelease.RunPrepared(owner,Context(owner),Serial.Zero,ground,TimedEffects.Now).Result==1&&DeathMarkDebuffs.State(target)==mark,"Native ground cast changed existing enemy mark");
        target.MoveToWorld(new(owner.X+20,owner.Y,owner.Z),owner.Map);
        Require(DeathMarkNativeRelease.RunPrepared(owner,Context(owner),target.Serial,ground,TimedEffects.Now).Result==0&&DeathMarkDebuffs.State(target)==mark,"Native release bypassed captured38unit range");
        target.MoveToWorld(new(owner.X+11,owner.Y,owner.Z),owner.Map);
        Require(NecromancerEffects.ManaBalance256(owner)==mana&&!WarlockCombat.Implemented(CombatAbility.DeathMark),"Unapproved prepared bridge paid mana/admitted learned skill");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,target.Serial.Value,pets.Select(Capture).ToArray(),mark,0)));
        Log.Information("Death Mark native proposal:real moved/stale/self/ground target resolution;actual follower roster/saved creation order;no demon and unregistered controller guards;actual original3species warp/commands/mark/range without payment/admission. Source selectors/counts/castid remain caller-qualified;native room policy is an explicit pending adaptation.");
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)s.Owner);var target=(BaseCreature)World.FindMobile((Serial)s.Target);
        Require(s.Generation<2&&DeathMarkDebuffs.State(target)==s.Mark,"Native bridge reset saved target mark/castid");FixtureNetwork.Authenticate(network,owner,"DeathMarkNativeReload");
        owner.MoveToWorld(new(s.Pets[0].Point.X-5,s.Pets[0].Point.Y-1,s.Pets[0].Point.Z),Map.Trammel);
        var pets=s.Pets.Select(p=>(SkeletonGuardian)World.FindMobile((Serial)p.Serial)).ToArray();
        for(int i=0;i<pets.Length;i++){Require(Birth(pets[i])==s.Pets[i].Birth&&SkillHealth.Balance256(pets[i])==s.Pets[i].Life&&BattleCommand.PetOrder(pets[i])==s.Pets[i].Order,"Native roster reload rebuilt/reset original demon");pets[i].MoveToWorld(s.Pets[i].Point,owner.Map);pets[i].AIObject?.AITimer.Stop();}
        Require(DeathMarkNativeRelease.Roster(owner).Select(p=>p.Serial).SequenceEqual(pets.Select(p=>p.Serial)),"Saved native roster order changed");
        Require(DeathMarkNativeRelease.RunPrepared(owner,Context(owner),Serial.Zero,new(owner.X+5,owner.Y+2,owner.Z),TimedEffects.Now).Result==1&&DeathMarkDebuffs.State(target)==s.Mark,"Reloaded native roster cannot relocate originals");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Pets=pets.Select(Capture).ToArray(),Generation=s.Generation+1}));
    }
}
