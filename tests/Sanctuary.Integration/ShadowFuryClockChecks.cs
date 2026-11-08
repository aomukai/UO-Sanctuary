// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowFuryClockChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowFuryClockChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-fury-clock-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool v,string why){if(!v){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("shadow-fury-clock-check",false);Register("shadow-fury-clock-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original Fury mode14 clock,critical proposal and unchanged original saves",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Differential()
    {
        using var d=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-fury-action-cases.json")));int n=0;
        foreach(var r in d.RootElement.GetProperty("initial").EnumerateArray())
        {
            var flags=new int[17];flags[9]=1;var s=SourceAnimationEvents.Initial(new(0,r[0].GetInt32(),17*256,flags,[]),1000);
            Require(s.Epoch256==r[1].GetInt32()&&Json(s.Events.Select(e=>new[]{e.Type,e.Frame,e.Kind}).ToArray())==r[2].GetRawText(),"Own SC source initial plan differs");n++;
        }
        foreach(var r in d.RootElement.GetProperty("rewind").EnumerateArray())
        {
            var flags=new int[17];flags[9]=1;var s=SourceAnimationEvents.Renew(new((1000-r[1].GetInt32())*256,r[0].GetInt32(),17*256,flags,[new(1,1001,1),new(2,1017,0),new(3,1021,0)]),1000,1);
            Require(s.Epoch256==r[2].GetInt32()&&Json(s.Events.Select(e=>new[]{e.Type,e.Frame,e.Kind}).ToArray())==r[3].GetRawText(),"Own SC source ordinary rewind differs");n++;
        }
        int differences=0;foreach(var r in d.RootElement.GetProperty("critical").EnumerateArray())
        {
            int draws=0;uint Raw()=>r[3+draws++].GetUInt32();bool proposed=NpcMissileCritical.ProposedUnsigned(r[0].GetInt32(),r[1].GetInt32(),r[2].GetInt32(),Raw);
            Require(proposed==(r[8].GetInt32()!=0)&&draws==r[9].GetInt32(),"Proposed critical arithmetic/draw short circuit differs from explicitly modified complete routine");if(r[6].GetInt32()!=r[8].GetInt32()){differences++;}n++;
        }
        Require(n==46&&differences>0,"Missing full SC/critical controls or hidden signed ambiguity");
        using var clock=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-fury-clock-cases.json")));int controls=0;
        foreach(var r in clock.RootElement.EnumerateArray())
        {
            Require(r[3].GetInt32()==256&&r[5].GetInt32()==10&&r[6].GetInt32()==18,"Complete actual NPC Fury source clock changed");var seq=ShadowFuryClock.Initial();int frame=1,marker=0;for(;seq.Remaining>0;frame++){if(seq.Action==1){marker=frame;}seq=ShadowFuryClock.Advance(seq);}Require(marker==r[5].GetInt32()&&frame==r[6].GetInt32(),"Native own Fury sequence advance differs from source");controls++;
        }
        Require(controls==12,"Missing original sequence controls");

    }
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b));
    private static void Clock(AssassinShadowBody b,PlayerMobile p)
    {
        b.ChangeSkills(s=>s.Use(266));var rng=NpcEquipmentRandom.Require(b);var world=NpcWorldAllocation.Capture();int mana=NecromancerEffects.ManaBalance256(p),own=NecromancerEffects.ManaBalance256(b);string effects=Json(WeaponMissiles.Snapshot());
        try
        {
            Require(ShadowSkillBook.Metadata(266).Mode==7&&b.OwnedSkills.Find(266).Mode==14,"Original initializer override was lost or hidden by generic metadata");
            var plan=ShadowFuryClock.Plan(b);Require(plan.Speed==256&&plan.Events.Select(e=>e.Frame).SequenceEqual(new[]{10,18})&&plan.Events[0].Param==9*256,"Original mode14 Fury clock differs");
            var graph=OriginalNpcEquipment.Graph(b);var actor=graph.Actor.Stats;int Before(int stat)=>EquipmentStatValues.Raw(actor.BaseValues,EquipmentStatKey.Pack(stat));int ias=Before(93),cast=Before(105),attack=Before(68);
            try
            {
                graph.Values.SetBase(actor,EquipmentStatKey.Pack(93),ias+100);graph.Values.SetBase(actor,EquipmentStatKey.Pack(105),cast+100);graph.Values.SetBase(actor,EquipmentStatKey.Pack(68),attack+100);
                Require(Json(ShadowFuryClock.Plan(b))==Json(plan),"Original Fury acquired player/ordinary-SC or attack rate timing");
            }finally{graph.Values.SetBase(actor,EquipmentStatKey.Pack(93),ias);graph.Values.SetBase(actor,EquipmentStatKey.Pack(105),cast);graph.Values.SetBase(actor,EquipmentStatKey.Pack(68),attack);}
            Require(NpcEquipmentRandom.Require(b)==rng&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(p)==mana&&NecromancerEffects.ManaBalance256(b)==own&&Json(WeaponMissiles.Snapshot())==effects&&ShadowActions.State(b)==null,"Clock rolled/allocated/debited or admitted unfinished Fury");
        }finally{b.ChangeSkills(s=>s.Use(null));}
    }
    private static void Check(bool reload)
    {
        Differential();PlayerMobile p;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));p=(PlayerMobile)World.FindMobile((Serial)s.Owner);generation=s.Generation+1;bodies=s.Bodies.Select(x=>(AssassinShadowBody)World.FindMobile((Serial)x.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(s.Bodies[i])&&ShadowActions.State(bodies[i])==null,"Reload changed originals/resources/wounds/RNG or replayed transient Fury");}p.MoveToWorld(new(6440,3570,0),Map.Trammel);
        }
        else
        {
            p=BladeBodyChecks.Fresh(6440,30,3570);ClassSkillChecks.Train(p,"assassin.shadowwarrior",20);ClassSkillChecks.Train(p,"assassin.shadowmaster",20);var world=NpcWorldAllocation.Capture();bodies=new AssassinShadowBody[2];
            try{int i=0;foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}){NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));bodies[i]=AssassinShadowBody.CreateInitialized(p,ability,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(bodies[i]);ShadowEquipmentDefense.Activate(bodies[i]);i++;}}finally{NpcWorldAllocation.Restore(world);}
        }
        foreach(var b in bodies)
        {
            b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.ChangeSkills(s=>{s.Assign(266,12);s.Assign(257,3);s.Assign(277,3);});Clock(b,p);SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);PersistentMinions.Park(b);
        }
        GeneratedNpcLiveResources.Snapshot();var held=bodies.Select(Capture).ToArray();PersistentMinions.Suspend(p);p.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,held,generation)));
        Require(!AssassinCombat.Implemented(CombatAbility.ShadowWarrior)&&!AssassinCombat.Implemented(CombatAbility.ShadowMaster),"Prepared SC action enabled learned Shadows");
        Log.Information("Original Fury:mode14 slot2/17point sequence,speed256/marker10/end18,own rate isolation,no RNG/payment/emission;source comparison and proposed critical arithmetic pass;own originals retained. Full lease/rewind/emission/critical/seed/AI remain gated.");
    }
}
