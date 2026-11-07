// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowVenomChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowVenomChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,uint Target,int TargetLife,TimedEffectState Poison);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-venom-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static TimedEffectState Poison(Mobile t)=>TimedEffects.Snapshot().SingleOrDefault(s=>s.Owner==t.Serial.Value&&s.PoisonMs>0);
    private static Held Capture(AssassinShadowBody b,BaseCreature t)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),t.Serial.Value,SkillHealth.Balance256(t),Poison(t));
    public static void Configure(){Register("shadow-venom-check",false);Register("shadow-venom-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own Shadow Venom lists,original melee/poison ticks and persistence",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void CaptureControls()
    {
        foreach(int rank in new[]{1,21})foreach(int mastery in new[]{0,25,50,100})
        {
            var d=SkillMechanics.For(CombatAbility.Venom);var values=new SkillValues(d,k=>k==d.SourceKey?rank:0,_=>0,99);
            var list=ShadowDefensiveBuffs.Capture(278,values,DateTime.UnixEpoch,mastery);ShadowDefensiveBuffs.Validate(list);
            int Read(int stat)=>list.Values.Single(v=>v.Stat==stat).Value;
            Require(Read(57)==(rank==1?1536:11520)*(100+mastery)/100&&Read(58)==(rank==1?2048:12032)*(100+mastery)/100&&Read(101)==10
                &&list.PoisonMastery==mastery&&list.UntilUtc==DateTime.UnixEpoch.AddSeconds(rank==1?120:360),"Current Venom endpoints/captured mastery/length/duration mismatch");
            bool bad=false;try{ShadowDefensiveBuffs.Validate(list with{PoisonMastery=mastery+1});}catch(InvalidDataException){bad=true;}
            Require(bad,"Captured mastery corruption accepted");
        }
    }
    private static BaseCreature Target(AssassinShadowBody b)
    {
        var t=new ValkyrieContactChecks.Recipient();t.SetHits(65000);foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}
        t.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();return t;
    }
    private static void Hit(AssassinShadowBody b,PlayerMobile owner,BaseCreature t,DateTime at)
    {
        ulong chosen=0;
        for(int i=0;i<10000;i++){ulong seed=SourceRandom.InitLow(i),copy=seed;if(SourceRandom.Roll(ref copy)%100==0){chosen=seed;break;}}
        Require(chosen!=0,"No guaranteed-contact seed");NpcEquipmentRandom.Set(b,chosen);
        var p=OriginalNpcMelee.Prepare(b,owner,t,_=>true,at);
        Require(p.Impact.Poison is{Frames:10,Rate256:>0},"Own Venom did not reach existing full weapon filler/override");
        var outcome=IronGolemContact.Release(b,owner,t,p,_=>true,at);
        Require((outcome.Packet.ResultFlags&1)!=0&&Poison(t)is{} poison&&poison.PoisonClock?.Rate256==p.Impact.Poison.Rate256
            &&poison.PoisonMs==400&&poison.PoisonSource==owner.Serial.Value,"Original contact lost own poison packet,frame length or owner credit");
    }
    private static void MasteryInput(AssassinShadowBody b,DateTime at)
    {
        b.ChangeSkills(book=>book.Assign(278,20));Require(ShadowActions.BeginBuff(b,null,at,278),"Mastery boundary SC refused");var action=ShadowActions.State(b);
        var graph=b.EquipmentNumbers;int original=graph.Read(332);int supplied=checked(original+50);
        // Declared transient numeric input,not an assertion that original
        // Shadow gear RNG can produce a poison-mastery item. Reconstruction
        // drops this input;the cast must retain the value it actually read.
        graph.Values.SetBase(graph.Actor.Stats,EquipmentStatKey.Pack(332),supplied,graph.Actor);
        ShadowActions.Tick(at.AddMilliseconds(action.Plan.Events.Single(e=>e.Type==1&&e.Kind==1).Frame*40),true,b.Serial.Value);
        var held=b.EquipmentNumbers.Passives.Capture().Lists.Single(l=>l.Skill==278);
        Require(held.PoisonMastery==supplied&&b.EquipmentNumbers.Read(332)==original,"Intrinsic release borrowed owner mastery or lost its captured numeric input");
        ShadowActions.Tick(at.AddMilliseconds(action.Plan.Events.Single(e=>e.Type==2).Frame*40),true,b.Serial.Value);
        GeneratedNpcLiveResources.RefreshShadowPassives(b);
        Require(Json(b.EquipmentNumbers.Passives.Capture().Lists.Single(l=>l.Skill==278))==Json(held),"Live mastery/book change retroactively rewrote the captured Venom list");
    }
    private static BaseCreature Live(AssassinShadowBody b,PlayerMobile owner)
    {
        b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);var at=TimedEffects.Now.AddMinutes(15);
        var fade=ShadowBuffChecks.Cast(b,267,10,at);var baseMin=b.EquipmentNumbers.Read(57);var baseMax=b.EquipmentNumbers.Read(58);
        var venom=ShadowBuffChecks.Cast(b,278,10,at.AddSeconds(1));
        Require(b.EquipmentNumbers.Passives.State(31)&&b.EquipmentNumbers.Passives.State(159)&&venom.PoisonMastery==b.EquipmentNumbers.Read(332)
            &&b.EquipmentNumbers.Read(57)==baseMin+venom.Values.Single(v=>v.Stat==57).Value&&b.EquipmentNumbers.Read(58)==baseMax+venom.Values.Single(v=>v.Stat==58).Value
            &&Venoms.State(owner)==null,"Venom replaced Fade,borrowed owner poison gear or buffed owner");
        b.ChangeSkills(book=>book.Assign(278,1));var random=NpcEquipmentRandom.Require(b);var order=Json(b.EquipmentNumbers.Passives.Capture().Order);
        Require(ShadowActions.BeginBuff(b,null,at.AddSeconds(2),278),"Lower-rank Venom windup refused");ShadowActions.Tick(at.AddMinutes(1),true,b.Serial.Value);
        Require(Json(b.EquipmentNumbers.Passives.Capture().Lists.Single(l=>l.Skill==278))==Json(venom)&&NpcEquipmentRandom.Require(b)==random
            &&Json(b.EquipmentNumbers.Passives.Capture().Order)==order,"Lower-rank Venom replaced/refreshed/reordered stronger state or drew RNG");
        var same=ShadowBuffChecks.Cast(b,278,10,at.AddMinutes(2));Require(same.UntilUtc>venom.UntilUtc&&Json(b.EquipmentNumbers.Passives.Capture().Order)==order,"Equal-rank Venom did not refresh in place");
        var quick=ShadowBuffChecks.Cast(b,258,20,at.AddMinutes(2).AddSeconds(1));
        Require(b.EquipmentNumbers.Passives.State(31)&&b.EquipmentNumbers.Passives.State(157)&&!b.EquipmentNumbers.Passives.State(159),"Group2 replacement removed independent Venom");
        b.ChangeSkills(book=>book.Add(252));GeneratedNpcLiveResources.RefreshShadowPassives(b);
        Require(Json(b.EquipmentNumbers.Passives.Capture().Lists.Single(l=>l.Skill==278))==Json(same),"Passive refresh rewrote captured Venom mastery or expiry");
        var target=Target(b);
        try
        {
            var hitAt=same.UntilUtc.Value.AddMilliseconds(-40);Hit(b,owner,target,hitAt);var poison=Poison(target);int before=SkillHealth.Balance256(target);
            GeneratedNpcLiveResources.ExpireShadowBuffs(b,same.UntilUtc.Value);
            Require(!b.EquipmentNumbers.Passives.State(31)&&b.EquipmentNumbers.Passives.State(157),"Venom expiry removed the independent Quickness state");
            TimedEffects.Settle(target,hitAt.AddMilliseconds(80));Require(before-SkillHealth.Balance256(target)==2*poison.PoisonClock.Rate256&&Poison(target).PoisonMs==320,"Expired weapon buff changed already-delivered poison ticks");
            TimedEffects.CleansePoison(target,0,hitAt.AddMilliseconds(80));int clean=SkillHealth.Balance256(target);TimedEffects.Settle(target,hitAt.AddSeconds(2));
            Require(Poison(target)==null&&SkillHealth.Balance256(target)==clean,"Cure retained poison ticks");
        }
        finally{target.Delete();}
        MasteryInput(b,at.AddMinutes(4));ShadowBuffChecks.Cast(b,267,10,at.AddMinutes(5));var final=ShadowBuffChecks.Cast(b,278,20,at.AddMinutes(6));
        var saved=Target(b);Hit(b,owner,saved,at.AddMinutes(7));PersistentMinions.Park(b);
        Require(b.EquipmentNumbers.Passives.Capture().Lists.Count(l=>l.UntilUtc.HasValue)==2&&final.PoisonMastery.HasValue,"Saved independent buff context lost");return saved;
    }
    private static void Check(bool reload)
    {
        CaptureControls();PlayerMobile owner;AssassinShadowBody[] bodies;BaseCreature[] targets;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
            bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();targets=saved.Bodies.Select(h=>(BaseCreature)World.FindMobile((Serial)h.Target)).ToArray();
            for(int i=0;i<bodies.Length;i++)
            {
                Require(Json(Capture(bodies[i],targets[i]))==Json(saved.Bodies[i]),"Reload changed original buff/poison/item/wound/RNG snapshot");
                var p=Poison(targets[i]);int life=SkillHealth.Balance256(targets[i]);TimedEffects.Settle(targets[i],p.UpdatedUtc.AddMilliseconds(40));
                Require(life-SkillHealth.Balance256(targets[i])==p.PoisonClock.Rate256&&Poison(targets[i]).PoisonMs==p.PoisonMs-40,"Reloaded own NPC poison lost its rate/frame clock");
            }
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3480);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);
            while(owner.RawStr<60){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Poison gear strength");}
            // Trang's full set is currently unavailable;use the existing
            // reviewed Wormskull poison producer without removing set rules.
            var gear=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Wormskull").Id,99,new Random(173),true);owner.AddToBackpack(gear);Require(owner.EquipItem(gear),"Actual owner poison helmet");ClassGear.Refresh(owner);
            Require(BowLaunch.RawPoison(owner,CombatTiming.Equipped(owner)).PoisonMin==102,"Owner poison-gear discrimination fixture");
            bodies=new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}.Select(a=>AssassinShadowBody.CreateInitialized(owner,a,TimedEffects.Now)).ToArray();targets=new BaseCreature[bodies.Length];
            for(int i=0;i<bodies.Length;i++)
            {
                var b=bodies[i];b.ChangeSkills(book=>{book.Assign(252,10);book.Assign(263,20);book.Assign(278,10);});GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);targets[i]=Live(b,owner);
            }
        }
        GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,bodies.Select((b,i)=>Capture(b,targets[i])).ToArray(),generation)));
        Log.Information("Shadow Venom:both original species cast through shared SC;independent group0,lower-rank rejection,equal-rank list order,cast mastery snapshot/no owner gear,existing full weapon filler and10frame poison/credit,cure and buff-expiry independence,preserved original buffs/items/wounds/RNG and actual poison continuation across reloads;other consumers/Master AI/death/learned routes remain pending");
    }
}
