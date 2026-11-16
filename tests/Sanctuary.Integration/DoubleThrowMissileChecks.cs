// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DoubleThrowMissileChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DoubleThrowMissileChecks));
    private sealed record Saved(uint Owner,uint Target,uint Primary,uint Secondary,uint Gloves,Point3D Point,string Gear,WeaponFlight Flight,int Life,int Mana,int First,int Second,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"double-throw-missile-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private sealed class High:Random{public override int Next(int n)=>n-1;}
    private sealed class Low:Random{public override int Next(int n)=>0;}
    private sealed class Rolls:IDisposable
    {
        private readonly Func<double> Hit=BarbarianDoubleThrow.HitRoll;
        private readonly Func<int,int> Damage=WeaponMissiles.DamageRoll,Block=SourceBlocking.BlockRoll,Choice=SourceBlocking.ChoiceRoll;
        private readonly Func<Random> Throw=BarbarianThrowing.RandomSource;
        private readonly Random Effects=WeaponMissiles.ItemEffects;
        internal Rolls(){BarbarianDoubleThrow.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=_=>1;BarbarianThrowing.RandomSource=()=>new High();WeaponMissiles.ItemEffects=new High();}
        public void Dispose(){BarbarianDoubleThrow.HitRoll=Hit;WeaponMissiles.DamageRoll=Damage;SourceBlocking.BlockRoll=Block;SourceBlocking.ChoiceRoll=Choice;BarbarianThrowing.RandomSource=Throw;WeaponMissiles.ItemEffects=Effects;}
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"double-throw-missile-reload-check":"double-throw-missile-check";ConsoleInputHandler.RegisterCommand(name,"Double Throw travelling snapshots,collision,poison and saved released missile",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"right","quick"})
        {string label="double-throw-missile-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated Double Throw release and travelling impact",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(out Item gloves)
    {
        var p=DoubleThrowChecks.Fresh();gloves=DruidSetupItemChecks.Named(p,"Venom Grip");ClassGear.Refresh(p);
        // A longer independent corridor permits moving-target and pierce tests.
        FixtureSpace.Corridor(p,6200,32,3100);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.DoubleThrow)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.DoubleThrow),"Throw bindings");return p;
    }
    private static BaseCreature Target(PlayerMobile p,int dx=3,int dy=0)
    {
        var t=new PoisonDaggerContactChecks.Recipient();t.SetHits(60000);t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Poison,0);return t;
    }
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value).OrderBy(f=>f.Id).ToArray();
    private static void Drain(PlayerMobile p)
    {for(int i=0;i<60&&Own(p).Length>0;i++){WeaponMissiles.Tick(Own(p).Min(f=>f.NextUtc),onlyOwner:p.Serial.Value);}Require(Own(p).Length==0,"Unfinished thrown flight");}
    private static WeaponFlight First(PlayerMobile p,Mobile t=null)
    {
        DoubleThrowChecks.Ready(p);Require(DoubleThrowChecks.Begin(p,t)&&ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"First learned throw release");var f=Own(p).Single();ClassCombat.Stop(p);return f;
    }
    private static void Clean(PlayerMobile p,params BaseCreature[] targets)
    {ClassCombat.Stop(p);WeaponMissiles.Cancel(p);foreach(var t in targets){if(t!=null){SourceKnockbacks.Clear(t);t.Delete();}}p.Delete();}
    private static void Scenarios()
    {
        foreach(string scenario in new[]{"travel","snapshot","miss","block","immune","move-out","move-in","wall","owner-death","facet"})
        {
            var p=Owner(out var gloves);var t=Target(p,scenario=="wall"?6:3,scenario=="move-in"?3:0);Item wall=null;
            try
            {
                BarbarianDoubleThrow.HitRoll=()=>scenario=="miss"?1:0;SourceBlocking.BlockRoll=n=>scenario=="block"?0:n-1;
                if(scenario=="immune"){t.SetResistance(ResistanceType.Poison,100);}
                int hp=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);var pair=BarbarianDoubleThrow.Pair(p).Value;int primary=NamedAmmunition.Remaining(pair.Primary),secondary=NamedAmmunition.Remaining(pair.Secondary);
                var f=First(p,scenario=="move-in"?null:t);
                Require(DoubleThrowMissiles.Valid(f)&&f.Throwing.Weapon==pair.Primary.Serial.Value&&f.Missile=="throwaxe"&&f.Frames==20&&f.Damage.Elements.PoisonMin==153&&f.Damage.Elements.PoisonImpactFrames==100&&f.Damage.SkillAttackPercent==20,"Pinned selected-hand missile/source packet");
                Require(SkillHealth.Balance256(t)==hp&&NecromancerEffects.ManaBalance256(p)==mana-256&&NamedAmmunition.Remaining(pair.Primary)==primary-1&&NamedAmmunition.Remaining(pair.Secondary)==secondary,"Throw dealt instant damage or changed wrong resource");
                WeaponMissiles.Tick(f.NextUtc,onlyOwner:p.Serial.Value);Require(SkillHealth.Balance256(t)==hp&&Own(p).Length==1,"Distant victim hit before travel");
                if(scenario=="snapshot"){p.AddToBackpack(gloves);p.AddToBackpack(pair.Primary);ClassGear.Refresh(p);Require(Own(p).Single().Damage==f.Damage,"Equipment rewrote released damage");}
                if(scenario=="move-out"){t.MoveToWorld(new(t.X,t.Y+3,t.Z),t.Map);}
                if(scenario=="move-in"){t.MoveToWorld(new(t.X,p.Y,t.Z),t.Map);}
                if(scenario=="wall"){wall=new Static(0x80);wall.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);}
                if(scenario=="owner-death"){p.Kill();}
                if(scenario=="facet"){p.MoveToWorld(new(100,100,0),Map.Felucca);}
                Drain(p);bool hit=scenario is "travel" or "snapshot" or "move-in";
                Require(hp-SkillHealth.Balance256(t)==(hit?153:0),"Thrown travel/snapshot/miss/block/immunity/terrain/lifecycle damage "+scenario);
                var poison=TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value&&x.PoisonMs>0);Require(hit?poison?.PoisonClock?.Rate256==153&&poison.PoisonMs==4000:poison==null,"Thrown initial poison and clock "+scenario);
                if(p.Alive){Require(NecromancerEffects.ManaBalance256(p)==mana-256&&NamedAmmunition.Remaining(pair.Primary)==primary-1,"Impact repaid resources");}
            }
            finally{wall?.Delete();p.Corpse?.Delete();Clean(p,t);}
        }
        BarbarianDoubleThrow.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;
        var piercer=Owner(out _);var a=Target(piercer);var b=Target(piercer,6);
        try
        {
            DruidSetupItemChecks.Named(piercer,"Razortail");ClassGear.Refresh(piercer);BarbarianThrowing.RandomSource=()=>new Low();var f=First(piercer,a);
            Require(f.Pierces==4,"Actual piercing item birth budget");int ah=SkillHealth.Balance256(a),bh=SkillHealth.Balance256(b);Drain(piercer);
            Require(ah-SkillHealth.Balance256(a)==153&&bh-SkillHealth.Balance256(b)==153,"Pierced missile rerolled quantity,missed or re-hit prior recipient");
        }
        finally{BarbarianThrowing.RandomSource=()=>new High();Clean(piercer,a,b);}
    }
    private static void Hold(PlayerMobile p,WeaponFlight flight)
    {var s=WeaponMissiles.Snapshot();WeaponMissiles.Restore(s with{Flights=s.Flights.Where(f=>f.Owner!=p.Serial.Value).Append(flight).ToArray()});}
    private static void Check(bool reload)
    {
        using var rolls=new Rolls();PlayerMobile p;BaseCreature t;Item gloves;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);t=(BaseCreature)World.FindMobile((Serial)s.Target);gloves=World.FindItem((Serial)s.Gloves);var pair=BarbarianDoubleThrow.Pair(p).Value;
            Require(generation<=2&&pair.Primary.Serial.Value==s.Primary&&pair.Secondary.Serial.Value==s.Secondary&&Json(new[]{ClassGear.State(pair.Primary),ClassGear.State(pair.Secondary),ClassGear.State(gloves)})==s.Gear&&Json(Own(p).Single())==Json(s.Flight)&&SkillHealth.Balance256(t)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&NamedAmmunition.Remaining(pair.Primary)==s.First&&NamedAmmunition.Remaining(pair.Secondary)==s.Second&&ClassCombat.For(p).DoubleThrow==null,"Saved released flight/originals/resources changed");
            p.MoveToWorld(s.Point,Map.Trammel);
            Log.Information("Double Throw reload recipient: point={Point} map={Map} frozen={Frozen} poison={Poison} physical={Physical} fire={Fire} cold={Cold} lightning={Lightning}",t.Location,t.Map?.MapID,t.Frozen,t.PoisonResistance,t.PhysicalResistance,t.FireResistance,t.ColdResistance,t.EnergyResistance);
            // Like the retained Fury flight fixture,re-establish its controlled
            // recipient after native deserialization,then resume at live time.
            t.Frozen=t.CantWalk=true;t.AIObject.AITimer.Stop();t.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
            foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Poison,0);
            Require(ClassCombat.IsHostile(p,t),"Reload fixture recipient lost hostility");Hold(p,s.Flight with{NextUtc=TimedEffects.Now});Drain(p);
            Require(s.Life-SkillHealth.Balance256(t)==153,"Saved missile did not deliver captured poison once: "+(s.Life-SkillHealth.Balance256(t)));Clean(p,t);
        }
        Scenarios();p=Owner(out gloves);t=Target(p);var f=First(p,t) with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};Hold(p,f);
        var all=WeaponMissiles.Snapshot();bool refused=false;try{WeaponMissiles.Restore(all with{Flights=all.Flights.Select(x=>x.Id==f.Id?x with{Throwing=x.Throwing with{ArmorDelta=1}}:x).ToArray()});}catch(InvalidDataException){refused=true;}
        Require(refused&&Json(WeaponMissiles.Snapshot())==Json(all),"Invalid new metadata partially rewrote existing flights");
        var weapons=BarbarianDoubleThrow.Pair(p).Value;var point=p.Location;p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,t.Serial.Value,weapons.Primary.Serial.Value,weapons.Secondary.Serial.Value,gloves.Serial.Value,point,Json(new[]{ClassGear.State(weapons.Primary),ClassGear.State(weapons.Secondary),ClassGear.State(gloves)}),f,SkillHealth.Balance256(t),NecromancerEffects.ManaBalance256(p),NamedAmmunition.Remaining(weapons.Primary),NamedAmmunition.Remaining(weapons.Secondary),generation)));
    }
    private static void Input(NetState n,string label)
    {
        var rolls=new Rolls();var p=Owner(out _);var t=Target(p);DoubleThrowChecks.Ready(p);var account=FixtureNetwork.Authenticate(n,p,"double-throw-missile-");var pair=BarbarianDoubleThrow.Pair(p).Value;
        int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t),first=NamedAmmunition.Remaining(pair.Primary),second=NamedAmmunition.Remaining(pair.Secondary);long deadline=Core.TickCount+6000;bool began=false,sawFlight=false;
        void Close(){Clean(p,t);rolls.Dispose();account.Delete();}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Double Throw wire timeout");var s=ClassCombat.For(p);
                if(!began&&s.DoubleThrow!=null){began=true;Require(SkillHealth.Balance256(t)==hp&&NecromancerEffects.ManaBalance256(p)==mana,"Wire startup dealt damage or charged early");Log.Information("{Label}: windup",label);}
                if(Own(p).Length>0){sawFlight=true;Require(Own(p).All(DoubleThrowMissiles.Valid),"Wire invalid flight");}
                if(!began||s.DoubleThrow!=null||Own(p).Length!=0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(sawFlight&&hp-SkillHealth.Balance256(t)>=306&&NecromancerEffects.ManaBalance256(p)==mana-512&&NamedAmmunition.Remaining(pair.Primary)==first-1&&NamedAmmunition.Remaining(pair.Secondary)==second-1,"Wire two released hands,travel,poison or resource phases differ");Close();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.DoubleThrow,(int)DiabloClass.Barbarian,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
