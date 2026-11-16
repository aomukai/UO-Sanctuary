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

public static class LightningFuryContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(LightningFuryContactChecks));
    private const CombatAbility Ability=CombatAbility.LightningFury;
    private sealed class Minimum:Random{public override int Next(int n)=>0;}
    private sealed class NoWeaponEffects:Random{public override int Next(int n)=>throw new InvalidOperationException("Source0 Fury bolt requested weapon effects");}
    private sealed record Saved(uint Owner,uint Primary,uint Secondary,uint Weapon,uint[] Items,Point3D Point,string Gear,string Profile,WeaponFlight[] Flights,int Life,int PrimaryLife,int SecondaryLife,int Mana,int Quantity,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"lightning-fury-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static WeaponFlight[] Flights(PlayerMobile p)=>MagicArrowContactChecks.Flights(p);
    private static WeaponFlight[] Parents(PlayerMobile p)=>Flights(p).Where(x=>!AmazonLightningFury.IsChild(x)).ToArray();
    private static WeaponFlight[] Children(PlayerMobile p)=>Flights(p).Where(AmazonLightningFury.IsChild).ToArray();
    private static PlayerProcSave Own(PlayerMobile p,Item w)=>MagicArrowContactChecks.Own(p,w);
    private static DateTime Pmh(Mobile t)=>TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value)?.HealBlockedUntilUtc??default;
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"lightning-fury-contact-reload-check":"lightning-fury-contact-check";ConsoleInputHandler.RegisterCommand(name,"Fury parent weapon versus source0 bolt phases and saved originals",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"right","quick"})
        {string label="lightning-fury-contact-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated Fury burst,weapon proc and independent bolts",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(int row,out BaseWeapon w,out Item[] gear)
    {var p=HellslayerChecks.Owner(DiabloClass.Amazon,row);ClassSkillChecks.Train(p,SkillMechanics.For(Ability).TalentId);w=(BaseWeapon)PlayerItemProcWorldChecks.Carrier(p,new SoulGlaive());gear=new[]{DruidSetupItemChecks.Named(p,"Venom Grip"),DruidSetupItemChecks.Named(p,"Blackhorn's Face")};ClassGear.Refresh(p);Require(AmazonJavelins.WeaponAllowed(p)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Fury javelin/bindings");return p;}
    private static BaseCreature Target(PlayerMobile p,int dx=3,bool lightning=false)
    {var t=new ExplosiveArrowContactChecks.AreaRecipient();t.SetHits(60000);t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(lightning?ResistanceType.Energy:ResistanceType.Poison,0);return t;}
    private static void Ready(PlayerMobile p)=>MagicArrowContactChecks.Ready(p);
    private static void Clean(PlayerMobile p,params BaseCreature[] targets)=>MagicArrowContactChecks.Clean(p,targets);
    private static void Keep(PlayerMobile p,params WeaponFlight[] flights)
    {var s=WeaponMissiles.Snapshot();WeaponMissiles.Restore(s with{Flights=s.Flights.Where(x=>x.Owner!=p.Serial.Value).Concat(flights).ToArray()});}
    private static WeaponFlight Launch(PlayerMobile p,BaseCreature t,BaseWeapon w)
    {
        int mana=NecromancerEffects.ManaBalance256(p),quantity=NamedAmmunition.Remaining(w),cost=SkillMechanics.Values(p,Ability).Mana256;
        Require(ClassCombat.Perform(p,Ability,t,t.Location,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&NamedAmmunition.Remaining(w)==quantity,"Fury start phase");ClassCombat.ReleaseCast(p);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Fury release");var f=Parents(p).Single();Require(BowMissileContact.Handles(f)&&!BowMissileContact.AccuracyPMH(f)&&f.Damage.LightningJavelinPercent==null&&f.Damage.Elements.PoisonMin==153&&NamedAmmunition.Remaining(w)==quantity-1&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Fury source128 parent or repeated release payment");return f;
    }
    // Isolate parent contact phases while retaining any independently saved bolt.
    private static int ParentContact(PlayerMobile p,long keep=0)
    {
        int born=0;
        for(int i=0;i<70&&Parents(p).Length>0;i++)
        {WeaponMissiles.Tick(Parents(p).Min(x=>x.NextUtc),onlyOwner:p.Serial.Value);var children=Children(p).Where(x=>x.Id!=keep).ToArray();Require(children.All(x=>x.Frame==0&&AmazonLightningFury.ValidChild(x.Damage)&&!BowMissileContact.Handles(x)),"Born Fury bolts inherited parent weapon data");born+=children.Length;Keep(p,Flights(p).Where(x=>!AmazonLightningFury.IsChild(x)||x.Id==keep).ToArray());}
        Require(Parents(p).Length==0,"Fury parent did not terminate");return born;
    }
    private static void ParentCases(PlayerMobile p,BaseWeapon w,Item[] gear)
    {
        using var candidate=PlayerItemProcWorld.Candidate(p);var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();var expected=new Queue<(BaseCreature Target,int Life)>();
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&v.Packet.HitFlags==32&&v.Packet.Poison256 is 0 or 153&&Children(p).Length>0&&expected.Count>0,"Fury burst/event/weapon flags");var e=expected.Dequeue();Require(SkillHealth.Balance256(e.Target)==e.Life&&Pmh(e.Target)==default,"Fury parent proc/HP/PMH order");visits.Add(v);}};
            foreach(string mode in new[]{"hit","block","immune","removed","physical","lethal","armor","pierce"})
            {
                Ready(p);var launchRandom=AmazonBow.LaunchRandom;var t=Target(p);var second=mode=="pierce"?Target(p,6):null;Item belt=null;if(mode=="immune"){t.SetResistance(ResistanceType.Poison,100);}if(mode=="physical"){t.SetResistance(ResistanceType.Physical,0);}if(mode=="lethal"){SkillHealth.SetBalance256(t,1);}if(mode=="pierce"){belt=DruidSetupItemChecks.Named(p,"Razortail");AmazonBow.LaunchRandom=new Minimum();}SourceBlocking.BlockRoll=n=>mode=="block"?0:n-1;int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),quantity=NamedAmmunition.Remaining(w),cost=SkillMechanics.Values(p,Ability).Mana256;visits.Clear();expected.Clear();NpcWorldAllocation.RequireStream();var world=NpcWorldAllocation.Capture();var profile=Own(p,w);
                try
                {
                    var f=Launch(p,t,w);if(mode=="armor"){f=f with{Damage=f.Damage with{ArmorDelta=-1}};Keep(p,f);}if(mode=="removed"){p.AddToBackpack(w);ClassGear.Refresh(p);}int direct=NamedElements.To256(SkillCombat.MitigatePrepared(p,t,f.Damage.Roll(t,_=>0)with{Poison=0}).Total);bool hit=mode!="block",proc=hit&&mode!="removed";if(proc){expected.Enqueue((t,before));if(second!=null){expected.Enqueue((second,SkillHealth.Balance256(second)));}}int born=ParentContact(p),count=proc?(second!=null?2:1):0;
                    Require(born==(second!=null?4:1)&&visits.Count==count&&expected.Count==0&&visits.All(v=>v.Outcomes.Single()is{CastAttempts:1,CastResults:1})&&before-SkillHealth.Balance256(t)==(hit?Math.Min(before,direct+(mode=="immune"?0:153)):0)&&NativeMonsterReturn.Session(t).Packets==(hit?1:0),"Fury parent HP/burst/block/provider: "+mode);Require(Pmh(t)==default&&NecromancerEffects.ManaBalance256(p)==mana-cost&&NamedAmmunition.Remaining(w)==quantity-1,"Fury parent PMH/resources");if(mode=="armor"){Require(TimedEffects.DefenseLoss(t)==1,"Fury captured armor");}if(second!=null){Require(second.HitsMax*256-SkillHealth.Balance256(second)==153,"Piercing Fury parent");}
                    ulong seed=world.Seed;var actor=profile.Owners.SingleOrDefault();ulong actorSeed=actor?.Seed??0;for(int i=0;i<count;i++){if(i==0&&actor==null){actorSeed=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref seed)));}SourceRandom.Roll(ref actorSeed);SourceRandom.Roll(ref seed);}if(count>0){Require(Own(p,w).Owners.Single().Seed==actorSeed,"Fury parent actor draws");}Require(NpcWorldAllocation.Capture().Seed==seed,"Fury parent world draws");
                }
                catch(Exception e){throw new InvalidOperationException("Fury parent "+mode,e);}
                finally{Clean(p,t,second);belt?.Delete();AmazonBow.LaunchRandom=launchRandom;if(w.Parent!=p){Require(p.EquipItem(w),"Restore parent weapon");}ClassGear.Refresh(p);SourceBlocking.BlockRoll=n=>n-1;}
            }
        }
        finally{PlayerItemProcWorld.Observed=old;}
    }
    private static WeaponFlight Birth(PlayerMobile p,BaseWeapon w,BaseCreature t)
    {Launch(p,t,w);for(int i=0;i<20&&Parents(p).Length>0;i++){WeaponMissiles.Tick(Parents(p).Min(x=>x.NextUtc),onlyOwner:p.Serial.Value);}Require(Parents(p).Length==0&&Children(p).Length==1,"Single-recipient birth");return Children(p).Single();}
    private static void ChildCases(PlayerMobile p,BaseWeapon w)
    {
        using var candidate=PlayerItemProcWorld.Candidate(p);
        foreach(string mode in new[]{"hit","immune","lethal","repeat","two","removed"})
        {
            Ready(p);var initial=Target(p);initial.SetResistance(ResistanceType.Poison,100);BaseCreature a=null,b=null;var effects=WeaponMissiles.ItemEffects;var old=PlayerItemProcWorld.Observed;
            try
            {
                var child=Birth(p,w,initial);initial.Internalize();a=Target(p,3,true);a.SetResistance(ResistanceType.Physical,0);if(mode=="immune"){a.SetResistance(ResistanceType.Energy,100);}if(mode=="lethal"){SkillHealth.SetBalance256(a,1);}if(mode=="two"){b=Target(p,5,true);b.SetResistance(ResistanceType.Physical,0);}if(mode=="removed"){p.AddToBackpack(w);ClassGear.Refresh(p);}Keep(p,child);int before=SkillHealth.Balance256(a),two=b==null?0:SkillHealth.Balance256(b),mana=NecromancerEffects.ManaBalance256(p),quantity=NamedAmmunition.Remaining(w),visits=0;var profile=Json(Own(p,w));var world=NpcWorldAllocation.Capture();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits++;}};WeaponMissiles.ItemEffects=new NoWeaponEffects();SourceBlocking.BlockRoll=_=>throw new InvalidOperationException("Source0 Fury bolt requested physical shield");MagicArrowContactChecks.Drain(p);
                Require(visits==0&&before-SkillHealth.Balance256(a)==(mode=="immune"?0:Math.Min(before,child.Damage.Elements.LightningMin))&&NativeMonsterReturn.Session(a).Packets==1&&Pmh(a)==default&&TimedEffects.Slow(a,TimedEffects.Now)==0&&TimedEffects.DefenseLoss(a)==0&&TimedEffects.Snapshot().All(x=>x.Owner!=a.Serial.Value||x.PoisonClock==null&&x.Cold==null),"Source0 child damage/status/events: "+mode);if(b!=null){Require(two-SkillHealth.Balance256(b)==child.Damage.Elements.LightningMin&&NativeMonsterReturn.Session(b).Packets==1,"Unlimited Fury child contacts");}Require(Json(Own(p,w))==profile&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(p)==mana&&NamedAmmunition.Remaining(w)==quantity,"Fury child RNG/resource replay");
            }
            catch(Exception e){throw new InvalidOperationException("Fury child "+mode,e);}
            finally{PlayerItemProcWorld.Observed=old;WeaponMissiles.ItemEffects=effects;SourceBlocking.BlockRoll=n=>n-1;Clean(p,initial,a,b);if(w.Parent!=p){Require(p.EquipItem(w),"Restore child weapon");}ClassGear.Refresh(p);}
        }
    }
    private static void Check(bool reload)
    {
        using var rolls=new MagicArrowContactChecks.Rolls();WeaponMissiles.HitRoll=()=>throw new InvalidOperationException("Fury attempted AR");PlayerMobile p;BaseWeapon w;Item[] gear;int generation=0;
        if(!reload){p=Owner(2800,out w,out gear);ParentCases(p,w,gear);ChildCases(p,w);}
        else
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);w=(BaseWeapon)World.FindItem((Serial)s.Weapon);gear=s.Items.Select(id=>World.FindItem((Serial)id)).ToArray();var t=(BaseCreature)World.FindMobile((Serial)s.Primary);var b=(BaseCreature)World.FindMobile((Serial)s.Secondary);
            Require(generation<=2&&w.Parent==p&&gear.All(x=>x.Parent==p)&&Json(new[]{(Item)w}.Concat(gear).Select(ClassGear.State))==s.Gear&&Json(Own(p,w))==s.Profile&&Json(Flights(p).OrderBy(x=>x.Id))==Json(s.Flights)&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(t)==s.PrimaryLife&&SkillHealth.Balance256(b)==s.SecondaryLife&&NecromancerEffects.ManaBalance256(p)==s.Mana&&NamedAmmunition.Remaining(w)==s.Quantity,"Saved Fury original parent/child/gear/proc/wound/resources");p.MoveToWorld(s.Point,Map.Trammel);t.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);b.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);foreach(var actor in new[]{t,b}){actor.CantWalk=actor.Frozen=true;actor.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){actor.SetResistance(r,100);}}t.SetResistance(ResistanceType.Poison,0);b.SetResistance(ResistanceType.Energy,0);var parent=s.Flights.Single(x=>!AmazonLightningFury.IsChild(x));var child=s.Flights.Single(AmazonLightningFury.IsChild);int before=SkillHealth.Balance256(t),secondary=SkillHealth.Balance256(b),count=0;var old=PlayerItemProcWorld.Observed;var effects=WeaponMissiles.ItemEffects;PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&SkillHealth.Balance256(t)==before,"Restored parent phase");count++;}};
            try
            {
                using var candidate=PlayerItemProcWorld.Candidate(p);Keep(p,parent with{NextUtc=TimedEffects.Now},child);int born=ParentContact(p,child.Id);Require(born==2&&count==1&&before-SkillHealth.Balance256(t)==153&&SkillHealth.Balance256(b)==secondary&&Children(p).Single()==child,"Restored parent emission or independent child changed");var profile=Json(Own(p,w));var world=NpcWorldAllocation.Capture();WeaponMissiles.ItemEffects=new NoWeaponEffects();SourceBlocking.BlockRoll=_=>throw new InvalidOperationException("Restored source0 child requested shield");Keep(p,child with{NextUtc=TimedEffects.Now});MagicArrowContactChecks.Drain(p);Require(count==1&&secondary-SkillHealth.Balance256(b)==child.Damage.Elements.LightningMin&&NativeMonsterReturn.Session(b).Packets==1&&Json(Own(p,w))==profile&&NpcWorldAllocation.Capture()==world&&Pmh(b)==default&&TimedEffects.Slow(b,TimedEffects.Now)==0&&NecromancerEffects.ManaBalance256(p)==s.Mana&&NamedAmmunition.Remaining(w)==s.Quantity,"Restored child damage/event/RNG/resource replay");
            }
            finally{PlayerItemProcWorld.Observed=old;WeaponMissiles.ItemEffects=effects;SourceBlocking.BlockRoll=n=>n-1;Clean(p,t,b);}
        }
        Ready(p);var primary=Target(p);primary.SetResistance(ResistanceType.Poison,100);WeaponFlight heldChild;using(PlayerItemProcWorld.Candidate(p)){heldChild=Birth(p,w,primary);}WeaponMissiles.Tick(heldChild.NextUtc,onlyOwner:p.Serial.Value);heldChild=Children(p).Single();var until=TimedEffects.Now.Add(FixtureSpace.Retention);heldChild=heldChild with{NextUtc=until};Keep(p,heldChild);primary.SetResistance(ResistanceType.Poison,0);var secondaryTarget=Target(p,6,true);Ready(p);var heldParent=Launch(p,primary,w);WeaponMissiles.Tick(heldParent.NextUtc,onlyOwner:p.Serial.Value);heldParent=Parents(p).Single()with{NextUtc=until};Keep(p,heldParent,heldChild);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);NecromancerEffects.SetMana256(p,17);var point=p.Location;p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,primary.Serial.Value,secondaryTarget.Serial.Value,w.Serial.Value,gear.Select(x=>x.Serial.Value).ToArray(),point,Json(new[]{(Item)w}.Concat(gear).Select(ClassGear.State)),Json(Own(p,w)),Flights(p).OrderBy(x=>x.Id).ToArray(),SkillHealth.Balance256(p),SkillHealth.Balance256(primary),SkillHealth.Balance256(secondaryTarget),NecromancerEffects.ManaBalance256(p),NamedAmmunition.Remaining(w),generation)));
    }
    private static void Input(NetState n,string label)
    {
        var rolls=new MagicArrowContactChecks.Rolls();WeaponMissiles.HitRoll=()=>throw new InvalidOperationException("Wire Fury attempted AR");var p=Owner(3400,out var w,out _);var t=Target(p);Ready(p);var account=FixtureNetwork.Authenticate(n,p,"lightning-fury-contact-");var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256,quantity=NamedAmmunition.Remaining(w);long deadline=Core.TickCount+6000;bool began=false,sawFlight=false,sawChild=false;
        void Close(){Clean(p,t);PlayerItemProcWorld.Observed=old;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try{Require(Core.TickCount<deadline,"Fury input timeout");var session=ClassCombat.For(p);if(!began&&session.PendingAbility==Ability){began=true;Require(visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana-cost&&NamedAmmunition.Remaining(w)==quantity,"Wire Fury start phase");Log.Information("{Label}: windup",label);}if(Flights(p).Length>0){sawFlight=true;}if(Children(p).Length>0){sawChild=true;Require(Children(p).All(x=>AmazonLightningFury.ValidChild(x.Damage)&&!BowMissileContact.Handles(x)),"Wire Fury child weapon inheritance");}if(!sawFlight||Flights(p).Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}Require(began&&sawChild&&visits.Count==1&&visits[0].Phase=="domissiledamage"&&visits[0].Packet.Poison256==153&&visits[0].Outcomes.Single()is{CastAttempts:1,CastResults:1}&&Pmh(t)==default&&NecromancerEffects.ManaBalance256(p)==mana-cost&&NamedAmmunition.Remaining(w)==quantity-1,"Wire Fury parent/child/proc/resource split");Close();Log.Information("{Label}-result: passed",label);}catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Amazon,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
