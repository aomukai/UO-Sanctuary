// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowChargeChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowChargeChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,MartialCharge[] Charges);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-charge-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static MartialCharge[] Charges(AssassinShadowBody b)=>AssassinCharges.Snapshot().Where(s=>s.Owner==b.Serial.Value).ToArray();
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),Charges(b));
    public static void Configure(){Register("shadow-charge-check",false);Register("shadow-charge-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own Shadow start23/do34 original contact and shared charge persistence",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static ulong Seed(bool hit)
    {
        for(int i=1;i<10000;i++){ulong seed=SourceRandom.InitLow(i),copy=seed;int draw=(int)SourceRandom.Limited(ref copy,100);if(hit?draw<5:draw>=95){return seed;}}
        throw new InvalidOperationException("Missing bounded min/max hit seed");
    }
    private static SkillValues Values(AssassinShadowBody b,int id)=>ShadowSkillCasting.Values(b,b.OwnedSkillRank(id),id);
    private static void Cast(AssassinShadowBody b,BaseCreature target,int id,DateTime at,bool hit)
    {
        NpcEquipmentRandom.Set(b,Seed(hit));var before=NpcEquipmentRandom.Require(b);var cast=ShadowSkillCasting.Start(b,target,target.Location,id,at);
        Require(cast!=null&&NpcEquipmentRandom.Require(b)==before,"Start23 rejected melee target or allocated hit RNG early");
        Require(ShadowSkillCasting.Release(cast,at)==1,"Own do34 dispatch failed");var after=NpcEquipmentRandom.Require(b);
        Require(ShadowSkillCasting.Release(cast,at)==0&&NpcEquipmentRandom.Require(b)==after,"Duplicate charge release replayed");ShadowSkillCasting.Complete(cast);
    }
    private static void Animation()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-charge-animation-cases.json")));int count=0;
        foreach(var c in doc.RootElement.EnumerateArray())
        {
            var r=c.GetProperty("Rates").EnumerateArray().Select(x=>x.GetInt32()).ToArray();var expected=c.GetProperty("Expected");
            var plan=ShadowAnimation.SuppliedCharge(r[0],r[1],r[2],r[3],r[4]);
            var events=expected[6].EnumerateArray().Select(x=>new SourceAnimationEvent(x[0].GetInt32(),x[1].GetInt32(),x[2].GetInt32(),x[3].GetInt32())).ToArray();
            Require(expected[2].GetInt32()==8&&plan.Speed==expected[4].GetInt32()&&plan.Events.SequenceEqual(events),"A2 transformed rate/events differ from full source context");count++;
        }
        Require(count==12,"Missing distinct new A2 source contexts");
    }
    private static void Action(AssassinShadowBody b,PlayerMobile owner,BaseCreature target,int id,DateTime at)
    {
        var ability=Values(b,id).Ability;var before=AssassinCharges.State(b,ability);int hp=SkillHealth.Balance256(target);NpcEquipmentRandom.Set(b,Seed(true));var rng=NpcEquipmentRandom.Require(b);
        Require(ShadowActions.BeginCharge(b,target,at,id),"Own A2 action entry refused");var action=ShadowActions.State(b);
        int release=action.Plan.Events.Single(e=>e.Type==1&&e.Kind==1).Frame,end=action.Plan.Events.Single(e=>e.Type==2).Frame;
        Require(release>1&&end>release&&action.Cast.Skill==id&&NpcEquipmentRandom.Require(b)==rng&&!action.Cast.Released,"A2 entry allocated early or used wrong clock");
        ShadowActions.Tick(at.AddMilliseconds((release-1)*40),true,b.Serial.Value);
        Require(SkillHealth.Balance256(target)==hp&&AssassinCharges.State(b,ability)==before&&NpcEquipmentRandom.Require(b)==rng,"A2 gained/damaged before marker");
        var phase=at.AddMilliseconds(release*40);ShadowActions.Tick(phase,true,b.Serial.Value);
        Require(action.Cast.Released&&SkillHealth.Balance256(target)<hp&&AssassinCharges.State(b,ability)is{Count:3} after
            &&after.UntilUtc==phase.AddMilliseconds(Values(b,id).Eval("auralencalc")*40),"A2 actual release/contact/charge deadline failed");
        hp=SkillHealth.Balance256(target);rng=NpcEquipmentRandom.Require(b);ShadowActions.Tick(at.AddMilliseconds(end*40),true,b.Serial.Value);
        ShadowActions.Tick(at.AddSeconds(20),true,b.Serial.Value);
        Require(ShadowActions.State(b)==null&&b.OwnedSkills.Used==null&&SkillHealth.Balance256(target)==hp&&NpcEquipmentRandom.Require(b)==rng,"A2 end/duplicate tick replayed release");
        // Same admitted enemy remains the target; leaving reach before the
        // marker must not hit it or silently select another nearby monster.
        at=at.AddSeconds(2);before=AssassinCharges.State(b,ability);var point=target.Location;
        Require(ShadowActions.BeginCharge(b,target,at,id),"A2 moved-target fixture entry");target.MoveToWorld(new(b.X+12,b.Y,b.Z),b.Map);
        ShadowActions.Tick(at.AddSeconds(2),true,b.Serial.Value);target.MoveToWorld(point,b.Map);
        Require(AssassinCharges.State(b,ability)==before&&SkillHealth.Balance256(target)==hp&&NpcEquipmentRandom.Require(b)==rng,"Out-of-reach A2 target gained/hit/rolled");
        for(int reason=0;reason<6;reason++)
        {
            at=at.AddSeconds(3);before=AssassinCharges.State(b,ability);rng=NpcEquipmentRandom.Require(b);
            Require(ShadowActions.BeginCharge(b,target,at,id),"A2 cancellation fixture entry");action=ShadowActions.State(b);
            if(reason==0){b.InterruptForRecovery();}if(reason==1){b.Frozen=true;}
            if(reason==2){b.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);}
            if(reason==3){GeneratedNpcLiveResources.SetItemActive(b,ShadowLoadout.Binding(b).Primary,false);}
            if(reason==4){PersistentMinions.Park(b);}
            ShadowActions.Tick(at.AddSeconds(2),reason!=5,b.Serial.Value);
            Require(ShadowActions.State(b)==null&&AssassinCharges.State(b,ability)==before&&SkillHealth.Balance256(target)==hp&&NpcEquipmentRandom.Require(b)==rng
                &&ShadowSkillCasting.Release(action.Cast,at)==0,"Interrupted A2 action retained/replayed/gained");
            if(reason==1){b.Frozen=false;}if(reason==3){GeneratedNpcLiveResources.SetItemActive(b,ShadowLoadout.Binding(b).Primary,true);}
            b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        }
    }
    private static void Exercise(AssassinShadowBody b,PlayerMobile owner,int generation)
    {
        b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        var target=new ShadowPsychicChecks.Witness{CasterId=b.Serial.Value};target.SetHits(65000);foreach(var type in Enum.GetValues<ResistanceType>()){target.SetResistance(type,0);}
        target.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);target.AIObject.AITimer.Stop();TimedEffects.PreventHealing(target,TimedEffects.Now);
        try
        {
            AssassinCharges.Clear(b);int mana=NecromancerEffects.ManaBalance256(owner),ownMana=b.EquipmentNumbers.Read(8);var items=ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray();
            var at=TimedEffects.Now.AddMinutes(10+generation);
            foreach(int id in new[]{254,265,280})
            {
                b.ChangeSkills(book=>book.Assign(id,3));var v=Values(b,id);var before=NpcEquipmentRandom.Require(b);
                target.MoveToWorld(new(b.X+12,b.Y,b.Z),b.Map);
                Require(ShadowSkillCasting.Start(b,target,target.Location,id,at)==null&&NpcEquipmentRandom.Require(b)==before,"Start23 admitted distant target or rolled");
                b.ChangeSkills(book=>book.Use(null));target.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);
                int hp=SkillHealth.Balance256(target);Cast(b,target,id,at,false);
                Require(AssassinCharges.State(b,v.Ability)==null&&SkillHealth.Balance256(target)==hp,"Miss gained charges or dealt damage");
                for(int n=1;n<=4;n++)
                {
                    Cast(b,target,id,at.AddMilliseconds(n*40),true);var state=AssassinCharges.State(b,v.Ability);
                    Require(state!=null&&state.Owner==b.Serial.Value&&state.Count==Math.Min(3,n)&&state.InitialRank==v.Rank
                        &&state.AttackPercent==Math.Min(3,n)*v.Eval("aurastatcalc2")
                        &&state.UntilUtc==at.AddMilliseconds(n*40+v.Eval("auralencalc")*40),"Own charge count/rank/AR/cap/refresh differs from shared source builder");
                }
                Require(SkillHealth.Balance256(target)<hp,"Successful own original charge contacts did not damage target");
                Action(b,owner,target,id,at.AddSeconds(1));
            }
            Require(AssassinCharges.Finisher(b).Length==3&&AssassinCharges.Any(b),"Own simultaneous progressive states are not visible to Master queries");
            var saved=Charges(b);AssassinCharges.Clear(b);var rest=AssassinCharges.Snapshot();AssassinCharges.Restore([..rest,..saved]);
            Require(Json(Charges(b))==Json(saved),"Shared charge restore lost original NPC slots");
            AssassinCharges.Clear(b);b.ChangeSkills(book=>book.Assign(274,3));AssassinCharges.Gain(b,ShadowSkillCasting.OwnValues(b,b.OwnedSkillRank(274),274),at);
            NpcEquipmentRandom.Set(b,Seed(true));var elemental=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at);
            Require((elemental.Impact.Packet.ResultFlags&1)!=0&&elemental.Impact.Packet.Cold256>0&&AssassinCharges.State(b,CombatAbility.BladesOfIce)==null,"Qualified Ice finisher failed actual allocation/consume-one");elemental.Cancel();
            AssassinCharges.Clear(b);rest=AssassinCharges.Snapshot();AssassinCharges.Restore([..rest,..saved]);
            // Real deadlines expire while parked; expiry grants no reset/heal.
            AssassinCharges.Gain(b,Values(b,254),TimedEffects.Now.AddMinutes(-1));
            Require(AssassinCharges.State(b,CombatAbility.TigerStrike)==null,"Expired replacement remains active");AssassinCharges.Prune();
            Require(Charges(b).All(s=>s.Ability!=CombatAbility.TigerStrike),"Expired own charge retained after prune");
            AssassinCharges.Gain(b,Values(b,254),at);SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);
            Require(NecromancerEffects.ManaBalance256(owner)==mana&&b.EquipmentNumbers.Read(8)==ownMana
                &&ShadowLoadout.Originals(b).Select(i=>i.SavedText).SequenceEqual(items),"Own builder borrowed mana or changed original inventory");
            Require(AssassinCharges.Finisher(owner).Length==0,"Own charges leaked to player owner");
        }
        finally{target.Delete();}
    }
    private static void Check(bool reload)
    {
        Animation();PlayerMobile owner;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
            bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(saved.Bodies[i]),"Reload lost original body/items/resources/wounds/RNG/charges or renewed expiry");}
            owner.MoveToWorld(new(6260,3640,0),Map.Trammel);
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3640);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);
            bodies=new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}.Select(a=>AssassinShadowBody.CreateInitialized(owner,a,TimedEffects.Now)).ToArray();
            foreach(var b in bodies){GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);}
        }
        foreach(var b in bodies){Exercise(b,owner,generation);PersistentMinions.Park(b);}
        GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());
        File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,bodies.Select(Capture).ToArray(),generation)));
        Require(!AssassinCombat.Implemented(CombatAbility.ShadowMaster)&&!AssassinCombat.Implemented(CombatAbility.ShadowWarrior),"Builder backend enabled unfinished learned Shadows");
        Log.Information("Shadow charge backend: own selected start23/do34 for Tiger/Cobra/Phoenix, full original contact/AR/RNG, hit-only three-charge cap/refresh and independent slots, no owner mana, duplicate/miss/range/expiry guards, original persistent body/charges through reloads. A2 conductor checked against12new full source contexts; automatic builders and progressive finishers remain gated.");
    }
}
