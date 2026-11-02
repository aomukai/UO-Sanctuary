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

public static class HeavenBoltChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(HeavenBoltChecks));
    public sealed class Recipient:Daemon
    {
        internal int Notifications,LifeAtNotification;
        [Constructible]public Recipient(){}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool willKill)
        {Notifications++;LifeAtNotification=SkillHealth.Balance256(this);base.OnDamage(amount,from,willKill);}
        public override void Serialize(IGenericWriter w){base.Serialize(w);w.Write(0);}
        public override void Deserialize(IGenericReader r){base.Deserialize(r);r.ReadInt();}
    }
    private sealed record Saved(uint Owner,int Map,Point3D Origin,int Mana,int Life,HolyBoltFlight Flight,int Generation,uint DeletedCollider=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"heaven-bolt-checks.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"FoH shared typed child/source flight",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("heaven-bolt-check",Check);Register("heaven-bolt-reload-check",()=>FixtureNetwork.Connected("heaven-bolt-reload-result",Reload));
        foreach(string mode in new[]{"left","right","quick"}){string label="heaven-bolt-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    private static PlayerMobile Owner()
    {
        var p=BlessedHammerDamageChecks.Owner();
        // These piercing tests need fourteen clear cells; the shared melee
        // origin only guarantees its shorter corridor.
        p.MoveToWorld(ZealChecks.Origin(p,point=>Enumerable.Range(0,15).Any(dx=>!p.Map.CanSpawnMobile(point.X+dx,point.Y,point.Z)
            ||!p.Map.LineOfSight(new Point3D(point.X,point.Y,point.Z+12),new Point3D(point.X+dx,point.Y,point.Z+12)))
            ||HolyBolts.Snapshot().Any(f=>f.Map==p.Map.MapID&&Math.Abs(f.X-point.X)<30&&Math.Abs(f.Y-point.Y)<30)
            ||BlessedHammers.Snapshot().Any(f=>f.Motion!=null&&f.Map==p.Map.MapID&&Math.Abs(f.OriginX+f.XFp16/131072.0-point.X)<30&&Math.Abs(f.OriginY+f.YFp16/131072.0-point.Y)<30)),p.Map);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.FistOfTheHeavens)&&SkillBindings.Instance.Assign(p,false,CombatAbility.FistOfTheHeavens)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.FistOfTheHeavens),"FoH bolt bindings");return p;
    }
    private static T Target<T>(PlayerMobile p,T t,int dx=6) where T:BaseCreature
    {
        t.NoKillAwards=t.Frozen=t.CantWalk=true;t.SetHits(30000);t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();
        foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static HolyBoltFlight Create(PlayerMobile p)
    {HolyBolts.CastSecondary(p,p.Location,new(p.X+12,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.FistOfTheHeavens),TimedEffects.Now);return HolyBolts.Snapshot().Where(f=>f.Owner==p.Serial.Value).MaxBy(f=>f.Id);}
    private static void Through(long id,int count,bool actions=true)
    {for(int i=0;i<count&&HolyBolts.State(id) is{} f;i++){HolyBoltFlights.TickFlight(id,f.NextUtc,actions);}}
    private static void Clear(PlayerMobile p)
    {HolyBolts.Restore(HolyBolts.Snapshot().Where(f=>f.Owner!=p.Serial.Value).ToArray());HeavenFists.Restore(HeavenFists.Snapshot().Where(f=>f.Owner!=p.Serial.Value).ToArray());}
    private static void Clean(PlayerMobile p,params Mobile[] targets)
    {Clear(p);AmazonSlowMissiles.Clear(p);foreach(var t in targets){t.Corpse?.Delete();t.Delete();}p.Corpse?.Delete();p.Delete();}
    private static void Check()
    {
        // The parent DLL can reach this regression before any new API is JITted.
        var p=Owner();try{Require(CombatPotions.Apply(p,3),"Actual child launch potion");var f=Create(p);Require(f.Min256==11776&&f.Max256==14720,"FoH child incorrectly snapshots potion into original magic");}finally{Clean(p);}
        var seed=HolyBoltFlights.SeedLow;var roll=HolyBolts.Roll;
        try
        {
            HolyBoltFlights.SeedLow=()=>0;HolyBolts.Roll=_=>throw new InvalidOperationException("FoH source child used compatibility RNG");
            Potions();SpeciesAndDeath();PathChecks();Controls();Atomic();Save();
            Log.Information("FoH child:typed impact T29;Holy3 original magic only;no ally healing;source2304/Activate2/50frames;12442then12034FP/RNG;birth-time hard synergy/slow;atomic/legacy;original paid parent-created moving child staged with explicit inspection clock hold");
        }
        finally{HolyBoltFlights.SeedLow=seed;HolyBolts.Roll=roll;}
    }
    private static void Potions()
    {
        foreach(string mode in new[]{"active","after-launch","removed","expired","mixed"})
        {
            var p=Owner();var t=Target(p,new Recipient());
            try
            {
                if(mode!="after-launch"){Require(CombatPotions.Apply(p,3),"Child potion "+mode);}var f=Create(p) with{Min256=25600,Max256=25600};
                Require(f.Captured is{Modifiers.Potion:0},"Child non-potion snapshot");if(mode=="after-launch"){Require(CombatPotions.Apply(p,3),"Child impact potion");}
                if(mode=="removed"){TimedEffects.Clear(p);}if(mode=="expired"){TimedEffects.Settle(p,TimedEffects.Now.AddMinutes(2));}
                if(mode=="mixed"){t.SetResistance(ResistanceType.Fire,50);t.SetResistance(ResistanceType.Cold,100);t.SetResistance(ResistanceType.Energy,-50);}
                int life=SkillHealth.Balance256(t);HolyBoltFlights.Impact(p,t,f,TimedEffects.Now);
                Require(life-SkillHealth.Balance256(t)==(mode is "removed" or "expired"?25600:mode=="mixed"?33280:35840)&&t.Notifications==1&&t.LifeAtNotification==life
                    &&NativeMonsterReturn.Session(t).Packets==1&&t.FindDamageEntryFor(p)?.Damager==p&&!TimedEffects.Snapshot().Any(s=>s.Owner==t.Serial.Value&&s.PoisonClock!=null),"Child potion/ordered packet "+mode+" loss="+(life-SkillHealth.Balance256(t)));
            }
            finally{Clean(p,t);}
        }
    }
    private static void SpeciesAndDeath()
    {
        foreach(string kind in new[]{"undead-immune","demon-resist","beast"})
        {
            var p=Owner();BaseCreature t=kind=="undead-immune"?new LichLord():kind=="demon-resist"?new DemonKnight():new Ogre();
            if(kind!="beast"){MonsterVariants.Assign(t,MonsterDifficulty.Hell);}Target(p,t);
            try
            {
                if(kind!="beast"){Require(NamedElements.MagicResistance(t)==(kind=="undead-immune"?100:25),"Actual species magic defense");}
                var f=Create(p) with{Min256=25600,Max256=25600};int life=SkillHealth.Balance256(t);ulong seed=HolyBoltFlights.Impact(p,t,f,TimedEffects.Now);
                Require(life-SkillHealth.Balance256(t)==(kind=="beast"?0:25600)&&NativeMonsterReturn.Session(t).Packets==(kind=="beast"?0:1)&&seed==f.Motion.Seed,"Holy3 species bypass/non-damaging beast "+kind);
            }
            finally{Clean(p,t);}
        }
        var owner=Owner();var victim=Target(owner,new Recipient());var created=CorpseRecoveryHooks.Created;bool died=false;
        try
        {
            PoisonDaggerChecks.Named(owner,"Greyform");SkillHealth.SetBalance256(owner,SkillHealth.Maximum256(owner)-20*256-17);int life=SkillHealth.Balance256(owner);
            PaladinAuras.ElementalWeapon(new(victim.Serial.Value,owner.Serial.Value,CombatAbility.Thorns,1,1000,TimedEffects.Now.AddDays(1)));
            var f=Create(owner) with{Min256=25600,Max256=25600};SkillHealth.SetBalance256(victim,256);
            CorpseRecoveryHooks.Created=c=>{if(c.Owner==victim){died=SkillHealth.Balance256(victim)==0&&victim.FindDamageEntryFor(owner)?.Damager==owner;}created?.Invoke(c);};
            HolyBoltFlights.Impact(owner,victim,f,TimedEffects.Now);
            Require(!victim.Alive&&died&&ClassLoot.Killer(victim)==owner&&SkillHealth.Balance256(owner)==life&&NativeMonsterReturn.Session(victim).Packets==1,"Child ordered death/credit or forbidden weapon leech/Thorns");
        }
        finally{CorpseRecoveryHooks.Created=created;PaladinAuras.Clear(victim);Clean(owner,victim);}
    }
    private static void PathChecks()
    {
        var p=Owner();var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.Name="SacrificeAllocationFixture";ally.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);
        var pet=Target(p,new GraveyardSkeleton(),3);Require(pet.SetControlMaster(ally),"Child allied pet fixture");SkillHealth.SetBalance256(pet,SkillHealth.Maximum256(pet)-10*256-17);int ph=SkillHealth.Balance256(pet);
        var beast=Target(p,new Ogre(),4);var demon=Target(p,new Recipient());var undead=Target(p,new ConversionContactChecks.Recipient(),10);
        try
        {
            SkillHealth.SetBalance256(ally,SkillHealth.Maximum256(ally)-50*256-17);int ah=SkillHealth.Balance256(ally),bh=SkillHealth.Balance256(beast),dh=SkillHealth.Balance256(demon),uh=SkillHealth.Balance256(undead);
            var f=Create(p);Require(f is{Frame:0,Vx:.28125,Vy:0,Motion.VelocityFp8:2304}&&f.Min256==11776&&f.Max256==14720&&f.Motion.Seed==((ulong)666<<32),"Child source factory/rank1hardHolyBolt1");
            Require(ClassCreation.Profiles.TryTrain(p.Serial,"paladin.holybolt",out _)&&ClassCreation.Profiles.TryTrain(p.Serial,"paladin.fistoftheheavens",out _),"Later child rank/synergy");
            Through(f.Id,20);var mid=HolyBolts.State(f.Id);
            Require(SkillHealth.Balance256(ally)==ah&&SkillHealth.Balance256(pet)==ph&&SkillHealth.Balance256(beast)==bh&&dh-SkillHealth.Balance256(demon)==12442&&demon.Notifications==1&&mid.Motion.Seed==666&&mid.LastCollision==demon.Serial.Value,"Child ally/beast piercing/first seeded captured damage");
            mid.Motion.Steps.Cells[0]=new(-1,-1);Require(HolyBolts.State(f.Id).Motion.Steps.Cells[0].X>=0,"Child cells alias snapshot");
            Through(f.Id,20);Require(uh-SkillHealth.Balance256(undead)==12034&&undead.Notifications==1&&HolyBolts.State(f.Id).Motion.Seed==1193071124610UL,"Child second source draw/piercing");
            var fresh=Create(p);Require(fresh.Rank==2&&fresh.Min256==15308&&fresh.Max256==18636,"Child did not use factory-time rank/current hard synergy");
            Through(f.Id,20);Require(HolyBolts.State(f.Id)==null&&dh-SkillHealth.Balance256(demon)==12442,"Child terminal lifetime/repeat collider");
        }
        finally{Clean(p,pet,ally,beast,demon,undead);}
        var owner=Owner();var near=Target(owner,new Recipient(),1);
        try{var f=Create(owner);int life=SkillHealth.Balance256(near);Through(f.Id,1);Require(SkillHealth.Balance256(near)==life,"Child collides before Activate2");Through(f.Id,1);Require(life-SkillHealth.Balance256(near)==12442,"Child fails activation at frame2");}
        finally{Clean(owner,near);}
    }
    private static void Controls()
    {
        foreach(string mode in new[]{"miss","wall","offline","internal","facet","slow","death","respec","wardrobe","unsupported"})
        {
            var p=Owner();var t=Target(p,new Recipient());Item wall=null;
            try
            {
                if(mode=="slow"){AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,t.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());}
                var f=Create(p);int life=SkillHealth.Balance256(t);
                if(mode=="unsupported"){t.IsParagon=true;life=SkillHealth.Balance256(t);}if(mode=="miss"){t.Location=new(t.X,t.Y+3,t.Z);}if(mode=="wall"){wall=new Static(0x80);wall.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);}
                if(mode=="internal"){p.Internalize();}if(mode=="facet"){p.MoveToWorld(p.Location,Map.Felucca);}
                if(mode=="death"){p.Kill();Require(HolyBolts.State(f.Id)==null,"Death retained source child");}
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(HolyBolts.State(f.Id)==null,"Respec retained source child");}
                if(mode=="wardrobe"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Druid,out _,out _)&&HolyBolts.State(f.Id)==null,"Wardrobe retained source child");}
                if(mode=="slow"){Require(f.Motion.VelocityFp8==1152&&f.Vx==.140625&&f.Min256==5888&&f.Max256==7360,"Child slow velocity/damage");AmazonSlowMissiles.Clear(p);}
                if(mode=="unsupported"){Through(f.Id,30);Require(HolyBolts.State(f.Id) is{Motion.Fault:not null}&&HolyBolts.State(f.Id).Motion.Seed==f.Motion.Seed,"Child unsupported receiver must suspend before RNG");}
                Through(f.Id,60,mode!="offline");Require(HolyBolts.State(f.Id)==null&&life-SkillHealth.Balance256(t)==(mode=="slow"?6554:0),"Child host control/expiry "+mode+" loss="+(life-SkillHealth.Balance256(t)));
            }
            finally{wall?.Delete();MonsterUninterruptibles.Clear(t);Clean(p,t);}
        }
        foreach(int frame in new[]{48,49})
        {var p=Owner();var t=Target(p,new Recipient(),1);try{var f=Create(p);HolyBolts.Update(f with{Frame=frame});int life=SkillHealth.Balance256(t);Through(f.Id,1);Require(life-SkillHealth.Balance256(t)==(frame==48?12442:0)&&(HolyBolts.State(f.Id)==null)==(frame==49),"Child collidable49/terminal50");}finally{Clean(p,t);}}
    }
    private static void Atomic()
    {
        var p=Owner();try
        {
            var f=Create(p);var all=HolyBolts.Snapshot();bool refused=false;
            try{HolyBolts.Restore(all.Append(f with{Id=f.Id+1000000,Motion=f.Motion with{VelocityFp8=3840}}).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&HolyBolts.Snapshot().SequenceEqual(all),"Child accepted wrong-route velocity or partially restored");refused=false;
            try{HolyBolts.Restore(all.Append(f).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&HolyBolts.Snapshot().SequenceEqual(all),"Child duplicate partially restored");
            var legacy=f with{Motion=null,Captured=null};HolyBolts.Restore(all.Select(x=>x.Id==f.Id?legacy:x).ToArray());Require(!JsonSerializer.Serialize(legacy).Contains("Motion")&&!JsonSerializer.Serialize(legacy).Contains("Captured"),"Legacy child gained guessed capture/motion");
        }
        finally{Clean(p);}
    }
    private static void Save()
    {
        var p=Owner();var primary=Target(p,new HeavenFistDamageChecks.Recipient(),3);var target=Target(p,new Recipient());NecromancerEffects.SetMana256(p,6417);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-7*256-17);
        Require(PaladinCombat.Release(p,CombatAbility.FistOfTheHeavens,primary.Serial,primary.Location),"Paid original parent release");var strike=HeavenFists.Snapshot().Single(f=>f.Owner==p.Serial.Value);
        // Controlled primary completion creates the actual child through hit22;
        // subsequent wire cases exercise ordinary primary delay plus child travel.
        HeavenFists.Impact(p,strike);HeavenFists.Restore(HeavenFists.Snapshot().Where(x=>x.Owner!=p.Serial.Value).ToArray());
        var f=HolyBolts.Snapshot().Single(x=>x.Owner==p.Serial.Value&&x.Vx==.28125&&x.Vy==0);
        HolyBolts.Restore(HolyBolts.Snapshot().Where(x=>x.Owner!=p.Serial.Value||x.Id==f.Id).ToArray());primary.Delete();target.Delete();Through(f.Id,2);
        f=HolyBolts.State(f.Id) with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention),UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention).AddSeconds(2)};HolyBolts.Update(f);
        Require(NecromancerEffects.ManaBalance256(p)==17&&f.Frame==2&&f.Motion.Seed==((ulong)666<<32),"Original paid child save state");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,p.Map.MapID,p.Location,17,SkillHealth.Balance256(p),f,0)));p.Internalize();
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var f=HolyBolts.State(s.Flight.Id);var expected=s.DeletedCollider==0?s.Flight:s.Flight with{LastCollision=0};
        Require(s.Generation<2&&f==expected&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life&&SkillBindings.Instance.Get(p).Right==CombatAbility.FistOfTheHeavens,"Original saved child/cells/seed/resources/binding");
        var account=FixtureNetwork.Authenticate(network,p,"foh-child-reload-");p.MoveToWorld(s.Origin,Map.Maps[s.Map]);var t=Target(p,new Recipient());t.MoveToWorld(new((int)Math.Ceiling(f.X)+3,(int)Math.Round(f.Y),f.Z),p.Map);int life=SkillHealth.Balance256(t);
        // Same original flight and random stream; only inspection clocks rearm.
        HolyBolts.Update(f with{NextUtc=TimedEffects.Now,UntilUtc=TimedEffects.Now.AddSeconds(2)});long deadline=Core.TickCount+6000;
        void Observe()
        {
            try
            {
                if(NativeMonsterReturn.Session(t).Packets==0){Require(Core.TickCount<deadline,$"Child natural resume timeout:flight={HolyBolts.State(f.Id)},target={t.Location}");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                var active=HolyBolts.State(f.Id);Require(life-SkillHealth.Balance256(t)==(s.Generation==0?12442:12034)&&t.Notifications==1&&active.Motion.Seed==(s.Generation==0?666UL:1193071124610UL)
                    &&active.LastCollision==t.Serial.Value&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life,"Original child natural impact/seed/once resources");
                active=active with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention),UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention).AddSeconds(2)};HolyBolts.Update(active);
                p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();t.Delete();
                File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Flight=active,Generation=s.Generation+1,DeletedCollider=t.Serial.Value}));Log.Information("heaven-bolt-reload-result: passed");
            }
            catch(Exception e){t.Delete();p.Internalize();Log.Error(e,"heaven-bolt-reload-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var primary=Target(p,new HeavenFistDamageChecks.Recipient(),3);var t=Target(p,new Recipient());Require(PaladinCombat.ValidTarget(p,CombatAbility.FistOfTheHeavens,primary.Serial,primary.Location),"Child input primary eligibility");
        var account=FixtureNetwork.Authenticate(network,p,"foh-child-input-");var seed=HolyBoltFlights.SeedLow;var roll=HolyBolts.Roll;HolyBoltFlights.SeedLow=()=>0;HolyBolts.Roll=_=>throw new InvalidOperationException("Natural child used compatibility RNG");NecromancerEffects.SetMana256(p,6417);int life=SkillHealth.Balance256(t);bool began=false;long deadline=Core.TickCount+8000;
        void Cleanup(){HolyBoltFlights.SeedLow=seed;HolyBolts.Roll=roll;Clean(p,primary,t);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Child input timeout");if(ClassCombat.For(p).PendingAbility==CombatAbility.FistOfTheHeavens&&!began){Require(NecromancerEffects.ManaBalance256(p)==6417&&SkillHealth.Balance256(t)==life,"Child input paid/hit before windup");began=true;Log.Information("{Label}: windup",label);}
                if(NativeMonsterReturn.Session(t).Packets==0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                var f=HolyBolts.Snapshot().Single(x=>x.Owner==p.Serial.Value&&x.Vx==.28125&&x.Vy==0);
                Require(began&&life-SkillHealth.Balance256(t)==12442&&t.Notifications==1&&t.LifeAtNotification==life&&f.Motion.Seed==666&&f.LastCollision==t.Serial.Value&&f.Frame>=2&&NecromancerEffects.ManaBalance256(p)==17,"Actual parent/child delayed input/seed/once payment");Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.FistOfTheHeavens,(int)DiabloClass.Paladin,primary.Serial.Value,primary.X,primary.Y,primary.Z);Observe();});
    }
}
