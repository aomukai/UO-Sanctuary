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

public static class HolyBoltFlightChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(HolyBoltFlightChecks));
    private sealed record Saved(uint Owner,int Map,Point3D Origin,int Mana,HolyBoltFlight Flight,int Generation,uint DeletedCollider=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"holy-bolt-flight-checks.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Holy Bolt saved source cells,RNG and natural resume",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("holy-bolt-flight-check",Check);Register("holy-bolt-flight-reload-check",()=>FixtureNetwork.Connected("holy-bolt-flight-reload-result",Reload));
        foreach(string mode in new[]{"left","right","quick"})
        {string label="holy-bolt-flight-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    private static PlayerMobile Owner()
    {
        var p=SacrificeContactChecks.Owner();p.MoveToWorld(ZealChecks.Origin(p),p.Map);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.HolyBolt)&&SkillBindings.Instance.Assign(p,false,CombatAbility.HolyBolt)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.HolyBolt),"Holy Bolt flight bindings");return p;
    }
    private static T Target<T>(PlayerMobile p,T t,int dx=6) where T:BaseCreature
    {
        t.NoKillAwards=t.Frozen=t.CantWalk=true;t.SetHits(30000);t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();
        foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static HolyBoltFlight Create(PlayerMobile p)
    {HolyBolts.Cast(p,new(p.X+12,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.HolyBolt));return HolyBolts.Snapshot().Where(x=>x.Owner==p.Serial.Value).MaxBy(x=>x.Id);}
    private static void Through(long id,int count,bool actions=true)
    {for(int i=0;i<count&&HolyBolts.State(id) is{} f;i++){HolyBoltFlights.TickFlight(id,f.NextUtc,actions);}}
    private static void Clean(PlayerMobile p,params Mobile[] targets)
    {HolyBoltFlights.Cancel(p);AmazonSlowMissiles.Clear(p);foreach(var t in targets){t.Corpse?.Delete();t.Delete();}p.Corpse?.Delete();p.Delete();}
    private static void Check()
    {
        // This first regression also executes against the parent content DLL:
        // no reference to the new motion API is needed before its outcome.
        var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());int life=SkillHealth.Balance256(t);Create(p);
        Timer.StartTimer(TimeSpan.FromMilliseconds(800),()=>Core.LoopContext.Post(()=>
        {
            try
            {
                Require(SkillHealth.Balance256(t)==life,"Disconnected Holy Bolt replayed combat instead of harmless aging");
                // Remove the first fixture through the existing compatibility
                // API so the parent regression cannot fail on a missing API.
                HolyBolts.Restore(HolyBolts.Snapshot().Where(x=>x.Owner!=p.Serial.Value).ToArray());t.Delete();p.Delete();
                Details();Log.Information("holy-bolt-flight-result: passed");
            }
            catch(Exception e)
            {HolyBolts.Restore(HolyBolts.Snapshot().Where(x=>x.Owner!=p.Serial.Value).ToArray());t.Delete();p.Delete();Log.Error(e,"holy-bolt-flight-result: failed");}
        }));
    }
    private static void Details()
    {
        var seed=HolyBoltFlights.SeedLow;var roll=HolyBolts.Roll;
        try
        {
            HolyBoltFlights.SeedLow=()=>0;HolyBolts.Roll=_=>throw new InvalidOperationException("Source flight used global compatibility RNG");
            PathHealingAndSeed();Controls();Atomic();Save();
            Log.Information("Holy Bolt flight:source50/3840/cells;1178FP current-Prayer heal then4226FP captured magic;beast consumes no RNG;last-collider/deep copy;9lifecycle/wall/slow controls;final49/50;atomic bad source/legacy omission;original paid moving flight staged");
        }
        finally{HolyBoltFlights.SeedLow=seed;HolyBolts.Roll=roll;}
    }
    private static void PathHealingAndSeed()
    {
        var p=Owner();var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.Name="SacrificeAllocationFixture";ally.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);
        var beast=Target(p,new Ogre(),4);var t=Target(p,new ConversionContactChecks.Recipient());
        try
        {
            SkillHealth.SetBalance256(ally,SkillHealth.Maximum256(ally)-50*256-17);int ah=SkillHealth.Balance256(ally),life=SkillHealth.Balance256(t),bh=SkillHealth.Balance256(beast);var f=Create(p);
            Require(f is{Frame:0,Vx:.46875,Vy:0,Motion.VelocityFp8:3840}&&f.Motion.Seed==((ulong)666<<32)&&f.Motion.Steps.Cells.SequenceEqual(new[]{new SourceMissileCell(p.X*2,p.Y*2)}),"Holy Bolt source factory");
            for(int i=0;i<4;i++){Require(ClassCreation.Profiles.TryTrain(p.Serial,"paladin.prayer",out _),"Live Prayer hard rank");}
            Require(ClassCreation.Profiles.TryTrain(p.Serial,"paladin.holybolt",out _)&&ClassCreation.Profiles.TryTrain(p.Serial,"paladin.fistoftheheavens",out _),"Post-launch Holy Bolt/FoH ranks");
            Through(f.Id,1);var first=HolyBolts.State(f.Id);
            Require(first.Frame==1&&first.X==p.X+.46875&&first.Motion.Seed==f.Motion.Seed&&first.Motion.Steps.Cells.SequenceEqual(new[]{new SourceMissileCell(p.X*2+1,p.Y*2)})
                &&SkillHealth.Balance256(ally)==ah&&SkillHealth.Balance256(t)==life,"Holy Bolt first source crossing/timing");
            first.Motion.Steps.Cells[0]=new(-1,-1);Require(HolyBolts.State(f.Id).Motion.Steps.Cells[0].X>=0,"Holy Bolt snapshot aliases live source cells");
            Through(f.Id,2);var healed=HolyBolts.State(f.Id);
            Require(SkillHealth.Balance256(ally)==ah+1178&&healed.LastCollision==ally.Serial.Value&&healed.Motion.Seed==666,"Holy Bolt original-rank/current-Prayer heal and first seeded draw");
            Through(f.Id,7);Require(SkillHealth.Balance256(beast)==bh&&HolyBolts.State(f.Id).Motion.Seed==666,"Beast collision consumed RNG or took damage");
            Through(f.Id,6);var damaged=HolyBolts.State(f.Id);
            Require(life-SkillHealth.Balance256(t)==4226&&t.Notifications==1&&damaged.LastCollision==t.Serial.Value&&damaged.Motion.Seed==1193071124610UL
                &&SkillHealth.Balance256(ally)==ah+1178,"Holy Bolt failed source piercing/second RNG/captured damage/last collider");
            Through(f.Id,50);Require(HolyBolts.State(f.Id)==null&&life-SkillHealth.Balance256(t)==4226,"Holy Bolt expiry/repeated hit");
        }
        finally{Clean(p,ally,beast,t);}
    }
    private static void Controls()
    {
        foreach(string mode in new[]{"miss","wall","offline","internal","facet","slow","death","respec","unsupported"})
        {
            var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());Item wall=null;
            try
            {
                if(mode=="slow"){AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,t.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());}
                var f=Create(p);int life=SkillHealth.Balance256(t);
                if(mode=="unsupported"){t.IsParagon=true;life=SkillHealth.Balance256(t);}
                if(mode=="miss"){t.Location=new(t.X,t.Y+3,t.Z);}
                if(mode=="wall"){wall=new Static(0x80);wall.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);}
                if(mode=="internal"){p.Internalize();}
                if(mode=="facet"){p.MoveToWorld(p.Location,p.Map==Map.Felucca?Map.Trammel:Map.Felucca);}
                if(mode=="death"){p.Kill();Require(HolyBolts.State(f.Id)==null,"Death retained source Holy Bolt");}
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(HolyBolts.State(f.Id)==null,"Respec retained source Holy Bolt");}
                if(mode=="slow")
                {Require(f.Motion.VelocityFp8==1920&&f.Vx==.234375&&f.Min256==1536&&f.Max256==3072,"Holy Bolt slow source factory");AmazonSlowMissiles.Clear(p);}
                if(mode=="unsupported")
                {Through(f.Id,20);Require(HolyBolts.State(f.Id) is{Motion.Fault:not null} suspended&&suspended.Motion.Seed==f.Motion.Seed,"Unsupported source impact did not suspend before RNG");}
                Through(f.Id,60,mode!="offline");Require(HolyBolts.State(f.Id)==null&&life-SkillHealth.Balance256(t)==(mode=="slow"?2202:0),"Holy Bolt control/expiry "+mode+" loss="+(life-SkillHealth.Balance256(t)));
            }
            finally{wall?.Delete();Clean(p,t);}
        }
        foreach(int frame in new[]{48,49})
        {
            var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient(),1);
            try
            {
                var f=Create(p);HolyBolts.Update(f with{Frame=frame,X=p.X+.46875,Motion=f.Motion with{Steps=new([new(p.X*2+1,p.Y*2)])}});int life=SkillHealth.Balance256(t);Through(f.Id,1);
                Require(life-SkillHealth.Balance256(t)==(frame==48?3738:0)&&(HolyBolts.State(f.Id)==null)==(frame==49),"Holy Bolt collidable49/terminal50 boundary");
            }
            finally{Clean(p,t);}
        }
    }
    private static void Atomic()
    {
        var p=Owner();try
        {
            var f=Create(p);var receiver=Target(p,new ConversionContactChecks.Recipient());
            HolyBolts.Update(f with{LastCollision=receiver.Serial.Value});HolyBoltFlights.RestoreReferences();
            Require(HolyBolts.State(f.Id).LastCollision==receiver.Serial.Value,"Live saved collider was cleared");receiver.Delete();HolyBoltFlights.RestoreReferences();
            Require(HolyBolts.State(f.Id).LastCollision==0,"Absent collider retained a reusable native serial");
            f=HolyBolts.State(f.Id);var all=HolyBolts.Snapshot();bool refused=false;
            try{HolyBolts.Restore(all.Append(f with{Id=f.Id+1000000,Motion=f.Motion with{Steps=new([new(-1,-1)])}}).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&HolyBolts.Snapshot().SequenceEqual(all),"Bad source tail partially replaced Holy Bolt pool");
            refused=false;try{HolyBolts.Restore(all.Append(f with{Id=f.Id+1000000,Motion=f.Motion with{VelocityFp8=3841}}).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&HolyBolts.Snapshot().SequenceEqual(all),"Invalid source velocity partially replaced pool");
            var legacy=f with{Motion=null};HolyBolts.Restore(all.Select(x=>x.Id==f.Id?legacy:x).ToArray());
            Require(HolyBolts.State(f.Id).Motion==null&&JsonSerializer.Serialize(legacy).IndexOf("Motion",StringComparison.Ordinal)<0,"Historical flight gained guessed source motion");
        }
        finally{HolyBolts.Restore(HolyBolts.Snapshot().Where(x=>x.Owner!=p.Serial.Value).ToArray());p.Delete();}
    }
    private static void Save()
    {
        var p=Owner();NecromancerEffects.SetMana256(p,529);int mana=NecromancerEffects.ManaBalance256(p);
        Require(PaladinCombat.Release(p,CombatAbility.HolyBolt,Serial.Zero,new(p.X+12,p.Y,p.Z)),"Paid Holy Bolt source release");
        var f=HolyBolts.Snapshot().Single(x=>x.Owner==p.Serial.Value);Through(f.Id,2);f=HolyBolts.State(f.Id) with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention),UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention).AddSeconds(2)};HolyBolts.Update(f);
        Require(mana-NecromancerEffects.ManaBalance256(p)==512&&f.Frame==2&&f.X==p.X+.9375,"Moving paid Holy Bolt save seed");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,p.Map.MapID,p.Location,NecromancerEffects.ManaBalance256(p),f,0)));p.Internalize();
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var original=HolyBolts.State(s.Flight.Id);
        var expected=s.DeletedCollider==0?s.Flight:s.Flight with{LastCollision=0};
        Require(s.DeletedCollider==0||s.DeletedCollider==s.Flight.LastCollision,"Saved collider cleanup fixture lost its original reference");
        Require(s.Generation<2&&original==expected&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillBindings.Instance.Get(p).Right==CombatAbility.HolyBolt,"Saved moving source Holy Bolt identity/seed/cells/resources/binding/reference normalization");
        var account=FixtureNetwork.Authenticate(network,p,"holy-bolt-source-reload-");p.MoveToWorld(s.Origin,Map.Maps[s.Map]);var t=Target(p,new ConversionContactChecks.Recipient());
        t.MoveToWorld(new((int)Math.Ceiling(original.X)+3,(int)Math.Round(original.Y),original.Z),p.Map);int life=SkillHealth.Balance256(t);
        // Rearm this actual original saved flight,not a reconstructed copy.
        HolyBolts.Update(original with{NextUtc=TimedEffects.Now,UntilUtc=TimedEffects.Now.AddSeconds(2)});long deadline=Core.TickCount+5000;
        HolyBoltFlight last=HolyBolts.State(original.Id);Require(last!=null,"Rearmed original Holy Bolt disappeared during authentication/placement");
        void Observe()
        {
            try
            {
                if(HolyBolts.State(original.Id) is{} seen){last=seen;}
                if(NativeMonsterReturn.Session(t).Packets==0){Require(Core.TickCount<deadline,$"Natural Holy Bolt resume timeout:exists={HolyBolts.State(original.Id)!=null},lastFrame={last?.Frame},lastXY={last?.X}/{last?.Y},target={t.X}/{t.Y},maps={p.Map?.MapID}/{t.Map?.MapID},connected={p.NetState?.Running},candidate={HolyBolts.Candidate(p,t)},fault={last?.Motion?.Fault},lastSeed={last?.Motion?.Seed},lastCollider={last?.LastCollision},targetSerial={t.Serial.Value},sourceSize={CreatureProfiles.For(t).Traits.Size}");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(life-SkillHealth.Balance256(t)==(s.Generation==0?3738:4226)&&t.Notifications==1&&NecromancerEffects.ManaBalance256(p)==s.Mana,$"Natural original source Holy Bolt resume changed seed/damage/resources:generation={s.Generation},loss={life-SkillHealth.Balance256(t)},notifications={t.Notifications},mana={NecromancerEffects.ManaBalance256(p)}/{s.Mana},seed={HolyBolts.State(original.Id)?.Motion?.Seed},frame={HolyBolts.State(original.Id)?.Frame},lastCollider={HolyBolts.State(original.Id)?.LastCollision},targetSerial={t.Serial.Value},life={life}/{SkillHealth.Balance256(t)}");
                var active=HolyBolts.State(original.Id);Require(active is{Frame:>2}&&active.Motion.Seed==(s.Generation==0?666UL:1193071124610UL)&&active.LastCollision==t.Serial.Value,"Natural resumed source payload missing");
                // Keep the same moving original for the next real reload;no
                // HP,mana,seed or path reset. Only its inspection clock holds.
                active=active with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention),UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention).AddSeconds(2)};HolyBolts.Update(active);
                p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();t.Delete();
                File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Flight=active,Generation=s.Generation+1,DeletedCollider=t.Serial.Value}));Log.Information("holy-bolt-flight-reload-result: passed");
            }
            catch(Exception e){t.Delete();p.Internalize();Log.Error(e,"holy-bolt-flight-reload-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);
    }
    private static void Input(NetState network,string label)
    {
        // Origin offsets the owner two cells into a six-cell checked corridor.
        var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient(),3);
        Require(PaladinCombat.ValidTarget(p,CombatAbility.HolyBolt,t.Serial,t.Location),"Source Holy Bolt input fixture must provide an eligible visible target");
        var account=FixtureNetwork.Authenticate(network,p,"holy-bolt-source-input-");
        var seed=HolyBoltFlights.SeedLow;var roll=HolyBolts.Roll;HolyBoltFlights.SeedLow=()=>0;HolyBolts.Roll=_=>throw new InvalidOperationException("Natural source used global RNG");NecromancerEffects.SetMana256(p,529);
        int life=SkillHealth.Balance256(t);bool began=false;long deadline=Core.TickCount+6000;
        void Cleanup(){HolyBoltFlights.SeedLow=seed;HolyBolts.Roll=roll;Clean(p,t);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Source Holy Bolt input timeout");
                if(ClassCombat.For(p).PendingAbility==CombatAbility.HolyBolt&&!began)
                {Require(NecromancerEffects.ManaBalance256(p)==529&&SkillHealth.Balance256(t)==life,"Source input paid/hit before event");began=true;Log.Information("{Label}: windup",label);}
                if(NativeMonsterReturn.Session(t).Packets==0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                var f=HolyBolts.Snapshot().Single(x=>x.Owner==p.Serial.Value);
                Require(began&&life-SkillHealth.Balance256(t)==3738&&t.Notifications==1&&f.Motion.Seed==666&&f.LastCollision==t.Serial.Value&&f.Frame>1
                    &&NecromancerEffects.ManaBalance256(p)==17,"Actual source Holy Bolt travel/RNG/once payment failed");Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.HolyBolt,(int)DiabloClass.Paladin,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
