// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class MageChainChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MageChainChecks));
    private sealed record Saved(uint Owner,uint Mage,uint First,uint Second,int Life,int FirstLife,int SecondLife,ulong ActorSeed,Point3D Origin,ChainFlight Flight,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"mage-chain-fixtures.json");
    public static void Configure()
    {
        Register("mage-chain-check",Check);Register("mage-chain-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("mage-chain-live","Natural fixed10 Mage Chain Lightning proc and delayed hops",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("mage-chain-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Approved Mage hard10 Chain Lightning via shared saved player controller",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner(int rank=10)=>MagePetBuffChecks.Owner(rank,7000,3100);
    private static GraveyardSkeleton Enemy(PlayerMobile p,MageWorldBody mage,int dx)
    {var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(mage.X+dx,mage.Y,mage.Z),p.Map);t.SetHits(20000);t.Hits=t.HitsMax;t.SetResistance(ResistanceType.Energy,0);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static ChainFlight Flight(long id)=>ChainLightning.Snapshot().SingleOrDefault(f=>f.Cast==id);
    private static ChainFlight For(MageWorldBody mage)=>ChainLightning.Snapshot().SingleOrDefault(f=>f.NpcCaster==mage.Serial.Value);
    private static void Replace(ChainFlight f)=>ChainLightning.Restore(ChainLightning.Snapshot().Where(x=>x.Cast!=f.Cast).Append(f).ToArray());
    private static void Step(long id,bool active=true)
    {if(Flight(id) is{} f){ChainLightning.Tick(f.NextUtc,active,id);}}
    private static void Clear(MageWorldBody mage)
    {ChainLightning.Cancel(mage);var saved=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(saved with{Bolts=saved.Bolts.Where(x=>x.Actor!=mage.Serial.Value).ToArray()});}
    private static void Delivery()
    {
        var p=Owner(9);var mage=MagePetBuffChecks.Pet(p,3);NecroSnapshotChecks.MissingMetadata(mage);var first=Enemy(p,mage,4);var second=Enemy(p,mage,7);var oldSlow=AmazonSlowMissiles.Snapshot();var oldSeed=NecromancerMageMissiles.SeedLow;NecromancerMageMissiles.SeedLow=()=>73;
        try
        {
            var helm=PoisonDaggerChecks.Named(p,"Wormskull");Require(SkillMechanics.Rank(p,CombatAbility.RaiseSkeletalMage)==10&&MagePetBuffs.HardRank(mage)==9,"Chain hard/soft fixture");
            ulong seed=MageStaticFieldChecks.Seed(0,4,false);NecromancerMageMissiles.SetActorSeed(mage,seed);int hp=SkillHealth.Balance256(first);
            Require(mage.Launch(p,first,TimedEffects.Now)&&For(mage)==null&&NecromancerMageMissiles.ActorSeed(mage).Seed==seed,"Soft9+1 unlocked Chain or drew a proc");Clear(mage);p.Backpack.DropItem(helm);ClassGear.Refresh(p);MagePetBuffChecks.Train(p,"necromancer.skeletalmage",10);
            NecromancerMageMissiles.SetActorSeed(mage,seed);ulong after=seed;SourceRandom.Roll(ref after);int mana=NecromancerEffects.ManaBalance256(p),npcMana=mage.Mana;bool cast= mage.Launch(p,first,TimedEffects.Now);
            Require(cast&&For(mage) is{}&&NecromancerEffects.ManaBalance256(p)==mana&&mage.Mana==npcMana
                &&NecromancerMageMissiles.ActorSeed(mage).Seed==after&&!NecromancerMageMissiles.Snapshot().Bolts.Any(x=>x.Actor==mage.Serial.Value),$"Hard10 Chain cast={cast},hard={MagePetBuffs.HardRank(mage)},flight={For(mage)},map={mage.Map},mana={NecromancerEffects.ManaBalance256(p)}/{mana},npcMana={mage.Mana}/{npcMana},seed={NecromancerMageMissiles.ActorSeed(mage)?.Seed}/{after}");
            var f=For(mage);var values=MagePetBuffs.Values(CombatAbility.ChainLightning,10,mage.Level);
            Require(f.NpcCaster==mage.Serial.Value&&f.Owner==p.Serial.Value&&f.Rank==10&&f.Remaining==7&&f.Min256==values.Damage256(false)&&f.Max256==values.Damage256(true)
                &&f.X==mage.X&&f.Y==mage.Y&&f.Vx==.703125&&f.NpcSlow==SlowMissilePayload.Normal&&SkillHealth.Balance256(first)==hp,"NPC initial identity/fixed rank/source velocity/endpoints or immediate damage");
            long id=f.Cast;for(int i=0;i<24&&SkillHealth.Balance256(first)==hp;i++){Step(id);}
            var child=Flight(id);Require(SkillHealth.Balance256(first)<hp&&child is{Remaining:6}&&child.AimTarget==second.Serial.Value&&child.NpcCaster==f.NpcCaster
                &&child.Rank==10&&child.Min256==f.Min256&&child.Max256==f.Max256&&child.Delays.Any(x=>x.Target==first.Serial.Value),"Shared NPC hop/identity/delay/fixed child values");
            ulong critical=after;SourceRandom.Roll(ref critical);Require(NecromancerMageMissiles.ActorSeed(mage).Seed==critical,"Shared NPC intrinsic critical stream lost");
            int b=SkillHealth.Balance256(second);for(int i=0;i<200&&Flight(id)!=null;i++){Step(id);}
            Require(Flight(id)==null&&SkillHealth.Balance256(second)<b,"NPC chain failed delayed second impact/finite retirement");
            AmazonSlowMissiles.Restore(oldSlow.Append(new SlowMissilesState(mage.Serial.Value,p.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());
            id=ChainLightning.CastNpc(mage,first.Location,values,TimedEffects.Now);f=Flight(id);
            Require(f.Vx==.3515625&&f.NpcSlow==new SlowMissilePayload(50,50)&&f.Min256==values.Damage256(false)/2&&f.Max256==values.Damage256(true)/2,"NPC Chain did not reuse captured Slow Missiles velocity/damage");
            AmazonSlowMissiles.Restore(oldSlow);hp=SkillHealth.Balance256(first);for(int i=0;i<24&&SkillHealth.Balance256(first)==hp;i++){Step(id);}
            child=Flight(id);Require(child is{Remaining:6,NpcSlow.VelocityPercent:100,NpcSlow.DamagePercent:100}&&child.Min256==values.Damage256(false)&&child.Max256==values.Damage256(true),"NPC child did not recapture its own live slow state at fixed spell rank");Clear(mage);
            id=ChainLightning.CastNpc(mage,first.Location,values,TimedEffects.Now);f=Flight(id);hp=SkillHealth.Balance256(first);seed=NecromancerMageMissiles.ActorSeed(mage).Seed;
            for(int i=0;i<30&&Flight(id)!=null;i++){Step(id,false);}Require(Flight(id)==null&&SkillHealth.Balance256(first)==hp&&NecromancerMageMissiles.ActorSeed(mage).Seed==seed,"Offline NPC Chain traveled/hit/rolled or outlived deadline");
            id=ChainLightning.CastNpc(mage,first.Location,values,TimedEffects.Now);f=Flight(id);string before=JsonSerializer.Serialize(ChainLightning.Snapshot());bool invalid=false;
            try{Replace(f with{Rank=11});}catch(InvalidDataException){invalid=true;}Require(invalid&&JsonSerializer.Serialize(ChainLightning.Snapshot())==before,"Invalid NPC context damaged existing player/NPC flights");
            // Read a historical player DTO shape with no new optional fields.
            var legacy=f with{Cast=ChainLightning.Snapshot().Max(x=>x.Cast)+1,NpcCaster=0,NpcSeed=0,NpcSlow=SlowMissilePayload.Normal,Rank=1,Remaining=1};
            var fields=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(JsonSerializer.Serialize(legacy));fields.Remove("NpcCaster");fields.Remove("NpcSeed");fields.Remove("NpcSlow");
            var parsed=JsonSerializer.Deserialize<ChainFlight>(JsonSerializer.Serialize(fields));Require(parsed.NpcCaster==0&&parsed.NpcSeed==0&&parsed.NpcSlow==SlowMissilePayload.Normal,"Historical player JSON defaults changed");
            ChainLightning.Restore(ChainLightning.Snapshot().Append(parsed).ToArray());ChainLightning.Restore(ChainLightning.Snapshot().Where(x=>x.Cast!=parsed.Cast).ToArray());Clear(mage);
        }
        finally{AmazonSlowMissiles.Restore(oldSlow);NecromancerMageMissiles.SeedLow=oldSeed;Clear(mage);first.Delete();second.Delete();mage.Delete();p.Delete();}
    }
    private static void Check()
    {
        Delivery();var p=Owner();var mage=MagePetBuffChecks.Pet(p,3);var first=Enemy(p,mage,4);var second=Enemy(p,mage,7);var values=MagePetBuffs.Values(CombatAbility.ChainLightning,10,mage.Level);
        long id=ChainLightning.CastNpc(mage,first.Location,values,TimedEffects.Now);for(int i=0;i<3;i++){Step(id);}var f=Flight(id);Require(f is{Frame:3,Remaining:7}&&SkillHealth.Balance256(first)==20000*256,"Pre-hit saved NPC flight missing");
        var hold=TimedEffects.Now.AddHours(1);f=f with{NextUtc=hold,UntilUtc=hold.AddSeconds(1)};Replace(f);SkillHealth.SetBalance256(mage,SkillHealth.Maximum256(mage)-257);TimedEffects.PreventHealing(mage,TimedEffects.Now);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,mage.Serial.Value,first.Serial.Value,second.Serial.Value,SkillHealth.Balance256(mage),SkillHealth.Balance256(first),SkillHealth.Balance256(second),NecromancerMageMissiles.ActorSeed(mage).Seed,mage.Location,f,0)));
        Log.Information("Mage Chain:hard9+real soft1 refusal/hard10 fixed10,one actor proc draw/no mana/basic bolt,shared delayed identity hops/critical/finite budget,live child slow capture,offline expiry,atomic invalid restore and historical player JSON shape pass");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var mage=(MageWorldBody)World.FindMobile((Serial)s.Mage);var first=(GraveyardSkeleton)World.FindMobile((Serial)s.First);var second=(GraveyardSkeleton)World.FindMobile((Serial)s.Second);
        Require(mage.ControlMaster==p&&SkillHealth.Balance256(mage)==s.Life&&SkillHealth.Balance256(first)==s.FirstLife&&SkillHealth.Balance256(second)==s.SecondLife
            &&NecromancerMageMissiles.ActorSeed(mage).Seed==s.ActorSeed&&JsonSerializer.Serialize(Flight(s.Flight.Cast))==JsonSerializer.Serialize(s.Flight),"Saved NPC Chain identity/packet/phase/seed/wounds changed");
        // Offline player mobiles load internally. Establish the same real
        // caster/owner facet before allowing this controlled copied flight.
        p.MoveToWorld(new(s.Origin.X-3,s.Origin.Y,s.Origin.Z),Map.Maps[s.Flight.Map]);
        mage.MoveToWorld(s.Origin,Map.Maps[s.Flight.Map]);var copy=s.Flight with{Cast=ChainLightning.Snapshot().Max(x=>x.Cast)+1,NextUtc=TimedEffects.Now,UntilUtc=TimedEffects.Now.AddSeconds(1)};Replace(copy);
        int hp=SkillHealth.Balance256(first);for(int i=0;i<24&&SkillHealth.Balance256(first)==hp;i++){Step(copy.Cast);}
        Require(SkillHealth.Balance256(first)<hp&&Flight(copy.Cast) is{Rank:10,Remaining:6} child&&child.AimTarget==second.Serial.Value
            &&JsonSerializer.Serialize(Flight(s.Flight.Cast))==JsonSerializer.Serialize(s.Flight),$"Saved NPC copy HP={SkillHealth.Balance256(first)}/{hp},flight={Flight(copy.Cast)},ownerMap={p.Map},casterMap={mage.Map},active={PersistentMinions.Active(p,mage)},originalEqual={JsonSerializer.Serialize(Flight(s.Flight.Cast))==JsonSerializer.Serialize(s.Flight)}");
        ChainLightning.Restore(ChainLightning.Snapshot().Where(x=>x.Cast!=copy.Cast).ToArray());
        SkillHealth.SetBalance256(first,s.FirstLife);NecromancerMageMissiles.SetActorSeed(mage,s.ActorSeed);mage.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner();FixtureNetwork.Authenticate(network,p,"mage-chain");var mage=MagePetBuffChecks.Pet(p,3,future:false);var first=Enemy(p,mage,4);var second=Enemy(p,mage,7);NecromancerMageMissiles.SetActorSeed(mage,MageStaticFieldChecks.Seed(0,4));
        int mana=NecromancerEffects.ManaBalance256(p);bool launched=false;long deadline=Core.TickCount+7000;
        void End(){Clear(mage);first.Delete();second.Delete();mage.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline&&mage.WorldState.Controller.Fault==null,"Natural Mage Chain timeout/fault");
                if(!launched&&For(mage) is{} f){Require(mage.WorldState.Controller.Frame>=35&&f.Rank==10&&f.NpcCaster==mage.Serial.Value&&SkillHealth.Balance256(first)==20000*256,"Natural Chain immediate hit/wrong source/rank");launched=true;mage.SuspendActions();}
                if(SkillHealth.Balance256(second)==20000*256){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(launched&&SkillHealth.Balance256(first)<20000*256&&NecromancerEffects.ManaBalance256(p)==mana&&network.Running,"Natural Chain missed delayed hops or spent owner mana");End();Log.Information("mage-chain-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"mage-chain-live-result: failed");}
        }
        Log.Information("mage-chain-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
}
