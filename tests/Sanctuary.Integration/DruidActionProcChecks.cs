// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DruidActionProcChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DruidActionProcChecks));
    private sealed record Saved(uint Owner,uint Item,CombatAbility Ability,CombatAbility Form,Point3D Point,string Gear,string Profile,string Shape,int Life,int Mana,int Generation);
    private static readonly (CombatAbility Ability,CombatAbility Form,string Label)[] Cases=[
        (CombatAbility.Hunger,CombatAbility.Werewolf,"hunger-wolf"),(CombatAbility.Hunger,CombatAbility.Werebear,"hunger-bear"),
        (CombatAbility.Rabies,CombatAbility.Werewolf,"rabies"),(CombatAbility.Fury,CombatAbility.Werewolf,"fury")];
    private static string Manifest=>Path.Join(Core.BaseDirectory,"druid-action-proc-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerProcSave Own(PlayerMobile p,Item item)
    {var s=PlayerItemProcs.Capture();return s with{Items=s.Items.Where(i=>i.Item==item.Serial.Value).ToArray(),Owners=s.Owners.Where(o=>o.Actor==p.Serial.Value).ToArray()};}
    private sealed class Rolls:IDisposable
    {
        private readonly Func<double> Hunger=DruidHunger.HitRoll,Rabies=DruidRabies.HitRoll,Fury=DruidFury.HitRoll;
        private readonly Func<int,int> Block=SourceBlocking.BlockRoll;
        internal static void Hit(bool yes){DruidHunger.HitRoll=DruidRabies.HitRoll=DruidFury.HitRoll=()=>yes?0:1;}
        internal Rolls(){Hit(true);SourceBlocking.BlockRoll=n=>n-1;}
        public void Dispose(){DruidHunger.HitRoll=Hunger;DruidRabies.HitRoll=Rabies;DruidFury.HitRoll=Fury;SourceBlocking.BlockRoll=Block;}
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"druid-action-proc-reload-check":"druid-action-proc-check";ConsoleInputHandler.RegisterCommand(name,"Action-time form contacts,whole originals and saved streams",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(var entry in Cases)foreach(string mode in new[]{"right","quick"})
        {var c=entry;string label="druid-action-proc-"+c.Label+"-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated whole-item action-time form skill",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label,c.Ability,c.Form));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(CombatAbility ability,CombatAbility form,int row,out Item item)
    {
        var p=DruidBasicProcChecks.Owner(form,row,out item);ClassSkillChecks.Train(p,SkillMechanics.For(ability).TalentId);
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Bind action-time form skill");return p;
    }
    private static void ClearRabies(PlayerMobile p)
    {
        var s=DruidRabiesInfections.Snapshot();DruidRabiesInfections.Restore(s with{Infections=s.Infections.Where(i=>i.Caster!=p.Serial.Value).ToArray(),Contagions=s.Contagions.Where(i=>i.Caster!=p.Serial.Value).ToArray(),Seeds=s.Seeds.Where(i=>i.Caster!=p.Serial.Value).ToArray(),Markers=s.Markers.Where(i=>i.Caster!=p.Serial.Value).ToArray()});
    }
    private static int Contacts(PlayerMobile p,CombatAbility ability)=>ability==CombatAbility.Fury?(int)SkillMechanics.Values(p,ability).Eval("calc1"):1;
    private static bool Pending(PlayerMobile p,CombatAbility ability)
    {var s=ClassCombat.For(p);return ability switch{CombatAbility.Hunger=>s.HungerAttack!=null,CombatAbility.Rabies=>s.RabiesAttack!=null,_=>s.Fury!=null};}
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Expected(PlayerMobile p,Item item,int contacts,out ulong seed,out ulong world,out int[] successes)
    {
        world=NpcWorldAllocation.RequireStream().Value;seed=Own(p,item).Owners.SingleOrDefault()?.Seed??SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref world)));successes=new int[contacts];
        for(int i=0;i<contacts;i++){bool hit=SourceRandom.Roll(ref seed)%100<10;successes[i]=hit?1:0;if(hit){SourceRandom.Roll(ref world);}}
    }
    private static void Finish(PlayerMobile p,IronGolemFireBallChecks.Recipient t,CombatAbility ability,int expected)
    {
        if(ability==CombatAbility.Rabies)
        {Require(DruidRabiesInfections.Infection(t)!=null&&TimedEffects.Snapshot().Any(x=>x.Owner==t.Serial.Value&&x.PoisonMs>0),"Bite lost post-contact infection/poison");TimedEffects.ClearGear(t);ClearRabies(p);}
        var flights=SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value).ToArray();Require(flights.Length==expected,"Action-time chance/allocation mismatch");
        foreach(var f in flights){HellslayerChecks.Finish(p,p,t,f);}
    }
    private static void Cast(PlayerMobile p,Item item,BaseCreature target,CombatAbility ability,bool miss=false)
    {
        Ready(p);Rolls.Hit(true);string own=Json(Own(p,item));var world=NpcWorldAllocation.Capture();int before=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
        Require(ClassCombat.Perform(p,ability,target,target.Location,Core.TickCount)&&Pending(p,ability),"Action-time skill start");var s=ClassCombat.For(p);
        Require(NecromancerEffects.ManaBalance256(p)==mana-(ability==CombatAbility.Hunger?0:cost)&&SkillHealth.Balance256(target)==before&&Json(Own(p,item))==own&&NpcWorldAllocation.Capture()==world&&!ClassCombat.ResolveAction(p,s.EffectAt-1),"Action-time start/windup changed payment,life or proc streams");
        Rolls.Hit(!miss);int released=0;
        do
        {
            long at=ClassCombat.For(p).EffectAt;Require(ClassCombat.ResolveAction(p,at),"Action-time skill release");released++;
            int life=SkillHealth.Balance256(target);var profile=Json(Own(p,item));var allocation=NpcWorldAllocation.Capture();Require(!ClassCombat.ResolveAction(p,at)&&SkillHealth.Balance256(target)==life&&Json(Own(p,item))==profile&&NpcWorldAllocation.Capture()==allocation,"Action-time skill replayed a contact/proc");
        }while(ability==CombatAbility.Fury&&ClassCombat.For(p).Fury is{Remaining:>0});
        Require(released==Contacts(p,ability)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Action-time skill lost full series or charged mana again");Rolls.Hit(true);
    }
    private static void Play(PlayerMobile p,Item item,CombatAbility ability)
    {
        var t=HellslayerChecks.Target(p);var observed=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();
        try
        {
            using var proof=PlayerItemProcWorld.Candidate(p);using var rolls=new Rolls();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
            int count=Contacts(p,ability);
            for(int attempt=0;attempt<1000;attempt++)
            {
                visits.Clear();SkillHealth.SetBalance256(t,t.HitsMax*256);Expected(p,item,count,out var seed,out var world,out var success);Cast(p,item,t,ability);
                Require(visits.Count==count*2&&visits.Where((_,i)=>i%2==0).All(v=>v.Phase=="domeleedamage"&&v.Packet.HitFlags==32&&v.Outcomes.Length==0)
                    &&visits.Where((_,i)=>i%2==1).Select(v=>v.Outcomes.Single().CastAttempts).SequenceEqual(success)&&Own(p,item).Owners.Single().Seed==seed&&NpcWorldAllocation.Capture().Seed==world,"Action-time source phases,real chance or separate streams differ");
                Finish(p,t,ability,success.Sum());if(success.Sum()>0){break;}Require(attempt<999,"No successful proc within1000ordinary series");
            }
            visits.Clear();string own=Json(Own(p,item));var allocation=NpcWorldAllocation.Capture();int before=SkillHealth.Balance256(t);Cast(p,item,t,ability,true);
            Require(visits.Count==count&&visits.All(v=>v.Phase=="domeleeattack"&&v.Outcomes.Single().CastAttempts==0)&&Json(Own(p,item))==own&&NpcWorldAllocation.Capture()==allocation&&(ability==CombatAbility.Rabies||SkillHealth.Balance256(t)==before),"Action-time miss emitted a weapon proc or changed streams");Finish(p,t,ability,0);
            visits.Clear();Ready(p);Require(ClassCombat.Perform(p,ability,t,t.Location,Core.TickCount),"Cancelled action-time start");long at=ClassCombat.For(p).EffectAt;int paid=NecromancerEffects.ManaBalance256(p);ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,at)&&visits.Count==0&&NecromancerEffects.ManaBalance256(p)==paid&&Json(Own(p,item))==own&&NpcWorldAllocation.Capture()==allocation,"Cancelled action-time skill replayed/refunded/rolled procs");
        }
        finally{ClassCombat.Stop(p);PlayerItemProcWorld.Observed=observed;SorceressSpellMissiles.Cancel(p);ClearRabies(p);t.Delete();}
    }
    private static void Check(bool reload)
    {
        var saved=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):new Saved[Cases.Length];
        for(int i=0;i<Cases.Length;i++)
        {
            var c=Cases[i];PlayerMobile p;Item item;int generation=0;
            if(!reload){p=Owner(c.Ability,c.Form,3200+i*36,out item);}
            else
            {
                var s=saved[i];generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);item=World.FindItem((Serial)s.Item);
                Require(generation<=2&&s.Ability==c.Ability&&s.Form==c.Form&&item.Parent==p&&Json(ClassGear.State(item))==s.Gear&&Json(Own(p,item))==s.Profile&&Json(DruidPlayerForms.State(p))==s.Shape&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Saved action-time original/form/profile/wounds/payment changed");p.MoveToWorld(s.Point,Map.Trammel);
            }
            Play(p,item,c.Ability);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-257);var point=p.Location;p.Internalize();
            saved[i]=new(p.Serial.Value,item.Serial.Value,c.Ability,c.Form,point,Json(ClassGear.State(item)),Json(Own(p,item)),Json(DruidPlayerForms.State(p)),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),generation);
        }
        File.WriteAllText(Manifest,Json(saved));
    }
    private static void Input(NetState network,string label,CombatAbility ability,CombatAbility form)
    {
        var p=Owner(ability,form,3100,out var item);Ready(p);var t=HellslayerChecks.Target(p);var account=FixtureNetwork.Authenticate(network,p,"druid-action-proc-");var rolls=new Rolls();
        var observed=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();var emitted=new Dictionary<long,SorceressSpellFlight>();
        PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);foreach(var f in SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value)){emitted.TryAdd(f.Id,f);}}};
        int count=Contacts(p,ability);Expected(p,item,count,out var seed,out var world,out var success);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
        bool began=false;long deadline=Core.TickCount+9000;var shape=DruidPlayerForms.State(p);var bindings=SkillBindings.Instance.Get(p);
        void Cleanup(){ClassCombat.Stop(p);rolls.Dispose();PlayerItemProcWorld.Observed=observed;SorceressSpellMissiles.Cancel(p);ClearRabies(p);DruidPlayerForms.Clear(p);t.Delete();item.Delete();p.Delete();account.Delete();}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Action-time skill input timeout");var s=ClassCombat.For(p);
                if(!began&&Pending(p,ability)){Require(!PlayerItemProcs.Initialized(p)&&visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana-(ability==CombatAbility.Hunger?0:cost),"Action-time wire windup rolled proc/changed payment");began=true;Log.Information("{Label}: windup",label);}
                if(visits.Count<count*2){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(began&&visits.Count==count*2&&visits.Where((_,i)=>i%2==0).All(v=>v.Phase=="domeleedamage"&&v.Outcomes.Length==0)
                    &&visits.Where((_,i)=>i%2==1).Select(v=>v.Outcomes.Single().CastAttempts).SequenceEqual(success)&&Own(p,item).Owners.Single().Seed==seed&&NpcWorldAllocation.Capture().Seed==world
                    &&NecromancerEffects.ManaBalance256(p)==mana-cost&&s.PendingTarget==t.Serial&&ReferenceEquals(DruidPlayerForms.State(p),shape)&&SkillBindings.Instance.Get(p)==bindings,"Action-time wire changed full series,chance,streams,shape,target,binding or mana");
                ClassCombat.Stop(p);Require(emitted.Count==success.Sum(),"Full-series wire emission count differs from real chance");
                // A first-swing proc can resolve before Fury's final contact.
                // Observe each birth without pausing its real world clock.
                if(ability==CombatAbility.Rabies){Require(DruidRabiesInfections.Infection(t)!=null,"Wire bite lost infection");TimedEffects.ClearGear(t);ClearRabies(p);}
                foreach(var f in emitted.Values)
                {Require(f.Rank==20&&f.PlayerItemClass==DiabloClass.Druid,"Wire emission borrowed skill rank/class");if(FireBoltChecks.State(f.Id)!=null){HellslayerChecks.Finish(p,p,t,f);}}
                Require(SorceressSpellMissiles.Snapshot().Flights.All(f=>f.Owner!=p.Serial.Value),"Wire left an uninspected proc flight");Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Druid,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
