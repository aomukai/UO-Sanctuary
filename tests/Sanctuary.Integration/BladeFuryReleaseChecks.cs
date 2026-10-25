// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class BladeFuryReleaseChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeFuryReleaseChecks));
    private sealed record Saved(uint Owner,int Mana256,BladeFuryChannelState Channel);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blade-fury-release-fixture.json");
    private static bool _creating;
    private static void Rates()
    {
        var old=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>_creating||p.Name=="BladeFuryReleaseFixture"?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;
    }
    public static void Configure()
    {
        Rates();
        Register("blade-fury-release-check",()=>{Differential();WorldCase();});
        Register("blade-fury-release-reload-check",()=>{Differential();Reload();});
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,
        "Blade Fury distinct stop-bit/lease and ordinary resources; no learned input",_=>Core.LoopContext.Post(()=>
        {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blade-fury-release-source-probe.json")));
        int count=0,releasedLists=0,noRewinds=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();bool B(int i)=>N(i)!=0;
            int mana=N(3),emits=0,made=0,paid=0,rewinds=0,allocs=0;string order="";
            var state=new BladeFuryChannelState(N(1)==1,N(1)==0?0:1020,N(2),192){Lease=N(1)!=0};
            // The native API consumes an already validated stop intent, not
            // the older game's wire packet. Invalid size is a source control.
            if(N(7)==1){state=BladeFuryChannel.Release(state);}
            Require(N(8)==(N(7)==1?0:3)&&state.Active==B(9)&&state.HasLease==B(10)
                &&state.Flags==192&&state.NextShot==N(2),"Source stop changed lease/bit/deadline/flags "+count);
            if(state.HasLease&&!state.Active){releasedLists++;}
            var services=new BladeFuryResourceServices(()=>mana,cost=>
            {if(mana<cost){return false;}mana-=cost;paid++;order+="M";return true;},()=>{allocs++;return true;},
                ()=>B(5),()=>{emits++;order+="E";if(B(6)){made++;}return B(6);},()=>rewinds++);
            var start=BladeFuryResources.Start(state,1000,N(0),services);int corrected=N(0)==1?256:0;
            Require(start.Success==B(11)&&mana==N(12)-N(13)*corrected&&emits==N(13)&&paid==N(14),"Released start/resource differential "+count);
            mana=N(4);var end=start;int result=-1;
            if(start.Success){end=BladeFuryResources.Do(start.State,1000,N(0),services);result=end.Success?1:0;}
            Require(result==N(15)&&mana==N(16)-(N(17)-N(13))*corrected&&emits==N(17)&&made==N(18)&&paid==N(19)
                &&end.State.Active==B(20)&&end.State.HasLease==B(21)&&(!end.State.HasLease||end.State.Expires==N(22))
                &&end.State.NextShot==N(23)&&rewinds==N(24)&&allocs==N(25)&&end.State.Flags==row[26].GetUInt32()
                &&order==row[27].GetString(),"Complete released list/start/do/resource differential "+count);
            if(emits>0&&rewinds==0){noRewinds++;}
            var restored=JsonSerializer.Deserialize<BladeFuryChannelState>(JsonSerializer.Serialize(end.State));
            Require(restored==end.State&&restored.HasLease==end.State.HasLease,"Distinct release/list component JSON");count++;
        }
        Require(count==5400&&releasedLists>0&&noRewinds>0,"Release/list/source branch inventory");
        var old=JsonSerializer.Deserialize<BladeFuryChannelState>("{\"Active\":true,\"Expires\":1020,\"NextShot\":1004,\"Flags\":192}");
        Require(old.Lease==null&&old.HasLease&&BladeFuryChannel.Release(old) is{Active:false,Lease:true,Expires:1020,NextShot:1004,Flags:192},"Old coincident component JSON compatibility");
        var expired=BladeFuryChannel.Expire(BladeFuryChannel.Release(old));
        Require(!expired.Active&&!expired.HasLease&&expired.Expires==0&&expired.Flags==192&&expired.NextShot==1004,"Stop incorrectly substituted expiry or reset skill deadline");
        Require(!AssassinCombat.Implemented(CombatAbility.BladeFury),"Release component exposed learned input");
        Log.Information("Blade Fury release:5400 complete stop/state-bit/list-walker/resource compositions match; retained released lease, no rewind without bit and current rank1 debit remain distinct from expiry; no client timing certificate");
    }
    private static void WorldCase()
    {
        PlayerMobile p;_creating=true;try{p=BladeBodyChecks.Fresh(6760,25);}finally{_creating=false;}
        p.Name="BladeFuryReleaseFixture";bool keep=false;
        var hit=WeaponMissiles.HitRoll;var block=SourceBlocking.BlockRoll;var roll=WeaponMissiles.DamageRoll;
        try
        {
            Require(SkillMechanics.Rank(p,CombatAbility.BladeFury)==1,"Actual learned rank");
            Require(Mobile.GetManaRegenRate(p)==TimeSpan.FromHours(1),"Release fixture regeneration policy");
            NecromancerEffects.SetMana256(p,1663);int frame=1000,rewinds=0,allocs=0;
            var aim=new Point3D(p.X+15,p.Y,p.Z);
            var services=BladeFuryResources.Player(p,()=>{allocs++;return true;},()=>true,
                ()=>BladeFuryMissiles.Launch(p,aim,1,new Random(117))>0,()=>rewinds++);
            var start=BladeFuryResources.Start(new(false,0,0,128),frame,1,services);
            Require(start.Success&&start.State.HasLease&&start.State.Active&&allocs==1&&NecromancerEffects.ManaBalance256(p)==1663,"Fresh state before stop");
            var state=BladeFuryChannel.Release(start.State);
            frame=1012;var shot=BladeFuryResources.Do(state,frame,1,services);state=shot.State;
            Require(shot.Success&&!state.Active&&state.HasLease&&state.Expires==1021&&state.NextShot==1016
                &&rewinds==0&&NecromancerEffects.ManaBalance256(p)==1407,"Released pending do suppressed/rewound or paid incorrectly");
            var flights=WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();
            Require(flights.Length==1&&flights[0].Ability==CombatAbility.BladeFury,"Release-to-world factory path");
            // A real travelling, paid missile remains independently owned.
            var target=MagicArrowChecks.Target(p);target.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);
            target.SetResistance(ResistanceType.Physical,0);int health=SkillHealth.Balance256(target);
            WeaponMissiles.HitRoll=()=>0;SourceBlocking.BlockRoll=_=>99;WeaponMissiles.DamageRoll=_=>0;
            int guard=50;while(WeaponMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value)&&guard-->0)
            {WeaponMissiles.Tick(WeaponMissiles.Snapshot().Flights.Single(x=>x.Owner==p.Serial.Value).NextUtc);}
            Require(guard>0&&SkillHealth.Balance256(target)<health&&NecromancerEffects.ManaBalance256(p)==1407,"Released blade failed real damage or charged twice");target.Delete();
            NecromancerEffects.SetMana256(p,607);frame=1013;
            var renewed=BladeFuryResources.Start(state,frame,1,services);state=renewed.State;
            Require(renewed.Success&&!state.Active&&state.HasLease&&state.Expires==1020&&state.NextShot==1016
                &&allocs==1&&rewinds==1&&NecromancerEffects.ManaBalance256(p)==607,"Retained list reactivated/reallocated/charged or required six mana");
            var expired=BladeFuryChannel.Expire(state);
            Require(!BladeFuryResources.Start(expired,1021,1,services).Success&&NecromancerEffects.ManaBalance256(p)==607,"Expired list bypassed fresh six-mana threshold");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,607,state)));keep=true;
            Log.Information("Blade Fury released player: pending do paid256FP once and launched real damaging flight without rewind; retained list renewed below six mana without reactivation/allocation;607FP owner saved with released component fixture");
        }
        finally
        {
            WeaponMissiles.HitRoll=hit;SourceBlocking.BlockRoll=block;WeaponMissiles.DamageRoll=roll;
            WeaponMissiles.Cancel(p);if(!keep){p.Delete();}
        }
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(p is{Alive:true,Deleted:false}&&NecromancerEffects.ManaBalance256(p)==saved.Mana256,
            $"Actual released-owner fractional mana reload: owner={saved.Owner} exists={p!=null} alive={p?.Alive} deleted={p?.Deleted} name={p?.Name} mana={(p==null?-1:NecromancerEffects.ManaBalance256(p))}/{saved.Mana256} regen={(p==null?TimeSpan.Zero:Mobile.GetManaRegenRate(p))}");
        Require(saved.Channel is{Active:false,Lease:true,Expires:1020,NextShot:1016}&&saved.Channel.HasLease,"Released component fixture lost list/bit distinction");
        Require(!WeaponMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value),"Released completed flight replayed on reload");
        int rewinds=0;var services=BladeFuryResources.Player(p,()=>throw new InvalidOperationException("Retained release allocated"),()=>true,()=>true,()=>rewinds++);
        var renewed=BladeFuryResources.Start(saved.Channel,1014,1,services);
        Require(renewed.Success&&!renewed.State.Active&&renewed.State.HasLease&&renewed.State.Expires==1021&&rewinds==1
            &&NecromancerEffects.ManaBalance256(p)==607,"Reloaded component was confused with expiry/fresh allocation");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Channel=renewed.State with{Expires=1020}}));
        Log.Information("Blade Fury release reload: actual607FP character retained; explicit released component fixture resumes without reactivation/payment; production held mode is not persisted by this test");
    }
}
