// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowFuryMissileChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowFuryMissileChecks));
    private sealed record Held(uint Actor,uint Target,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,int Health,WeaponFlight Flight);
    private sealed record Saved(uint Owner,int Mana,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-fury-missiles-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool v,string why){if(!v){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("shadow-fury-missiles-check",false);Register("shadow-fury-missiles-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own Shadow Fury travelling NPC receiver and saved flights",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static ulong Seed(bool hit)
    {for(int i=1;i<10000;i++){ulong s=SourceRandom.InitLow(i),r=s;uint n=SourceRandom.Limited(ref r,100);if(hit?n<5:n>=95){return s;}}throw new InvalidOperationException("No NPC hit seed");}
    private static ShadowTailChecks.Witness Target(AssassinShadowBody b,int dx=8)
    {var t=new ShadowTailChecks.Witness{Actor=b.Serial.Value};t.SetHits(65000);foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}t.MoveToWorld(new(b.X+dx,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static WeaponFlight[] Own(AssassinShadowBody b)=>WeaponMissiles.Snapshot().Flights.Where(f=>f.Owner==b.Serial.Value).ToArray();
    private static void Hold(AssassinShadowBody b,WeaponFlight f)
    {var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Where(x=>x.Owner!=b.Serial.Value).Append(f).ToArray()});}
    private static WeaponFlight Launch(AssassinShadowBody b,bool critical=false,Action<GeneratedNpcStatGraph> prepare=null)
    {b.ChangeSkills(s=>s.Use(266));prepare?.Invoke(OriginalNpcEquipment.Graph(b));long id=ShadowFuryMissiles.LaunchPrepared(b,new(b.X+22,b.Y,b.Z),12,critical,SourceRandom.InitLow(173),TimedEffects.Now);Require(id>0,"Prepared own Fury launch refused");b.ChangeSkills(s=>s.Use(null));return WeaponMissiles.State(id);}
    private static void Drain(AssassinShadowBody b)
    {int guard=45;while(Own(b).Length>0&&guard-->0){WeaponMissiles.Tick(Own(b).Single().NextUtc,true,b.Serial.Value);}Require(guard>0,"Fury flight did not terminate");}
    private static Held Capture(AssassinShadowBody b,BaseCreature target)=>new(b.Serial.Value,target.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),SkillHealth.Balance256(target),Own(b).Single());
    private static int Expected(WeaponFlight f,BaseCreature t,out ulong seed)
    {ulong s=f.Npc.Seed;var d=f.Damage.Roll(t,n=>(int)SourceRandom.Limited(ref s,n),out _);seed=s;return NamedElements.To256(d.Total)+f.Damage.Elements.LifeDrain;}
    private static void Live(AssassinShadowBody b,PlayerMobile p)
    {
        var choice=SourceBlocking.ChoiceRoll;var block=SourceBlocking.BlockRoll;SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>99;
        int mana=NecromancerEffects.ManaBalance256(p),own=NecromancerEffects.ManaBalance256(b);var charges=Json(AssassinCharges.Snapshot());
        try
        {
            foreach(bool critical in new[]{false,true})
            {
                var t=Target(b);try
                {
                    NpcEquipmentRandom.Set(b,Seed(true));int hp=SkillHealth.Balance256(t);var f=Launch(b,critical);int expected=Expected(f,t,out _);var rng=NpcEquipmentRandom.Require(b);
                    WeaponMissiles.Tick(f.NextUtc,true,b.Serial.Value);Require(SkillHealth.Balance256(t)==hp&&Own(b).Single().Frame==1&&NpcEquipmentRandom.Require(b)==rng,"Fury hit or rolled before travelling to target");
                    Drain(b);Require(SkillHealth.Balance256(t)==hp-expected&&Own(b).Length==0,"Own travelling Fury typed/critical/flat-drain loss differs: "+(hp-SkillHealth.Balance256(t))+" expected "+expected);
                }finally{WeaponMissiles.Cancel(b);t.Delete();}
            }
            // Accuracy consumes only the actor's stream; failed accuracy never
            // fills damage,consumes private RNG,or reaches the receiver.
            var miss=Target(b);try
            {
                var f=Launch(b);WeaponMissiles.Cancel(b);NpcEquipmentRandom.Set(b,Seed(false));int hp=SkillHealth.Balance256(miss);int packets=NativeMonsterReturn.Session(miss).Packets;
                Require(ShadowFuryMissiles.Impact(b,miss,f,TimedEffects.Now,out ulong end)==0&&end==f.Npc.Seed&&SkillHealth.Balance256(miss)==hp&&NativeMonsterReturn.Session(miss).Packets==packets,"Accuracy miss reached fill/return or advanced child RNG");
                int chance=ShadowFuryMissiles.Chance(b,miss,120),strength=p.RawStr,dex=p.RawDex;p.RawStr+=200;p.RawDex+=200;
                Require(ShadowFuryMissiles.Chance(b,miss,120)==chance,"Own missile hit borrowed controller stats");p.RawStr=strength;p.RawDex=dex;
                var g=OriginalNpcEquipment.Graph(b);int expected=AssassinTrapAttack.Chance(OriginalNpcEquipment.Level(b),g.Read(19),g.Read(2),g.Read(119)+(SorceressEffects.Enchantment(b)?.AttackPercent??0)+CombatAuras.AttackPercent(b)+NecromancerEffects.AttackStatPercent(b)+HexBane.TargetAttackPercent(b),ClassStats.Level(miss),NamedCombat.DefenseFor(b,miss,ClassStats.Defense(miss),true),120);
                Require(chance==expected,"Own flat missile AR did not reuse qualified NPC hit helper");
            }finally{miss.Delete();}
            var shield=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};try
            {
                shield.SetHits(65000);shield.MoveToWorld(new(b.X+8,b.Y,b.Z),b.Map);shield.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){shield.SetResistance(r,0);}TimedEffects.PreventHealing(shield,TimedEffects.Now);
                SourceBlocking.BlockRoll=_=>0;var f=Launch(b);WeaponMissiles.Cancel(b);NpcEquipmentRandom.Set(b,Seed(true));int hp=SkillHealth.Balance256(shield),packets=NativeMonsterReturn.Session(shield).Packets;_=Expected(f,shield,out ulong expected);
                Require(ShadowFuryMissiles.Impact(b,shield,f,TimedEffects.Now,out ulong end)==0&&SkillHealth.Balance256(shield)==hp&&end==expected&&NativeMonsterReturn.Session(shield).Packets==packets+1,"Shield block skipped private fill/return or committed HP");SourceBlocking.BlockRoll=_=>99;
            }finally{shield.Delete();}
            foreach(int resist in new[]{0,100})
            {
                var t=Target(b);try
                {
                    t.SetResistance(ResistanceType.Physical,resist);NecromancerEffects.Profiles.Store(t,new(Curse:new(CombatAbility.LifeTap,p.Serial.Value,3,TimedEffects.Now.AddMinutes(1),p.X,p.Y)));
                    NecromancerEffects.Profiles.Store(b,new(Curse:new(CombatAbility.IronMaiden,p.Serial.Value,3,TimedEffects.Now.AddMinutes(1),p.X,p.Y)));
                    SkillHealth.SetBalance256(b,1);NpcEquipmentRandom.Set(b,Seed(true));var f=Launch(b);WeaponMissiles.Cancel(b);ulong s=f.Npc.Seed;var raw=f.Damage.Roll(t,n=>(int)SourceRandom.Limited(ref s,n));int physical=resist==0?NamedElements.To256(raw.Physical):0;
                    Require(GemCombat.Drain(t)==100,"Fixture drain effectiveness changed");int healing=NecromancerEffects.LifeTapHealing256(physical,50)+Math.Min(f.Damage.Elements.LifeDrain,physical)+Math.Min(f.Damage.Elements.ManaDrain,NecromancerEffects.ManaBalance256(t));int hp=SkillHealth.Balance256(t);
                    _=ShadowFuryMissiles.Impact(b,t,f,TimedEffects.Now,out _);
                    Require(SkillHealth.Balance256(b)==1+healing,"Missile Life Tap/flat drain/physical immunity or melee-only Iron Maiden changed source health: "+SkillHealth.Balance256(b)+" expected "+(1+healing));
                    Require(SkillHealth.Balance256(t)<hp,"Immune physical recipient lost all independent cold missile damage");
                }finally{NecromancerEffects.Profiles.Store(b,new());t.Delete();}
            }
            var poison=Target(b);
            try
            {
                var f=Launch(b,prepare:g=>{var n=g.Item(ShadowLoadout.Binding(b).Primary).Unit.Stats;foreach(var pair in new[]{(57,256),(58,256),(59,25),(326,1)}){g.Values.SetBase(n,EquipmentStatKey.Pack(pair.Item1),pair.Item2);}});WeaponMissiles.Cancel(b);NpcEquipmentRandom.Set(b,Seed(true));_=ShadowFuryMissiles.Impact(b,poison,f,TimedEffects.Now,out _);
                var state=TimedEffects.Snapshot().Single(t=>t.Owner==poison.Serial.Value);Require(state.PoisonSource==p.Serial.Value&&state.PoisonMs==1000&&state.PoisonRate==192/10240.0,"Own Fury lost independent source poison rate/length/credit: "+Json(new{state.PoisonSource,state.PoisonMs,state.PoisonRate,f.Damage.Elements}));
            }finally{poison.Delete();}
            foreach(bool hit in new[]{false,true})
            {
                var t=Target(b);try
                {
                    var f=Launch(b,prepare:g=>g.Values.SetBase(g.Actor.Stats,EquipmentStatKey.Pack(120),-5));WeaponMissiles.Cancel(b);NpcEquipmentRandom.Set(b,Seed(hit));int before=TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value).DefenseLoss;bool observed=false;t.Observe=_=>{observed=true;Require(TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value).DefenseLoss==before,"Missile armor mutation preceded HP callback");};
                    _=ShadowFuryMissiles.Impact(b,t,f,TimedEffects.Now,out _);Require(TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value).DefenseLoss==Math.Min(CreatureProfiles.Defense(t)+NpcEquipmentCombat.Defense(t),before+(hit?10:0))&&observed==hit,"Saved double armor mutation did not follow accuracy and damage: "+Json(new{hit,observed,f.Npc.Packet.TargetArmorDelta,Loss=TimedEffects.DefenseLoss(t),Basis=CreatureProfiles.Defense(t)+NpcEquipmentCombat.Defense(t)}));
                    Require(!ShadowFuryMissiles.Valid(f with{Npc=f.Npc with{CreditOwner=b.Serial.Value}})&&!ShadowFuryMissiles.Valid(f with{Damage=f.Damage with{Min256=f.Damage.Min256+1}})&&!ShadowFuryMissiles.Valid(f with{MechanicalProfile="unknown"}),"NPC Fury accepted a corrupt saved identity/packet/profile");
                }finally{t.Delete();}
            }
            var rngBefore=NpcEquipmentRandom.Require(b);var flightsBefore=Json(WeaponMissiles.Snapshot());bool refused=false;
            try{_=Launch(b,prepare:g=>g.Values.SetBase(g.Actor.Stats,EquipmentStatKey.Pack(120),1));}catch(NotSupportedException){refused=true;}finally{b.ChangeSkills(x=>x.Use(null));}
            Require(refused&&NpcEquipmentRandom.Require(b)==rngBefore&&Json(WeaponMissiles.Snapshot())==flightsBefore,"Unsupported positive armor mutated RNG/allocated a flight");
            foreach(bool parked in new[]{false,true})
            {
                var t=Target(b);try
                {
                    var f=Launch(b);int hp=SkillHealth.Balance256(t);var rng=NpcEquipmentRandom.Require(b);var origin=b.Location;if(parked){PersistentMinions.Park(b);}
                    WeaponMissiles.Tick(f.NextUtc.AddMilliseconds(200),false,b.Serial.Value);var skipped=Own(b).Single();Require(skipped.Frame==6&&skipped.X==f.X&&skipped.Y==f.Y&&SkillHealth.Balance256(t)==hp&&NpcEquipmentRandom.Require(b)==rng,"Inactive Fury moved,damaged,rolled,or reset its lifetime");
                    WeaponMissiles.Tick(skipped.NextUtc.AddSeconds(2),false,b.Serial.Value);Require(Own(b).Length==0&&SkillHealth.Balance256(t)==hp,"Inactive Fury failed original expiry");if(parked){b.MoveToWorld(origin,p.Map);}
                }finally{WeaponMissiles.Cancel(b);t.Delete();}
            }
            Require(NecromancerEffects.ManaBalance256(p)==mana&&NecromancerEffects.ManaBalance256(b)==own&&Json(AssassinCharges.Snapshot())==charges,"Prepared Fury charged owner/pet mana or consumed melee charges");
        }
        finally{SourceBlocking.ChoiceRoll=choice;SourceBlocking.BlockRoll=block;WeaponMissiles.Cancel(b);}
    }
    private static void Check(bool reload)
    {
        using(var cases=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-fury-missile-cases.json"))))
        {
            int n=0;foreach(var c in cases.RootElement.EnumerateArray()){int V(int i)=>c[i].GetInt32();Require(ShadowFuryMissiles.Result(V(0),V(1)!=0)==V(3)&&V(4)==10&&V(5)==32&&V(6)==(V(0)==0?1:0)&&V(7)==1&&V(8)==1&&V(9)==(V(2)==0?0:1),"Complete source missile flags/events/return differ");n++;}Require(n==128,"Incomplete missile flag contexts");
        }
        PlayerMobile p;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));p=(PlayerMobile)World.FindMobile((Serial)s.Owner);generation=s.Generation+1;bodies=s.Bodies.Select(x=>(AssassinShadowBody)World.FindMobile((Serial)x.Actor)).ToArray();
            Require(NecromancerEffects.ManaBalance256(p)==s.Mana,"Reload changed owner mana");
            for(int i=0;i<bodies.Length;i++){var t=(BaseCreature)World.FindMobile((Serial)s.Bodies[i].Target);Require(Json(Capture(bodies[i],t))==Json(s.Bodies[i]),"Reload changed saved original body/items/resources/wounds/RNG or partial projectile");}
            p.MoveToWorld(new(6420,3570,0),Map.Trammel);
            for(int i=0;i<bodies.Length;i++)
            {
                var b=bodies[i];b.MoveToWorld(new(p.X+1,(int)s.Bodies[i].Flight.Y,p.Z),p.Map);var t=(BaseCreature)World.FindMobile((Serial)s.Bodies[i].Target);t.MoveToWorld(new(b.X+8,b.Y,b.Z),p.Map);var f=Own(b).Single();int hp=SkillHealth.Balance256(t),expected=Expected(f,t,out _);Hold(b,f with{NextUtc=TimedEffects.Now});Drain(b);Require(SkillHealth.Balance256(t)==hp-expected,"Reloaded own Fury did not continue exactly once: loss="+(hp-SkillHealth.Balance256(t))+",expected="+expected+",saved="+f.X+","+f.Y+",target="+t.Location);t.Delete();PersistentMinions.Park(b);
            }
        }
        else
        {
            p=BladeBodyChecks.Fresh(6420,30,3570);ClassSkillChecks.Train(p,"assassin.shadowwarrior",20);ClassSkillChecks.Train(p,"assassin.shadowmaster",20);var world=NpcWorldAllocation.Capture();bodies=new AssassinShadowBody[2];
            try{int i=0;foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}){NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));bodies[i]=AssassinShadowBody.CreateInitialized(p,ability,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(bodies[i]);ShadowEquipmentDefense.Activate(bodies[i]);i++;}}finally{NpcWorldAllocation.Restore(world);}
        }
        var held=new Held[2];for(int i=0;i<bodies.Length;i++)
        {
            var b=bodies[i];b.MoveToWorld(new(p.X+1,p.Y+i*3,p.Z),p.Map);b.ChangeSkills(s=>{s.Assign(266,12);s.Assign(257,3);s.Assign(277,3);});Live(b,p);
            var target=Target(b,15);NpcEquipmentRandom.Set(b,Seed(true));var f=Launch(b);WeaponMissiles.Tick(f.NextUtc.AddMilliseconds(40),true,b.Serial.Value);f=Own(b).Single();Require(f.Frame==2&&f.X>b.X,"Partial Fury did not move");Hold(b,f with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)});SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);PersistentMinions.Park(b);GeneratedNpcLiveResources.Snapshot();held[i]=Capture(b,target);
        }
        PersistentMinions.Suspend(p);p.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,NecromancerEffects.ManaBalance256(p),held,generation)));

        Log.Information("Own Fury missiles: both original species,travel/critical/typed damage,flat NPC drains,accuracy/private RNG,shield return,Life Tap versus Iron Maiden,poison,inactive expiry,and exact partial-flight/body/items/resources/RNG continuity; full action remains gated.");
    }
}
