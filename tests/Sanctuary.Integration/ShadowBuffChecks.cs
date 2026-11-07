// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowBuffChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowBuffChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-buffs-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b));
    public static void Configure(){Register("shadow-buffs-check",false);Register("shadow-buffs-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own Shadow quickness/fade source lists,SC events,expiry and original saves",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Source()
    {
        using var d=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-buff-animation-cases.json")));int count=0;
        foreach(var row in d.RootElement.EnumerateArray())
        {
            var a=row.GetProperty("Rates").EnumerateArray().Select(x=>x.GetInt32()).ToArray();var expected=row.GetProperty("Expected");var plan=ShadowAnimation.SuppliedBuff(a[0],a[1],a[2],a[3],a[4]);
            Require(expected[2].GetInt32()==10&&plan.Speed==expected[4].GetInt32()&&Json(plan.Events.Select(e=>new[]{e.Type,e.Frame,e.Kind,e.Param}).ToArray())==expected[6].GetRawText(),"Whole converted SC clock mismatch");count++;
        }
        Require(count==324,"Missing defensive cast source clocks");
    }
    private static ShadowPassiveList Cast(AssassinShadowBody b,int skill,int rank,DateTime at)
    {
        b.ChangeSkills(book=>book.Assign(skill,rank));var owner=(PlayerMobile)b.ControlMaster;
        int mana=NecromancerEffects.ManaBalance256(owner),life=SkillHealth.Balance256(b);var random=NpcEquipmentRandom.Require(b);var game=NpcWorldAllocation.Capture();
        var before=b.EquipmentNumbers.Passives.Capture();
        Require(ShadowActions.BeginBuff(b,null,at,skill),"Own SC buff startup refused");var action=ShadowActions.State(b);
        int release=action.Plan.Events.Single(e=>e.Type==1&&e.Kind==1).Frame,end=action.Plan.Events.Single(e=>e.Type==2).Frame;
        ShadowActions.Tick(at.AddMilliseconds((release-1)*40),true,b.Serial.Value);
        Require(Json(b.EquipmentNumbers.Passives.Capture().Lists)==Json(before.Lists),"Buff applied before own SC release");
        ShadowActions.Tick(at.AddMilliseconds(release*40),true,b.Serial.Value);
        var list=b.EquipmentNumbers.Passives.Capture().Lists.Single(l=>l.Skill==skill);int effective=b.OwnedSkillRank(skill);
        var expected=ShadowDefensiveBuffs.Capture(skill,ShadowSkillCasting.Values(b,effective,skill),at.AddMilliseconds(release*40));
        Require(Json(list)==Json(expected)&&list.Level==effective,"SC release did not post its own source buff values/expiry");
        ShadowActions.Tick(at.AddMilliseconds(end*40+400),true,b.Serial.Value);
        Require(ShadowActions.State(b)==null&&b.OwnedSkills.Used==null&&NpcEquipmentRandom.Require(b)==random&&NpcWorldAllocation.Capture()==game
            &&NecromancerEffects.ManaBalance256(owner)==mana&&SkillHealth.Balance256(b)==life,"Self buff replayed,used owner mana,allocated missiles or reset wounds/RNG");
        Require(AssassinBuffs.State(owner)==null,"NPC buff was applied to owner");return list;
    }
    private static void Live(AssassinShadowBody b,PlayerMobile owner)
    {
        b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);var originalItems=ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray();
        var at=TimedEffects.Now.AddMinutes(8);var baseGraph=b.EquipmentNumbers;int[] stats=[67,68,69,36,39,41,43,45,109,181];
        // Pin the selected-hand merge before comparing the attached lists.
        OriginalNpcEquipment.WeaponFrame(b).Enter();var baseline=stats.Select(s=>baseGraph.Read(s)).ToArray();
        var quick=Cast(b,258,10,at);var capture=AssassinBuffs.Capture(0,ShadowSkillCasting.Values(b,quick.Level,258),at);
        Require(b.EquipmentNumbers.Read(67)==baseline[0]+capture.Velocity&&b.EquipmentNumbers.Read(68)==baseline[1]+capture.AttackRate
            &&b.EquipmentNumbers.Read(69)==baseline[2]+capture.AttackRate,"Own quickness did not reach velocity/attack/other-animation consumers");
        var low=Cast(b,258,1,at.AddSeconds(1));Require(low.Level<quick.Level&&b.EquipmentNumbers.Passives.Capture().Lists.Count(l=>l.UntilUtc.HasValue)==1,"Group2 same-state lower-rank recast was incorrectly rejected or stacked");
        var fade=Cast(b,267,20,at.AddSeconds(2));var graph=b.EquipmentNumbers;
        Require(!graph.Passives.State(157)&&graph.Passives.State(159)&&graph.Read(67)==baseline[0]&&graph.Read(68)==baseline[1]&&graph.Read(69)==baseline[2],"Fade failed to replace all quickness channels");
        var f=AssassinBuffs.Capture(0,ShadowSkillCasting.Values(b,fade.Level,267),at);
        Require(graph.Read(36)==baseline[3]+f.PhysicalResistance&&graph.Read(39)==baseline[4]+f.ElementResistance
            &&graph.Read(41)==baseline[5]+f.ElementResistance&&graph.Read(43)==baseline[6]+f.ElementResistance&&graph.Read(45)==baseline[7]+f.ElementResistance
            &&graph.Read(109)==baseline[8]+f.CurseResistance&&graph.Read(181)==baseline[9]+f.Fade,"Fade numeric resistance/passive channels escaped own graph");
        Require(SourceCurseDuration.Incoming(b,"weaken",1000,out int frames)&&frames==1000-SourcePercentage.Calculate(1000,graph.Read(109),100),"Incoming curse did not use Shadow's own Fade/curse resistance");
        b.ChangeSkills(book=>book.Add(252));GeneratedNpcLiveResources.RefreshShadowPassives(b);
        Require(Json(b.EquipmentNumbers.Passives.Capture().Lists.Single(l=>l.Skill==267))==Json(fade),"Passive/book refresh rewrote or removed timed buff");
        var wounded=SkillHealth.Balance256(b);var rng=NpcEquipmentRandom.Require(b);PersistentMinions.Park(b);
        GeneratedNpcLiveResources.ExpireShadowBuffs(b,fade.UntilUtc.Value.AddMilliseconds(-1));Require(b.EquipmentNumbers.Passives.State(159),"Parked buff expired early");
        GeneratedNpcLiveResources.ExpireShadowBuffs(b,fade.UntilUtc.Value);
        Require(!b.EquipmentNumbers.Passives.State(159)&&b.EquipmentNumbers.Read(39)==baseline[4]&&b.EquipmentNumbers.Read(109)==baseline[8]
            &&SkillHealth.Balance256(b)==wounded&&NpcEquipmentRandom.Require(b)==rng,"Parked expiry reset wounds/RNG or retained Fade");
        b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);var saved=Cast(b,267,5,at.AddMinutes(1));
        Require(ShadowActions.BeginBuff(b,null,at.AddMinutes(2),258),"Cancel fixture failed to enter SC");PersistentMinions.Park(b);ShadowActions.Tick(at.AddMinutes(3),true,b.Serial.Value);
        Require(ShadowActions.State(b)==null&&Json(b.EquipmentNumbers.Passives.Capture().Lists.Single(l=>l.Skill==267))==Json(saved),"Cancelled SC replaced an existing buff");
        Require(ShadowLoadout.Originals(b).Select(i=>i.SavedText).SequenceEqual(originalItems),"Buff changed original item payloads");
    }
    private static void Warrior(AssassinShadowBody b,PlayerMobile owner)
    {
        b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);TimedEffects.PreventHealing(b,TimedEffects.Now);
        ShadowGroundMotion.Activate(b,owner,TimedEffects.Now.AddMinutes(12));
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.BasicAttack)&&SkillBindings.Instance.Assign(owner,false,CombatAbility.BurstOfSpeed),"Owner quickness selection refused");
        var target=new ValkyrieContactChecks.Recipient();target.SetHits(65000);target.MoveToWorld(new(b.X+6,b.Y,b.Z),b.Map);target.AIObject.AITimer.Stop();
        try
        {
            ulong chosen=0;
            for(int i=1;i<10000;i++)
            {
                ulong seed=SourceRandom.InitLow(i),copy=seed;if((SourceRandom.Roll(ref copy)&1)!=0||SourceRandom.Roll(ref copy)%100>84){continue;}
                if(SourceRandom.Roll(ref copy)%5<=SourceRandom.Roll(ref copy)%100){chosen=seed;break;}
            }
            Require(chosen!=0,"No Warrior quickness seed");NpcEquipmentRandom.Set(b,chosen);int mana=NecromancerEffects.ManaBalance256(owner);var speed=ShadowGroundMotion.Speed(b,0,false);
            void Through(int frame)
            {var s=b.WorldGround.State.Controller;IronGolemWorld.Advance(b,s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L),true);Require(b.WorldGround.State.Controller.Fault==null,"Warrior buff controller fault");}
            Through(20);var action=ShadowActions.State(b);Require(action?.Cast.Skill==258&&ShadowGroundMotion.Mode(b)==7,"Own AI did not select/enter SC quickness while Fade is active");
            Through(20+action.Plan.Events.Single(e=>e.Type==1&&e.Kind==1).Frame);
            Require(b.EquipmentNumbers.Passives.State(157)&&!b.EquipmentNumbers.Passives.State(159)&&ShadowGroundMotion.Speed(b,0,false).Velocity>speed.Velocity
                &&NecromancerEffects.ManaBalance256(owner)==mana,"AI-released quickness did not replace Fade and accelerate own actual movement");
            Through(20+action.Plan.Events.Single(e=>e.Type==2).Frame);
            Require(ShadowActions.State(b)==null&&b.OwnedSkills.Used==null,"Warrior SC completion left pending state");
        }
        finally{target.Delete();PersistentMinions.Park(b);}
    }
    private static void Check(bool reload)
    {
        Source();PlayerMobile owner;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++)
            {
                Require(Json(Capture(bodies[i]))==Json(saved.Bodies[i])&&ShadowActions.State(bodies[i])==null,"Reload changed original buff values/expiry/items/wounds/RNG or replayed cast");
                var list=bodies[i].EquipmentNumbers.Passives.Capture().Lists.Single(l=>l.UntilUtc.HasValue);
                Require(bodies[i].EquipmentNumbers.Passives.State(list.State)&&(list.Skill==267?bodies[i].EquipmentNumbers.Read(109)>0:list.Skill==258&&bodies[i].EquipmentNumbers.Read(68)>100),"Reload lost actual self-buff numeric/state consumers");
            }
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3480);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);
            bodies=new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}.Select(a=>AssassinShadowBody.CreateInitialized(owner,a,TimedEffects.Now)).ToArray();
            foreach(var b in bodies){b.ChangeSkills(book=>{book.Assign(252,10);book.Assign(263,20);book.Assign(258,10);book.Assign(267,20);});GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);Live(b,owner);}
            Warrior(bodies[0],owner);
        }
        ShadowPassiveChecks.SourceControls(); // Needs a real parked original actor; exercises changed shared cache/attachment path.
        GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,bodies.Select(Capture).ToArray(),generation)));
        Log.Information("Shadow buffs:324complete SC clocks,1600retained passive contexts;both own species use quickness/fade posted lists,actual SC release/end,group2 replacement,source numeric and curse consumers,book refresh,parked expiry/cancellation and original values/expiry/items/wounds/RNG across reloads;Venom/other skills/Master AI/death/learned routes remain pending");
    }
}
