// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class DragonFlightLandingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DragonFlightLandingChecks));
    private sealed record Saved(uint Owner,uint Target,Point3D Destination,int Mana,int Life,int TargetLife,MartialCharge Charge);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"dragon-flight-landing-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Flight free-cell/actual native transfer without payment policy",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("dragon-flight-landing-check",Check);Register("dragon-flight-landing-reload-check",Reload);
        var mana=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="DragonFlightLandingFixture"&&mana?.Invoke(p)!=false;
    }
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var life=ClassGearHooks.CanRegenerateHits;ClassGearHooks.CanRegenerateHits=p=>p.Name!="DragonFlightLandingFixture"&&life?.Invoke(p)!=false;
        foreach(var target in World.Mobiles.Values.OfType<BaseCreature>().Where(p=>p.Name=="DragonFlightLandingTarget"))
        {target.Frozen=true;target.CantWalk=true;target.AIObject?.AITimer.Stop();}
    }
    private static void Differential()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"free-coordinates-cases.json")));
        int count=0;
        foreach(var row in source.RootElement.EnumerateArray())
        {
            bool fallback=row[0].GetInt32()!=0;int rule=row[1].GetInt32(),dx=row[2].GetInt32(),dy=row[3].GetInt32(),calls=0;ulong trace=14695981039346656037;
            bool Clear(LeapCell cell)
            {
                calls++;trace=unchecked((trace^(uint)cell.X)*1099511628211);trace=unchecked((trace^(uint)cell.Y)*1099511628211);
                int x=cell.X-100,y=cell.Y-100;
                return rule==0||rule==2&&x==dx&&y==dy||rule==3&&((x+y)&1)!=0||rule==4&&(Math.Abs(x)==dx||Math.Abs(y)==dy);
            }
            var found=SourceFreeCoordinates.Find(new(100,100),Clear,fallback);
            Require(found.HasValue==(row[4].GetInt32()!=0)&&(!found.HasValue||found.Value==new LeapCell(row[5].GetInt32(),row[6].GetInt32()))
                &&calls==row[7].GetInt32()&&trace==ulong.Parse(row[8].GetString()),"Complete free-cell wrapper/search/tie/trace differential");
            if(fallback)
            {
                calls=0;trace=14695981039346656037;
                Require(LeapGeometry.Free(new(100,100),Clear)==found.Value&&calls==row[7].GetInt32()&&trace==ulong.Parse(row[8].GetString()),"Retained Leap fallback wrapper changed");
            }
            count++;
        }
        Require(count==1210,"Incomplete source free-coordinate fixture");
        Require(SourceFreeCoordinates.Find(new(10,10),_=>false)==null&&LeapGeometry.Free(new(10,10),_=>false)==new LeapCell(10,10),"Flight failure versus Leap fallback merged");
    }
    private static BaseCreature Target(PlayerMobile owner,int dx,int dy=0)
    {
        var t=new CobraPassiveTarget{Name="DragonFlightLandingTarget",Frozen=true,CantWalk=true,NoKillAwards=true};
        t.MoveToWorld(new(owner.X+dx,owner.Y+dy,owner.Z),owner.Map);t.AIObject?.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;return t;
    }
    private static void Check()
    {
        Differential();var owner=BladeBodyChecks.Fresh(6400,30,3600);owner.Name="DragonFlightLandingFixture";
        var target=Target(owner,19);Require(DragonFlightLanding.StartTarget(owner,target),"Inclusive38source-unit start range");
        target.MoveToWorld(new(owner.X+20,owner.Y,owner.Z),owner.Map);Require(!DragonFlightLanding.StartTarget(owner,target),"Out-of-range start accepted");
        target.MoveToWorld(new(owner.X+13,owner.Y+13,owner.Z),owner.Map);Require(DragonFlightLanding.StartTarget(owner,target),"Squared diagonal inside range");
        target.MoveToWorld(new(owner.X+14,owner.Y+14,owner.Z),owner.Map);Require(!DragonFlightLanding.StartTarget(owner,target),"Squared diagonal outside range");
        // Source do52 reads the target's current point without repeating start12.
        target.MoveToWorld(new(owner.X+22,owner.Y,owner.Z),owner.Map);
        NecromancerEffects.SetMana256(owner,owner.ManaMax*256-17);SkillHealth.SetBalance256(owner,SkillHealth.Balance256(owner)-128);
        var charge=new MartialCharge(owner.Serial.Value,CombatAbility.TigerStrike,3,1,150,TimedEffects.Now.AddHours(1));
        AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=owner.Serial.Value||x.Ability!=CombatAbility.TigerStrike).Append(charge).ToArray());
        int mana=NecromancerEffects.ManaBalance256(owner),life=SkillHealth.Balance256(owner),targetLife=SkillHealth.Balance256(target);var origin=owner.Location;
        Require(DragonFlightLanding.Destination(owner,target,out var chosen)&&chosen!=target.Location&&chosen!=origin
            &&!Teleportation.Landing(owner,chosen),"Prepared Flight reused Sorceress18tile range or occupied center");
        Require(owner.Location==origin&&NecromancerEffects.ManaBalance256(owner)==mana&&SkillHealth.Balance256(owner)==life,"Search moved/paid/healed");
        var wall=new Static(0x80);wall.MoveToWorld(chosen,owner.Map);
        Require(!Teleportation.Space(owner.Map,chosen,owner),"Late collision fixture did not block");
        var close=Target(owner,1);ClassCombat.Stop(owner);ClassCombat.For(owner).NextAction=ClassCombat.For(owner).RecoveryUntil=Core.TickCount;
        Require(ClassCombat.Perform(owner,CombatAbility.DragonTail,close,close.Location,Core.TickCount)&&ClassCombat.For(owner).TailAttack!=null,"Pending Tail cancellation control");
        Require(DragonFlightLanding.Transfer(owner,target,out var destination)&&owner.Location==destination&&destination!=chosen&&destination!=origin,"Actual transfer failed to re-search a late obstacle");
        Require(ClassCombat.For(owner).TailAttack==null&&ClassCombat.For(owner).PendingAbility!=CombatAbility.DragonTail,"Native location change retained pending Tail");
        Require(NecromancerEffects.ManaBalance256(owner)==mana&&SkillHealth.Balance256(owner)==life&&SkillHealth.Balance256(target)==targetLife
            &&AssassinCharges.State(owner)==charge,"Prepared transfer applied kick/payment/charge/healing policy");
        close.Delete();wall.Delete();
        var other=Target(owner,1);other.MoveToWorld(other.Location,Map.Felucca);
        Require(!DragonFlightLanding.Transfer(owner,other,out _)&&owner.Location==destination,"Cross-facet target moved owner");other.Delete();
        Require(!DragonFlightLanding.Transfer(owner,other,out _)&&owner.Location==destination,"Deleted target moved owner");
        Require(AssassinCombat.Implemented(CombatAbility.DragonFlight),"Approved Flight lost landing adapter");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,target.Serial.Value,destination,mana,life,targetLife,charge)));
        Log.Information("Dragon Flight landing:1210 source traces,retained Leap fallback,source start range,current-target native free-cell search,late-obstacle transfer,unpaid Tail cancellation;no learned Flight payment/contact policy");
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=(BaseCreature)World.FindMobile((Serial)saved.Target);
        Require(owner.Location==saved.Destination&&NecromancerEffects.ManaBalance256(owner)==saved.Mana&&SkillHealth.Balance256(owner)==saved.Life
            &&SkillHealth.Balance256(target)==saved.TargetLife&&AssassinCharges.State(owner)==saved.Charge,"Original moved owner/wounds/mana/charge/target changed");
        Require(ClassCombat.For(owner).TailAttack==null&&ClassCombat.For(owner).PendingAbility!=CombatAbility.DragonTail,"Cancelled Tail replayed after reload");
    }
}
