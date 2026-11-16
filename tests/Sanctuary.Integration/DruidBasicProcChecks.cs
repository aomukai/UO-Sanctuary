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

public static class DruidBasicProcChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DruidBasicProcChecks));
    private sealed record Saved(uint Owner,uint Item,Point3D Point,string Gear,string Profile,string Form,int Life,int Mana,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"druid-basic-proc-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerProcSave Own(PlayerMobile p,Item item)
    {var s=PlayerItemProcs.Capture();return s with{Items=s.Items.Where(i=>i.Item==item.Serial.Value).ToArray(),Owners=s.Owners.Where(o=>o.Actor==p.Serial.Value).ToArray()};}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"druid-basic-proc-reload-check":"druid-basic-proc-check";
            ConsoleInputHandler.RegisterCommand(name,"Complete original form melee proc delivery and saved wounds/streams",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"wolf-left","wolf-right","bear-left","bear-right"})
        {string label="druid-basic-proc-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated transformed common Attack with complete original",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label,mode.StartsWith("wolf",StringComparison.Ordinal)?CombatAbility.Werewolf:CombatAbility.Werebear));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(CombatAbility form,int row,out Item item)
    {
        var p=HellslayerChecks.Owner(DiabloClass.Druid,row);
        ClassSkillChecks.Train(p,"druid.werewolf");ClassSkillChecks.Train(p,"druid.werebear");
        for(int i=0;i<20;i++){if(ClassCreation.Profiles.GetRank(p.Serial,"druid.lycanthropy")<=i){Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.lycanthropy",out _),"Train saved form duration");}}
        item=HellslayerChecks.Original(p);p.AddToBackpack(item);Require(p.EquipItem(item),"Equip complete form original");ClassGear.Refresh(p);
        Require(DruidPlayerForms.ApplyPrepared(p,form,TimedEffects.Now),"Enter trained form");NecromancerEffects.SetMana256(p,0);return p;
    }
    private static bool Attack(PlayerMobile p,BaseCreature t,bool checkWindup=true)
    {
        ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;
        if(!ClassCombat.Perform(p,CombatAbility.BasicAttack,t,t.Location,Core.TickCount)){return false;}
        int before=SkillHealth.Balance256(t);var profile=Json(Own(p,CombatTiming.Equipped(p)));var world=NpcWorldAllocation.Capture();
        Require(s.Timing==DruidFormAttacks.Timing(p)&&s.Animation==(byte)WeaponAnimation.Wrestle,"Proc equipment replaced form A1 clock/pose");
        if(checkWindup){Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&SkillHealth.Balance256(t)==before&&Json(Own(p,CombatTiming.Equipped(p)))==profile&&NpcWorldAllocation.Capture()==world,"Form windup emitted damage or drew proc RNG");}
        bool result=ClassCombat.ResolveAction(p,s.EffectAt);profile=Json(Own(p,CombatTiming.Equipped(p)));world=NpcWorldAllocation.Capture();before=SkillHealth.Balance256(t);
        Require(!ClassCombat.ResolveAction(p,s.EffectAt)&&Json(Own(p,CombatTiming.Equipped(p)))==profile&&NpcWorldAllocation.Capture()==world&&SkillHealth.Balance256(t)==before,"Form action replayed its packet/proc");return result;
    }
    private static void Controls(PlayerMobile p,Item original)
    {
        var t=HellslayerChecks.Target(p);var hit=DruidFormAttacks.HitRoll;var block=SourceBlocking.BlockRoll;var observe=PlayerItemProcWorld.Observed;
        var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
        try
        {
            using var candidate=PlayerItemProcWorld.Candidate(p);DruidFormAttacks.HitRoll=()=>1;
            var world=NpcWorldAllocation.Capture();string own=Json(Own(p,original));int life=SkillHealth.Balance256(p);
            PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,1,TimedEffects.Now.AddMinutes(5)));
            Require(Attack(p,t)&&visits.Count==1&&visits[0].Phase=="domeleeattack"&&visits[0].Outcomes.Single().CastAttempts==0&&Json(Own(p,original))==own&&NpcWorldAllocation.Capture()==world&&life-SkillHealth.Balance256(p)==256,"Form miss rolled procs or duplicated/lost one flat return");PaladinAuras.Clear(t);
            visits.Clear();DruidFormAttacks.HitRoll=()=>0;SourceBlocking.BlockRoll=_=>0;
            var choice=SourceBlocking.ChoiceRoll;SourceBlocking.ChoiceRoll=_=>1;
            var shield=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};
            try
            {
                shield.SetHits(60000);shield.MoveToWorld(t.Location,t.Map);shield.AIObject.AITimer.Stop();
                Require(SourceBlocking.TryRate(shield,out int chance,out _)&&chance>0,"Missing actual shield control");
                Require(Attack(p,shield)&&visits.Count==1&&visits[0].Outcomes.Single().CastAttempts==0&&Json(Own(p,original))==own&&NpcWorldAllocation.Capture()==world,"Blocked form contact fabricated proc eligibility");
            }
            finally{SourceBlocking.ChoiceRoll=choice;shield.Delete();}
            visits.Clear();t.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);int before=SkillHealth.Balance256(t);
            Require(Attack(p,t)&&visits.Count==0&&SkillHealth.Balance256(t)==before&&Json(Own(p,original))==own&&NpcWorldAllocation.Capture()==world,"Form inherited projectile reach or out-of-range RNG");
            // The existing form melee scope applies even to ranged/throwing
            // originals. These explicit event-only fixtures do not admit names.
            p.AddToBackpack(original);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);SourceBlocking.BlockRoll=n=>n-1;
            foreach(BaseWeapon weapon in new BaseWeapon[]{new Bow(),new Crossbow(),ItemOrigins.Create(ItemOrigin.Drop,()=>new SoulGlaive())})
            {
                if(weapon is SoulGlaive){ClassGear.Profiles.FromMonster(weapon,30,GearRarity.Normal,"glv",GearTier.Normal,new Random(173));}
                else{ClassGear.Profiles.Convert(weapon,30,GearRarity.Normal,new Random(173));}
                p.AddToBackpack(weapon);var graph=new ItemPropertyCreationContext(weapon,new EquipmentStatValues(_=>4,_=>null,_=>null,(_,_,_,_,_)=>{}),default,false,false,false,SourceRandom.InitLow(17),[]);
                graph.ApplySkill(EquipmentPropertyWriter.Plan("att-skill"),new(100,20,47),ItemSkillProperties.ForLevel(99));PlayerItemProcs.Register(weapon,4,graph.Capture());Require(p.EquipItem(weapon),"Equip form ranged carrier");
                int quantity=NamedAmmunition.Remaining(weapon);long first=SorceressSpellMissiles.Snapshot().NextId;visits.Clear();
                Require(PlayerBasicContact.Handles(p,t)&&Attack(p,t)&&visits.Count==2&&visits[1].Outcomes.Single().CastResults==1&&NamedAmmunition.Remaining(weapon)==quantity&&NecromancerEffects.ManaBalance256(p)==0,"Form ranged original lost melee proc/resource semantics");
                var f=SorceressSpellMissiles.Snapshot().Flights.Single(f=>f.Owner==p.Serial.Value&&f.Id>first);HellslayerChecks.Finish(p,p,t,f);
                p.AddToBackpack(weapon);weapon.Delete();
            }
            Require(p.EquipItem(original),"Restore same complete original after controls");ClassGear.Refresh(p);
        }
        finally{PaladinAuras.Clear(t);ClassCombat.Stop(p);SorceressSpellMissiles.Cancel(p);DruidFormAttacks.HitRoll=hit;SourceBlocking.BlockRoll=block;PlayerItemProcWorld.Observed=observe;t.Delete();}
    }
    private static void Check(bool reload)
    {
        var saved=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):new Saved[2];var hit=DruidFormAttacks.HitRoll;
        try
        {
            DruidFormAttacks.HitRoll=()=>0;
            for(int i=0;i<2;i++)
            {
                PlayerMobile p;Item item;int generation=0;
                if(!reload){p=Owner(i==0?CombatAbility.Werewolf:CombatAbility.Werebear,3700+i*45,out item);}
                else
                {
                    var s=saved[i];generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);item=World.FindItem((Serial)s.Item);
                    Require(generation<=2&&item.Parent==p&&Json(ClassGear.State(item))==s.Gear&&Json(Own(p,item))==s.Profile&&Json(DruidPlayerForms.State(p))==s.Form&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Saved form/original/streams/wounds changed");p.MoveToWorld(s.Point,Map.Trammel);
                }
                HellslayerChecks.PlayerAttack(p,item);Controls(p,item);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-257);var point=p.Location;ClassCombat.Stop(p);p.Internalize();
                saved[i]=new(p.Serial.Value,item.Serial.Value,point,Json(ClassGear.State(item)),Json(Own(p,item)),Json(DruidPlayerForms.State(p)),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),generation);
            }
            File.WriteAllText(Manifest,Json(saved));
        }
        finally{DruidFormAttacks.HitRoll=hit;}
    }
    private static void Input(NetState network,string label,CombatAbility form)
    {
        var p=Owner(form,3000,out var item);var t=HellslayerChecks.Target(p);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.BasicAttack)&&SkillBindings.Instance.Assign(p,false,CombatAbility.BasicAttack),"Bind form common Attack");var account=FixtureNetwork.Authenticate(network,p,"druid-basic-proc-");
        var binding=SkillBindings.Instance.Get(p);var stateForm=DruidPlayerForms.State(p);var hit=DruidFormAttacks.HitRoll;var block=SourceBlocking.BlockRoll;var observed=PlayerItemProcWorld.Observed;DruidFormAttacks.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;
        ulong expectedWorld=NpcWorldAllocation.RequireStream().Value;ulong expectedActor=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref expectedWorld)));bool success=SourceRandom.Roll(ref expectedActor)%100<10;if(success){SourceRandom.Roll(ref expectedWorld);}
        bool began=false;long deadline=Core.TickCount+5000;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
        void Cleanup(){ClassCombat.Stop(p);DruidFormAttacks.HitRoll=hit;SourceBlocking.BlockRoll=block;PlayerItemProcWorld.Observed=observed;SorceressSpellMissiles.Cancel(p);DruidPlayerForms.Clear(p);t.Delete();item.Delete();p.Delete();account.Delete();}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Form original input timeout");var state=ClassCombat.For(p);
                if(!began&&state.PendingAbility==CombatAbility.BasicAttack){Require(!PlayerItemProcs.Initialized(p)&&visits.Count==0&&NecromancerEffects.ManaBalance256(p)==0&&state.Timing==DruidFormAttacks.Timing(p),"Form windup changed clock/stream/resources");began=true;Log.Information("{Label}: windup",label);}
                if(visits.Count<2){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(began&&visits[0].Phase=="domeleedamage"&&visits[0].Outcomes.Length==0&&visits[1].Phase=="domeleeattack"&&visits[1].Outcomes.Single().CastAttempts==(success?1:0)
                    &&PlayerItemProcs.Capture().Owners.Single(o=>o.Actor==p.Serial.Value).Seed==expectedActor&&NpcWorldAllocation.Capture().Seed==expectedWorld&&NecromancerEffects.ManaBalance256(p)==0&&state.PendingTarget==t.Serial&&SkillBindings.Instance.Get(p)==binding&&ReferenceEquals(DruidPlayerForms.State(p),stateForm),"Form wire changed chance/streams/form/target/bindings or mana");
                ClassCombat.Stop(p);var flights=SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value).ToArray();Require(flights.Length==(success?1:0),"Form chance/allocation mismatch");if(success){HellslayerChecks.Finish(p,p,t,flights.Single());}Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.BasicAttack,(int)DiabloClass.Druid,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
