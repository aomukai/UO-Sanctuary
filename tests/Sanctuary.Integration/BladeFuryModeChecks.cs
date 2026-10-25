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

public static class BladeFuryModeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeFuryModeChecks));
    private sealed record Saved(uint Owner,int Mana256);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blade-fury-mode-fixture.json");
    private static bool _creating;
    private static void Rates()
    {
        var old=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>_creating||p.Name=="BladeFuryModeFixture"?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;
    }
    public static void Configure()
    {
        Rates();Register("blade-fury-mode-reload-check",Reload);
        Register("blade-fury-mode-live-check",Live);
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,
        "Prepared Fury player timer, explicit intents, real mana/damage; learned input disabled",_=>Core.LoopContext.Post(()=>
        {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static void Reset(PlayerMobile p,int mana=1663)
    {
        BladeFuryModes.Cancel(p);WeaponMissiles.Cancel(p);ClassCombat.Stop(p);
        var s=ClassCombat.For(p);s.PendingAbility=CombatAbility.None;s.NextAction=s.RecoveryUntil=Core.TickCount-1;
        NecromancerEffects.SetMana256(p,mana);
    }
    private static void Cases(PlayerMobile p)
    {
        var at=TimedEffects.Now;var point=new Point3D(p.X+15,p.Y,p.Z);
        Reset(p,1535);Require(!BladeFuryModes.BeginPrepared(p,Serial.Zero,point,at)&&BladeFuryModes.State(p)==null
            &&NecromancerEffects.ManaBalance256(p)==1535,"Prepared fresh six-mana threshold");
        Reset(p);Require(BladeFuryModes.BeginPrepared(p,Serial.Zero,point,at),"Prepared fresh mode entry");
        Require(BladeFuryModes.State(p).Sequence.Channel.Flags==0&&Own(p).Length==0&&NecromancerEffects.ManaBalance256(p)==1663,"Fresh mode flag/order/upfront debit");
        BladeFuryModes.StopIntent(p,at.AddMilliseconds(40));BladeFuryModes.Advance(p,at.AddMilliseconds(11*40));
        Require(Own(p).Length==0,"Prepared mode skipped initial warmup");
        BladeFuryModes.Advance(p,at.AddMilliseconds(12*40));
        Require(Own(p).Length==1&&NecromancerEffects.ManaBalance256(p)==1407&&!BladeFuryModes.State(p).Sequence.Channel.Active,"Pending released mode event did not emit/pay once");
        BladeFuryModes.Advance(p,at.AddMilliseconds(19*40));
        Require(!BladeFuryModes.Active(p)&&BladeFuryModes.State(p).Sequence.Channel.HasLease&&Own(p).Length==1,"End animation confused with lease expiry");
        BladeFuryModes.Advance(p,at.AddMilliseconds(21*40));Require(BladeFuryModes.State(p)==null&&Own(p).Length==1,"Expiry retained mode or deleted released blade");
        Reset(p);Require(BladeFuryModes.BeginPrepared(p,Serial.Zero,point,at),"Dry-mode end/stop setup");
        NecromancerEffects.SetMana256(p,0);BladeFuryModes.Advance(p,at.AddMilliseconds(19*40));
        Require(!BladeFuryModes.Active(p)&&BladeFuryModes.State(p).Sequence.Channel is{Active:true,Lease:true},"Dry event/end removed independent state bit or lease");
        Require(BladeFuryModes.StopIntent(p,at.AddMilliseconds(20*40))&&BladeFuryModes.State(p).Sequence.Channel is{Active:false,Lease:true},"Stop intent failed after the animation ended");
        Reset(p);Require(BladeFuryModes.BeginPrepared(p,Serial.Zero,point,at),"Early accepted callback setup");
        Require(BladeFuryModes.StartCallback(p,at.AddMilliseconds(6*40)),"Explicit accepted callback");
        BladeFuryModes.Advance(p,at.AddMilliseconds(13*40));
        Require(Own(p).Length==1&&NecromancerEffects.ManaBalance256(p)==1407&&!BladeFuryModes.State(p).Sequence.Channel.HasLease,"Expiry/action FIFO tie added a second blade");
        Reset(p);Require(BladeFuryModes.BeginPrepared(p,Serial.Zero,point,at),"Block/recovery mode setup");
        long recovery=ClassCombat.For(p).RecoveryUntil;HitRecovery.Block(p,Core.TickCount);
        Require(BladeFuryModes.Active(p)&&ClassCombat.For(p).RecoveryUntil==recovery,"Block interrupted prepared active skill");
        Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount)&&!BladeFuryModes.Active(p)
            &&BladeFuryModes.State(p).Sequence.Channel.HasLease,"Accepted hit recovery did not interrupt while retaining lease");
        Reset(p);Require(BladeFuryModes.BeginPrepared(p,Serial.Zero,point,at),"Movement mode setup");
        var origin=p.Location;p.MoveToWorld(new(origin.X+1,origin.Y,origin.Z),p.Map);ClassMovement.Moved(p,Direction.East);p.MoveToWorld(origin,p.Map);
        Require(!BladeFuryModes.Active(p)&&BladeFuryModes.State(p).Sequence.Channel.HasLease,"Movement retained mode or removed its lease");
        BladeFuryModes.Advance(p,at.AddMilliseconds(21*40));Require(Own(p).Length==0&&BladeFuryModes.State(p)==null,"Returned-to-origin revived mode");
        Reset(p);Require(BladeFuryModes.BeginPrepared(p,Serial.Zero,point,at),"Weapon change mode setup");
        var claw=new Tekagi();ClassGear.Profiles.Convert(claw,99,GearRarity.Normal,new Random(173));p.AddToBackpack(claw);Require(p.EquipItem(claw),"Real selected claw equip");
        Require(!BladeFuryModes.Active(p)&&BladeFuryModes.State(p).Sequence.Channel.HasLease,"Weapon change did not interrupt prepared mode");p.AddToBackpack(claw);
        Reset(p);Require(BladeFuryModes.BeginPrepared(p,Serial.Zero,point,at),"Actual soft-rank mode setup");
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm)&&BladeFuryModes.Active(p),"Nonweapon rank change cancelled prepared mode");
        Require(SkillMechanics.Rank(p,CombatAbility.BladeFury)==2,"Actual soft Fury rank");
        BladeFuryModes.Advance(p,at.AddMilliseconds(12*40));Require(Own(p).Single().SkillRank==2&&NecromancerEffects.ManaBalance256(p)==1375,"Mode event cached stale used-skill rank/cost");p.AddToBackpack(helm);helm.Delete();
        Reset(p);var target=MagicArrowChecks.Target(p);target.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);
        Require(BladeFuryModes.BeginPrepared(p,target.Serial,point,at),"Targeted prepared mode");target.MoveToWorld(new(p.X,p.Y+6,p.Z),p.Map);
        BladeFuryModes.Advance(p,at.AddMilliseconds(12*40));Require(Own(p).Single().Vy>0&&Math.Abs(Own(p).Single().Vx)<0.01,"Present target coordinates were not requeried");
        target.Delete();BladeFuryModes.Advance(p,at.AddMilliseconds(17*40));Require(Own(p).Length==2&&Own(p).Last().Vx>0&&Math.Abs(Own(p).Last().Vy)<0.01,"Deleted target failed retained-point fallback");
        Reset(p);Require(BladeFuryModes.BeginPrepared(p,Serial.Zero,point,at),"Respec prepared mode");
        ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);
        Require(BladeFuryModes.State(p)==null,"Respec retained transient prepared mode");
        // Re-train only the mode's normal prerequisite path after the real respec.
        void Train(string id){Require(TalentCatalog.TryGet(DiabloClass.Assassin,id,out var t),"Mode talent");foreach(string pre in t.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,pre)==0){Train(pre);}}if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Mode retrain");}}
        Train("assassin.bladefury");Reset(p);
        Require(!AssassinCombat.Implemented(CombatAbility.BladeFury),"Prepared timer exposed learned input");
        Log.Information("Prepared Fury mode cases: actual fresh threshold,12-frame warmup, pending stop shot, independent end/expiry and released flight, FIFO expiry tie, block/FHR, move-away/return, real claw change, Tarnhelm live rank, target requery/deletion and respec pass; accepted callback is not a client heartbeat gate");
    }
    private static void Live()
    {
        PlayerMobile p;_creating=true;try{p=BladeBodyChecks.Fresh(6860,30);}finally{_creating=false;}p.Name="BladeFuryModeFixture";
        while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Mode strength allocation");}
        while(p.RawDex<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Mode dexterity allocation");}ClassGear.Refresh(p);
        var net=NetState.Instances.Single(x=>x.Mobile==null&&IPAddress.IsLoopback(x.Address));net.Seeded=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;
        var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();
        var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;
        void Restore(){WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;}
        Mobile enemy=null;
        try
        {
            Cases(p);WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
            enemy=MagicArrowChecks.Target(p);enemy.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);int health=SkillHealth.Balance256(enemy);
            Reset(p);Require(BladeFuryModes.BeginPrepared(p,Serial.Zero,enemy.Location,TimedEffects.Now),"Natural prepared mode start");
            Timer.StartTimer(TimeSpan.FromMilliseconds(1450),()=>
            {
                try
                {
                    Require(BladeFuryModes.State(p)==null&&SkillHealth.Balance256(enemy)<health&&Own(p).Length==0
                        &&NecromancerEffects.ManaBalance256(p)==1151,"Natural mode clock failed warmup/two paid blades/expiry/damage");
                    Reset(p);Require(BladeFuryModes.BeginPrepared(p,Serial.Zero,enemy.Location,TimedEffects.Now),"Offline mode boundary setup");int before=SkillHealth.Balance256(enemy);
#pragma warning disable CS0618
                    net.Dispose();
#pragma warning restore CS0618
                    Timer.StartTimer(TimeSpan.FromMilliseconds(250),()=>
                    {
                        try
                        {
                            Require(p.NetState==null&&BladeFuryModes.State(p)==null&&Own(p).Length==0
                                &&NecromancerEffects.ManaBalance256(p)==1663&&SkillHealth.Balance256(enemy)==before,"Disconnected prepared mode emitted or paid");
                            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,1663)));
                            Log.Information("blade-fury-mode-live-result: passed; real socket, natural server mode/weapon clocks, paid damage and offline no-action; character retained for reload, transient mode not serialized");
                        }
                        catch(Exception e){p.Delete();Log.Error(e,"blade-fury-mode-live-result: failed");}
                        finally{Restore();enemy.Delete();BladeFuryModes.Cancel(p);WeaponMissiles.Cancel(p);}
                    });
                }
                catch(Exception e){Restore();enemy.Delete();p.Delete();Log.Error(e,"blade-fury-mode-live-result: failed");}
            });
        }
        catch{Restore();enemy?.Delete();p.Delete();throw;}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        Require(p is{Alive:true,Deleted:false}&&NecromancerEffects.ManaBalance256(p)==saved.Mana256
            &&BladeFuryModes.State(p)==null&&Own(p).Length==0,"Saved character lost mana or replayed transient prepared mode");
        Log.Information("Prepared Fury mode reload: actual1663FP character preserved; no transient mode/intent replay or released-flight replay");
    }
}
