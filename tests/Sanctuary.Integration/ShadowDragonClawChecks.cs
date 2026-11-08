// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowDragonClawChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowDragonClawChecks));
    public sealed class Witness:GraveyardZombie
    {
        internal uint Actor;internal uint Selected;internal bool Flag;internal int Notifications;
        public Witness(){Frozen=true;CantWalk=true;NoKillAwards=true;}public Witness(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool willKill)
        {if(World.FindMobile((Serial)Actor)is AssassinShadowBody b&&ShadowActions.State(b)is{} action){Notifications++;Selected=OriginalNpcEquipment.WeaponFrame(b).Selected;Flag=action.Cast.ServerDo;}base.OnDamage(amount,from,willKill);}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);Require(reader.ReadInt()==0,"Witness version");}
    }
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,MartialCharge[] Charges);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-dragon-claw-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static MartialCharge[] Charges(AssassinShadowBody b)=>AssassinCharges.Snapshot().Where(c=>c.Owner==b.Serial.Value).ToArray();
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),Charges(b));
    public static void Configure(){Register("shadow-dragon-claw-check",false);Register("shadow-dragon-claw-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own NPC Dragon Claw mode14/start25/do46 original contacts and saves",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    internal static void Clear(AssassinShadowBody b)
    {
        ShadowActions.Cancel(b);AssassinCharges.Clear(b);var c=MartialCold.Snapshot();MartialCold.Restore(c with{Cubes=c.Cubes.Where(x=>x.Npc?.Actor!=b.Serial.Value).ToArray()});
        var f=FireGround.Snapshot();FireGround.Restore(f with{Patches=f.Patches.Where(x=>x.PhoenixNpc?.Actor!=b.Serial.Value).ToArray()});
        var t=MartialLightning.Snapshot();var ids=t.Flights.Where(x=>x.Npc?.Actor==b.Serial.Value).Select(x=>x.Cast).ToHashSet();MartialLightning.Restore(t with{Flights=t.Flights.Where(x=>x.Npc?.Actor!=b.Serial.Value).ToArray(),Leases=t.Leases.Where(x=>!ids.Contains(x.Cast)).ToArray()});
        var p=MartialPhoenix.Snapshot();ids=p.Flights.Where(x=>x.Npc?.Actor==b.Serial.Value).Select(x=>x.Cast).ToHashSet();MartialPhoenix.Restore(p with{Drops=p.Drops.Where(x=>x.Npc?.Actor!=b.Serial.Value).ToArray(),Flights=p.Flights.Where(x=>x.Npc?.Actor!=b.Serial.Value).ToArray(),Leases=p.Leases.Where(x=>!ids.Contains(x.Cast)).ToArray()});
    }
    private static ulong Seed(bool hit)
    {for(int i=1;i<10000;i++){ulong s=SourceRandom.InitLow(i),c=s;uint n=SourceRandom.Limited(ref c,100);if(hit?n<5:n>=95){return s;}}throw new InvalidOperationException("Missing bounded hit seed");}
    private static void Gain(AssassinShadowBody b,int id,int n,DateTime at)
    {var v=ShadowSkillCasting.OwnValues(b,b.OwnedSkillRank(id),id);for(int i=0;i<n;i++){AssassinCharges.Gain(b,v,at);}}
    private static Witness Target(AssassinShadowBody b,int life=65000)
    {var t=new Witness{Actor=b.Serial.Value};t.SetHits(life);foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}t.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-dragon-claw-cases.json")));int count=0;
        foreach(var c in doc.RootElement.EnumerateArray())
        {
            int speed=c.GetProperty("Speed").GetInt32();bool present=c.GetProperty("Target").GetInt32()!=0;Require(c.GetProperty("Started").GetInt32()==(present?1:0),"Whole start25 changed target-only condition");
            var state=ShadowClawSequence.Initial();int calls=0,tick=0;
            foreach(var r in c.GetProperty("Trace").EnumerateArray())
            {
                bool end=ShadowClawSequence.Complete(state),release=ShadowClawSequence.Releases(state);int hand=0,inside=0;bool flag=state.ServerDo;
                if(release&&present){hand=((state.Clock.Point>>8)&1)==0?1:2;calls++;flag=hand==2;}
                var next=end?state:new ShadowClawSequenceState(AssassinSentrySequence.AdvanceIn(state.Clock,ShadowClawSequence.Points,new[]{6,10},speed),flag);
                int[] actual=[++tick,state.Clock.Point,state.Clock.Remaining,state.Clock.Action,state.ServerDo?1:0,end?2:1,next.Clock.Point,next.Clock.Remaining,next.Clock.Action,next.ServerDo?1:0,hand,inside,inside,calls,calls];
                Require(actual.SequenceEqual(r.EnumerateArray().Select(x=>x.GetInt32())),"Whole NPC callback/do46 flag/hand trace differs");state=next;
            }
            count++;
        }
        Require(count==24,"Missing new NPC do46 source contexts");
    }
    private static void Run(AssassinShadowBody b,PlayerMobile owner,DateTime at,bool charged,bool hit)
    {
        Clear(b);var target=Target(b);try
        {
            if(charged){foreach(int id in new[]{254,265,280,259,274,269}){Gain(b,id,3,at);}}
            int mana=NecromancerEffects.ManaBalance256(owner),ownMana=NecromancerEffects.ManaBalance256(b),releases=0;
            NpcEquipmentRandom.Set(b,Seed(hit));var before=NpcEquipmentRandom.Require(b);Require(ShadowActions.BeginDragonClaw(b,target,at),"Own Dragon Claw entry refused");var cast=ShadowActions.State(b).Cast;var plan=ShadowActions.State(b).Plan;
            Require(NpcEquipmentRandom.Require(b)==before&&!cast.Released&&ShadowSkillCasting.Release(cast,at)==0,"Dragon entry drew RNG or allowed one-shot release");
            int end=plan.Events.Single(e=>e.Type==2).Frame;
            for(int frame=1;frame<=end;frame++)
            {
                var a=ShadowActions.State(b);bool release=a?.Sequence is{} seq&&ShadowClawSequence.Releases(seq);int hp=SkillHealth.Balance256(target),oldNotify=target.Notifications,point=a?.Sequence?.Clock.Point??0;
                var slots=Charges(b);NpcEquipmentRandom.Set(b,Seed(hit));var rng=NpcEquipmentRandom.Require(b);int expectedLoss=0;
                uint expectedHand=release?OriginalNpcEquipment.WeaponFrame(b).Selected:0;
                if(release&&!charged&&hit)
                {
                    // Independent composition against the already-qualified
                    // full original filler, with table formula inputs supplied.
                    int rank=b.OwnedSkillRank(260),hard=b.OwnedSkills.Find(252).Base,ed=50+15*(rank-1)+4*hard;ulong s=rng.Seed;SourceRandom.Limited(ref s,100);NpcEquipmentRandom.Set(b,s);
                    expectedLoss=OriginalNpcMelee.Impact(b,target,5,new(AdditionalPercent:ed,HitFlags:2)).Packet.Total256;NpcEquipmentRandom.Set(b,rng.Seed);
                }
                ShadowActions.Tick(at.AddMilliseconds(frame*40),true,b.Serial.Value);
                if(release)
                {
                    releases++;bool success=charged||hit;
                    Require(success?SkillHealth.Balance256(target)<hp&&target.Notifications>oldNotify:SkillHealth.Balance256(target)==hp&&target.Notifications==oldNotify,"Dragon charged guarantee/unfunded NPC/hit/miss failed");
                    if(success)
                    {
                        Require(target.Selected==expectedHand&&target.Flag,"Dragon hand selection or during-contact server-do flag changed");
                        if(!charged){Require(hp-SkillHealth.Balance256(target)==expectedLoss,"Dragon skill ED/hard mastery/source128 differs from full original allocator");}
                    }
                    if(charged){Require(Charges(b).Length==6&&Charges(b).All(s=>s.Count==3-releases),"Dragon failed per-contact all-six consume-one");}
                    else{Require(Charges(b).Length==0,"Uncharged Dragon gained a progressive state");}
                    Require(ShadowSkillCasting.ReleaseSequence(cast,point,at)==0,"Duplicate Dragon sequence point replayed");
                }
                else{Require(SkillHealth.Balance256(target)==hp&&Json(Charges(b))==Json(slots)&&NpcEquipmentRandom.Require(b)==rng,"Non-release Dragon frame mutated HP/charges/RNG");}
            }
            Require(releases==plan.Events.Count(e=>e.Type==1)&&cast.Released&&ShadowActions.State(b)==null&&b.OwnedSkills.Used==null
                &&NecromancerEffects.ManaBalance256(owner)==mana&&NecromancerEffects.ManaBalance256(b)==ownMana,"Dragon failed end/no-NPC-mana/duplicate guards");
        }
        finally{Clear(b);target.Delete();}
    }
    private static void Block(AssassinShadowBody b,PlayerMobile owner,DateTime at)
    {
        Clear(b);var choice=SourceBlocking.ChoiceRoll;var roll=SourceBlocking.BlockRoll;BaseCreature target=null;
        try
        {
            SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>0;target=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};target.SetHits(65000);target.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);target.AIObject.AITimer.Stop();
            Require(SourceBlocking.TryRate(target,out int rate,out _)&&rate>0,"Mapped skeleton shield fixture missing");Gain(b,254,3,at);var slots=Json(Charges(b));int hp=SkillHealth.Balance256(target);var rng=NpcEquipmentRandom.Require(b);
            Require(ShadowActions.BeginDragonClaw(b,target,at),"Blocked Dragon entry refused");int frames=ShadowActions.State(b).Plan.Events.Single(e=>e.Type==2).Frame;ShadowActions.Tick(at.AddMilliseconds(frames*40),true,b.Serial.Value);
            Require(SkillHealth.Balance256(target)==hp&&Json(Charges(b))==slots&&NpcEquipmentRandom.Require(b)==rng,"Charged Dragon bypassed shield/consumed on block/rolled accuracy");
        }
        finally{SourceBlocking.ChoiceRoll=choice;SourceBlocking.BlockRoll=roll;Clear(b);target?.Delete();}
    }
    private static void Cancel(AssassinShadowBody b,PlayerMobile owner,DateTime at)
    {
        for(int reason=0;reason<8;reason++)
        {
            Clear(b);var target=Target(b,reason==0?1:65000);var point=b.Location;try
            {
                Gain(b,254,3,at);Require(ShadowActions.BeginDragonClaw(b,target,at),"Dragon cancellation entry refused");var action=ShadowActions.State(b);int first=action.Plan.Events.First(e=>e.Type==1).Frame;
                if(reason is 0 or 1){NpcEquipmentRandom.Set(b,Seed(true));ShadowActions.Tick(at.AddMilliseconds(first*40),true,b.Serial.Value);}
                if(reason==1){target.MoveToWorld(new(b.X+12,b.Y,b.Z),b.Map);}if(reason==2){b.InterruptForRecovery();}if(reason==3){b.Frozen=true;}
                if(reason==4){b.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);}if(reason==5){GeneratedNpcLiveResources.SetItemActive(b,ShadowLoadout.Binding(b).Primary,false);}if(reason==6){PersistentMinions.Park(b);}
                int hp=SkillHealth.Balance256(target);string slots=Json(Charges(b));var rng=NpcEquipmentRandom.Require(b);ShadowActions.Tick(at.AddSeconds(2),reason!=7,b.Serial.Value);
                Require(ShadowActions.State(b)==null&&SkillHealth.Balance256(target)==hp&&Json(Charges(b))==slots&&NpcEquipmentRandom.Require(b)==rng&&ShadowSkillCasting.ReleaseSequence(action.Cast,10*256,at)==0,"Dragon interruption/target-loss replayed a remaining strike");
                if(reason==3){b.Frozen=false;}if(reason==5){GeneratedNpcLiveResources.SetItemActive(b,ShadowLoadout.Binding(b).Primary,true);}b.MoveToWorld(point,owner.Map);
            }
            finally{Clear(b);target.Corpse?.Delete();target.Delete();}at=at.AddSeconds(3);
        }
    }
    private static void Ai(AssassinShadowBody b,PlayerMobile owner,DateTime at)
    {
        Clear(b);var t=Target(b);try
        {
            int frames=((INecromancerGroundBrain)b).GroundSkill(owner,14,260,t,at);Require(frames>0,"Dragon own world request refused");NpcEquipmentRandom.Set(b,Seed(true));int hp=SkillHealth.Balance256(t);ShadowActions.Tick(at.AddMilliseconds(frames*40),true,b.Serial.Value);Require(SkillHealth.Balance256(t)<hp,"Dragon own world caused no damage");Clear(b);
            if(b.Prepared.Capture.Ability==CombatAbility.ShadowWarrior)
            {
                if(!ShadowGroundMotion.Active(b)){ShadowGroundMotion.Activate(b,owner,at.AddMinutes(1));}
                Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.DragonClaw)&&SkillBindings.Instance.Assign(owner,false,CombatAbility.DragonClaw),"Dragon Warrior binding refused");bool selected=false;hp=SkillHealth.Balance256(t);NpcEquipmentRandom.Set(b,SourceRandom.InitLow(1234));
                for(int i=0;i<800&&!(selected&&SkillHealth.Balance256(t)<hp);i++){IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);Require(b.WorldGround.State.Controller.Fault==null,"Dragon Warrior fault: "+b.WorldGround.State.Controller.Fault);selected|=ShadowActions.State(b)?.Cast.Skill==260;}
                Require(selected&&SkillHealth.Balance256(t)<hp,"Natural Warrior AI did not select/damage with Dragon Claw");Clear(b);
            }
        }
        finally{Clear(b);t.Delete();}
    }
    private static void Check(bool reload)
    {
        Source();PlayerMobile owner;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(saved.Bodies[i])&&ShadowActions.State(bodies[i])==null,"Reload lost original Dragon state or replayed transient windup");}owner.MoveToWorld(new(6260,3440,0),Map.Trammel);
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3440);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);ClassSkillChecks.Train(owner,"assassin.dragonclaw",1);var world=NpcWorldAllocation.Capture();
            try{bodies=new AssassinShadowBody[2];int i=0;foreach(var a in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}){NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));bodies[i]=AssassinShadowBody.CreateInitialized(owner,a,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(bodies[i]);ShadowEquipmentDefense.Activate(bodies[i]);i++;}}finally{NpcWorldAllocation.Restore(world);}
        }
        foreach(var b in bodies)
        {
            b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);foreach(int id in new[]{260,254,265,280,259,274,269}){b.ChangeSkills(book=>book.Assign(id,3));}b.ChangeSkills(book=>book.Assign(252,4));
            var at=TimedEffects.Now.AddMinutes(15+generation);var items=ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray();NecromancerEffects.SetMana256(owner,0);
            var far=Target(b);far.MoveToWorld(new(b.X+12,b.Y,b.Z),b.Map);var rng=NpcEquipmentRandom.Require(b);var started=ShadowSkillCasting.Start(b,far,far.Location,260,at);
            Require(started!=null&&NpcEquipmentRandom.Require(b)==rng,"Source start25 incorrectly gated distant target or drew RNG");ShadowSkillCasting.Complete(started);far.Delete();
            Run(b,owner,at,false,true);Run(b,owner,at.AddSeconds(2),false,false);Run(b,owner,at.AddSeconds(4),true,false);Block(b,owner,at.AddSeconds(6));Cancel(b,owner,at.AddSeconds(8));Ai(b,owner,at.AddMinutes(1));
            Require(ShadowLoadout.Originals(b).Select(i=>i.SavedText).SequenceEqual(items),"Dragon changed original item payloads");
            // Save an unreleased series: parking/restart must not replay it.
            var pending=Target(b);Gain(b,254,3,at);Require(ShadowActions.BeginDragonClaw(b,pending,at.AddMinutes(2)),"Pending-save Dragon entry refused");pending.Delete();SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);PersistentMinions.Park(b);
        }
        GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,bodies.Select(Capture).ToArray(),generation)));
        Require(!AssassinCombat.Implemented(CombatAbility.ShadowWarrior)&&!AssassinCombat.Implemented(CombatAbility.ShadowMaster),"Dragon Claw enabled unfinished learned Shadows");
        Log.Information("Own Dragon Claw:24whole NPC callback/start25/do46 controls,original raw-point hands/flag phase,skill ED/hard synergy,uncharged accuracy/charged guarantee with retained block,per-contact all-six release/consume-one,no NPC mana,world/basic/natural Warrior AI,cancellation/no replay and exact original saves through two reloads;modern NPC boundary and remaining intrinsic/death/learned scopes retained.");
    }
}
