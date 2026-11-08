// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowFinisherChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowFinisherChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,MartialCharge[] Charges);
    private sealed record LoadoutGap(int Seed,CombatAbility Ability,string Reason);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0,LoadoutGap[] Loadouts=null);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-finisher-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static MartialCharge[] Charges(AssassinShadowBody b)=>AssassinCharges.Snapshot().Where(s=>s.Owner==b.Serial.Value).ToArray();
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),Charges(b));
    public static void Configure(){Register("shadow-finisher-check",false);Register("shadow-finisher-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own Shadow Tiger/Cobra finisher, flat NPC drains and AI admission",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static ulong Seed(bool hit)
    {for(int i=1;i<10000;i++){ulong seed=SourceRandom.InitLow(i),copy=seed;int draw=(int)SourceRandom.Limited(ref copy,100);if(hit?draw<5:draw>=95){return seed;}}throw new InvalidOperationException("Missing bounded hit seed");}
    private static void Gain(AssassinShadowBody b,int id,int count,DateTime at)
    {var v=ShadowSkillCasting.Values(b,b.OwnedSkillRank(id),id);for(int i=0;i<count;i++){AssassinCharges.Gain(b,v,at);}}
    private static int ExpectedPhysical(AssassinShadowBody b,BaseCreature target,int tiger,int flags)
    {
        // Predict this stream with the already-qualified physical allocator.
        // Enlarging an integer RNG bound changes raw%bound: equal seeds do
        // not imply that every enhanced realized roll exceeds the old roll.
        var graph=OriginalNpcEquipment.Graph(b);var frame=OriginalNpcEquipment.WeaponFrame(b);frame.Enter();
        int extra=tiger+CombatAuras.DamagePercent(b)+NecromancerEffects.DamageStatPercent(b);
        if(NamedCombat.Demon(target)){extra+=Math.Max(0,graph.Read(121));}
        if(GemCombat.Undead(target)){extra+=Math.Max(0,graph.Read(122)+(frame.SelectedBlunt?50:0));}
        ulong seed=Seed(true);SourceRandom.Limited(ref seed,100);
        int physical=NpcEquipmentDamage.Roll256(frame.Physical(0,1,extra),n=>(int)SourceRandom.Limited(ref seed,n));
        return (flags&8192)!=0?checked(physical*2):physical;
    }
    private static void Frame(AssassinShadowBody b)
    {IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);Require(b.WorldGround.State.Controller.Fault==null,"Own finisher ground fault: "+b.WorldGround.State.Controller.Fault);}
    private static void Ai(AssassinShadowBody b,PlayerMobile owner,BaseCreature target)
    {
        AssassinCharges.Clear(b);ShadowActions.Cancel(b);
        if(!ShadowGroundMotion.Active(b)){ShadowGroundMotion.Activate(b,owner,TimedEffects.Now.AddMinutes(25));}
        b.WorldGround.Store(IronGolemWorld.Park(b.WorldGround.State.Controller)with{Motion=DruidRavenMotion.Center(b.X*2,b.Y*2),Target=0});
        if(b.Prepared.Capture.Ability==CombatAbility.ShadowMaster)
        {
            // Exercise the exact world request consumer without replacing the
            // Master's full book/policy with a restricted synthetic skill list.
            foreach(int id in new[]{254,265})
            {
                NpcEquipmentRandom.Set(b,Seed(true));var at=TimedEffects.Now.AddMinutes(25);int hp=SkillHealth.Balance256(target);
                int frames=((INecromancerGroundBrain)b).GroundSkill(owner,5,id,target,at);
                Require(frames>0,"Master own world charge request refused");ShadowActions.Tick(at.AddMilliseconds(frames*40),true,b.Serial.Value);
                Require(ShadowChargeStrikes.Charge(b,id)?.Count==1&&SkillHealth.Balance256(target)<hp,"Master world charge request did not use own A2/contact");
            }
            return;
        }
        NpcEquipmentRandom.Set(b,SourceRandom.InitLow(1234));
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.TigerStrike)&&SkillBindings.Instance.Assign(owner,false,CombatAbility.TigerStrike),"Cannot bind Warrior Tiger selection");
        bool selected=false,consumed=false;int previous=Charges(b).Sum(s=>s.Count);
        for(int i=0;i<1000&&!(selected&&consumed);i++)
        {
            Frame(b);selected|=ShadowActions.State(b)?.Cast.Skill==254;
            int total=Charges(b).Sum(s=>s.Count);consumed|=total<previous;previous=total;
        }
        Require(selected&&consumed,"Warrior did not build/finish its selected Tiger family");
        // Current Cobra is AI category12; the unchanged older AI105 rejects
        // that category in melee. Preserve this reference conflict explicitly.
        Require(ItemSkillExecution.Skill(265).Number("aitype")==12,"Cobra source category changed");
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.CobraStrike)&&SkillBindings.Instance.Assign(owner,false,CombatAbility.CobraStrike),"Cannot bind Warrior Cobra reference case");
        for(int i=0;i<120;i++){Frame(b);Require(ShadowActions.State(b)?.Cast.Skill!=265,"Warrior silently bypassed source category12 admission");}
        Require(b.OwnedSkills.Find(265)!=null&&ShadowChargeStrikes.Charge(b,265)==null,"Warrior Cobra reference case did not retain its mirrored but unselected skill");
        ShadowActions.Cancel(b);
    }
    private static void Exercise(AssassinShadowBody b,PlayerMobile owner,int generation)
    {
        b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);AssassinCharges.Clear(b);
        b.ChangeSkills(book=>{book.Assign(254,3);book.Assign(265,3);});
        var target=new ShadowPsychicChecks.Witness{CasterId=b.Serial.Value};target.SetHits(65000);target.SetMana(500);
        foreach(var type in Enum.GetValues<ResistanceType>()){target.SetResistance(type,0);}target.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);target.AIObject.AITimer.Stop();TimedEffects.PreventHealing(target,TimedEffects.Now);
        try
        {
            var at=TimedEffects.Now.AddMinutes(20+generation);NecromancerEffects.SetMana256(target,250*256);SkillHealth.SetBalance256(b,b.MaximumLife256/2);
            int ownerMana=NecromancerEffects.ManaBalance256(owner),ownMana=NecromancerEffects.ManaBalance256(b);var items=ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray();
            NpcEquipmentRandom.Set(b,Seed(true));var ordinary=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at);var baseline=ordinary.Impact.Packet;ordinary.Cancel();
            Gain(b,254,3,at);Gain(b,265,3,at);var slots=Charges(b);var rng=NpcEquipmentRandom.Require(b);
            // An unsupported elemental slot remains an explicit, atomic gate.
            b.ChangeSkills(book=>book.Assign(259,3));AssassinCharges.Gain(b,ShadowSkillCasting.OwnValues(b,b.OwnedSkillRank(259),259),at);bool refused=false;
            try{_=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at);}catch(NotSupportedException e){refused=e.Message.Contains("elemental",StringComparison.Ordinal);}
            Require(refused&&NpcEquipmentRandom.Require(b)==rng&&AssassinCharges.State(b,CombatAbility.FistsOfFire)?.Count==1,"Unsupported elemental finisher mutated state/RNG");
            AssassinCharges.Clear(b);foreach(var slot in slots){Gain(b,slot.Ability==CombatAbility.TigerStrike?254:265,3,at);}
            NpcEquipmentRandom.Set(b,Seed(false));var missed=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at);
            Require((missed.Impact.Packet.ResultFlags&1)==0&&Json(Charges(b))==Json(slots),"Miss consumed own charges");missed.Cancel();
            NpcEquipmentRandom.Set(b,Seed(true));var edge=OriginalNpcMelee.Prepare(b,owner,target,bonus=>bonus>0,at);
            Require((edge.Impact.Packet.ResultFlags&1)!=0&&Json(Charges(b))==Json(slots),"Result-range hit incorrectly consumed outside tight progressive range");edge.Cancel();
            NpcEquipmentRandom.Set(b,Seed(true));var prepared=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at);var packet=prepared.Impact.Packet;
            var drain=AssassinCharges.CobraDrain(slots.Single(s=>s.Ability==CombatAbility.CobraStrike),b.OwnedSkillRank(265));
            Require(packet.Physical256==ExpectedPhysical(b,target,AssassinCharges.TigerPercent(slots.Single(s=>s.Ability==CombatAbility.TigerStrike),b.OwnedSkillRank(254)),packet.ResultFlags)&&packet.LifeDrain256==baseline.LifeDrain256+drain.LifePercent
                &&packet.ManaDrain256==baseline.ManaDrain256+drain.ManaPercent&&(packet.HitFlags&2)!=0,"Tiger/Cobra packet mismatch species="+b.Prepared.Capture.Ability+" baseline="+Json(baseline)+" charged="+Json(packet)+" drain="+Json(drain)+" slots="+Json(slots));
            Require(Charges(b).All(s=>s.Count==2&&s.AttackPercent==100)&&Charges(b).Length==2,"Successful allocation did not consume exactly one per family");
            int hp=SkillHealth.Balance256(target),life=SkillHealth.Balance256(b),mana=NecromancerEffects.ManaBalance256(target);
            int heal=NpcFlatDrains.Healing(packet.Physical256,mana,BattleOrders.Stamina256(target),b.MaximumLife256-life,packet.LifeDrain256,packet.ManaDrain256,packet.StaminaDrain256,GemCombat.Drain(target));
            var result=IronGolemContact.Release(b,owner,target,prepared,_=>true,at);
            Require(result.Commit!=null&&SkillHealth.Balance256(target)<hp&&SkillHealth.Balance256(b)==life+heal
                &&NecromancerEffects.ManaBalance256(target)==NpcFlatDrains.Remaining(mana,packet.ManaDrain256),"Actual NPC flat drains/HP/contact did not match shared receiver");
            Require(NecromancerEffects.ManaBalance256(b)==ownMana&&NecromancerEffects.ManaBalance256(owner)==ownerMana,"NPC Cobra used player percentage mana recovery");
            var current=Json(Charges(b));bool duplicate=false;try{_=IronGolemContact.Release(b,owner,target,prepared,_=>true,at);}catch(InvalidOperationException){duplicate=true;}
            Require(duplicate&&Json(Charges(b))==current,"Contact replay consumed another charge");
            // Full basic conductor, not only a direct prepared packet.
            NpcEquipmentRandom.Set(b,Seed(true));Require(ShadowActions.BeginMelee(b,target,at.AddSeconds(1)),"Own basic finisher entry refused");var action=ShadowActions.State(b);int release=action.Plan.Events.Single(e=>e.Type==1&&e.Kind==1).Frame;
            ShadowActions.Tick(at.AddMilliseconds(1000+(release-1)*40),true,b.Serial.Value);Require(Json(Charges(b))==current,"Basic finisher consumed before hit marker");
            ShadowActions.Tick(at.AddSeconds(3),true,b.Serial.Value);Require(Charges(b).All(s=>s.Count==1),"Basic conductor did not finish both own charge families");
            Require(ShadowLoadout.Originals(b).Select(i=>i.SavedText).SequenceEqual(items),"Finisher changed original inventory");
            Ai(b,owner,target);AssassinCharges.Clear(b);Gain(b,254,2,at);Gain(b,265,2,at);
        }
        finally{ShadowActions.Cancel(b);target.Delete();}
    }
    private static LoadoutGap[] Loadouts(PlayerMobile owner)
    {
        var game=NpcWorldAllocation.Capture();int count=0,lower=0;var gaps=new System.Collections.Generic.List<LoadoutGap>();
        try
        {
            foreach(int low in Enumerable.Range(1,32))
            foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster})
            {
                NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,(uint)low,SourceRandom.InitLow(low)));
                var b=AssassinShadowBody.CreateInitialized(owner,ability,TimedEffects.Now);BaseCreature target=null;
                try
                {
                    GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
                    b.ChangeSkills(book=>{book.Assign(254,3);book.Assign(265,3);});
                    target=new ShadowPsychicChecks.Witness{CasterId=b.Serial.Value};target.SetHits(65000);foreach(var type in Enum.GetValues<ResistanceType>()){target.SetResistance(type,0);}target.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);target.AIObject.AITimer.Stop();
                    var at=TimedEffects.Now.AddMinutes(20);NpcEquipmentRandom.Set(b,Seed(true));var baseline=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at);baseline.Cancel();
                    b.ChangeSkills(book=>book.Assign(280,3));Gain(b,254,3,at);Gain(b,265,3,at);NpcEquipmentRandom.Set(b,Seed(true));
                    int tiger=AssassinCharges.TigerPercent(AssassinCharges.State(b,CombatAbility.TigerStrike),b.OwnedSkillRank(254));
                    var cobra=AssassinCharges.CobraDrain(AssassinCharges.State(b,CombatAbility.CobraStrike),b.OwnedSkillRank(265));
                    var charged=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at);charged.Cancel();var a=baseline.Impact.Packet;var z=charged.Impact.Packet;
                    if(!(z.Physical256==ExpectedPhysical(b,target,tiger,z.ResultFlags)&&z.LifeDrain256==a.LifeDrain256+cobra.LifePercent&&z.ManaDrain256==a.ManaDrain256+cobra.ManaPercent))
                    {
                        File.WriteAllText(Path.Join(Core.BaseDirectory,"shadow-finisher-loadout-failure.json"),Json(new{Seed=low,Ability=ability,Body=Capture(b),Baseline=a,Charged=z,Cobra=cobra}));
                        throw new InvalidOperationException("Finisher loadout differential seed="+low+" ability="+ability+" baseline="+Json(a)+" charged="+Json(z)+" cobra="+Json(cobra));
                    }
                    if(z.Physical256<=a.Physical256){lower++;}count++;
                }
                catch(NotSupportedException e)when(e.Message.StartsWith("NPC item spell requires its own damage/execution adapter:",StringComparison.Ordinal))
                {gaps.Add(new(low,ability,e.Message));}
                finally{if(target!=null){target.Delete();}AssassinCharges.Clear(b);b.Delete();}
            }
        }
        finally{NpcWorldAllocation.Restore(game);}
        Require(count+gaps.Count==64&&count>=32,"Missing original finisher loadout samples");Log.Information("Own finisher loadout differential: {Count} admitted, {Gaps} explicitly unsupported original item-spell loadouts; {Lower} lower realized enhanced rolls correctly match source RNG",count,gaps.Count,lower);return gaps.ToArray();
    }
    private static void Check(bool reload)
    {
        PlayerMobile owner;AssassinShadowBody[] bodies;int generation=0;LoadoutGap[] loadouts=[];
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;loadouts=saved.Loadouts;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(saved.Bodies[i]),"Reload lost original finisher actor/charges/resources/wounds/RNG");}owner.MoveToWorld(new(6260,3680,0),Map.Trammel);
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3680);foreach(string name in new[]{"shadowwarrior","shadowmaster","tigerstrike","cobrastrike"}){ClassSkillChecks.Train(owner,"assassin."+name,name.StartsWith("shadow",StringComparison.Ordinal)?20:1);}
            bodies=new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}.Select(a=>AssassinShadowBody.CreateInitialized(owner,a,TimedEffects.Now)).ToArray();foreach(var b in bodies){GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);}
        }
        if(!reload){loadouts=Loadouts(owner);}
        foreach(var b in bodies){Exercise(b,owner,generation);PersistentMinions.Park(b);}
        GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,bodies.Select(Capture).ToArray(),generation,loadouts)));
        Require(!AssassinCombat.Implemented(CombatAbility.ShadowWarrior)&&!AssassinCombat.Implemented(CombatAbility.ShadowMaster),"Unfinished public Shadows enabled");
        Log.Information("Own Tiger/Cobra finishers:ordered prefill/postfill,raw fractional NPC drains,modern one-charge consumption,miss/tight-range/unsupported/replay guards,real basic conductor,Warrior Tiger AI/category12 refusal and Master own world request,original saved state through reloads;elemental finishers and other learned gates remain.");
    }
}
