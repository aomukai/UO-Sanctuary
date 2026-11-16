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

public static class MagicArrowContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(MagicArrowContactChecks));
    private sealed record Saved(uint Owner,uint Target,uint Bow,uint Gloves,uint Helm,Point3D Point,string Gear,string Profile,WeaponFlight Flight,int Life,int TargetLife,int Mana,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"magic-arrow-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private sealed class High:Random{public override int Next(int n)=>n-1;}
    private sealed class Low:Random{public override int Next(int n)=>0;}
    internal sealed class Rolls:IDisposable
    {
        private readonly Func<double> Hit=WeaponMissiles.HitRoll;
        private readonly Func<int,int> Damage=WeaponMissiles.DamageRoll,Block=SourceBlocking.BlockRoll,Choice=SourceBlocking.ChoiceRoll;
        private readonly Random Effects=WeaponMissiles.ItemEffects,Launch=AmazonBow.LaunchRandom;
        internal Rolls(){WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=_=>1;WeaponMissiles.ItemEffects=AmazonBow.LaunchRandom=new High();}
        public void Dispose(){WeaponMissiles.HitRoll=Hit;WeaponMissiles.DamageRoll=Damage;SourceBlocking.BlockRoll=Block;SourceBlocking.ChoiceRoll=Choice;WeaponMissiles.ItemEffects=Effects;AmazonBow.LaunchRandom=Launch;}
    }
    internal static PlayerProcSave Own(PlayerMobile p,Item bow)
    {var s=PlayerItemProcs.Capture();return s with{Items=s.Items.Where(x=>x.Item==bow.Serial.Value).ToArray(),Owners=s.Owners.Where(x=>x.Actor==p.Serial.Value).ToArray()};}
    internal static WeaponFlight[] Flights(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"magic-arrow-contact-reload-check":"magic-arrow-contact-check";ConsoleInputHandler.RegisterCommand(name,"Magic Arrow ordered weapon missile contact and captured armor metadata",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"right","quick"})
        {string label="magic-arrow-contact-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated Magic Arrow hit proc and no-ammo/resource rule",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    internal static PlayerMobile Owner(int row,bool crossbow,out BaseWeapon bow,out Item gloves,out Item helm)
    {
        var p=HellslayerChecks.Owner(DiabloClass.Amazon,row);ClassSkillChecks.Train(p,"amazon.magicarrow");bow=(BaseWeapon)PlayerItemProcWorldChecks.Carrier(p,crossbow?new Crossbow():new Bow());gloves=DruidSetupItemChecks.Named(p,"Venom Grip");helm=DruidSetupItemChecks.Named(p,"Blackhorn's Face");ClassGear.Refresh(p);
        p.AddToBackpack(new Arrow(7));p.AddToBackpack(new Bolt(11));Require(SkillBindings.Instance.Assign(p,false,CombatAbility.MagicArrow)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.MagicArrow),"Magic Arrow contact bindings");return p;
    }
    internal static PoisonDaggerContactChecks.Recipient Target(PlayerMobile p,int dx=3)
    {var t=new PoisonDaggerContactChecks.Recipient();t.SetHits(60000);t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Poison,0);return t;}
    internal static void Ready(PlayerMobile p)
    {AmazonBow.Cancel(p);ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Ammo(PlayerMobile p)=>Require(p.Backpack.FindItemByType<Arrow>()?.Amount==7&&p.Backpack.FindItemByType<Bolt>()?.Amount==11,"Magic Arrow consumed a quiver");
    internal static void Clean(PlayerMobile p,params BaseCreature[] targets)
    {AmazonBow.Cancel(p);ClassCombat.Stop(p);WeaponMissiles.Cancel(p);SorceressSpellMissiles.Cancel(p);foreach(var t in targets){if(t!=null){SourceKnockbacks.Clear(t);t.Corpse?.Delete();t.Delete();}}}
    internal static void Hold(PlayerMobile p,WeaponFlight f)
    {var s=WeaponMissiles.Snapshot();WeaponMissiles.Restore(s with{Flights=s.Flights.Where(x=>x.Owner!=p.Serial.Value).Append(f).ToArray()});}
    private static WeaponFlight Launch(PlayerMobile p,BaseCreature target)
    {Require(ClassCombat.Perform(p,CombatAbility.MagicArrow,target,target.Location,Core.TickCount),"Learned Magic Arrow contact entry");ClassCombat.ReleaseCast(p);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Learned Magic Arrow release");return Flights(p).Single();}
    internal static void Drain(PlayerMobile p)
    {for(int i=0;i<80&&Flights(p).Length>0;i++){WeaponMissiles.Tick(Flights(p).Min(x=>x.NextUtc),onlyOwner:p.Serial.Value);}Require(Flights(p).Length==0,"Magic Arrow flight did not finish");}
    // Packet-boundary fixture: whole Soul Drainer admission is deliberately not bypassed.
    // A small supported flat armor value is injected into a released packet;
    // live gear remains unchanged, proving the impact/reload reads the packet.
    private static WeaponFlight ArmorPacket(PlayerMobile p,WeaponFlight f)
    {f=f with{Damage=f.Damage with{ArmorDelta=-1,Elements=f.Damage.Elements with{PoisonMin=0,PoisonMax=0,PoisonFrames=0,PoisonCount=0}}};Hold(p,f);return f;}
    private static void Scenarios(PlayerMobile p,BaseWeapon bow,Item gloves)
    {
        using var candidate=PlayerItemProcWorld.Candidate(p);var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();var expectedTargets=new Queue<(BaseCreature Target,int Life)>();
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&v.Packet.HitFlags==32&&expectedTargets.Count>0,"Magic Arrow fabricated an attack event or lost weapon flags");var e=expectedTargets.Dequeue();Require(SkillHealth.Balance256(e.Target)==e.Life,"Magic Arrow proc followed HP commit");visits.Add(v);}};
            foreach(string scenario in new[]{"hit","miss","block","immune","removed","physical","lethal","suppressed","pierce","armor","blocked-armor","suppressed-armor"})
            {
                Ready(p);var t=Target(p);var second=scenario=="pierce"?Target(p,6):null;Item belt=null;bool armor=scenario.Contains("armor"),blocked=scenario is "block" or "blocked-armor",suppressed=scenario.StartsWith("suppressed");
                if(scenario=="immune"){t.SetResistance(ResistanceType.Poison,100);}if(scenario=="physical"){t.SetResistance(ResistanceType.Physical,0);t.SetResistance(ResistanceType.Energy,100);}
                if(scenario=="lethal"){SkillHealth.SetBalance256(t,1);}WeaponMissiles.HitRoll=()=>scenario=="miss"?1:0;SourceBlocking.BlockRoll=n=>{Require(!TimedEffects.CanRegenerateHits(t),"Accuracy PMH did not precede block");return blocked?0:n-1;};
                if(scenario=="pierce"){belt=DruidSetupItemChecks.Named(p,"Razortail");AmazonBow.LaunchRandom=new Low();}
                visits.Clear();expectedTargets.Clear();NpcWorldAllocation.RequireStream();var world=NpcWorldAllocation.Capture();var profile=Own(p,bow);int mana=NecromancerEffects.ManaBalance256(p),before=SkillHealth.Balance256(t),cost=SkillMechanics.Values(p,CombatAbility.MagicArrow).Mana256;
                try
                {
                    var f=Launch(p,t);if(armor){f=ArmorPacket(p,f);}Require(BowMissileContact.Handles(f)&&f.Damage.PhysicalToMagicPercent==5&&f.Damage.ArmorDelta==(armor?-1:0)&&visits.Count==0&&SkillHealth.Balance256(t)==before&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Json(Own(p,bow))==Json(profile)&&NpcWorldAllocation.Capture()==world,"Magic Arrow release snapshot/proc/resource");Ammo(p);
                    if(suppressed){f=f with{Flags=f.Flags|0x10000};Hold(p,f);}if(scenario=="removed"){p.AddToBackpack(bow);p.AddToBackpack(gloves);ClassGear.Refresh(p);}
                    bool hit=scenario!="miss"&&!blocked,proc=hit&&!suppressed&&scenario!="removed";
                    if(proc){expectedTargets.Enqueue((t,before));if(second!=null){expectedTargets.Enqueue((second,SkillHealth.Balance256(second)));}}
                    var projected=f.Damage.Roll(t,_=>0,out _);int direct=NamedElements.To256(SkillCombat.MitigatePrepared(p,t,projected with{Poison=0}).Total);Drain(p);int count=proc?(second==null?1:2):0,rate=hit&&!armor&&scenario!="immune"?153:0;
                    Require(visits.Count==count&&visits.All(v=>v.Outcomes.Single()is{CastAttempts:1,CastResults:1}),"Magic Arrow proc eligibility");
                    int expected=hit?Math.Min(before,direct+rate):0;
                    Require(before-SkillHealth.Balance256(t)==expected&&NecromancerEffects.ManaBalance256(p)==mana-cost&&NativeMonsterReturn.Session(t).Packets==(hit?1:0),"Magic Arrow typed HP,return or repeated payment");Ammo(p);
                    if(scenario!="lethal"){Require(TimedEffects.Slow(t,TimedEffects.Now)==(hit&&!suppressed?20:0),"Magic Arrow slow/suppression/immunity event");}
                    Require(TimedEffects.CanRegenerateHits(t)==(scenario=="miss"),"Magic Arrow PMH hit/block/suppression behavior");
                    if(armor){Require(TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value).DefenseLoss==1,"Captured armor delta absent,duplicated or reread after gear change");}
                    if(scenario=="physical"){Require(t.Notifications==1&&t.AtNotification==before&&visits[0].Packet.Physical256>0&&visits[0].Packet.Magic256>0,"Magic Arrow conversion/native notification");}
                    if(second!=null){Require(second.HitsMax*256-SkillHealth.Balance256(second)==direct+153,"Piercing Magic Arrow missed second contact");}
                    ulong seed=world.Seed;var actor=profile.Owners.SingleOrDefault();ulong actorSeed=actor?.Seed??0;
                    if(count>0){if(actor==null){actorSeed=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref seed)));}for(int i=0;i<count;i++){SourceRandom.Roll(ref actorSeed);SourceRandom.Roll(ref seed);}Require(Own(p,bow).Owners.Single().Seed==actorSeed,"Magic Arrow actor proc draws");}
                    else if(actor!=null){Require(Own(p,bow).Owners.Single().Seed==actorSeed,"Rejected arrow proc advanced actor");}Require(NpcWorldAllocation.Capture().Seed==seed,"Magic Arrow child/world stream draws");
                }
                catch(Exception e){throw new InvalidOperationException("Magic Arrow contact "+scenario,e);}
                finally{belt?.Delete();Clean(p,t,second);if(bow.Parent!=p){Require(p.EquipItem(bow),"Restore bow");}if(gloves.Parent!=p){Require(p.EquipItem(gloves),"Restore gloves");}ClassGear.Refresh(p);AmazonBow.LaunchRandom=new High();SourceBlocking.BlockRoll=n=>n-1;}
            }
        }
        finally{PlayerItemProcWorld.Observed=old;}
    }
    private static void Check(bool reload)
    {
        using var rolls=new Rolls();PlayerMobile p;BaseWeapon bow;Item gloves,helm;int generation=0;
        if(!reload){p=Owner(1700,false,out bow,out gloves,out helm);}
        else
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);bow=(BaseWeapon)World.FindItem((Serial)s.Bow);gloves=World.FindItem((Serial)s.Gloves);helm=World.FindItem((Serial)s.Helm);var target=(BaseCreature)World.FindMobile((Serial)s.Target);
            Require(generation<=2&&bow.Parent==p&&gloves.Parent==p&&helm.Parent==p&&Json(new[]{ClassGear.State(bow),ClassGear.State(gloves),ClassGear.State(helm)})==s.Gear&&Json(Own(p,bow))==s.Profile&&Json(Flights(p).Single())==Json(s.Flight)&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(target)==s.TargetLife&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Saved Magic Arrow original/proc/flight/resource state");
            p.MoveToWorld(s.Point,Map.Trammel);target.Frozen=target.CantWalk=true;target.AIObject.AITimer.Stop();target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);foreach(var r in Enum.GetValues<ResistanceType>()){target.SetResistance(r,100);}target.SetResistance(ResistanceType.Poison,0);
            int before=SkillHealth.Balance256(target);var projected=s.Flight.Damage.Roll(target,_=>0,out _);int direct=NamedElements.To256(SkillCombat.MitigatePrepared(p,target,projected with{Poison=0}).Total);using(PlayerItemProcWorld.Candidate(p)){Hold(p,s.Flight with{NextUtc=TimedEffects.Now});Drain(p);}Require(before-SkillHealth.Balance256(target)==direct&&TimedEffects.Snapshot().Single(x=>x.Owner==target.Serial.Value).DefenseLoss==1&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Restored captured armor flight changed damage/debited");Clean(p,target);
        }
        Scenarios(p,bow,gloves);
        var other=Owner(2200,true,out _,out _,out _);var victim=Target(other);Ready(other);using(PlayerItemProcWorld.Candidate(other)){var arrow=Launch(other,victim);var projected=arrow.Damage.Roll(victim,_=>0,out _);int direct=NamedElements.To256(SkillCombat.MitigatePrepared(other,victim,projected with{Poison=0}).Total);Drain(other);Require(victim.HitsMax*256-SkillHealth.Balance256(victim)==direct+153,"Crossbow shared Magic Arrow route");}Ammo(other);Clean(other,victim);other.Delete();
        Ready(p);var t=Target(p);var f=ArmorPacket(p,Launch(p,t));WeaponMissiles.Tick(f.NextUtc,onlyOwner:p.Serial.Value);f=Flights(p).Single()with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};Hold(p,f);
        var all=WeaponMissiles.Snapshot();bool refused=false;try{WeaponMissiles.Restore(all with{Flights=all.Flights.Select(x=>x.Id==f.Id?x with{Damage=x.Damage with{ArmorDelta=1}}:x).ToArray()});}catch(InvalidDataException){refused=true;}Require(refused&&Json(WeaponMissiles.Snapshot())==Json(all),"Invalid armor delta partially mutated flight store");
        SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);NecromancerEffects.SetMana256(p,17);var point=p.Location;p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,t.Serial.Value,bow.Serial.Value,gloves.Serial.Value,helm.Serial.Value,point,Json(new[]{ClassGear.State(bow),ClassGear.State(gloves),ClassGear.State(helm)}),Json(Own(p,bow)),f,SkillHealth.Balance256(p),SkillHealth.Balance256(t),NecromancerEffects.ManaBalance256(p),generation)));
    }
    private static void Input(NetState n,string label)
    {
        var rolls=new Rolls();var p=Owner(2300,false,out _,out _,out _);var t=Target(p);Ready(p);var account=FixtureNetwork.Authenticate(n,p,"magic-arrow-contact-");var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.MagicArrow).Mana256;long deadline=Core.TickCount+6000;bool began=false,sawFlight=false;
        void Close(){Clean(p,t);PlayerItemProcWorld.Observed=old;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try
            {Require(Core.TickCount<deadline,"Magic Arrow contact input timeout");var s=ClassCombat.For(p);if(!began&&s.PendingAbility==CombatAbility.MagicArrow){began=true;Require(visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana,"Wire bow early debit/proc");Log.Information("{Label}: windup",label);}if(Flights(p).Length>0){sawFlight=true;}
                if(!sawFlight||Flights(p).Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(began&&visits.Count==1&&visits[0].Phase=="domissiledamage"&&visits[0].Packet.Poison256==153&&visits[0].Outcomes.Single()is{CastAttempts:1,CastResults:1}&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Wire Magic Arrow impact/proc/payment");Ammo(p);Close();Log.Information("{Label}-result: passed",label);}
            catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.MagicArrow,(int)DiabloClass.Amazon,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
