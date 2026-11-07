// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcOriginalProcChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcOriginalProcChecks));
    private sealed record Held(int Skill,uint Actor,uint Target,Point3D Point,int TargetLife,string Body,string[] Items,string Resources,NpcEquipmentSeed Random,int Life,long[] Ids);
    private sealed record Saved(uint Owner,Point3D Point,Held[] Held,SorceressSpellFlight[] Spells,ChainFlight[] Chains,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-original-procs-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Phase(int n)=>n switch{1=>"damagedinmelee",2=>"damagedbymissile",5=>"domeleedamage",6=>"domissiledamage",7=>"domeleeattack",8=>"domissileattack",_=>throw new InvalidOperationException("Unexpected proc phase")};
    private static string[] Items(AmazonValkyrieBody b)=>ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray();
    private static SorceressSpellFlight[] Spells(AmazonValkyrieBody b)=>SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Npc?.Actor==b.Serial.Value).ToArray();
    private static ChainFlight[] Chains(AmazonValkyrieBody b)=>ChainLightning.Snapshot().Where(f=>f.OriginalNpc?.Source.Actor==b.Serial.Value).ToArray();
    private static void Cancel(AmazonValkyrieBody b){SorceressSpellMissiles.Cancel(b);ChainLightning.Cancel(b);}
    private static long[][] Casts(JsonElement c)=>c.GetProperty("Results").EnumerateArray().SelectMany(r=>r[2].EnumerateArray()).Where(t=>t[0].GetInt32()==3).Select(t=>t.EnumerateArray().Select(n=>n.GetInt64()).ToArray()).ToArray();
    private static PreparedMonsterDamagePacket Packet(JsonElement c)=>c.GetProperty("Variant").GetInt32()==2?null:new(0,0,0,0,c.GetProperty("Variant").GetInt32()==0?4:0,HitFlags:c.GetProperty("Variant").GetInt32()==0?32u:0u);
    public static void Configure(){Register("npc-original-procs-check",false);Register("npc-original-procs-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Full original item proc dispatch and saved actual emitted spells",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Run(AmazonValkyrieBody b,BaseCreature target,JsonElement c,Func<NpcItemMissileRequest,NpcItemSpellAllocation> allocate,Action<NpcOriginalProcVisit> visit=null)
    {
        NpcEquipmentRandom.Set(b,ulong.Parse(c.GetProperty("Seed").GetString()));
        NpcOriginalItemProcs.Dispatch(b,target,Phase(c.GetProperty("Phase").GetInt32()),Packet(c),TimedEffects.Now.AddMinutes(10),allocate,observed:visit);
    }
    private static void Scalars(AmazonValkyrieBody b)
    {
        var target=NpcItemChainChecks.Target(b,12);var graph=b.EquipmentNumbers;var rows=graph.ItemEvents.Capture().Events.Entries;
        var fields=new[]{(81,"knock"),(86,"heal-kill"),(108,"rip"),(112,"howl"),(113,"stupidity"),(114,"dmg-to-mana"),(128,"light-thorns"),(134,"freeze"),(135,"openwounds"),(136,"crush"),(138,"mana-kill"),(139,"demon-heal"),(150,"slow")};
        var before=fields.Select(v=>NpcEquipmentCombat.Bonus(b,v.Item2)).ToArray();int callbacks=0;ulong rng=NpcEquipmentRandom.Require(b).Seed;
        try
        {
            foreach(byte phase in rows.Select(r=>r.Event).Distinct().Where(p=>rows.Where(r=>r.Event==p).All(r=>r.Function is not(20 or 21))))
            {
                string name=phase switch{1=>"damagedinmelee",2=>"damagedbymissile",3=>"attackedinmelee",5=>"domeleedamage",6=>"domissiledamage",9=>"kill",_=>throw new InvalidOperationException("Unexpected non-proc phase")};
                NpcOriginalItemProcs.Dispatch(b,target,name,new(0,0,0,0,5,HitFlags:32),TimedEffects.Now.AddMinutes(10),_=>throw new InvalidOperationException("Non-proc allocated spell"),n=>NpcEquipmentRandom.Limited(b,n),observed:_=>
                {callbacks++;foreach(var f in fields){Require(NpcEquipmentCombat.Bonus(b,f.Item2)==graph.Read(f.Item1),"Shared NPC effect read a missing/owner scalar instead of actual original graph");}});
            }
            Require(callbacks==rows.Count(r=>rows.Where(n=>n.Event==r.Event).All(n=>n.Function is not(20 or 21))),"Original non-proc callback traversal incomplete");
            Require(fields.Select(v=>NpcEquipmentCombat.Bonus(b,v.Item2)).SequenceEqual(before),"Original scalar scope leaked beyond dispatch");
        }
        finally{target.Delete();Cancel(b);NpcEquipmentRandom.Set(b,rng);}
    }
    private static void PathGuards(AmazonValkyrieBody b,JsonElement[] rows)
    {
        if(rows.Length==0){return;}int allocations=0;
        var closed=rows.First(c=>c.GetProperty("Variant").GetInt32()==1);ulong seed=ulong.Parse(closed.GetProperty("Seed").GetString());NpcEquipmentRandom.Set(b,seed);
        string history=Json(b.EquipmentNumbers.ItemEvents.Capture());
        NpcOriginalItemProcs.Dispatch(b,null,Phase(closed.GetProperty("Phase").GetInt32()),Packet(closed),TimedEffects.Now.AddMinutes(10),_=>{allocations++;throw new InvalidOperationException("Closed proc allocated");});
        Require(allocations==0&&NpcEquipmentRandom.Require(b).Seed==seed&&Json(b.EquipmentNumbers.ItemEvents.Capture())==history,"Closed targetless proc requested path/rolled/reordered");
        var open=rows.First(c=>Casts(c).Length>0);seed=ulong.Parse(open.GetProperty("Seed").GetString());NpcEquipmentRandom.Set(b,seed);bool refused=false;
        try{NpcOriginalItemProcs.Dispatch(b,null,Phase(open.GetProperty("Phase").GetInt32()),Packet(open),TimedEffects.Now.AddMinutes(10),_=>{allocations++;return new(true,1);});}catch(NotSupportedException e){Require(e.Message=="Missing original NPC path service","Unexpected targetless proc refusal");refused=true;}
        int success=Array.FindIndex(open.GetProperty("Results").EnumerateArray().ToArray(),r=>r[2].EnumerateArray().Any(t=>t[0].GetInt32()==3));
        Require(refused&&allocations==0&&NpcEquipmentRandom.Require(b).Seed==ulong.Parse(open.GetProperty("Results")[success][1].GetString())&&Json(b.EquipmentNumbers.ItemEvents.Capture())==history&&!NpcItemSpellCasts.KeepsCombatTarget(b),"Missing path did not retain source chance draw/restore event frame");
    }
    private static int Matrix(AmazonValkyrieBody b,JsonElement[] rows)
    {
        PathGuards(b,rows);int total=0;var ordinary=NpcItemChainChecks.Target(b,2);var target=NpcItemChainChecks.Target(b,12);b.Combatant=ordinary;
        try
        {
            foreach(var c in rows)
            {
                var casts=Casts(c);var expected=casts.SelectMany(t=>Enumerable.Repeat((Skill:(int)t[2],Rank:(int)t[3]),t[2]is 44 or 48?64:t[2]==38?(int)t[3]+2:1)).ToArray();
                var nodes=c.GetProperty("Nodes").EnumerateArray().ToArray();var results=c.GetProperty("Results").EnumerateArray().ToArray();int requests=0,visits=0,life=SkillHealth.Balance256(b),mana=NecromancerEffects.ManaBalance256(b.ControlMaster),targetLife=SkillHealth.Balance256(target);
                string items=Json(Items(b)),resources=Json(GeneratedNpcLiveResources.State(b)),history=Json(b.EquipmentNumbers.ItemEvents.Capture());ulong game=SourceRandom.InitLow(9000+total),start=game;bool allocated=c.GetProperty("Allocate").GetInt32()!=0;
                Run(b,target,c,r=>
                {
                    Require(requests<expected.Length&&(r.Skill,r.Rank)==expected[requests],"Original callback emitted wrong explicit skill/rank/order");
                    Require(NpcItemSpellCasts.KeepsCombatTarget(b),"Original proc lost protected wrapper");
                    b.Combatant=target;Require(b.Combatant==ordinary,"Original proc reassigned ordinary target");
                    requests++;return new(allocated,SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref game))));
                },v=>
                {
                    var node=nodes[visits];var result=results[visits];ulong before=visits==0?ulong.Parse(c.GetProperty("Seed").GetString()):ulong.Parse(results[visits-1][1].GetString());
                    Require(v.Function==node[4].GetInt32()&&v.Phase==node[1].GetInt32()&&v.Packed==node[2].GetUInt32()&&v.Result==result[0].GetInt32()&&v.Before==before&&v.After==ulong.Parse(result[1].GetString()),"Actual original graph callback order/result/actor stream differs");visits++;
                });
                Require(requests==expected.Length&&visits==nodes.Length&&NpcEquipmentRandom.Require(b).Seed==ulong.Parse(results[^1][1].GetString()),"Original proc sequence incomplete");
                ulong seed=start;for(int n=0;n<requests;n++){SourceRandom.Roll(ref seed);}Require(game==seed,"Allocation stream mixed with actor chance stream");
                Require(Spells(b).Length+Chains(b).Length==(allocated?requests:0),"Original callback published wrong actual projectile population");
                Require(b.Combatant==ordinary&&!NpcItemSpellCasts.KeepsCombatTarget(b)&&SkillHealth.Balance256(b)==life&&NecromancerEffects.ManaBalance256(b.ControlMaster)==mana&&SkillHealth.Balance256(target)==targetLife&&Json(Items(b))==items&&Json(GeneratedNpcLiveResources.State(b))==resources&&Json(b.EquipmentNumbers.ItemEvents.Capture())==history,"Original callback changed owner/payment/source/target/graph before projectile impact");
                Cancel(b);total++;
            }
        }
        finally{Cancel(b);b.Combatant=null;ordinary.Delete();target.Delete();}
        return total;
    }
    private static Held Hold(AmazonValkyrieBody b,JsonElement c)
    {
        int skill=(int)Casts(c).Single()[2];var target=NpcItemChainChecks.Target(b,12);ulong game=SourceRandom.InitLow(9900+skill);
        Run(b,target,c,_=>new(true,SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref game)))));target.Internalize();long[] ids;Point3D point;
        if(skill==53)
        {
            var f=Chains(b).Single();NpcItemChainChecks.Step(b,f.Cast,[],_=>throw new InvalidOperationException("Held proc root allocated child"));f=Chains(b).Single();ids=[f.Cast];point=new((int)Math.Round(f.X+f.Vx),(int)Math.Round(f.Y+f.Vy),b.Z);
        }
        else
        {
            foreach(var f in Spells(b)){SorceressSpellMissiles.Tick(f.NextUtc,true,f.Id);}var shots=Spells(b);var first=shots[0];ids=shots.Select(f=>f.Id).ToArray();
            point=first.Charged is{} bolt?new(bolt.OriginX+bolt.Path[3].X,bolt.OriginY+bolt.Path[3].Y,b.Z):new((int)Math.Round(first.X+first.Vx*4),(int)Math.Round(first.Y+first.Vy*4),b.Z);
        }
        return new(skill,b.Serial.Value,target.Serial.Value,point,SkillHealth.Balance256(target),b.SavedText,Items(b),Json(GeneratedNpcLiveResources.State(b)),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),ids);
    }
    private static void Resume(AmazonValkyrieBody b,Held h)
    {
        var target=(NpcItemChainChecks.Recipient)World.FindMobile((Serial)h.Target);var rng=NpcEquipmentRandom.Require(b);ulong critical=rng.Seed;int expected=h.TargetLife,hits=0;
        Require(SkillHealth.Balance256(target)==h.TargetLife,"Proc target wounds changed on reload");
        if(h.Skill==53)
        {
            var contacts=new List<NpcItemChainChecks.Contact>();var f=Chains(b).Single();NpcItemChainChecks.Step(b,f.Cast,contacts,_=>throw new InvalidOperationException("Loaded proc replayed child"));var aged=Chains(b).Single();
            Require(aged.X==f.X&&aged.Y==f.Y&&aged.Frame==f.Frame+1&&contacts.Count==0,"Loaded proc chain replayed interval");target.MoveToWorld(h.Point,b.Map);
            NpcItemChainChecks.Through(b,f.Cast,contacts,_=>throw new InvalidOperationException("Lone proc chain found another target"));Require(contacts.Count==1,"Saved proc chain did not hit");NpcItemChainChecks.Verify(b,rng,contacts,[target],[h.TargetLife]);
        }
        else
        {
            var old=Spells(b);foreach(var f in old){SorceressSpellMissiles.Tick(f.NextUtc,true,f.Id);}var aged=Spells(b);
            Require(aged.Length==old.Length&&aged.Zip(old).All(p=>p.First.X==p.Second.X&&p.First.Y==p.Second.Y&&p.First.Frame==p.Second.Frame+1)&&NpcEquipmentRandom.Require(b)==rng,"Loaded original proc replayed interval");target.MoveToWorld(h.Point,b.Map);
            for(int i=0;i<100&&Spells(b).Length>0;i++)foreach(var f in Spells(b))
            {
                int life=SkillHealth.Balance256(target);SorceressSpellMissiles.Tick(f.NextUtc,true,f.Id);
                if(SkillHealth.Balance256(target)<life){int raw=SorceressSpellMissiles.Roll(f,out _);if(SourceRandom.Roll(ref critical)%100<5){raw*=2;}expected-=raw;hits++;Require(SkillHealth.Balance256(target)==expected,"Saved original proc natural HP differs");}
            }
            Require(hits>0&&NpcEquipmentRandom.Require(b)==rng with{Seed=critical},"Saved original proc RNG or natural hit missing");
        }
        Cancel(b);target.Delete();
    }
    private static void Check(bool reload)
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-original-procs-cases.json")));var cases=source.RootElement.EnumerateArray().ToArray();Require(cases.Length==1536,"Missing original proc corpus");
        PlayerMobile p;Point3D point;int generation=0;var bodies=new Dictionary<int,AmazonValkyrieBody>();
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;Require(generation<=2,"Extra original proc reload");p=World.FindMobile((Serial)s.Owner)as PlayerMobile;point=s.Point;Require(p is{Alive:true,Deleted:false},"Original proc owner lost");
            foreach(var h in s.Held)
            {
                var b=World.FindMobile((Serial)h.Actor)as AmazonValkyrieBody;Require(b is{Alive:true,Deleted:false}&&b.ControlMaster==p&&b.SavedText==h.Body&&Json(Items(b))==Json(h.Items)&&Json(GeneratedNpcLiveResources.State(b))==h.Resources&&NpcEquipmentRandom.Require(b)==h.Random&&SkillHealth.Balance256(b)==h.Life,"Original proc source/history/items/resources/RNG changed on reload");
            }
            Require(Json(SorceressSpellMissiles.Snapshot().Flights.Where(f=>s.Held.Any(h=>h.Actor==f.Npc?.Actor)).ToArray())==Json(s.Spells)&&Json(ChainLightning.Snapshot().Where(f=>s.Held.Any(h=>h.Actor==f.OriginalNpc?.Source.Actor)).ToArray())==Json(s.Chains),"Actual proc-created projectile persistence differs");
            p.MoveToWorld(point,Map.Trammel);foreach(var h in s.Held){var b=(AmazonValkyrieBody)World.FindMobile((Serial)h.Actor);NpcItemChainChecks.Place(b,p);Resume(b,h);PersistentMinions.Park(b);bodies.Add(Case(cases,h.Skill),b);}
        }
        else
        {
            p=new PlayerMobile{Name="NpcOriginalProcFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);FixtureSpace.Corridor(p,6500,32,3100);point=p.Location;
        }
        using var raw=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-damage-cases.json")));var originals=raw.RootElement.EnumerateArray().Where(c=>c.GetProperty("Critical").GetInt32()==1).ToArray();int count=0;
        for(int index=0;index<originals.Length;index++)
        {
            if(!bodies.TryGetValue(index,out var b)){b=ValkyrieDamageChecks.Body(p,originals[index]);ValkyrieEquipmentResources.Activate(b,0,0,0,0,300);GeneratedNpcLiveResources.ActivateItemEvents(b);bodies.Add(index,b);}
            NpcItemChainChecks.Place(b,p);_=NpcOriginalItemProcs.Require(b);Scalars(b);count+=Matrix(b,cases.Where(c=>c.GetProperty("Case").GetInt32()==index).ToArray());PersistentMinions.Park(b);
        }
        Require(count==cases.Length,"Incomplete original graph source contexts");var held=new List<Held>();
        foreach(int skill in new[]{36,38,44,48,53})
        {
            int index=Case(cases,skill);var b=bodies[index];NpcItemChainChecks.Place(b,p);held.Add(Hold(b,CaseRow(cases,skill)));PersistentMinions.Park(b);
        }
        var ids=held.Select(h=>h.Actor).ToHashSet();var spells=SorceressSpellMissiles.Snapshot().Flights.Where(f=>ids.Contains(f.Npc?.Actor??0)).ToArray();var chains=ChainLightning.Snapshot().Where(f=>ids.Contains(f.OriginalNpc?.Source.Actor??0)).ToArray();
        PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,point,held.ToArray(),spells,chains,generation)));
        Log.Information("Original NPC procs:{Count}source callback contexts across57unfiltered loadouts;actual full graph order/chance/rank/flags and actor/allocation streams;five proc-created saved spell bodies resume exact natural HP/RNG through two reloads;ordinary producers/game stream remain separate",count);
    }
    private static JsonElement CaseRow(JsonElement[] cases,int skill)=>cases.First(c=>c.GetProperty("Case").GetInt32()==(skill switch{36=>8,38=>10,44=>46,48=>1,53=>26,_=>throw new InvalidOperationException()})&&c.GetProperty("Variant").GetInt32()==0&&c.GetProperty("Allocate").GetInt32()==1&&Casts(c)is{Length:1} casts&&casts[0][2]==skill);
    private static int Case(JsonElement[] cases,int skill)=>CaseRow(cases,skill).GetProperty("Case").GetInt32();
}
