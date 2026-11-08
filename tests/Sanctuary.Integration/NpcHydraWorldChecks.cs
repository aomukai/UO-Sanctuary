// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcHydraWorldChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcHydraWorldChecks));
    private sealed record Saved(uint Owner,uint Caster,uint MissingCaster,uint Target,int Life,HydraHead[] Heads,HydraBolt[] Bolts,NpcEquipmentSeed Random,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-hydra-world-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool v,string why){if(!v){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true}){string name=reload?"npc-hydra-world-reload-check":"npc-hydra-world-check";ConsoleInputHandler.RegisterCommand(name,"Actual NPC-owned Hydra heads/AI/bolts/credit and prepared removal proposal",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static PlayerMobile Owner()=>BladeBodyChecks.Fresh(6560,40,3570);
    private static AssassinShadowBody Caster(PlayerMobile p)
    {
        ClassSkillChecks.Train(p,"assassin.shadowmaster",20);var world=NpcWorldAllocation.Capture();AssassinShadowBody b;
        try{NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,9,SourceRandom.InitLow(9)));b=AssassinShadowBody.CreateInitialized(p,CombatAbility.ShadowMaster,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);}
        finally{NpcWorldAllocation.Restore(world);}b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);return b;
    }
    private static ShadowTailChecks.Witness Target(PlayerMobile p)
    {var t=new ShadowTailChecks.Witness();t.SetHits(65000);t.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static HydraHead[] Heads(PlayerMobile p)=>Hydras.Snapshot().Heads.Where(h=>h.Owner==p.Serial.Value).ToArray();
    private static HydraBolt[] Bolts(PlayerMobile p)=>Hydras.Snapshot().Bolts.Where(b=>b.Owner==p.Serial.Value).ToArray();
    private static int Cast(AssassinShadowBody b,PlayerMobile p,DateTime at,int mask=7,bool deterministic=false)
    {
        var factory=Hydras.PreparedNpcFactory(b,at,r=>(mask&(1<<r.Index))==0?new(false,0):new(true,deterministic?SourceRandom.InitLow(1):NpcWorldAllocation.UnitSeed()));
        var frame=NpcItemSpellCasts.PreparedHydraFrame(b,at,factory);using var protect=NpcItemSpellCasts.PreserveCombatTarget(b);
        return ItemEffectCasts.Position(frame,62,3,p.X+9,p.Y,1);
    }
    private static void Clean(PlayerMobile p,AssassinShadowBody caster,BaseCreature target)
    {Hydras.Cancel(p);if(caster is{Deleted:false}){PersistentMinions.Park(caster);}caster?.Delete();target?.Corpse?.Delete();target?.Delete();p.Delete();}
    private static void Creation()
    {
        var p=Owner();var b=Caster(p);var at=TimedEffects.Now.Add(FixtureSpace.Retention);var seed=NpcEquipmentRandom.Require(b);string body=b.SavedText;
        int mana=NecromancerEffects.ManaBalance256(p);_=NpcWorldAllocation.RequireStream();var before=NpcWorldAllocation.Capture();ulong expected=before.Seed;
        try
        {
            Require(Cast(b,p,at,0)==0&&Heads(p).Length==0&&NpcWorldAllocation.Capture()==before,"Failed NPC allocation created heads/used seeds");
            for(int i=0;i<7;i++){Require(Cast(b,p,at)==1,"NPC owned Hydra cast refused");for(int j=0;j<3;j++){SourceRandom.Roll(ref expected);}}
            var heads=Heads(p);Require(heads.Length==21&&heads.GroupBy(h=>h.Group).All(g=>g.Count()==3)&&NpcWorldAllocation.Capture().Seed==expected,"NPC heads entered player18-head FIFO or wrong world allocation");
            Require(heads.All(h=>h.Source.NpcOwner is{} n&&n.Caster==b.Serial.Value&&n.Binding==b.Prepared.Weapons.Identity
                &&World.FindMobile((Serial)h.Source.Actor) is HydraBody live&&live.SummonMaster==b&&live.GetDamageMaster(b)==p&&live.BlocksFit==false&&!live.CanBeDamaged()),"Actual head lost NPC owner/root credit or untargetable/nonblocking identity");
            Hydras.Restore(Hydras.Snapshot());Hydras.ValidateWorld();Require(Heads(p).Length==21,"NPC21-head saved roster rejected");
            var save=Hydras.Snapshot();var first=heads[0];try{Hydras.Restore(save with{Heads=save.Heads.Select(h=>h.Id==first.Id?h with{Source=h.Source with{NpcOwner=h.Source.NpcOwner with{Caster=p.Serial.Value}}}:h).ToArray()});throw new Exception("Malformed NPC ownership accepted");}catch(InvalidDataException){}
            Require(Json(Hydras.Snapshot())==Json(save)&&heads.All(h=>World.FindMobile((Serial)h.Source.Actor)!=null),"Malformed restore damaged original head graph");
            Require(NpcEquipmentRandom.Require(b)==seed&&b.SavedText==body&&NecromancerEffects.ManaBalance256(p)==mana,"NPC factory spent actor RNG/player mana or rewrote source state");
        }
        finally{Clean(p,b,null);}
    }
    private static void Damage(bool proposal)
    {
        var p=Owner();var b=Caster(p);var target=Target(p);var at=TimedEffects.Now.Add(FixtureSpace.Retention);uint parent=b.Serial.Value;
        try
        {
            Require(Cast(b,p,at,deterministic:true)==1,"NPC damage heads refused");var heads=Heads(p);var birth=heads.Select(h=>h.Source.Birth).ToArray();
            if(proposal){b.Kill();Require(b.Retiring,"Approved death did not retain actual NPC");b.AdvanceDeath(b.DeathState.ExpiryUtc,false);Require(b.Deleted&&World.FindMobile((Serial)parent)==null,"Proposal parent was not removed");}
            Hydras.Advance(p,at.AddMilliseconds(35*40),true);Require(Bolts(p).Length==3&&Bolts(p).All(x=>x.Frame==2)&&Heads(p).All(h=>h.Source.Control.Dispatches==1),"NPC own AI/action/partial bolt did not run");
            int hp=SkillHealth.Balance256(target);Hydras.Advance(p,at.AddMilliseconds(49*40),true);Require(SkillHealth.Balance256(target)<hp,"Actual NPC-owned Hydra bolts failed typed HP");
            Require(Heads(p).Select(h=>h.Source.Birth).SequenceEqual(birth)&&Heads(p).All(h=>h.Source.Fault==null),"NPC head rewrote snapshot or faulted while supported/proposed");
            SkillHealth.SetBalance256(target,256);Hydras.Advance(p,at.AddSeconds(5),true);Require(!target.Alive&&heads.Any(h=>h.Source.Actor==target.LastKiller?.Serial.Value)&&ClassLoot.Killer(target)==p,"NPC head lost actual attacker/root credit");
            if(proposal){Require(Heads(p).All(h=>h.Source.NpcOwner!=null)&&World.FindMobile((Serial)parent)==null,"Proposal fabricated parent/player caster");}
            Hydras.Advance(p,at.AddSeconds(13),false);Hydras.Advance(p,at.AddSeconds(14),false);Require(Heads(p).Length==0&&Bolts(p).Length==0&&heads.All(h=>World.FindMobile((Serial)h.Source.Actor)==null),"NPC Hydra expiry retained heads/bolts");
        }
        finally{Clean(p,b,target);}
    }
    private static void Automatic()
    {
        var p=Owner();var b=Caster(p);var target=Target(p);var at=TimedEffects.Now.Add(FixtureSpace.Retention);
        try
        {
            var graph=NpcOriginalItemProcs.RequireHost(b);
            var property=graph.Stats.First(v=>v.Value>0&&((ushort)v.Packed>>6)==62&&ItemEventDefinitions.Bindings(v.Stat)?.Any(e=>e.Function is 20 or 21)==true);
            var entry=ItemEventDefinitions.Rows.Value[property.Stat].Events.First(e=>e.Function is 20 or 21);
            int mana=NecromancerEffects.ManaBalance256(p);bool accepted=false;var original=b.Combatant;
            for(int i=1;i<=100&&Heads(p).Length==0;i++)
            {
                NpcEquipmentRandom.Set(b,SourceRandom.InitLow(i));
                NpcOriginalItemProcs.Dispatch(b,target,entry.Event,new(256,256,0,0,5,HitFlags:32),at,
                    request=>NpcWorldAllocation.Root(b,request,at),observed:v=>accepted|=((ushort)v.Packed>>6)==62&&v.Result==1,firstPathPoint:new(p.X+9,p.Y,p.Z));
            }
            Require(accepted&&Heads(p).Length==3&&Heads(p).All(h=>h.Rank==((ushort)property.Packed&63)&&h.Source.NpcOwner.Caster==b.Serial.Value)
                &&b.Combatant==original&&NecromancerEffects.ManaBalance256(p)==mana,"Real registered Hydra item callback did not create actual NPC-owned heads/restore/free resources");
            Hydras.Cancel(p);
            var frame=NpcItemSpellCasts.OriginalFrame(b,at,r=>NpcWorldAllocation.Root(b,r,at));
            Require(ItemEffectCasts.Position(frame,62,3,0,0,1)==0&&Heads(p).Length==0,"Automatic failed start allocated/threw");
        }
        finally{Clean(p,b,target);}
    }
    private static void Save(PlayerMobile p,AssassinShadowBody b,uint missing,BaseCreature target,int generation)
    {
        var held=Hydras.Snapshot();var until=TimedEffects.Now.Add(FixtureSpace.Retention);
        Hydras.Restore(held with{Heads=held.Heads.Select(h=>h.Owner==p.Serial.Value?h with{UntilUtc=h.UntilUtc+(until-h.Source.NextUtc),Source=h.Source with{NextUtc=until}}:h).ToArray(),
            Bolts=held.Bolts.Select(x=>x.Owner==p.Serial.Value?x with{UntilUtc=x.UntilUtc+(until-x.NextUtc),NextUtc=until}:x).ToArray()});
        var saved=new Saved(p.Serial.Value,b.Serial.Value,missing,target.Serial.Value,SkillHealth.Balance256(target),Heads(p),Bolts(p),NpcEquipmentRandom.Require(b),generation);
        PersistentMinions.Park(b);p.Internalize();File.WriteAllText(Manifest,Json(saved));
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;AssassinShadowBody b;BaseCreature target;uint missing;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);b=(AssassinShadowBody)World.FindMobile((Serial)saved.Caster);target=(BaseCreature)World.FindMobile((Serial)saved.Target);missing=saved.MissingCaster;generation=saved.Generation+1;
            Require(Heads(p).SequenceEqual(saved.Heads)&&Bolts(p).SequenceEqual(saved.Bolts)&&NpcEquipmentRandom.State(b)==saved.Random&&World.FindMobile((Serial)missing)==null&&SkillHealth.Balance256(target)==saved.Life,"Reload changed original NPC-owned graph/packets/RNG or fabricated missing caster");Hydras.ValidateWorld();
            p.MoveToWorld(new(6560,3570,0),Map.Trammel);b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);target.Frozen=true;target.CantWalk=true;target.AIObject.AITimer.Stop();
            int hp=SkillHealth.Balance256(target);var next=Heads(p)[0].Source.NextUtc;Hydras.Advance(p,next,false);Require(SkillHealth.Balance256(target)==hp,"Reload/inactive step replayed past damage");Hydras.Advance(p,next.AddSeconds(2),true);Require(SkillHealth.Balance256(target)<hp,"Saved real/orphan NPC Hydra did not resume delivery");
        }
        else
        {
            p=Owner();b=Caster(p);target=Target(p);var orphan=Caster(p);missing=orphan.Serial.Value;var at=TimedEffects.Now.Add(FixtureSpace.Retention);
            Require(Cast(b,p,at,deterministic:true)==1&&Cast(orphan,p,at,deterministic:true)==1,"Saved NPC Hydra casts");Hydras.Advance(p,at.AddMilliseconds(35*40),true);Require(Bolts(p).Length==6&&Bolts(p).All(x=>x.Frame==2),"Saved NPC bolts not partially travelled");PersistentMinions.Park(orphan);orphan.Delete();
        }
        Save(p,b,missing,target,generation);Creation();Damage(false);Damage(true);Automatic();Require(!AssassinCombat.Implemented(CombatAbility.ShadowMaster),"Prepared/proposed Hydra enabled learned Master");
        Log.Information("NPC Hydra world:actual NPC summoner/root credit,21heads/no player FIFO,private world/head/bolt seeds,source controller and travelling typed damage,kill credit/expiry,atomic graph restore and original real/orphan proposal saves pass. Approved parent-independent lifetime is standard;automatic item62 metadata/start/do is connected for qualified immunity-pierce-zero originals.");
    }
}
