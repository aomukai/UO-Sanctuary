// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowClawChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowClawChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,MartialCharge[] Charges);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-claw-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static MartialCharge[] Charges(AssassinShadowBody b)=>AssassinCharges.Snapshot().Where(s=>s.Owner==b.Serial.Value).OrderBy(s=>s.Ability).ToArray();
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),Charges(b));
    public static void Configure(){Register("shadow-claw-check",false);Register("shadow-claw-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own NPC mode14/do35 elemental builders",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static ulong Seed(bool hit)
    {
        for(int i=1;i<10000;i++){ulong seed=SourceRandom.InitLow(i),copy=seed;int draw=(int)SourceRandom.Limited(ref copy,100);if(hit?draw<5:draw>=95){return seed;}}
        throw new InvalidOperationException("Missing bounded hit seed");
    }
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-claw-cases.json")));int count=0;
        foreach(var c in doc.RootElement.EnumerateArray())
        {
            int speed=c.GetProperty("Speed").GetInt32(),mask=c.GetProperty("Mask").GetInt32(),charges=c.GetProperty("Initial").GetInt32(),ordinal=0;
            var state=ShadowClawSequence.Initial();int tick=0;var events=new System.Collections.Generic.List<SourceAnimationEvent>();
            foreach(var r in c.GetProperty("Trace").EnumerateArray())
            {
                bool end=ShadowClawSequence.Complete(state),release=ShadowClawSequence.Releases(state);int hand=0;
                if(release){hand=((state.Clock.Point>>8)&1)==0?1:2;if((mask&(1<<ordinal++))!=0){charges=Math.Min(3,charges+1);}events.Add(new(1,tick+1,1,state.Clock.Point));}
                var next=end?state:ShadowClawSequence.Advance(state,speed);
                int[] actual=[++tick,state.Clock.Point,state.Clock.Remaining,state.Clock.Action,state.ServerDo?1:0,end?2:1,
                    next.Clock.Point,next.Clock.Remaining,next.Clock.Action,next.ServerDo?1:0,charges,hand];
                Require(actual.SequenceEqual(r.EnumerateArray().Select(x=>x.GetInt32())),"NPC mode14 callback/point/flag/hand/charge differs from full source trace");state=next;
            }
            events.Add(new(2,tick,0));Require(ShadowClawSequence.Plan(speed).Events.SequenceEqual(events),"NPC sequence plan changed callback/end order");count++;
        }
        Require(count==144,"Missing complete NPC sequence contexts");
        using var rates=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-claw-rates.json")));count=0;
        foreach(var r in rates.RootElement.EnumerateArray())
        {
            Require(r[5].GetInt32()==0&&r[6].GetInt32()==6&&r[7].GetInt32()==18&&r[9].GetInt32()==1
                &&ShadowAnimation.SuppliedClaws(r[1].GetInt32(),r[2].GetInt32(),r[3].GetInt32(),r[4].GetInt32()).Speed==r[8].GetInt32(),"NPC sequence GH conversion/rate differs from source");count++;
        }
        Require(count==12,"Missing NPC sequence rate contexts");
        // Retain the one-action overload's exact behavior in the shared kernel.
        foreach(int speed in new[]{38,128,256,448})
        {
            var s=new SentrySequenceState();while(!AssassinSentrySequence.Complete(s))
            {Require(AssassinSentrySequence.Advance(s,speed)==AssassinSentrySequence.AdvanceIn(s,15*256,new[]{2},speed),"Shared stationary sequence changed");s=AssassinSentrySequence.Advance(s,speed);}
        }
    }
    private static SkillValues Values(AssassinShadowBody b,int id)=>ShadowSkillCasting.Values(b,b.OwnedSkillRank(id),id);
    private static void Run(AssassinShadowBody b,PlayerMobile owner,BaseCreature target,int id,DateTime at,bool hit)
    {
        var ability=Values(b,id).Ability;var before=AssassinCharges.State(b,ability);int previous=before?.Count??0,hp=SkillHealth.Balance256(target);NpcEquipmentRandom.Set(b,Seed(hit));var rng=NpcEquipmentRandom.Require(b);
        Require(ShadowActions.BeginCharge(b,target,at,id),"Own NPC claw sequence entry refused");var action=ShadowActions.State(b);int end=action.Plan.Events.Single(e=>e.Type==2).Frame;
        Require(action.Sequence!=null&&action.Cast.Skill==id&&b.OwnedSkills.Find(id).Mode==14&&NpcEquipmentRandom.Require(b)==rng&&!action.Cast.Released
            &&ShadowSkillCasting.Release(action.Cast,at)==0,"Sequence borrowed one-shot/player release or allocated early");
        var markers=action.Plan.Events.Where(e=>e.Type==1).ToArray();Require(markers.Length is 1 or 2,"Unexpected NPC release count");
        int releases=0;
        for(int frame=1;frame<=end;frame++)
        {
            var pending=ShadowActions.State(b);bool release=pending?.Sequence is{} s&&ShadowClawSequence.Releases(s);
            int point=pending?.Sequence?.Clock.Point??0;var bind=ShadowLoadout.Binding(b);uint selected=((point>>8)&1)==0?bind.Primary:bind.Secondary;
            if(release){Require(OriginalNpcEquipment.WeaponFrame(b).Selected==selected,"Contact selected player ordinal instead of raw NPC sequence point");NpcEquipmentRandom.Set(b,Seed(hit));}
            int oldHp=SkillHealth.Balance256(target);rng=NpcEquipmentRandom.Require(b);var charge=AssassinCharges.State(b,ability);var phase=at.AddMilliseconds(frame*40);
            ShadowActions.Tick(phase,true,b.Serial.Value);
            if(release)
            {
                releases++;Require(NpcEquipmentRandom.Require(b)!=rng,"Actual sequence release omitted its own contact roll");
                var after=AssassinCharges.State(b,ability);
                Require(hit?after!=null&&after.Count==Math.Min(3,previous+releases)&&after.InitialRank==Values(b,id).Rank
                    &&after.AttackPercent==after.Count*Values(b,id).Eval("aurastatcalc2")&&after.UntilUtc==phase.AddMilliseconds(Values(b,id).Eval("auralencalc")*40)
                    &&SkillHealth.Balance256(target)<oldHp:after==charge&&SkillHealth.Balance256(target)==oldHp,"Own sequence hit/miss charge/contact/rank/deadline failed");
                Require(b.EquipmentNumbers.DamageItems.Contains(selected)&&!b.EquipmentNumbers.DamageItems.Contains(selected==bind.Primary?bind.Secondary:bind.Primary),"Damage stat graph used both claws or wrong hand");
                var afterRng=NpcEquipmentRandom.Require(b);Require(ShadowSkillCasting.ReleaseSequence(action.Cast,point,phase)==0&&NpcEquipmentRandom.Require(b)==afterRng,"Duplicate NPC sequence event replayed");
            }
            else{Require(SkillHealth.Balance256(target)==oldHp&&AssassinCharges.State(b,ability)==charge&&NpcEquipmentRandom.Require(b)==rng,"Non-release frame mutated health/charge/RNG");}
        }
        Require(releases==markers.Length&&ShadowActions.State(b)==null&&b.OwnedSkills.Used==null&&action.Cast.Released,"NPC sequence failed to end");
        int final=SkillHealth.Balance256(target);rng=NpcEquipmentRandom.Require(b);ShadowActions.Tick(at.AddSeconds(20),true,b.Serial.Value);
        Require(SkillHealth.Balance256(target)==final&&NpcEquipmentRandom.Require(b)==rng&&(hit?final<hp:final==hp),"Ended NPC sequence replayed damage/RNG");
    }
    private static void Interrupt(AssassinShadowBody b,PlayerMobile owner,BaseCreature target,int id,DateTime at)
    {
        var ability=Values(b,id).Ability;
        for(int reason=0;reason<6;reason++)
        {
            at=at.AddSeconds(3);Require(ShadowActions.BeginCharge(b,target,at,id),"Sequence interruption entry refused");var action=ShadowActions.State(b);
            if(reason==0){int first=action.Plan.Events.First(e=>e.Type==1).Frame;NpcEquipmentRandom.Set(b,Seed(true));ShadowActions.Tick(at.AddMilliseconds(first*40),true,b.Serial.Value);b.InterruptForRecovery();}
            if(reason==1){b.Frozen=true;}if(reason==2){b.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);}
            if(reason==3){GeneratedNpcLiveResources.SetItemActive(b,ShadowLoadout.Binding(b).Primary,false);}if(reason==4){PersistentMinions.Park(b);}
            var charge=AssassinCharges.State(b,ability);int hp=SkillHealth.Balance256(target);var rng=NpcEquipmentRandom.Require(b);
            ShadowActions.Tick(at.AddSeconds(2),reason!=5,b.Serial.Value);
            Require(ShadowActions.State(b)==null&&AssassinCharges.State(b,ability)==charge&&SkillHealth.Balance256(target)==hp&&NpcEquipmentRandom.Require(b)==rng
                &&ShadowSkillCasting.ReleaseSequence(action.Cast,10*256,at)==0,"Interrupted sequence replayed a remaining strike");
            if(reason==1){b.Frozen=false;}if(reason==3){GeneratedNpcLiveResources.SetItemActive(b,ShadowLoadout.Binding(b).Primary,true);}b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        }
    }
    private static void Exercise(AssassinShadowBody b,PlayerMobile owner,int generation)
    {
        b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);var target=new ShadowPsychicChecks.Witness{CasterId=b.Serial.Value};target.SetHits(65000);
        foreach(var type in Enum.GetValues<ResistanceType>()){target.SetResistance(type,0);}target.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);target.AIObject.AITimer.Stop();TimedEffects.PreventHealing(target,TimedEffects.Now);
        try
        {
            AssassinCharges.Clear(b);int mana=NecromancerEffects.ManaBalance256(owner),ownMana=b.EquipmentNumbers.Read(8);var items=ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray();var at=TimedEffects.Now.AddMinutes(10+generation);
            foreach(int id in new[]{259,269,274})
            {
                b.ChangeSkills(book=>book.Assign(id,3));Run(b,owner,target,id,at,false);Run(b,owner,target,id,at.AddSeconds(2),true);Run(b,owner,target,id,at.AddSeconds(4),true);
                Interrupt(b,owner,target,id,at.AddSeconds(6));at=at.AddMinutes(1);
            }
            Require(Charges(b).Length==3&&Charges(b).All(s=>s.Count==3)&&AssassinCharges.Any(b),"Independent elemental charge slots lost");
            foreach(int id in new[]{259,269,274}){Require(ShadowChargeStrikes.State(b,GenericPetMetadata.Table.Skill(id).AuraState),"NPC progressive state query lost elemental family");}
            Require(NecromancerEffects.ManaBalance256(owner)==mana&&b.EquipmentNumbers.Read(8)==ownMana&&ShadowLoadout.Originals(b).Select(i=>i.SavedText).SequenceEqual(items),"NPC claw builder borrowed mana or changed original inventory");
            SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);
            // A pending windup is deliberately saved; it must never replay on reload.
            Require(ShadowActions.BeginCharge(b,target,at,259),"Pending-save sequence entry refused");
        }
        finally{target.Delete();}
    }
    private static void Check(bool reload)
    {
        Source();PlayerMobile owner;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(saved.Bodies[i])&&ShadowActions.State(bodies[i])==null,"Reload lost original state or replayed pending sequence");}
            owner.MoveToWorld(new(6260,3520,0),Map.Trammel);
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3520);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);var world=NpcWorldAllocation.Capture();
            try{bodies=new AssassinShadowBody[2];int i=0;foreach(var a in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}){NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));bodies[i]=AssassinShadowBody.CreateInitialized(owner,a,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(bodies[i]);ShadowEquipmentDefense.Activate(bodies[i]);i++;}}
            finally{NpcWorldAllocation.Restore(world);}
        }
        foreach(var b in bodies){Exercise(b,owner,generation);PersistentMinions.Park(b);}GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,bodies.Select(Capture).ToArray(),generation)));
        Require(!AssassinCombat.Implemented(CombatAbility.ShadowMaster)&&!AssassinCombat.Implemented(CombatAbility.ShadowWarrior),"Unfinished learned Shadows enabled");
        Log.Information("Own elemental claw builders:144 complete NPC sequence traces/12 mode14 rate controls,actual original contacts/hands/RNG/charges,miss/cap/refresh/independent slots,interruption/no replay and exact original resources/items/wounds/charges through two reloads. Elemental finishers/automatic AI/learned routes remain gated;modern parity unverified.");
    }
}
