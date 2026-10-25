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
public static class PhoenixReleaseChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PhoenixReleaseChecks));
    private sealed class Maximum:Random{public override int Next(int max)=>Math.Max(0,max-1);}
    private sealed record Saved(uint Owner,uint Target,Point3D Origin,PhoenixSave Projectiles,FirePatch[] Fields,int Mana256,int Life256);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"phoenix-release-fixtures.json");
    private static bool Creating;
    private static void Rates(){var hp=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=p=>Creating || p.Name=="PhoenixReleaseFixture"?TimeSpan.FromHours(1):hp?.Invoke(p)??Mobile.DefaultHitsRate;var mp=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>Creating || p.Name=="PhoenixReleaseFixture"?TimeSpan.FromHours(1):mp?.Invoke(p)??Mobile.DefaultManaRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    public static void Configure(){Rates();Register("phoenix-release-check",Check);Register("phoenix-release-reload-check",Reload);Register("phoenix-natural-check",Natural);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Phoenix saved release/world component; no learned input separate",_=>Core.LoopContext.Post(()=>{try{Controlled(action);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Controlled(Action action){var seed=MartialPhoenix.SeedLow;var roll=MartialPhoenix.DamageRoll;try{MartialPhoenix.SeedLow=()=>7;MartialPhoenix.DamageRoll=_=>0;action();}finally{MartialPhoenix.SeedLow=seed;MartialPhoenix.DamageRoll=roll;}}
    private static PlayerMobile Fresh(){Creating=true;try{var p=BladeBodyChecks.Fresh(6760,40,3050);p.Name="PhoenixReleaseFixture";while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Str");}while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dex");}return p;}finally{Creating=false;}}
    private static BaseCreature Target(PlayerMobile p,int x=1){var t=new CobraPassiveTarget{Name="PhoenixReleaseTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+x,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(type,0);}return t;}
    private static MartialCharge Charge(PlayerMobile p,int count)=>new(p.Serial.Value,CombatAbility.PhoenixStrike,count,SkillMechanics.Rank(p,CombatAbility.PhoenixStrike),count*25,TimedEffects.Now.AddHours(1));
    private static PhoenixSave Own(PlayerMobile p){var s=MartialPhoenix.Snapshot();var flights=s.Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();var casts=flights.Select(x=>x.Cast).ToHashSet();return s with{Drops=s.Drops.Where(x=>x.Owner==p.Serial.Value).ToArray(),Flights=flights,Leases=s.Leases.Where(x=>casts.Contains(x.Cast)).ToArray()};}
    private static void Replace(PlayerMobile p,PhoenixSave own)
    {var all=MartialPhoenix.Snapshot();var foreign=all.Flights.Where(x=>x.Owner!=p.Serial.Value).ToArray();var casts=foreign.Select(x=>x.Cast).ToHashSet();MartialPhoenix.Restore(all with{Drops=all.Drops.Where(x=>x.Owner!=p.Serial.Value).Concat(own.Drops).OrderBy(x=>x.Id).ToArray(),Flights=foreign.Concat(own.Flights).OrderBy(x=>x.Id).ToArray(),Leases=all.Leases.Where(x=>casts.Contains(x.Cast)).Concat(own.Leases).OrderBy(x=>x.Cast).ThenBy(x=>x.Target).ToArray(),NextId=Math.Max(all.NextId,own.NextId),NextCast=Math.Max(all.NextCast,own.NextCast)});}
    private static void Clear(PlayerMobile p)=>Replace(p,Own(p) with{Drops=[],Flights=[],Leases=[]});
    private static void Tick(PlayerMobile p,DateTime at,bool active=true){var all=MartialPhoenix.Snapshot();var own=Own(p);var after=own;MartialPhoenix.Restore(own);try{MartialPhoenix.Tick(at,active);after=MartialPhoenix.Snapshot();}finally{MartialPhoenix.Restore(all);Replace(p,after);}}
    private static FirePatch[] Fields(PlayerMobile p)=>FireGround.Snapshot().Patches.Where(x=>x.Owner==p.Serial.Value && x.Ability==CombatAbility.PhoenixStrike).OrderBy(x=>x.Id).ToArray();
    private static void ReplaceFields(PlayerMobile p,FirePatch[] own){var all=FireGround.Snapshot();FireGround.Restore(all with{Patches=all.Patches.Where(x=>x.Owner!=p.Serial.Value || x.Ability!=CombatAbility.PhoenixStrike).Concat(own).ToArray()});}
    private static void FireTick(PlayerMobile p,DateTime at,bool active=true)
    {var all=FireGround.Snapshot();var own=Fields(p);var after=own;FireGround.Restore(new(own,[],[],[]));try{FireGround.Tick(at,active);after=Fields(p);}finally{FireGround.Restore(all);ReplaceFields(p,after);}}
    private static void Emit(PlayerMobile p,BaseCreature t,int count,DateTime at)=>Require(MartialPhoenix.AfterParent(p,t,t.Location,p.Map,Charge(p,count),at)>0,"Phoenix release factory");
    private static void Meteors(PlayerMobile p,BaseCreature t)
    {
        Clear(p);ReplaceFields(p,[]);
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Maximum(),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Actual meteor rank gear");ClassGear.Refresh(p);
        int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t);var at=TimedEffects.Now;Emit(p,t,1,at);int initialRank=SkillMechanics.Rank(p,CombatAbility.PhoenixStrike);Require(Own(p).Drops.Length==1 && Own(p).Flights.Length==0 && Fields(p).Length==0,"Tier1 emitted other tiers");
        Tick(p,at.AddMilliseconds(2360));Require(Own(p).Drops.Single().Frame==59 && SkillHealth.Balance256(t)==hp && Fields(p).Length==0,"Meteor warned too early");
        p.AddToBackpack(helm);ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,CombatAbility.PhoenixStrike)<initialRank && NamedElements.MasteryPercent(p,"fire")==0,"Removed actual warning-time gear");
        var drop=Own(p).Drops.Single();Require(drop.Rank==initialRank,"Warning retained rank");var values=SkillMechanics.Values(p,CombatAbility.PhoenixStrike,drop.Rank);t.SetResistance(ResistanceType.Fire,50);int expected=NamedElements.Resolve256(p,t,MartialPhoenixPackets.Capture("royalstrikemeteor",values,NamedElements.MasteryPercent(p,"fire")).Minimum256,"fire",enhance:false).Damage256;
        Tick(p,at.AddMilliseconds(2400));Require(Own(p).Drops.Length==0 && SkillHealth.Balance256(t)==hp-expected && Fields(p).Length==18 && NecromancerEffects.ManaBalance256(p)==mana,"Meteor impact/18fields/no release debit");
        t.SetResistance(ResistanceType.Fire,0);var fields=Fields(p);Require(fields.All(x=>x.Size==2 && x.DamageRate==0 && x.SkillRank==drop.Rank && x.UntilUtc-x.NextUtc==TimeSpan.FromMilliseconds((MartialPhoenixPackets.MeteorFieldFrames(drop.Rank)-1)*40L)),"Source field rank/size/lifetime");
        var first=fields[0];t.MoveToWorld(new((int)first.X,(int)first.Y,first.Z),p.Map);var twin=Target(p,8);twin.MoveToWorld(t.Location,p.Map);ReplaceFields(p,[first]);int before=SkillHealth.Balance256(t)+SkillHealth.Balance256(twin);var roll=WeaponMissiles.DamageRoll;var soft=ImmolationArrows.SoftHitRoll;
        try{WeaponMissiles.DamageRoll=_=>0;ImmolationArrows.SoftHitRoll=_=>throw new InvalidOperationException("Phoenix fire has no source damage3 RNG");FireTick(p,first.NextUtc);}finally{WeaponMissiles.DamageRoll=roll;ImmolationArrows.SoftHitRoll=soft;}
        Require(before-SkillHealth.Balance256(t)-SkillHealth.Balance256(twin)==first.Min256,"A field hit more than one actor or wrong fixed-point damage");twin.Delete();ReplaceFields(p,fields);hp=SkillHealth.Balance256(t);FireTick(p,fields.Max(x=>x.UntilUtc).AddSeconds(1),false);Require(Fields(p).Length==0 && SkillHealth.Balance256(t)==hp,"Offline field fought or failed to expire");t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
        Emit(p,t,1,TimedEffects.Now);hp=SkillHealth.Balance256(t);Tick(p,TimedEffects.Now.AddSeconds(3),false);Require(Own(p).Drops.Length==0 && Fields(p).Length==0 && SkillHealth.Balance256(t)==hp,"Offline warning replayed impact");
    }
    private static void Waves(PlayerMobile p,BaseCreature t)
    {
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Griffon's Eye").Id,99,new Maximum(),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Actual Griffon");ClassGear.Refresh(p);Require(NamedElements.MasteryPercent(p,"ltng")>0 && ClassGear.Bonus(p,"pierce-ltng")>0,"Actual lightning mastery/pierce");
        var at=TimedEffects.Now;int draws=0;MartialPhoenix.SeedLow=()=>{draws++;return 7;};Emit(p,t,2,at);var wave=Own(p);Require(wave.Drops.Length==0 && wave.Flights.Length==7 && wave.Flights.All(x=>!x.Cold && x.Frames==60 && x.Path.Length==60) && draws==1,"Tier2 seven source paths/nonstacked/owner seed");
        var f=wave.Flights[0];p.AddToBackpack(helm);ClassGear.Refresh(p);Require(NamedElements.MasteryPercent(p,"ltng")==0 && ClassGear.Bonus(p,"pierce-ltng")==0,"Removed actual lightning gear");ulong expectedSeed=f.Seed;int rolled=WeaponMissileDamage.RollValue(f.Minimum256,f.Maximum256,0,n=>(int)SourceRandom.Limited(ref expectedSeed,n));var reduction=NamedElements.Resolve256(p,t,rolled,"ltng",enhance:false,pierceOverride:f.Pierce);int hp=SkillHealth.Balance256(t);var hit=MartialPhoenix.Impact(p,t,f);Require(hp-SkillHealth.Balance256(t)==reduction.Damage256 && hit.Seed==expectedSeed && hit.Seed!=f.Seed && !MartialPhoenix.Ready(f.Cast,t.Serial.Value,f.NextUtc.AddMilliseconds(159)) && MartialPhoenix.Ready(f.Cast,t.Serial.Value,f.NextUtc.AddMilliseconds(160)),"Source damage RNG/shared4frame lease boundary");Require(MartialPhoenix.Recipient(p,wave.Flights[1],new(t.X*2,t.Y*2))==null,"Sibling ignored trigger lease");
        Emit(p,t,2,at);Require(Own(p).Flights.Select(x=>x.Cast).Distinct().Count()==2,"Separate triggers conflated");Clear(p);
        Emit(p,t,3,at);wave=Own(p);Require(wave.Drops.Length==0 && wave.Flights.Length==16 && wave.Flights.All(x=>x.Cold && x.Frames==40 && x.Path.Length==0) && draws==3,"Tier3 sixteen chaos missiles/nonstacked/one owner draw");
        var cold=wave.Flights[0];var expected=MartialPhoenixPaths.Curve(unchecked((int)cold.TargetSeed),cold.Packed);var origin=t.Location;t.MoveToWorld(new(p.X+30,p.Y,p.Z),p.Map);Tick(p,at.AddMilliseconds(40));var next=Own(p).Flights.Single(x=>x.Id==cold.Id);Require(next.Frame==1 && next.Packed==expected.Packed && next.TargetSeed==expected.TargetSeed && next.Seed==expected.Seed && Own(p).Flights.All(x=>x.Fault==null),"First source elapsed0curve/state/movement");
        t.MoveToWorld(origin,p.Map);TimedEffects.Clear(t);hp=SkillHealth.Balance256(t);MartialPhoenix.Impact(p,t,next);Require(SkillHealth.Balance256(t)<hp && TimedEffects.TryActor(t,out var state) && state.Control?.Freeze?.Source==p.Serial.Value && state.Cold==null,"Chaos freeze-only damage2");TimedEffects.Clear(t);Clear(p);
        Emit(p,t,3,TimedEffects.Now);hp=SkillHealth.Balance256(t);Tick(p,TimedEffects.Now.AddSeconds(2),false);Require(Own(p).Flights.Length==0 && SkillHealth.Balance256(t)==hp,"Offline chaos fought/stopped lifetime");
        Emit(p,t,2,TimedEffects.Now);var where=p.Location;p.MoveToWorld(where,Map.Felucca);Tick(p,TimedEffects.Now.AddMilliseconds(40));Require(Own(p).Flights.Length==0,"Facet change retained player missiles");p.MoveToWorld(where,Map.Trammel);
        Emit(p,t,3,TimedEffects.Now);var good=MartialPhoenix.Snapshot();bool refused=false;try{MartialPhoenix.Restore(good with{Flights=good.Flights.Select(x=>x.Owner==p.Serial.Value?x with{Frames=99}:x).ToArray()});}catch(InvalidDataException){refused=true;}finally{MartialPhoenix.Restore(good);}Require(refused,"Corrupt Phoenix lifetime accepted");Clear(p);
    }
    private static void Natural()
    {
        var p=Fresh();var primary=Target(p);var secondary=Target(p,2);secondary.MoveToWorld(primary.Location,p.Map);var weapon=new Dagger();ClassGear.Profiles.Convert(weapon,99,GearRarity.Normal,new Random(173));p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Natural melee weapon");ClassGear.Refresh(p);
        var net=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));net.Seeded=true;net.SentFirstPacket=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();net.SendMobileUpdate(p);
        var hit=TigerStrikes.HitRoll;var block=SourceBlocking.BlockRoll;TigerStrikes.HitRoll=()=>0;SourceBlocking.BlockRoll=_=>99;int phase=0,mana=0,hp=0;bool damaged=false;long deadline=0;
        void Cleanup(){Clear(p);ReplaceFields(p,[]);TigerStrikes.HitRoll=hit;SourceBlocking.BlockRoll=block;primary.Corpse?.Delete();secondary.Corpse?.Delete();primary.Delete();secondary.Delete();p.Delete();}
        void Start()
        {
            int count=phase+1;AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=p.Serial.Value).Append(Charge(p,count)).ToArray());if(phase==2){SkillHealth.SetBalance256(primary,1);SkillHealth.SetBalance256(secondary,1);}hp=SkillHealth.Balance256(secondary);mana=NecromancerEffects.ManaBalance256(p);damaged=false;deadline=Core.TickCount+6500;Require(TigerStrikes.BasicStrike(p,primary),"Natural ordinary Phoenix finisher");CombatDamage.Finish(primary,p);
            var own=Own(p);Require(phase==0?own.Drops.Length==1 && own.Flights.Length==0:own.Drops.Length==0 && own.Flights.Length==(phase==1?7:16),"Natural selected tier factory");if(phase==2){Require(!primary.Alive,"Natural lethal parent");}
        }
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline && net.Running && p.NetState==net,"Natural Phoenix lifetime/client");var own=Own(p);Require(own.Flights.All(x=>x.Fault==null) && own.Drops.All(x=>x.Fault==null),"Natural Phoenix fault");damaged|=SkillHealth.Balance256(secondary)<hp;
                if(own.Flights.Length==0 && own.Drops.Length==0 && Fields(p).Length==0)
                {
                    Require(damaged && NecromancerEffects.ManaBalance256(p)==mana && (AssassinCharges.State(p,CombatAbility.PhoenixStrike)?.Count??0)==phase,"Natural damage/expiry/once consumption/no debit");
                    if(phase==2){var corpse=secondary.Corpse as Corpse;Require(!secondary.Alive && corpse!=null && CorpseSkillRules.Shattered(corpse) && !CorpseSkillRules.Usable(p,corpse,20),"Natural chaos lethal freeze/corpse");corpse.DropItem(new Gold(17));Require(corpse.Items.Count>0 && !corpse.SanctuaryConsumed,"Natural frozen loot");Cleanup();Log.Information("phoenix-natural-result: passed");return;}
                    phase++;Start();
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
            }
            catch(Exception e){Cleanup();Log.Error(e,"phoenix-natural-result: failed");}
        }
        Start();Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
    private static void Check()
    {
        var p=Fresh();var t=Target(p);Meteors(p,t);Waves(p,t);var at=TimedEffects.Now;Emit(p,t,1,at);Emit(p,t,2,at);Emit(p,t,3,at);t.MoveToWorld(new(p.X+30,p.Y,p.Z),p.Map);Tick(p,at.AddMilliseconds(120));var s=Own(p);Require(s.Drops.Single().Frame==3 && s.Flights.Length==23 && s.Flights.All(x=>x.Frame==3 && x.Fault==null),"Partial warning/chain/chaos source clocks");
        var shift=TimedEffects.Now.AddHours(1)-s.Flights.Min(x=>x.NextUtc);s=s with{Drops=s.Drops.Select(x=>x with{NextUtc=x.NextUtc+shift}).ToArray(),Flights=s.Flights.Select(x=>x with{NextUtc=x.NextUtc+shift}).ToArray(),Leases=s.Leases.Select(x=>x with{UntilUtc=x.UntilUtc+shift}).ToArray()};Replace(p,s);
        var values=SkillMechanics.Values(p,CombatAbility.PhoenixStrike);var packet=MartialPhoenixPackets.Capture("royalstrikemeteorfire",values,0);FireGround.AddPhoenix(p,values.Rank,p.X+2,p.Y,p.Z,packet.Minimum256,packet.Maximum256,TimedEffects.Now);var fields=Fields(p);shift=TimedEffects.Now.AddHours(1)-fields[0].NextUtc;ReplaceFields(p,fields.Select(x=>x with{NextUtc=x.NextUtc+shift,UntilUtc=x.UntilUtc+shift}).ToArray());NecromancerEffects.SetMana256(p,12345);SkillHealth.SetBalance256(p,5432);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,p.Location,Own(p),Fields(p),NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p))));Log.Information("Phoenix world release:nonstacked3tiers,60frame warning/current packet/18fields,one field recipient/no damage3 RNG,7exact seeded paths,16elapsed0curves/freeze-only,4frame leases,offline/facet/corrupt-save and partial state pass;learned input verified separately");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var own=Own(p);Require(JsonSerializer.Serialize(own)==JsonSerializer.Serialize(s.Projectiles with{NextId=own.NextId,NextCast=own.NextCast}),"Saved exact warning/path/curve/seed/clock/lease");Require(JsonSerializer.Serialize(Fields(p))==JsonSerializer.Serialize(s.Fields) && NecromancerEffects.ManaBalance256(p)==s.Mana256 && SkillHealth.Balance256(p)==s.Life256,"Saved fields/fractional mana/injury");p.MoveToWorld(s.Origin,Map.Trammel);Tick(p,own.Flights.Min(x=>x.NextUtc));Require(Own(p).Drops.Single().Frame==4 && Own(p).Flights.All(x=>x.Frame==4 && x.Fault==null),"Resumed partial warning/chain/chaos");Replace(p,s.Projectiles);ReplaceFields(p,s.Fields);p.Internalize();
    }
}
