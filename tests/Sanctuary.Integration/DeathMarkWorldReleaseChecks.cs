// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DeathMarkWorldReleaseChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DeathMarkWorldReleaseChecks));
    private sealed record Pet(uint Serial,string Birth,int Life,int Maximum,int Mana,long Order,Point3D Point,DeathMarkSavedActor Command);
    private sealed record Saved(uint Owner,uint Target,DeathMarkDebuff Mark,Pet[] Pets,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"death-mark-world-release-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static string Birth(SkeletonGuardian b)=>b switch{WarlockGoatmanBody g=>g.SavedText,WarlockTaintedBody t=>t.SavedText,WarlockDefilerBody d=>d.SavedText,_=>throw new NotSupportedException()};
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true})
        {
            string name=reload?"death-mark-world-release-reload-check":"death-mark-world-release-check";bool again=reload;
            ConsoleInputHandler.RegisterCommand(name,"Captured Death Mark composition with actual original pets and approved recipient",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",network=>
            {try{if(again){Reload(network);}else{Check(network);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
    }
    private static int Cast(PlayerMobile owner,BaseCreature target,SkeletonGuardian[] pets,int rank,int dx,int dy,int castId=0)
    {
        var point=new Point2D((owner.X+dx)*2,(owner.Y+dy)*2);
        var context=new DeathMarkCastContext(owner.Serial.Value,new(0,true,owner.X*2,owner.Y*2),true,375,rank,1,castId,20,21,212,213);
        // Explicit source room/level/target environment supplied by this test.
        // No D2 Act/level IDs or level-byte14 values are inferred from UO facets.
        void Unbound()=>throw new InvalidOperationException("World adapter left an effect service unbound");
        var environment=new DeathMarkCastServices(()=>true,()=>true,()=>{},_=>throw new InvalidOperationException(),()=>point,()=>2,()=>1,
            (_,_)=>throw new InvalidOperationException("Mode1mustnotcallmode2collision"),()=>{},()=>target==null?null:new(target.Serial.Value,1),
            t=>World.FindMobile((Serial)t.Id)==target&&ClassCombat.IsHostile(owner,target),_=>2,(_,_)=>Unbound(),_=>throw new InvalidOperationException(),Unbound,Unbound,(_,_)=>Unbound());
        return DeathMarkWorldRelease.RunPrepared(owner,context,environment,pets,TimedEffects.Now);
    }
    private static Pet Capture(SkeletonGuardian b)=>new(b.Serial.Value,Birth(b),SkillHealth.Balance256(b),SkillHealth.Maximum256(b),b.Mana,BattleCommand.PetOrder(b),b.Location,DeathMarkWorld.State(b));
    private static void Unchanged(SkeletonGuardian body,Pet before,bool command=false)
    {
        Require(body.Serial.Value==before.Serial&&Birth(body)==before.Birth&&SkillHealth.Balance256(body)==before.Life&&SkillHealth.Maximum256(body)==before.Maximum
            &&body.Mana==before.Mana&&BattleCommand.PetOrder(body)==before.Order,"Cast rebuilt/healed/reset original demon");
        if(command){Require(body.Location==before.Point&&DeathMarkWorld.State(body)==before.Command,"Rejected/quota-limited cast moved/reset demon");}
    }
    private static void Check(NetState network)
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Warlock);FixtureSpace.Corridor(owner,6400,40,3100);FixtureNetwork.Authenticate(network,owner,"DeathMarkRelease");
        var target=PoisonDaggerChecks.Target(owner);target.MoveToWorld(new(owner.X+10,owner.Y,owner.Z),owner.Map);
        SkeletonGuardian[] pets=[WarlockGoatmanBody.CreatePrepared(owner,new(owner.X+1,owner.Y,owner.Z),TimedEffects.Now),
            WarlockTaintedBody.CreatePrepared(owner,new(owner.X+3,owner.Y,owner.Z),TimedEffects.Now),
            WarlockDefilerBody.CreatePrepared(owner,new(owner.X+5,owner.Y,owner.Z),TimedEffects.Now),
            WarlockGoatmanBody.CreatePrepared(owner,new(owner.X+7,owner.Y,owner.Z),TimedEffects.Now)];
        // Four supplied actors intentionally exercise the captured three-pet quota;
        // this is not a learned summon-cap admission.
        foreach(var pet in pets){DeathMarkWorldChecks.Register(pet);SkillHealth.SetBalance256(pet,SkillHealth.Maximum256(pet)-333);}
        var before=pets.Select(Capture).ToArray();int mana=NecromancerEffects.ManaBalance256(owner);
        Require(Cast(owner,target,[],255,7,0)==1&&DeathMarkDebuffs.State(target)==null,"No successful relocation still marked target");
        Require(Cast(owner,target,pets,255,20,0)==0,"Source range gate accepted beyond38sourceunits");
        for(int i=0;i<pets.Length;i++){Unchanged(pets[i],before[i],true);}
        bool refused=false;try{Cast(owner,target,[pets[0],pets[0]],255,7,0);}catch(NotSupportedException){refused=true;}
        Require(refused&&DeathMarkDebuffs.State(target)==null,"Duplicate roster mutated native actors");Unchanged(pets[0],before[0],true);
        Require(Cast(owner,target,pets,255,7,1,123456)==1,"Actual captured composition refused world release");
        var mark=DeathMarkDebuffs.State(target);Require(mark is{Rank:255,CastId:123456}&&mark.Creator==owner.Serial.Value,"Ordered skill/rank/cast metadata did not reach saved mark");
        for(int i=0;i<3;i++)
        {
            Unchanged(pets[i],before[i]);var command=DeathMarkWorld.State(pets[i]);
            Require(pets[i].Location!=before[i].Point&&command.Type4c==2&&command.Target48==checked((int)target.Serial.Value)&&command.Mode==1,"Original demon did not receive real relocation/enemy command");
        }
        Unchanged(pets[3],before[3],true);
        var first=pets.Take(3).Select(Capture).ToArray();
        Require(Cast(owner,target,pets,1,7,2,222)==0&&DeathMarkDebuffs.State(target)==mark,"Weaker mark did not fail after relocation without replacing state");
        for(int i=0;i<3;i++){Unchanged(pets[i],first[i]);Require(pets[i].Location!=first[i].Point,"Allocation refusal rolled back earlier pet relocation");}
        Require(Cast(owner,null,pets,255,7,3)==1&&DeathMarkDebuffs.State(target)==mark,"Ground warp incorrectly applied/changed target mark");
        Require(Cast(owner,target,pets,255,7,4,654321)==1&&DeathMarkDebuffs.State(target).CastId==654321,"Repeated enemy release did not update cast metadata");
        Require(NecromancerEffects.ManaBalance256(owner)==mana&&!WarlockCombat.Implemented(CombatAbility.DeathMark),"Prepared transaction spent player mana or admitted learned route");
        var legacy=JsonSerializer.Deserialize<DeathMarkDebuff>("{\"Target\":1,\"Creator\":2,\"Rank\":1,\"UntilUtc\":\"2026-11-09T00:00:00Z\"}");
        Require(legacy.CastId==0,"Previous debuff JSON cannot load without cast metadata");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,target.Serial.Value,DeathMarkDebuffs.State(target),pets.Select(Capture).ToArray(),0)));
        Log.Information("Death Mark world release:actual original three demon species relocate and command through current do155;shared3quota leaves fourth untouched;no-pet/range/duplicate controls;weaker recipient refusal preserves already-completed relocation;ground-only warp;ordered350/351/202metadata and original wounds/resources persist. Automatic AI,source room providers,payment and learned input remain separate.");
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)s.Owner);var target=(BaseCreature)World.FindMobile((Serial)s.Target);
        Require(s.Generation<2&&DeathMarkDebuffs.State(target)==s.Mark&&s.Mark.CastId==654321,"Saved original target state or cast metadata changed");
        FixtureNetwork.Authenticate(network,owner,"DeathMarkReleaseReload");owner.MoveToWorld(new(s.Pets[0].Point.X-7,s.Pets[0].Point.Y-4,s.Pets[0].Point.Z),Map.Trammel);
        var pets=s.Pets.Select(p=>(SkeletonGuardian)World.FindMobile((Serial)p.Serial)).ToArray();
        for(int i=0;i<pets.Length;i++){Unchanged(pets[i],s.Pets[i]);Require(DeathMarkWorld.State(pets[i])==s.Pets[i].Command,"Saved pet command changed");pets[i].MoveToWorld(s.Pets[i].Point,Map.Maps[s.Pets[i].Command.Map]);pets[i].AIObject?.AITimer.Stop();}
        Require(Cast(owner,null,pets,255,7,5)==1&&DeathMarkDebuffs.State(target)==s.Mark,"Reloaded source transaction failed original ground relocation or changed saved mark");
        for(int i=0;i<pets.Length;i++){Unchanged(pets[i],s.Pets[i],i==3);}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Pets=pets.Select(Capture).ToArray(),Generation=s.Generation+1}));
    }
}
