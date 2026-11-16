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

public static class ExplosiveArrowContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ExplosiveArrowContactChecks));
    private static readonly CombatAbility[] Abilities=[CombatAbility.ExplodingArrow,CombatAbility.FreezingArrow];
    // Native return wakes AI. Suppress this fixture's separate source regeneration,
    // without inventing PMH on an element-only area recipient.
    public sealed class AreaRecipient:GraveyardSkeleton
    {
        [Constructible] public AreaRecipient(){CantWalk=Frozen=true;NoKillAwards=true;}
        public AreaRecipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnThink(){}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);reader.ReadInt();}
    }
    private sealed record Saved(uint Owner,uint Target,uint Bow,uint Gloves,uint Helm,uint Ammo,Point3D Point,string Gear,string Profile,WeaponFlight Flight,int Life,int TargetLife,int Mana,int Quantity,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"explosive-arrow-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static WeaponFlight[] Flights(PlayerMobile p)=>MagicArrowContactChecks.Flights(p);
    private static BowExplosion[] Blasts(PlayerMobile p)=>BowExplosions.Snapshot().Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static PlayerProcSave Own(PlayerMobile p,Item bow)=>MagicArrowContactChecks.Own(p,bow);
    private static Item Ammo(PlayerMobile p)=>BowAmmunition.Stack(p,CombatTiming.Equipped(p));
    private static DateTime Pmh(Mobile t)=>TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value)?.HealBlockedUntilUtc??default;
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"explosive-arrow-contact-reload-check":"explosive-arrow-contact-check";ConsoleInputHandler.RegisterCommand(name,"Explosive/Freezing parent weapon versus child elemental packets and saved originals",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(var ability in Abilities)foreach(string mode in new[]{"right","quick"})
        {var a=ability;string label="explosive-arrow-"+(a==CombatAbility.ExplodingArrow?"fire":"freeze")+"-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated explosive parent and independent child",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label,a));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(int row,CombatAbility a,bool crossbow,out BaseWeapon bow,out Item gloves,out Item helm)
    {var p=MagicArrowContactChecks.Owner(row,crossbow,out bow,out gloves,out helm);ClassSkillChecks.Train(p,SkillMechanics.For(a).TalentId);p.Backpack.FindItemByType<Arrow>().Amount=100;p.Backpack.FindItemByType<Bolt>().Amount=100;Require(SkillBindings.Instance.Assign(p,false,a)&&SkillBindings.Instance.AssignQuick(p,7,a),"Explosive bindings");return p;}
    private static BaseCreature Satellite(PlayerMobile p,CombatAbility a,int y=2,bool immune=false)
    {var t=new AreaRecipient();t.SetHits(60000);t.MoveToWorld(new(p.X+2,p.Y+y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Poison,100);t.SetResistance(a==CombatAbility.ExplodingArrow?ResistanceType.Fire:ResistanceType.Cold,immune?100:0);return t;}
    private static WeaponFlight Launch(PlayerMobile p,CombatAbility a,BaseCreature t)
    {
        int mana=NecromancerEffects.ManaBalance256(p),quantity=Ammo(p).Amount,cost=SkillMechanics.Values(p,a).Mana256;
        Require(ClassCombat.Perform(p,a,t,t.Location,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity,"Explosive start phase");ClassCombat.ReleaseCast(p);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Explosive parent release");var f=Flights(p).Single();
        Require(BowMissileContact.Handles(f)&&f.Damage.Conversion==null&&!f.Damage.FreezeColdLength&&f.Damage.Elements.FireMin==0&&f.Damage.Elements.ColdMin==0&&f.Damage.Elements.PoisonMin==153&&Blasts(p).Length==0&&Ammo(p).Amount==quantity-1&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Weapon-only parent,child timing or resources");return f;
    }
    private static BowExplosion Contact(PlayerMobile p)
    {for(int i=0;i<100&&Flights(p).Length>0;i++){WeaponMissiles.Tick(Flights(p).Min(x=>x.NextUtc),onlyOwner:p.Serial.Value);}Require(Flights(p).Length==0&&Blasts(p).Length==1,"Parent did not create exactly one independent child");return Blasts(p).Single();}
    private static void ChildState(PlayerMobile p,BaseCreature t,BowExplosion blast,bool immune=false)
    {
        if(!t.Alive||!blast.FreezeColdLength){return;}TimedEffects.TryActor(t,out var actor);
        if(immune){Require(actor?.Control?.Freeze==null&&actor?.Cold==null,"Immune child recipient frozen");return;}
        Require(actor?.Control?.Freeze?.Source==p.Serial.Value&&actor.Cold==null&&Math.Abs((actor.Control.FreezeUntil-blast.NextUtc).TotalMilliseconds-blast.ColdFrames*40)<2,"Child freeze packet/duration/owner or duplicate chill");
    }
    private static void Scenarios(PlayerMobile p,BaseWeapon bow,Item gloves,CombatAbility a)
    {
        using var candidate=PlayerItemProcWorld.Candidate(p);var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();BaseCreature recipient=null;int before=0;
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&v.Packet.HitFlags==32&&v.Packet.FreezeFrames==0&&SkillHealth.Balance256(recipient)==before&&Blasts(p).Length==1,"Parent pre-HP weapon event or child creation order");visits.Add(v);}};
            foreach(string name in new[]{"hit","miss","block","removed","parent-immune","physical","child-immune","child-lethal","parent-lethal","armor"})
            {
                MagicArrowContactChecks.Ready(p);var t=MagicArrowContactChecks.Target(p);recipient=t;var sat=Satellite(p,a,immune:name=="child-immune");var outside=Satellite(p,a,3);if(name=="parent-immune"){t.SetResistance(ResistanceType.Poison,100);}if(name=="physical"){t.SetResistance(ResistanceType.Physical,0);}if(name=="parent-lethal"){SkillHealth.SetBalance256(t,1);}if(name=="child-lethal"){SkillHealth.SetBalance256(sat,1);}before=SkillHealth.Balance256(t);int satBefore=SkillHealth.Balance256(sat),outsideBefore=SkillHealth.Balance256(outside);visits.Clear();WeaponMissiles.HitRoll=()=>name=="miss"?1:0;SourceBlocking.BlockRoll=n=>name=="block"?0:n-1;NpcWorldAllocation.RequireStream();var world=NpcWorldAllocation.Capture();var profile=Own(p,bow);int mana=NecromancerEffects.ManaBalance256(p),quantity=Ammo(p).Amount,cost=SkillMechanics.Values(p,a).Mana256;
                try
                {
                    var f=Launch(p,a,t);Require(visits.Count==0&&SkillHealth.Balance256(t)==before&&Json(Own(p,bow))==Json(profile)&&NpcWorldAllocation.Capture()==world,"Explosive launch proc/damage/RNG");if(name=="armor"){f=f with{Damage=f.Damage with{ArmorDelta=-1}};MagicArrowContactChecks.Hold(p,f);}if(name=="removed"){p.AddToBackpack(bow);p.AddToBackpack(gloves);ClassGear.Refresh(p);}int direct=NamedElements.To256(SkillCombat.MitigatePrepared(p,t,f.Damage.Roll(t,_=>0)with{Poison=0}).Total);bool hit=name is not("miss" or "block"),proc=hit&&name!="removed";
                    var blast=Contact(p);Require(visits.Count==(proc?1:0)&&visits.All(v=>v.Outcomes.Single()is{CastAttempts:1,CastResults:1})&&SkillHealth.Balance256(sat)==satBefore&&SkillHealth.Balance256(outside)==outsideBefore&&before-SkillHealth.Balance256(t)==(hit?Math.Min(before,direct+(name=="parent-immune"?0:153)):0),"Parent/child phase,hit/block/miss/provider or typed HP");
                    if(t.Alive){Require((Pmh(t)>TimedEffects.Now)==(name!="miss"),"Parent accuracy PMH");}if(name=="armor"){Require(TimedEffects.DefenseLoss(t)==1,"Parent captured armor");}
                    var postParent=Own(p,bow);var postWorld=NpcWorldAllocation.Capture();int parentPackets=NativeMonsterReturn.Session(t).Packets;bool alive=t.Alive;int primaryAfter=SkillHealth.Balance256(t);BowExplosions.Tick(blast.NextUtc.AddTicks(-1));Require(SkillHealth.Balance256(sat)==satBefore,"Child fired before one-frame expiry");int calls=0;var damageRoll=WeaponMissiles.DamageRoll;WeaponMissiles.DamageRoll=n=>{calls++;return 0;};try{BowExplosions.Tick(blast.NextUtc);BowExplosions.Tick(blast.NextUtc);}finally{WeaponMissiles.DamageRoll=damageRoll;}
                    int child=name=="child-immune"?0:Math.Min(satBefore,blast.Min256);Require(calls==1&&Blasts(p).Length==0&&SkillHealth.Balance256(sat)==satBefore-child&&SkillHealth.Balance256(outside)==outsideBefore&&SkillHealth.Balance256(t)==primaryAfter&&NativeMonsterReturn.Session(sat).Packets==1&&NativeMonsterReturn.Session(outside).Packets==0&&NativeMonsterReturn.Session(t).Packets==parentPackets+(alive?1:0),"One independent elemental roll,area bounds,primary immunity or native return");
                    Require(visits.Count==(proc?1:0)&&Json(Own(p,bow))==Json(postParent)&&NpcWorldAllocation.Capture()==postWorld&&Pmh(sat)==default&&TimedEffects.Slow(sat,TimedEffects.Now)==0&&TimedEffects.DefenseLoss(sat)==0,"Elemental child inherited weapon proc,RNG,PMH,slow or armor");ChildState(p,sat,blast,name=="child-immune");if(name=="child-lethal"){Require(sat.Corpse is Corpse corpse&&CorpseSkillRules.Shattered(corpse)==(a==CombatAbility.FreezingArrow),"Elemental child lethal freeze mask");}
                    Require(NecromancerEffects.ManaBalance256(p)==mana-cost&&p.Backpack.FindItemByType(bow is Crossbow?typeof(Bolt):typeof(Arrow)).Amount==quantity-1,"Child repaid resources");ulong seed=world.Seed;var actor=profile.Owners.SingleOrDefault();ulong actorSeed=actor?.Seed??0;if(proc){if(actor==null){actorSeed=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref seed)));}SourceRandom.Roll(ref actorSeed);SourceRandom.Roll(ref seed);Require(Own(p,bow).Owners.Single().Seed==actorSeed,"Explosive parent actor draws");}else if(actor!=null){Require(Own(p,bow).Owners.Single().Seed==actorSeed,"Rejected parent advanced actor");}Require(NpcWorldAllocation.Capture().Seed==seed,"Explosive parent/child world draws");
                }
                catch(Exception e){throw new InvalidOperationException(a+" parent/child "+name,e);}
                finally{MagicArrowContactChecks.Clean(p,t,sat,outside);if(bow.Parent!=p){Require(p.EquipItem(bow),"Restore explosive bow");}if(gloves.Parent!=p){Require(p.EquipItem(gloves),"Restore explosive gloves");}ClassGear.Refresh(p);SourceBlocking.BlockRoll=n=>n-1;}
            }
        }
        finally{PlayerItemProcWorld.Observed=old;}
    }
    private static void Check(bool reload)
    {
        using var rolls=new MagicArrowContactChecks.Rolls();var prior=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):null;var saved=new List<Saved>();
        for(int i=0;i<Abilities.Length;i++)
        {
            var a=Abilities[i];PlayerMobile p;BaseWeapon bow;Item gloves,helm;int generation=0;
            if(!reload){p=Owner(2800+i*32,a,false,out bow,out gloves,out helm);Scenarios(p,bow,gloves,a);}
            else
            {
                var s=prior[i];generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);bow=(BaseWeapon)World.FindItem((Serial)s.Bow);gloves=World.FindItem((Serial)s.Gloves);helm=World.FindItem((Serial)s.Helm);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var ammo=World.FindItem((Serial)s.Ammo);
                Require(generation<=2&&bow.Parent==p&&gloves.Parent==p&&helm.Parent==p&&Json(new[]{ClassGear.State(bow),ClassGear.State(gloves),ClassGear.State(helm)})==s.Gear&&Json(Own(p,bow))==s.Profile&&Json(Flights(p).Single())==Json(s.Flight)&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(t)==s.TargetLife&&NecromancerEffects.ManaBalance256(p)==s.Mana&&ammo.Amount==s.Quantity,"Saved explosive original/packet/proc/wound/resources");
                p.MoveToWorld(s.Point,Map.Trammel);t.Frozen=t.CantWalk=true;t.AIObject.AITimer.Stop();t.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Poison,0);var sat=Satellite(p,a);int before=SkillHealth.Balance256(t),satBefore=SkillHealth.Balance256(sat),count=0;var observer=PlayerItemProcWorld.Observed;PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&SkillHealth.Balance256(t)==before&&Blasts(p).Length==1,"Restored parent/child order");count++;}};
                try{using(PlayerItemProcWorld.Candidate(p)){MagicArrowContactChecks.Hold(p,s.Flight with{NextUtc=TimedEffects.Now});var blast=Contact(p);BowExplosions.Tick(blast.NextUtc);Require(count==1&&before-SkillHealth.Balance256(t)==153&&satBefore-SkillHealth.Balance256(sat)==blast.Min256&&NecromancerEffects.ManaBalance256(p)==s.Mana&&ammo.Amount==s.Quantity,"Restored parent/proc/child damage or repayment");ChildState(p,sat,blast);}}finally{PlayerItemProcWorld.Observed=observer;MagicArrowContactChecks.Clean(p,t,sat);}
            }
            var other=Owner(3200+i*32,a,true,out _,out _,out _);var victim=MagicArrowContactChecks.Target(other);var satellite=Satellite(other,a);MagicArrowContactChecks.Ready(other);using(PlayerItemProcWorld.Candidate(other)){Launch(other,a,victim);var blast=Contact(other);BowExplosions.Tick(blast.NextUtc);ChildState(other,satellite,blast);}MagicArrowContactChecks.Clean(other,victim,satellite);other.Delete();
            MagicArrowContactChecks.Ready(p);var target=MagicArrowContactChecks.Target(p);var f=Launch(p,a,target);WeaponMissiles.Tick(f.NextUtc,onlyOwner:p.Serial.Value);f=Flights(p).Single()with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};MagicArrowContactChecks.Hold(p,f);var ammoItem=Ammo(p);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);NecromancerEffects.SetMana256(p,17);var point=p.Location;p.Internalize();saved.Add(new(p.Serial.Value,target.Serial.Value,bow.Serial.Value,gloves.Serial.Value,helm.Serial.Value,ammoItem.Serial.Value,point,Json(new[]{ClassGear.State(bow),ClassGear.State(gloves),ClassGear.State(helm)}),Json(Own(p,bow)),f,SkillHealth.Balance256(p),SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(p),ammoItem.Amount,generation));
        }
        File.WriteAllText(Manifest,Json(saved));
    }
    private static void Input(NetState n,string label,CombatAbility a)
    {
        var rolls=new MagicArrowContactChecks.Rolls();var p=Owner(3400,a,false,out _,out _,out _);var t=MagicArrowContactChecks.Target(p);var sat=Satellite(p,a);MagicArrowContactChecks.Ready(p);var account=FixtureNetwork.Authenticate(n,p,"explosive-arrow-contact-");var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,a).Mana256,quantity=Ammo(p).Amount,satBefore=SkillHealth.Balance256(sat);long deadline=Core.TickCount+6000;bool began=false,sawFlight=false;
        void Close(){MagicArrowContactChecks.Clean(p,t,sat);PlayerItemProcWorld.Observed=old;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try{Require(Core.TickCount<deadline,"Explosive input timeout");var session=ClassCombat.For(p);if(!began&&session.PendingAbility==a){began=true;Require(visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity,"Wire explosive start phase");Log.Information("{Label}: windup",label);}if(Flights(p).Length>0){sawFlight=true;}if(!sawFlight||Flights(p).Length>0||Blasts(p).Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}Require(began&&visits.Count==1&&visits[0].Phase=="domissiledamage"&&visits[0].Packet.Poison256==153&&visits[0].Outcomes.Single()is{CastAttempts:1,CastResults:1}&&SkillHealth.Balance256(sat)<satBefore&&NativeMonsterReturn.Session(sat).Packets==1&&Pmh(sat)==default&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity-1,$"Wire parent/proc/child/resources began={began} visits={visits.Count} phases={string.Join(",",visits.Select(v=>v.Phase))} poison={string.Join(",",visits.Select(v=>v.Packet.Poison256))} outcomes={Json(visits.Select(v=>v.Outcomes))} satellite={satBefore}->{SkillHealth.Balance256(sat)} packets={NativeMonsterReturn.Session(sat).Packets} pmh={Pmh(sat):O} mana={NecromancerEffects.ManaBalance256(p)}/{mana-cost} ammo={Ammo(p).Amount}/{quantity-1}");if(a==CombatAbility.FreezingArrow){Require(NamedControl.FrozenRemaining(sat)>0&&TimedEffects.Chill(sat,TimedEffects.Now)==0,"Wire child freeze");}Close();Log.Information("{Label}-result: passed",label);}catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)a,(int)DiabloClass.Amazon,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
