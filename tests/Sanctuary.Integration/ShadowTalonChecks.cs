// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowTalonChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowTalonChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,MartialCharge[] Charges);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-talon-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Clear(AssassinShadowBody b)=>ShadowDragonClawChecks.Clear(b);
    private static void Gain(AssassinShadowBody b,int id,int n,DateTime at)
    {var v=ShadowSkillCasting.OwnValues(b,b.OwnedSkillRank(id),id);for(int i=0;i<n;i++){AssassinCharges.Gain(b,v,at);}}
    private static MartialCharge[] Charges(AssassinShadowBody b)=>AssassinCharges.Snapshot().Where(s=>s.Owner==b.Serial.Value).ToArray();
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),Charges(b));
    private static ShadowDragonClawChecks.Witness Target(AssassinShadowBody b,int life=65000)
    {var t=new ShadowDragonClawChecks.Witness{Actor=b.Serial.Value};t.SetHits(life);foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}t.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static ulong HitSeed()
    {for(int i=1;i<10000;i++){ulong s=SourceRandom.InitLow(i),v=s;if(SourceRandom.Limited(ref v,100)<5){return s;}}throw new InvalidOperationException("No bounded hit seed");}
    public static void Configure(){Register("shadow-talon-check",false);Register("shadow-talon-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own NPC Talon startup cache,do42 repeats,charges,world/AI and original saves",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Source()
    {
        using var rewind=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-kick-rewind-cases.json")));int count=0;
        foreach(var c in rewind.RootElement.EnumerateArray())
        {
            int I(string n)=>c.GetProperty(n).GetInt32();var flags=new int[13];flags[4]=1;
            var actual=SourceAnimationEvents.Percent(new((200-I("Elapsed"))*256,I("Speed"),13*256,flags,[]),200,I("Percent"),I("Prefix"));
            Require(actual.Epoch256==I("Epoch256")&&Json(actual.Events.Select(e=>new[]{e.Type,e.Frame,e.Kind}))==c.GetProperty("Events").GetRawText(),"Complete ordinary percent rewind differs");count++;
        }
        Require(count==46,"Missing complete percent-rewind controls");
        using var sequences=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-talon-cases.json")));count=0;
        foreach(var c in sequences.RootElement.EnumerateArray())
        {
            int speed=c.GetProperty("Speed").GetInt32(),rank=c.GetProperty("Rank").GetInt32();var flags=new int[13];flags[4]=1;
            var initial=SourceAnimationEvents.Initial(new(0,speed,13*256,flags,[]),0);var plan=new ShadowAnimationPlan(speed,initial.Events);
            Require(ShadowDragonTalon.Duration(plan,rank/6+1)==c.GetProperty("End").GetInt32(),"NPC series occupancy differs from four complete source routines");int rewinds=0;
            foreach(var row in c.GetProperty("Trace").EnumerateArray())
            {
                int frame=row[0].GetInt32();Require(plan.Events.Any(e=>e.Type==1&&e.Kind==1&&e.Frame==frame),"NPC callback marker differs from complete source");
                if(row[4].GetInt32()>rewinds){plan=ShadowAnimation.RewindTalon(plan,frame);rewinds++;}
            }
            Require(plan.Events.Single(e=>e.Type==2).Frame==c.GetProperty("End").GetInt32(),"NPC final recovery lost full rewind clock");count++;
        }
        Require(count==20,"Missing composed NPC start/do/clock controls");
    }
    private static void Series(AssassinShadowBody b,PlayerMobile p,int rank,DateTime at)
    {
        Clear(b);b.ChangeSkills(book=>book.Assign(255,rank));var t=Target(b);
        try
        {
            Gain(b,254,3,at);int hp=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),petMana=NecromancerEffects.ManaBalance256(b);var world=NpcWorldAllocation.Capture();
            Require(ShadowActions.BeginDragonTalon(b,t,at),"Own Talon start refused");var action=ShadowActions.State(b);var cast=action.Cast;var cached=cast.Talon.Cached;int kicks=rank/6+1,end=ShadowDragonTalon.Duration(action.Plan,kicks),contacts=0;
            Require(cached!=null&&!cached.Consumed&&SkillHealth.Balance256(t)==hp&&AssassinCharges.State(b).Count==3
                &&NpcWorldAllocation.Capture()==world&&ShadowSkillCasting.Release(cast,at)==0,"Talon startup failed cache/no delivery/no one-shot contract");
            for(int frame=1;frame<=end;frame++)
            {
                var current=ShadowActions.State(b);bool marker=current.Plan.Events.Any(e=>e.Type==1&&e.Frame==frame);int before=SkillHealth.Balance256(t),seen=t.Notifications;var rng=NpcEquipmentRandom.Require(b);string charges=Json(Charges(b));
                int expected=marker?(cast.Talon.Cached!=null?1:0)+(cast.Talon.Remaining>0?1:0):0;
                ShadowActions.Tick(at.AddMilliseconds(frame*40),true,b.Serial.Value);
                if(marker)
                {
                    contacts+=expected;Require(t.Notifications-seen==expected&&SkillHealth.Balance256(t)<before&&t.Flag&&t.Selected==0,"NPC cached/follow-up kick count,progressive flag or weapsel4 changed");
                    Require((AssassinCharges.State(b)?.Count??0)==3-contacts&&cached.Consumed,"Talon did not consume one Tiger charge per successful contact");
                    Require(ShadowSkillCasting.ReleaseTalon(cast,frame,at)==0,"Duplicate Talon callback replayed");
                }
                else{Require(SkillHealth.Balance256(t)==before&&NpcEquipmentRandom.Require(b)==rng&&Json(Charges(b))==charges,"Non-marker Talon frame mutated HP/RNG/charges");}
            }
            Require(contacts==kicks&&cast.Released&&cast.Talon==null&&ShadowActions.State(b)==null&&b.OwnedSkills.Used==null
                &&NecromancerEffects.ManaBalance256(p)==mana&&NecromancerEffects.ManaBalance256(b)==petMana&&SourceKnockbacks.Remaining(t)>0,"Talon end/recovery/knockback/free NPC startup differs");
        }
        finally{SourceKnockbacks.Clear(t);Clear(b);t.Delete();}
    }
    private static void Elements(AssassinShadowBody b,PlayerMobile p,DateTime at)
    {
        Clear(b);b.ChangeSkills(book=>book.Assign(255,12));var t=Target(b);
        try
        {
            foreach(int id in new[]{254,265,280,259,274,269}){Gain(b,id,3,at);}var states=ShadowFinisherDamage.States(b,t,at);var rng=NpcEquipmentRandom.Require(b);
            _=ShadowFinisherDamage.Effects(b,states);Require(NpcEquipmentRandom.Require(b)==rng,"Live release context drew unused inline RNG");
            var world=NpcWorldAllocation.Capture();Require(ShadowActions.BeginDragonTalon(b,t,at),"All-six Talon refused");var a=ShadowActions.State(b);int first=a.Plan.Events.Single(e=>e.Type==1).Frame;
            Require(NpcWorldAllocation.Capture()==world&&Charges(b).All(s=>s.Count==3),"Talon emitted or consumed during cache allocation");ShadowActions.Tick(at.AddMilliseconds(first*40),true,b.Serial.Value);
            Require(Charges(b).Length==6&&Charges(b).All(s=>s.Count==1)&&MartialCold.Snapshot().Cubes.Any(c=>c.Npc?.Actor==b.Serial.Value)
                &&FireGround.Snapshot().Patches.Any(c=>c.PhoenixNpc?.Actor==b.Serial.Value)&&MartialLightning.Snapshot().Flights.Any(c=>c.Npc?.Actor==b.Serial.Value),"Talon omitted ordered six-family emissions or double-contact consumption");
            int end=ShadowActions.State(b).Plan.Events.Single(e=>e.Type==2).Frame;ShadowActions.Tick(at.AddMilliseconds(end*40),true,b.Serial.Value);Require(Charges(b).Length==0,"Talon last kick retained charges");
        }
        finally{SourceKnockbacks.Clear(t);Clear(b);t.Delete();}
    }
    private static void Cancellation(AssassinShadowBody b,PlayerMobile p,DateTime at)
    {
        for(int reason=0;reason<8;reason++)
        {
            Clear(b);var t=Target(b);var position=b.Location;try
            {
                Gain(b,254,3,at);Require(ShadowActions.BeginDragonTalon(b,t,at),"Talon cancellation startup refused");var a=ShadowActions.State(b);var cached=a.Cast.Talon.Cached;
                if(reason==0){t.MoveToWorld(new(b.X+12,b.Y,b.Z),b.Map);}if(reason==1){b.InterruptForRecovery();}if(reason==2){b.Frozen=true;}if(reason==3){t.Delete();}
                if(reason==4){b.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);}if(reason==5){GeneratedNpcLiveResources.SetItemActive(b,ShadowLoadout.Binding(b).Primary,false);}if(reason==6){PersistentMinions.Park(b);}
                var rng=NpcEquipmentRandom.Require(b);string states=Json(Charges(b));int hp=t.Deleted?0:SkillHealth.Balance256(t);ShadowActions.Tick(at.AddSeconds(3),reason!=7,b.Serial.Value);
                Require(ShadowActions.State(b)==null&&cached.Consumed&&a.Cast.Talon==null&&NpcEquipmentRandom.Require(b)==rng&&Json(Charges(b))==states
                    &&(t.Deleted||SkillHealth.Balance256(t)==hp)&&ShadowSkillCasting.ReleaseTalon(a.Cast,100,at)==0,"Canceled Talon delivered cached packet or replayed windup");
                if(reason==2){b.Frozen=false;}if(reason==5){GeneratedNpcLiveResources.SetItemActive(b,ShadowLoadout.Binding(b).Primary,true);}b.MoveToWorld(position,p.Map);
            }
            finally{Clear(b);t.Delete();}at=at.AddSeconds(4);
        }
    }
    private static void Edges(AssassinShadowBody b,PlayerMobile p,DateTime at)
    {
        Clear(b);b.ChangeSkills(book=>book.Assign(255,12));var choice=SourceBlocking.ChoiceRoll;var roll=SourceBlocking.BlockRoll;BaseCreature shield=null;
        try
        {
            SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>0;shield=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};shield.SetHits(65000);shield.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);shield.AIObject.AITimer.Stop();
            Require(SourceBlocking.TryRate(shield,out int rate,out _)&&rate>0,"Talon mapped shield witness missing");Gain(b,254,3,at);var rng=NpcEquipmentRandom.Require(b);int hp=SkillHealth.Balance256(shield);
            Require(ShadowActions.BeginDragonTalon(b,shield,at)&&NpcEquipmentRandom.Require(b)==rng,"Charged blocked startup rolled accuracy or filled damage");var a=ShadowActions.State(b);int end=ShadowDragonTalon.Duration(a.Plan,3);
            ShadowActions.Tick(at.AddMilliseconds(end*40),true,b.Serial.Value);Require(SkillHealth.Balance256(shield)==hp&&AssassinCharges.State(b)?.Count==3,"Charged Talon bypassed shield or consumed on block");
        }
        finally{SourceBlocking.ChoiceRoll=choice;SourceBlocking.BlockRoll=roll;Clear(b);shield?.Delete();}
        var t=Target(b);try
        {
            foreach(int id in new[]{254,265,280,259,274,269}){Gain(b,id,3,at);}Require(ShadowActions.BeginDragonTalon(b,t,at),"Expiry Talon start refused");var a=ShadowActions.State(b);int first=a.Plan.Events.Single(e=>e.Type==1).Frame;
            var cached=a.Cast.Talon.Cached;int hp=SkillHealth.Balance256(t);Require(cached.Impact.Packet.FreezeFrames>0,"Cached Ice freeze witness missing");
            AssassinCharges.Restore(AssassinCharges.Snapshot().Select(s=>s.Owner==b.Serial.Value?s with{UntilUtc=at.AddMilliseconds(first*40-1)}:s).ToArray());var world=NpcWorldAllocation.Capture();
            ShadowActions.Tick(at.AddMilliseconds(first*40),true,b.Serial.Value);
            Require(SkillHealth.Balance256(t)<hp&&cached.Consumed&&NpcWorldAllocation.Capture()==world&&Charges(b).All(s=>s.Count==3)
                &&!MartialCold.Snapshot().Cubes.Any(c=>c.Npc?.Actor==b.Serial.Value)&&!FireGround.Snapshot().Patches.Any(c=>c.PhoenixNpc?.Actor==b.Serial.Value)
                &&!MartialLightning.Snapshot().Flights.Any(c=>c.Npc?.Actor==b.Serial.Value),"Expired charge emitted/consumed or erased already-cached kick damage");
        }
        finally{Clear(b);t.Delete();}
        t=Target(b,1);try
        {
            Gain(b,254,3,at);Require(ShadowActions.BeginDragonTalon(b,t,at),"Lethal Talon start refused");var a=ShadowActions.State(b);
            ShadowActions.Tick(at.AddSeconds(3),true,b.Serial.Value);Require(t.Notifications==1&&AssassinCharges.State(b)?.Count==2&&ShadowActions.State(b)==null,"Target death released remaining kicks or consumed extra charges");
        }
        finally{Clear(b);t.Corpse?.Delete();t.Delete();}
    }
    private static void WorldAi(AssassinShadowBody b,PlayerMobile p,DateTime at)
    {
        Clear(b);b.ChangeSkills(book=>book.Assign(255,12));var t=Target(b);
        try
        {
            Gain(b,254,3,at);int hp=SkillHealth.Balance256(t);int duration=((INecromancerGroundBrain)b).GroundSkill(p,4,255,t,at);
            Require(duration==ShadowDragonTalon.Duration(ShadowActions.State(b).Plan,3),"World request released its series occupancy early");ShadowActions.Tick(at.AddMilliseconds(duration*40),true,b.Serial.Value);Require(SkillHealth.Balance256(t)<hp&&ShadowActions.State(b)==null,"Own world Talon did no damage or retained action");Clear(b);SourceKnockbacks.Clear(t);
            if(b.Prepared.Capture.Ability==CombatAbility.ShadowWarrior)
            {
                if(!ShadowGroundMotion.Active(b)){ShadowGroundMotion.Activate(b,p,at.AddMinutes(1));}Require(SkillBindings.Instance.Assign(p,true,CombatAbility.DragonTalon)&&SkillBindings.Instance.Assign(p,false,CombatAbility.DragonTalon),"Warrior Talon binding refused");
                hp=SkillHealth.Balance256(t);NpcEquipmentRandom.Set(b,HitSeed());bool selected=false;
                for(int i=0;i<800&&!(selected&&SkillHealth.Balance256(t)<hp);i++){IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);Require(b.WorldGround.State.Controller.Fault==null,"Natural Talon AI fault: "+b.WorldGround.State.Controller.Fault);selected|=ShadowActions.State(b)?.Cast.Skill==255;}
                Require(selected&&SkillHealth.Balance256(t)<hp,"Natural Warrior AI failed Talon selection/contact");Clear(b);
            }
        }
        finally{SourceKnockbacks.Clear(t);Clear(b);t.Delete();}
    }
    private static void Check(bool reload)
    {
        Source();PlayerMobile p;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(saved.Bodies[i])&&ShadowActions.State(bodies[i])==null,"Reload changed original Talon actor/items/resources/wounds/RNG/charges or replayed windup");}p.MoveToWorld(new(6310,3510,0),Map.Trammel);
        }
        else
        {
            p=BladeBodyChecks.Fresh(6310,20,3510);ClassSkillChecks.Train(p,"assassin.shadowwarrior",20);ClassSkillChecks.Train(p,"assassin.shadowmaster",20);var world=NpcWorldAllocation.Capture();bodies=new AssassinShadowBody[2];
            try{int i=0;foreach(var a in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}){NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));bodies[i]=AssassinShadowBody.CreateInitialized(p,a,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(bodies[i]);ShadowEquipmentDefense.Activate(bodies[i]);i++;}}finally{NpcWorldAllocation.Restore(world);}
        }
        foreach(var b in bodies)
        {
            b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);foreach(int id in new[]{255,254,265,280,259,274,269}){b.ChangeSkills(book=>book.Assign(id,3));}Clear(b);NecromancerEffects.SetMana256(p,0);var at=TimedEffects.Now.AddMinutes(15+generation);
            var far=Target(b);far.MoveToWorld(new(b.X+12,b.Y,b.Z),b.Map);var seed=NpcEquipmentRandom.Require(b);Require(!ShadowActions.BeginDragonTalon(b,far,at)&&NpcEquipmentRandom.Require(b)==seed&&b.OwnedSkills.Used==null,"Distant Talon startup drew RNG/retained skill");far.Delete();
            foreach(int rank in new[]{1,6,12}){Series(b,p,rank,at);at=at.AddSeconds(3);}Edges(b,p,at);Elements(b,p,at.AddSeconds(3));Cancellation(b,p,at.AddSeconds(6));WorldAi(b,p,at.AddMinutes(1));
            Clear(b);var pending=Target(b);Gain(b,254,3,at);Require(ShadowActions.BeginDragonTalon(b,pending,at.AddMinutes(3)),"Pending-save Talon entry refused");pending.Delete();SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);PersistentMinions.Park(b);
        }
        GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(p);p.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,bodies.Select(Capture).ToArray(),generation)));

        Log.Information("Own NPC Talon:20composed source/46rewind controls,original startup cache,do42immediate follow-up/repeats/full recovery,approved per-contact charge/accuracy policy,all-six emissions,knockback,original hands/no NPC mana,world/natural Warrior AI,cancel/no replay and original saves;modern NPC boundaries remain scoped.");
    }
}
