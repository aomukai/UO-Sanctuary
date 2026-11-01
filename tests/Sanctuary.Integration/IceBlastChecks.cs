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

public static class IceBlastChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(IceBlastChecks));
    private sealed record Saved(uint Owner,uint Target,int Map,Point3D Point,int Mana,int Life,SorceressSpellFlight Flight,NamedControlState Control,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"ice-blast-fixtures.json");
    public static void Configure()
    {
        Register("ice-blast-check",Check);Register("ice-blast-reload-check",()=>FixtureNetwork.Connected("ice-blast-reload-result",Reload));
        foreach(bool quick in new[]{false,true})
        {bool q=quick;string label=q?"ice-blast-quick":"ice-blast-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,q)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Player Ice Blast freeze-only hit, input and saves",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=FireBoltChecks.Owner(CombatAbility.IceBolt,7010);p.Name="IceBlastFixture";
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.iceblast",out _),"Ice Blast prerequisite/train");
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.IceBlast)&&SkillBindings.Instance.Assign(p,false,CombatAbility.IceBlast)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.IceBlast),"Ice Blast bindings");return p;
    }
    private static long Create(PlayerMobile p)=>SorceressSpellMissiles.Commit(SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.IceBlast),TimedEffects.Now));
    private static SorceressSpellFlight State(long id)=>FireBoltChecks.State(id);
    private static DateTime Finish(long id)=>IceBoltChecks.Finish(id);
    private static NamedControlState Frozen(Mobile t)
    {var c=NamedControl.State(t);Require(c?.Freeze is{Removed:false},"Missing Ice Blast source freeze");return c;}
    private static void NoChill(Mobile t)=>Require(!TimedEffects.TryActor(t,out var a)||a.Cold==null&&a.ChillPercent==0,"Dmg4 retained a duplicate ordinary cold lane");
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.IceBlast);string[] damage=["Ice Bolt","Blizzard","Frozen Orb"];
        int Basis(int rank,bool max)
        {int n=max?24:16;for(int level=2;level<=rank;level++){n+=(level<=8?14:level<=16?28:level<=22?42:level<=28?56:70)+(max?1:0);}return n*128;}
        for(int rank=1;rank<=255;rank++)foreach(int hard in new[]{0,1,20})
        {
            var v=new SkillValues(row,k=>k=="Ice Blast"?rank:200,k=>damage.Contains(k)||k=="Glacial Spike"?hard:0,99);
            Require(v.Mana256==1536+128*(rank-1)&&v.DurationFrames==(75+5*(rank-1))*(100+10*hard)/100
                &&v.Damage256(false)==Basis(rank,false)*(100+24*hard)/100&&v.Damage256(true)==Basis(rank,true)*(100+24*hard)/100,
                "Independent Ice Blast rank/hard-only damage/length: "+rank+"/"+hard);
        }
    }
    private static void Check()
    {
        Goldens();var p=Owner();var t=FireBoltChecks.Target(p,14);var behind=FireBoltChecks.Target(p,17);var side=FireBoltChecks.Target(p,14);side.MoveToWorld(new(side.X,side.Y+2,side.Z),side.Map);
        try
        {
            int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t),other=SkillHealth.Balance256(behind),off=SkillHealth.Balance256(side);
            Require(SorceressCombat.Release(p,CombatAbility.IceBlast,Serial.Zero,new(p.X+10,p.Y,p.Z)),"Learned Ice Blast release");
            var s=SorceressSpellMissiles.Snapshot().Flights.Single(x=>x.Owner==p.Serial.Value);
            Require(s is{Frame:0,Frames:50,VelocityFp8:2304,Vx:0.28125,Vy:0,Minimum256:2211,Maximum256:3317,ColdFrames:75}
                &&NecromancerEffects.ManaBalance256(p)==mana-1536&&SkillHealth.Balance256(t)==life,"Ice Blast factory/payment/prerequisite hard synergy");
            FireBoltChecks.Through(s.Id,40);Require(State(s.Id)!=null&&SkillHealth.Balance256(t)==life,"Ice Blast used faster/instant travel");
            DateTime at=Finish(s.Id);var c=Frozen(t);int loss=life-SkillHealth.Balance256(t);NoChill(t);
            Require(loss>=2211&&loss<3317&&SkillHealth.Balance256(behind)==other&&SkillHealth.Balance256(side)==off
                &&c.Freeze.Source==p.Serial.Value&&Math.Abs((c.FreezeUntil-at).TotalMilliseconds-3000)<2
                &&NamedControl.Think((BaseCreature)t)&&!NamedControl.SpecialAllowed(t),"Ice Blast damage/splash/freeze/source/AI consumer");
            TimedEffects.Settle(t,c.FreezeUntil);Require(TimedEffects.TryActor(t,out var a)&&a.Control.Freeze is{Removed:true}
                &&a.Control.Freeze.AiUntilUtc==c.FreezeUntil.AddMilliseconds(600),"Source skeleton thaw delay");
            TimedEffects.Settle(t,a.Control.Freeze.AiUntilUtc);Require(NamedControl.State(t)?.Freeze==null,"Freeze/thaw expiry");
            NecromancerEffects.SetMana256(p,1535);Require(!SorceressCombat.Release(p,CombatAbility.IceBlast,Serial.Zero,new(p.X+10,p.Y,p.Z))
                &&NecromancerEffects.ManaBalance256(p)==1535&&!SorceressSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value),"Lost mana emitted/paid Ice Blast");
        }
        finally{FireBoltChecks.Clean(p,t,behind,side);}
        Capture();Resistances();Controls();Corpse();Save();
        Log.Information("Ice Blast:765independent rank/hard suppliers,source dmg4 freeze-only hit,actual Nightwing/hard Glacial length,typed immunity/potion,elite fallback,thaw/corpse/lifecycle controls pass");
    }
    private static void Capture()
    {
        var p=Owner();try
        {
            var helm=PoisonDaggerChecks.Named(p,"Nightwing's Veil");long id=Create(p);var captured=State(id);
            Require(captured.Rank==3&&captured.ColdFrames==85&&captured.Modifiers.ColdMastery>0,"Actual Nightwing Ice Blast capture/soft length synergy");
            p.Backpack.DropItem(helm);ClassGear.Refresh(p);Require(State(id)==captured,"Gear removal rewrote Ice Blast");SorceressSpellMissiles.Cancel(p);
            id=Create(p);Require(State(id) is{Rank:1,ColdFrames:75,Minimum256:2211,Maximum256:3317},"Fresh Ice Blast retained soft gear/synergies");SorceressSpellMissiles.Cancel(p);
            Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.glacialspike",out _),"Train actual hard Glacial length synergy");
            id=Create(p);Require(State(id) is{Rank:1,ColdFrames:82,Minimum256:2211,Maximum256:3317},"Hard Glacial changed damage or missed freeze length");
            var pool=SorceressSpellMissiles.Snapshot();bool rejected=false;try{SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Id==id?x with{ColdFrames=0}:x).ToArray()});}catch(InvalidDataException){rejected=true;}
            Require(rejected&&State(id).ColdFrames==82,"Malformed Ice Blast partially restored");
        }
        finally{FireBoltChecks.Clean(p);}
    }
    private static void Resistances()
    {
        foreach(var (resist,potion,expected,frames) in new[]{(0,false,25600,75),(50,false,12800,37),(100,false,0,0),(100,true,7680,0),(0,true,35840,75)})
        {
            var p=Owner();var t=FireBoltChecks.Target(p,6);try
            {
                t.SetResistance(ResistanceType.Cold,resist);if(potion){Require(CombatPotions.Apply(p,3),"Actual Ice Blast potion");}
                var packet=SorceressSpellMissiles.Prepare(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.IceBlast),TimedEffects.Now) with{Minimum256=25600,Maximum256=25600};
                int before=SkillHealth.Balance256(t);DateTime at=Finish(SorceressSpellMissiles.Commit(packet));NoChill(t);
                Require(before-SkillHealth.Balance256(t)==expected,"Ice Blast resisted damage/separate potion lanes");
                if(frames==0){Require(NamedControl.State(t)?.Freeze==null,"Immune target froze through potion");}
                else{Require(Math.Abs((Frozen(t).FreezeUntil-at).TotalMilliseconds-frames*40)<2,"Resisted freeze length");}
            }
            finally{FireBoltChecks.Clean(p,t);}
        }
        foreach(var difficulty in new[]{MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
        {
            var p=Owner();var t=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};try
            {
                MonsterVariants.Assign(t,difficulty);t.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;t.SetResistance(ResistanceType.Cold,0);TimedEffects.PreventHealing(t,TimedEffects.Now);
                DateTime at=Finish(Create(p));NoChill(t);Require(Math.Abs((Frozen(t).FreezeUntil-at).TotalMilliseconds-(difficulty==MonsterDifficulty.Nightmare?1480:720))<2,"Actual variant freeze truncation");
            }
            finally{FireBoltChecks.Clean(p,t);}
        }
        foreach(var grade in new[]{EncounterGrade.Champion,EncounterGrade.Unique,EncounterGrade.SuperUnique})
        {
            var p=Owner();var t=new EncounterCreature{NoKillAwards=true,Frozen=true,CantWalk=true};try
            {
                var profile=EncounterProfiles.Profile("crypt-sentinel-90");t.Assign(profile,profile.MinimumLevel,MonsterDifficulty.Normal,grade,ChampionKind.Champion,[],EliteAura.None,null,new Random(173));
                t.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetResistance(ResistanceType.Cold,0);int before=SkillHealth.Balance256(t);Finish(Create(p));
                Require(SkillHealth.Balance256(t)<before&&TimedEffects.Chill(t,TimedEffects.Now)>0&&NamedControl.FrozenRemaining(t)==0,"Native elite freeze-to-cold fallback");
            }
            finally{FireBoltChecks.Clean(p,t);}
        }
    }
    private static void Controls()
    {
        foreach(string mode in new[]{"miss","wall","offline","facet","slow","death","respec","wardrobe"})
        {
            var p=Owner();var t=FireBoltChecks.Target(p,6);Item wall=null;try
            {
                if(mode=="slow"){AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,t.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());}
                int before=SkillHealth.Balance256(t);long id=Create(p);
                if(mode=="miss"){t.MoveToWorld(new(t.X,t.Y+3,t.Z),t.Map);}
                if(mode=="wall"){wall=new Static(0x80);wall.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);}
                if(mode=="facet"){p.MoveToWorld(p.Location,Map.Felucca);}
                if(mode=="slow"){Require(State(id) is{VelocityFp8:1152,Minimum256:1105,Maximum256:1658,ColdFrames:75},"Slow Ice Blast changed length or missed speed/damage");AmazonSlowMissiles.Clear(p);}
                if(mode=="death"){p.Kill();Require(State(id)==null,"Death retained Ice Blast");}
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(State(id)==null,"Respec retained Ice Blast");}
                if(mode=="wardrobe"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _)&&State(id)==null,"Wardrobe retained Ice Blast");}
                FireBoltChecks.Through(id,50,mode!="offline");Require(State(id)==null&&(SkillHealth.Balance256(t)<before)==(mode=="slow"),"Ice Blast lifecycle: "+mode);
            }
            finally{wall?.Delete();FireBoltChecks.Clean(p,t);}
        }
    }
    private static void Corpse()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,6);try
        {
            // Native UO inventory is deliberately suppressed. Supply controlled
            // loot in the existing class-drop callback,after ordinary producers,
            // and require it to survive the rest of the actual death path.
            Gold gold=null;var previous=ClassLootHooks.CreatureDied;
            ClassLootHooks.CreatureDied=(victim,container)=>{previous?.Invoke(victim,container);if(victim==t&&container is Corpse body){gold=new(777);body.DropItem(gold);}};
            try{SkillHealth.SetBalance256(t,256);Finish(Create(p));}finally{ClassLootHooks.CreatureDied=previous;}
            var corpse=t.Corpse as Corpse;
            Require(!t.Alive&&corpse!=null&&CorpseSkillRules.Shattered(corpse)&&!CorpseSkillRules.Usable(p,corpse,20)&&gold.IsChildOf(corpse),
                "Lethal Ice Blast: alive="+t.Alive+" corpse="+corpse?.Serial+" shattered="+(corpse!=null&&CorpseSkillRules.Shattered(corpse))
                +" usable="+(corpse!=null&&CorpseSkillRules.Usable(p,corpse,20))+" goldParent="+gold.Parent+" retained="+(corpse!=null&&gold.IsChildOf(corpse)));
            p.MoveToWorld(corpse.Location,corpse.Map);Require(ClassLoot.Take(p,gold)&&gold.Parent==p.Backpack&&!gold.Deleted,"Shattered corpse blocked normal class loot pickup");
        }
        finally{FireBoltChecks.Clean(p,t);}
    }
    private static void Save()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,6);int mana=NecromancerEffects.ManaBalance256(p);Finish(Create(p));var c=Frozen(t);
        DateTime until=TimedEffects.Now.Add(FixtureSpace.Retention);var held=c with{FreezeUntil=until,Freeze=c.Freeze with{AiUntilUtc=until.AddMilliseconds(40)}};TimedEffects.Actor(t,TimedEffects.Now).Control=held;
        long id=Create(p);var s=State(id) with{NextUtc=until};var pool=SorceressSpellMissiles.Snapshot();SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(x=>x.Id==id?s:x).ToArray()});
        var saved=new Saved(p.Serial.Value,t.Serial.Value,p.Map.MapID,p.Location,mana,SkillHealth.Balance256(t),s,held,0);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var held=World.FindMobile((Serial)s.Target);
        Require(s.Generation<2&&State(s.Flight.Id)==s.Flight&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(held)==s.Life
            &&NamedControl.State(held)==s.Control&&SkillBindings.Instance.Get(p).Right==CombatAbility.IceBlast,"Saved Ice Blast/freeze/wounds/resources/bindings");
        var account=FixtureNetwork.Authenticate(network,p,"ice-blast-reload-");p.MoveToWorld(s.Point,Map.Maps[s.Map]);var point=held.Location;held.MoveToWorld(new(p.X+6,p.Y+2,p.Z),p.Map);
        var t=FireBoltChecks.Target(p,13);int life=SkillHealth.Balance256(t);
        void Start()
        {
            var pool=SorceressSpellMissiles.Snapshot();var copy=s.Flight with{Id=pool.NextId+1,NextUtc=TimedEffects.Now};SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Append(copy).ToArray(),NextId=copy.Id});var deadline=TimedEffects.Now.AddSeconds(5);
            void Observe()
            {
                if(SkillHealth.Balance256(t)==life&&TimedEffects.Now<deadline){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));return;}
                try
                {
                    Require(SkillHealth.Balance256(t)<life&&Frozen(t).Freeze.Source==p.Serial.Value&&State(copy.Id)==null&&State(s.Flight.Id)==s.Flight,"Actual post-load Ice Blast did not travel/hit/freeze");NoChill(t);
                    Require(NamedControl.State(held)==s.Control&&SkillHealth.Balance256(held)==s.Life,"Reload refreshed held wounds/freeze");
                    t.Delete();held.MoveToWorld(point,p.Map);p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
                    File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));Log.Information("ice-blast-reload-result: passed");
                }
                catch(Exception e){Log.Error(e,"ice-blast-reload-result: failed");}
            }
            Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));
        }
        FixtureClock.NextLoop("ice-blast-reload-result",Start);
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=FireBoltChecks.Target(p,14);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t);string label=quick?"ice-blast-quick":"ice-blast-right";
        var account=FixtureNetwork.Authenticate(network,p,"ice-blast-input-");bool began=false;long deadline=Core.TickCount+7000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Ice Blast real input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.IceBlast&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(t)==life,"Ice Blast input paid/hit before release");began=true;Log.Information("{Label}: windup",label);}
                if(SkillHealth.Balance256(t)==life){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-1536&&Frozen(t).Freeze.Source==p.Serial.Value&&!SorceressSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value),"Actual Ice Blast input payment/collision/freeze");NoChill(t);
                FireBoltChecks.Clean(p,t);account.Delete();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){FireBoltChecks.Clean(p,t);account.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)CombatAbility.IceBlast,(int)DiabloClass.Sorceress,p.X+10,p.Y,p.Z);Observe();});
    }
}
