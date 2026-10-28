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

public sealed class ShockWaveTarget:GraveyardSkeleton
{
    internal bool Observe,StunDuringDamage;
    public ShockWaveTarget(){}
    public ShockWaveTarget(Serial serial):base(serial){}
    public override bool CanRegenHits=>false;
    public override void OnThink(){}
    public override void OnSectorActivate()=>AIObject?.AITimer.Stop();
    public override void OnDamage(int amount,Mobile from,bool willKill)
    {if(Observe){StunDuringDamage|=CombatStuns.Remaining(this)>0;}base.OnDamage(amount,from,willKill);}
    public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
    public override void Deserialize(IGenericReader reader){base.Deserialize(reader);if(reader.ReadInt()!=0){throw new InvalidDataException("Invalid Shock Wave fixture");}}
}
public static class ShockWaveChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ShockWaveChecks));
    private sealed record Saved(uint Owner,uint Target,int Life256,ShockWaveSave State,CombatStunState[] Stuns,
        uint ExpiryOwner=0,uint ExpiryTarget=0,int ExpiryLife256=0,int ExpiredChecks=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shock-wave-fixtures.json");
    private static int _scene;
    public static void Configure()
    {Register("shock-wave-components-check",Check);Register("shock-wave-components-reload-check",Reload);Register("shock-wave-expiry-stage-check",ExpiryStage);Register("shock-wave-live-check",()=>FixtureNetwork.Connected("shock-wave-live-result",Live));}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared travelling Shock Wave components",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Name="ShockWaveFixture",Body=0x190,Player=true};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);FixtureSpace.Corridor(p,6260,20,2200+32*_scene++);return p;
    }
    private static ShockWaveTarget Target(PlayerMobile p,int offset)
    {
        var t=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(50000);t.Hits=t.HitsMax;
        t.MoveToWorld(new(p.X+offset,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}return t;
    }
    private static SkillValues Values(int rank=1,int hard=0)=>new(SkillMechanics.For(CombatAbility.ShockWave),_=>rank,key=>key=="Maul"?hard:0,99);
    private static ShockWaveMissile One(PlayerMobile p,DateTime at)
    {DruidShockWave.Create(p,new(p.X+10,p.Y,p.Z),Values(),at);return DruidShockWave.Snapshot().Missiles.Last(x=>x.Owner==p.Serial.Value);}
    private static void Arithmetic()
    {
        using var damage=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-shock-wave-damage-source-probe.json")));int physical=0,stuns=0;
        foreach(var c in damage.RootElement.GetProperty("physicalCases").EnumerateArray())
        {var v=Values(c[0].GetInt32(),c[1].GetInt32());Require(v.PhysicalDamage256(false)==c[3].GetInt32()&&v.PhysicalDamage256(true)==c[4].GetInt32()&&v.Mana256==1792,"Complete physical getter/current mana differs");physical++;}
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-shock-wave-stun-source-probe.json")));
        foreach(var c in source.RootElement.EnumerateArray())
        {
            if(c[9].GetInt32()!=0){continue;}int frames=CombatStuns.Frames(c[2].GetInt32(),true,c[3].GetInt32()!=0,c[4].GetInt32()!=0,c[5].GetInt32(),c[6].GetInt32()!=0,c[7].GetInt32());
            int applied=c[13].GetInt32()+c[15].GetInt32();Require(frames==(applied>0?c[12].GetInt32()-1000:0),"Full source eligibility/RNG/cap differs");stuns++;
        }
        Require(physical==3825&&stuns==17920,"Missing source comparisons");Log.Information("Shock Wave:3825physical/17920stun source comparisons pass");
    }
    private static void Cases()
    {
        var held=DruidShockWave.Snapshot();var p=Fresh();var a=Target(p,1);var b=Target(p,1);var at=TimedEffects.Now.Add(FixtureSpace.Retention);
        var block=SourceBlocking.BlockRoll;var seed=DruidShockWave.SeedLow;var allocate=DruidShockWave.Allocate;
        var shields=SourceBlocking.Snapshot();var slows=AmazonSlowMissiles.Snapshot();
        try
        {
            SourceBlocking.BlockRoll=_=>99;DruidShockWave.SeedLow=()=>1;NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p);
            var wave=One(p,at);var group=DruidShockWave.Snapshot().Missiles.Where(x=>x.Owner==p.Serial.Value).ToArray();
            Require(group.Length==5&&group.Select(x=>x.Cast).Distinct().Count()==1&&group.All(x=>x.Remaining==14&&x.LastCollision==p.Serial.Value&&x.Min256==2560&&x.Max256==5120),"Five-ray source-zero snapshot");
            AmazonSlowMissiles.Restore(slows.Where(x=>x.Target!=p.Serial.Value).Append(new SlowMissilesState(p.Serial.Value,p.Serial.Value,1,50,100,at.AddSeconds(1))).ToArray());
            var slowed=One(p,at);Require(Math.Abs(slowed.Vx-wave.Vx/2)<=1/131072.0&&Math.Abs(slowed.Vy-wave.Vy/2)<=1/131072.0&&slowed.Min256==wave.Min256,"Captured50percent factory slow/endpoints");
            AmazonSlowMissiles.Restore(slows.Where(x=>x.Target!=p.Serial.Value).Append(new SlowMissilesState(p.Serial.Value,p.Serial.Value,1,50,50,at.AddSeconds(1))).ToArray());
            string unsupported=Json(DruidShockWave.Snapshot());
            try{One(p,at);throw new InvalidOperationException("Unverified damage scale silently ignored");}catch(NotSupportedException){}
            Require(Json(DruidShockWave.Snapshot())==unsupported,"Unsupported damage scale partially allocated");AmazonSlowMissiles.Restore(slows);
            a.Observe=true;int life=SkillHealth.Balance256(a);var hit=DruidShockWave.Impact(p,a,wave);
            Require(SkillHealth.Balance256(a)<life&&!a.StunDuringDamage&&CombatStuns.Remaining(a)>0&&hit.LastCollision==a.Serial.Value,"Health-before-stun/source history");
            var cell=new SourceMissileCell(a.X*2,a.Y*2);
            Require(DruidShockWave.Recipient(p,group[0],cell)==b,"Delayed overlapping first target hid valid second recipient");
            DruidShockWave.Impact(p,b,group[0]);Require(DruidShockWave.Recipient(p,group[1],cell)==null,"Cast-local window not shared across rays");
            Require(!DruidShockWave.Ready(wave.Cast,a.Serial.Value,wave.NextUtc.AddMilliseconds(159))&&DruidShockWave.Ready(wave.Cast,a.Serial.Value,wave.NextUtc.AddMilliseconds(160)),"Four-frame inclusive expiry");
            var other=One(p,at);Require(DruidShockWave.Ready(other.Cast,a.Serial.Value,wave.NextUtc),"Different execution shared window");
            CombatStuns.Clear(a);a.SetResistance(ResistanceType.Physical,100);life=SkillHealth.Balance256(a);DruidShockWave.Impact(p,a,other);
            Require(SkillHealth.Balance256(a)==life&&CombatStuns.Remaining(a)>0,"Physical immunity suppressed stun");
            CombatStuns.Clear(a);a.SetResistance(ResistanceType.Physical,0);
            string reference=CreatureProfiles.Definition(a).Reference;var rule=BalanceCatalog.Read<SourceBlockRule>("source-block-rules").Single(x=>x.Source==reference);
            int choice=Array.FindIndex(rule.ShieldItems,x=>x);Require(choice>=0,"Fixture mapped shield missing");
            SourceBlocking.Restore(shields.Where(x=>x.Actor!=a.Serial.Value).Append(new SourceShield(a.Serial.Value,reference,choice)).ToArray());SourceBlocking.BlockRoll=_=>0;
            Require(SourceBlocking.TryRate(a,out int rate,out _)&&rate>0,"Fixture shield cannot block");life=SkillHealth.Balance256(a);
            DruidShockWave.Impact(p,a,One(p,at));Require(SkillHealth.Balance256(a)==life&&CombatStuns.Remaining(a)==0,"Blocked wave damaged or stunned");SourceBlocking.BlockRoll=_=>99;
            var saved=DruidShockWave.Snapshot();string before=Json(saved);
            try{DruidShockWave.Restore(saved with{Missiles=[..saved.Missiles,saved.Missiles[0]]});throw new InvalidOperationException("Duplicate accepted");}catch(InvalidDataException){}
            Require(Json(DruidShockWave.Snapshot())==before,"Invalid restore partially mutated state");saved.Missiles[0].Steps.Cells[0]=new(1,1);Require(Json(DruidShockWave.Snapshot())==before,"Snapshot aliases mutable steps");
            DruidShockWave.Tick(at.AddSeconds(1),false);Require(DruidShockWave.Snapshot().Missiles.All(x=>x.Owner!=p.Serial.Value),"Offline lifetime frozen");
            DruidShockWave.Allocate=_=>false;Require(DruidShockWave.Create(p,new(p.X+10,p.Y,p.Z),Values(),at).Length==0,"Failed factory made waves");DruidShockWave.Allocate=allocate;
            Require(NecromancerEffects.ManaBalance256(p)==mana,"Prepared controller charged learned-cast mana");
        }
        finally{DruidShockWave.Restore(held);SourceBlocking.BlockRoll=block;DruidShockWave.SeedLow=seed;DruidShockWave.Allocate=allocate;AmazonSlowMissiles.Restore(slows);CombatStuns.Clear(a);CombatStuns.Clear(b);a.Delete();b.Delete();p.Delete();SourceBlocking.Restore(shields);}
    }
    private static void Check()
    {
        Arithmetic();Cases();
        var p=Fresh();var t=Target(p,1);var at=TimedEffects.Now.Add(FixtureSpace.Retention);var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try{One(p,at);DruidShockWave.Tick(at.AddMilliseconds(40),true);}finally{SourceBlocking.BlockRoll=block;}
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,t.Serial.Value,SkillHealth.Balance256(t),DruidShockWave.Snapshot(),CombatStuns.Snapshot())));
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        // Actual startup elapsed time,not a synthetic timestamp shift. Force
        // the first due dispatch before inspection if the wheel has not run yet.
        DruidShockWave.Tick(TimedEffects.Now,true);
        Require(s.ExpiryOwner!=0&&DruidShockWave.Snapshot().Missiles.All(x=>x.Owner!=s.ExpiryOwner)
            &&World.FindMobile((Serial)s.ExpiryTarget) is ShockWaveTarget expired&&SkillHealth.Balance256(expired)==s.ExpiryLife256,"Restart replayed an expired wave or erased its target");
        Require(Json(DruidShockWave.Snapshot())==Json(s.State)&&Json(CombatStuns.Snapshot())==Json(s.Stuns)&&World.FindMobile((Serial)s.Target) is ShockWaveTarget t&&SkillHealth.Balance256(t)==s.Life256,"Saved wave/seed/history/window/stun/life differs");
        World.FindMobile((Serial)s.ExpiryTarget).Delete();World.FindMobile((Serial)s.ExpiryOwner).Delete();
        File.WriteAllText(Manifest,Json(s with{ExpiryOwner=0,ExpiryTarget=0,ExpiredChecks=s.ExpiredChecks+1}));
    }
    private static void ExpiryStage()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=Fresh();var t=Target(p,1);
        var ids=DruidShockWave.Create(p,new(p.X+10,p.Y,p.Z),Values(),TimedEffects.Now);
        Require(ids.Length==5,"Expired-at-restart fixture missing real waves");
        File.WriteAllText(Manifest,Json(s with{ExpiryOwner=p.Serial.Value,ExpiryTarget=t.Serial.Value,ExpiryLife256=SkillHealth.Balance256(t),State=s.State with{NextId=DruidShockWave.Snapshot().NextId}}));
    }
    private static void Live(NetState network)
    {
        var held=DruidShockWave.Snapshot();var p=Fresh();var t=Target(p,2);int life=SkillHealth.Balance256(t);var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));
        DruidShockWave.Create(p,new(p.X+10,p.Y,p.Z),Values(),TimedEffects.Now);long deadline=Core.TickCount+5000;
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Natural Shock Wave expiry timeout");
                if(DruidShockWave.Snapshot().Missiles.Any(x=>x.Owner==p.Serial.Value)){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(SkillHealth.Balance256(t)<life&&CombatStuns.Remaining(t)>0&&Json(DruidShockWave.Snapshot() with{NextId=held.NextId})==Json(held),"Natural wave damage/stun/expiry or held-state contamination");
                SourceBlocking.BlockRoll=block;CombatStuns.Clear(t);t.Delete();p.Delete();
                var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
                File.WriteAllText(Manifest,Json(saved with{State=DruidShockWave.Snapshot(),Stuns=CombatStuns.Snapshot()}));
                Log.Information("shock-wave-live-result: passed");
            }
            catch(Exception e){SourceBlocking.BlockRoll=block;CombatStuns.Clear(t);t.Delete();p.Delete();Log.Error(e,"shock-wave-live-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
}
