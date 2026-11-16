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

public static class ImmolationContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ImmolationContactChecks));
    private const CombatAbility Ability=CombatAbility.ImmolationArrow;
    private sealed record Saved(uint Owner,uint Target,uint Bow,uint Gloves,uint Helm,uint Ammo,Point3D Point,string Gear,string Profile,WeaponFlight Flight,FirePatch Field,FireCooldown Cooldown,int Life,int TargetLife,int Mana,int Quantity,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"immolation-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static WeaponFlight[] Flights(PlayerMobile p)=>MagicArrowContactChecks.Flights(p);
    private static FirePatch[] Fields(PlayerMobile p)=>FireGround.Snapshot().Patches.Where(x=>x.Owner==p.Serial.Value&&x.Ability==Ability).ToArray();
    private static PlayerProcSave Own(PlayerMobile p,Item bow)=>MagicArrowContactChecks.Own(p,bow);
    private static Item Ammo(PlayerMobile p)=>BowAmmunition.Stack(p,CombatTiming.Equipped(p));
    private static DateTime Pmh(Mobile t)=>TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value)?.HealBlockedUntilUtc??default;
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"immolation-contact-reload-check":"immolation-contact-check";ConsoleInputHandler.RegisterCommand(name,"Immolation ordered area/parent/periodic field and saved originals",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"right","quick"})
        {string label="immolation-contact-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated Immolation three-stage delivery",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(int row,bool crossbow,out BaseWeapon bow,out Item gloves,out Item helm)
    {var p=MagicArrowContactChecks.Owner(row,crossbow,out bow,out gloves,out helm);ClassSkillChecks.Train(p,SkillMechanics.For(Ability).TalentId);p.Backpack.FindItemByType<Arrow>().Amount=100;p.Backpack.FindItemByType<Bolt>().Amount=100;Require(SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Immolation bindings");return p;}
    private static BaseCreature Satellite(PlayerMobile p,int y=1,bool immune=false)
    {var t=new ExplosiveArrowContactChecks.AreaRecipient();t.SetHits(60000);t.MoveToWorld(new(p.X+3,p.Y+y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Fire,immune?100:0);return t;}
    private static void Ready(PlayerMobile p)
    {MagicArrowContactChecks.Ready(p);var s=FireGround.Snapshot();FireGround.Restore(s with{Cooldowns=s.Cooldowns.Where(x=>x.Owner!=p.Serial.Value||x.Ability!=Ability).ToArray()});}
    private static void Clean(PlayerMobile p,params BaseCreature[] targets)
    {FireGround.Cancel(p);MagicArrowContactChecks.Clean(p,targets);}
    private static void OnlyField(PlayerMobile p,FirePatch field)
    {var s=FireGround.Snapshot();FireGround.Restore(s with{Patches=s.Patches.Where(x=>x.Owner!=p.Serial.Value).Append(field).ToArray()});}
    private static WeaponFlight Launch(PlayerMobile p,BaseCreature t)
    {
        int mana=NecromancerEffects.ManaBalance256(p),quantity=Ammo(p).Amount,cost=SkillMechanics.Values(p,Ability).Mana256;
        Require(ClassCombat.Perform(p,Ability,t,t.Location,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity,"Immolation start payment");ClassCombat.ReleaseCast(p);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Immolation release");var f=Flights(p).Single();
        Require(BowMissileContact.Handles(f)&&f.Damage.Conversion==null&&!f.Damage.FreezeColdLength&&f.Damage.Elements.FireMin>0&&f.Damage.Elements.PoisonMin==153&&Ammo(p).Amount==quantity-1&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Immolation captured parent/ammo");return f;
    }
    private static void Contact(PlayerMobile p)
    {for(int i=0;i<100&&Flights(p).Length>0;i++){WeaponMissiles.Tick(Flights(p).Min(x=>x.NextUtc),onlyOwner:p.Serial.Value);}Require(Flights(p).Length==0&&Fields(p).Length>=29,"Immolation parent/fields missing");}
    private static void Scenarios(PlayerMobile p,BaseWeapon bow,Item gloves)
    {
        using var candidate=PlayerItemProcWorld.Candidate(p);var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();BaseCreature primary=null,area=null;int before=0;
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&v.Packet.HitFlags==32&&v.Packet.ColdFrames==0&&SkillHealth.Balance256(primary)==before&&NativeMonsterReturn.Session(area).Packets==1&&Fields(p).Length==29,"Immolation area before pre-HP weapon event");visits.Add(v);}};
            foreach(string name in new[]{"hit","miss","block","removed","parent-immune","physical","area-immune","area-lethal","parent-lethal","area-primary-lethal","armor"})
            {
                Ready(p);var t=MagicArrowContactChecks.Target(p);primary=t;var sat=Satellite(p,immune:name=="area-immune");area=sat;var outside=Satellite(p,3);if(name=="parent-immune"){t.SetResistance(ResistanceType.Poison,100);}if(name=="physical"){t.SetResistance(ResistanceType.Physical,0);}if(name is "parent-lethal" or "area-primary-lethal"){SkillHealth.SetBalance256(t,1);}if(name=="area-primary-lethal"){t.SetResistance(ResistanceType.Fire,0);}if(name=="area-lethal"){SkillHealth.SetBalance256(sat,1);}before=SkillHealth.Balance256(t);int satBefore=SkillHealth.Balance256(sat),outsideBefore=SkillHealth.Balance256(outside);visits.Clear();WeaponMissiles.HitRoll=()=>name=="miss"?1:0;SourceBlocking.BlockRoll=n=>name=="block"?0:n-1;
                try
                {
                    var f=Launch(p,t);if(name=="armor"){f=f with{Damage=f.Damage with{ArmorDelta=-1}};MagicArrowContactChecks.Hold(p,f);}if(name=="removed"){p.AddToBackpack(bow);p.AddToBackpack(gloves);ClassGear.Refresh(p);}int direct=NamedElements.To256(SkillCombat.MitigatePrepared(p,t,f.Damage.Roll(t,_=>0)with{Poison=0}).Total);bool hit=name is not("miss" or "block" or "area-primary-lethal"),proc=hit&&name!="removed";int mana=NecromancerEffects.ManaBalance256(p),quantity=p.Backpack.FindItemByType(bow is Crossbow?typeof(Bolt):typeof(Arrow)).Amount;NpcWorldAllocation.RequireStream();var world=NpcWorldAllocation.Capture();var profile=Own(p,bow);
                    Contact(p);int loss=name=="area-primary-lethal"?before:hit?Math.Min(before,direct+(name=="parent-immune"?0:153)):0;int fire=name=="area-immune"?0:Math.Min(satBefore,f.Damage.Elements.FireMin);
                    Require(visits.Count==(proc?1:0)&&visits.All(v=>v.Outcomes.Single()is{CastAttempts:1,CastResults:1})&&before-SkillHealth.Balance256(t)==loss&&satBefore-SkillHealth.Balance256(sat)==fire&&SkillHealth.Balance256(outside)==outsideBefore,"Immolation parent/area damage,accuracy,block or proc gate: "+name);
                    Require(NativeMonsterReturn.Session(t).Packets==(name=="area-primary-lethal"?1:1+(hit?1:0))&&NativeMonsterReturn.Session(sat).Packets==1&&NativeMonsterReturn.Session(outside).Packets==0&&Pmh(sat)==default&&TimedEffects.Slow(sat,TimedEffects.Now)==0&&TimedEffects.DefenseLoss(sat)==0,"Immolation elemental packet inherited weapon effect");
                    if(t.Alive){Require((Pmh(t)>TimedEffects.Now)==(name!="miss"),"Immolation accuracy PMH");}if(name=="armor"){Require(TimedEffects.DefenseLoss(t)==1,"Immolation captured armor");}if(name=="area-lethal"){Require(sat.Corpse is Corpse c&&!CorpseSkillRules.Shattered(c),"Fire area corpse");}
                    ulong seed=world.Seed;var actor=profile.Owners.SingleOrDefault();ulong actorSeed=actor?.Seed??0;if(proc){if(actor==null){actorSeed=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref seed)));}SourceRandom.Roll(ref actorSeed);SourceRandom.Roll(ref seed);Require(Own(p,bow).Owners.Single().Seed==actorSeed,"Immolation proc actor draws");}Require(NpcWorldAllocation.Capture().Seed==seed&&NecromancerEffects.ManaBalance256(p)==mana&&p.Backpack.FindItemByType(bow is Crossbow?typeof(Bolt):typeof(Arrow)).Amount==quantity,"Immolation elemental stages allocated proc or paid again");
                }
                catch(Exception e){throw new InvalidOperationException("Immolation "+name,e);}
                finally{Clean(p,t,sat,outside);if(bow.Parent!=p){Require(p.EquipItem(bow),"Restore bow");}if(gloves.Parent!=p){Require(p.EquipItem(gloves),"Restore gloves");}ClassGear.Refresh(p);SourceBlocking.BlockRoll=n=>n-1;}
            }
        }
        finally{PlayerItemProcWorld.Observed=old;}
    }
    private static void FieldCases(PlayerMobile p,BaseWeapon bow)
    {
        foreach(string mode in new[]{"normal","soft","immune","lethal"})
        {
            Ready(p);var t=MagicArrowContactChecks.Target(p);var sat=Satellite(p);try
            {
                using var candidate=PlayerItemProcWorld.Candidate(p);Launch(p,t);Contact(p);var field=Fields(p).Single(x=>x.X==sat.X&&x.Y==sat.Y);OnlyField(p,field);if(mode=="immune"){sat.SetResistance(ResistanceType.Fire,100);}if(mode=="lethal"){SkillHealth.SetBalance256(sat,1);}int hp=SkillHealth.Balance256(sat),packets=NativeMonsterReturn.Session(sat).Packets,notifications=NativeMonsterReturn.Session(sat).RecoveryNotifications,directEvents=0,visits=0,draws=0;var state=Json(Own(p,bow));var world=NpcWorldAllocation.Capture();var old=PlayerItemProcWorld.Observed;var soft=ImmolationArrows.SoftHitRoll;var observer=new Action<SkillDamageContact>(x=>{if(x.Source==p){directEvents++;}});SkillDamageEvents.BeforeDirectDamage+=observer;PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits++;}};ImmolationArrows.SoftHitRoll=n=>{Require(n==128,"Field draw domain");draws++;return mode=="soft"?0:127;};
                try{FireGround.Tick(field.NextUtc.AddTicks(-1));Require(SkillHealth.Balance256(sat)==hp&&draws==0,"Field early hit");FireGround.Tick(field.NextUtc);FireGround.Tick(field.NextUtc);Require(hp-SkillHealth.Balance256(sat)==(mode=="immune"?0:Math.Min(hp,field.Min256))&&NativeMonsterReturn.Session(sat).Packets==packets+1&&draws==1&&directEvents==0&&visits==0&&Json(Own(p,bow))==state&&NpcWorldAllocation.Capture()==world&&Pmh(sat)==default&&CreatureHitRecovery.Remaining(sat)==0,"Periodic field damage/event/return/SOFTHIT/no-replay: "+mode);Require(NativeMonsterReturn.Session(sat).RecoveryNotifications==notifications+(mode=="soft"?1:0),"SOFTHIT versus ordinary field notification");}
                finally{SkillDamageEvents.BeforeDirectDamage-=observer;PlayerItemProcWorld.Observed=old;ImmolationArrows.SoftHitRoll=soft;}
            }
            finally{Clean(p,t,sat);}
        }
    }
    private static void Check(bool reload)
    {
        using var rolls=new MagicArrowContactChecks.Rolls();var oldSoft=ImmolationArrows.SoftHitRoll;ImmolationArrows.SoftHitRoll=_=>127;
        try
        {
            PlayerMobile p;BaseWeapon bow;Item gloves,helm;int generation=0;
            if(!reload){p=Owner(2800,false,out bow,out gloves,out helm);Scenarios(p,bow,gloves);FieldCases(p,bow);}
            else
            {
                var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);bow=(BaseWeapon)World.FindItem((Serial)s.Bow);gloves=World.FindItem((Serial)s.Gloves);helm=World.FindItem((Serial)s.Helm);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var ammo=World.FindItem((Serial)s.Ammo);
                Require(generation<=2&&bow.Parent==p&&gloves.Parent==p&&helm.Parent==p&&Json(new[]{ClassGear.State(bow),ClassGear.State(gloves),ClassGear.State(helm)})==s.Gear&&Json(Own(p,bow))==s.Profile&&Json(Flights(p).Single())==Json(s.Flight)&&Fields(p).Single()==s.Field&&FireGround.Snapshot().Cooldowns.Contains(s.Cooldown)&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(t)==s.TargetLife&&NecromancerEffects.ManaBalance256(p)==s.Mana&&ammo.Amount==s.Quantity,$"Saved Immolation original/parent/field/cooldown/proc/wound/resources life={SkillHealth.Balance256(p)}/{s.Life} target={SkillHealth.Balance256(t)}/{s.TargetLife} flight={Json(Flights(p).Single())==Json(s.Flight)} field={Fields(p).Single()==s.Field} cooldown={FireGround.Snapshot().Cooldowns.Contains(s.Cooldown)} gear={Json(new[]{ClassGear.State(bow),ClassGear.State(gloves),ClassGear.State(helm)})==s.Gear} profile={Json(Own(p,bow))==s.Profile} mana={NecromancerEffects.ManaBalance256(p)}/{s.Mana} ammo={ammo.Amount}/{s.Quantity}");
                p.MoveToWorld(s.Point,Map.Trammel);t.Frozen=t.CantWalk=true;t.AIObject.AITimer.Stop();t.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Poison,0);var sat=Satellite(p);int before=SkillHealth.Balance256(t),satBefore=SkillHealth.Balance256(sat),count=0;var observer=PlayerItemProcWorld.Observed;PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){count++;}};
                try{using(PlayerItemProcWorld.Candidate(p)){MagicArrowContactChecks.Hold(p,s.Flight with{NextUtc=TimedEffects.Now});Contact(p);Require(count==1&&before-SkillHealth.Balance256(t)==153&&satBefore-SkillHealth.Balance256(sat)==s.Flight.Damage.Elements.FireMin&&NecromancerEffects.ManaBalance256(p)==s.Mana&&ammo.Amount==s.Quantity,"Restored Immolation parent/area proc or repayment");var at=TimedEffects.Now;var field=s.Field with{NextUtc=at,UntilUtc=at.AddMilliseconds(4000)};OnlyField(p,field);t.SetResistance(ResistanceType.Fire,0);before=SkillHealth.Balance256(t);var profile=Json(Own(p,bow));FireGround.Tick(at);Require(before-SkillHealth.Balance256(t)==field.Min256&&count==1&&Json(Own(p,bow))==profile&&NecromancerEffects.ManaBalance256(p)==s.Mana&&ammo.Amount==s.Quantity,"Restored independent source0 field or replay");}}
                finally{PlayerItemProcWorld.Observed=observer;Clean(p,t,sat);}
            }
            var other=Owner(3200,true,out _,out _,out _);var victim=MagicArrowContactChecks.Target(other);Ready(other);using(PlayerItemProcWorld.Candidate(other)){Launch(other,victim);Contact(other);}Require(victim.HitsMax*256-SkillHealth.Balance256(victim)==153,"Crossbow Immolation");Clean(other,victim);other.Delete();
            Ready(p);var target=MagicArrowContactChecks.Target(p);target.SetResistance(ResistanceType.Poison,100);Launch(p,target);using(PlayerItemProcWorld.Candidate(p)){Contact(p);}var retained=Fields(p).Single(x=>x.X==target.X&&x.Y==target.Y);target.SetResistance(ResistanceType.Poison,0);Ready(p);var f=Launch(p,target);WeaponMissiles.Tick(f.NextUtc,onlyOwner:p.Serial.Value);var until=TimedEffects.Now.Add(FixtureSpace.Retention);f=Flights(p).Single()with{NextUtc=until};MagicArrowContactChecks.Hold(p,f);retained=retained with{NextUtc=until,UntilUtc=until.AddMilliseconds(3960)};OnlyField(p,retained);var state=FireGround.Snapshot();var cd=state.Cooldowns.Single(x=>x.Owner==p.Serial.Value&&x.Ability==Ability)with{UntilUtc=until};FireGround.Restore(state with{Cooldowns=state.Cooldowns.Where(x=>x.Owner!=p.Serial.Value||x.Ability!=Ability).Append(cd).ToArray()});var ammoItem=Ammo(p);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);NecromancerEffects.SetMana256(p,17);var point=p.Location;p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,target.Serial.Value,bow.Serial.Value,gloves.Serial.Value,helm.Serial.Value,ammoItem.Serial.Value,point,Json(new[]{ClassGear.State(bow),ClassGear.State(gloves),ClassGear.State(helm)}),Json(Own(p,bow)),f,retained,cd,SkillHealth.Balance256(p),SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(p),ammoItem.Amount,generation)));
        }
        finally{ImmolationArrows.SoftHitRoll=oldSoft;}
    }
    private static void Input(NetState n,string label)
    {
        var rolls=new MagicArrowContactChecks.Rolls();var p=Owner(3400,false,out _,out _,out _);var t=MagicArrowContactChecks.Target(p);var sat=Satellite(p);Ready(p);var account=FixtureNetwork.Authenticate(n,p,"immolation-contact-");var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256,quantity=Ammo(p).Amount,satBefore=SkillHealth.Balance256(sat);long deadline=Core.TickCount+6000;bool began=false,sawFlight=false;
        void Close(){Clean(p,t,sat);PlayerItemProcWorld.Observed=old;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try{Require(Core.TickCount<deadline,"Immolation input timeout");var session=ClassCombat.For(p);if(!began&&session.PendingAbility==Ability){began=true;Require(visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity,"Wire Immolation start phase");Log.Information("{Label}: windup",label);}if(Flights(p).Length>0){sawFlight=true;}if(!sawFlight||Flights(p).Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}Require(began&&visits.Count==1&&visits[0].Phase=="domissiledamage"&&visits[0].Packet.Poison256==153&&visits[0].Packet.ColdFrames==0&&visits[0].Outcomes.Single()is{CastAttempts:1,CastResults:1}&&SkillHealth.Balance256(sat)<satBefore&&NativeMonsterReturn.Session(sat).Packets>=1&&Fields(p).Length==29&&Pmh(sat)==default&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity-1,"Wire Immolation area/parent/field/proc/resources");Close();Log.Information("{Label}-result: passed",label);}catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Amazon,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
