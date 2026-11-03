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

public static class TaintedAuraChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(TaintedAuraChecks));
    private sealed record Saved(uint Owner,uint Body,uint Helm,int Mana,int Life,int Map,Point3D Point,Point3D BodyPoint,
        string BodyState,TaintedAuraEmitter Emitter,PaladinAuraLease[] Leases,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"tainted-aura-fixtures.json");
    public static void Configure()
    {
        Register("tainted-aura-check",Check);
        Register("tainted-aura-reload-check",()=>FixtureClock.NextLoop("tainted-aura-reload-result",()=>FixtureNetwork.Connected("tainted-aura-reload-result",Reload)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Tainted inherent resistance state, pulses and original saved emitter",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PaladinAuraLease Lease(Mobile target)=>PaladinAuras.Effect(target,CombatAbility.ResistFire);
    private static PaladinAuraLease[] Leases(WarlockTaintedBody b,PlayerMobile p)=>PaladinAuras.Snapshot().Leases
        .Where(s=>s.Ability==CombatAbility.ResistFire&&(s.Target==b.Serial.Value||s.Target==p.Serial.Value)).OrderBy(s=>s.Target).ToArray();
    private static void Source(WarlockTaintedBody body,PlayerMobile target)
    {
        var original=PaladinAuras.Snapshot();var at=TimedEffects.Now.AddDays(1);int count=0;
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"tainted-aura-callback-cases.json")));
        try
        {
            foreach(var row in doc.RootElement.EnumerateArray())
            {
                if(row[3].GetInt32()!=420){continue;}
                int oldSkill=row[2].GetInt32(),oldRank=row[4].GetInt32(),rank=row[5].GetInt32();
                var old=new PaladinAuraLease(target.Serial.Value,row[6].GetInt32()==202?body.Serial.Value:123,CombatAbility.ResistFire,oldRank,777,at.AddDays(1),oldSkill==100?8:0,SourceSkill:oldSkill);
                PaladinAuras.Restore(new(original.Emitters,original.Leases.Where(s=>s.Target!=target.Serial.Value).Concat(oldRank>0?[old]:Array.Empty<PaladinAuraLease>()).ToArray()));
                int percent=TaintedNpcSkills.ResistFire(body.Prepared.Snapshot.Birth.Suppliers with{ResistFireRank=rank}).Percent;
                bool result=PaladinAuras.ApplyNpcResistance(body,target,420,rank,percent,at,at.AddMilliseconds(2040));var actual=Lease(target);
                Require(result==(row[7].GetInt32()!=0)&&PaladinAuras.SourceSkill(actual)==row[8].GetInt32()&&actual.Rank==row[9].GetInt32()
                    &&actual.Percent==row[12].GetInt32()&&actual.MaximumResistance==row[13].GetInt32()
                    &&actual.Source==(row[10].GetInt32()==202?body.Serial.Value:123)
                    &&actual.UntilUtc==(result?at.AddMilliseconds(2040):old.UntilUtc),"Complete source NPC420 callback replacement projection");count++;
            }
            Require(count==576,"Missing source NPC420 callback contexts");
            using var pulse=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"tainted-aura-pulse-cases.json")));count=0;
            foreach(var row in pulse.RootElement.EnumerateArray())
            {
                var value=TaintedNpcSkills.ResistFire(body.Prepared.Snapshot.Birth.Suppliers with{ResistFireRank=row[0].GetInt32()});
                Require(value.Percent==row[6][0][3].GetInt32()&&value.PeriodFrames==row[4].GetInt32()-row[1].GetInt32()
                    &&row[6][0][4].GetInt32()==TaintedResistFire.LeaseFrames,"Do65 source formula/lease/period projection");count++;
            }
            Require(count==770,"Missing do65 source cases");
        }
        finally{PaladinAuras.Restore(original);}
    }
    private static void Atomic(WarlockTaintedBody body,PlayerMobile owner)
    {
        var state=PaladinAuras.Snapshot();var lease=Lease(owner);string before=JsonSerializer.Serialize(state);
        foreach(var bad in new[]{lease with{SourceSkill=999},lease with{Ability=CombatAbility.ResistCold},lease with{MaximumResistance=1},lease with{Rank=100}})
        {
            bool refused=false;try{PaladinAuras.Restore(state with{Leases=state.Leases.Where(s=>s.Target!=owner.Serial.Value).Append(bad).ToArray()});}catch(InvalidDataException){refused=true;}
            Require(refused&&JsonSerializer.Serialize(PaladinAuras.Snapshot())==before,"Malformed NPC aura restore was not atomic");
        }
        var emitters=TaintedResistFire.Snapshot();var emitter=TaintedResistFire.State(body);before=JsonSerializer.Serialize(emitters);
        foreach(var bad in new[]{emitter with{Actor=0},emitter with{NextUtc=DateTime.SpecifyKind(emitter.NextUtc,DateTimeKind.Local)}})
        {
            bool refused=false;try{TaintedResistFire.Restore(emitters.Append(bad).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&JsonSerializer.Serialize(TaintedResistFire.Snapshot())==before,"Malformed emitter restore was not atomic");
        }
        // Legacy V0 lease JSON omits SourceSkill and still resolves to Paladin100.
        var legacy=new PaladinAuraLease(owner.Serial.Value,owner.Serial.Value,CombatAbility.ResistFire,1,52,TimedEffects.Now.AddDays(1));
        string json=JsonSerializer.Serialize(legacy);Require(!json.Contains("SourceSkill")&&PaladinAuras.SourceSkill(JsonSerializer.Deserialize<PaladinAuraLease>(json))==100,"Legacy source identity migration");
    }
    private static void Check()
    {
        var p=TaintedFireBallChecks.Owner();var at=TimedEffects.Now.AddDays(2);var b=WarlockTaintedBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),at);
        var hostile=FireBoltChecks.Target(p,8);var ally=TaintedFireBallChecks.Owner();ally.MoveToWorld(new(b.X+3,b.Y,b.Z),b.Map);
        var pet=WarlockTaintedBody.CreatePrepared(ally,new(b.X+4,b.Y,b.Z),at);
        try
        {
            Source(b,p);int mana=NecromancerEffects.ManaBalance256(p);SkillHealth.SetBalance256(b,b.MaximumLife256-257);int life=SkillHealth.Balance256(b);
            Require(TaintedResistFire.State(b)?.NextUtc==at.AddMilliseconds(2000)&&Lease(p)==null,"Offline birth failed to register selection or buffed an owner");
            Require(TaintedResistFire.PulsePrepared(b,at)&&Lease(p) is{Rank:2,Percent:29,SourceSkill:420,MaximumResistance:0}
                &&Lease(b)?.Percent==29&&Lease(ally)?.Percent==29&&Lease(pet)?.Percent==29&&Lease(hostile)==null,"Aura self/allies/pet/hostile or NPC payload");
            Require(b.FireResistance==50&&NamedElements.Resistance(hostile,b,"fire",b.FireResistance)==79,"Shared typed NPC resistance consumer");
            Atomic(b,p);
            var supplier=ClassSkillChecks.Player(DiabloClass.Barbarian);ClassSkillChecks.Train(supplier,"barbarian.battlecommand");
            BattleCommand.Apply(supplier,b,1,at);TaintedResistFire.PulsePrepared(b,at);Require(Lease(p) is{Rank:3,Percent:34},"Aura pulse ignored own effective Command rank");
            BattleCommand.Clear(b);supplier.Delete();PaladinAuras.ClearResistanceState(b);PaladinAuras.ClearResistanceState(p);TaintedResistFire.PulsePrepared(b,at);
            ally.MoveToWorld(new(b.X+10,b.Y,b.Z),b.Map);Require(!TaintedResistFire.Recipient(b,p,ally,18),"Outside source radius accepted");
            ally.MoveToWorld(new(b.X+9,b.Y,b.Z),b.Map);Require(TaintedResistFire.Recipient(b,p,ally,18),"Exact source radius rejected");
            ally.MoveToWorld(new(b.X+1,b.Y,b.Z+16),b.Map);Require(!TaintedResistFire.Recipient(b,p,ally,18),"Cross-height aura accepted");
            var paladin=ClassSkillChecks.Player(DiabloClass.Paladin);ClassSkillChecks.Train(paladin,"paladin.resistfire",20);paladin.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);
            PaladinAuras.ApplyNpcResistance(b,p,420,99,70,at,at.AddMilliseconds(2040));
            PaladinAuras.SelectionChanged(paladin,CombatAbility.None,CombatAbility.ResistFire);
            Require(PaladinAuras.SourceSkill(Lease(p))==100&&Lease(p).Rank==20,"Lower Paladin source did not replace a different NPC skill");
            TaintedResistFire.PulsePrepared(b,at);Require(Lease(p) is{Rank:2,SourceSkill:420,MaximumResistance:0},"Lower NPC source did not replace Paladin state3");
            PaladinAuras.Clear(paladin);paladin.Delete();
            TaintedResistFire.Tick(at.AddHours(1),false);
            Require(TaintedResistFire.State(b).NextUtc==at.AddHours(1).AddMilliseconds(2000)&&Lease(p).UntilUtc==at.AddMilliseconds(2040),"Offline clock caught up or refreshed leases");
            PersistentMinions.Suspend(p);Require(TaintedResistFire.State(b)!=null&&!TaintedResistFire.PulsePrepared(b,at.AddHours(1))&&SkillHealth.Balance256(b)==life,"Parking lost selection, pulsed or healed");
            Require(NecromancerEffects.ManaBalance256(p)==mana,"Aura spent owner mana");
            b.Delete();Require(TaintedResistFire.State(b)==null&&Lease(p)!=null,"Deletion retained emitter or prematurely erased recipient lease");
        }
        finally{hostile.Delete();pet.Delete();ally.Delete();b.Delete();PaladinAuras.Clear(p);p.Delete();}
        Save();
    }
    private static Saved Hold(PlayerMobile p,WarlockTaintedBody b,int generation)
    {
        var at=TimedEffects.Now.AddDays(2);Require(TaintedResistFire.PulsePrepared(b,at),"Hold original aura recipients");
        TaintedResistFire.Restore(TaintedResistFire.Snapshot().Select(s=>s.Actor==b.Serial.Value?s with{NextUtc=at.AddMilliseconds(2000)}:s).ToArray());
        return new(p.Serial.Value,b.Serial.Value,p.FindItemOnLayer(Layer.Helm).Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(b),p.Map.MapID,p.Location,b.Location,b.SavedText,TaintedResistFire.State(b),Leases(b,p),generation);
    }
    private static void Save()
    {
        var p=TaintedFireBallChecks.Owner();var b=WarlockTaintedBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now.AddDays(2));
        SkillHealth.SetBalance256(b,b.MaximumLife256-257);var saved=Hold(p,b,0);PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var b=(WarlockTaintedBody)World.FindMobile((Serial)saved.Body);
        Require(saved.Generation<2&&b.ControlMaster==p&&b.SavedText==saved.BodyState&&SkillHealth.Balance256(b)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana
            &&World.FindItem((Serial)saved.Helm)?.Parent==p&&TaintedResistFire.State(b)==saved.Emitter&&Leases(b,p).SequenceEqual(saved.Leases),"Original actor/helm/lease/emitter/resources changed across reload");
        var account=FixtureNetwork.Authenticate(network,p,"tainted-aura-reload-");p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);PersistentMinions.Resume(p);b.MoveToWorld(saved.BodyPoint,p.Map);
        // Connected creation must immediately apply, independently of its first periodic deadline.
        var immediate=WarlockTaintedBody.CreatePrepared(p,new(b.X+1,b.Y,b.Z),TimedEffects.Now);Require(Lease(immediate)?.SourceSkill==420,"Connected birth did not immediately pulse");immediate.Delete();PaladinAuras.ClearResistanceState(b);PaladinAuras.ClearResistanceState(p);
        TaintedResistFire.Restore(TaintedResistFire.Snapshot().Select(s=>s.Actor==b.Serial.Value?s with{NextUtc=TimedEffects.Now.AddMilliseconds(40)}:s).ToArray());
        bool pulsed=false;var deadline=TimedEffects.Now.AddSeconds(7);
        void Observe()
        {
            try
            {
                if(!pulsed&&Lease(b) is{} lease&&lease.UntilUtc<TimedEffects.Now.AddSeconds(3))
                {pulsed=true;Require(lease.SourceSkill==420&&lease.Percent==29,"Real timer payload");PersistentMinions.Suspend(p);}
                if((!pulsed||Lease(b)!=null||Lease(p)!=null)&&TimedEffects.Now<deadline){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));return;}
                Require(pulsed&&Lease(b)==null&&Lease(p)==null&&TaintedResistFire.State(b)!=null&&SkillHealth.Balance256(b)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana,"Real pulse/parking expiry healed, charged, reset selection or failed to expire");
                PersistentMinions.Resume(p);b.MoveToWorld(saved.BodyPoint,p.Map);var next=Hold(p,b,saved.Generation+1);PersistentMinions.Suspend(p);p.Internalize();
                network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();File.WriteAllText(Manifest,JsonSerializer.Serialize(next));Log.Information("tainted-aura-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"tainted-aura-reload-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));
    }
}
