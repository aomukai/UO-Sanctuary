// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class MageBasicChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MageBasicChecks));
    private sealed record Saved(uint Owner,uint Mage,Point3D Origin,int Map,int Life,int Maximum,long Order,MageCriticalSeed Seed,MageMissileState[] Flights,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"mage-basics-fixtures.json");
    public static void Configure()
    {
        Register("mage-basics-check",Check);Register("mage-basics-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("mage-basics-live","Natural three-element basic volleys",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("mage-basics-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Approved Sorceress basics/flat Mastery/triples/saved charged path",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,string skill,int rank)=>MagePetBuffChecks.Train(p,"necromancer."+skill,rank);
    private static PlayerMobile Owner(int rank,int mastery=0)
    {var p=MagePetBuffChecks.Owner(rank,6660,2700);if(mastery>0){Train(p,"skeletonmastery",mastery);}return p;}
    private static void Reset(PlayerMobile p,Item helm)
    {
        ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Restore fixture Strength after real respec");}
        while(p.RawDex<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Restore fixture Dexterity after real respec");}
        Train(p,"skeletalmage",4);Train(p,"skeletonmastery",3);Require(p.EquipItem(helm),"Reequip original fixture helm after real respec");ClassGear.Refresh(p);
    }
    private static MageMissileState[] Flights(NecromancerMinion m)=>NecromancerMageMissiles.Snapshot().Bolts.Where(x=>x.Actor==m.Serial.Value).ToArray();
    private static void Clear(NecromancerMinion m)
    {var all=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(all with{Bolts=all.Bolts.Where(x=>x.Actor!=m.Serial.Value).ToArray()});}
    private static void Step(long id,bool fight=true)
    {var f=NecromancerMageMissiles.Snapshot().Bolts.SingleOrDefault(x=>x.Id==id);if(f!=null){NecromancerMageMissiles.Tick(f.NextUtc,fight,id);}}
    private static GraveyardSkeleton Target(PlayerMobile p,Mobile m,int dx=6)
    {var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(m.X+dx,m.Y,m.Z),p.Map);foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static void Inspect(PlayerMobile owner,NecromancerMinion mage,int minimum,int maximum,int count)
    {
        var gump=(PetStatsGump)Activator.CreateInstance(typeof(PetStatsGump),BindingFlags.Instance|BindingFlags.NonPublic,null,[owner],null);
        string html=string.Join("\n",ClassNameChecks.GumpStrings(gump));
        string spell=mage.MageElement switch{1=>"Ice Bolt",2=>"Fire Bolt",3=>"Charged Bolt",_=>throw new InvalidOperationException("Unsupported fixture element")};
        string element=mage.MageElement switch{1=>"Cold",2=>"Fire",3=>"Lightning",_=>""};
        string expected=FormattableString.Invariant($"{spell} rank {mage.Rank}: {element} damage {minimum/256.0:0.##}–{maximum/256.0:0.##} per bolt<BR>Bolts per volley {count} (before critical hits and target resistances)");
        Require(html.Contains("Skeletal Mage — Level ")&&html.Contains(expected),"Compiled inspector has wrong basic/Mastery/per-bolt/volley text");
    }
    private static void Formula()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"mage-missile-source-cases.json")));
        var old=doc.RootElement.EnumerateArray().ToDictionary(x=>(x[0].GetInt32(),x[1].GetInt32()),x=>(Min:x[2].GetInt32(),Max:x[3].GetInt32()));int cases=0;
        foreach(int rank in new[]{1,3,8,20})foreach(int mastery in new[]{0,1,7,20})
        {
            var p=Owner(rank,mastery);try
            {
                foreach(int element in new[]{1,2,3})
                {
                    var m=MagePetBuffChecks.Pet(p,element);try
                    {
                        var before=NecromancerMageMissiles.ActorSeed(m);int life=SkillHealth.Balance256(m),maximum=SkillHealth.Maximum256(m);long order=BattleCommand.PetOrder(m);
                        var packet=MageBasicSkills.Packet(m);var ability=element==1?CombatAbility.IceBolt:element==2?CombatAbility.FireBolt:CombatAbility.ChargedBolt;
                        var values=MagePetBuffs.Values(ability,rank,m.Level);int zero=Math.Max(rank<4?0:(rank-2)/2,1),full=Math.Max(mastery+(rank<4?0:(rank-2)/2),1);var a=old[(element,zero)];var b=old[(element,full)];
                        Require(packet.Spell==ability&&packet.Rank==rank&&packet.Minimum256==values.Damage256(false)+b.Min-a.Min&&packet.Maximum256==values.Damage256(true)+b.Max-a.Max,"Approved basic/Mastery differs from independent source matrix");
                        Require(packet.ElementFrames==(element==1?values.DurationFrames:0)&&NecromancerMageMissiles.ActorSeed(m)==before&&SkillHealth.Balance256(m)==life&&SkillHealth.Maximum256(m)==maximum&&BattleCommand.PetOrder(m)==order,"Basic query changed duration/resources/order/stream");
                        if(rank==1&&mastery==0){Require((packet.Minimum256,packet.Maximum256)==(element==1?(768,1280):element==2?(768,1536):(512,1024)),"Independent rank1Sorceress golden");}
                        Inspect(p,m,values.Damage256(false)+b.Min-a.Min,values.Damage256(true)+b.Max-a.Max,MageBasicSkills.Count(m)*MageBasicSkills.Copies(m));
                        cases++;
                    }finally{m.Delete();}
                }
            }finally{p.Delete();}
        }
        Require(cases==48,"Missing basic formula cases");
    }
    private static void Volleys()
    {
        var p=Owner(4,3);var helm=PoisonDaggerChecks.Named(p,"Wormskull");
        try
        {
            foreach(int element in new[]{1,2,3})
            {
                if(ClassCreation.Profiles.GetRank(p.Serial,"necromancer.skeletalmage")>=5){Reset(p,helm);}
                var m=MagePetBuffChecks.Pet(p,element);var t=Target(p,m);var original=AmazonSlowMissiles.Snapshot();
                try
                {
                    int rank=m.Rank;Require(rank==5,"Real effective rank boundary");var packet=MageBasicSkills.Packet(m);ulong seed=NecromancerMageMissiles.ActorSeed(m).Seed;
                    Require(MageBasicSkills.Copies(m)==1,"Soft rank unlocked triples");int count=MageBasicSkills.Count(m);long first=NecromancerMageMissiles.Launch(m,t,TimedEffects.Now);Require(first>0&&Flights(m).Length==count,"Normal volley count");Inspect(p,m,packet.Minimum256,packet.Maximum256,count);Clear(m);Train(p,"skeletalmage",5);
                    first=NecromancerMageMissiles.Launch(m,t,TimedEffects.Now);var flights=Flights(m);Require(flights.Length==count*3&&flights.All(x=>x.Packet==packet)&&NecromancerMageMissiles.ActorSeed(m).Seed==seed,"Live triple changed basic rank/Mastery or spent actor RNG");Inspect(p,m,packet.Minimum256,packet.Maximum256,count*3);
                    Require(flights.All(x=>x.Frames==(element==3?77:50)&&x.VelocityFp8==(element==2?3840:2304)&&x.Frame==0),"Wrong Sorceress flight velocity/lifetime");
                    // Exercise the new packet through the actual shared HP
                    // receiver,including the per-bolt Mastery addition.
                    var contact=flights[0];ulong critical=1,nextCritical;
                    while(true){nextCritical=critical;if(SourceRandom.Roll(ref nextCritical)%100>=5){break;}critical++;}
                    NecromancerMageMissiles.SetActorSeed(m,critical);ulong damageSeed=contact.Seed;
                    int expectedDamage=WeaponMissileDamage.RollValue(contact.Packet.Minimum256,contact.Packet.Maximum256,0,n=>(int)SourceRandom.Limited(ref damageSeed,n));int before=SkillHealth.Balance256(t);
                    NecromancerMageMissiles.Impact(m,t,contact);Require(before-SkillHealth.Balance256(t)==expectedDamage&&NecromancerMageMissiles.ActorSeed(m).Seed==nextCritical,"Actual basic/Mastery HP or intrinsic stream changed");NecromancerMageMissiles.SetActorSeed(m,seed);
                    if(element==3)
                    {
                        Require(flights.All(x=>x.Charged!=null)&&flights.Select(x=>JsonSerializer.Serialize(x.Charged.Path)).Distinct().Count()>count,"Charged triples reused identical paths");
                        var f=flights[0];var expected=ChargedBolts.Advance(f.Charged.Path,f.Charged.OriginX,f.Charged.OriginY,f.X,f.Y,1,SkillGeometry.Tiles(f.VelocityFp8/4096.0));Step(f.Id,false);Clear(m);
                        // Separate active path check;inactive expiry above must not move.
                        first=NecromancerMageMissiles.Launch(m,t,TimedEffects.Now);f=Flights(m)[0];Step(f.Id);var moved=Flights(m).Single(x=>x.Id==f.Id);Require(moved.X==expected.X&&moved.Y==expected.Y&&moved.Charged.Next==expected.Next,"Charged path did not use shared mover");
                    }
                    else{Require(flights.Select(x=>(x.Vx,x.Vy)).Distinct().Count()==3&&flights.All(x=>x.Charged==null),"Basic fan directions or stray charged motion");}
                    Clear(m);AmazonSlowMissiles.Restore(original.Append(new SlowMissilesState(m.Serial.Value,p.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());first=NecromancerMageMissiles.Launch(m,t,TimedEffects.Now);flights=Flights(m);
                    Require(flights.All(x=>x.Packet.Minimum256==packet.Minimum256/2&&x.Packet.Maximum256==packet.Maximum256/2&&x.Packet.ElementFrames==packet.ElementFrames&&x.VelocityFp8==(element==2?1920:1152)),"Slow changed duration or missed any child");Clear(m);AmazonSlowMissiles.Restore(original);
                    first=NecromancerMageMissiles.Launch(m,t,TimedEffects.Now);flights=Flights(m);int hp=SkillHealth.Balance256(t);foreach(var f in flights){for(int i=0;i<f.Frames;i++){Step(f.Id,false);}}Require(Flights(m).Length==0&&SkillHealth.Balance256(t)==hp&&NecromancerMageMissiles.ActorSeed(m).Seed==seed,"Inactive basic volley fought or replayed RNG");
                    // Fixed secondary construction remains independent of the basic bonus.
                    long glacial=NecromancerMageMissiles.LaunchGlacial(m,t,TimedEffects.Now);var g=Flights(m).Single();var fixedValues=MagePetBuffs.Values(CombatAbility.GlacialSpike,10,m.Level);Require(glacial>0&&g.Packet.Minimum256==fixedValues.Damage256(false)&&g.Packet.Rank==10,"Basic Mastery/triples leaked into fixed secondary");
                }
                finally{AmazonSlowMissiles.Restore(original);Clear(m);t.Delete();m.Delete();}
            }
        }
        finally{p.Delete();}
    }
    private static void Check()
    {
        Formula();Volleys();var p=Owner(9,20);var m=MagePetBuffChecks.Pet(p,1);var t=Target(p,m,25);var element=typeof(NecromancerMinion).GetField("_element",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        try
        {
            foreach(int e in new[]{1,2,3}){element.SetValue(m,e);NecromancerMageMissiles.Launch(m,t,TimedEffects.Now);}
            var all=NecromancerMageMissiles.Snapshot();var ids=Flights(m).Select(x=>x.Id).ToArray();foreach(long id in ids){for(int i=0;i<3;i++){Step(id);}}
            var held=TimedEffects.Now.Add(FixtureSpace.Retention);all=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(all with{Bolts=all.Bolts.Select(x=>x.Actor==m.Serial.Value?x with{NextUtc=held}:x).ToArray()});
            var original=JsonSerializer.Serialize(NecromancerMageMissiles.Snapshot());var charged=Flights(m).First(x=>x.Charged!=null);bool bad=false;
            try{NecromancerMageMissiles.Restore(NecromancerMageMissiles.Snapshot() with{Bolts=NecromancerMageMissiles.Snapshot().Bolts.Select(x=>x.Id==charged.Id?x with{Charged=x.Charged with{Path=[new(0,0)]}}:x).ToArray()});}catch(InvalidDataException){bad=true;}
            Require(bad&&JsonSerializer.Serialize(NecromancerMageMissiles.Snapshot())==original,"Malformed path partially replaced the saved pool");
            SkillHealth.SetBalance256(m,SkillHealth.Maximum256(m)-257);TimedEffects.PreventHealing(m,TimedEffects.Now);var point=p.Location;int map=p.Map.MapID;PersistentMinions.Suspend(p);p.Internalize();
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,m.Serial.Value,point,map,SkillHealth.Balance256(m),SkillHealth.Maximum256(m),BattleCommand.PetOrder(m),NecromancerMageMissiles.ActorSeed(m),Flights(m),0)));
        }
        finally{t.Delete();}
        Log.Information("Mage basics:48independent source-delta contexts,rank1Sorceress goldens,live hard4+soft1/5triples,normal/triple charged counts and distinct paths,shared motion,all-child Slow,offline expiry,no actor RNG or fixed-secondary bonus,atomic invalid path,saved three-spell cohort and compiled per-bolt/volley inspector pass");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var m=(MageWorldBody)World.FindMobile((Serial)s.Mage);
        Require(s.Generation<2&&m.ControlMaster==p&&m.Map==Map.Internal&&SkillHealth.Balance256(m)==s.Life&&SkillHealth.Maximum256(m)==s.Maximum&&BattleCommand.PetOrder(m)==s.Order&&NecromancerMageMissiles.ActorSeed(m)==s.Seed&&JsonSerializer.Serialize(Flights(m))==JsonSerializer.Serialize(s.Flights),"Saved new Mage packet/path/resources/stream changed");
        p.MoveToWorld(s.Origin,Map.Maps[s.Map]);m.MoveToWorld(s.Origin,p.Map);var all=NecromancerMageMissiles.Snapshot();var f=s.Flights.First(x=>x.Charged!=null);var copy=f with{Id=all.NextId+1,NextUtc=TimedEffects.Now};NecromancerMageMissiles.Restore(all with{NextId=copy.Id,Bolts=all.Bolts.Append(copy).ToArray()});
        var expected=ChargedBolts.Advance(f.Charged.Path,f.Charged.OriginX,f.Charged.OriginY,f.X,f.Y,f.Charged.Next,SkillGeometry.Tiles(f.VelocityFp8/4096.0));Step(copy.Id);var moved=Flights(m).Single(x=>x.Id==copy.Id);Require(moved.Frame==copy.Frame+1&&moved.X==expected.X&&moved.Y==expected.Y&&moved.Charged.Next==expected.Next,"Reloaded charged path failed to resume once");
        all=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(all with{Bolts=all.Bolts.Where(x=>x.Id!=copy.Id).ToArray()});Require(JsonSerializer.Serialize(Flights(m))==JsonSerializer.Serialize(s.Flights),"Copied resumption advanced held originals");PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        int phase=1;Item helm=null;PlayerMobile p=null;MageWorldBody m=null;GraveyardSkeleton t=null;bool trained=false,launched=false;int hp=0,mana=0;long deadline=0;
        void Clean(){if(m!=null){Clear(m);m.Delete();}t?.Delete();p?.Delete();}
        void Start()
        {
            if(p==null){p=Owner(4,3);helm=PoisonDaggerChecks.Named(p,"Wormskull");FixtureNetwork.Authenticate(network,p,"mage-basic");}
            else{Reset(p,helm);}
            m=MagePetBuffChecks.Pet(p,phase);t=Target(p,m,phase==3?3:5);m.Ground.Store(m.WorldState.Controller with{NextUtc=TimedEffects.Now.AddMilliseconds(40)});NecromancerEffects.SetMana256(p,0);mana=NecromancerEffects.ManaBalance256(p);hp=SkillHealth.Balance256(t);trained=launched=false;deadline=Core.TickCount+12000;
        }
        void Wait()
        {
            try
            {
                Require(network.Running&&Core.TickCount<deadline&&m.WorldState.Controller.Fault==null,"Natural basic volley timeout/fault phase"+phase);
                if(!trained&&m.WorldState.Controller is{Mode:4} windup&&windup.ActionAt>windup.Frame){Require(MageBasicSkills.Copies(m)==1,"Soft point unlocked live triple");Train(p,"skeletalmage",5);Require(MageBasicSkills.Copies(m)==3,"Live hard5missing");var shown=MageBasicSkills.Packet(m);Inspect(p,m,shown.Minimum256,shown.Maximum256,MageBasicSkills.Count(m)*3);PetStatsGump.DisplayTo(p);Require(p.FindGump<PetStatsGump>()!=null,"Connected inspector did not open");p.CloseGump<PetStatsGump>();trained=true;}
                if(!launched&&Flights(m) is{Length:>0} flights){Require(trained&&flights.Length==3*MageBasicSkills.Count(m)&&flights.All(x=>x.Packet.Spell==MageBasicSkills.Ability(phase)&&x.Packet.Rank==5)&&SkillHealth.Balance256(t)==hp,$"Natural basic volley phase={phase},trained={trained},count={flights.Length},expected={3*MageBasicSkills.Count(m)},rank={m.Rank},hard={MagePetBuffs.HardRank(m)},hpLoss={hp-SkillHealth.Balance256(t)},frame={m.WorldState.Controller.Frame},packets={string.Join(",",flights.Select(x=>x.Packet.Spell+"/"+x.Packet.Rank))}");launched=true;m.SuspendActions();m.Ground.Store(m.WorldState.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});}
                if(SkillHealth.Balance256(t)==hp){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(trained&&launched&&NecromancerEffects.ManaBalance256(p)==mana&&m.WorldState.Controller.Attempts==1,"Natural basic collision/mana/controller");Clear(m);m.Delete();t.Delete();if(++phase<=3){Start();Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}p.Delete();Log.Information("mage-basics-live-result: passed");
            }
            catch(Exception e){Clean();Log.Error(e,"mage-basics-live-result: failed");}
        }
        Start();Log.Information("mage-basics-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
}
