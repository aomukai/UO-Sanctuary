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

public static class LightningBoltContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(LightningBoltContactChecks));
    private const CombatAbility Ability=CombatAbility.LightningBolt;
    private sealed class Minimum:Random{public override int Next(int n)=>0;}
    private sealed record Saved(uint Owner,uint Target,uint Weapon,uint[] Items,Point3D Point,string Gear,string Profile,WeaponFlight Flight,int Life,int TargetLife,int Mana,int Quantity,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"lightning-bolt-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static WeaponFlight[] Flights(PlayerMobile p)=>MagicArrowContactChecks.Flights(p);
    private static PlayerProcSave Own(PlayerMobile p,Item w)=>MagicArrowContactChecks.Own(p,w);
    private static DateTime Pmh(Mobile t)=>TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value)?.HealBlockedUntilUtc??default;
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"lightning-bolt-contact-reload-check":"lightning-bolt-contact-check";ConsoleInputHandler.RegisterCommand(name,"Lightning Bolt reset,ordered item events and saved original",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"right","quick"})
        {string label="lightning-bolt-contact-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated pure-lightning javelin with ordered hit proc",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(int row,out BaseWeapon w,out Item[] gear)
    {
        var p=HellslayerChecks.Owner(DiabloClass.Amazon,row);ClassSkillChecks.Train(p,SkillMechanics.For(Ability).TalentId);w=(BaseWeapon)PlayerItemProcWorldChecks.Carrier(p,new SoulGlaive());gear=new[]{DruidSetupItemChecks.Named(p,"Venom Grip"),DruidSetupItemChecks.Named(p,"Blackhorn's Face"),DruidSetupItemChecks.Named(p,"The Eye of Etlich"),DruidSetupItemChecks.Named(p,"Manald Heal")};ClassGear.Refresh(p);
        Require(AmazonJavelins.WeaponAllowed(p)&&ClassGear.State(w).NamedItemId==null&&NamedAmmunition.Remaining(w)>10,"Raw javelin carrier");Require(SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Lightning Bolt bindings");return p;
    }
    private static BaseCreature Target(PlayerMobile p,int dx=3)
    {var t=new ExplosiveArrowContactChecks.AreaRecipient();t.SetHits(60000);t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Energy,0);return t;}
    private static void Ready(PlayerMobile p)=>MagicArrowContactChecks.Ready(p);
    private static void Clean(PlayerMobile p,params BaseCreature[] targets)=>MagicArrowContactChecks.Clean(p,targets);
    private static WeaponFlight Launch(PlayerMobile p,BaseCreature t,BaseWeapon w)
    {
        int mana=NecromancerEffects.ManaBalance256(p),quantity=NamedAmmunition.Remaining(w),cost=SkillMechanics.Values(p,Ability).Mana256;
        Require(ClassCombat.Perform(p,Ability,t,t.Location,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&NamedAmmunition.Remaining(w)==quantity,"Lightning Bolt start mana/quantity");ClassCombat.ReleaseCast(p);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Lightning Bolt release");var f=Flights(p).Single();
        Require(BowMissileContact.Handles(f)&&!BowMissileContact.AccuracyPMH(f)&&f.Damage.LightningJavelinPercent==100&&f.SourceSteps!=null&&f.Damage.Elements.PoisonMin==114&&f.Damage.Elements.ColdMin>0&&f.Damage.Elements.LifeDrain>0&&f.Damage.Elements.ManaDrain>0&&NamedAmmunition.Remaining(w)==quantity-1&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Captured source96 lanes or once release");return f;
    }
    private static GearDamage Pure(WeaponFlight f,BaseCreature t)
    {var d=f.Damage.Roll(t,_=>0,out int poison);Require(d.Physical==0&&d.Fire==0&&d.Cold==0&&d.Magic==0&&d.Poison==0&&d.ColdMs==0&&d.PoisonMs==0&&poison==0&&d.Lightning>0,"srvdmg12 reset");return d;}
    private static void Scenarios(PlayerMobile p,BaseWeapon w,Item[] gear)
    {
        using var candidate=PlayerItemProcWorld.Candidate(p);var prior=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();var expected=new Queue<(BaseCreature Target,int Life)>();
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&v.Packet.HitFlags==32&&v.Packet.Physical256==0&&v.Packet.Poison256==0&&v.Packet.Cold256==0&&v.Packet.ColdFrames==0&&v.Packet.FreezeFrames==0&&v.Packet.Fire256==0&&v.Packet.Magic256==0&&expected.Count>0,"Lightning Bolt retained reset lanes or fabricated attack event");var e=expected.Dequeue();Require(SkillHealth.Balance256(e.Target)==e.Life&&Pmh(e.Target)==default,"Lightning Bolt proc/HP/accuracy PMH order");visits.Add(v);}};
            foreach(string name in new[]{"hit","immune","resisted","removed","lethal","armor","pierce","item-freeze"})
            {
                Ready(p);var launchRandom=AmazonBow.LaunchRandom;var effects=WeaponMissiles.ItemEffects;var t=Target(p);var second=name=="pierce"?Target(p,6):null;Item extra=null;if(name=="immune"){t.SetResistance(ResistanceType.Energy,100);}if(name=="resisted"){t.SetResistance(ResistanceType.Energy,50);}if(name=="lethal"){SkillHealth.SetBalance256(t,1);}if(name=="pierce"){extra=DruidSetupItemChecks.Named(p,"Razortail");AmazonBow.LaunchRandom=new Minimum();}if(name=="item-freeze"){extra=DruidSetupItemChecks.Named(p,"Iceblink");t.SetResistance(ResistanceType.Cold,0);WeaponMissiles.ItemEffects=new Minimum();}
                int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),quantity=NamedAmmunition.Remaining(w),cost=SkillMechanics.Values(p,Ability).Mana256;SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-4096);int ownerLife=SkillHealth.Balance256(p);visits.Clear();expected.Clear();NpcWorldAllocation.RequireStream();var world=NpcWorldAllocation.Capture();var profile=Own(p,w);
                try
                {
                    var f=Launch(p,t,w);Require(visits.Count==0&&Json(Own(p,w))==Json(profile)&&NpcWorldAllocation.Capture()==world&&SkillHealth.Balance256(t)==before,"Lightning Bolt launch damage/proc/RNG");if(name=="armor"){f=f with{Damage=f.Damage with{ArmorDelta=-1}};MagicArrowContactChecks.Hold(p,f);}if(name=="removed"){p.AddToBackpack(w);ClassGear.Refresh(p);}var d=Pure(f,t);int damage=NamedElements.To256(SkillCombat.MitigatePrepared(p,t,d).Total);bool proc=name!="removed";if(proc){expected.Enqueue((t,before));if(second!=null){expected.Enqueue((second,SkillHealth.Balance256(second)));}}
                    MagicArrowContactChecks.Drain(p);int count=proc?(second!=null?2:1):0;Require(visits.Count==count&&expected.Count==0&&visits.All(v=>v.Outcomes.Single()is{CastAttempts:1,CastResults:1})&&before-SkillHealth.Balance256(t)==Math.Min(before,damage)&&NativeMonsterReturn.Session(t).Packets==1,"Lightning Bolt typed reset damage,immunity or item event");Require(Pmh(t)==default&&TimedEffects.Snapshot().All(x=>x.Owner!=t.Serial.Value||x.PoisonClock==null&&x.Cold==null)&&SkillHealth.Balance256(p)==ownerLife&&NecromancerEffects.ManaBalance256(p)==mana-cost&&NamedAmmunition.Remaining(w)==quantity-1,"Lightning Bolt retained poison/chill/drain/PMH or repaid resources");
                    if(name=="armor"){Require(TimedEffects.DefenseLoss(t)==1,"Captured javelin armor");}if(name=="item-freeze"){Require(NamedControl.FrozenRemaining(t)>0,"Missile reset incorrectly suppressed owner item-freeze event");}if(second!=null){int two=NamedElements.To256(SkillCombat.MitigatePrepared(p,second,Pure(f,second)).Total);Require(second.HitsMax*256-SkillHealth.Balance256(second)==two&&NativeMonsterReturn.Session(second).Packets==1&&Pmh(second)==default,"Piercing pure-lightning contact");}
                    ulong seed=world.Seed;var actor=profile.Owners.SingleOrDefault();ulong actorSeed=actor?.Seed??0;for(int i=0;i<count;i++){if(i==0&&actor==null){actorSeed=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref seed)));}SourceRandom.Roll(ref actorSeed);SourceRandom.Roll(ref seed);}if(count>0){Require(Own(p,w).Owners.Single().Seed==actorSeed,"Lightning Bolt actor draws");}else if(actor!=null){Require(Own(p,w).Owners.Single().Seed==actorSeed,"Removed provider advanced actor");}Require(NpcWorldAllocation.Capture().Seed==seed,"Lightning Bolt world draws");
                }
                catch(Exception e){throw new InvalidOperationException("Lightning Bolt "+name,e);}
                finally{Clean(p,t,second);extra?.Delete();AmazonBow.LaunchRandom=launchRandom;WeaponMissiles.ItemEffects=effects;if(w.Parent!=p){Require(p.EquipItem(w),"Restore javelin");}ClassGear.Refresh(p);}
            }
        }
        finally{PlayerItemProcWorld.Observed=prior;}
    }
    private static void Check(bool reload)
    {
        using var rolls=new MagicArrowContactChecks.Rolls();WeaponMissiles.HitRoll=()=>throw new InvalidOperationException("Lightning Bolt attempted AR");SourceBlocking.BlockRoll=_=>throw new InvalidOperationException("Pure lightning attempted physical shield block");PlayerMobile p;BaseWeapon w;Item[] gear;int generation=0;
        if(!reload){p=Owner(2800,out w,out gear);Scenarios(p,w,gear);}
        else
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);w=(BaseWeapon)World.FindItem((Serial)s.Weapon);gear=s.Items.Select(id=>World.FindItem((Serial)id)).ToArray();var t=(BaseCreature)World.FindMobile((Serial)s.Target);
            Require(generation<=2&&w.Parent==p&&gear.All(x=>x.Parent==p)&&Json(new[]{(Item)w}.Concat(gear).Select(ClassGear.State))==s.Gear&&Json(Own(p,w))==s.Profile&&Json(Flights(p).Single())==Json(s.Flight)&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(t)==s.TargetLife&&NecromancerEffects.ManaBalance256(p)==s.Mana&&NamedAmmunition.Remaining(w)==s.Quantity,"Saved Lightning Bolt original/packet/proc/wound/resources");p.MoveToWorld(s.Point,Map.Trammel);t.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);t.CantWalk=t.Frozen=true;t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Energy,0);int before=SkillHealth.Balance256(t),count=0,expected=NamedElements.To256(SkillCombat.MitigatePrepared(p,t,Pure(s.Flight,t)).Total);var old=PlayerItemProcWorld.Observed;PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&v.Packet.Poison256==0&&SkillHealth.Balance256(t)==before&&Pmh(t)==default,"Restored pure-lightning phase");count++;}};
            try{using(PlayerItemProcWorld.Candidate(p)){MagicArrowContactChecks.Hold(p,s.Flight with{NextUtc=TimedEffects.Now});MagicArrowContactChecks.Drain(p);}Require(count==1&&before-SkillHealth.Balance256(t)==expected&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&NamedAmmunition.Remaining(w)==s.Quantity&&Pmh(t)==default,"Restored lightning damage or resource/drain replay");}finally{PlayerItemProcWorld.Observed=old;Clean(p,t);}
        }
        Ready(p);var target=Target(p);var f=Launch(p,target,w);WeaponMissiles.Tick(f.NextUtc,onlyOwner:p.Serial.Value);f=Flights(p).Single()with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};MagicArrowContactChecks.Hold(p,f);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);NecromancerEffects.SetMana256(p,17);var point=p.Location;p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,target.Serial.Value,w.Serial.Value,gear.Select(x=>x.Serial.Value).ToArray(),point,Json(new[]{(Item)w}.Concat(gear).Select(ClassGear.State)),Json(Own(p,w)),f,SkillHealth.Balance256(p),SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(p),NamedAmmunition.Remaining(w),generation)));
    }
    private static void Input(NetState n,string label)
    {
        var rolls=new MagicArrowContactChecks.Rolls();WeaponMissiles.HitRoll=()=>throw new InvalidOperationException("Wire Lightning Bolt attempted AR");SourceBlocking.BlockRoll=_=>throw new InvalidOperationException("Wire pure lightning attempted shield block");var p=Owner(3400,out var w,out _);var t=Target(p);Ready(p);var account=FixtureNetwork.Authenticate(n,p,"lightning-bolt-contact-");var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256,quantity=NamedAmmunition.Remaining(w),hp=SkillHealth.Balance256(t);long deadline=Core.TickCount+6000;bool began=false,sawFlight=false;
        void Close(){Clean(p,t);PlayerItemProcWorld.Observed=old;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try{Require(Core.TickCount<deadline,"Lightning Bolt input timeout");var session=ClassCombat.For(p);if(!began&&session.PendingAbility==Ability){began=true;Require(visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana-cost&&NamedAmmunition.Remaining(w)==quantity,"Wire start payment/quantity phase");Log.Information("{Label}: windup",label);}if(Flights(p).Length>0){sawFlight=true;}if(!sawFlight||Flights(p).Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}Require(began&&visits.Count==1&&visits[0].Phase=="domissiledamage"&&visits[0].Packet.Poison256==0&&visits[0].Packet.Physical256==0&&visits[0].Packet.Cold256==0&&visits[0].Packet.Lightning256>0&&visits[0].Outcomes.Single()is{CastAttempts:1,CastResults:1}&&NativeMonsterReturn.Session(t).Packets==1&&SkillHealth.Balance256(t)<hp&&Pmh(t)==default&&NecromancerEffects.ManaBalance256(p)==mana-cost&&NamedAmmunition.Remaining(w)==quantity-1,"Wire pure-lightning hit/proc/resources");Close();Log.Information("{Label}-result: passed",label);}catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Amazon,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
