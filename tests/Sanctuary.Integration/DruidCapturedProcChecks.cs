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

public static class DruidCapturedProcChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DruidCapturedProcChecks));
    private sealed record Saved(uint Owner,uint Item,CombatAbility Ability,CombatAbility Form,Point3D Point,string Gear,string Profile,string Shape,int Life,int Mana,int Generation);
    private static readonly (CombatAbility Ability,CombatAbility Form,string Label)[] Cases=[
        (CombatAbility.Maul,CombatAbility.Werebear,"maul"),(CombatAbility.FeralRage,CombatAbility.Werewolf,"feral"),
        (CombatAbility.FireClaws,CombatAbility.Werewolf,"fire-wolf"),(CombatAbility.FireClaws,CombatAbility.Werebear,"fire-bear")];
    private static string Manifest=>Path.Join(Core.BaseDirectory,"druid-captured-proc-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerProcSave Own(PlayerMobile p,Item item)
    {var s=PlayerItemProcs.Capture();return s with{Items=s.Items.Where(i=>i.Item==item.Serial.Value).ToArray(),Owners=s.Owners.Where(o=>o.Actor==p.Serial.Value).ToArray()};}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"druid-captured-proc-reload-check":"druid-captured-proc-check";ConsoleInputHandler.RegisterCommand(name,"Captured form skill packets,whole originals and saved streams",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(var entry in Cases)foreach(string mode in new[]{"right","quick"})
        {var c=entry;string label="druid-captured-proc-"+c.Label+"-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated captured form skill with complete Hellslayer",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label,c.Ability,c.Form));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private sealed class Rolls:IDisposable
    {
        private readonly Func<double> Charged=DruidChargedAttacks.HitRoll,Fire=DruidFireClaws.HitRoll;
        private readonly Func<int,int> Block=SourceBlocking.BlockRoll;
        internal Rolls(){DruidChargedAttacks.HitRoll=DruidFireClaws.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;}
        internal static void Miss()=>DruidChargedAttacks.HitRoll=DruidFireClaws.HitRoll=()=>1;
        internal static void Hit()=>DruidChargedAttacks.HitRoll=DruidFireClaws.HitRoll=()=>0;
        public void Dispose(){DruidChargedAttacks.HitRoll=Charged;DruidFireClaws.HitRoll=Fire;SourceBlocking.BlockRoll=Block;}
    }
    private static PlayerMobile Owner(CombatAbility ability,CombatAbility form,int row,out Item item)
    {
        var p=DruidBasicProcChecks.Owner(form,row,out item);ClassSkillChecks.Train(p,SkillMechanics.For(ability).TalentId);
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Bind trained captured skill");Mana(p);return p;
    }
    private static void Mana(PlayerMobile p)=>NecromancerEffects.SetMana256(p,p.ManaMax*256);
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;}
    private static PlayerWeaponContactPrepared Contact(PlayerMobile p,CombatAbility ability)=>ability==CombatAbility.FireClaws?ClassCombat.For(p).FireClawAttack?.Contact:ClassCombat.For(p).DruidChargeAttack?.Contact;
    private static bool Cast(PlayerMobile p,BaseCreature target,CombatAbility ability,bool miss=false)
    {
        Ready(p);Mana(p);if(miss){Rolls.Miss();}else{Rolls.Hit();}
        int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(target);string own=Json(Own(p,CombatTiming.Equipped(p)));var world=NpcWorldAllocation.Capture();
        Require(ClassCombat.Perform(p,ability,target,target.Location,Core.TickCount),"Captured skill start");var s=ClassCombat.For(p);var contact=Contact(p,ability);
        Require(contact!=null&&NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,ability).Mana256&&SkillHealth.Balance256(target)==life&&Json(Own(p,CombatTiming.Equipped(p)))==own&&NpcWorldAllocation.Capture()==world,"Captured start changed proc streams/life or payment");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&Json(Own(p,CombatTiming.Equipped(p)))==own&&NpcWorldAllocation.Capture()==world,"Captured skill ran before its event");
        // Reverse the roll supplier after capture:release must not call it.
        if(miss){Rolls.Hit();}else{Rolls.Miss();}
        bool result=ClassCombat.ResolveAction(p,s.EffectAt);Rolls.Hit();own=Json(Own(p,CombatTiming.Equipped(p)));world=NpcWorldAllocation.Capture();life=SkillHealth.Balance256(target);
        Require(!ClassCombat.ResolveAction(p,s.EffectAt)&&Json(Own(p,CombatTiming.Equipped(p)))==own&&NpcWorldAllocation.Capture()==world&&SkillHealth.Balance256(target)==life,"Captured skill replayed damage/proc");return result;
    }
    private static void Play(PlayerMobile p,Item item,CombatAbility ability)
    {
        var t=HellslayerChecks.Target(p);var observer=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();
        try
        {
            using var candidate=PlayerItemProcWorld.Candidate(p);using var rolls=new Rolls();
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
            for(int i=0;i<1000;i++)
            {
                visits.Clear();SkillHealth.SetBalance256(t,t.HitsMax*256);ulong world=NpcWorldAllocation.RequireStream().Value;var actor=Own(p,item).Owners.SingleOrDefault();
                ulong seed=actor?.Seed??SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref world)));bool success=SourceRandom.Roll(ref seed)%100<10;if(success){SourceRandom.Roll(ref world);}
                Require(Cast(p,t,ability)&&visits.Select(v=>v.Phase).SequenceEqual(new[]{"domeleedamage","domeleeattack"})&&visits[0].Outcomes.Length==0&&visits[0].Packet.HitFlags==32
                    &&visits[1].Outcomes.Single().CastAttempts==(success?1:0)&&Own(p,item).Owners.Single().Seed==seed&&NpcWorldAllocation.Capture().Seed==world,"Captured contact lost real chance/source phases/streams");
                var flights=SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value).ToArray();Require(flights.Length==(success?1:0),"Captured chance/allocation mismatch");
                if(success){HellslayerChecks.Finish(p,p,t,flights.Single());break;}Require(i<999,"No real proc in1000draws");
            }
            DruidAttackCharges.Clear(p);visits.Clear();string profile=Json(Own(p,item));var allocation=NpcWorldAllocation.Capture();int life=SkillHealth.Balance256(t);
            Require(Cast(p,t,ability,true)&&visits.Count==1&&visits[0].Outcomes.Single().CastAttempts==0&&Json(Own(p,item))==profile&&NpcWorldAllocation.Capture()==allocation&&SkillHealth.Balance256(t)==life&&DruidAttackCharges.State(p,CombatAbility.Maul)==null&&DruidAttackCharges.State(p,CombatAbility.FeralRage)==null,"Latched miss rerolled/gained/rolled proc");
            visits.Clear();Ready(p);Mana(p);Require(ClassCombat.Perform(p,ability,t,t.Location,Core.TickCount),"Cancelled paid cast start");long at=ClassCombat.For(p).EffectAt;int paid=NecromancerEffects.ManaBalance256(p);ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,at)&&visits.Count==0&&NecromancerEffects.ManaBalance256(p)==paid&&Json(Own(p,item))==profile&&NpcWorldAllocation.Capture()==allocation,"Cancelled captured packet replayed/refunded/rolled proc");
        }
        finally{ClassCombat.Stop(p);PlayerItemProcWorld.Observed=observer;SorceressSpellMissiles.Cancel(p);t.Delete();}
    }
    private static void Poison(PlayerMobile p)
    {
        foreach(bool sourcePacket in new[]{false,true})
        {
            var t=HellslayerChecks.Target(p);var parts=new GearDamage(0,0,2,0,0);var raw=new GearDamage(0,0,2,0,10,4000,2000);
            if(sourcePacket){raw=raw with{SourcePoison=new(64,100)};}
            try
            {
                var impact=new PreparedWeaponImpact(raw,parts,0);var captured=DruidWeaponContact.Capture(p,t,true,false,impact,default,new Random(17));int life=SkillHealth.Balance256(t);
                DruidWeaponContact.Release(p,t,captured,TimedEffects.Now);
                var poison=TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value);
                Require(life-SkillHealth.Balance256(t)==512+(sourcePacket?64:0)&&poison.PoisonMs==4000&&Math.Abs(poison.PoisonRate-(sourcePacket?0.00625:0.0025))<1e-9&&poison.PoisonSource==p.Serial.Value&&poison.Cold!=null&&poison.ChillUntilUtc>TimedEffects.Now,"Captured cold/poison lost source/length/rate or fabricated direct poison damage");
                TimedEffects.ClearGear(t);Require(TimedEffects.Snapshot().All(x=>x.Owner!=t.Serial.Value||x.PoisonMs==0),"Captured poison survived the shared gear-effect cleanup");
            }
            finally{SorceressSpellMissiles.Cancel(p);t.Delete();}
        }
    }
    private static void Check(bool reload)
    {
        var saved=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):new Saved[Cases.Length];
        for(int i=0;i<Cases.Length;i++)
        {
            var c=Cases[i];PlayerMobile p;Item item;int generation=0;
            if(!reload){p=Owner(c.Ability,c.Form,2900+i*36,out item);}
            else
            {
                var s=saved[i];generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);item=World.FindItem((Serial)s.Item);
                Require(generation<=2&&s.Ability==c.Ability&&s.Form==c.Form&&item.Parent==p&&Json(ClassGear.State(item))==s.Gear&&Json(Own(p,item))==s.Profile&&Json(DruidPlayerForms.State(p))==s.Shape&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Saved captured-skill original/form/profile/wounds/payment changed");p.MoveToWorld(s.Point,Map.Trammel);
            }
            Play(p,item,c.Ability);Poison(p);DruidAttackCharges.Clear(p);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-257);DruidFormWorldChecks.RetainForInspection(p);var point=p.Location;p.Internalize();
            saved[i]=new(p.Serial.Value,item.Serial.Value,c.Ability,c.Form,point,Json(ClassGear.State(item)),Json(Own(p,item)),Json(DruidPlayerForms.State(p)),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),generation);
        }
        File.WriteAllText(Manifest,Json(saved));
    }
    private static void Input(NetState network,string label,CombatAbility ability,CombatAbility form)
    {
        var p=Owner(ability,form,3100,out var item);var t=HellslayerChecks.Target(p);var account=FixtureNetwork.Authenticate(network,p,"druid-captured-proc-");var rolls=new Rolls();
        var observer=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
        ulong world=NpcWorldAllocation.RequireStream().Value;ulong seed=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref world)));bool success=SourceRandom.Roll(ref seed)%100<10;if(success){SourceRandom.Roll(ref world);}
        int mana=NecromancerEffects.ManaBalance256(p);bool began=false;long deadline=Core.TickCount+5000;var shape=DruidPlayerForms.State(p);var bindings=SkillBindings.Instance.Get(p);
        void Cleanup(){ClassCombat.Stop(p);rolls.Dispose();PlayerItemProcWorld.Observed=observer;SorceressSpellMissiles.Cancel(p);DruidPlayerForms.Clear(p);t.Delete();item.Delete();p.Delete();account.Delete();}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Captured skill input timeout");var state=ClassCombat.For(p);
                if(!began&&Contact(p,ability)!=null){Require(!PlayerItemProcs.Initialized(p)&&visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,ability).Mana256,"Captured wire windup rolled proc/changed payment");began=true;Log.Information("{Label}: windup",label);}
                if(visits.Count<2){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(began&&visits[0].Phase=="domeleedamage"&&visits[0].Outcomes.Length==0&&visits[1].Phase=="domeleeattack"&&visits[1].Outcomes.Single().CastAttempts==(success?1:0)&&Own(p,item).Owners.Single().Seed==seed&&NpcWorldAllocation.Capture().Seed==world
                    &&NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,ability).Mana256&&state.PendingTarget==t.Serial&&ReferenceEquals(DruidPlayerForms.State(p),shape)&&SkillBindings.Instance.Get(p)==bindings,"Captured wire changed real chance,streams,shape,target,binding or payment");
                if(ability!=CombatAbility.FireClaws){Require(DruidAttackCharges.State(p,ability)?.Count==1,"Captured wire lost post-contact charge");}
                var flights=SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value).ToArray();Require(flights.Length==(success?1:0),"Captured wire allocation mismatch");if(success){HellslayerChecks.Finish(p,p,t,flights.Single());}Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Druid,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
