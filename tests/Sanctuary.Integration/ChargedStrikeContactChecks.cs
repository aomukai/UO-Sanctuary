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

public static class ChargedStrikeContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ChargedStrikeContactChecks));
    private const CombatAbility Ability=CombatAbility.ChargedStrike;
    private sealed class High:Random{public override int Next(int n)=>n-1;}
    private sealed class NoWeaponEffects:Random{public override int Next(int n)=>throw new InvalidOperationException("Charged Strike source0 bolt requested weapon effects");}
    internal sealed class Rolls:IDisposable
    {
        private readonly MagicArrowContactChecks.Rolls Missiles=new();
        private readonly Func<double> Hit=AmazonMelee.HitRoll,Element=AmazonMelee.ElementRoll;
        private readonly Func<int,int> Physical=AmazonMelee.PhysicalRoll,Poison=AmazonMelee.PoisonRoll,Lightning=AmazonMelee.LightningRoll,Bolt=ChargedStrikeBolts.DamageRoll;
        private readonly Random Effects=AmazonMelee.Effects,Wear=AmazonMelee.WearRoll;
        internal Rolls(){AmazonMelee.HitRoll=()=>1;AmazonMelee.ElementRoll=()=>0;AmazonMelee.PhysicalRoll=AmazonMelee.PoisonRoll=AmazonMelee.LightningRoll=ChargedStrikeBolts.DamageRoll=_=>0;AmazonMelee.Effects=AmazonMelee.WearRoll=new High();}
        public void Dispose(){AmazonMelee.HitRoll=Hit;AmazonMelee.ElementRoll=Element;AmazonMelee.PhysicalRoll=Physical;AmazonMelee.PoisonRoll=Poison;AmazonMelee.LightningRoll=Lightning;ChargedStrikeBolts.DamageRoll=Bolt;AmazonMelee.Effects=Effects;AmazonMelee.WearRoll=Wear;Missiles.Dispose();}
    }
    private sealed record Saved(uint Owner,uint Target,uint Weapon,uint[] Items,Point3D Point,string Gear,string Profile,ChargedStrikeFlight Flight,int Life,int TargetLife,int Mana,int Durability,int Quantity,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"charged-strike-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static ChargedStrikeFlight[] Flights(PlayerMobile p)=>ChargedStrikeBolts.Snapshot().Where(x=>x.Owner==p.Serial.Value).OrderBy(x=>x.Id).ToArray();
    private static PlayerProcSave Own(PlayerMobile p,Item w)=>MagicArrowContactChecks.Own(p,w);
    private static TimedEffectState State(Mobile t)=>TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value);
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"charged-strike-contact-reload-check":"charged-strike-contact-check";ConsoleInputHandler.RegisterCommand(name,"Charged Strike source0 bolt contacts and original saved context",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"right","quick"})
        {string label="charged-strike-contact-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated Charged Strike miss still emits independent source0 damage",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    internal static PlayerMobile Owner(int row,out BaseWeapon w,out Item[] gear)
    {var p=HellslayerChecks.Owner(DiabloClass.Amazon,row);ClassSkillChecks.Train(p,SkillMechanics.For(Ability).TalentId);w=(BaseWeapon)PlayerItemProcWorldChecks.Carrier(p,new Spear());gear=new[]{DruidSetupItemChecks.Named(p,"Venom Grip"),DruidSetupItemChecks.Named(p,"Blackhorn's Face")};ClassGear.Refresh(p);Require(SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Charged Strike bindings");return p;}
    internal static BaseCreature Target(PlayerMobile p,int dx=1)
    {var t=new ExplosiveArrowContactChecks.AreaRecipient();t.SetHits(60000);t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Energy,0);return t;}
    internal static void Ready(PlayerMobile p){AmazonMelee.Cancel(p);MagicArrowContactChecks.Ready(p);}
    internal static void Clean(PlayerMobile p,params BaseCreature[] targets){ChargedStrikeBolts.Cancel(p);MagicArrowContactChecks.Clean(p,targets);}
    private static void Keep(PlayerMobile p,params ChargedStrikeFlight[] flights)
        =>ChargedStrikeBolts.Restore(ChargedStrikeBolts.Snapshot().Where(x=>x.Owner!=p.Serial.Value).Concat(flights).ToArray());
    // Actual learned miss emits the original three factory-created bolts. The
    // separately qualified on-attack Fire Ball is cancelled before bolt isolation.
    internal static ChargedStrikeFlight[] Birth(PlayerMobile p,BaseWeapon w,bool successful=false)
    {
        Ready(p);var savedHit=AmazonMelee.HitRoll;AmazonMelee.HitRoll=()=>successful?0:1;var t=Target(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256,dur=w.HitPoints,quantity=NamedAmmunition.Remaining(w);
        try{Require(ClassCombat.Perform(p,Ability,t,t.Location,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Charged Strike start payment");ClassCombat.ReleaseCast(p);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Charged Strike do11 release");var f=Flights(p);Require(f.Length==3&&f.All(x=>x.Frame==0&&x.FactoryMotion==new ChargedStrikeFactoryMotion(2304,100))&&NecromancerEffects.ManaBalance256(p)==mana-cost&&(successful||w.HitPoints==dur)&&NamedAmmunition.Remaining(w)==quantity&&NativeMonsterReturn.Session(t).Packets==1,"Charged Strike missed parent/emission/resource split");return f;}
        finally{AmazonMelee.HitRoll=savedHit;SorceressSpellMissiles.Cancel(p);t.Corpse?.Delete();t.Delete();}
    }
    private static void BoltCases(PlayerMobile p,BaseWeapon w,Item[] gear)
    {
        using var candidate=PlayerItemProcWorld.Candidate(p);
        foreach(string mode in new[]{"hit","immune","resisted","lethal","removed","two","shotgun","legacy"})
        {
            var old=PlayerItemProcWorld.Observed;var effects=WeaponMissiles.ItemEffects;BaseCreature a=null,b=null;
            try
            {
                var born=Birth(p,w);var first=born[0];a=Target(p);a.SetResistance(ResistanceType.Physical,0);if(mode=="two"){b=Target(p);}if(mode=="immune"){a.SetResistance(ResistanceType.Energy,100);}if(mode=="resisted"){a.SetResistance(ResistanceType.Energy,50);}if(mode=="lethal"){SkillHealth.SetBalance256(a,1);}if(mode=="legacy"){first=first with{FactoryMotion=null};}Keep(p,mode=="shotgun"?born:new[]{first});if(mode=="removed"){foreach(var item in new[]{(Item)w}.Concat(gear)){p.AddToBackpack(item);}ClassGear.Refresh(p);}int before=SkillHealth.Balance256(a),other=b==null?0:SkillHealth.Balance256(b),mana=NecromancerEffects.ManaBalance256(p),dur=w.HitPoints,quantity=NamedAmmunition.Remaining(w),visits=0;var profile=Json(Own(p,w));NpcWorldAllocation.RequireStream();var world=NpcWorldAllocation.Capture();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits++;}};WeaponMissiles.ItemEffects=new NoWeaponEffects();SourceBlocking.BlockRoll=_=>throw new InvalidOperationException("Charged Strike bolt requested physical shield");WeaponMissiles.HitRoll=()=>throw new InvalidOperationException("Charged Strike bolt requested accuracy");
                foreach(var f in Flights(p)){ChargedStrikeBolts.Tick(f.NextUtc,f.Id);}Require(Flights(p).Length==0,"Bolt failed first-collision removal");int damage=mode=="immune"?0:mode=="resisted"?first.Min256/2:first.Min256,count=mode=="shotgun"?3:1;
                Require(before-SkillHealth.Balance256(a)+(b==null?0:other-SkillHealth.Balance256(b))==Math.Min(before,damage*count)&&NativeMonsterReturn.Session(a).Packets+(b==null?0:NativeMonsterReturn.Session(b).Packets)==count,"Bolt initial HP/native packet: "+mode);
                Require(visits==0&&State(a)?.PoisonClock==null&&State(a)?.Cold==null&&(State(a)?.HealBlockedUntilUtc??default)==default&&TimedEffects.Slow(a,TimedEffects.Now)==0&&TimedEffects.DefenseLoss(a)==0&&Json(Own(p,w))==profile&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(p)==mana&&w.HitPoints==dur&&NamedAmmunition.Remaining(w)==quantity,"Bolt weapon event/status/RNG/resource leak: "+mode);
            }
            catch(Exception e){throw new InvalidOperationException("Charged Strike bolt "+mode,e);}
            finally{PlayerItemProcWorld.Observed=old;WeaponMissiles.ItemEffects=effects;SourceBlocking.BlockRoll=n=>n-1;WeaponMissiles.HitRoll=()=>0;Clean(p,a,b);foreach(var item in new[]{(Item)w}.Concat(gear)){if(item.Parent!=p){Require(p.EquipItem(item),"Restore bolt provider");}}ClassGear.Refresh(p);}
        }
    }
    private static void Check(bool reload)
    {
        using var rolls=new Rolls();PlayerMobile p;BaseWeapon w;Item[] gear;int generation=0;
        if(!reload){p=Owner(3200,out w,out gear);using(PlayerItemProcWorld.Candidate(p)){Birth(p,w,true);ChargedStrikeBolts.Cancel(p);}Require(Own(p,w).Owners.Length==1,"Real melee did not initialize original proc stream");BoltCases(p,w,gear);}
        else
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);w=(BaseWeapon)World.FindItem((Serial)s.Weapon);gear=s.Items.Select(id=>World.FindItem((Serial)id)).ToArray();var t=(BaseCreature)World.FindMobile((Serial)s.Target);
            Require(generation<=2&&w.Parent==p&&gear.All(x=>x.Parent==p)&&Json(new[]{(Item)w}.Concat(gear).Select(ClassGear.State))==s.Gear&&Json(Own(p,w))==s.Profile&&Flights(p).Single()==s.Flight&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(t)==s.TargetLife&&NecromancerEffects.ManaBalance256(p)==s.Mana&&w.HitPoints==s.Durability&&NamedAmmunition.Remaining(w)==s.Quantity,"Original bolt/path/gear/proc/wound/resources changed on reload");p.MoveToWorld(s.Point,Map.Trammel);t.CantWalk=t.Frozen=true;t.AIObject.AITimer.Stop();t.MoveToWorld(new((int)Math.Round(s.Flight.X),(int)Math.Round(s.Flight.Y),s.Flight.Z),p.Map);foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Energy,0);var old=PlayerItemProcWorld.Observed;var effects=WeaponMissiles.ItemEffects;int visits=0;var world=NpcWorldAllocation.Capture();
            try{using var candidate=PlayerItemProcWorld.Candidate(p);PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits++;}};WeaponMissiles.ItemEffects=new NoWeaponEffects();SourceBlocking.BlockRoll=_=>throw new InvalidOperationException("Restored bolt requested shield");var at=TimedEffects.Now;Keep(p,s.Flight with{NextUtc=at,UntilUtc=at.AddSeconds(4)});ChargedStrikeBolts.Tick(at,s.Flight.Id);Require(Flights(p).Length==0&&s.TargetLife-SkillHealth.Balance256(t)==s.Flight.Min256&&NativeMonsterReturn.Session(t).Packets==1&&visits==0&&Json(Own(p,w))==s.Profile&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(p)==s.Mana&&w.HitPoints==s.Durability&&NamedAmmunition.Remaining(w)==s.Quantity,"Restored original bolt replayed weapon/resource/RNG or lost damage");}
            finally{PlayerItemProcWorld.Observed=old;WeaponMissiles.ItemEffects=effects;SourceBlocking.BlockRoll=n=>n-1;Clean(p,t);}
        }
        ChargedStrikeFlight held;using(PlayerItemProcWorld.Candidate(p)){held=Birth(p,w)[0];}Keep(p,held);var target=Target(p,6);ChargedStrikeBolts.Tick(held.NextUtc,held.Id);held=Flights(p).Single();Require(held.Frame==1,"Original partial bolt setup");var until=TimedEffects.Now.Add(FixtureSpace.Retention);held=held with{NextUtc=until,UntilUtc=until.AddMinutes(1)};Keep(p,held);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2+17);NecromancerEffects.SetMana256(p,17);var point=p.Location;p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,target.Serial.Value,w.Serial.Value,gear.Select(x=>x.Serial.Value).ToArray(),point,Json(new[]{(Item)w}.Concat(gear).Select(ClassGear.State)),Json(Own(p,w)),held,SkillHealth.Balance256(p),SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(p),w.HitPoints,NamedAmmunition.Remaining(w),generation)));
    }
    private static void Input(NetState n,string label)
    {
        var rolls=new Rolls();var p=Owner(3400,out var w,out _);var t=Target(p);Ready(p);var account=FixtureNetwork.Authenticate(n,p,"charged-strike-contact-");var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256,dur=w.HitPoints,quantity=NamedAmmunition.Remaining(w),minimum=AmazonMelee.LightningRange(SkillMechanics.Values(p,Ability),NamedElements.MasteryPercent(p,"ltng")).Min;long deadline=Core.TickCount+6000;bool began=false;
        void Close(){Clean(p,t);PlayerItemProcWorld.Observed=old;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try{Require(Core.TickCount<deadline,"Charged Strike input timeout");if(!began&&ClassCombat.For(p).PendingAbility==Ability){began=true;Require(visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Wire Charged Strike start");Log.Information("{Label}: windup",label);}if(NativeMonsterReturn.Session(t).Packets<4||Flights(p).Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}Require(began&&visits.Count==1&&visits.Single().Phase=="domeleeattack"&&visits.Single().Outcomes.Single()is{CastAttempts:0,CastResults:0}&&NativeMonsterReturn.Session(t).Packets==4&&before-SkillHealth.Balance256(t)==3*minimum&&State(t)?.PoisonClock==null&&TimedEffects.Slow(t,TimedEffects.Now)==0&&NecromancerEffects.ManaBalance256(p)==mana-cost&&w.HitPoints==dur&&NamedAmmunition.Remaining(w)==quantity,"Wire missed melee and independent bolt HP/proc/resource split");Close();Log.Information("{Label}-result: passed",label);}catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Amazon,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
