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

public static class ThrownContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ThrownContactChecks));
    private sealed record Saved(uint Owner,uint Primary,uint Secondary,uint Gloves,uint Helm,Point3D Point,string Gear,string Profile,int Life,int Mana,int First,int Second,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"thrown-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private sealed class High:Random{public override int Next(int n)=>n-1;}
    private sealed class Low:Random{public override int Next(int n)=>0;}
    internal sealed class Rolls:IDisposable
    {
        private readonly Func<double> Hit=BarbarianDoubleThrow.HitRoll;
        private readonly Func<int,int> Damage=WeaponMissiles.DamageRoll,Block=SourceBlocking.BlockRoll,Choice=SourceBlocking.ChoiceRoll;
        private readonly Random Effects=WeaponMissiles.ItemEffects;
        private readonly Func<Random> Throw=BarbarianThrowing.RandomSource;
        internal Rolls(){BarbarianDoubleThrow.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=_=>1;WeaponMissiles.ItemEffects=new High();BarbarianThrowing.RandomSource=()=>new High();}
        public void Dispose(){BarbarianDoubleThrow.HitRoll=Hit;WeaponMissiles.DamageRoll=Damage;SourceBlocking.BlockRoll=Block;SourceBlocking.ChoiceRoll=Choice;WeaponMissiles.ItemEffects=Effects;BarbarianThrowing.RandomSource=Throw;}
    }
    internal static PlayerProcSave Own(PlayerMobile p,Item item)
    {var s=PlayerItemProcs.Capture();return s with{Items=s.Items.Where(x=>x.Item==item.Serial.Value).ToArray(),Owners=s.Owners.Where(x=>x.Actor==p.Serial.Value).ToArray()};}
    internal static WeaponFlight[] Flights(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"thrown-contact-reload-check":"thrown-contact-check";ConsoleInputHandler.RegisterCommand(name,"Thrown ordered missile damage,actual item hit procs and saved originals",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"right","quick"})
        {string label="thrown-contact-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated first Double Throw release,travel and item hit proc",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    internal static PlayerMobile Owner(int row,out BaseWeapon primary,out BaseWeapon secondary,out Item gloves,out Item helm)
    {
        var p=DoubleThrowChecks.Fresh();p.Name="PlayerItemFireBallFixture";FixtureSpace.Corridor(p,6200,32,row);var pair=BarbarianDoubleThrow.Pair(p).Value;
        p.AddToBackpack(pair.Primary);p.AddToBackpack(pair.Secondary);primary=(BaseWeapon)PlayerItemProcWorldChecks.Carrier(p,new NamedThrowingKnife());secondary=pair.Secondary;Require(p.EquipItem(secondary),"Restore secondary beside raw primary");gloves=DruidSetupItemChecks.Named(p,"Venom Grip");helm=DruidSetupItemChecks.Named(p,"Blackhorn's Face");ClassGear.Refresh(p);
        Require(BarbarianDoubleThrow.Pair(p)==new WeaponPair(primary,secondary),"Raw carrier changed selected pair");Require(SkillBindings.Instance.Assign(p,false,CombatAbility.DoubleThrow)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.DoubleThrow),"Throw bindings");return p;
    }
    internal static PoisonDaggerContactChecks.Recipient Target(PlayerMobile p,int dx=3)
    {var t=new PoisonDaggerContactChecks.Recipient();t.SetHits(60000);t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Poison,0);return t;}
    internal static void Ready(PlayerMobile p,BaseWeapon a,BaseWeapon b)
    {BarbarianDoubleThrow.Cancel(p);ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);DoubleThrowChecks.Quantity(a,40);DoubleThrowChecks.Quantity(b,40);}
    internal static void Clean(PlayerMobile p,params BaseCreature[] targets)
    {BarbarianDoubleThrow.Cancel(p);ClassCombat.Stop(p);WeaponMissiles.Cancel(p);SorceressSpellMissiles.Cancel(p);foreach(var t in targets){if(t!=null){SourceKnockbacks.Clear(t);t.Corpse?.Delete();t.Delete();}}}
    internal static void Drain(PlayerMobile p)
    {for(int i=0;i<60&&Flights(p).Length>0;i++){WeaponMissiles.Tick(Flights(p).Min(x=>x.NextUtc),onlyOwner:p.Serial.Value);}Require(Flights(p).Length==0,"Thrown contact flight did not finish");}
    private static void Scenarios(PlayerMobile p,BaseWeapon primary,BaseWeapon secondary)
    {
        using var candidate=PlayerItemProcWorld.Candidate(p);var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();var expectedRecipients=new Queue<(BaseCreature Target,int Life)>();int life=0;
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&v.Packet.HitFlags==32&&expectedRecipients.Count>0,"Thrown hit invented an attack event or lost flags");var expected=expectedRecipients.Dequeue();Require(SkillHealth.Balance256(expected.Target)==expected.Life,"Thrown hit proc ran after target HP commitment");visits.Add(v);}};
            foreach(string scenario in new[]{"hit","miss","block","immune","removed","physical","lethal","pierce"})
            {
                Ready(p,primary,secondary);var t=Target(p);var second=scenario=="pierce"?Target(p,6):null;Item belt=null;
                if(scenario=="immune"){t.SetResistance(ResistanceType.Poison,100);}if(scenario=="physical"){t.SetResistance(ResistanceType.Physical,0);}if(scenario=="lethal"){SkillHealth.SetBalance256(t,1);}
                BarbarianDoubleThrow.HitRoll=()=>scenario=="miss"?1:0;SourceBlocking.BlockRoll=n=>scenario=="block"?0:n-1;
                if(scenario=="pierce"){belt=DruidSetupItemChecks.Named(p,"Razortail");BarbarianThrowing.RandomSource=()=>new Low();}
                visits.Clear();expectedRecipients.Clear();NpcWorldAllocation.RequireStream();var world=NpcWorldAllocation.Capture();var profile=Own(p,primary);int mana=NecromancerEffects.ManaBalance256(p);life=SkillHealth.Balance256(t);
                try
                {
                    Require(DoubleThrowChecks.Begin(p,t),"Throw entry "+scenario);ClassCombat.ReleaseCast(p);long at=ClassCombat.For(p).EffectAt;
                    Require(visits.Count==0&&Flights(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana&&!ClassCombat.ResolveAction(p,at-1),"Throw startup emitted or paid");
                    Require(ClassCombat.ResolveAction(p,at),"Throw first release");var f=Flights(p).Single();ClassCombat.Stop(p);
                    Require(SkillHealth.Balance256(t)==life&&visits.Count==0&&NpcWorldAllocation.Capture()==world&&Json(Own(p,primary))==Json(profile)&&NecromancerEffects.ManaBalance256(p)==mana-256&&NamedAmmunition.Remaining(primary)==39&&NamedAmmunition.Remaining(secondary)==40,"Throw paid/procced at wrong phase");
                    if(scenario=="removed"){p.AddToBackpack(primary);ClassGear.Refresh(p);}
                    if(scenario is not("miss" or "block" or "removed")){expectedRecipients.Enqueue((t,life));if(second!=null){expectedRecipients.Enqueue((second,SkillHealth.Balance256(second)));}}
                    Drain(p);bool hit=scenario is not("miss" or "block"),proc=hit&&scenario!="removed";int count=proc?(second==null?1:2):0;
                    Require(visits.Count==count&&visits.All(v=>v.Outcomes.Single() is{CastAttempts:1,CastResults:1}),"Thrown hit/block/miss/provider proc gate");
                    int expected=hit?(scenario=="physical"?visits[0].Packet.Total256:scenario=="immune"?0:scenario=="lethal"?1:153):0;
                    Require(life-SkillHealth.Balance256(t)==expected&&NecromancerEffects.ManaBalance256(p)==mana-256&&NamedAmmunition.Remaining(primary)==39&&NamedAmmunition.Remaining(secondary)==40,"Thrown ordered HP or once-only resource");
                    Require(NativeMonsterReturn.Session(t).Packets==(hit?1:0),"Thrown packet return was missing or replayed");
                    if(scenario!="lethal"){Require(TimedEffects.Slow(t,TimedEffects.Now)==(hit?20:0),"Thrown item slow lost zero-damage hit or applied on miss/block");}
                    if(scenario=="physical"){Require(t.Notifications==1&&t.AtNotification==life&&visits[0].Packet.Physical256>0,"Thrown native notification/physical order");}
                    if(scenario=="lethal"){Require(!t.Alive&&visits[0].Packet.Poison256==153,"Lethal thrown hit suppressed its pre-HP proc");}
                    if(second!=null){Require(second.HitsMax*256-SkillHealth.Balance256(second)==153,"Pierced thrown contact missed second recipient");}
                    ulong seed=world.Seed;var actor=profile.Owners.SingleOrDefault();ulong actorSeed=actor?.Seed??0;
                    if(count>0)
                    {if(actor==null){actorSeed=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref seed)));}for(int i=0;i<count;i++){SourceRandom.Roll(ref actorSeed);SourceRandom.Roll(ref seed);}Require(Own(p,primary).Owners.Single().Seed==actorSeed,"Thrown actor stream draws changed");}
                    else if(actor!=null){Require(Own(p,primary).Owners.Single().Seed==actorSeed,"Rejected thrown proc rolled actor RNG");}
                    Require(NpcWorldAllocation.Capture().Seed==seed,"Thrown proc borrowed or rerolled world/projectile seeds");
                }
                catch(Exception e){throw new InvalidOperationException("Thrown provider scenario "+scenario,e);}
                finally{belt?.Delete();Clean(p,t,second);if(primary.Parent!=p){Require(p.EquipItem(primary),"Restore primary");DualEquip.Profiles.Store(p,primary.Serial);ClassGear.Refresh(p);}BarbarianThrowing.RandomSource=()=>new High();}
            }
        }
        finally{PlayerItemProcWorld.Observed=old;}
    }
    private static void Check(bool reload)
    {
        using var rolls=new Rolls();PlayerMobile p;BaseWeapon primary,secondary;Item gloves,helm;int generation=0;
        if(!reload){p=Owner(1700,out primary,out secondary,out gloves,out helm);}
        else
        {var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);primary=(BaseWeapon)World.FindItem((Serial)s.Primary);secondary=(BaseWeapon)World.FindItem((Serial)s.Secondary);gloves=World.FindItem((Serial)s.Gloves);helm=World.FindItem((Serial)s.Helm);
            Require(generation<=2&&primary.Parent==p&&secondary.Parent==p&&gloves.Parent==p&&helm.Parent==p&&Json(new[]{ClassGear.State(primary),ClassGear.State(secondary),ClassGear.State(gloves),ClassGear.State(helm)})==s.Gear&&Json(Own(p,primary))==s.Profile&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&NamedAmmunition.Remaining(primary)==s.First&&NamedAmmunition.Remaining(secondary)==s.Second,"Saved thrown originals,proc stream,wounds,mana or quantities changed");p.MoveToWorld(s.Point,Map.Trammel);}
        Scenarios(p,primary,secondary);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);NecromancerEffects.SetMana256(p,17);var point=p.Location;p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,primary.Serial.Value,secondary.Serial.Value,gloves.Serial.Value,helm.Serial.Value,point,Json(new[]{ClassGear.State(primary),ClassGear.State(secondary),ClassGear.State(gloves),ClassGear.State(helm)}),Json(Own(p,primary)),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),NamedAmmunition.Remaining(primary),NamedAmmunition.Remaining(secondary),generation)));
    }
    private static void Input(NetState n,string label)
    {
        var rolls=new Rolls();var p=Owner(2300,out var primary,out var secondary,out _,out _);var t=Target(p);Ready(p,primary,secondary);var account=FixtureNetwork.Authenticate(n,p,"thrown-contact-");var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
        int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t);long deadline=Core.TickCount+6000;bool began=false,emitted=false;
        void Close(){Clean(p,t);PlayerItemProcWorld.Observed=old;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try
            {Require(Core.TickCount<deadline,"Thrown proc wire timeout");if(!began&&ClassCombat.For(p).DoubleThrow!=null){began=true;Require(visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana,"Wire throw paid/procced at startup");Log.Information("{Label}: windup",label);}
                if(!emitted&&Flights(p).Length>0){emitted=true;Require(visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana-256,"Wire launch proc or payment");ClassCombat.Stop(p);}
                if(!emitted||Flights(p).Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(began&&visits.Count==1&&visits[0].Phase=="domissiledamage"&&visits[0].Outcomes.Single() is{CastAttempts:1,CastResults:1}&&hp-SkillHealth.Balance256(t)>=153&&NamedAmmunition.Remaining(primary)==39&&NamedAmmunition.Remaining(secondary)==40&&NecromancerEffects.ManaBalance256(p)==mana-256,"Wire thrown proc/HP/phase/resource");Close();Log.Information("{Label}-result: passed",label);}
            catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.DoubleThrow,(int)DiabloClass.Barbarian,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
