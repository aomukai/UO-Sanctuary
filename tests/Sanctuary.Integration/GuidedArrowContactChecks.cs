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

public static class GuidedArrowContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GuidedArrowContactChecks));
    private sealed record Saved(uint Owner,uint Target,uint Bow,uint Gloves,uint Helm,uint Ammo,Point3D Point,string Gear,string Profile,WeaponFlight Flight,int Life,int TargetLife,int Mana,int Quantity,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"guided-arrow-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static WeaponFlight[] Flights(PlayerMobile p)=>MagicArrowContactChecks.Flights(p);
    private static PlayerProcSave Own(PlayerMobile p,Item bow)=>MagicArrowContactChecks.Own(p,bow);
    private static Item Ammo(PlayerMobile p)=>BowAmmunition.Stack(p,CombatTiming.Equipped(p));
    private static DateTime Pmh(Mobile t)=>TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value)?.HealBlockedUntilUtc??default;
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"guided-arrow-contact-reload-check":"guided-arrow-contact-check";ConsoleInputHandler.RegisterCommand(name,"Guided auto-hit ordered impact,blocking,no accuracy PMH,item procs and saved original",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"right","quick"})
        {string label="guided-arrow-contact-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated Guided Arrow auto-hit and item hit proc",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(int row,bool crossbow,out BaseWeapon bow,out Item gloves,out Item helm)
    {var p=MagicArrowContactChecks.Owner(row,crossbow,out bow,out gloves,out helm);ClassSkillChecks.Train(p,"amazon.guidedarrow");p.Backpack.FindItemByType<Arrow>().Amount=100;p.Backpack.FindItemByType<Bolt>().Amount=100;Require(SkillBindings.Instance.Assign(p,false,CombatAbility.GuidedArrow)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.GuidedArrow),"Guided bindings");return p;}
    private static void Drain(PlayerMobile p)
    {for(int i=0;i<260&&Flights(p).Length>0;i++){WeaponMissiles.Tick(Flights(p).Min(x=>x.NextUtc),onlyOwner:p.Serial.Value);}Require(Flights(p).Length==0,"Guided flight did not finish");}
    private static WeaponFlight Launch(PlayerMobile p,BaseCreature target,bool ground=false)
    {
        int mana=NecromancerEffects.ManaBalance256(p),quantity=Ammo(p).Amount,cost=SkillMechanics.Values(p,CombatAbility.GuidedArrow).Mana256;var aim=ground?new Point3D(p.X+2,p.Y,p.Z):target.Location;
        Require(ClassCombat.Perform(p,CombatAbility.GuidedArrow,ground?null:target,aim,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity,"Guided paid start phase");ClassCombat.ReleaseCast(p);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Guided release");var f=Flights(p).Single();
        Require(BowMissileContact.Handles(f)&&f.Guided?.Mode==(ground?2:1)&&f.Pierces==0&&f.Damage.Conversion==null&&f.Damage.Elements.PoisonMin==153&&Ammo(p).Amount==quantity-1&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Guided released mode/poison/no pierce/resources");return f;
    }
    private static void Scenarios(PlayerMobile p,BaseWeapon bow,Item gloves)
    {
        using var candidate=PlayerItemProcWorld.Candidate(p);var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();BaseCreature recipient=null;int before=0;
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&v.Packet.HitFlags==32&&SkillHealth.Balance256(recipient)==before&&Pmh(recipient)==default,"Guided pre-HP event or accuracy-PMH inference");visits.Add(v);}};
            foreach(string name in new[]{"hit","block","immune","removed","physical","lethal","interceptor","ground","armor"})
            {
                MagicArrowContactChecks.Ready(p);var t=MagicArrowContactChecks.Target(p,name=="interceptor"?6:3);var decoy=name=="interceptor"?MagicArrowContactChecks.Target(p):null;recipient=t;if(name=="immune"){t.SetResistance(ResistanceType.Poison,100);}if(name=="physical"){t.SetResistance(ResistanceType.Physical,0);}if(name=="lethal"){SkillHealth.SetBalance256(t,1);}before=SkillHealth.Balance256(t);visits.Clear();SourceBlocking.BlockRoll=n=>name=="block"?0:n-1;NpcWorldAllocation.RequireStream();var world=NpcWorldAllocation.Capture();var profile=Own(p,bow);int mana=NecromancerEffects.ManaBalance256(p),quantity=Ammo(p).Amount,cost=SkillMechanics.Values(p,CombatAbility.GuidedArrow).Mana256;
                try
                {
                    var f=Launch(p,t,name=="ground");Require(visits.Count==0&&SkillHealth.Balance256(t)==before&&Json(Own(p,bow))==Json(profile)&&NpcWorldAllocation.Capture()==world&&Pmh(t)==default,"Guided launch proc/damage/RNG/setup");
                    if(name=="armor"){f=f with{Damage=f.Damage with{ArmorDelta=-1}};MagicArrowContactChecks.Hold(p,f);}if(name=="removed"){p.AddToBackpack(bow);p.AddToBackpack(gloves);ClassGear.Refresh(p);}var raw=f.Damage.Roll(t,_=>0);int direct=NamedElements.To256(SkillCombat.MitigatePrepared(p,t,raw with{Poison=0}).Total);bool hit=name!="block",proc=hit&&name!="removed";
                    Drain(p);Require(visits.Count==(proc?1:0)&&visits.All(v=>v.Outcomes.Single()is{CastAttempts:1,CastResults:1}),"Guided auto-hit/block/provider proc gate");Require(before-SkillHealth.Balance256(t)==(hit?Math.Min(before,direct+(name=="immune"?0:153)):0)&&NativeMonsterReturn.Session(t).Packets==(hit?1:0)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&p.Backpack.FindItemByType(bow is Crossbow?typeof(Bolt):typeof(Arrow)).Amount==quantity-1,"Guided typed HP/return/resources");
                    if(t.Alive){Require(Pmh(t)==default&&TimedEffects.Slow(t,TimedEffects.Now)==(hit?20:0),"Guided PMH or item slow");}if(name=="immune"){Require(TimedEffects.CanRegenerateHits(t),"Zero-damage Guided unexpectedly created heal prevention");}if(name=="armor"){Require(TimedEffects.DefenseLoss(t)==1,"Guided captured armor missing/doubled");}if(name=="physical"){Require(t.Notifications==1&&t.AtNotification==before&&visits[0].Packet.Physical256>0,"Guided native notification/physical lane");}if(decoy!=null){Require(SkillHealth.Balance256(decoy)==decoy.HitsMax*256&&NativeMonsterReturn.Session(decoy).Packets==0&&Pmh(decoy)==default,"Locked Guided hit/procced on interceptor");}
                    ulong seed=world.Seed;var actor=profile.Owners.SingleOrDefault();ulong actorSeed=actor?.Seed??0;if(proc){if(actor==null){actorSeed=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref seed)));}SourceRandom.Roll(ref actorSeed);SourceRandom.Roll(ref seed);Require(Own(p,bow).Owners.Single().Seed==actorSeed,"Guided actor draws");}else if(actor!=null){Require(Own(p,bow).Owners.Single().Seed==actorSeed,"Rejected Guided advanced actor");}Require(NpcWorldAllocation.Capture().Seed==seed,"Guided child/world draws");
                }
                catch(Exception e){throw new InvalidOperationException("Guided contact "+name,e);}
                finally{MagicArrowContactChecks.Clean(p,t,decoy);if(bow.Parent!=p){Require(p.EquipItem(bow),"Restore Guided bow");}if(gloves.Parent!=p){Require(p.EquipItem(gloves),"Restore Guided gloves");}ClassGear.Refresh(p);SourceBlocking.BlockRoll=n=>n-1;}
            }
        }
        finally{PlayerItemProcWorld.Observed=old;}
    }
    private static void Check(bool reload)
    {
        using var rolls=new MagicArrowContactChecks.Rolls();WeaponMissiles.HitRoll=()=>throw new InvalidOperationException("Guided impact attempted accuracy");PlayerMobile p;BaseWeapon bow;Item gloves,helm;int generation=0;
        if(!reload){p=Owner(2700,false,out bow,out gloves,out helm);Scenarios(p,bow,gloves);}
        else
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);bow=(BaseWeapon)World.FindItem((Serial)s.Bow);gloves=World.FindItem((Serial)s.Gloves);helm=World.FindItem((Serial)s.Helm);var target=(BaseCreature)World.FindMobile((Serial)s.Target);var ammo=World.FindItem((Serial)s.Ammo);
            Require(generation<=2&&bow.Parent==p&&gloves.Parent==p&&helm.Parent==p&&Json(new[]{ClassGear.State(bow),ClassGear.State(gloves),ClassGear.State(helm)})==s.Gear&&Json(Own(p,bow))==s.Profile&&Json(Flights(p).Single())==Json(s.Flight)&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(target)==s.TargetLife&&NecromancerEffects.ManaBalance256(p)==s.Mana&&ammo.Amount==s.Quantity,"Saved Guided original/controller/proc/wound/resources");
            p.MoveToWorld(s.Point,Map.Trammel);target.Frozen=target.CantWalk=true;target.AIObject.AITimer.Stop();target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);foreach(var r in Enum.GetValues<ResistanceType>()){target.SetResistance(r,100);}target.SetResistance(ResistanceType.Poison,0);
            var observer=PlayerItemProcWorld.Observed;int count=0,before=SkillHealth.Balance256(target);PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&SkillHealth.Balance256(target)==before&&Pmh(target)==default&&v.Outcomes.Single()is{CastAttempts:1,CastResults:1},"Restored Guided proc/PMH phase");count++;}};
            try{using(PlayerItemProcWorld.Candidate(p)){MagicArrowContactChecks.Hold(p,s.Flight with{NextUtc=TimedEffects.Now});Drain(p);}Require(count==1&&before-SkillHealth.Balance256(target)==153&&Pmh(target)==default&&NecromancerEffects.ManaBalance256(p)==s.Mana&&ammo.Amount==s.Quantity,"Restored Guided damage/proc/repayment");}finally{PlayerItemProcWorld.Observed=observer;MagicArrowContactChecks.Clean(p,target);}
        }
        var other=Owner(3200,true,out _,out _,out _);var victim=MagicArrowContactChecks.Target(other);MagicArrowContactChecks.Ready(other);using(PlayerItemProcWorld.Candidate(other)){Launch(other,victim);Drain(other);}Require(victim.HitsMax*256-SkillHealth.Balance256(victim)==153&&Pmh(victim)==default,"Crossbow Guided route");MagicArrowContactChecks.Clean(other,victim);other.Delete();
        MagicArrowContactChecks.Ready(p);var t=MagicArrowContactChecks.Target(p);var f=Launch(p,t);WeaponMissiles.Tick(f.NextUtc,onlyOwner:p.Serial.Value);f=Flights(p).Single()with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};MagicArrowContactChecks.Hold(p,f);var ammoItem=Ammo(p);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);NecromancerEffects.SetMana256(p,17);var point=p.Location;p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,t.Serial.Value,bow.Serial.Value,gloves.Serial.Value,helm.Serial.Value,ammoItem.Serial.Value,point,Json(new[]{ClassGear.State(bow),ClassGear.State(gloves),ClassGear.State(helm)}),Json(Own(p,bow)),f,SkillHealth.Balance256(p),SkillHealth.Balance256(t),NecromancerEffects.ManaBalance256(p),ammoItem.Amount,generation)));
    }
    private static void Input(NetState n,string label)
    {
        var rolls=new MagicArrowContactChecks.Rolls();WeaponMissiles.HitRoll=()=>throw new InvalidOperationException("Wire Guided attempted accuracy");var p=Owner(3400,false,out _,out _,out _);var t=MagicArrowContactChecks.Target(p);MagicArrowContactChecks.Ready(p);var account=FixtureNetwork.Authenticate(n,p,"guided-arrow-contact-");var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.GuidedArrow).Mana256,quantity=Ammo(p).Amount;long deadline=Core.TickCount+6000;bool began=false,sawFlight=false;
        void Close(){MagicArrowContactChecks.Clean(p,t);PlayerItemProcWorld.Observed=old;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try{Require(Core.TickCount<deadline,"Guided input timeout");var session=ClassCombat.For(p);if(!began&&session.PendingAbility==CombatAbility.GuidedArrow){began=true;Require(visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity,"Wire Guided start/ammo/proc phase");Log.Information("{Label}: windup",label);}if(Flights(p).Length>0){sawFlight=true;}if(!sawFlight||Flights(p).Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}Require(began&&visits.Count==1&&visits[0].Phase=="domissiledamage"&&visits[0].Packet.Poison256==153&&visits[0].Outcomes.Single()is{CastAttempts:1,CastResults:1}&&NecromancerEffects.ManaBalance256(p)==mana-cost&&Ammo(p).Amount==quantity-1&&Pmh(t)==default,"Wire Guided impact/proc/resources/no accuracy PMH");Close();Log.Information("{Label}-result: passed",label);}catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.GuidedArrow,(int)DiabloClass.Amazon,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
