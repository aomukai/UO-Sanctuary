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

// Qualify existing learned consumers;no alternate skill implementation.
public static class ExistingMeleeProcChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ExistingMeleeProcChecks));
    private static readonly CombatAbility[] Abilities=[CombatAbility.Sacrifice,CombatAbility.Vengeance,CombatAbility.Cleave];
    private sealed record Saved(uint Owner,uint Weapon,uint Gloves,uint Helm,CombatAbility Ability,Point3D Point,string Gear,string Profile,int Life,int Mana,int Durability,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"existing-melee-proc-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private sealed class High:Random{public override int Next(int n)=>n-1;}
    private sealed class Rolls:IDisposable
    {
        private readonly Func<double> SacHit=PaladinSacrifice.HitRoll,SacElement=PaladinSacrifice.ElementRoll,VenHit=PaladinVengeance.HitRoll,VenElement=PaladinVengeance.PhysicalRoll,CleaveHit=CleaveActionProposal.HitRoll,CleaveElement=CleaveActionProposal.ElementRoll;
        private readonly Func<int,int> SacPhysical=PaladinSacrifice.Roll,SacPoison=PaladinSacrifice.PoisonRoll,VenPhysical=PaladinVengeance.WeaponRoll,VenSkill=PaladinVengeance.Roll,VenPoison=PaladinVengeance.PoisonRoll,CleavePhysical=CleaveActionProposal.PhysicalRoll,CleavePoison=CleaveActionProposal.PoisonRoll,Block=SourceBlocking.BlockRoll,Choice=SourceBlocking.ChoiceRoll;
        private readonly Random SacEffects=PaladinSacrifice.Effects,VenEffects=PaladinVengeance.Effects,CleaveEffects=CleaveActionProposal.Effects;
        internal Rolls(){Hit(false);PaladinSacrifice.ElementRoll=PaladinVengeance.PhysicalRoll=CleaveActionProposal.ElementRoll=()=>0;PaladinSacrifice.Roll=PaladinSacrifice.PoisonRoll=PaladinVengeance.WeaponRoll=PaladinVengeance.Roll=PaladinVengeance.PoisonRoll=CleaveActionProposal.PhysicalRoll=CleaveActionProposal.PoisonRoll=_=>0;PaladinSacrifice.Effects=PaladinVengeance.Effects=CleaveActionProposal.Effects=new High();SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=_=>1;}
        internal static void Hit(bool miss){PaladinSacrifice.HitRoll=PaladinVengeance.HitRoll=CleaveActionProposal.HitRoll=()=>miss?1:0;}
        public void Dispose(){PaladinSacrifice.HitRoll=SacHit;PaladinSacrifice.ElementRoll=SacElement;PaladinVengeance.HitRoll=VenHit;PaladinVengeance.PhysicalRoll=VenElement;CleaveActionProposal.HitRoll=CleaveHit;CleaveActionProposal.ElementRoll=CleaveElement;PaladinSacrifice.Roll=SacPhysical;PaladinSacrifice.PoisonRoll=SacPoison;PaladinVengeance.WeaponRoll=VenPhysical;PaladinVengeance.Roll=VenSkill;PaladinVengeance.PoisonRoll=VenPoison;CleaveActionProposal.PhysicalRoll=CleavePhysical;CleaveActionProposal.PoisonRoll=CleavePoison;SourceBlocking.BlockRoll=Block;SourceBlocking.ChoiceRoll=Choice;PaladinSacrifice.Effects=SacEffects;PaladinVengeance.Effects=VenEffects;CleaveActionProposal.Effects=CleaveEffects;}
    }
    private static PlayerProcSave Own(PlayerMobile p,Item item)
    {var s=PlayerItemProcs.Capture();return s with{Owners=s.Owners.Where(x=>x.Actor==p.Serial.Value).ToArray(),Items=s.Items.Where(x=>x.Item==item.Serial.Value).ToArray()};}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"existing-melee-proc-reload-check":"existing-melee-proc-check";ConsoleInputHandler.RegisterCommand(name,"Existing Sacrifice/Vengeance/Cleave proc delivery and exact saved originals",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(var a in Abilities)foreach(string mode in new[]{"right","quick"})
        {var ability=a;string label="existing-melee-proc-"+a.ToString().ToLowerInvariant()+"-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated learned melee with actual item proc providers",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label,ability));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(CombatAbility a,int row,out Item weapon,out Item gloves,out Item helm)
    {
        var p=HellslayerChecks.Owner(a==CombatAbility.Cleave?DiabloClass.Warlock:DiabloClass.Paladin,row);ClassSkillChecks.Train(p,SkillMechanics.For(a).TalentId);weapon=PlayerItemProcWorldChecks.Carrier(p,new Broadsword());gloves=DruidSetupItemChecks.Named(p,"Venom Grip");helm=DruidSetupItemChecks.Named(p,"Blackhorn's Face");ClassGear.Refresh(p);
        Require(SkillBindings.Instance.Assign(p,false,a)&&SkillBindings.Instance.AssignQuick(p,7,a),"Existing melee bindings");return p;
    }
    private static void Ready(PlayerMobile p)
    {PaladinSacrifice.Cancel(p);PaladinVengeance.Cancel(p);CleaveActionProposal.Cancel(p);ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static BaseCreature Target(PlayerMobile p,int dy=0)
    {var t=new PoisonDaggerContactChecks.Recipient();t.SetHits(60000);t.MoveToWorld(new(p.X+1,p.Y+dy,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Poison,0);Require(SourceBlocking.TryRate(t,out int chance,out _)&&chance>0,"Recipient shield control");return t;}
    private static void Clean(PlayerMobile p,params BaseCreature[] targets)
    {Ready(p);SorceressSpellMissiles.Cancel(p);foreach(var t in targets){if(t!=null){SourceKnockbacks.Clear(t);t.Corpse?.Delete();t.Delete();}}}
    private static PlayerWeaponContactPrepared Contact(PlayerMobile p,CombatAbility a)=>a==CombatAbility.Sacrifice?ClassCombat.For(p).SacrificeAttack?.Contact:a==CombatAbility.Vengeance?ClassCombat.For(p).VengeanceAttack?.Contact:null;
    private static long At(PlayerMobile p,CombatAbility a)=>a==CombatAbility.Sacrifice?ClassCombat.For(p).SacrificeAttack.EffectAt:a==CombatAbility.Vengeance?ClassCombat.For(p).VengeanceAttack.EffectAt:ClassCombat.For(p).Cleave.EffectAt;
    private static void Start(PlayerMobile p,BaseCreature t,CombatAbility a)
    {Require(ClassCombat.Perform(p,a,t,t.Location,Core.TickCount),"Existing melee start "+a);ClassCombat.ReleaseCast(p);}
    private static void Scenarios(PlayerMobile p,Item weapon,Item gloves,CombatAbility a)
    {
        using var candidate=PlayerItemProcWorld.Candidate(p);var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();int lifeAtDamage=0;
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){if(v.Phase=="domeleedamage"&&lifeAtDamage!=0){Require(SkillHealth.Balance256(p)==lifeAtDamage,"Sacrifice item hit proc preceded self-cost");}visits.Add(v);}};
            var cases=new List<string>{"hit","miss","block","change","immune","cancel","range","unfunded-release"};if(a==CombatAbility.Cleave){cases.Add("sweep");}if(a==CombatAbility.Sacrifice){cases.Add("self-cost");}
            foreach(string scenario in cases)
            {
                Ready(p);var t=Target(p);BaseCreature second=scenario=="sweep"?Target(p,1):null;visits.Clear();lifeAtDamage=0;Rolls.Hit(scenario=="miss");SourceBlocking.BlockRoll=n=>scenario=="block"?0:n-1;
                if(scenario=="immune"){t.SetResistance(ResistanceType.Poison,100);}if(scenario=="self-cost"){t.SetResistance(ResistanceType.Physical,0);SkillHealth.SetBalance256(p,100*256+17);}
                int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),cost=a==CombatAbility.Sacrifice?0:SkillMechanics.Values(p,a).Mana256;string profile=Json(Own(p,weapon));
                // This family runs before other proc actors;establish the test world domain before measuring draws.
                NpcWorldAllocation.RequireStream();var world=NpcWorldAllocation.Capture();string phase="start";
                try
                {
                    Start(p,t,a);phase="prepared";long at=At(p,a);var contact=Contact(p,a);
                    Require((a==CombatAbility.Cleave||contact!=null)&&visits.Count==0&&SkillHealth.Balance256(t)==before&&Json(Own(p,weapon))==profile&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(p)==mana,"Existing windup consumed contact/proc/RNG/mana");
                    ClassCombat.ResolveAction(p,at-1);Require(visits.Count==0&&SkillHealth.Balance256(t)==before,"Early melee delivery");
                    if(scenario=="change"){p.AddToBackpack(gloves);ClassGear.Refresh(p);t.SetResistance(ResistanceType.Poison,100);}
                    if(scenario=="range"){t.MoveToWorld(new(p.X+10,p.Y,p.Z),p.Map);}
                    if(scenario=="cancel"){ClassCombat.Stop(p);}
                    if(scenario=="unfunded-release"){NecromancerEffects.SetMana256(p,17);mana=17;if(a==CombatAbility.Cleave){cost=0;}}
                    if(scenario=="self-cost"){lifeAtDamage=SkillHealth.Balance256(p)-PaladinSacrifice.Cost256(NamedElements.To256(contact.Impact.Parts.Physical),before,(int)SkillMechanics.Values(p,a).Eval("calc2"));Require(lifeAtDamage<SkillHealth.Balance256(p),"Sacrifice physical self-cost control");}
                    phase="release";bool released=ClassCombat.ResolveAction(p,at);bool absent=scenario=="cancel"||scenario=="range"||scenario=="unfunded-release"&&a==CombatAbility.Vengeance;
                    if(absent)
                    {int spent=scenario=="range"&&a==CombatAbility.Cleave?cost:0;Require(visits.Count==0&&SkillHealth.Balance256(t)==before&&NecromancerEffects.ManaBalance256(p)==mana-spent&&Json(Own(p,weapon))==profile&&NpcWorldAllocation.Capture()==world,"Rejected contact spent proc RNG or damage");continue;}
                    Require(released,"Existing melee release");bool hit=scenario is not("miss" or "block");int contacts=second==null?1:2,events=hit?2*contacts:contacts;
                    Require(visits.Count==events&&visits.Select((v,i)=>v.Phase==(hit&&i%2==0?"domeleedamage":"domeleeattack")).All(x=>x)&&visits.All(v=>v.Packet.HitFlags==(hit?32u:0u)),"Existing proc phase/flags/contact count");
                    int rate=hit&&scenario!="immune"&&!(scenario=="change"&&a==CombatAbility.Cleave)?153:0;
                    Require(visits[0].Packet.Poison256==rate&&before-SkillHealth.Balance256(t)==(hit?(a==CombatAbility.Cleave?rate:contact.Impact.LifeDamage256.Value):0)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Captured/event-time packet or once payment");
                    var poison=TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value&&x.PoisonMs>0);Require(rate==0?poison==null:poison?.PoisonClock?.Rate256==153&&poison.PoisonMs==4000,"Existing source poison/clock changed");
                    Require(hit?visits.All(v=>v.Outcomes.Single() is{CastAttempts:1,CastResults:1}):visits.All(v=>v.Outcomes.All(o=>o.CastAttempts==0)),"Existing hit/attempt proc eligibility");
                    phase="streams";ulong expectedWorld=world.Seed;var actor=JsonSerializer.Deserialize<PlayerProcSave>(profile).Owners.SingleOrDefault();ulong expectedActor=actor?.Seed??SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref expectedWorld)));
                    for(int i=0;i<(hit?events:0);i++){SourceRandom.Roll(ref expectedActor);SourceRandom.Roll(ref expectedWorld);}
                    Require(Own(p,weapon).Owners.Single().Seed==expectedActor&&NpcWorldAllocation.Capture().Seed==expectedWorld,"Existing proc stream/child seed order");
                    phase="sweep";if(second!=null){Require(second.HitsMax*256-SkillHealth.Balance256(second)==153&&ClassCombat.For(p).Cleave.Contacts==2,"Cleave failed per-recipient proc delivery");}
                    phase="replay";int count=visits.Count,remaining=NecromancerEffects.ManaBalance256(p);ClassCombat.ResolveAction(p,at);Require(visits.Count==count&&NecromancerEffects.ManaBalance256(p)==remaining,"Existing contact replayed");
                }
                catch(Exception e){throw new InvalidOperationException($"Existing melee {a}/{scenario}/{phase}",e);}
                finally{lifeAtDamage=0;Clean(p,t,second);if(gloves.Parent!=p){Require(p.EquipItem(gloves),"Restore poison gloves");ClassGear.Refresh(p);}SourceBlocking.BlockRoll=n=>n-1;}
            }
        }
        finally{PlayerItemProcWorld.Observed=old;}
    }
    private static void Check(bool reload)
    {
        using var rolls=new Rolls();var saved=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):new Saved[3];
        for(int i=0;i<3;i++)
        {
            var a=Abilities[i];PlayerMobile p;Item weapon,gloves,helm;int generation=0;
            if(!reload){p=Owner(a,1700+i*36,out weapon,out gloves,out helm);}
            else
            {var s=saved[i];generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);weapon=World.FindItem((Serial)s.Weapon);gloves=World.FindItem((Serial)s.Gloves);helm=World.FindItem((Serial)s.Helm);
                Require(generation<=2&&s.Ability==a&&weapon.Parent==p&&gloves.Parent==p&&helm.Parent==p&&Json(new[]{ClassGear.State(weapon),ClassGear.State(gloves),ClassGear.State(helm)})==s.Gear&&Json(Own(p,weapon))==s.Profile&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&((BaseWeapon)weapon).HitPoints==s.Durability,"Saved existing melee originals,streams,wounds,mana,wear changed");p.MoveToWorld(s.Point,Map.Trammel);}
            Scenarios(p,weapon,gloves,a);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);NecromancerEffects.SetMana256(p,17);var point=p.Location;p.Internalize();saved[i]=new(p.Serial.Value,weapon.Serial.Value,gloves.Serial.Value,helm.Serial.Value,a,point,Json(new[]{ClassGear.State(weapon),ClassGear.State(gloves),ClassGear.State(helm)}),Json(Own(p,weapon)),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),((BaseWeapon)weapon).HitPoints,generation);
        }
        File.WriteAllText(Manifest,Json(saved));
    }
    private static void Input(NetState n,string label,CombatAbility a)
    {
        var rolls=new Rolls();var p=Owner(a,2300,out _,out _,out _);var t=Target(p);Ready(p);var account=FixtureNetwork.Authenticate(n,p,"existing-melee-proc-");var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
        int mana=NecromancerEffects.ManaBalance256(p),cost=a==CombatAbility.Sacrifice?0:SkillMechanics.Values(p,a).Mana256;long deadline=Core.TickCount+6000;bool began=false;
        void Close(){Clean(p,t);PlayerItemProcWorld.Observed=old;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try
            {Require(Core.TickCount<deadline,"Existing melee input timeout");var s=ClassCombat.For(p);if(!began&&(s.SacrificeAttack!=null||s.VengeanceAttack!=null||s.Cleave!=null)){began=true;Require(visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana,"Existing wire windup procced or paid");Log.Information("{Label}: windup",label);}
                if(visits.Count<2){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(began&&visits.Count==2&&visits[0].Packet.Poison256==153&&visits[1].Phase=="domeleeattack"&&visits.All(v=>v.Outcomes.Single() is{CastAttempts:1,CastResults:1})&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Existing wire packet/procs/payment");Close();Log.Information("{Label}-result: passed",label);}
            catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)a,(int)(a==CombatAbility.Cleave?DiabloClass.Warlock:DiabloClass.Paladin),t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
