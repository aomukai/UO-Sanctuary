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
namespace Sanctuary.Integration;

public static class PlayerBlizzardChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PlayerBlizzardChecks));
    private sealed record Saved(uint Owner,uint First,uint Second,uint Orb,int Mana,int Life,int FirstLife,int SecondLife,int Map,Point3D Point,BlizzardStorm[] Storms,BlizzardShard[] Shards,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"player-blizzard-fixtures.json");
    public static void Configure()
    {
        var prior=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="PlayerBlizzardFixture"&&prior?.Invoke(p)!=false;
        Register("player-blizzard-check",Check);Register("player-blizzard-reload-check",()=>FixtureClock.NextLoop("player-blizzard-reload-result",()=>FixtureNetwork.Connected("player-blizzard-reload-result",Reload)));
        foreach(string mode in new[]{"right","quick"}){string label="player-blizzard-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    [CallPriority(int.MaxValue)]public static void Initialize(){var prior=ClassGearHooks.CanRegenerateHits;ClassGearHooks.CanRegenerateHits=p=>p.Name!="PlayerBlizzardFixture"&&prior?.Invoke(p)!=false;}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Prepared player Blizzard,source scatter/cold/LastCollide and original saved progression",_=>Core.LoopContext.Post(()=>{try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=FireBoltChecks.Owner(CombatAbility.IceBolt,6340);p.Name="PlayerBlizzardFixture";
        foreach(var a in new[]{CombatAbility.FrostNova,CombatAbility.IceBlast,CombatAbility.GlacialSpike,CombatAbility.Blizzard}){EnchantCastChecks.Train(p,a,1);}
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Blizzard)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Blizzard)&&!SkillBindings.Instance.Assign(p,true,CombatAbility.Blizzard),"Blizzard right/quick only");return p;
    }
    private static BlizzardStorm[] Storms(PlayerMobile p)=>Blizzards.Snapshot().Storms.Where(s=>s.Owner==p.Serial.Value).OrderBy(s=>s.Id).ToArray();
    private static BlizzardShard[] Shards(PlayerMobile p)=>Blizzards.Snapshot().Shards.Where(s=>s.Owner==p.Serial.Value).OrderBy(s=>s.Id).ToArray();
    private static void Replace(PlayerMobile p,BlizzardStorm[] storms,BlizzardShard[] shards)
    {var all=Blizzards.Snapshot();Blizzards.Restore(all with{Storms=all.Storms.Where(x=>x.Owner!=p.Serial.Value).Concat(storms).ToArray(),Shards=all.Shards.Where(x=>x.Owner!=p.Serial.Value).Concat(shards).ToArray()});}
    private static void Clean(PlayerMobile p,params Mobile[] others){Blizzards.Cancel(p);FireBoltChecks.Clean(p,others);}
    private static void Check(){Source();Admission();WorldChecks();Contacts();Save();Log.Information("Player Blizzard:12240native source endpoints/24384scatter comparisons,prepared moving-target release/45frame delay,100frame center/25shards/9frame LastCollide,cold/immunity/potion/return/death/offline/load/legacy and original saved progression pass");}
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blizzard-damage-source-cases.json")));int count=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            int I(int n)=>r[0][n].GetInt32();if(I(0)==0){continue;}var row=SkillMechanics.For(CombatAbility.Blizzard);var v=new SkillValues(row,k=>k==row.SourceKey?I(1):255,k=>k=="Ice Bolt"?I(2):k=="Ice Blast"?I(3):k=="Glacial Spike"?I(4):0,99);var ends=DruidFirestorm.Endpoints(v,I(5),SlowMissilePayload.Normal);
            Require(ends.Min==r[2][4].GetInt32()&&ends.Max==r[2][5].GetInt32()&&v.DurationFrames==100&&v.Mana256==(22+I(1))*256,"Complete-source cold endpoints/length/cost");count++;
        }
        Require(count==12240,"Incomplete Blizzard endpoint scope");using var center=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blizzard-center-source-cases.json")));count=0;
        foreach(var r in center.RootElement.GetProperty("center").EnumerateArray())
        {
            if(r[1][4].GetInt32()!=1){continue;}int x=r[0][5].GetInt32(),frame=r[0][1].GetInt32();var sample=GroundMissileScatter.Sample(x,frame,7);
            Require((long)x+sample.X==r[1][5].GetInt64()&&400+sample.Y==r[1][6].GetInt32()&&sample.Seed==r[1][8].GetUInt64(),"Complete do10 scatter/seed");count++;
        }
        Require(count==24384,"Incomplete Blizzard scatter scope");PlayerBlizzards.Route();
    }
    private static void Admission()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,4);try
        {
            int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.ContinueCast(p,CombatAbility.Blizzard,t,t.Location,Core.TickCount),"Blizzard windup");long at=ClassCombat.For(p).EffectAt;Require(Storms(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Blizzard paid during windup");ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at),"Cancelled Blizzard fired");
            NecromancerEffects.SetMana256(p,5887);Require(!SorceressCombat.Release(p,CombatAbility.Blizzard,t.Serial,t.Location)&&Storms(p).Length==0&&NecromancerEffects.ManaBalance256(p)==5887,"Insufficient Blizzard emitted/paid");NecromancerEffects.SetMana256(p,mana);
            Require(ClassCombat.ContinueCast(p,CombatAbility.Blizzard,t,t.Location,Math.Max(Core.TickCount,ClassCombat.For(p).NextAction)),"Second Blizzard windup");at=ClassCombat.For(p).EffectAt;t.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);EnchantCastChecks.Train(p,CombatAbility.Blizzard,2);Require(ClassCombat.ResolveAction(p,at)&&mana-NecromancerEffects.ManaBalance256(p)==6144&&Storms(p).Single().X==t.X&&Storms(p).Single().Rank==2,"Blizzard live release aim/rank/payment");
            var s=Storms(p).Single();t.MoveToWorld(new(p.X+12,p.Y,p.Z),p.Map);Require(Storms(p).Single()==s,"Blizzard followed released target");mana=NecromancerEffects.ManaBalance256(p);Require(!SorceressCombat.Release(p,CombatAbility.Blizzard,t.Serial,t.Location)&&NecromancerEffects.ManaBalance256(p)==mana,"Blizzard repeated local delay paid");var c=FireGround.Snapshot().Cooldowns.Single(x=>x.Owner==p.Serial.Value&&x.Ability==CombatAbility.Blizzard);Require((c.UntilUtc-TimedEffects.Now).TotalMilliseconds is >1700 and <=1800&&s.UntilUtc>c.UntilUtc,"45frame Blizzard localdelay");
            p.Internalize();Require(!SorceressCombat.Release(p,CombatAbility.Blizzard,Serial.Zero,t.Location)&&NecromancerEffects.ManaBalance256(p)==mana,"Internal Blizzard paid");
        }
        finally{ClassCombat.Stop(p);Clean(p,t);}
    }
    private static void WorldChecks()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,4);try
        {
            var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");Blizzards.Cast(p,t.Location,SkillMechanics.Values(p,CombatAbility.Blizzard));var s=Storms(p).Single();Require(s.Source&&s.Frame==0&&Shards(p).Length==0,"Blizzard immediate field or missing source clock");p.Backpack.DropItem(orb);ClassGear.Refresh(p);EnchantCastChecks.Train(p,CombatAbility.IceBolt,20);int life=SkillHealth.Balance256(t);
            Blizzards.Advance(p,s.NextUtc.AddTicks(-1),true);Require(Shards(p).Length==0&&SkillHealth.Balance256(t)==life,"Early Blizzard emission");Blizzards.Advance(p,s.NextUtc,true);var shard=Shards(p).Single();var v=SkillMechanics.Values(p,CombatAbility.Blizzard,s.Rank);var mods=DruidFirestorm.Modifiers(p);var ends=DruidFirestorm.Endpoints(v,mods.ColdMastery,SlowMissilePayload.Normal);Require(shard.Rank==s.Rank&&shard.Min256==ends.Min&&shard.Max256==ends.Max&&shard.ColdFrames==100&&shard.Modifiers==mods&&shard.Remaining==9&&shard.NextUtc==s.NextUtc.AddMilliseconds(40)&&shard.UntilUtc==s.NextUtc.AddMilliseconds(360),"Retained rank/live child capture or9frame shard");
            t.Internalize();int draws=0;var oldSeed=Blazes.SeedLow;Blizzards.Cancel(p);try{Blazes.SeedLow=()=>{draws++;return 1;};Blizzards.Cast(p,new(p.X+4,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.Blizzard));s=Storms(p).Single();Blizzards.Advance(p,s.UntilUtc,true);Require(Storms(p).Length==0&&draws==25,"100frame do10 emission/lifetime");Blizzards.Advance(p,s.UntilUtc.AddMilliseconds(360),true);Require(Shards(p).Length==0,"Independent shard expiry");}finally{Blazes.SeedLow=oldSeed;}
            t.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);Blizzards.Cast(p,t.Location,SkillMechanics.Values(p,CombatAbility.Blizzard));s=Storms(p).Single();Blizzards.Advance(p,s.NextUtc.AddMilliseconds(200),false);Require(Storms(p).Single().Frame>0&&Shards(p).Length==0,"Offline storm emitted");Blizzards.Advance(p,s.UntilUtc,false);Require(Storms(p).Length==0,"Offline storm lifetime reset");
            Blizzards.Cast(p,t.Location,SkillMechanics.Values(p,CombatAbility.Blizzard));s=Storms(p).Single();var past=TimedEffects.Now.AddMilliseconds(-400);Replace(p,[s with{NextUtc=past,UntilUtc=past+(s.UntilUtc-s.NextUtc)}],[]);Blizzards.Restore(Blizzards.Snapshot(),true);Blizzards.Advance(p,TimedEffects.Now,true);Require(Storms(p).Single().Frame>0&&Shards(p).Length==0,"Disk-loaded missed storm replayed");
            Blizzards.Cancel(p);Blizzards.Cast(p,t.Location,SkillMechanics.Values(p,CombatAbility.Blizzard));s=Storms(p).Single();Blizzards.Advance(p,s.NextUtc,true);shard=Shards(p).Single();Replace(p,[],[shard]);life=SkillHealth.Balance256(t);Blizzards.Advance(p,shard.NextUtc.AddMilliseconds(80),false);Require(Shards(p).Single().Seed==shard.Seed&&Shards(p).Single().Remaining==6&&SkillHealth.Balance256(t)==life,"Offline shard damaged/drew/reset lifetime");
            var paused=Shards(p).Single();var missedAt=TimedEffects.Now.AddMilliseconds(-80);var pastShard=paused with{NextUtc=missedAt,UntilUtc=missedAt+(paused.UntilUtc-paused.NextUtc)};Replace(p,[],[pastShard]);Blizzards.Restore(Blizzards.Snapshot(),true);Blizzards.Advance(p,TimedEffects.Now,true);Require(Shards(p).Single().Seed==paused.Seed&&Shards(p).Single().Remaining<paused.Remaining&&SkillHealth.Balance256(t)==life,"Disk-loaded overdue shard replayed damage/RNG");
            var legacy=s with{Source=false};var old=shard with{Modifiers=null,Rank=0,Seed=0};Replace(p,[legacy],[old]);var json=JsonSerializer.Serialize(Blizzards.Snapshot());Blizzards.Restore(JsonSerializer.Deserialize<BlizzardSave>(json));Require(Storms(p).Single()==legacy&&Shards(p).Single()==old,"Legacy Blizzard payload reconstructed");bool refused=false;try{Replace(p,[s with{Frame=100}],[old]);}catch(InvalidDataException){refused=true;}Require(refused&&Storms(p).Single()==legacy&&Shards(p).Single()==old,"Blizzard restore was not atomic");
        }
        finally{Clean(p,t);}
    }
    private static BlizzardShard Shard(PlayerMobile p,Mobile t,int damage=100)
    {
        long id=Blizzards.Snapshot().Storms.Select(x=>x.Id).Concat(Blizzards.Snapshot().Shards.Select(x=>x.Id)).DefaultIfEmpty().Max()+1;var at=TimedEffects.Now;
        return new(id,p.Serial.Value,p.Map.MapID,t.X,t.Y,t.Z,damage*256,damage*256,100,9,0,at.AddMilliseconds(360),at.AddMilliseconds(40)){Rank=1,Modifiers=DruidFirestorm.Modifiers(p),Seed=SourceRandom.InitLow(1)};
    }
    private static void Contacts()
    {
        var p=Owner();var a=FireBoltChecks.Target(p,4);var b=FireBoltChecks.Target(p,4);try
        {
            var s=Shard(p,a);Replace(p,[],[s]);int life=SkillHealth.Balance256(a);Blizzards.Advance(p,s.UntilUtc,true);Require(life-SkillHealth.Balance256(a)==400*256&&life-SkillHealth.Balance256(b)==400*256&&Shards(p).Length==0,"Overlapped LastCollide alternation/one recipient/final frame");
            b.Internalize();s=Shard(p,a);Replace(p,[],[s]);life=SkillHealth.Balance256(a);Blizzards.Advance(p,s.UntilUtc,true);Require(life-SkillHealth.Balance256(a)==100*256,"One target hit more than once per shard");
            a.Frozen=false;a.CantWalk=true;a.SetHits(400);SkillHealth.SetBalance256(a,400*256);s=Shard(p,a,200);Replace(p,[],[s]);Blizzards.Advance(p,s.NextUtc,true);Require(SkillHealth.Balance256(a)==200*256&&NativeMonsterReturn.Session(a).LastMode==PreparedMonsterReturn.GetHit&&CreatureHitRecovery.Remaining(a)>0&&GearEffects.ChillPercent(a)>0,"Blizzard ordinary ordered cold/get-hit");
            a.SetHits(50000);SkillHealth.SetBalance256(a,50000*256);a.SetResistance(ResistanceType.Cold,100);s=Shard(p,a);Replace(p,[],[s]);life=SkillHealth.Balance256(a);Blizzards.Advance(p,s.NextUtc,true);Require(SkillHealth.Balance256(a)==life,"Cold immunity failed");Require(CombatPotions.Apply(p,3),"Blizzard potion");s=Shard(p,a);Replace(p,[],[s]);Blizzards.Advance(p,s.NextUtc,true);Require(SkillHealth.Balance256(a)<life,"Separate potion lanes failed on cold immune");TimedEffects.Clear(p);life=SkillHealth.Balance256(a);s=Shard(p,a);Replace(p,[],[s]);Blizzards.Advance(p,s.NextUtc,true);Require(SkillHealth.Balance256(a)==life,"Cleared Blizzard potion persisted");
            a.SetResistance(ResistanceType.Cold,0);SkillHealth.SetBalance256(a,100*256);s=Shard(p,a,200);Replace(p,[],[s]);Blizzards.Advance(p,s.NextUtc,true);Require(!a.Alive,"Blizzard lethal ordered delivery");
        }
        finally{Clean(p,a,b);}
    }
    private static void Hold(PlayerMobile p)
    {var at=TimedEffects.Now.Add(FixtureSpace.Retention);Replace(p,Storms(p).Select(s=>s with{NextUtc=at,UntilUtc=at+(s.UntilUtc-s.NextUtc)}).ToArray(),Shards(p).Select(s=>s with{NextUtc=at,UntilUtc=at+(s.UntilUtc-s.NextUtc)}).ToArray());}
    private static Saved Capture(PlayerMobile p,BaseCreature a,BaseCreature b,Item orb,int gen,int map,Point3D point)=>new(p.Serial.Value,a.Serial.Value,b.Serial.Value,orb.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),SkillHealth.Balance256(a),SkillHealth.Balance256(b),map,point,Storms(p),Shards(p),gen);
    private static void Save()
    {
        var p=Owner();EnchantCastChecks.Train(p,CombatAbility.Blizzard,20);var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");var a=FireBoltChecks.Target(p,4);var b=FireBoltChecks.Target(p,4);Require(SorceressCombat.Release(p,CombatAbility.Blizzard,a.Serial,a.Location),"Saved learned Blizzard");Blizzards.Advance(p,Storms(p).Single().NextUtc,true);Require(Storms(p).Single().Frame==1&&Shards(p).Length==1,"Saved partial source storm/shard");Hold(p);SkillHealth.Damage(p,p,5*256+17);var saved=Capture(p,a,b,orb,0,p.Map.MapID,p.Location);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var a=(BaseCreature)World.FindMobile((Serial)s.First);var b=(BaseCreature)World.FindMobile((Serial)s.Second);var orb=World.FindItem((Serial)s.Orb);
        Require(s.Generation<2&&orb?.Parent==p&&Storms(p).SequenceEqual(s.Storms)&&Shards(p).SequenceEqual(s.Shards)&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(a)==s.FirstLife&&SkillHealth.Balance256(b)==s.SecondLife,"Saved original Blizzard actors/gear/state/wounds/resources changed");p.MoveToWorld(s.Point,Map.Maps[s.Map]);a.AIObject.AITimer.Stop();b.AIObject.AITimer.Stop();var account=FixtureNetwork.Authenticate(network,p,"player-blizzard-reload-");var at=TimedEffects.Now.AddMilliseconds(40);Replace(p,s.Storms.Select(x=>x with{NextUtc=at,UntilUtc=at+(x.UntilUtc-x.NextUtc)}).ToArray(),s.Shards.Select(x=>x with{NextUtc=at,UntilUtc=at+(x.UntilUtc-x.NextUtc)}).ToArray());long deadline=Core.TickCount+5000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Natural saved Blizzard timeout");var storms=Storms(p);var shards=Shards(p);bool originalAdvanced=s.Shards.Any(old=>shards.Any(x=>x.Id==old.Id&&x.Seed!=old.Seed));
                if(!originalAdvanced||s.Generation==0&&shards.Length<=s.Shards.Length){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(storms.Length==1&&storms[0].Id==s.Storms[0].Id&&storms[0].Frame>s.Storms[0].Frame&&s.Shards.All(old=>shards.Any(x=>x.Id==old.Id&&x.Min256==old.Min256&&x.Modifiers==old.Modifiers&&x.Remaining<old.Remaining))&&SkillHealth.Balance256(a)+SkillHealth.Balance256(b)<s.FirstLife+s.SecondLife&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life,"Original center/shard natural progression reset payload/clock/resources");Hold(p);p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();File.WriteAllText(Manifest,JsonSerializer.Serialize(Capture(p,a,b,orb,s.Generation+1,s.Map,s.Point)));Log.Information("player-blizzard-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"player-blizzard-reload-result: failed");}
        }
        Observe();
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var t=FireBoltChecks.Target(p,4);var account=FixtureNetwork.Authenticate(network,p,"player-blizzard-input-");int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t);bool began=false;long deadline=Core.TickCount+7000;
        void Finish(){ClassCombat.Stop(p);Clean(p,t);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Blizzard wire/natural source timeout");if(ClassCombat.For(p).PendingAbility==CombatAbility.Blizzard&&!began){Require(Storms(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Wire Blizzard paid in windup");began=true;Log.Information("{Label}: windup",label);}
                if(SkillHealth.Balance256(t)>=life){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}Require(began&&NecromancerEffects.ManaBalance256(p)==mana-5888&&Storms(p).Single().Source&&Shards(p).Any(x=>x.Modifiers!=null),"Wire Blizzard payment/source progression");Finish();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Finish();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Blizzard,(int)DiabloClass.Sorceress,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
