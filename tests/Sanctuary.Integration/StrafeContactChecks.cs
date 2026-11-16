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

public static class StrafeContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(StrafeContactChecks));
    private const CombatAbility Ability=CombatAbility.Strafe;
    private sealed class Minimum:Random{public override int Next(int n)=>0;}
    private sealed record Saved(uint Owner,uint Target,uint Bow,uint Gloves,uint Helm,uint Ammo,Point3D Point,string Gear,string Profile,WeaponFlight Flight,WeaponFlightLease Lease,int Life,int TargetLife,int Mana,int Quantity,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"strafe-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static WeaponFlight[] Flights(PlayerMobile p)=>MagicArrowContactChecks.Flights(p);
    private static PlayerProcSave Own(PlayerMobile p,Item bow)=>MagicArrowContactChecks.Own(p,bow);
    private static Item Ammo(PlayerMobile p)=>BowAmmunition.Stack(p,CombatTiming.Equipped(p));
    private static DateTime Pmh(Mobile t)=>TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value)?.HealBlockedUntilUtc??default;
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"strafe-contact-reload-check":"strafe-contact-check";ConsoleInputHandler.RegisterCommand(name,"Strafe ordered contacts,retained hit-delay and original proc-bearing flight saves",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"right","quick"})
        {string label="strafe-contact-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated Strafe series with ordered missile procs",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(int row,bool crossbow,out BaseWeapon bow,out Item gloves,out Item helm)
    {var p=MagicArrowContactChecks.Owner(row,crossbow,out bow,out gloves,out helm);ClassSkillChecks.Train(p,SkillMechanics.For(Ability).TalentId);p.Backpack.FindItemByType<Arrow>().Amount=100;p.Backpack.FindItemByType<Bolt>().Amount=100;Require(SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Strafe bindings");return p;}
    private static void Ready(PlayerMobile p){AmazonStrafe.Cancel(p);MagicArrowContactChecks.Ready(p);}
    private static void Clean(PlayerMobile p,params BaseCreature[] targets){AmazonStrafe.Cancel(p);MagicArrowContactChecks.Clean(p,targets);}
    private static void Hold(PlayerMobile p,WeaponFlight f,WeaponFlightLease lease=null)
    {var s=WeaponMissiles.Snapshot();WeaponMissiles.Restore(s with{Flights=s.Flights.Where(x=>x.Owner!=p.Serial.Value).Append(f).ToArray(),Leases=lease==null?s.Leases:s.Leases.Where(x=>x.Cast!=f.Cast).Append(lease).ToArray()});}
    private static WeaponFlight Emit(PlayerMobile p)
    {
        var s=ClassCombat.For(p);var ids=Flights(p).Select(x=>x.Id).ToHashSet();long at=s.Strafe.EffectAt;Require(!ClassCombat.ResolveAction(p,at-1)&&ClassCombat.ResolveAction(p,at)&&!ClassCombat.ResolveAction(p,at),"Strafe event/replay");return Flights(p).Single(x=>!ids.Contains(x.Id));
    }
    private static WeaponFlight Launch(PlayerMobile p,BaseCreature target)
    {
        int mana=NecromancerEffects.ManaBalance256(p),quantity=Ammo(p).Amount,cost=SkillMechanics.Values(p,Ability).Mana256;var ids=Flights(p).Select(x=>x.Id).ToHashSet();
        Require(ClassCombat.Perform(p,Ability,target,target.Location,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity-1&&Flights(p).All(x=>ids.Contains(x.Id)),"Strafe start quantity/payment versus emission");ClassCombat.ReleaseCast(p);var f=Emit(p);
        Require(BowMissileContact.Handles(f)&&f.NextDelay==4&&f.Damage.SkillAttackPercent==30&&f.Damage.Elements.PoisonMin==153&&f.Damage.Conversion==null&&Ammo(p).Amount==quantity-1&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Strafe source128/AR/event packet or repeated payment");return f;
    }
    private static void Drain(PlayerMobile p)=>MagicArrowContactChecks.Drain(p);
    private static void Scenarios(PlayerMobile p,BaseWeapon bow,Item gloves)
    {
        using var candidate=PlayerItemProcWorld.Candidate(p);var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();var expected=new Queue<(BaseCreature Target,int Life)>();
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&v.Packet.HitFlags==32&&expected.Count>0,"Strafe fabricated launch/attack/suppressed proc");var e=expected.Dequeue();Require(SkillHealth.Balance256(e.Target)==e.Life,"Strafe proc after HP");visits.Add(v);}};
            foreach(string name in new[]{"hit","miss","block","immune","removed","physical","lethal","armor","pierce","gap-delay","gap-expiry","independent-casts"})
            {
                Ready(p);var launchRandom=AmazonBow.LaunchRandom;var t=MagicArrowContactChecks.Target(p);var second=name=="pierce"?MagicArrowContactChecks.Target(p,6):null;Item belt=null;if(name=="immune"){t.SetResistance(ResistanceType.Poison,100);}if(name=="physical"){t.SetResistance(ResistanceType.Physical,0);}if(name=="lethal"){SkillHealth.SetBalance256(t,1);}WeaponMissiles.HitRoll=()=>name=="miss"?1:0;SourceBlocking.BlockRoll=n=>name=="block"?0:n-1;
                if(name=="pierce"){belt=DruidSetupItemChecks.Named(p,"Razortail");AmazonBow.LaunchRandom=new Minimum();}
                int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),quantity=Ammo(p).Amount,cost=SkillMechanics.Values(p,Ability).Mana256;visits.Clear();expected.Clear();NpcWorldAllocation.RequireStream();var world=NpcWorldAllocation.Capture();var profile=Own(p,bow);
                try
                {
                    var f=Launch(p,t);Require(visits.Count==0&&SkillHealth.Balance256(t)==before&&Json(Own(p,bow))==Json(profile)&&NpcWorldAllocation.Capture()==world,"Strafe launch damage/proc/RNG");if(name=="armor"){f=f with{Damage=f.Damage with{ArmorDelta=-1}};Hold(p,f);}if(name=="removed"){p.AddToBackpack(bow);p.AddToBackpack(gloves);ClassGear.Refresh(p);}
                    int direct=NamedElements.To256(SkillCombat.MitigatePrepared(p,t,f.Damage.Roll(t,_=>0)with{Poison=0}).Total),rate=name=="immune"?0:153;bool hit=name is not("miss" or "block"),proc=hit&&name!="removed";int packets=name is "gap-expiry" or "independent-casts"?2:1,catchup=0;
                    if(proc){expected.Enqueue((t,before));if(packets==2){expected.Enqueue((t,before-direct-rate));}if(second!=null){expected.Enqueue((second,SkillHealth.Balance256(second)));}}
                    if(name=="independent-casts"){AmazonStrafe.Cancel(p);ClassCombat.Stop(p);var session=ClassCombat.For(p);session.NextAction=session.RecoveryUntil=Core.TickCount;Launch(p,t);}
                    Drain(p);
                    if(name is "gap-delay" or "gap-expiry")
                    {
                        var poisonBefore=TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value);var lease=WeaponMissiles.Snapshot().Leases.Single(x=>x.Cast==f.Cast&&x.Target==t.Serial.Value);Require(lease.RetainWithoutFlight&&Flights(p).Length==0&&ClassCombat.For(p).Strafe is{Remaining:1},"Strafe proc cancelled the paid series or lost empty-gap lease");var next=Emit(p);Require(next.Cast==f.Cast,"Second Strafe arrow lost cast identity");if(name=="gap-expiry"){Hold(p,next with{NextUtc=lease.UntilUtc});}Drain(p);
                        if(name=="gap-expiry"){var poisonAfter=TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value);int elapsed=(int)Math.Min(poisonBefore.PoisonMs,(poisonAfter.UpdatedUtc-poisonBefore.UpdatedUtc).TotalMilliseconds);catchup=elapsed/40*poisonBefore.PoisonClock.Rate256;Require(catchup>0,"Delayed reapplication failed to settle existing poison");}
                    }
                    int count=proc?(second!=null?2:packets):0;Require(visits.Count==count&&expected.Count==0&&visits.All(v=>v.Outcomes.Single()is{CastAttempts:1,CastResults:1}),"Strafe proc partition: "+name);
                    Require(before-SkillHealth.Balance256(t)==(hit?Math.Min(before,(direct+rate)*packets+catchup):0)&&NativeMonsterReturn.Session(t).Packets==(hit?packets:0)&&NecromancerEffects.ManaBalance256(p)==mana-cost*(name=="independent-casts"?2:1)&&p.Backpack.FindItemByType(bow is Crossbow?typeof(Bolt):typeof(Arrow)).Amount==quantity-(name=="independent-casts"?2:1),$"Strafe HP/NHD/resources before={before} now={SkillHealth.Balance256(t)} direct={direct} rate={rate} catchup={catchup} expectedPackets={packets} native={NativeMonsterReturn.Session(t).Packets} mana={NecromancerEffects.ManaBalance256(p)}/{mana-cost*(name=="independent-casts"?2:1)} ammo={p.Backpack.FindItemByType(bow is Crossbow?typeof(Bolt):typeof(Arrow)).Amount}/{quantity-(name=="independent-casts"?2:1)}");if(t.Alive){Require((Pmh(t)>TimedEffects.Now)==(name!="miss"),"Strafe accuracy PMH");}if(name=="armor"){Require(TimedEffects.DefenseLoss(t)==1,"Captured Strafe armor");}if(second!=null){Require(visits.Count==2&&second.HitsMax*256-SkillHealth.Balance256(second)==153,"Piercing Strafe contact");}
                    ulong seed=world.Seed;var actor=profile.Owners.SingleOrDefault();ulong actorSeed=actor?.Seed??0;for(int i=0;i<count;i++){if(i==0&&actor==null){actorSeed=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref seed)));}SourceRandom.Roll(ref actorSeed);SourceRandom.Roll(ref seed);}if(count>0){Require(Own(p,bow).Owners.Single().Seed==actorSeed,"Strafe actor draws");}else if(actor!=null){Require(Own(p,bow).Owners.Single().Seed==actorSeed,"Rejected Strafe advanced actor");}Require(NpcWorldAllocation.Capture().Seed==seed,"Strafe world draws");
                }
                catch(Exception e){throw new InvalidOperationException("Strafe "+name,e);}
                finally{Clean(p,t,second);AmazonBow.LaunchRandom=launchRandom;belt?.Delete();if(bow.Parent!=p){Require(p.EquipItem(bow),"Restore bow");}if(gloves.Parent!=p){Require(p.EquipItem(gloves),"Restore gloves");}ClassGear.Refresh(p);SourceBlocking.BlockRoll=n=>n-1;}
            }
        }
        finally{PlayerItemProcWorld.Observed=old;}
    }
    private static void Check(bool reload)
    {
        using var rolls=new MagicArrowContactChecks.Rolls();PlayerMobile p;BaseWeapon bow;Item gloves,helm;int generation=0;
        if(!reload){p=Owner(2800,false,out bow,out gloves,out helm);Scenarios(p,bow,gloves);}
        else
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);bow=(BaseWeapon)World.FindItem((Serial)s.Bow);gloves=World.FindItem((Serial)s.Gloves);helm=World.FindItem((Serial)s.Helm);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var ammo=World.FindItem((Serial)s.Ammo);
            Require(generation<=2&&bow.Parent==p&&gloves.Parent==p&&helm.Parent==p&&Json(new[]{ClassGear.State(bow),ClassGear.State(gloves),ClassGear.State(helm)})==s.Gear&&Json(Own(p,bow))==s.Profile&&Json(Flights(p).Single())==Json(s.Flight)&&WeaponMissiles.Snapshot().Leases.Contains(s.Lease)&&ClassCombat.For(p).Strafe==null&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(t)==s.TargetLife&&NecromancerEffects.ManaBalance256(p)==s.Mana&&ammo.Amount==s.Quantity,"Saved Strafe original/flight/lease/proc/wound/resources");
            p.MoveToWorld(s.Point,Map.Trammel);t.Frozen=t.CantWalk=true;t.AIObject.AITimer.Stop();t.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Poison,0);var observer=PlayerItemProcWorld.Observed;int count=0,before=SkillHealth.Balance256(t);PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&SkillHealth.Balance256(t)==before,"Restored Strafe proc phase");count++;}};
            try
            {
                using var candidate=PlayerItemProcWorld.Candidate(p);var f=s.Flight with{NextUtc=TimedEffects.Now};var lease=s.Lease with{UntilUtc=f.NextUtc.AddMilliseconds(160)};t.MoveToWorld(new((int)Math.Floor(f.X+1),p.Y,p.Z),p.Map);Hold(p,f,lease);var profile=Json(Own(p,bow));var world=NpcWorldAllocation.Capture();
                for(int i=0;i<8&&Flights(p).Single().X<=t.X;i++){var live=Flights(p).Single();Require(live.NextUtc<lease.UntilUtc,"Saved gate control exceeded delay");WeaponMissiles.Tick(live.NextUtc,onlyOwner:p.Serial.Value);}
                Require(Flights(p).Single().X>t.X&&count==0&&SkillHealth.Balance256(t)==before&&Json(Own(p,bow))==profile&&NpcWorldAllocation.Capture()==world,"Saved Strafe gate allowed damage/proc/RNG");f=Flights(p).Single();t.MoveToWorld(new((int)Math.Ceiling(f.X+3),p.Y,p.Z),p.Map);Hold(p,f with{NextUtc=lease.UntilUtc});Drain(p);Require(count==1&&before-SkillHealth.Balance256(t)==153&&NecromancerEffects.ManaBalance256(p)==s.Mana&&ammo.Amount==s.Quantity,"Restored Strafe post-delay damage or repayment");
            }
            finally{PlayerItemProcWorld.Observed=observer;Clean(p,t);}
        }
        var other=Owner(3200,true,out _,out _,out _);var victim=MagicArrowContactChecks.Target(other);Ready(other);using(PlayerItemProcWorld.Candidate(other)){Launch(other,victim);Drain(other);}Require(victim.HitsMax*256-SkillHealth.Balance256(victim)==153,"Crossbow Strafe");Clean(other,victim);other.Delete();
        Ready(p);var target=MagicArrowContactChecks.Target(p);target.SetResistance(ResistanceType.Poison,100);var first=Launch(p,target);using(PlayerItemProcWorld.Candidate(p)){Drain(p);}var gate=WeaponMissiles.Snapshot().Leases.Single(x=>x.Cast==first.Cast&&x.Target==target.Serial.Value);target.SetResistance(ResistanceType.Poison,0);var f2=Emit(p);WeaponMissiles.Tick(f2.NextUtc,onlyOwner:p.Serial.Value);var until=TimedEffects.Now.Add(FixtureSpace.Retention);f2=Flights(p).Single()with{NextUtc=until};gate=gate with{UntilUtc=until.AddMilliseconds(160)};Hold(p,f2,gate);AmazonStrafe.Cancel(p);var ammoItem=Ammo(p);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);NecromancerEffects.SetMana256(p,17);var point=p.Location;p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,target.Serial.Value,bow.Serial.Value,gloves.Serial.Value,helm.Serial.Value,ammoItem.Serial.Value,point,Json(new[]{ClassGear.State(bow),ClassGear.State(gloves),ClassGear.State(helm)}),Json(Own(p,bow)),f2,gate,SkillHealth.Balance256(p),SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(p),ammoItem.Amount,generation)));
    }
    private static void Input(NetState n,string label)
    {
        var rolls=new MagicArrowContactChecks.Rolls();var p=Owner(3400,false,out _,out _,out _);var t=MagicArrowContactChecks.Target(p);Ready(p);var account=FixtureNetwork.Authenticate(n,p,"strafe-contact-");var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256,quantity=Ammo(p).Amount;long deadline=Core.TickCount+6000;bool began=false,sawFlight=false;
        void Close(){Clean(p,t);PlayerItemProcWorld.Observed=old;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try{Require(Core.TickCount<deadline,"Strafe input timeout");var session=ClassCombat.For(p);if(!began&&session.PendingAbility==Ability){began=true;Require(visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity-1,"Wire Strafe start quantity/payment");Log.Information("{Label}: windup",label);}if(Flights(p).Length>0){sawFlight=true;}if(!sawFlight||Flights(p).Length>0||session.Strafe!=null){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}Require(began&&visits.Count is >=1 and <=2&&visits.All(v=>v.Phase=="domissiledamage"&&v.Packet.HitFlags==32&&v.Packet.Poison256==153&&v.Outcomes.Single()is{CastAttempts:1,CastResults:1})&&NativeMonsterReturn.Session(t).Packets==visits.Count&&Pmh(t)>TimedEffects.Now&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity-1,"Wire Strafe series/delay/proc/resources");Close();Log.Information("{Label}-result: passed",label);}catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Amazon,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
