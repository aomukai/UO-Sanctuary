// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;
public static class MageMeteorChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MageMeteorChecks));
    private sealed record Saved(uint Owner,uint WarningMage,uint FieldMage,uint First,uint Second,int WarningLife,int FieldLife,int FirstLife,int SecondLife,ulong WarningSeed,ulong FieldSeed,Point3D Origin,Point3D FieldOrigin,MeteorDrop Warning,FirePatch[] Fields,FireCooldown[] Cooldowns,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"mage-meteor-fixtures.json");
    public static void Configure()
    {
        Register("mage-meteor-check",Check);Register("mage-meteor-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("mage-meteor-live","Natural fixed10 NPC Meteor warning/impact/field",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("mage-meteor-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Approved Mage shared Meteor/cooldown/field/save checks",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner(int rank=10)=>MagePetBuffChecks.Owner(rank,6700,3000);
    private static GraveyardZombie Enemy(PlayerMobile p,Mobile caster,int dx=4,int dy=0)
    {var t=new GraveyardZombie{Name="PoisonDaggerFixtureMeteorTarget",NoKillAwards=true,Frozen=true,CantWalk=true};t.MoveToWorld(new(caster.X+dx,caster.Y+dy,caster.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(20000);t.Hits=t.HitsMax;t.SetResistance(ResistanceType.Fire,0);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static MeteorDrop Warning(Mobile mage)=>FireGround.Snapshot().Meteors.SingleOrDefault(x=>x.NpcCaster==mage.Serial.Value);
    private static FirePatch[] Fields(Mobile mage)=>FireGround.Snapshot().Patches.Where(x=>x.NpcCaster==mage.Serial.Value).OrderBy(x=>x.Id).ToArray();
    private static void Clear(Mobile mage)
    {FireGround.Cancel(mage);var all=FireGround.Snapshot();FireGround.Restore(all with{Cooldowns=all.Cooldowns.Where(x=>x.Owner!=mage.Serial.Value).ToArray()});var bolts=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(bolts with{Bolts=bolts.Bolts.Where(x=>x.Actor!=mage.Serial.Value).ToArray()});}
    private static long NextId(FireGroundSave s)=>s.Patches.Select(x=>x.Id).Concat(s.Meteors.Select(x=>x.Id)).Concat(s.Makers.Select(x=>x.Id)).DefaultIfEmpty().Max()+1;
    private static ulong NoCriticals(){for(ulong n=1;n<10000;n++){ulong seed=n;if(SourceRandom.Roll(ref seed)%100>=5&&SourceRandom.Roll(ref seed)%100>=5){return n;}}throw new InvalidOperationException("Meteor critical control");}
    private static void Source()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"mage-meteor-source-probe.json")));int count=0;
        foreach(var row in source.RootElement.EnumerateArray())
        {
            if(row[8].GetInt32()>0)
            {
                int rank=row[1].GetInt32(),length=rank>0?30+15*(rank-1):0;var fields=row[12].EnumerateArray().ToArray();var offsets=FireGround.MeteorOffsets();Require(fields.Length==offsets.Length,"Source Meteor offset count");
                for(int i=0;i<fields.Length;i++){Require(fields[i][1].GetInt32()==40+offsets[i].X&&fields[i][2].GetInt32()==70+offsets[i].Y&&fields[i][4].GetInt32()==length,"Shared Meteor source position/lifetime");}
            }
            count++;
        }
        Require(count==320&&MartialPhoenixPackets.MeteorOffsets().SequenceEqual(FireGround.MeteorOffsets()),"Normal/Phoenix Meteor producer reuse");
        Log.Information("Mage Meteor:320 complete normal-center source expiry/hit14/area/field contexts and shared Phoenix offset consumer pass");
    }
    private static void Delivery()
    {
        var p=Owner(9);var mage=MagePetBuffChecks.Pet(p,2);NecroSnapshotChecks.MissingMetadata(mage);var first=Enemy(p,mage);var peer=Enemy(p,mage,4,1);var at=TimedEffects.Now;
        try
        {
            var helm=PoisonDaggerChecks.Named(p,"Wormskull");ulong seed=MageStaticFieldChecks.Seed(0,4,false);NecromancerMageMissiles.SetActorSeed(mage,seed);
            Require(SkillMechanics.Rank(p,CombatAbility.RaiseSkeletalMage)==10&&MagePetBuffs.HardRank(mage)==9&&mage.Launch(p,first,at)&&Warning(mage)==null&&NecromancerMageMissiles.ActorSeed(mage).Seed==seed,"Soft point unlocked/drew Meteor");Clear(mage);p.Backpack.DropItem(helm);ClassGear.Refresh(p);MagePetBuffChecks.Train(p,"necromancer.skeletalmage",10);
            NecromancerMageMissiles.SetActorSeed(mage,seed);ulong after=seed;SourceRandom.Roll(ref after);int mana=NecromancerEffects.ManaBalance256(p),npcMana=mage.Mana,hp=SkillHealth.Balance256(first);
            Require(mage.Launch(p,first,at)&&NecromancerMageMissiles.ActorSeed(mage).Seed==after&&NecromancerEffects.ManaBalance256(p)==mana&&mage.Mana==npcMana&&!NecromancerMageMissiles.Snapshot().Bolts.Any(x=>x.Actor==mage.Serial.Value),"Meteor proc/payment/basic bolt");
            var warning=Warning(mage);var values=MagePetBuffs.Values(CombatAbility.Meteor,10,mage.Level);
            Require(warning is{Rank:10,Radius:3}&&warning.Min256==values.Damage256(false)&&warning.Max256==values.Damage256(true)&&warning.ImpactUtc==at.AddMilliseconds(2400)&&warning.Owner==p.Serial.Value&&warning.NpcCaster==mage.Serial.Value,"Fixed10 Meteor warning packet/source/deadline");
            Require(!FireGround.Ready(mage,CombatAbility.Meteor,at.AddMilliseconds(1199))&&FireGround.Ready(mage,CombatAbility.Meteor,at.AddMilliseconds(1200))&&FireGround.Ready(p,CombatAbility.Meteor,at),"NPC30frame cooldown affected owner or boundary");
            seed=NecromancerMageMissiles.ActorSeed(mage).Seed;Require(mage.Launch(p,first,at.AddMilliseconds(100))&&NecromancerMageMissiles.ActorSeed(mage).Seed==seed&&Warning(mage)==warning,"Unavailable Meteor rerolled/doubled instead of basic");var bolts=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(bolts with{Bolts=bolts.Bolts.Where(x=>x.Actor!=mage.Serial.Value).ToArray()});
            FireGround.Tick(warning.ImpactUtc.AddTicks(-1),true,mage.Serial.Value);Require(SkillHealth.Balance256(first)==hp&&Fields(mage).Length==0,"Meteor hit before60frames");
            seed=NoCriticals();NecromancerMageMissiles.SetActorSeed(mage,seed);FireGround.Tick(warning.ImpactUtc,true,mage.Serial.Value);
            int loss=hp-SkillHealth.Balance256(first);var fields=Fields(mage);Require(loss>0&&hp-SkillHealth.Balance256(peer)==loss&&Warning(mage)==null&&fields.Length==18,"One Meteor raw area packet or18fields");
            var offsets=FireGround.MeteorOffsets();for(int i=0;i<fields.Length;i++){Require(fields[i].X==warning.X+offsets[i].X/2.0&&fields[i].Y==warning.Y+offsets[i].Y/2.0&&fields[i].Min256==FireGround.MeteorFireDamage256(values,false)&&fields[i].Max256==FireGround.MeteorFireDamage256(values,true)&&fields[i].SkillRank==10&&fields[i].Size==2&&fields[i].DamageRate==41&&fields[i].NextUtc==warning.ImpactUtc.AddMilliseconds(40)&&fields[i].UntilUtc==warning.ImpactUtc.AddMilliseconds(6600),"Meteor fixed10 field geometry/packet/clock");}
            ulong critical=seed;SourceRandom.Roll(ref critical);SourceRandom.Roll(ref critical);Require(NecromancerMageMissiles.ActorSeed(mage).Seed==critical,"Meteor area recipient critical stream");
            var single=fields.Single(x=>x.X==warning.X+.5&&x.Y==warning.Y+.5) with{Min256=64,Max256=64};var all=FireGround.Snapshot();FireGround.Restore(all with{Patches=all.Patches.Where(x=>x.NpcCaster!=mage.Serial.Value).Append(single).ToArray()});
            int a=SkillHealth.Balance256(first),b=SkillHealth.Balance256(peer);critical=NoCriticals();NecromancerMageMissiles.SetActorSeed(mage,critical);SourceRandom.Roll(ref critical);CreatureProfiles.Session(first).RecoveryUntil=CreatureProfiles.Session(peer).RecoveryUntil=Core.TickCount-1;seed=single.Seed;SourceRandom.Limited(ref seed,128);FireGround.Tick(single.NextUtc,true,mage.Serial.Value);var updated=Fields(mage).Single();
            Require(a+b-SkillHealth.Balance256(first)-SkillHealth.Balance256(peer)==64&&(a==SkillHealth.Balance256(first)||b==SkillHealth.Balance256(peer))&&updated.Seed==seed&&NecromancerMageMissiles.ActorSeed(mage).Seed==critical,"Single field failed one-recipient fractional damage/soft and actor draws");
            ulong criticalStart=0;for(ulong n=1;n<10000;n++){ulong candidate=n;if(SourceRandom.Roll(ref candidate)%100<5){criticalStart=n;critical=candidate;break;}}Require(criticalStart!=0,"Forced field critical control");NecromancerMageMissiles.SetActorSeed(mage,criticalStart);a=SkillHealth.Balance256(first);b=SkillHealth.Balance256(peer);FireGround.Tick(updated.NextUtc,true,mage.Serial.Value);updated=Fields(mage).Single();Require(a+b-SkillHealth.Balance256(first)-SkillHealth.Balance256(peer)==128&&NecromancerMageMissiles.ActorSeed(mage).Seed==critical&&CreatureHitRecovery.Remaining(first)==0&&CreatureHitRecovery.Remaining(peer)==0,"NPC field critical or no-recovery contract");
            first.SetResistance(ResistanceType.Fire,100);peer.SetResistance(ResistanceType.Fire,100);a=SkillHealth.Balance256(first);b=SkillHealth.Balance256(peer);FireGround.Tick(updated.NextUtc,true,mage.Serial.Value);Require(SkillHealth.Balance256(first)==a&&SkillHealth.Balance256(peer)==b,"NPC fire-immune field recipient");first.SetResistance(ResistanceType.Fire,0);peer.SetResistance(ResistanceType.Fire,0);updated=Fields(mage).Single();SourceRandom.Roll(ref critical);Require(NecromancerMageMissiles.ActorSeed(mage).Seed==critical,"Immune field omitted NPC critical draw");
            a=SkillHealth.Balance256(first);b=SkillHealth.Balance256(peer);FireGround.Tick(updated.UntilUtc.AddSeconds(1),false,mage.Serial.Value);Require(Fields(mage).Length==0&&a==SkillHealth.Balance256(first)&&b==SkillHealth.Balance256(peer)&&NecromancerMageMissiles.ActorSeed(mage).Seed==critical,"Offline field replay/damage/critical or lifetime extension");Clear(mage);
            // A moving target does not drag the captured ground warning.
            long id=FireGround.CastNpcMeteor(mage,first,at);warning=Warning(mage);first.MoveToWorld(new(mage.X+12,mage.Y,mage.Z),p.Map);a=SkillHealth.Balance256(first);b=SkillHealth.Balance256(peer);FireGround.Tick(warning.ImpactUtc,true,mage.Serial.Value);Require(id>0&&a==SkillHealth.Balance256(first)&&SkillHealth.Balance256(peer)<b,"Meteor homed on a moving target");Clear(mage);
            FireGround.CastNpcMeteor(mage,peer,at);warning=Warning(mage);a=SkillHealth.Balance256(peer);seed=NecromancerMageMissiles.ActorSeed(mage).Seed;FireGround.Tick(warning.ImpactUtc,false,mage.Serial.Value);Require(Warning(mage)==null&&Fields(mage).Length==0&&SkillHealth.Balance256(peer)==a&&NecromancerMageMissiles.ActorSeed(mage).Seed==seed,"Offline warning replayed impact");Clear(mage);
            FireGround.CastNpcMeteor(mage,peer,at);warning=Warning(mage);string before=JsonSerializer.Serialize(FireGround.Snapshot());bool invalid=false;all=FireGround.Snapshot();try{FireGround.Restore(all with{Meteors=all.Meteors.Select(x=>x.Id==warning.Id?x with{Rank=11}:x).ToArray()});}catch(InvalidDataException){invalid=true;}Require(invalid&&JsonSerializer.Serialize(FireGround.Snapshot())==before,"Invalid NPC Meteor erased shared player/field/cooldown state");
            var json=JsonSerializer.SerializeToNode(warning);json.AsObject().Remove("NpcCaster");json.AsObject().Remove("Seed");Require(json.Deserialize<MeteorDrop>() is{NpcCaster:0,Seed:0},"Historical player Meteor defaults");
            json=JsonSerializer.SerializeToNode(single);json.AsObject().Remove("NpcCaster");json.AsObject().Remove("Seed");Require(json.Deserialize<FirePatch>() is{NpcCaster:0,Seed:0},"Historical player field defaults");
        }
        finally{Clear(mage);first.Delete();peer.Delete();mage.Delete();p.Delete();}
        Log.Information("Mage Meteor:hard9+soft1 refusal/hard10 fixed10,one proc/no mana/basic replacement,per-caster30frame delay,60frame ground warning,shared raw impact/18fields,165frame field/one collider/FP damage/critical/seed,offline expiry,atomic restore/historical defaults pass");
    }
    private static void Check()
    {
        Source();Delivery();var p=Owner();var warningMage=MagePetBuffChecks.Pet(p,2);var fieldMage=MagePetBuffChecks.Pet(p,2,5);var first=Enemy(p,warningMage);var second=Enemy(p,fieldMage);var at=TimedEffects.Now;
        Require(FireGround.CastNpcMeteor(warningMage,first,at)>0&&FireGround.CastNpcMeteor(fieldMage,second,at)>0,"Independent Mage cooldowns");FireGround.Tick(Warning(fieldMage).ImpactUtc,true,fieldMage.Serial.Value);
        var hold=TimedEffects.Now.AddHours(1);var warning=Warning(warningMage) with{ImpactUtc=hold};var fields=Fields(fieldMage).Select(x=>x with{NextUtc=hold,UntilUtc=hold.AddMilliseconds(6560)}).ToArray();var all=FireGround.Snapshot();
        FireGround.Restore(all with{Meteors=all.Meteors.Select(x=>x.Id==warning.Id?warning:x).ToArray(),Patches=all.Patches.Select(x=>fields.FirstOrDefault(f=>f.Id==x.Id)??x).ToArray(),Cooldowns=all.Cooldowns.Select(x=>x.Owner==warningMage.Serial.Value||x.Owner==fieldMage.Serial.Value?x with{UntilUtc=hold.AddSeconds(1)}:x).ToArray()});
        foreach(var mage in new[]{warningMage,fieldMage}){SkillHealth.SetBalance256(mage,SkillHealth.Maximum256(mage)-257);TimedEffects.PreventHealing(mage,TimedEffects.Now);}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,warningMage.Serial.Value,fieldMage.Serial.Value,first.Serial.Value,second.Serial.Value,SkillHealth.Balance256(warningMage),SkillHealth.Balance256(fieldMage),SkillHealth.Balance256(first),SkillHealth.Balance256(second),NecromancerMageMissiles.ActorSeed(warningMage).Seed,NecromancerMageMissiles.ActorSeed(fieldMage).Seed,warningMage.Location,fieldMage.Location,warning,fields,FireGround.Snapshot().Cooldowns.Where(x=>x.Owner==warningMage.Serial.Value||x.Owner==fieldMage.Serial.Value).ToArray(),0)));
    }
    private static void Reload()
    {
        Source();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var a=(MageWorldBody)World.FindMobile((Serial)s.WarningMage);var b=(MageWorldBody)World.FindMobile((Serial)s.FieldMage);var first=World.FindMobile((Serial)s.First);var second=World.FindMobile((Serial)s.Second);
        Require(a.ControlMaster==p&&b.ControlMaster==p&&SkillHealth.Balance256(a)==s.WarningLife&&SkillHealth.Balance256(b)==s.FieldLife&&SkillHealth.Balance256(first)==s.FirstLife&&SkillHealth.Balance256(second)==s.SecondLife&&NecromancerMageMissiles.ActorSeed(a).Seed==s.WarningSeed&&NecromancerMageMissiles.ActorSeed(b).Seed==s.FieldSeed&&Warning(a)==s.Warning&&Fields(b).SequenceEqual(s.Fields)&&FireGround.Snapshot().Cooldowns.Where(x=>x.Owner==a.Serial.Value||x.Owner==b.Serial.Value).SequenceEqual(s.Cooldowns),"Saved Meteor warning/fields/owner/wounds/seeds/cooldowns changed");
        p.MoveToWorld(new(s.Origin.X-3,s.Origin.Y,s.Origin.Z),Map.Maps[s.Warning.Map]);a.MoveToWorld(s.Origin,p.Map);b.MoveToWorld(s.FieldOrigin,p.Map);
        var all=FireGround.Snapshot();var copy=s.Warning with{Id=NextId(all),ImpactUtc=TimedEffects.Now};FireGround.Restore(all with{Meteors=all.Meteors.Append(copy).ToArray()});FireGround.Tick(copy.ImpactUtc,true,a.Serial.Value);Require(SkillHealth.Balance256(first)<s.FirstLife&&Fields(a).Length==18&&Warning(a)==s.Warning,"Copied warning did not resume independently");FireGround.Cancel(a);all=FireGround.Snapshot();FireGround.Restore(all with{Meteors=all.Meteors.Append(s.Warning).ToArray()});
        all=FireGround.Snapshot();var field=s.Fields.Single(x=>x.X==second.X+.5&&x.Y==second.Y+.5) with{Id=NextId(all),NextUtc=TimedEffects.Now,UntilUtc=TimedEffects.Now.AddSeconds(1)};FireGround.Restore(all with{Patches=all.Patches.Append(field).ToArray()});int hp=SkillHealth.Balance256(second);FireGround.Tick(field.NextUtc,true,b.Serial.Value);Require(SkillHealth.Balance256(second)<hp&&Fields(b).Where(x=>x.Id!=field.Id).SequenceEqual(s.Fields),"Copied saved field did not resume without advancing held originals");all=FireGround.Snapshot();FireGround.Restore(all with{Patches=all.Patches.Where(x=>x.Id!=field.Id).ToArray()});
        SkillHealth.SetBalance256(first,s.FirstLife);SkillHealth.SetBalance256(second,s.SecondLife);NecromancerMageMissiles.SetActorSeed(a,s.WarningSeed);NecromancerMageMissiles.SetActorSeed(b,s.FieldSeed);a.Internalize();b.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=MagePetBuffChecks.Owner(10,6700,3400);FixtureNetwork.Authenticate(network,p,"meteor");var mage=MagePetBuffChecks.Pet(p,2,future:false);var first=Enemy(p,mage);NecromancerMageMissiles.SetActorSeed(mage,MageStaticFieldChecks.Seed(0,4));int mana=NecromancerEffects.ManaBalance256(p),impactLife=0;MeteorDrop seen=null;bool impacted=false;long deadline=Core.TickCount+8000;
        void End(){Clear(mage);first.Delete();mage.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline&&mage.WorldState.Controller.Fault==null,"Natural Mage Meteor timeout/fault");
                if(seen==null&&Warning(mage) is{} warning){seen=warning;Require(mage.WorldState.Controller.Frame>=35&&warning.Rank==10&&SkillHealth.Balance256(first)==20000*256,"Natural warning source or early damage");mage.SuspendActions();mage.Ground.Store(mage.WorldState.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});}
                if(!impacted&&Fields(mage).Length>0){Require(seen!=null&&TimedEffects.Now>=seen.ImpactUtc&&SkillHealth.Balance256(first)<20000*256,"Natural Meteor warning/impact");impacted=true;impactLife=SkillHealth.Balance256(first);}
                if(!impacted||SkillHealth.Balance256(first)==impactLife){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Wait);return;}
                Require(network.Running&&NecromancerEffects.ManaBalance256(p)==mana&&Fields(mage).All(x=>x.SkillRank==10&&x.Owner==p.Serial.Value),"Natural field/payment/source");End();Log.Information("mage-meteor-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"mage-meteor-live-result: failed");}
        }
        Log.Information("mage-meteor-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Wait);
    }
}
