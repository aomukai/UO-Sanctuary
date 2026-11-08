// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowFireBlastChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowFireBlastChecks));
    private sealed record Held(uint Actor,string Body,string[] Items,NpcEquipmentSeed Random,int Life);
    private sealed record Saved(uint Owner,uint Target,Held[] Bodies,FireBomb[] Bombs,int Health,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-fire-blast-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static Held Capture(AssassinShadowBody body)=>new(body.Serial.Value,body.SavedText,ShadowLoadout.Originals(body).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(body),SkillHealth.Balance256(body));
    public static void Configure(){Register("shadow-fire-blast-check",false);Register("shadow-fire-blast-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Intrinsic Shadow Fire Blast through shared startup/do/lob and NPC damage services",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static FireBomb Find(long id)=>FireBlasts.Snapshot().SingleOrDefault(b=>b.Id==id);
    private static FireBomb Cast(AssassinShadowBody body,BaseCreature target,DateTime at)
    {
        var owner=(PlayerMobile)body.ControlMaster;int mana=NecromancerEffects.ManaBalance256(owner),petMana=NecromancerEffects.ManaBalance256(body);
        var actor=NpcEquipmentRandom.Require(body);var game=NpcWorldAllocation.Capture();var stream=new SourceSeedStream(game.Seed);
        uint low=SourceRandom.Roll(ref stream.Value);var request=ShadowSkillCasting.Start(body,target,target.Location);
        Require(request!=null&&body.OwnedSkills.Used==251,"Real NPC selected/start context refused owned Fire Blast");
        long previous=FireBlasts.Snapshot().Select(b=>b.Id).DefaultIfEmpty(0).Max();
        Require(ShadowSkillCasting.Release(request,at)==1&&ShadowSkillCasting.Release(request,at)==0,"Common do dispatcher failed or replayed the action event");
        ShadowSkillCasting.Complete(request);
        var bomb=FireBlasts.Snapshot().Single(b=>b.Id>previous);
        Require(bomb.Npc is{} npc&&npc.Actor==body.Serial.Value&&npc.Binding==ShadowLoadout.Binding(body).Identity
            &&npc.Seed==SourceRandom.InitLow(unchecked((int)low))&&NpcWorldAllocation.Capture().Seed==stream.Value
            &&bomb.Rank==body.OwnedSkillRank(251)&&body.OwnedSkills.Used==null,"Intrinsic rank/source/child allocation or used-skill completion changed");
        Require(NpcEquipmentRandom.Require(body)==actor&&NecromancerEffects.ManaBalance256(owner)==mana&&NecromancerEffects.ManaBalance256(body)==petMana,"NPC startup/do borrowed player mana or actor RNG");
        return bomb;
    }
    private static FireBomb Land(FireBomb b)
    {
        for(int i=0;i<300;i++)
        {var current=Find(b.Id);Require(current!=null,"Bomb disappeared before shared landing");if(current.Ground){return current;}FireBlasts.Tick(current.NextUtc,true);}
        throw new InvalidOperationException("Lob never landed");
    }
    private static void Finish(FireBomb b)
    {for(int i=0;i<300&&Find(b.Id)is{} current;i++){FireBlasts.Tick(current.NextUtc,true);}Require(Find(b.Id)==null,"Lob did not expire");}
    private static void Live(AssassinShadowBody body,PlayerMobile owner,BaseCreature target)
    {
        var at=TimedEffects.Now.AddMinutes(10);var original=Capture(body);int health=SkillHealth.Balance256(target);
        var ground=Land(Cast(body,target,at));var values=ShadowSkillCasting.Values(body,ground.Rank);var graph=OriginalNpcEquipment.Graph(body);
        int Enhance(int damage)=>(int)((long)damage+(long)damage*graph.Read(329)/100);
        Require(ground.Min256==Enhance(values.Damage256(false))&&ground.Max256==Enhance(values.Damage256(true))&&ground.Potion==0
            &&ground.FireMastery==graph.Read(329)&&ground.Npc.FirePierce==graph.Read(333),"Landing borrowed owner damage or discarded own skill/gear capture");
        int raw=SorceressSpellMissiles.Roll(ground.Min256,ground.Max256,ground.Npc.Seed,out _);
        var mitigated=SkillCombat.MitigatePrepared(body,target,new(0,raw/256.0,0,0,0),out int absorb,pierceOverride:e=>e=="fire"?ground.Npc.FirePierce:0);
        Require(absorb==0,"Fixture unexpectedly absorbs fire");Finish(ground);
        Require(SkillHealth.Balance256(target)==health-NamedElements.To256(mitigated.Total),"Natural NPC explosion did not deliver its own source packet");
        SkillHealth.SetBalance256(target,health);
        var missed=Cast(body,target,at);var point=target.Location;target.MoveToWorld(new(point.X+10,point.Y,point.Z),target.Map);Finish(missed);
        Require(SkillHealth.Balance256(target)==health,"Lob chased a moving monster after release");target.MoveToWorld(point,body.Map);
        var offline=Cast(body,target,at);FireBlasts.Tick(offline.NextUtc,false);Finish(offline);
        Require(SkillHealth.Balance256(target)==health,"Offline NPC projectile replayed deferred damage");
        var before=NpcWorldAllocation.Capture();PersistentMinions.Park(body);
        Require(ShadowSkillCasting.Start(body,target,target.Location)==null&&NpcWorldAllocation.Capture()==before,"Parked Shadow cast or drew world RNG");
        body.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        Require(Json(Capture(body))==Json(original),"Intrinsic casting changed original body/items/wounds/actor RNG");
    }
    private static void Check(bool reload)
    {
        PlayerMobile owner;BaseCreature target;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);target=(BaseCreature)World.FindMobile((Serial)saved.Target);
            bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(saved.Bodies[i]),"Saved intrinsic caster/body/items/wounds/RNG changed");}
            Require(saved.Bombs.All(b=>FireBlasts.Snapshot().Contains(b))&&SkillHealth.Balance256(target)==saved.Health,"Saved NPC air/ground packets or target HP changed");
            owner.MoveToWorld(new(6220,3440,0),Map.Trammel);foreach(var body in bodies){body.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);}
            FireBlasts.Tick(saved.Bombs.Max(b=>b.NextUtc).AddSeconds(20),true);
            Require(!FireBlasts.Snapshot().Any(b=>saved.Bombs.Any(s=>s.Id==b.Id))&&SkillHealth.Balance256(target)==saved.Health,"Reload replayed NPC damage/actions or lost lifetime expiry");
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6220,24,3440);ClassSkillChecks.Train(owner,"assassin.fireblast",20);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);
            bodies=new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}.Select(a=>AssassinShadowBody.CreateInitialized(owner,a,TimedEffects.Now)).ToArray();
            foreach(var body in bodies)
            {
                if(body.Prepared.Capture.Ability==CombatAbility.ShadowWarrior){body.ChangeSkills(book=>book.Mirror(body.Prepared.Capture,251,0));}
                GeneratedNpcLiveResources.ActivateItemEvents(body);ShadowEquipmentDefense.Activate(body);
            }
            var ranks=bodies.Select(b=>b.OwnedSkillRank(251)).ToArray();var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);owner.AddToBackpack(helm);Require(owner.EquipItem(helm),"Owner soft rank helm");ClassGear.Refresh(owner);
            Require(bodies.Select(b=>b.OwnedSkillRank(251)).SequenceEqual(ranks),"Post-summon owner gear rewrote intrinsic NPC ranks");NecromancerEffects.SetMana256(owner,0);
            foreach(var body in bodies){body.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);}
            target=new ValkyrieContactChecks.Recipient();target.SetHits(65000);target.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);target.AIObject.AITimer.Stop();
        }
        foreach(var body in bodies){Live(body,owner,target);}
        var held=FireBlasts.Snapshot();bool refused=false;
        try{FireBlasts.Restore(held.Append(new FireBomb(999999,owner.Serial.Value,owner.Map.MapID,1,owner.X,owner.Y,owner.Z,0,0,1,true,TimedEffects.Now){Npc=new(owner.Serial.Value,"bad",1)}).ToArray());}
        catch(InvalidDataException){refused=true;}
        Require(refused&&FireBlasts.Snapshot().SequenceEqual(held),"Malformed NPC batch partially replaced the shared bomb pool");
        var pending=bodies.Select((b,i)=>Land(Cast(b,target,TimedEffects.Now.AddMinutes(25-i)))).Concat(bodies.Select((b,i)=>Cast(b,target,TimedEffects.Now.AddMinutes(30+i)))).ToArray();
        Require(pending.All(b=>Find(b.Id)==b),"Sibling lob advancement changed held air/ground fixtures before save");
        PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());
        File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,target.Serial.Value,bodies.Select(Capture).ToArray(),pending,SkillHealth.Balance256(target),generation)));

        Log.Information("Shadow Fire Blast:both intrinsic skill books through common startup/do,own rank/synergy/item packets,exact child/actor RNG,no NPC/owner mana,lob landing/explosion/moving miss,offline/reload no replay,atomic shared-pool restore and original saved bodies;animation/AI/learned routes remain gated");
    }
}
