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

public static class MultipleShotContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(MultipleShotContactChecks));
    private sealed record Saved(uint Owner,uint Target,uint Bow,uint Gloves,uint Helm,uint Ammo,Point3D Point,string Gear,string Profile,WeaponFlight Flight,int Life,int TargetLife,int Mana,int Quantity,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"multiple-shot-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static WeaponFlight[] Flights(PlayerMobile p)=>MagicArrowContactChecks.Flights(p);
    private static PlayerProcSave Own(PlayerMobile p,Item bow)=>MagicArrowContactChecks.Own(p,bow);
    private static void Ready(PlayerMobile p)=>MagicArrowContactChecks.Ready(p);
    private static void Drain(PlayerMobile p)=>MagicArrowContactChecks.Drain(p);
    private static void Clean(PlayerMobile p,params BaseCreature[] t)=>MagicArrowContactChecks.Clean(p,t);
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"multiple-shot-contact-reload-check":"multiple-shot-contact-check";ConsoleInputHandler.RegisterCommand(name,"Multiple Shot ordered central/outer impact,source poison,item procs and saved original",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"right","quick"})
        {string label="multiple-shot-contact-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated Multiple Shot fan with once-only resources and central hit proc",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(int row,bool crossbow,out BaseWeapon bow,out Item gloves,out Item helm)
    {var p=MagicArrowContactChecks.Owner(row,crossbow,out bow,out gloves,out helm);ClassSkillChecks.Train(p,"amazon.multipleshot");p.Backpack.FindItemByType<Arrow>().Amount=100;p.Backpack.FindItemByType<Bolt>().Amount=100;Require(SkillBindings.Instance.Assign(p,false,CombatAbility.MultipleShot)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.MultipleShot),"Multiple Shot contact bindings");return p;}
    private static Item Ammo(PlayerMobile p)=>BowAmmunition.Stack(p,CombatTiming.Equipped(p));
    private static void Hold(PlayerMobile p,params WeaponFlight[] flights)
    {var s=WeaponMissiles.Snapshot();WeaponMissiles.Restore(s with{Flights=s.Flights.Where(x=>x.Owner!=p.Serial.Value).Concat(flights).ToArray()});}
    private static WeaponFlight[] Launch(PlayerMobile p,BaseCreature target)
    {
        var prior=Flights(p).Select(x=>x.Id).ToHashSet();int mana=NecromancerEffects.ManaBalance256(p),quantity=Ammo(p).Amount,cost=SkillMechanics.Values(p,CombatAbility.MultipleShot).Mana256;
        Require(ClassCombat.Perform(p,CombatAbility.MultipleShot,target,target.Location,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity,"Fan start payment/ammo phase");ClassCombat.ReleaseCast(p);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Learned fan release");var fan=Flights(p).Where(x=>!prior.Contains(x.Id)).ToArray();
        Require(fan.Length==Math.Min(24,SkillMechanics.Rank(p,CombatAbility.MultipleShot)+1)&&fan.Count(x=>(x.Flags&0x10000)==0)==2&&fan.All(x=>BowMissileContact.Handles(x)&&x.NextDelay==4&&x.Damage.PhysicalToMagicPercent==0)&&Ammo(p).Amount==quantity-1&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Fan partition,captured packet or once resources");return fan;
    }
    private static void Scenarios(PlayerMobile p,BaseWeapon bow,Item gloves)
    {
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"amazon.multipleshot",out _),"Rank2 fan for outer arrow");using var candidate=PlayerItemProcWorld.Candidate(p);var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();var expected=new Queue<(BaseCreature Target,int Life)>();
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&v.Packet.HitFlags==32&&expected.Count>0,"Outer/launch arrow fabricated item event");var e=expected.Dequeue();Require(SkillHealth.Balance256(e.Target)==e.Life,"Fan item proc followed HP");visits.Add(v);}};
            foreach(string name in new[]{"central","outer","miss","block","immune","removed","physical","armor-outer","pierce","fan-delay","independent-casts"})
            {
                Ready(p);var t=MagicArrowContactChecks.Target(p);var second=name=="pierce"?MagicArrowContactChecks.Target(p,6):null;Item belt=null;
                if(name=="immune"){t.SetResistance(ResistanceType.Poison,100);}if(name=="physical"){t.SetResistance(ResistanceType.Physical,0);}WeaponMissiles.HitRoll=()=>name=="miss"?1:0;SourceBlocking.BlockRoll=n=>{Require(!TimedEffects.CanRegenerateHits(t),"Fan accuracy PMH did not precede block");return name=="block"?0:n-1;};
                if(name=="pierce"){belt=DruidSetupItemChecks.Named(p,"Razortail");AmazonBow.LaunchRandom=new Minimum();PlayerPierceFixtures.NextBudget(p,4);}
                visits.Clear();expected.Clear();NpcWorldAllocation.RequireStream();var world=NpcWorldAllocation.Capture();var profile=Own(p,bow);int mana=NecromancerEffects.ManaBalance256(p),before=SkillHealth.Balance256(t),quantity=Ammo(p).Amount,cost=SkillMechanics.Values(p,CombatAbility.MultipleShot).Mana256;
                try
                {
                    var fan=Launch(p,t);var central=fan.Where(x=>(x.Flags&0x10000)==0).OrderBy(x=>Math.Abs(x.Vy)).First();var selected=name is "outer" or "armor-outer"?fan.First(x=>(x.Flags&0x10000)!=0):central;
                    Require(selected.Damage.Elements.PoisonMin==114&&visits.Count==0&&SkillHealth.Balance256(t)==before&&Json(Own(p,bow))==Json(profile)&&NpcWorldAllocation.Capture()==world,"Fan SrcDamage96/source poison or launch proc");
                    if(name=="armor-outer"){selected=selected with{Damage=selected.Damage with{ArmorDelta=-1}};}
                    Hold(p,selected);
                    if(name=="fan-delay"){Hold(p,central,fan.First(x=>x.Id!=central.Id&&(x.Flags&0x10000)==0));}
                    if(name=="independent-casts")
                    {AmazonBow.Cancel(p);ClassCombat.Stop(p);var session=ClassCombat.For(p);session.NextAction=session.RecoveryUntil=Core.TickCount;var next=Launch(p,t).Where(x=>x.Cast!=central.Cast&&(x.Flags&0x10000)==0).OrderBy(x=>Math.Abs(x.Vy)).First();Hold(p,central,next);}
                    bool hit=name is not("miss" or "block"),proc=hit&&name is not("outer" or "armor-outer" or "removed");int packets=name=="independent-casts"?2:1;
                    if(name=="removed"){p.AddToBackpack(bow);p.AddToBackpack(gloves);ClassGear.Refresh(p);}
                    var damage=selected.Damage.Roll(t,_=>0,out _);int direct=NamedElements.To256(SkillCombat.MitigatePrepared(p,t,damage with{Poison=0}).Total),rate=hit&&name!="immune"?114:0;
                    if(proc){expected.Enqueue((t,before));if(name=="independent-casts"){expected.Enqueue((t,before-direct-rate));}if(second!=null){expected.Enqueue((second,SkillHealth.Balance256(second)));}}
                    Drain(p);int count=proc?(second!=null?2:packets):0;
                    Require(visits.Count==count&&visits.All(v=>v.Outcomes.Single()is{CastAttempts:1,CastResults:1}),"Fan central/outer/provider/delay proc partition: actual="+visits.Count+" expected="+count);
                    Require(before-SkillHealth.Balance256(t)==(hit?(direct+rate)*packets:0)&&NativeMonsterReturn.Session(t).Packets==(hit?packets:0)&&NecromancerEffects.ManaBalance256(p)==mana-cost*packets,"Fan HP/delay/return/payment");Require(p.Backpack.FindItemByType(bow is Crossbow?typeof(Bolt):typeof(Arrow)).Amount==quantity-packets,"Fan quantity paid per projectile/impact");
                    Require(TimedEffects.Slow(t,TimedEffects.Now)==(hit&&name is not("outer" or "armor-outer")?20:0)&&TimedEffects.CanRegenerateHits(t)==(name=="miss"),"Fan hit-event suppression or accuracy PMH");
                    if(name=="armor-outer"){Require(TimedEffects.DefenseLoss(t)==1,"Outer arrow discarded or reread captured armor");}if(second!=null){Require(second.HitsMax*256-SkillHealth.Balance256(second)==direct+114,"Piercing fan missed second recipient");}
                    ulong seed=world.Seed;var actor=profile.Owners.SingleOrDefault();ulong actorSeed=actor?.Seed??0;if(count>0){if(actor==null){actorSeed=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref seed)));}for(int i=0;i<count;i++){SourceRandom.Roll(ref actorSeed);SourceRandom.Roll(ref seed);}Require(Own(p,bow).Owners.Single().Seed==actorSeed,"Fan actor proc draws");}else if(actor!=null){Require(Own(p,bow).Owners.Single().Seed==actorSeed,"Suppressed fan advanced actor");}Require(NpcWorldAllocation.Capture().Seed==seed,"Fan child/world proc draws");
                }
                catch(Exception e){throw new InvalidOperationException("Multiple Shot contact "+name,e);}
                finally{belt?.Delete();Clean(p,t,second);if(bow.Parent!=p){Require(p.EquipItem(bow),"Restore fan bow");}if(gloves.Parent!=p){Require(p.EquipItem(gloves),"Restore fan gloves");}ClassGear.Refresh(p);AmazonBow.LaunchRandom=new Maximum();SourceBlocking.BlockRoll=n=>n-1;}
            }
        }
        finally{PlayerItemProcWorld.Observed=old;}
    }
    private sealed class Minimum:Random{public override int Next(int n)=>0;}
    private sealed class Maximum:Random{public override int Next(int n)=>n-1;}
    private static void Check(bool reload)
    {
        using var rolls=new MagicArrowContactChecks.Rolls();PlayerMobile p;BaseWeapon bow;Item gloves,helm;int generation=0;
        if(!reload){p=Owner(2700,false,out bow,out gloves,out helm);Scenarios(p,bow,gloves);}
        else
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);bow=(BaseWeapon)World.FindItem((Serial)s.Bow);gloves=World.FindItem((Serial)s.Gloves);helm=World.FindItem((Serial)s.Helm);var target=(BaseCreature)World.FindMobile((Serial)s.Target);var ammo=World.FindItem((Serial)s.Ammo);
            Require(generation<=2&&bow.Parent==p&&gloves.Parent==p&&helm.Parent==p&&Json(new[]{ClassGear.State(bow),ClassGear.State(gloves),ClassGear.State(helm)})==s.Gear&&Json(Own(p,bow))==s.Profile&&Json(Flights(p).Single())==Json(s.Flight)&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(target)==s.TargetLife&&NecromancerEffects.ManaBalance256(p)==s.Mana&&ammo.Amount==s.Quantity,"Saved fan original/packet/proc/wound/resource state");
            p.MoveToWorld(s.Point,Map.Trammel);target.Frozen=target.CantWalk=true;target.AIObject.AITimer.Stop();target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);foreach(var r in Enum.GetValues<ResistanceType>()){target.SetResistance(r,100);}target.SetResistance(ResistanceType.Poison,0);
            var observer=PlayerItemProcWorld.Observed;int count=0,before=SkillHealth.Balance256(target);PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&SkillHealth.Balance256(target)==before&&v.Outcomes.Single()is{CastAttempts:1,CastResults:1},"Restored central arrow proc phase");count++;}};
            try{using(PlayerItemProcWorld.Candidate(p)){Hold(p,s.Flight with{NextUtc=TimedEffects.Now});Drain(p);}Require(count==1&&before-SkillHealth.Balance256(target)==114&&NecromancerEffects.ManaBalance256(p)==s.Mana&&ammo.Amount==s.Quantity,"Restored central arrow damage/proc/repayment");}finally{PlayerItemProcWorld.Observed=observer;Clean(p,target);}
        }
        var other=Owner(3100,true,out _,out _,out _);var victim=MagicArrowContactChecks.Target(other);Ready(other);int quantity=Ammo(other).Amount;using(PlayerItemProcWorld.Candidate(other)){var f=Launch(other,victim).First();Hold(other,f);Drain(other);}Require(victim.HitsMax*256-SkillHealth.Balance256(victim)==114&&Ammo(other).Amount==quantity-1,"Crossbow fan damage/quantity");Clean(other,victim);other.Delete();
        Ready(p);var t=MagicArrowContactChecks.Target(p);var selected=Launch(p,t).Where(x=>(x.Flags&0x10000)==0).OrderBy(x=>Math.Abs(x.Vy)).First();Hold(p,selected);WeaponMissiles.Tick(selected.NextUtc,onlyOwner:p.Serial.Value);selected=Flights(p).Single()with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};Hold(p,selected);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);NecromancerEffects.SetMana256(p,17);var point=p.Location;var savedAmmo=Ammo(p);p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,t.Serial.Value,bow.Serial.Value,gloves.Serial.Value,helm.Serial.Value,savedAmmo.Serial.Value,point,Json(new[]{ClassGear.State(bow),ClassGear.State(gloves),ClassGear.State(helm)}),Json(Own(p,bow)),selected,SkillHealth.Balance256(p),SkillHealth.Balance256(t),NecromancerEffects.ManaBalance256(p),savedAmmo.Amount,generation)));
    }
    private static void Input(NetState n,string label)
    {
        var rolls=new MagicArrowContactChecks.Rolls();var p=Owner(3300,false,out _,out _,out _);var t=MagicArrowContactChecks.Target(p);Ready(p);var account=FixtureNetwork.Authenticate(n,p,"multiple-shot-contact-");var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.MultipleShot).Mana256,quantity=Ammo(p).Amount;long deadline=Core.TickCount+6000;bool began=false,sawFlight=false;
        void Close(){Clean(p,t);PlayerItemProcWorld.Observed=old;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try{Require(Core.TickCount<deadline,"Multiple Shot contact input timeout");var session=ClassCombat.For(p);if(!began&&session.PendingAbility==CombatAbility.MultipleShot){began=true;Require(visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity,"Wire fan start/ammo/proc phase");Log.Information("{Label}: windup",label);}if(Flights(p).Length>0){sawFlight=true;}if(!sawFlight||Flights(p).Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}Require(began&&visits.Count==1&&visits[0].Phase=="domissiledamage"&&visits[0].Packet.Poison256==114&&visits[0].Outcomes.Single()is{CastAttempts:1,CastResults:1}&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity-1,"Wire fan impact/delay/proc/resources");Close();Log.Information("{Label}-result: passed",label);}catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.MultipleShot,(int)DiabloClass.Amazon,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
