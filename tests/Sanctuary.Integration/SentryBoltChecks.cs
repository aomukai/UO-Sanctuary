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

public static class SentryBoltChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SentryBoltChecks));
    private sealed record Saved(uint Owner,uint Actor,uint Target,int Map,Point3D Position,SentryBoltState Bolt,int Health);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sentry-bolt-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("sentry-bolt-check",Check);Register("sentry-bolt-reload-check",Reload);Register("sentry-bolt-live-check",Live);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Real prepared trap-owned Sentry bolts, captured lightning and persistence; learned cast gated",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static BladeMissileTarget Target(PlayerMobile p,int dx=4)
    {
        var t=new BladeMissileTarget{Name="SentryBoltTarget",NoKillAwards=true};t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();
        t.SetHits(2000);t.Hits=t.HitsMax;foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}return t;
    }
    private static AssassinSentryBody Actor(PlayerMobile p,DateTime at)=>AssassinSentryBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),at,SourceRandom.InitLow(19));
    private static SentryBoltSpawn Spawn(AssassinSentryBody b,int dx=20,int ordinal=0)=>new(ordinal,new((b.X+dx)*2,b.Y*2),ChargedBolts.SeedSubtile((b.X+dx)*2,ordinal),77,77);
    private static void Frames(long[] ids,int count,bool actions)
    {
        for(int i=0;i<count;i++)
        {
            var bolts=ids.Select(AssassinSentryBolts.State).Where(x=>x!=null).ToArray();if(bolts.Length==0){return;}
            AssassinSentryBolts.TickFrame(bolts.Min(x=>x.NextUtc),actions);
        }
    }
    private static void CaptureAndDamage(PlayerMobile p,AssassinSentryBody b,BladeMissileTarget t)
    {
        var id=AssassinSentryBolts.Create(b,Spawn(b));var s=AssassinSentryBolts.State(id);var ends=AssassinSentryCreation.Lightning(b.Prepared.Creation);
        Require(s.Minimum256==ends.Min&&s.Maximum256==ends.Max&&s.Actor==b.Serial.Value&&s.Owner==p.Serial.Value&&s.Frames==77&&s.VelocityFp8==2304,"Actual bolt release endpoints/owner/factory velocity");
        int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(p);
        foreach(int pierce in new[]{20,-20})
        {
            t.SetResistance(ResistanceType.Energy,0);t.Hits=t.HitsMax;SkillHealth.Clear(t);int before=SkillHealth.Balance256(t);
            var packet=s with{Minimum256=10*256,Maximum256=10*256,Pierce=pierce};
            int expected=2560*(100+pierce)/100; // Independent source resistance arithmetic at base0.
            AssassinSentryBolts.Impact(b,t,packet);Require(before-SkillHealth.Balance256(t)==expected,"Trap captured signed pierce/FP256 mitigation");
        }
        t.SetResistance(ResistanceType.Energy,100);t.Hits=t.HitsMax;SkillHealth.Clear(t);int immune=SkillHealth.Balance256(t);
        AssassinSentryBolts.Impact(b,t,s with{Minimum256=2560,Maximum256=2560,Pierce=90});Require(SkillHealth.Balance256(t)==immune,"Ordinary trap pierce broke immunity");t.SetResistance(ResistanceType.Energy,0);
        Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(p)==life,"Pure lightning inherited weapon leech or source resource cost");
        var kill=Target(p,6);kill.SetHits(1);kill.Hits=1;AssassinSentryBolts.Impact(b,kill,s with{Minimum256=2560,Maximum256=2560});
        Require(!kill.Alive&&kill.LastKiller==b&&ClassLoot.Killer(kill)==p,"Trap lethal damage lost actual NPC/original player credit");kill.Delete();
        Frames([id],77,false);Require(AssassinSentryBolts.State(id)==null,"Offline flight lifetime did not expire");
    }
    private static void Check()
    {
        var p=BladeBodyChecks.Fresh(6240,40);p.Name="SentryBoltFixture";
        while(p.RawStr<60){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength for real gear");}
        var eye=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Griffon's Eye").Id,99,new Random(173),true);
        p.AddToBackpack(eye);Require(p.EquipItem(eye),"Actual Griffon supplier");ClassGear.Refresh(p);
        var b=Actor(p,DateTime.UtcNow.AddDays(1));var t=Target(p);
        Require(b.Prepared.Creation.Rank==2&&b.Passive("passive_ltng_mastery")>0&&b.Passive("passive_ltng_pierce")>0,"Actual actor birth lost real soft rank/mastery/pierce");
        var frozen=AssassinSentryCreation.Lightning(b.Prepared.Creation);p.AddToBackpack(eye);ClassGear.Refresh(p);
        Require(AssassinSentryCreation.Lightning(b.Prepared.Creation)==frozen&&ClassGear.Bonus(p,"extra-ltng")==0,"Actor followed owner gear removal");
        CaptureAndDamage(p,b,t);t.Hits=t.HitsMax;SkillHealth.Clear(t);int before=SkillHealth.Balance256(t);
        for(int i=0;i<28;i++){b.AdvanceController(b.Prepared.NextFrameUtc,true);}
        var volley=AssassinSentryBolts.Snapshot().Bolts.Where(x=>x.Actor==b.Serial.Value).Select(x=>x.Id).ToArray();
        Require(volley.Length==AssassinSentryCreation.Bolts(b.Prepared.Creation)&&b.Prepared.Control is{Dispatches:1,Ai.RemainingShots:4},"Actual source controller did not create its real full volley");
        Frames(volley,30,true);Require(SkillHealth.Balance256(t)<before,"Travelling source-owned volley caused no real damage");Frames(volley,77,false);
        var id=AssassinSentryBolts.Create(b,Spawn(b));var old=AssassinSentryBolts.State(id);Frames([id],10,false);var offline=AssassinSentryBolts.State(id);
        Require(offline.Frame==old.Frame+10&&offline.X==old.X&&offline.Y==old.Y&&offline.Seed==old.Seed,"Offline bolt moved or replayed random draws");Frames([id],77,false);
        // Actual Slow Missiles state component, no claim of an enabled cast.
        var amazon=ClassSkillChecks.Player(DiabloClass.Amazon);Require(AmazonSlowMissiles.Apply(amazon,b,1,TimedEffects.Now),"Slow state service");
        var slowId=AssassinSentryBolts.Create(b,Spawn(b));var slowed=AssassinSentryBolts.State(slowId);
        Require(slowed.Slow.VelocityPercent<100&&slowed.VelocityFp8<2304&&slowed.Maximum256<AssassinSentryCreation.Lightning(b.Prepared.Creation).Max,"Actual factory ignored captured slow velocity/damage");
        Frames([slowId],77,false);amazon.Delete();AmazonSlowMissiles.Clear(b);
        var orphan=Actor(p,DateTime.UtcNow.AddDays(1));var orphanId=AssassinSentryBolts.Create(orphan,Spawn(orphan,2));orphan.Delete();
        int orphanHealth=SkillHealth.Balance256(t);Frames([orphanId],77,true);
        Require(AssassinSentryBolts.State(orphanId)==null&&SkillHealth.Balance256(t)==orphanHealth,"Missing actor became a player-owned attacker or stopped expiring");
        var held=AssassinSentryBolts.Create(b,Spawn(b));Frames([held],1,true);var saved=AssassinSentryBolts.State(held);
        Require(saved is{Frame:1,Fault:null}&&saved.X!=saved.OriginX&&saved.MotionAdapter=="native-shared-chargedbolt-v1","Prepared native movement did not advance");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,t.Serial.Value,p.Map.MapID,p.Location,saved,SkillHealth.Balance256(t))));
        Require(!AssassinCombat.Implemented(CombatAbility.ChargedBoltSentry),"Bolt backend enabled unfinished learned casting");
        Log.Information("Sentry real bolts: actual source controller volley, travelling lightning, signed captured pierce, immunity, no weapon leech, NPC/player kill credit, slow capture, offline lifetime and saved path/seed pass; movement remains native adapter");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var b=(AssassinSentryBody)World.FindMobile((Serial)saved.Actor);var t=(BladeMissileTarget)World.FindMobile((Serial)saved.Target);
        var state=AssassinSentryBolts.State(saved.Bolt.Id);
        Require(JsonSerializer.Serialize(state)==JsonSerializer.Serialize(saved.Bolt)&&b.SummonMaster==p&&SkillHealth.Balance256(t)==saved.Health,"Real bolt reload lost path/pose/index/seed/clock/damage/ownership");
        p.MoveToWorld(saved.Position,Map.Maps[saved.Map]);Frames([state.Id],1,true);var next=AssassinSentryBolts.State(state.Id);
        Require(next!=null&&next.Fault==null&&next.Frame==state.Frame+1&&next.X!=state.X&&next.Minimum256==state.Minimum256&&next.Pierce==state.Pierce,"Saved bolt did not resume once with retained packet");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Bolt=next}));
        Require(!AssassinCombat.Implemented(CombatAbility.ChargedBoltSentry),"Reload enabled unfinished learned casting");
    }
    private static void Live()
    {
        var p=BladeBodyChecks.Fresh(6280,40);p.Name="SentryBoltLiveFixture";var t=Target(p);var net=NetState.Instances.Single(x=>x.Mobile==null&&IPAddress.IsLoopback(x.Address));
        net.Seeded=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;
        var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();
        var b=Actor(p,TimedEffects.Now);int health=SkillHealth.Balance256(t);
        Timer.StartTimer(TimeSpan.FromMilliseconds(2300),()=>
        {
            try
            {
                Require(b.Prepared.Control.Dispatches>0&&SkillHealth.Balance256(t)<health,"Connected automatic trap clock did not fire real damaging bolts");
                var ids=AssassinSentryBolts.Snapshot().Bolts.Where(x=>x.Actor==b.Serial.Value).Select(x=>x.Id).ToArray();Require(ids.Length>0,"No released flight for offline expiry test");
                // NetState documents immediate Dispose for testing cleanup.
                // Use that explicit boundary; graceful disconnect can await I/O.
#pragma warning disable CS0618
                net.Dispose();
#pragma warning restore CS0618
                Require(p.NetState==null,"Immediate fixture socket disposal did not clear owner connection");
                int remaining=SkillHealth.Balance256(t),attempts=b.Prepared.Control.Attempts,dispatches=b.Prepared.Control.Dispatches;
                Timer.StartTimer(TimeSpan.FromMilliseconds(3300),()=>
                {
                    try{Require(ids.All(x=>AssassinSentryBolts.State(x)==null)&&SkillHealth.Balance256(t)==remaining&&b.Prepared.Control.Attempts==attempts&&b.Prepared.Control.Dispatches==dispatches,"Offline natural trap clock attacked or froze bolt lifetime");Log.Information("sentry-bolt-live-result: passed");}
                    catch(Exception e){Log.Error(e,"sentry-bolt-live-result: failed");}finally{t.Delete();b.Delete();p.Delete();}
                });
            }
            catch(Exception e){t.Delete();b.Delete();p.Delete();Log.Error(e,"sentry-bolt-live-result: failed");}
        });
    }
}
