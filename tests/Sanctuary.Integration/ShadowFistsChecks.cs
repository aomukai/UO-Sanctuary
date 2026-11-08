// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowFistsChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowFistsChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,MartialCharge[] Charges,FirePatch[] Fields);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-fists-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static MartialCharge[] Charges(AssassinShadowBody b)=>AssassinCharges.Snapshot().Where(c=>c.Owner==b.Serial.Value).ToArray();
    private static FirePatch[] Fields(AssassinShadowBody b)=>FireGround.Snapshot().Patches.Where(p=>p.PhoenixNpc?.Actor==b.Serial.Value).OrderBy(p=>p.Id).ToArray();
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),Charges(b),Fields(b));
    public static void Configure(){Register("shadow-fists-check",false);Register("shadow-fists-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own Shadow Fists inline/burst/fields and original persistence",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Clear(AssassinShadowBody b)
    {
        ShadowActions.Cancel(b);AssassinCharges.Clear(b);var all=FireGround.Snapshot();FireGround.Restore(all with{Patches=all.Patches.Where(p=>p.PhoenixNpc?.Actor!=b.Serial.Value).ToArray()});
    }
    private static ulong Seed(bool hit)
    {for(int i=1;i<10000;i++){ulong seed=SourceRandom.InitLow(i),copy=seed;uint r=SourceRandom.Limited(ref copy,100);if(hit?r<5:r>=95){return seed;}}throw new InvalidOperationException("Missing bounded hit seed");}
    private static MartialCharge Charge(AssassinShadowBody b,int count,DateTime at)
    {
        AssassinCharges.Clear(b);var v=ShadowSkillCasting.OwnValues(b,b.OwnedSkillRank(259),259);
        for(int i=0;i<count;i++){AssassinCharges.Gain(b,v,at);}return AssassinCharges.State(b,CombatAbility.FistsOfFire);
    }
    private static ShadowPsychicChecks.Witness Target(AssassinShadowBody b,int dx=1,int dy=0,int life=65000)
    {
        var t=new ShadowPsychicChecks.Witness{CasterId=b.Serial.Value};t.SetHits(life);foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}
        t.MoveToWorld(new(b.X+dx,b.Y+dy,b.Z),b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static void Contact(AssassinShadowBody b,PlayerMobile owner,int count,DateTime at,bool immune=false,bool lethal=false)
    {
        Clear(b);var target=Target(b,life:lethal?1:65000);var side=Target(b,1,1);
        try
        {
            if(immune){target.SetResistance(ResistanceType.Fire,100);side.SetResistance(ResistanceType.Fire,100);}
            NpcEquipmentRandom.Set(b,Seed(true));var baseline=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at);var normal=baseline.Impact.Packet;var afterFill=NpcEquipmentRandom.Require(b);baseline.Cancel();
            var charge=Charge(b,count,at);var values=ShadowSkillCasting.OwnValues(b,Math.Max(charge.InitialRank,b.OwnedSkillRank(259)),259);ulong seed=afterFill.Seed;
            var expected=FistsFireRelease.Capture(values,count,b.EquipmentNumbers.Read(329),n=>(int)SourceRandom.Limited(ref seed,n));
            int converted=Math.Min(SourcePercentage.Calculate(normal.Physical256,expected.ConversionPercent,100),normal.Physical256);
            int hp=SkillHealth.Balance256(target),near=SkillHealth.Balance256(side);NpcEquipmentRandom.Set(b,Seed(true));var prepared=OriginalNpcMelee.Prepare(b,owner,target,_=>true,at);var packet=prepared.Impact.Packet;
            Require(packet.Physical256==normal.Physical256-converted&&packet.Fire256==(immune?0:normal.Fire256+expected.Inline256+converted)
                &&(packet.ResultFlags&16384)!=0&&NpcEquipmentRandom.Require(b).Seed==seed,"Own Fists post-fill element/conversion/mastery/RNG differs");
            Require(SkillHealth.Balance256(target)==hp&&SkillHealth.Balance256(side)==near&&Fields(b).Length==0
                &&(AssassinCharges.State(b,CombatAbility.FistsOfFire)?.Count??0)==count-1,"Own Fists applied before parent or consumed wrong charge count");
            bool refused=false;if(count>=2){try{_=IronGolemContact.Capture(prepared);}catch(InvalidOperationException){refused=true;}Require(refused,"Transient progressive callback was silently omitted from saved contact");}
            _=NpcWorldAllocation.RequireStream();var world=NpcWorldAllocation.Capture();ulong worldSeed=world.Seed;int[] attempts=[];
            if(count>=2){_=SourceRandom.Limited(ref seed,Math.Max(0,expected.Max256-expected.Min256));}
            if(count==3)
            {
                int low=unchecked((int)SourceRandom.Roll(ref seed));attempts=FistsFireRelease.Scatter(expected.ScatterRadius,low).SelectMany(p=>new[]{p.X,p.Y}).ToArray();
                for(int i=0;i<attempts.Length/2;i++){SourceRandom.Roll(ref worldSeed);}
            }
            var result=IronGolemContact.Release(b,owner,target,prepared,_=>true,at);
            Require(result.Commit!=null&&prepared.AfterContact==null&&(lethal?!target.Alive:SkillHealth.Balance256(target)<hp),"Own Fists parent contact did not complete");
            Require(count==1?SkillHealth.Balance256(side)==near:immune?SkillHealth.Balance256(side)==near:SkillHealth.Balance256(side)<near,"Copied Fists burst missing,wrong tier or ignored immunity");
            // No relevant item callbacks on the declared seed1 original loadout.
            Require(NpcEquipmentRandom.Require(b).Seed==seed&&NpcWorldAllocation.Capture().Seed==worldSeed,"Fists burst/scatter caster/world draw order differs");
            var fields=Fields(b);Require(fields.Length==attempts.Length/2&&fields.All(p=>p.Ability==CombatAbility.FistsOfFire&&p.PhoenixNpc.Actor==b.Serial.Value
                &&p.PhoenixNpc.Binding==OriginalNpcEquipment.Binding(b)&&p.Owner==owner.Serial.Value&&p.Min256==expected.FieldMin256&&p.Max256==expected.FieldMax256
                &&p.Seed==p.PhoenixNpc.UnitSeed&&p.Size==2&&p.DamageRate==0&&p.UntilUtc==at.AddMilliseconds(64*40)&&p.NextUtc==at.AddMilliseconds(40)),"Fists child identity/endpoints/seed/lifetime differs");
            Require(fields.SelectMany(p=>new[]{(int)Math.Round((p.X-target.X)*2),(int)Math.Round((p.Y-target.Y)*2)}).SequenceEqual(attempts),"Fists local scatter lost order or duplicate positions");
            var rng=NpcEquipmentRandom.Require(b);bool duplicate=false;try{_=IronGolemContact.Release(b,owner,target,prepared,_=>true,at);}catch(InvalidOperationException){duplicate=true;}
            Require(duplicate&&NpcEquipmentRandom.Require(b)==rng,"Duplicate contact replayed burst/fields");
        }
        finally{Clear(b);target.Corpse?.Delete();target.Delete();side.Delete();}
    }
    private static void FieldsAndEdges(AssassinShadowBody b,PlayerMobile owner,DateTime at)
    {
        Clear(b);var t=Target(b);try
        {
            var state=Charge(b,3,at);NpcEquipmentRandom.Set(b,Seed(false));var miss=OriginalNpcMelee.Prepare(b,owner,t,_=>true,at);
            Require((miss.Impact.Packet.ResultFlags&1)==0&&miss.AfterContact==null&&AssassinCharges.State(b,CombatAbility.FistsOfFire)==state&&Fields(b).Length==0,"Miss emitted/consumed Fists");miss.Cancel();
            NpcEquipmentRandom.Set(b,Seed(true));var edge=OriginalNpcMelee.Prepare(b,owner,t,bonus=>bonus>0,at);Require(edge.AfterContact==null&&AssassinCharges.State(b,CombatAbility.FistsOfFire)==state,"Loose-range contact scheduled Fists release");edge.Cancel();
            NpcEquipmentRandom.Set(b,Seed(true));var canceled=OriginalNpcMelee.Prepare(b,owner,t,_=>true,at);canceled.Cancel();Require(canceled.AfterContact==null&&Fields(b).Length==0,"Canceled transient packet retained its callback");
            Clear(b);var source=new PhoenixNpcSource(b.Serial.Value,OriginalNpcEquipment.Binding(b),NpcWorldAllocation.UnitSeed(),0);FireGround.AddShadowFists(b,3,t.X,t.Y,t.Z,256,512,source,at);
            var patch=Fields(b).Single();ulong seed=patch.Seed;int raw=WeaponMissileDamage.RollValue(256,512,0,n=>(int)SourceRandom.Limited(ref seed,n));var caster=NpcEquipmentRandom.Require(b);var world=NpcWorldAllocation.Capture();int hp=SkillHealth.Balance256(t);
            FireGround.Tick(at.AddMilliseconds(40),true,b.Serial.Value);var next=Fields(b).Single();Require(next.PhoenixNpc.Fault==null&&next.Seed==seed&&hp-SkillHealth.Balance256(t)==raw
                &&NpcEquipmentRandom.Require(b)==caster&&NpcWorldAllocation.Capture()==world,"Fists field typed damage/private one-draw seed or source separation failed");
            // Exactly63 damaging updates; expiry at64 precedes collision.
            int remaining=0;for(int i=1;i<63;i++){remaining+=WeaponMissileDamage.RollValue(256,512,0,n=>(int)SourceRandom.Limited(ref seed,n));}
            hp=SkillHealth.Balance256(t);FireGround.Tick(at.AddMilliseconds(64*40),true,b.Serial.Value);Require(Fields(b).Length==0&&hp-SkillHealth.Balance256(t)==remaining,"Fists field failed63damaging updates/64frame expiry");
            FireGround.AddShadowFists(b,3,t.X,t.Y,t.Z,256,512,source,at);hp=SkillHealth.Balance256(t);caster=NpcEquipmentRandom.Require(b);FireGround.Tick(at.AddSeconds(3),false,b.Serial.Value);
            Require(Fields(b).Length==0&&SkillHealth.Balance256(t)==hp&&NpcEquipmentRandom.Require(b)==caster,"Offline Fists field fought or failed to expire");
            var all=FireGround.Snapshot();FireGround.AddShadowFists(b,3,t.X,t.Y,t.Z,256,512,source,at);var good=FireGround.Snapshot();bool bad=false;
            try{FireGround.Restore(good with{Patches=good.Patches.Select(p=>p.PhoenixNpc?.Actor==b.Serial.Value?p with{PhoenixNpc=p.PhoenixNpc with{Actor=owner.Serial.Value}}:p).ToArray()});}catch(InvalidDataException){bad=true;}
            Require(bad&&Json(FireGround.Snapshot())==Json(good),"Malformed shared NPC field restore replaced existing pools");FireGround.Restore(all);
        }
        finally{Clear(b);t.Delete();}
    }
    private static void WorldAction(AssassinShadowBody b,PlayerMobile owner,DateTime at)
    {
        Clear(b);var t=Target(b);try
        {
            int frames=((INecromancerGroundBrain)b).GroundSkill(owner,14,259,t,at);Require(frames>0,"Own mode14 world Fists request refused");
            while(ShadowActions.State(b) is{} pending){NpcEquipmentRandom.Set(b,Seed(true));ShadowActions.Tick(pending.NextUtc,true,b.Serial.Value);}
            Require(AssassinCharges.State(b,CombatAbility.FistsOfFire)?.Count>=1,"Own world sequence did not build Fists");
            Charge(b,3,at);NpcEquipmentRandom.Set(b,Seed(true));Require(ShadowActions.BeginMelee(b,t,at.AddSeconds(2)),"Own basic Fists finisher entry refused");
            frames=ShadowActions.State(b).Plan.Events.Single(e=>e.Type==2).Frame;ShadowActions.Tick(at.AddMilliseconds(2000+frames*40),true,b.Serial.Value);
            Require(Fields(b).Length>0&&AssassinCharges.State(b,CombatAbility.FistsOfFire)?.Count==2,"Own basic clock did not release Fists/consume one");
            Clear(b);
            if(b.Prepared.Capture.Ability==CombatAbility.ShadowWarrior)
            {
                if(!ShadowGroundMotion.Active(b)){ShadowGroundMotion.Activate(b,owner,at.AddMinutes(1));}
                Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.FistsOfFire)&&SkillBindings.Instance.Assign(owner,false,CombatAbility.FistsOfFire),"Fists Warrior selection refused");
                NpcEquipmentRandom.Set(b,SourceRandom.InitLow(1234));bool builder=false,emitted=false;
                for(int i=0;i<800&&!(builder&&emitted);i++)
                {IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);Require(b.WorldGround.State.Controller.Fault==null,"Fists Warrior world fault: "+b.WorldGround.State.Controller.Fault);builder|=ShadowActions.State(b)?.Cast.Skill==259;emitted=Fields(b).Length>0;}
                Require(builder&&emitted,"Natural Warrior AI did not build/release Fists");Clear(b);
            }
        }
        finally{Clear(b);t.Delete();}
    }
    private static void Check(bool reload)
    {
        PlayerMobile owner;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++)
            {
                var actual=Capture(bodies[i]);var expected=saved.Bodies[i] with{Fields=saved.Bodies[i].Fields.Select(p=>p with{PhoenixNpc=p.PhoenixNpc with{Dormant=true}}).ToArray()};
                Require(Json(actual)==Json(expected)&&ShadowActions.State(bodies[i])==null,"Reload lost original body/resources/charges/fields or reset deadlines");Clear(bodies[i]);
            }
            owner.MoveToWorld(new(6260,3560,0),Map.Trammel);
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3560);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);ClassSkillChecks.Train(owner,"assassin.fistsoffire",1);var world=NpcWorldAllocation.Capture();
            try{bodies=new AssassinShadowBody[2];int i=0;foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}){NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));bodies[i]=AssassinShadowBody.CreateInitialized(owner,ability,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(bodies[i]);ShadowEquipmentDefense.Activate(bodies[i]);i++;}}finally{NpcWorldAllocation.Restore(world);}
        }
        foreach(var b in bodies)
        {
            b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);b.ChangeSkills(book=>book.Assign(259,3));var at=TimedEffects.Now.AddMinutes(15+generation);int mana=NecromancerEffects.ManaBalance256(owner);var items=ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray();
            for(int count=1;count<=3;count++){Contact(b,owner,count,at.AddSeconds(count));}Contact(b,owner,3,at.AddSeconds(4),immune:true);Contact(b,owner,3,at.AddSeconds(5),lethal:true);
            FieldsAndEdges(b,owner,at.AddSeconds(6));WorldAction(b,owner,at.AddSeconds(10));
            Require(NecromancerEffects.ManaBalance256(owner)==mana&&ShadowLoadout.Originals(b).Select(i=>i.SavedText).SequenceEqual(items),"Own Fists borrowed owner mana or altered original inventory");
            // Preserve a naturally short field's real absolute deadline but
            // place its start fifteen minutes ahead for the two reloads.
            Charge(b,3,at);var source=new PhoenixNpcSource(b.Serial.Value,OriginalNpcEquipment.Binding(b),NpcWorldAllocation.UnitSeed(),0);
            FireGround.AddShadowFists(b,3,b.X+1,b.Y,b.Z,256,512,source,at);SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);PersistentMinions.Park(b);
        }
        GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,bodies.Select(Capture).ToArray(),generation)));
        Require(!AssassinCombat.Implemented(CombatAbility.ShadowWarrior)&&!AssassinCombat.Implemented(CombatAbility.ShadowMaster),"Fists enabled unfinished learned Shadows");
        Log.Information("Own Fists:inline post-fill conversion/SOFTHIT,actual parent before burst and independent scatter/field seeds,death/immunity/miss/range/cancel/duplicates,63field hits/64frame expiry,world sequence/basic action,atomic restore and original saved source/items/resources/wounds/RNG/charges/fields through two reloads. Other intrinsic/modern/learned gates remain.");
    }
}
