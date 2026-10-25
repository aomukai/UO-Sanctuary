// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class InfernoSentryChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(InfernoSentryChecks));
    private sealed record Saved(uint Owner,uint Actor,uint First,uint Second,SentryBodyState Body,SentryLightningState Beam,int Health1,int Health2,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"inferno-sentry-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("inferno-sentry-check",Check);Register("inferno-sentry-reload-check",Reload);Register("inferno-sentry-live-check",Live);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared real NPC311 channel/fire beam; learned placement/retirement gated",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Train(PlayerMobile p,CombatAbility ability,int rank)
    {string id=SkillMechanics.For(ability).TalentId;while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}}
    private static AssassinSentryBody Actor(PlayerMobile p,DateTime at)=>AssassinSentryBody.CreateEmerging(p,new(p.X+2,p.Y,p.Z),at,SourceRandom.InitLow(19),CombatAbility.WakeOfInferno);
    private static BladeMissileTarget Target(AssassinSentryBody b,int dx=2)
    {
        var t=new BladeMissileTarget{Name="InfernoSentryTarget",NoKillAwards=true};t.MoveToWorld(new(b.X+dx,b.Y,b.Z),b.Map);t.AIObject?.AITimer.Stop();
        t.SetHits(100000);t.Hits=t.HitsMax;foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}return t;
    }
    private static void Frames(long id,int count,bool actions)
    {for(int i=0;i<count&&AssassinSentryLightning.State(id) is{} s;i++){AssassinSentryLightning.TickFrame(s.NextUtc,actions);}}
    private static void Drain(AssassinSentryBody b)
    {foreach(var s in AssassinSentryLightning.Snapshot().Bolts.Where(s=>s.Actor==b.Serial.Value)){Frames(s.Id,s.Frames,false);}}
    private static void BurstGoldens()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"inferno-sentry-controller-source-probe.json")));int cases=0,frames=0;
        foreach(var c in doc.RootElement.EnumerateArray())
        {
            var input=c.GetProperty("input");if(input[4].GetInt32()!=0){continue;}
            int hard=input[1].GetInt32(),failure=input[2].GetInt32(),kind=input[3].GetInt32();
            var state=AssassinInfernoBurst.Start(25,hard);int ended=-1;var emitted=new System.Collections.Generic.List<int>();
            foreach(var row in c.GetProperty("trace").EnumerateArray())
            {
                int frame=row[0].GetInt32();bool failed=failure>=0&&frame>=25+failure;
                state=AssassinInfernoBurst.Advance(state,!(failed&&kind==2),at=>
                {
                    var result=failed?kind switch{0=>InfernoEmission.NoCoordinates,1=>InfernoEmission.AllocationFailed,_=>InfernoEmission.Success}:InfernoEmission.Success;
                    if(result==InfernoEmission.Success){emitted.Add(at);}return result;
                });
                if(state.Ended&&ended<0){ended=frame;}
                int[] actual=[state.Frame,state.Sequence.Point,state.Sequence.Remaining,state.Sequence.Action,0,state.Ended?1:14,state.ModeCalls,ended,state.Emissions,state.Creates,state.ModeAt==0?-2:state.ModeAt,state.EndAt==0?-2:state.EndAt];
                Require(actual.SequenceEqual(row.EnumerateArray().Select(x=>x.GetInt32())),"Complete source burst phase/sequence/event differential");
                state=JsonSerializer.Deserialize<InfernoBurstState>(JsonSerializer.Serialize(state));AssassinInfernoBurst.Validate(state);frames++;
            }
            Require(emitted.SequenceEqual(c.GetProperty("emissions").EnumerateArray().Select(x=>x.GetInt32())),"Source emission chronology");cases++;
        }
        Require(cases==120&&frames==14400,"Incomplete Inferno burst source inventory");
        foreach(int hard in new[]{0,20})
        {
            var state=AssassinInfernoControl.Start();int shots=0;var emitted=new System.Collections.Generic.List<int>();
            for(int i=0;i<800;i++)
            {
                state=AssassinInfernoControl.Advance(state,true,true,false,true,true,hard,()=>10,()=>new(7,12),()=>true,()=>{},(_,frame)=>{emitted.Add(frame);return InfernoEmission.Success;});
                if(state.Mode==0){break;}
            }
            int entry=25;var expected=new System.Collections.Generic.List<int>();
            for(int n=0;n<10;n++)
            {
                int emit=entry+10,deadline=entry+15+hard;
                while(true){expected.Add(emit);if(emit>=deadline){break;}emit+=3;}
                entry=emit+14+15;shots++;
            }
            Require(state.Mode==0&&state.Attempts==shots&&state.Ai.RemainingShots==0&&state.Frame==entry&&emitted.SequenceEqual(expected),"AI101/channel/neutral fixed-ten-burst composition");
            var inactive=AssassinInfernoControl.Start();for(int i=0;i<40;i++){inactive=AssassinInfernoControl.Advance(inactive,true,true,false,true,true,hard,()=>10,()=>new(7,13),()=>throw new Exception("Out-of-range RNG draw"),()=>throw new Exception("Out-of-range start"),(_,_)=>throw new Exception("Out-of-range emission"));}
            Require(inactive.Attempts==0&&inactive.Ai.RemainingShots==10,"Strict source13distance gate");
        }
        Log.Information("Inferno burst:120 complete source contexts/14400 phases and JSON resumptions; shared AI fixed10burst/strict13range checks pass");
    }
    private static void Creation()
    {
        BurstGoldens();using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"wake-of-inferno-damage-source-probe.json")));
        var fields=doc.RootElement.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();int min=Array.IndexOf(fields,"nFireMinDamage"),max=Array.IndexOf(fields,"nFireMaxDamage"),count=0;
        var row=SkillMechanics.For(CombatAbility.WakeOfInferno);
        foreach(var r in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();if(V(5)!=0){continue;}
            int Rank(string key)=>key==row.SourceKey?V(0):key=="Fire Trauma"?V(1):key=="Wake of Fire Sentry"?V(2):0;
            var stats=AssassinSentryCreation.Prepare(new(row,Rank,Rank,99),99,MonsterDifficulty.Normal,name=>name=="passive_fire_mastery"?V(3):0);
            Require(stats.Ability==CombatAbility.WakeOfInferno&&stats.Skill("mon inferno sentry")==V(0)&&stats.Skill("Fire Trauma")==V(1)&&stats.Skill("Wake of Fire Sentry")==V(2)
                &&AssassinSentryCreation.Shots(stats)==10&&AssassinSentryCreation.Fire(stats)==(r[7][min].GetInt32(),r[7][max].GetInt32()),"Captured current Inferno source packet "+count);count++;
        }
        Require(count==1983,"Inferno damage source inventory");
        foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
        {
            var stats=AssassinSentryCreation.Prepare(new(row,_=>20,_=>20,99),99,difficulty,key=>key=="passive_fire_mastery"?55:key=="passive_fire_pierce"?-11:0);
            var next=AssassinSentryCreation.Rescale(stats,MonsterDifficulty.Hell);Require(stats.Skills==next.Skills&&stats.Passives==next.Passives&&AssassinSentryCreation.Fire(stats)==AssassinSentryCreation.Fire(next),"Difficulty changed captured fire packet");
        }
        Log.Information("Inferno creation:1983 applicable complete source packet comparisons/all255ranks; hard synergies/mastery/fixed10burst capture pass");
    }
    private static void Damage(PlayerMobile p,AssassinSentryBody b,BladeMissileTarget t)
    {
        int mana=NecromancerEffects.ManaBalance256(p),health=SkillHealth.Balance256(p);
        foreach(int pierce in new[]{20,-20})
        {
            t.SetResistance(ResistanceType.Energy,100);t.SetResistance(ResistanceType.Fire,0);t.Hits=t.HitsMax;SkillHealth.Clear(t);int before=SkillHealth.Balance256(t);
            AssassinSentryBolts.Impact(b,t,"inferno sentry 1",2560,2560,pierce,SourceRandom.InitLow(19),"fire",82);
            Require(before-SkillHealth.Balance256(t)==2560*(100+pierce)/100,"Fire packet inherited lightning resistance or lost signed pierce");
        }
        t.SetResistance(ResistanceType.Fire,100);int hp=SkillHealth.Balance256(t);Require(AssassinSentryBolts.Impact(b,t,"inferno sentry 1",2560,2560,90,1,"fire",82).WholeLoss==0&&SkillHealth.Balance256(t)==hp,"Ordinary fire pierce broke immunity");
        t.SetResistance(ResistanceType.Fire,0);t.SetResistance(ResistanceType.Energy,0);
        bool refused=false;try{AssassinSentryBolts.Impact(b,t,"inferno sentry 1",2560,2560,0,1);}catch(NotSupportedException){refused=true;}Require(refused,"Fire missile accepted default lightning/reduction route");
        var kill=Target(b,7);kill.SetHits(1);kill.Hits=1;AssassinSentryBolts.Impact(b,kill,"inferno sentry 1",2560,2560,0,1,"fire",82);Require(!kill.Alive&&kill.LastKiller==b&&ClassLoot.Killer(kill)==p,"Actual fire NPC/original player kill credit");kill.Delete();
        Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(p)==health,"Source-zero fire inherited player payment/leech");
    }
    private static void Save(PlayerMobile p,AssassinSentryBody b,BladeMissileTarget first,BladeMissileTarget second,long id)
    {first.Blessed=second.Blessed=true;File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,first.Serial.Value,second.Serial.Value,b.Prepared,AssassinSentryLightning.State(id),SkillHealth.Balance256(first),SkillHealth.Balance256(second),NecromancerEffects.ManaBalance256(p))));}
    private static void Check()
    {
        Creation();var p=BladeBodyChecks.Fresh(6740,40);p.Name="InfernoSentryFixture";Train(p,CombatAbility.FireBlast,20);Train(p,CombatAbility.WakeOfFire,15);
        while(p.RawStr<80){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength for actual fire gear");}
        var staff=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Mang Song's Lesson").Id,99,new Random(173),true);var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);
        Require(p.EquipItem(staff)&&p.EquipItem(helm),"Actual fire pierce/soft-rank gear");ClassGear.Refresh(p);
        var b=Actor(p,DateTime.UtcNow.AddDays(3));var first=Target(b);var second=Target(b);int mana=NecromancerEffects.ManaBalance256(p);
        Require(b.Prepared.Creation is{Ability:CombatAbility.WakeOfInferno,Rank:7,Level:81,Life256:25600}&&b.Prepared.InfernoControl is{Mode:8,EndAt:15,NextThink:25}
            &&b.Passive("passive_fire_pierce")>0&&!b.BlocksFit&&!b.CanBeDamaged()&&b.Name.Contains("[placeholder]"),"Actual Inferno actor/S1/rank/fire context");
        var ends=AssassinSentryCreation.Fire(b.Prepared.Creation);p.AddToBackpack(staff);p.AddToBackpack(helm);ClassGear.Refresh(p);Require(AssassinSentryCreation.Fire(b.Prepared.Creation)==ends&&b.Prepared.Creation.Skill("Wake of Fire Sentry")==15,"Birth fire stats followed later owner gear");
        Damage(p,b,first);first.Blessed=second.Blessed=true;Wall(b);first.Blessed=second.Blessed=false;
        Require(AssassinSentryLightning.CreateInferno(b,new(0,b.Y*2))==0&&AssassinSentryLightning.CreateInferno(b,new(b.X*2+100,b.Y*2))==0,"Inferno zero-coordinate/factory range guards");
        for(int i=0;i<35;i++){b.AdvanceController(b.Prepared.NextFrameUtc,true);}
        var beam=AssassinSentryLightning.Snapshot().Bolts.Single(x=>x.Actor==b.Serial.Value);
        Require(b.Prepared.InfernoControl is{Attempts:1,Emissions:1,Burst.ModeAt:38,Burst.Deadline:55,Ai.RemainingShots:9}&&beam.Ability==CombatAbility.WakeOfInferno&&beam.HardWake==15
            &&beam.Frames==35&&beam.VelocityFp8==2304&&beam.Minimum256==ends.Min&&beam.Maximum256==ends.Max&&beam.Vx>0&&beam.Vy==0,"Real first beam/custom sparse channel/captured fire duration");
        int hp1=SkillHealth.Balance256(first),hp2=SkillHealth.Balance256(second);
        for(int i=0;i<14&&AssassinSentryLightning.State(beam.Id) is{LastCollision:0};i++){Frames(beam.Id,1,true);}
        var hit=AssassinSentryLightning.State(beam.Id);Require(hit is{Fault:null,LastCollision:>0}&&((SkillHealth.Balance256(first)<hp1)^(SkillHealth.Balance256(second)<hp2)),"Size1 first overlap contact");
        Frames(beam.Id,1,true);var other=AssassinSentryLightning.State(beam.Id);Require(other!=null&&other.LastCollision!=hit.LastCollision&&other.Seed!=hit.Seed&&SkillHealth.Balance256(first)<hp1&&SkillHealth.Balance256(second)<hp2,"Fire LastCollide filtered after selection or failed penetration");
        Require(NecromancerEffects.ManaBalance256(p)==mana,"NPC beam paid player mana");Lifecycle(p);Save(p,b,first,second,beam.Id);Require(!AssassinCombat.Implemented(CombatAbility.WakeOfInferno),"Unfinished learned Inferno enabled");
    }
    private static void Wall(AssassinSentryBody b)
    {
        var target=Target(b,8);var wall=new Static(0x80);wall.MoveToWorld(new(b.X+2,b.Y,b.Z),b.Map);
        try{int hp=SkillHealth.Balance256(target);long id=AssassinSentryLightning.CreateInferno(b,new((b.X+15)*2,b.Y*2));Frames(id,20,true);Require(AssassinSentryLightning.State(id)==null&&SkillHealth.Balance256(target)==hp,"Inferno crossed native wall");}
        finally{target.Delete();wall.Delete();}
    }
    private static void Lifecycle(PlayerMobile owner)
    {
        var b=Actor(owner,DateTime.UtcNow.AddDays(4));var t=Target(b,5);var facets=SummonDifficulties.Facets();var map=owner.Map;var point=owner.Location;
        SkillHealth.SetBalance256(b,12850);var captured=b.Prepared.Creation;SummonDifficulties.SetFacets([new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);owner.MoveToWorld(point,Map.Felucca);SummonDifficulties.Sync(owner);
        Require(b.Map==map&&b.Prepared.Creation.Difficulty==MonsterDifficulty.Hell&&SkillHealth.Balance256(b)==12850&&b.Prepared.Creation.Skills==captured.Skills&&b.Prepared.Creation.Passives==captured.Passives,"Inferno old-facet scaling/fractional injury/capture");
        var selector=AssassinSentryActors.SelectTarget;AssassinSentryActors.SelectTarget=_=>new(t.Serial.Value,10);
        try
        {
            for(int i=0;i<800&&b.Prepared.InfernoControl.Mode!=0;i++){b.AdvanceController(b.Prepared.NextFrameUtc,true);}
            Require(!b.Deleted&&b.Prepared.InfernoControl is{Mode:0,Attempts:10,Ai.RemainingShots:0}&&b.Prepared.ControllerFault.Contains("retirement"),"Fixed ten channel bursts or retirement boundary");
            var beam=AssassinSentryLightning.Snapshot().Bolts.First(s=>s.Actor==b.Serial.Value);Frames(beam.Id,1,false);var offline=AssassinSentryLightning.State(beam.Id);Require(offline.Frame==1&&offline.X==beam.X&&offline.Seed==beam.Seed,"Offline fire moved/rolled");Drain(b);
            Require(!AssassinSentryLightning.Snapshot().Bolts.Any(x=>x.Actor==b.Serial.Value),"Offline fire lifetime froze");
        }
        finally{AssassinSentryActors.SelectTarget=selector;owner.MoveToWorld(point,map);SummonDifficulties.SetFacets(facets);SummonDifficulties.Sync(owner);t.Delete();b.Delete();}
    }
    private static void Reload()
    {
        Creation();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(AssassinSentryBody)World.FindMobile((Serial)s.Actor);var first=(BladeMissileTarget)World.FindMobile((Serial)s.First);var second=(BladeMissileTarget)World.FindMobile((Serial)s.Second);
        Require(JsonSerializer.Serialize(b.Prepared)==JsonSerializer.Serialize(s.Body)&&JsonSerializer.Serialize(AssassinSentryLightning.State(s.Beam.Id))==JsonSerializer.Serialize(s.Beam)
            &&b.SummonMaster==p&&first.Blessed&&second.Blessed&&SkillHealth.Balance256(first)==s.Health1&&SkillHealth.Balance256(second)==s.Health2&&NecromancerEffects.ManaBalance256(p)==s.Mana,
            $"Saved Inferno mismatch: master={b.SummonMaster?.Serial.Value}/{s.Owner}, protected={first.Blessed}/{second.Blessed}, health={SkillHealth.Balance256(first)}/{s.Health1},{SkillHealth.Balance256(second)}/{s.Health2},mana={NecromancerEffects.ManaBalance256(p)}/{s.Mana}; body={JsonSerializer.Serialize(b.Prepared)} expected={JsonSerializer.Serialize(s.Body)};beam={JsonSerializer.Serialize(AssassinSentryLightning.State(s.Beam.Id))} expected={JsonSerializer.Serialize(s.Beam)}");
        first.Blessed=second.Blessed=false;var point=new Point3D((int)Math.Round(s.Beam.X+s.Beam.Vx),(int)Math.Round(s.Beam.Y),s.Beam.Z);first.MoveToWorld(point,b.Map);second.MoveToWorld(point,b.Map);
        Frames(s.Beam.Id,1,true);var next=AssassinSentryLightning.State(s.Beam.Id);Require(next is{Fault:null}&&next.Frame==s.Beam.Frame+1&&next.X>s.Beam.X&&next.LastCollision!=s.Beam.LastCollision&&next.Seed!=s.Beam.Seed,"Saved fire did not resume once on other collider");
        int emissions=b.Prepared.InfernoControl.Emissions;b.AdvanceController(b.Prepared.NextFrameUtc,true);Require(b.Prepared.InfernoControl.Emissions==emissions&&b.Prepared.InfernoControl.Frame==s.Body.InfernoControl.Frame+1,"Reload replayed sparse channel event");
        Save(p,b,first,second,next.Id);Require(!AssassinCombat.Implemented(CombatAbility.WakeOfInferno),"Reload enabled learned Inferno");
    }
    private static void Live()
    {
        var p=BladeBodyChecks.Fresh(6820,32);p.Name="InfernoSentryLiveFixture";Train(p,CombatAbility.WakeOfFire,20);var b=Actor(p,TimedEffects.Now);var t=Target(b);
        var net=NetState.Instances.Single(x=>x.Mobile==null&&IPAddress.IsLoopback(x.Address));net.Seeded=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;
        var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();int health=SkillHealth.Balance256(t);
        Timer.StartTimer(TimeSpan.FromMilliseconds(3000),()=>
        {
            try
            {
                Require(b.Prepared.InfernoControl.Emissions>=5&&SkillHealth.Balance256(t)<health,"Connected source channel did not emit real damaging fire");var ids=AssassinSentryLightning.Snapshot().Bolts.Where(x=>x.Actor==b.Serial.Value).Select(x=>x.Id).ToArray();Require(ids.Length>0,"No active beam at disconnect");
#pragma warning disable CS0618
                net.Dispose();
#pragma warning restore CS0618
                int hp=SkillHealth.Balance256(t),shots=b.Prepared.InfernoControl.Attempts;Require(p.NetState==null,"Inferno owner did not disconnect");
                Timer.StartTimer(TimeSpan.FromMilliseconds(2000),()=>
                {
                    try{Require(ids.All(id=>AssassinSentryLightning.State(id)==null)&&SkillHealth.Balance256(t)==hp&&b.Prepared.InfernoControl.Attempts==shots,"Offline source channel attacked or froze lifetime");Log.Information("inferno-sentry-live-result: passed");}
                    catch(Exception e){Log.Error(e,"inferno-sentry-live-result: failed");}finally{t.Delete();b.Delete();p.Delete();}
                });
            }
            catch(Exception e){Log.Error(e,"inferno-sentry-live-result: failed");t.Delete();b.Delete();p.Delete();}
        });
    }
}
