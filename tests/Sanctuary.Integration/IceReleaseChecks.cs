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
public static class IceReleaseChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(IceReleaseChecks));
    private sealed class Maximum:Random{public override int Next(int max)=>Math.Max(0,max-1);}
    private sealed record Saved(uint Owner,uint Target,Point3D Origin,IceCubeSave Fields,MartialCharge[] Charges,uint Helm,uint Ring,int Mana256,int Life256,NamedControlState Control,DateTime ChillUntil,uint Corpse);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"ice-release-fixtures.json");
    private static bool Creating;
    private static void Rates(){var hp=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=p=>Creating || p.Name=="IceReleaseFixture"?TimeSpan.FromHours(1):hp?.Invoke(p)??Mobile.DefaultHitsRate;var mp=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>Creating || p.Name=="IceReleaseFixture"?TimeSpan.FromHours(1):mp?.Invoke(p)??Mobile.DefaultManaRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    public static void Configure(){Rates();Register("ice-release-check",Check);Register("ice-release-reload-check",Reload);Register("ice-natural-check",Natural);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Ice inline/cold/freeze/area/stationary cubes and persistence",_=>Core.LoopContext.Post(()=>{try{Controlled(action);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Controlled(Action action)
    {
        var hit=TigerStrikes.HitRoll;var element=TigerStrikes.ElementRoll;var physical=TigerStrikes.PhysicalRoll;var effects=TigerStrikes.Effects;var block=SourceBlocking.BlockRoll;var roll=MartialCold.DamageRoll;var seed=MartialCold.SeedLow;
        try{TigerStrikes.HitRoll=()=>0;TigerStrikes.ElementRoll=()=>0;TigerStrikes.PhysicalRoll=_=>0;TigerStrikes.Effects=new Maximum();SourceBlocking.BlockRoll=_=>99;MartialCold.DamageRoll=_=>0;MartialCold.SeedLow=()=>7;action();}
        finally{TigerStrikes.HitRoll=hit;TigerStrikes.ElementRoll=element;TigerStrikes.PhysicalRoll=physical;TigerStrikes.Effects=effects;SourceBlocking.BlockRoll=block;MartialCold.DamageRoll=roll;MartialCold.SeedLow=seed;}
    }
    private static SkillValues Values(int rank,int synergy=0)=>new(SkillMechanics.For(CombatAbility.BladesOfIce),k=>k=="Blades of Ice"?rank:0,k=>k=="Royal Strike"?synergy:0,99);
    private static PlayerMobile Fresh(int row=3050)
    {Creating=true;try{var p=BladeBodyChecks.Fresh(6940,20,row);p.Name="IceReleaseFixture";while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Str");}while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dex");}var claw=new Tekagi();ClassGear.Profiles.Convert(claw,99,GearRarity.Normal,new Random(173));p.AddToBackpack(claw);Require(p.EquipItem(claw),"Primary claw");ClassGear.Refresh(p);return p;}finally{Creating=false;}}
    private static BaseCreature Target(PlayerMobile p,int x=1)=>TargetAt(p,new(p.X+x,p.Y,p.Z));
    private static BaseCreature TargetAt(PlayerMobile p,Point3D point)
    {var t=new CobraPassiveTarget{Name="IceReleaseTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(point,p.Map);t.AIObject?.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(type,0);}return t;}
    private static MartialCharge Charge(PlayerMobile p,int count=3,CombatAbility ability=CombatAbility.BladesOfIce)=>new(p.Serial.Value,ability,count,SkillMechanics.Rank(p,ability),count*50,TimedEffects.Now.AddHours(1));
    private static void Hold(PlayerMobile p,params MartialCharge[] charges)=>AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=p.Serial.Value).Concat(charges).ToArray());
    private static IceCubeSave Own(PlayerMobile p){var all=MartialCold.Snapshot();return all with{Cubes=all.Cubes.Where(x=>x.Owner==p.Serial.Value).ToArray()};}
    private static void Clear(PlayerMobile p){var all=MartialCold.Snapshot();MartialCold.Restore(all with{Cubes=all.Cubes.Where(x=>x.Owner!=p.Serial.Value).ToArray()});}
    private static void TickOwner(PlayerMobile p,DateTime at,bool active=true)
    {var all=MartialCold.Snapshot();var own=Own(p);var after=own;MartialCold.Restore(own);try{MartialCold.Tick(at,active);after=MartialCold.Snapshot();}finally{MartialCold.Restore(all with{Cubes=all.Cubes.Where(x=>x.Owner!=p.Serial.Value).Concat(after.Cubes).OrderBy(x=>x.Id).ToArray()});}}
    private static void Differential()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"ice-damage-source-probe.json")));int packets=0;
        foreach(var row in source.RootElement.EnumerateArray())
        {int V(int i)=>row[i].GetInt32();var p=MartialCold.Capture(Values(V(0),V(1)),3,V(2),_=>0);Require(p.Minimum256==V(4) && p.Maximum256==V(5) && p.Inline256==V(4) && p.Frames==V(6) && p.Radius==6 && p.ScatterRadius==3 && V(7)==17+p.Inline256 && V(8)==p.Frames,"Full source skill endpoints/mastery/length replacement");int cube=FireGround.MissileDamage256("bladesoficecubes",V(0),0,false)*(100+V(2))/100;Require(cube==V(9) && cube==V(10) && V(11)==100 && V(12)==0 && V(13)==0 && V(14)==0 && V(15)==0,"Full source cube creator endpoints/length/no weapon/mastery duplication");packets++;}
        Require(packets==216,"Complete numeric fixture");
        using var scatter=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"martial-cold-source-probe.json")));int patterns=0;
        foreach(var row in scatter.RootElement.EnumerateArray())
        {int V(int i)=>row[i].GetInt32();if(V(0)!=3 || V(1)<=0 || V(2)!=63 || V(4)!=0){continue;}ulong seed=SourceRandom.InitLow(V(3));int child=unchecked((int)SourceRandom.Roll(ref seed));var expected=row[17].EnumerateArray().ToArray();var actual=FistsFireRelease.Scatter(3,child);Require(actual.Length==expected.Length,"Full source radius3 attempts");for(int i=0;i<actual.Length;i++){Require(actual[i].X==expected[i][0].GetInt32()-10 && actual[i].Y==expected[i][1].GetInt32()-20 && expected[i][2].GetInt32()==561,"Full source LCG/scatter/child identity");}patterns++;}
        Require(patterns>0,"Missing full source scatter cases");
    }
    private static void States(PlayerMobile p,BaseCreature t)
    {
        foreach(int resistance in new[]{0,50,100})
        {
            TimedEffects.Clear(t);t.SetResistance(ResistanceType.Cold,resistance);var now=TimedEffects.Now;MartialCold.Impact(p,t,0,100,100,0,Core.TickCount);
            if(resistance==100){Require(!TimedEffects.TryActor(t,out _),"Immune target chilled/frozen");continue;}
            Require(TimedEffects.TryActor(t,out var a) && a.Cold?.Source==p.Serial.Value && a.Control?.Freeze?.Source==p.Serial.Value,"Zero-damage cold+freeze states");Require(Math.Abs((a.ChillUntil-now).TotalMilliseconds-(100-resistance)*40)<50 && Math.Abs((a.Control.FreezeUntil-now).TotalMilliseconds-(100-resistance)*40)<50,"Independent resisted duration lanes");
        }
        t.SetResistance(ResistanceType.Cold,0);TimedEffects.Clear(t);
        foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
        {var mob=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};MonsterVariants.Assign(mob,difficulty);mob.MoveToWorld(new(p.X+7,p.Y,p.Z),p.Map);mob.AIObject.AITimer.Stop();mob.SetResistance(ResistanceType.Cold,0);var now=TimedEffects.Now;MartialCold.Impact(p,mob,0,101,101,0,Core.TickCount);Require(TimedEffects.TryActor(mob,out var a),"Variant state");int frames=101/(difficulty==MonsterDifficulty.Normal?1:difficulty==MonsterDifficulty.Nightmare?2:4);Require(a.Cold!=null && a.Control?.Freeze!=null && Math.Abs((a.Control.FreezeUntil-now).TotalMilliseconds-frames*40)<50 && Math.Abs((a.ChillUntil-now).TotalMilliseconds-frames*40)<50,"Native difficulty divisor");TimedEffects.Clear(mob);mob.Delete();}
    }
    private static (Item Helm,Item Ring,uint Corpse) Parent(PlayerMobile p,BaseCreature t)
    {
        var helm=NamedGear.Create("unique/343",99,new Maximum(),true);var ring=NamedGear.Create("unique/275",99,new Maximum(),true);p.AddToBackpack(helm);p.AddToBackpack(ring);Require(p.EquipItem(helm) && p.EquipItem(ring),"Actual Nightwing/Raven");ClassGear.Refresh(p);
        Hold(p,Charge(p,1));var packet=MartialCold.Capture(p,AssassinCharges.State(p,CombatAbility.BladesOfIce));Require(packet.Rank==SkillMechanics.Rank(p,CombatAbility.BladesOfIce) && NamedElements.MasteryPercent(p,"cold")>0,$"Actual soft rank/mastery rank={packet.Rank}/{SkillMechanics.Rank(p,CombatAbility.BladesOfIce)} mastery={NamedElements.MasteryPercent(p,"cold")}");t.SetResistance(ResistanceType.Physical,100);TimedEffects.Clear(t);int before=SkillHealth.Balance256(t);var raw=GearEffects.Damage(p,0,t,0,_=>0);var prepared=GearEffects.PrepareWeaponImpact(p,t,0,new Maximum(),raw,skillElements:new(0,0,packet.Inline256/256.0,0,0));
        Require(TigerStrikes.BasicStrike(p,t),"Inline ordinary finisher");Require(before-SkillHealth.Balance256(t)==NamedElements.To256(prepared.Parts.Total) && AssassinCharges.State(p,CombatAbility.BladesOfIce)==null && Own(p).Cubes.Length==0,"Inline typed damage/mastery/consume");Require(TimedEffects.TryActor(t,out var a) && a.Cold!=null && a.Control?.Freeze==null && Math.Abs((a.ChillUntil-TimedEffects.Now).TotalMilliseconds-packet.Frames*40)<50,"Skill replaced Raven cold duration");
        TimedEffects.Clear(t);Hold(p,Charge(p));var held=AssassinCharges.State(p,CombatAbility.BladesOfIce);TigerStrikes.HitRoll=()=>1;Require(!TigerStrikes.BasicStrike(p,t) && AssassinCharges.State(p,CombatAbility.BladesOfIce)==held && Own(p).Cubes.Length==0,"Miss consumed/released");TigerStrikes.HitRoll=()=>0;
        // Item removed after inline capture: callbacks must read current live rank/mastery.
        var initial=Charge(p) with{InitialRank=1};var captured=MartialCold.Capture(p,initial);p.AddToBackpack(helm);ClassGear.Refresh(p);int current=SkillMechanics.Rank(p,CombatAbility.BladesOfIce);MartialCold.AfterParent(p,t,t.Location,p.Map,captured,TimedEffects.Now);Require(current<captured.Rank && Own(p).Cubes.Length>0 && Own(p).Cubes.All(x=>x.Rank==current && x.Minimum256==256 && x.Maximum256==256 && x.ColdFrames==100),"Callback retained removed item rank/mastery");Clear(p);p.EquipItem(helm);ClassGear.Refresh(p);
        Hold(p,Charge(p),Charge(p,2,CombatAbility.TigerStrike),Charge(p,3,CombatAbility.CobraStrike),Charge(p,3,CombatAbility.FistsOfFire),Charge(p,3,CombatAbility.ClawsOfThunder));Require(TigerStrikes.BasicStrike(p,t),"Five-family finisher");Require(AssassinCharges.Finisher(p).Length==5 && AssassinCharges.Finisher(p).All(x=>x.Count==(x.Ability==CombatAbility.TigerStrike?1:2)),"Five independent families");Clear(p);FireGround.Cancel(p);var lightning=MartialLightning.Snapshot();var casts=lightning.Flights.Where(x=>x.Owner==p.Serial.Value).Select(x=>x.Cast).ToHashSet();MartialLightning.Restore(lightning with{Flights=lightning.Flights.Where(x=>x.Owner!=p.Serial.Value).ToArray(),Leases=lightning.Leases.Where(x=>!casts.Contains(x.Cast)).ToArray()});
        var dying=Target(p);Hold(p,Charge(p));SkillHealth.SetBalance256(dying,1);Require(TigerStrikes.BasicStrike(p,dying),"Lethal frozen ordinary finisher");CombatDamage.Finish(dying,p);var corpse=dying.Corpse as Corpse;Require(!dying.Alive && Own(p).Cubes.Length>0 && corpse!=null && CorpseSkillRules.Shattered(corpse) && !CorpseSkillRules.Usable(p,corpse,20),"Lethal freeze/children/corpse eligibility");corpse.BeginDecay(TimeSpan.FromHours(1));corpse.DropItem(new Gold(777));Require(corpse.Items.Count>0 && !corpse.SanctuaryConsumed,"Frozen corpse lost loot");Clear(p);dying.Delete();t.SetResistance(ResistanceType.Physical,0);return(helm,ring,corpse.Serial.Value);
    }
    private static void Fields(PlayerMobile p,BaseCreature t)
    {
        Clear(p);var at=TimedEffects.Now;var packet=MartialCold.Capture(Values(1),3,0,_=>0);int draws=0;MartialCold.SeedLow=()=>{draws++;return 7;};MartialCold.AfterParent(p,t,t.Location,p.Map,packet with{Count=2},at);Require(draws==0 && Own(p).Cubes.Length==0,"Count2 created fields/seed draw");
        MartialCold.AfterParent(p,t,t.Location,p.Map,packet,at);var cubes=Own(p).Cubes;Require(draws==1 && cubes.Length==FistsFireRelease.Scatter(3,7).Length && cubes.All(x=>x.Frames==30 && x.ColdFrames==100 && x.Frame==0 && x.Minimum256==256 && x.Maximum256==256 && x.LastCollision==p.Serial.Value),"Cube factory/seed/constant packet");
        var one=cubes[0] with{X=t.X,Y=t.Y,LastCollision=p.Serial.Value};var all=MartialCold.Snapshot();MartialCold.Restore(all with{Cubes=all.Cubes.Where(x=>x.Owner!=p.Serial.Value).Append(one).ToArray()});TimedEffects.Clear(t);int hp=SkillHealth.Balance256(t);TickOwner(p,one.NextUtc);Require(SkillHealth.Balance256(t)==hp-256 && Own(p).Cubes.Single().LastCollision==t.Serial.Value,"First cube collision");TickOwner(p,one.NextUtc.AddMilliseconds(40));Require(SkillHealth.Balance256(t)==hp-256,"LastCollision repeated same victim");var other=TargetAt(p,t.Location);TickOwner(p,one.NextUtc.AddMilliseconds(80));Require(SkillHealth.Balance256(other)==50000*256-256 && Own(p).Cubes.Single().LastCollision==other.Serial.Value,"Alternating second collider");TickOwner(p,one.NextUtc.AddMilliseconds(120));Require(SkillHealth.Balance256(t)==hp-512 && Own(p).Cubes.Single().LastCollision==t.Serial.Value,"Alternating collider could not rehit first");other.Delete();
        var valid=MartialCold.Snapshot();bool refused=false;try{MartialCold.Restore(valid with{Cubes=valid.Cubes.Select(x=>x.Owner==p.Serial.Value?x with{Frames=31}:x).ToArray()});}catch(InvalidDataException){refused=true;}finally{MartialCold.Restore(valid);}Require(refused,"Corrupt lifetime accepted");hp=SkillHealth.Balance256(t);TickOwner(p,at.AddSeconds(2),false);Require(Own(p).Cubes.Length==0 && SkillHealth.Balance256(t)==hp,"Offline cubes fought or did not age");
        MartialCold.AfterParent(p,t,t.Location,p.Map,packet,TimedEffects.Now);var origin=p.Location;p.MoveToWorld(origin,Map.Felucca);TickOwner(p,TimedEffects.Now.AddMilliseconds(40));Require(Own(p).Cubes.Length==0,"Player projectile followed facet change");p.MoveToWorld(origin,Map.Trammel);
        // The range is centered on the target but LOS is from the caster.
        var farther=Target(p,3);Require(MartialCold.AreaAllowed(p,farther,t.Location,p.Map,6),"Clear native caster LOS/radius");var wall=new Static(0x80);wall.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);Require(!MartialCold.AreaAllowed(p,farther,t.Location,p.Map,6),"Caster LOS ignored native wall");wall.Delete();farther.Delete();TimedEffects.Clear(t);Clear(p);
    }
    private static void Natural()
    {
        var p=Fresh(3300);var primary=Target(p);var secondary=Target(p,2);var net=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));net.Seeded=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;var f=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);f.SetValue(net,Enum.Parse(f.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();net.SendMobileUpdate(p);Hold(p,Charge(p));SkillHealth.SetBalance256(primary,1);int hp=SkillHealth.Balance256(secondary),mana=NecromancerEffects.ManaBalance256(p);Require(TigerStrikes.BasicStrike(p,primary),"Natural lethal finisher");CombatDamage.Finish(primary,p);Require(!primary.Alive && Own(p).Cubes.Length>0,"Natural children");var cube=Own(p).Cubes[0];secondary.MoveToWorld(new Point3D((int)Math.Round(cube.X),(int)Math.Round(cube.Y),cube.Z),p.Map);hp=SkillHealth.Balance256(secondary);long deadline=Core.TickCount+4000;bool damaged=false;
        void Cleanup(){Clear(p);primary.Corpse?.Delete();primary.Delete();secondary.Delete();p.Delete();}
        void Wait(){try{Require(Core.TickCount<deadline && net.Running && p.NetState==net,"Natural lifetime/continuous client");Require(Own(p).Cubes.All(x=>x.Fault==null),"Natural cube fault");damaged|=SkillHealth.Balance256(secondary)<hp;if(Own(p).Cubes.Length==0){Require(damaged && NecromancerEffects.ManaBalance256(p)==mana && AssassinCharges.State(p,CombatAbility.BladesOfIce)?.Count==2,"Natural damage/expiry/resources");Cleanup();Log.Information("ice-natural-result: passed");return;}Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);}catch(Exception e){Cleanup();Log.Error(e,"ice-natural-result: failed");}}
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
    private static void Check()
    {
        Differential();var p=Fresh();var t=Target(p);States(p,t);Fields(p,t);var gear=Parent(p,t);Hold(p,Charge(p),Charge(p,2,CombatAbility.FistsOfFire));TimedEffects.Clear(t);var packet=MartialCold.Capture(p,AssassinCharges.State(p,CombatAbility.BladesOfIce));var at=TimedEffects.Now;MartialCold.AfterParent(p,t,t.Location,p.Map,packet,at);TickOwner(p,at.AddMilliseconds(40));var all=MartialCold.Snapshot();var own=Own(p);Require(own.Cubes.Length>0 && own.Cubes.All(x=>x.Frame==1),"Partial fields");var shift=TimedEffects.Now.AddHours(1)-own.Cubes.Min(x=>x.NextUtc);MartialCold.Restore(all with{Cubes=all.Cubes.Select(x=>x.Owner==p.Serial.Value?x with{NextUtc=x.NextUtc+shift}:x).ToArray()});Require(TimedEffects.TryActor(t,out var state) && state.Control?.Freeze!=null && state.Cold!=null,"Saved both state lanes");state.ChillUntil=TimedEffects.Now.AddHours(1);state.Control=state.Control with{FreezeUntil=TimedEffects.Now.AddHours(1),Freeze=state.Control.Freeze with{AiUntilUtc=TimedEffects.Now.AddHours(1)}};p.AddToBackpack(gear.Helm);p.AddToBackpack(gear.Ring);ClassGear.Refresh(p);NecromancerEffects.SetMana256(p,12345);SkillHealth.SetBalance256(p,5432);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,p.Location,Own(p),AssassinCharges.Finisher(p),gear.Helm.Serial.Value,gear.Ring.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),state.Control,state.ChillUntil,gear.Corpse)));
        Log.Information("Ice release:216 full source getter/creator/writer contexts,full LCG scatter,actual Nightwing/Raven,independent5families,zero-damage duration lanes/immunity/difficulty/LOS,alternating LastCollision,offline/facet/corrupt-save and lethal frozen loot pass");
    }
    private static void Reload()
    {
        Differential();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var own=Own(p);Require(JsonSerializer.Serialize(own)==JsonSerializer.Serialize(s.Fields with{NextId=own.NextId}),"Saved exact cubes/LastCollision/clock/mastery");Require(AssassinCharges.Finisher(p).SequenceEqual(s.Charges) && NecromancerEffects.ManaBalance256(p)==s.Mana256 && SkillHealth.Balance256(p)==s.Life256,"Saved charges/fractional resources");Require(TimedEffects.TryActor(t,out var state) && state.Cold?.Source==p.Serial.Value && state.Control==s.Control && state.ChillUntil==s.ChillUntil,"Saved chill/freeze lanes");var corpse=(Corpse)World.FindItem((Serial)s.Corpse);Require(CorpseSkillRules.Shattered(corpse) && corpse.Items.Count>0 && !CorpseSkillRules.Usable(p,corpse,20),"Saved frozen corpse/loot");Require(World.FindItem((Serial)s.Helm).Parent==p.Backpack && World.FindItem((Serial)s.Ring).Parent==p.Backpack,"Saved removed actual gear");p.MoveToWorld(s.Origin,Map.Trammel);TickOwner(p,own.Cubes.Min(x=>x.NextUtc));Require(Own(p).Cubes.All(x=>x.Frame==2 && x.Fault==null),"Restored cubes did not resume");var all=MartialCold.Snapshot();MartialCold.Restore(all with{Cubes=all.Cubes.Where(x=>x.Owner!=p.Serial.Value).Concat(s.Fields.Cubes).OrderBy(x=>x.Id).ToArray()});p.Internalize();
    }
}
