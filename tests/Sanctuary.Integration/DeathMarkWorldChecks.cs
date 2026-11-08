// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DeathMarkWorldChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DeathMarkWorldChecks));
    private sealed record Entry(uint Body,string State,int Life,int Maximum,int Mana,long Order,Point3D Point,DeathMarkSavedActor Command);
    private sealed record Saved(uint Owner,uint Target,Entry[] Entries,int OwnerMana,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"death-mark-world-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static string Text(SkeletonGuardian b)=>b switch{WarlockGoatmanBody g=>g.SavedText,WarlockTaintedBody t=>t.SavedText,WarlockDefilerBody d=>d.SavedText,_=>throw new InvalidOperationException()};
    private static void Invalid(DeathMarkWorldImage image)
    {
        string before=JsonSerializer.Serialize(DeathMarkWorld.Capture());bool refused=false;
        try{DeathMarkWorld.Restore(image);}catch(InvalidDataException){refused=true;}
        Require(refused&&JsonSerializer.Serialize(DeathMarkWorld.Capture())==before,"Malformed world image replaced original graph/commands");
    }
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true})
        {
            string name=reload?"death-mark-world-reload-check":"death-mark-world-check";bool again=reload;
            ConsoleInputHandler.RegisterCommand(name,"Original live demon placement,commands,control managers and saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",network=>
            {try{if(again){Reload(network);}else{Check(network);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}}); }));
        }
    }
    internal static void Register(SkeletonGuardian body)
    {
        int id=WarlockDemonPlacement.ClassId(body);var secondary=GenericPetSecondaryTables.Table.Row(id);
        // Explicit supplied controller path,not a claim of birth-path recovery.
        var path=new GenericPetCommitPath{X=((uint)(body.X*2)<<16)|0x8000,Y=((uint)(body.Y*2)<<16)|0x8000,
            Room=GenericPetNativePlacement.RoomHandle(body.Map,body.X*2,body.Y*2),Flags=0x20,Word30=11,Word34=37,Raw96=0x7788,
            Size5c=1,Mask60=0x100,Mask64=0x1c09,Actor=new(checked((int)body.Serial.Value),1)};
        var unit=new GenericPetMonsterPlacementUnit(new(id,0),true,1,id,secondary.SizeX,secondary.SizeY,37);
        DeathMarkWorld.Register(body,unit,path,1,new(-1,0),4,TimedEffects.Now);
    }
    private static void Live(SkeletonGuardian body,PlayerMobile owner,BaseCreature target,int index)
    {
        var at=TimedEffects.Now;long tick=Core.TickCount;
        Require(CombatStuns.ApplyPreparedDuration(body,200,at),"Stun manager refused original demon");
        Require(FreezeStates.Commit(target,body,500,true,1,tick)&&ColdStates.Commit(target,body,500,true,-50,1,tick),"Freeze/chill manager refused fixture");
        Require(TimedEffects.PoisonFrames(target,body,1,1500,at),"Poison control missing");
        var timed=TimedEffects.Actor(body,at);timed.Control=timed.Control with{FearUntil=at.AddMinutes(1),FearSource=target.Serial.Value,FearX=target.X,FearY=target.Y};
        var curse=new NecromancerCurse(CombatAbility.Weaken,target.Serial.Value,1,at.AddMinutes(1),target.X,target.Y);
        NecromancerEffects.Profiles.Store(body,new(Curse:curse));
        SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)-333);
        int life=SkillHealth.Balance256(body),mana=body.Mana,poison=timed.PoisonMs;var clock=timed.PoisonClock;string saved=Text(body);long order=BattleCommand.PetOrder(body);
        var first=new DeathMarkDirective{X=(owner.X+12+index*3)*2,Y=owner.Y*2,Remaining=1,TargetType=2,TargetId=checked((int)target.Serial.Value),ClearControl=false};
        Require(DeathMarkWorld.ApplyPrepared(body,first,at)&&first.Remaining==0&&CombatStuns.Remaining(body)>0&&FreezeStates.Shattered(body)&&ColdStates.OtherRate(body)==-50,
            "Disabled control clearing changed statuses or failed real relocation");
        var second=new DeathMarkDirective{X=(owner.X+22+index*3)*2,Y=owner.Y*2,Remaining=1,TargetType=2,TargetId=checked((int)target.Serial.Value),ClearControl=true};
        Require(DeathMarkWorld.ApplyPrepared(body,second,at)&&second.Remaining==0&&CombatStuns.Remaining(body)==0&&!FreezeStates.Shattered(body)&&ColdStates.OtherRate(body)==0,
            "Actual source stun/freeze/cold did not clear");
        var command=DeathMarkWorld.State(body);var path=DeathMarkWorld.Path(body);
        Require(command.Target48==checked((int)target.Serial.Value)&&command.Type4c==2&&command.Mode==1&&command.NextAiUtc==at.AddMilliseconds(40)
            &&((ushort)(path.X>>16)+1)/2==body.X&&((ushort)(path.Y>>16)+1)/2==body.Y,"Saved command/neutral request/path disagree with original actor");
        Require(SkillHealth.Balance256(body)==life&&body.Mana==mana&&Text(body)==saved&&BattleCommand.PetOrder(body)==order&&body.ControlMaster==owner
            &&NecromancerEffects.Curse(body)==curse&&timed.Control.FearUntil==at.AddMinutes(1)&&timed.PoisonMs==poison&&timed.PoisonClock==clock,
            "Relocation healed/reset resources,snapshots,ownership,order or unrelated effects");
        int occurrences=0;foreach(var other in body.Map.GetMobilesInRange<Mobile>(body.Location,0)){if(other==body){occurrences++;}}Require(occurrences==1,"Original body missing/duplicated in native sector");
        var before=JsonSerializer.Serialize(DeathMarkWorld.Capture());var exhausted=new DeathMarkDirective{X=second.X,Y=second.Y,Remaining=0};
        Require(!DeathMarkWorld.ApplyPrepared(body,exhausted,at)&&JsonSerializer.Serialize(DeathMarkWorld.Capture())==before,"Exhausted quota moved/reset actor");
        bool rejected=false;try{DeathMarkWorld.ApplyPrepared(body,new(){X=0,Y=0,Remaining=1},at);}catch(NotSupportedException){rejected=true;}
        Require(rejected&&JsonSerializer.Serialize(DeathMarkWorld.Capture())==before,"Invalid destination changed registered graph/commands");
        TimedEffects.CleansePoison(body,0,TimedEffects.Now); // Fixture cleanup after preservation assertion,not Death Mark behavior.
        CombatStuns.Clear(body);body.AIObject?.AITimer.Stop();
    }
    private static void Check(NetState network)
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Warlock);owner.AddItem(new Backpack());FixtureSpace.Corridor(owner,6400,40,3500);FixtureNetwork.Authenticate(network,owner,"DeathMarkWorld");
        var target=PoisonDaggerChecks.Target(owner);target.MoveToWorld(new(owner.X+34,owner.Y,owner.Z),owner.Map);int mana=NecromancerEffects.ManaBalance256(owner);
        SkeletonGuardian[] bodies=[WarlockGoatmanBody.CreatePrepared(owner,new(owner.X+1,owner.Y,owner.Z),TimedEffects.Now),
            WarlockTaintedBody.CreatePrepared(owner,new(owner.X+3,owner.Y,owner.Z),TimedEffects.Now),WarlockDefilerBody.CreatePrepared(owner,new(owner.X+5,owner.Y,owner.Z),TimedEffects.Now)];
        foreach(var body in bodies){Register(body);}
        for(int i=0;i<bodies.Length;i++){Live(bodies[i],owner,target,i);}
        var image=DeathMarkWorld.Capture();Invalid(image with{Actors=image.Actors.Concat([image.Actors[0]]).ToArray()});
        Invalid(image with{Actors=image.Actors.Select((a,i)=>i==0?a with{Type4c=5}:a).ToArray()});Invalid(image with{Actors=[]});
        Require(NecromancerEffects.ManaBalance256(owner)==mana&&!WarlockCombat.Implemented(CombatAbility.DeathMark),"Prepared world route paid owner or admitted unfinished cast");
        var entries=bodies.Select(b=>new Entry(b.Serial.Value,Text(b),SkillHealth.Balance256(b),SkillHealth.Maximum256(b),b.Mana,BattleCommand.PetOrder(b),b.Location,DeathMarkWorld.State(b))).ToArray();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,target.Serial.Value,entries,mana,0)));
        Log.Information("Death Mark world:three original demon species relocate through shared source room/path services;actual stun/freeze/chill managers clear only when requested,poison/fear/curse/wounds/mana/ownership/order unchanged. Commands/neutral AI request and source graph saved;automatic AI and learned cast remain gated.");
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Unexpected world reload generation");var owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;
        var target=World.FindMobile((Serial)saved.Target)as BaseCreature;Require(owner!=null&&target!=null,"Saved owner/target missing");FixtureNetwork.Authenticate(network,owner,"DeathMarkWorldReload");
        owner.MoveToWorld(new(saved.Entries[0].Point.X-22,saved.Entries[0].Point.Y,saved.Entries[0].Point.Z),Map.Trammel);
        foreach(var entry in saved.Entries)
        {
            var body=World.FindMobile((Serial)entry.Body)as SkeletonGuardian;
            Require(body!=null&&Text(body)==entry.State&&SkillHealth.Balance256(body)==entry.Life&&SkillHealth.Maximum256(body)==entry.Maximum&&body.Mana==entry.Mana
                &&body.ControlMaster==owner&&BattleCommand.PetOrder(body)==entry.Order&&DeathMarkWorld.State(body)==entry.Command,"Original demon/commands/resources did not survive reload");
            body.MoveToWorld(entry.Point,Map.Maps[entry.Command.Map]);body.AIObject?.AITimer.Stop();
            var d=new DeathMarkDirective{X=entry.Point.X*2,Y=(entry.Point.Y+1)*2,Remaining=1,TargetType=2,TargetId=checked((int)target.Serial.Value)};
            Require(DeathMarkWorld.ApplyPrepared(body,d,TimedEffects.Now)&&d.Remaining==0&&Text(body)==entry.State&&SkillHealth.Balance256(body)==entry.Life,"Original saved source path could not resume actual relocation");
        }
        Require(NecromancerEffects.ManaBalance256(owner)==saved.OwnerMana,"Reload/world cast changed owner resources");
        var next=saved.Entries.Select(e=>{var b=(SkeletonGuardian)World.FindMobile((Serial)e.Body);return e with{Point=b.Location,Command=DeathMarkWorld.State(b)};}).ToArray();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Entries=next,Generation=saved.Generation+1}));
    }
}
