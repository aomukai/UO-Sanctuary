// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowTailChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowTailChecks));
    public sealed class Witness:GraveyardZombie
    {
        internal uint Actor;internal uint Selected;internal bool Flag;internal Action<int> Observe;
        public Witness(){Frozen=true;CantWalk=true;NoKillAwards=true;}public Witness(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool willKill)
        {Observe?.Invoke(amount);if(World.FindMobile((Serial)Actor)is AssassinShadowBody b&&ShadowActions.State(b)is{} a){Selected=OriginalNpcEquipment.WeaponFrame(b).Selected;Flag=a.Cast.ServerDo;}base.OnDamage(amount,from,willKill);}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);Require(reader.ReadInt()==0,"Tail witness version");}
    }
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,MartialCharge[] Charges);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-tail-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Clear(AssassinShadowBody b)=>ShadowDragonClawChecks.Clear(b);
    private static MartialCharge[] Charges(AssassinShadowBody b)=>AssassinCharges.Snapshot().Where(s=>s.Owner==b.Serial.Value).ToArray();
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),Charges(b));
    private static void Gain(AssassinShadowBody b,int id,DateTime at)
    {var v=ShadowSkillCasting.OwnValues(b,b.OwnedSkillRank(id),id);for(int i=0;i<3;i++){AssassinCharges.Gain(b,v,at);}}
    private static ulong Seed(bool hit)
    {for(int i=1;i<10000;i++){ulong s=SourceRandom.InitLow(i),v=s;uint n=SourceRandom.Limited(ref v,100);if(hit?n<5:n>=95){return s;}}throw new InvalidOperationException("Missing bounded hit seed");}
    private static Witness Target(AssassinShadowBody b,int dx=1,int dy=0,int life=65000)
    {var t=new Witness{Actor=b.Serial.Value};t.SetHits(life);foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}t.MoveToWorld(new(b.X+dx,b.Y+dy,b.Z),b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    public static void Configure(){Register("shadow-tail-check",false);Register("shadow-tail-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own NPC Tail cached kick,source physical copy/fire order and original world/saves",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Combat(AssassinShadowBody b,PlayerMobile p,int resistance,DateTime at)
    {
        Clear(b);var t=Target(b);var near=Target(b,2,1);var far=Target(b,6);t.SetResistance(ResistanceType.Physical,resistance);
        try
        {
            b.ChangeSkills(book=>book.Use(0));var ordinary=ShadowAnimation.Melee(b);b.ChangeSkills(book=>book.Use(null));var graph=b.EquipmentNumbers;int rate=graph.Read(68),lists=graph.SourceListOrder().Length;
            NpcEquipmentRandom.Set(b,Seed(true));int hp=SkillHealth.Balance256(t),nhp=SkillHealth.Balance256(near),fhp=SkillHealth.Balance256(far),mana=NecromancerEffects.ManaBalance256(p),own=NecromancerEffects.ManaBalance256(b);
            Require(ShadowActions.BeginDragonTail(b,t,at),"Own Tail startup refused");var a=ShadowActions.State(b);var cached=a.Cast.Tail;var v=ShadowSkillCasting.OwnValues(b,b.OwnedSkillRank(270),270);
            int physical=cached.Impact.Packet.Physical256,fire=DragonKickPackets.TailFire256(physical,v,graph.Read(329));
            Require(resistance!=100||physical==0&&fire==0,"Physical immunity did not suppress copied Tail fire");
            Require(graph.Read(333)==0&&graph.Read(68)==rate&&graph.SourceListOrder().Length==lists&&a.Plan.Speed<=ordinary.Speed
                &&SkillHealth.Balance256(t)==hp&&SkillHealth.Balance256(near)==nhp,"Tail leaked speed modifier,list,owner fire pierce or early damage");
            int marker=a.Plan.Events.Single(e=>e.Type==1).Frame,end=a.Plan.Events.Single(e=>e.Type==2).Frame;var rng=NpcEquipmentRandom.Require(b);
            ShadowActions.Tick(at.AddMilliseconds((marker-1)*40),true,b.Serial.Value);Require(!cached.Consumed&&NpcEquipmentRandom.Require(b)==rng&&SkillHealth.Balance256(near)==nhp,"Tail delivered/rolled before source marker");
            var deliveries=new List<(uint Target,int Amount)>();t.Observe=n=>deliveries.Add((t.Serial.Value,n));near.Observe=n=>deliveries.Add((near.Serial.Value,n));
            int parentAmount=PreparedMonsterCredit.Amount(t,hp-cached.Impact.Packet.Total256);
            ShadowActions.Tick(at.AddMilliseconds(marker*40),true,b.Serial.Value);
            if(parentAmount>0&&fire>=256)
            {Require(deliveries.Count==3&&deliveries[0]==(t.Serial.Value,parentAmount)&&deliveries.Skip(1).Any(d=>d.Target==near.Serial.Value),"Native damage notifications did not deliver parent before Tail fire");}
            Require(cached.Consumed&&a.Cast.Tail==null&&SkillHealth.Balance256(near)==nhp-fire&&SkillHealth.Balance256(t)==hp-cached.Impact.Packet.Total256-fire
                &&SkillHealth.Balance256(far)==fhp&&SourceKnockbacks.Remaining(near)>0,"Tail copied wrong physical lane,ordered parent/fire incorrectly or lost zero-fire knockback/radius");
            if(physical>0){Require(t.Flag&&t.Selected==0,"Tail lost progressive flag or original weapsel4");}
            int after=SkillHealth.Balance256(near);Require(ShadowSkillCasting.Release(a.Cast,at)==0,"Tail release replayed");ShadowActions.Tick(at.AddMilliseconds(end*40),true,b.Serial.Value);
            Require(ShadowActions.State(b)==null&&b.OwnedSkills.Used==null&&graph.Read(68)==rate&&graph.SourceListOrder().Length==lists
                &&NecromancerEffects.ManaBalance256(p)==mana&&NecromancerEffects.ManaBalance256(b)==own&&SkillHealth.Balance256(near)==after,"Tail retained action/rate/list,debit or duplicate burst");
        }
        finally{foreach(var target in new[]{t,near,far}){SourceKnockbacks.Clear(target);target.Delete();}Clear(b);}
    }
    private static void Failures(AssassinShadowBody b,PlayerMobile p,DateTime at)
    {
        Clear(b);var t=Target(b);try
        {
            int rate=b.EquipmentNumbers.Read(68),hp=SkillHealth.Balance256(t);ulong seed=Seed(false),expected=seed;SourceRandom.Limited(ref expected,100);NpcEquipmentRandom.Set(b,seed);
            Require(!ShadowActions.BeginDragonTail(b,t,at)&&NpcEquipmentRandom.Require(b).Seed==expected&&ShadowActions.State(b)==null&&b.OwnedSkills.Used==null
                &&b.EquipmentNumbers.Read(68)==rate&&SkillHealth.Balance256(t)==hp,"Source start27miss allocated or left action/speed/skill");
        }
        finally{Clear(b);t.Delete();}
        var choice=SourceBlocking.ChoiceRoll;var roll=SourceBlocking.BlockRoll;BaseCreature shield=null;
        try
        {
            SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>0;shield=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};shield.SetHits(65000);shield.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);shield.AIObject.AITimer.Stop();Gain(b,254,at);var rng=NpcEquipmentRandom.Require(b);
            Require(!ShadowActions.BeginDragonTail(b,shield,at)&&NpcEquipmentRandom.Require(b)==rng&&AssassinCharges.State(b)?.Count==3&&ShadowActions.State(b)==null,"Charged Tail bypassed shield,rolled accuracy or consumed on refused startup");
        }
        finally{SourceBlocking.ChoiceRoll=choice;SourceBlocking.BlockRoll=roll;Clear(b);shield?.Delete();}
        for(int reason=0;reason<5;reason++)
        {
            t=Target(b);var position=b.Location;try
            {
                Gain(b,254,at);Require(ShadowActions.BeginDragonTail(b,t,at),"Tail interruption startup refused");var a=ShadowActions.State(b);var cached=a.Cast.Tail;int hp=SkillHealth.Balance256(t),rate=b.EquipmentNumbers.Read(68);var rng=NpcEquipmentRandom.Require(b);string charges=Json(Charges(b));
                if(reason==0){b.InterruptForRecovery();}if(reason==1){b.Frozen=true;}if(reason==2){b.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);}if(reason==3){PersistentMinions.Park(b);}
                ShadowActions.Tick(at.AddSeconds(3),reason!=4,b.Serial.Value);
                Require(cached.Consumed&&ShadowActions.State(b)==null&&a.Cast.Tail==null&&NpcEquipmentRandom.Require(b)==rng&&Json(Charges(b))==charges&&SkillHealth.Balance256(t)==hp&&b.EquipmentNumbers.Read(68)==rate,"Interrupted Tail delivered,replayed or leaked modifier");
                if(reason==1){b.Frozen=false;}b.MoveToWorld(position,p.Map);
            }
            finally{Clear(b);t.Delete();}at=at.AddSeconds(4);
        }
    }
    private static void Elements(AssassinShadowBody b,DateTime at)
    {
        Clear(b);var t=Target(b);var near=Target(b,2,1);
        try
        {
            foreach(int id in new[]{254,265,280,259,274,269}){Gain(b,id,at);}Require(ShadowActions.BeginDragonTail(b,t,at),"Six-family Tail startup refused");var a=ShadowActions.State(b);int marker=a.Plan.Events.Single(e=>e.Type==1).Frame;
            Require(Charges(b).All(s=>s.Count==3)&&a.Cast.Tail.Impact.Packet.Physical256>0,"Tail consumed at startup or lost Tiger/Fists physical composition");
            int hp=SkillHealth.Balance256(near);ShadowActions.Tick(at.AddMilliseconds(marker*40),true,b.Serial.Value);
            Require(Charges(b).Length==6&&Charges(b).All(s=>s.Count==2)&&SkillHealth.Balance256(near)<hp
                &&MartialCold.Snapshot().Cubes.Any(c=>c.Npc?.Actor==b.Serial.Value)&&FireGround.Snapshot().Patches.Any(c=>c.PhoenixNpc?.Actor==b.Serial.Value)
                &&MartialLightning.Snapshot().Flights.Any(c=>c.Npc?.Actor==b.Serial.Value),"Tail omitted shared six-family callbacks/consume-one or fire burst");
        }
        finally{Clear(b);foreach(var target in new[]{t,near}){SourceKnockbacks.Clear(target);target.Delete();}}
    }
    private static void WorldAi(AssassinShadowBody b,PlayerMobile p,DateTime at)
    {
        Clear(b);var t=Target(b);try
        {
            Gain(b,254,at);int hp=SkillHealth.Balance256(t);int duration=((INecromancerGroundBrain)b).GroundSkill(p,4,270,t,at);Require(duration>0,"Own Tail world request refused");ShadowActions.Tick(at.AddMilliseconds(duration*40),true,b.Serial.Value);Require(SkillHealth.Balance256(t)<hp&&ShadowActions.State(b)==null,"Own Tail world action did no damage");Clear(b);SourceKnockbacks.Clear(t);
            if(b.Prepared.Capture.Ability==CombatAbility.ShadowWarrior)
            {
                if(!ShadowGroundMotion.Active(b)){ShadowGroundMotion.Activate(b,p,at.AddMinutes(1));}Require(SkillBindings.Instance.Assign(p,true,CombatAbility.DragonTail)&&SkillBindings.Instance.Assign(p,false,CombatAbility.DragonTail),"Warrior Tail bindings refused");
                hp=SkillHealth.Balance256(t);NpcEquipmentRandom.Set(b,Seed(true));bool selected=false;
                for(int i=0;i<800&&!(selected&&SkillHealth.Balance256(t)<hp);i++){IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);Require(b.WorldGround.State.Controller.Fault==null,"Natural Tail AI fault: "+b.WorldGround.State.Controller.Fault);selected|=ShadowActions.State(b)?.Cast.Skill==270;}
                Require(selected&&SkillHealth.Balance256(t)<hp,"Natural Warrior AI did not use Tail");Clear(b);
            }
        }
        finally{Clear(b);SourceKnockbacks.Clear(t);t.Delete();}
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);bodies=s.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(s.Bodies[i])&&ShadowActions.State(bodies[i])==null,"Reload changed original Tail identity/items/resources/wounds/RNG/charges or replayed windup");}p.MoveToWorld(new(6350,3510,0),Map.Trammel);
        }
        else
        {
            p=BladeBodyChecks.Fresh(6350,20,3510);ClassSkillChecks.Train(p,"assassin.shadowwarrior",20);ClassSkillChecks.Train(p,"assassin.shadowmaster",20);var world=NpcWorldAllocation.Capture();bodies=new AssassinShadowBody[2];
            try{int i=0;foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}){NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));bodies[i]=AssassinShadowBody.CreateInitialized(p,ability,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(bodies[i]);ShadowEquipmentDefense.Activate(bodies[i]);i++;}}finally{NpcWorldAllocation.Restore(world);}
        }
        foreach(var b in bodies)
        {
            b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);foreach(int id in new[]{270,254,265,280,259,274,269}){b.ChangeSkills(book=>book.Assign(id,3));}NecromancerEffects.SetMana256(p,0);var at=TimedEffects.Now.AddMinutes(15+generation);
            foreach(int resist in new[]{0,50,100}){Combat(b,p,resist,at);at=at.AddSeconds(3);}Failures(b,p,at);Elements(b,at.AddMinutes(1));WorldAi(b,p,at.AddMinutes(2));
            Clear(b);var pending=Target(b);Gain(b,254,at.AddMinutes(3));Require(ShadowActions.BeginDragonTail(b,pending,at.AddMinutes(3)),"Pending-save Tail entry refused");pending.Delete();SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);PersistentMinions.Park(b);
        }
        GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(p);p.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,bodies.Select(Capture).ToArray(),generation)));

        Log.Information("Own NPC Tail:original startup27cache,action-only speed penalty,shared kick/accuracy/block,mitigated physical copy before charge/contact,fire after parent incl immunity/zero-fire knockback,all-six release/consume-one,world/natural Warrior AI,cancel/no replay and original saves through two reloads;modern NPC boundaries remain scoped.");
    }
}
