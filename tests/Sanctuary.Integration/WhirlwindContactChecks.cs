// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class WhirlwindContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(WhirlwindContactChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Gloves,Point3D Point,string Gear,string Profile,int Life,int Mana,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"whirlwind-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private sealed class High:Random{public override int Next(int n)=>n-1;}
    private sealed class Rolls:IDisposable
    {
        private readonly Func<double> Hit=BarbarianWhirlwind.HitRoll,Element=BarbarianWhirlwind.ElementRoll;
        private readonly Func<int,int> Physical=BarbarianWhirlwind.PhysicalRoll,Poison=BarbarianWhirlwind.PoisonRoll,Block=SourceBlocking.BlockRoll,Choice=SourceBlocking.ChoiceRoll;
        private readonly Random Effects=BarbarianWhirlwind.Effects;
        internal Rolls(){BarbarianWhirlwind.HitRoll=BarbarianWhirlwind.ElementRoll=()=>0;BarbarianWhirlwind.PhysicalRoll=BarbarianWhirlwind.PoisonRoll=_=>0;BarbarianWhirlwind.Effects=new High();SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=_=>1;}
        public void Dispose(){BarbarianWhirlwind.HitRoll=Hit;BarbarianWhirlwind.ElementRoll=Element;BarbarianWhirlwind.PhysicalRoll=Physical;BarbarianWhirlwind.PoisonRoll=Poison;BarbarianWhirlwind.Effects=Effects;SourceBlocking.BlockRoll=Block;SourceBlocking.ChoiceRoll=Choice;}
    }
    private static PlayerProcSave Own(PlayerMobile p,Item item)
    {var all=PlayerItemProcs.Capture();return all with{Items=all.Items.Where(x=>x.Item==item.Serial.Value).ToArray(),Owners=all.Owners.Where(x=>x.Actor==p.Serial.Value).ToArray()};}
    private static ulong Seed(PlayerMobile p)=>PlayerItemProcs.Capture().Owners.Single(o=>o.Actor==p.Serial.Value).Seed;
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"whirlwind-contact-reload-check":"whirlwind-contact-check";ConsoleInputHandler.RegisterCommand(name,"Whirlwind ordered contacts,uninterruptible proc rolls,poison and saved originals",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"right","quick"})
        {string label="whirlwind-contact-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated learned Whirlwind and suppressed equipped proc",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(out Item weapon,out Item gloves)
    {
        var p=WhirlwindChecks.Fresh(false);p.AddToBackpack(CombatTiming.Equipped(p));
        // Exact raw-property boundary fixture,not a new named-item admission.
        weapon=PlayerItemProcWorldChecks.Carrier(p,new Longsword());gloves=DruidSetupItemChecks.Named(p,"Venom Grip");ClassGear.Refresh(p);
        Require(WeaponHands.Pair(p)==null&&SkillBindings.Instance.Assign(p,false,CombatAbility.Whirlwind)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Whirlwind),"Single hand and learned bindings");return p;
    }
    private static BaseCreature Target(PlayerMobile p,int poisonResistance=0)
    {
        var t=new PoisonDaggerContactChecks.Recipient();t.SetHits(60000);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();
        foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Poison,poisonResistance);
        Require(SourceBlocking.TryRate(t,out int chance,out _)&&chance>0,"Missing real shield control");return t;
    }
    private static void Clean(PlayerMobile p,BaseCreature t)
    {if(BarbarianWhirlwind.State(p)!=null){WhirlwindChecks.Finish(p);}SorceressSpellMissiles.Cancel(p);if(t!=null){SourceKnockbacks.Clear(t);t.Delete();}WhirlwindChecks.Ready(p);}
    private static void Scenarios(PlayerMobile p,Item weapon)
    {
        using var proof=PlayerItemProcWorld.Candidate(p);var observed=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();var origin=p.Location;var map=p.Map;
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
            foreach(string scenario in new[]{"hit","miss","block","immune","empty"})
            {
                WhirlwindChecks.Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);BarbarianWhirlwind.HitRoll=()=>scenario=="miss"?1:0;SourceBlocking.BlockRoll=n=>scenario=="block"?0:n-1;
                var t=scenario=="empty"?null:Target(p,scenario=="immune"?100:0);int before=t==null?0:SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);visits.Clear();
                var world=NpcWorldAllocation.Capture();ulong expectedWorld=world.Seed,expectedActor=PlayerItemProcs.Initialized(p)?Seed(p):SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref expectedWorld)));
                bool hit=scenario is "hit" or "immune";if(hit){SourceRandom.Roll(ref expectedActor);SourceRandom.Roll(ref expectedActor);}long first=SorceressSpellMissiles.Snapshot().NextId;
                try
                {
                    Require(WhirlwindChecks.Begin(p),"Whirlwind entry "+scenario);Require(BarbarianWhirlwind.State(p).Path.Length==4,"Fixture spin corridor blocked");WhirlwindChecks.Step(p,2);Require(visits.Count==0,"Contact before initial pulse");WhirlwindChecks.Step(p);
                    Require(BarbarianWhirlwind.Locked(p)&&NecromancerEffects.ManaBalance256(p)==mana-3200,"Spin state or once-only12.5mana");
                    if(t==null){Require(visits.Count==0&&NpcWorldAllocation.Capture()==world,"Empty pulse allocated proc RNG");continue;}
                    Require(Seed(p)==expectedActor&&NpcWorldAllocation.Capture().Seed==expectedWorld&&SorceressSpellMissiles.Snapshot().NextId==first,"Whirlwind failed to retain proc rolls without child allocation");
                    Require(visits.Count==(hit?2:1)&&visits[^1].Phase=="domeleeattack"&&visits[^1].Packet.HitFlags==(hit?32u:0u),"Whirlwind source phases/flags");
                    Require(visits.All(v=>v.Outcomes.Length==1&&v.Outcomes[0].CastResults==0&&v.Outcomes[0].CastAttempts==(hit?1:0)),"Spin suppressed rolls instead of casting,or emitted a spell");
                    Require(Seed(p)==expectedActor&&NpcWorldAllocation.Capture().Seed==expectedWorld&&SorceressSpellMissiles.Snapshot().NextId==first,"Spin RNG or world allocation differs");
                    int rate=scenario=="hit"?153:0;Require(before-SkillHealth.Balance256(t)==rate,"Initial poison must apply one mitigated rate,including zero physical damage");
                    var poison=TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value&&x.PoisonMs>0);Require(rate==0?poison==null:poison?.PoisonClock?.Rate256==153&&poison.PoisonMs==4000,"Whirlwind poison clock/avoidance/immunity");
                    int count=visits.Count;WhirlwindChecks.Step(p);Require(visits.Count==count,"Spin replayed contact before next cadence");
                    t.Delete();WhirlwindChecks.Finish(p);Require(NecromancerEffects.ManaBalance256(p)==mana-3200,"Spin repaid mana");
                }
                finally{Clean(p,t);p.MoveToWorld(origin,map);}
            }
            BarbarianWhirlwind.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;
            // The same original is permitted to cast through ordinary contact as
            // soon as the controller ends;the exception is not a global mute.
            var after=Target(p);visits.Clear();long id=SorceressSpellMissiles.Snapshot().NextId;
            try{var prepared=PlayerWeaponContact.PrepareOrdinary(p,after,0,0,()=>true,()=>0,()=>0,_=>0,_=>0,new High(),true);PlayerWeaponContact.Release(p,after,prepared,TimedEffects.Now,true);
                Require(visits.Count==2&&visits.All(v=>v.Outcomes.Single().CastResults==1)&&SorceressSpellMissiles.Snapshot().NextId==id+2,"Proc restriction leaked after spin");}
            finally{Clean(p,after);}
        }
        finally{PlayerItemProcWorld.Observed=observed;}
    }
    private static void DelayedDeath()
    {
        var p=Owner(out _,out _);var t=Target(p);using var proof=PlayerItemProcWorld.Candidate(p);var observed=PlayerItemProcWorld.Observed;int count=0;
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){count++;}};int before=SkillHealth.Balance256(t);
            Require(WhirlwindChecks.Begin(p),"Zero-life spin entry");p.Damage(p.Hits+1);Require(p.Alive&&SkillHealth.Balance256(p)==0&&BarbarianWhirlwind.State(p).DelayedDeath,"Live source mode did not defer death");
            WhirlwindChecks.Step(p,3);Require(count==2&&before-SkillHealth.Balance256(t)==153&&p.Alive,"Zero-life active mode lost contact");t.Delete();WhirlwindChecks.Finish(p);
            Require(!p.Alive&&BarbarianWhirlwind.State(p)==null&&p.Corpse!=null,"Delayed death did not settle after contact");
        }
        finally{PlayerItemProcWorld.Observed=observed;Clean(p,t);p.Corpse?.Delete();p.Delete();}
    }
    private static void DualHands()
    {
        var p=WhirlwindChecks.Fresh();var a=Target(p);var b=Target(p);b.MoveToWorld(new(p.X+1,p.Y+1,p.Z),p.Map);DruidSetupItemChecks.Named(p,"Venom Grip");ClassGear.Refresh(p);
        // Real Hellplague and Deathspade carry different selected-hand poison.
        var pair=WeaponHands.Pair(p).Value;var observed=PlayerItemProcWorld.Observed;
        try
        {
            // These originals have no admitted chance-to-cast;observe packets
            // using the already-qualified complete Hellslayer in separate tests.
            var ah=SkillHealth.Balance256(a);var bh=SkillHealth.Balance256(b);
            Require(WhirlwindChecks.Begin(p),"Dual hand spin");WhirlwindChecks.Step(p,3);
            foreach(var (weapon,t,before) in new[]{(pair.Primary,a,ah),(pair.Secondary,b,bh)})
            {using(WeaponHands.Use(p,weapon)){var raw=BowLaunch.RawPoison(p,weapon);Require(before-SkillHealth.Balance256(t)==raw.PoisonMin,"Selected hand initial poison leaked across weapons");}}
        }
        finally{PlayerItemProcWorld.Observed=observed;a.Delete();b.Delete();Clean(p,null);p.Delete();}
    }
    private static void Check(bool reload)
    {
        using var rolls=new Rolls();PlayerMobile p;Item weapon,gloves;int generation=0;
        if(!reload){p=Owner(out weapon,out gloves);}
        else
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);weapon=World.FindItem((Serial)s.Weapon);gloves=World.FindItem((Serial)s.Gloves);
            Require(generation<=2&&weapon.Parent==p&&gloves.Parent==p&&Json(new[]{ClassGear.State(weapon),ClassGear.State(gloves)})==s.Gear&&Json(Own(p,weapon))==s.Profile&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Saved original/proc/wounds/resources changed");p.MoveToWorld(s.Point,Map.Trammel);
        }
        Scenarios(p,weapon);DelayedDeath();DualHands();SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);var point=p.Location;p.Internalize();
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,weapon.Serial.Value,gloves.Serial.Value,point,Json(new[]{ClassGear.State(weapon),ClassGear.State(gloves)}),Json(Own(p,weapon)),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),generation)));
    }
    private static void Input(NetState n,string label)
    {
        var rolls=new Rolls();var p=Owner(out _,out _);var t=Target(p);WhirlwindChecks.Ready(p);var account=FixtureNetwork.Authenticate(n,p,"whirlwind-contact-");var observed=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();
        PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};int mana=NecromancerEffects.ManaBalance256(p);long first=SorceressSpellMissiles.Snapshot().NextId,deadline=Core.TickCount+6000;bool began=false;
        void Close(){Clean(p,t);PlayerItemProcWorld.Observed=observed;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Whirlwind wire timeout");if(!began&&BarbarianWhirlwind.Locked(p)){began=true;Log.Information("{Label}: acknowledged",label);}if(visits.Count<2){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(began&&visits.Count==2&&BarbarianWhirlwind.Locked(p)&&visits[0].Phase=="domeleedamage"&&visits[0].Packet.Poison256==153&&visits[1].Phase=="domeleeattack"&&visits.All(v=>v.Outcomes.Single() is{CastAttempts:1,CastResults:0})&&SorceressSpellMissiles.Snapshot().NextId==first&&NecromancerEffects.ManaBalance256(p)==mana-3200,"Wire spin packet,proc suppression or payment differs");
                t.Delete();Close();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Whirlwind,(int)DiabloClass.Barbarian,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
