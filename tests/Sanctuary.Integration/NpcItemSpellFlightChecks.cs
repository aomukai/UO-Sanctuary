// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcItemSpellFlightChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcItemSpellFlightChecks));
    private sealed record Saved(uint Owner,uint Actor,Point3D Point,string Body,string[] Items,NpcEquipmentSeed Random,int Life,string Resources,
        uint Target,int TargetLife,SorceressSpellFlight[] Flights,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-item-spell-flights-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-item-spell-flights-check",false);Register("npc-item-spell-flights-reload-check",true);}
    private static void Register(string command,bool reload)=>ConsoleInputHandler.RegisterCommand(command,"Original NPC spell emission,HP,cold,ownership and actual flight saves",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",command);}catch(Exception e){Log.Error(e,"{Name}: failed",command);}}));
    private static SorceressSpellFlight[] Flights(AmazonValkyrieBody b)=>SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Npc?.Actor==b.Serial.Value).ToArray();
    private static string[] Items(AmazonValkyrieBody b)=>ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray();
    private static void Place(AmazonValkyrieBody b,PlayerMobile p)
    {
        b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.AIObject?.AITimer.Stop();
        var row=PersistentMinions.Profiles.Track(b);PersistentMinions.Profiles.Store(b,row with{Parked=false});
        Require(NpcItemSpellFlights.Attached(b,p,p.Map.MapID),"Prepared original NPC source not active");
    }
    private static ValkyrieContactChecks.Recipient Target(AmazonValkyrieBody b)
    {
        var t=new ValkyrieContactChecks.Recipient();t.SetHits(65000);t.MoveToWorld(new(b.X+4,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();
        foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static SorceressSpellFlight[] Prepare(AmazonValkyrieBody b,Point3D aim,int skill,DateTime at)
    {
        string text=b.SavedText,items=Json(Items(b));var random=NpcEquipmentRandom.State(b);int count=0;
        var f=SorceressSpellMissiles.PrepareValkyrie(b,aim,skill,3,at,i=>{Require(i==count++,"Child seed factory skipped/repeated ordinal");return SourceRandom.InitLow(900+i);});
        Require(count==f.Length&&count==(skill is 44 or 48?64:skill==38?5:1),"Original emission count");
        Require(text==b.SavedText&&items==Json(Items(b))&&random==NpcEquipmentRandom.State(b),"Factory changed original source/items/actor seed");
        Require(f.All(x=>x.X==b.X&&x.Y==b.Y&&x.Owner==b.ControlMaster.Serial.Value&&x.Npc.Actor==b.Serial.Value&&x.Modifiers==new FirestormModifiers(0,0,0,0,0)),"NPC origin/credit/own packet context lost");
        return f;
    }
    private static ulong CriticalSeed(bool critical)
    {
        for(int i=1;i<1000;i++){ulong seed=SourceRandom.InitLow(i),copy=seed;if((SourceRandom.Roll(ref copy)%100<5)==critical){return seed;}}
        throw new InvalidOperationException("No deterministic source critical seed");
    }
    private static void Hits(AmazonValkyrieBody b,PlayerMobile p)
    {
        foreach(int skill in new[]{36,38,44,48})foreach(bool critical in new[]{false,true})foreach(bool immune in new[]{false,true})
        {
            var t=Target(b);
            try
            {
                var f=Prepare(b,t.Location,skill,TimedEffects.Now)[0];var packet=f.Npc.Packet;
                if(immune){t.SetResistance(skill==36?ResistanceType.Fire:skill==44?ResistanceType.Cold:ResistanceType.Energy,100);}
                NpcEquipmentRandom.Set(b,CriticalSeed(critical));var start=NpcEquipmentRandom.State(b);ulong expectedSeed=start.Seed;SourceRandom.Roll(ref expectedSeed);
                int raw=SorceressSpellMissiles.Roll(f,out ulong child);if(critical){raw=checked(raw*2);}int life=SkillHealth.Balance256(t),packets=NativeMonsterReturn.Session(t).Packets;
                int loss=SorceressSpellMissiles.Impact(p,t,f,out ulong next);
                Require(next==child&&NpcEquipmentRandom.State(b)==start with{Seed=expectedSeed},"Projectile/source intrinsic critical streams merged or rerolled");
                Require(loss==(immune?0:raw)&&SkillHealth.Balance256(t)==life-loss&&NativeMonsterReturn.Session(t).Packets==packets+1,"Original elemental HP/critical/immunity/free differs");
                Require(immune?t.Notifications==0:t.Notifications==1&&t.AtNotification==life,"NPC spell HP notification was not before commitment");
                if(skill==44&&!immune){Require(TimedEffects.TryActor(t,out var a)&&a.Cold?.Source==b.Serial.Value&&a.ChillUntil>TimedEffects.Now,"NPC cold status credited owner instead of actual emitter");}
                if(skill==44&&immune){Require(!TimedEffects.TryActor(t,out var a)||a.Cold==null,"Immune recipient gained cold state");}
            }
            finally{t.Delete();}
        }
        // The existing public fire receiver uses the same new common helper.
        var fire=Target(b);try{int hp=SkillHealth.Balance256(fire);NpcFireSpellHit.Deliver(b,p,fire,513,TimedEffects.Now,"Existing fire receiver regression");Require(SkillHealth.Balance256(fire)==hp-513&&fire.Notifications==1&&fire.AtNotification==hp,"Existing NPC fire helper changed HP/event order");}finally{fire.Delete();}
    }
    private static void Batches(AmazonValkyrieBody b,PlayerMobile p)
    {
        foreach(int skill in new[]{36,38,44,48})
        {
            var t=Target(b);
            try
            {
                int hp=SkillHealth.Balance256(t);var f=Prepare(b,t.Location,skill,TimedEffects.Now.AddHours(1));string pool=Json(Flights(b));bool refused=false;
                if(f.Length>1)
                {try{SorceressSpellMissiles.CommitValkyrie(f[..^1]);}catch(NotSupportedException){refused=true;}Require(refused&&Json(Flights(b))==pool,"Incomplete batch partly committed");}
                var bad=f.ToArray();bad[^1]=bad[^1]with{Npc=bad[^1].Npc with{Packet=bad[^1].Npc.Packet with{Minimum256=-1}}};refused=false;
                try{SorceressSpellMissiles.CommitValkyrie(bad);}catch(InvalidDataException){refused=true;}Require(refused&&Json(Flights(b))==pool,"Invalid final child partly committed earlier rays");
                SorceressSpellMissiles.CommitValkyrie(f);Require(Flights(b).Length==f.Length&&SkillHealth.Balance256(t)==hp,"Commit hit early or lost children");
                var original=f[0];if(original.Charged is{} path){path.Path[1]=new(30000,30000);Require(Flights(b)[0].Charged.Path[1]!=path.Path[1],"Committed charged path aliases caller");}
                SorceressSpellMissiles.Cancel(b);Require(Flights(b).Length==0,"NPC actor cancellation did not release its children");
            }
            finally{t.Delete();}
        }
        var target=Target(b);
        try
        {
            var f=Prepare(b,target.Location,36,TimedEffects.Now);SorceressSpellMissiles.CommitValkyrie(f);int hp=SkillHealth.Balance256(target);var seed=NpcEquipmentRandom.State(b);
            SorceressSpellMissiles.Tick(f[0].NextUtc,false,f[0].Id);var aged=Flights(b).Single();Require(aged.Frame==1&&aged.X==f[0].X&&SkillHealth.Balance256(target)==hp&&NpcEquipmentRandom.State(b)==seed,"Offline flight moved,fought or consumed RNG");
            PersistentMinions.Park(b);SorceressSpellMissiles.Tick(aged.NextUtc,true,aged.Id);aged=Flights(b).Single();Require(aged.Frame==2&&aged.X==f[0].X&&SkillHealth.Balance256(target)==hp,"Parked source fought");Place(b,p);
            for(int i=0;i<55&&Flights(b).Length>0;i++){var current=Flights(b).Single();SorceressSpellMissiles.Tick(current.NextUtc,true,current.Id);}
            Require(Flights(b).Length==0&&SkillHealth.Balance256(target)<hp&&target.Notifications==1,"Natural shared Fire Bolt flight did not hit exactly once");
        }
        finally{SorceressSpellMissiles.Cancel(b);target.Delete();}
        var before=NpcEquipmentRandom.State(b);bool rejected=false;
        try{SorceressSpellMissiles.PrepareValkyrie(b,b.Location,53,3,TimedEffects.Now,_=>throw new InvalidOperationException("Should not allocate a child"));}catch(NotSupportedException){rejected=true;}
        Require(rejected&&NpcEquipmentRandom.State(b)==before&&Flights(b).Length==0,"Unfinished Chain Lightning silently launched or consumed actor RNG");
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;AmazonValkyrieBody b;Point3D point;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;Require(generation<=2,"Extra flight reload");
            p=World.FindMobile((Serial)s.Owner)as PlayerMobile;b=World.FindMobile((Serial)s.Actor)as AmazonValkyrieBody;var t=World.FindMobile((Serial)s.Target)as ValkyrieContactChecks.Recipient;
            Require(p is{Alive:true,Deleted:false}&&b is{Alive:true,Deleted:false}&&t is{Alive:true,Deleted:false},"Original saved projectile identities lost");point=s.Point;
            Require(b.SavedText==s.Body&&Json(Items(b))==Json(s.Items)&&NpcEquipmentRandom.State(b)==s.Random&&SkillHealth.Balance256(b)==s.Life
                &&Json(GeneratedNpcLiveResources.State(b))==s.Resources&&SkillHealth.Balance256(t)==s.TargetLife&&Json(Flights(b))==Json(s.Flights),"Original flights/items/source seed/resources/wounds changed across actual reload");
            p.MoveToWorld(point,Map.Trammel);Place(b,p);var f=Flights(b).Single(x=>x.Ability==CombatAbility.FireBolt);int life=SkillHealth.Balance256(t);var actorSeed=NpcEquipmentRandom.State(b);
            SorceressSpellMissiles.Tick(f.NextUtc,true,f.Id);var aged=Flights(b).Single(x=>x.Id==f.Id);Require(aged.Frame==1&&aged.X==f.X&&NpcEquipmentRandom.State(b)==actorSeed&&SkillHealth.Balance256(t)==life,"Reload replayed elapsed travel/damage");
            for(int i=0;i<55&&Flights(b).Any(x=>x.Id==f.Id);i++){var current=Flights(b).Single(x=>x.Id==f.Id);SorceressSpellMissiles.Tick(current.NextUtc,true,current.Id);}
            Require(!Flights(b).Any(x=>x.Id==f.Id)&&SkillHealth.Balance256(t)<life,"Original saved Fire Bolt did not resume with real HP");SorceressSpellMissiles.Cancel(b);t.Delete();
        }
        else
        {
            p=new PlayerMobile{Name="NpcItemSpellFlightFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);FixtureSpace.Corridor(p,6540,20,3680);point=p.Location;
            using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-damage-cases.json")));var c=source.RootElement.EnumerateArray().First(x=>x.GetProperty("Critical").GetInt32()==1&&x.GetProperty("Rank").GetInt32()==20);
            b=ValkyrieDamageChecks.Body(p,c);ValkyrieEquipmentResources.Activate(b,0,0,0,0,300);Place(b,p);
        }
        Hits(b,p);Batches(b,p);
        var target=Target(b);var until=TimedEffects.Now.AddMinutes(10);
        foreach(int skill in new[]{36,38,44,48}){SorceressSpellMissiles.CommitValkyrie(Prepare(b,target.Location,skill,until));}
        var flights=Flights(b);Require(flights.Length==134,"Four saved original emissions lost children");
        int health=SkillHealth.Balance256(b);string resources=Json(GeneratedNpcLiveResources.State(b));var random=NpcEquipmentRandom.State(b);
        PersistentMinions.Suspend(p);p.Internalize();
        Require(health==SkillHealth.Balance256(b)&&resources==Json(GeneratedNpcLiveResources.State(b))&&random==NpcEquipmentRandom.State(b),"Parking reset source wounds/resources/RNG");
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,b.Serial.Value,point,b.SavedText,Items(b),random,health,resources,target.Serial.Value,SkillHealth.Balance256(target),flights,generation)));
        Log.Information("Original NPC spell flights:Fire Bolt/Charged Bolt/Nova/Frost Nova share checked motion,explicit child seeds and own packets;16HP/critical/immunity controls,emitter cold ownership,atomic batch/path-copy/offline/park/natural hit;134actual saved flights and original items/resources/wounds/RNG through two reloads;Chain/proc callbacks/shield targets/AI/learned cast remain gated");
    }
}
