// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowShockWebChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowShockWebChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life);
    private sealed record Saved(uint Owner,uint Target,int Health,Held[] Bodies,ShockShard[] Shards,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-shock-web-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b));
    public static void Configure(){Register("shadow-shock-web-check",false);Register("shadow-shock-web-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own Shadow Shock Web field,leases,soft hit and saves;action/cooldown coverage is in the shared S2 cohort",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static BaseCreature Target(AssassinShadowBody b,int life=65000)
    {
        var t=new ShadowPsychicChecks.Witness{CasterId=b.Serial.Value};t.SetHits(life);foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}
        t.MoveToWorld(new(b.X+5,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static ShockShard[] Emit(AssassinShadowBody b,Point3D aim,DateTime at)
    {
        b.ChangeSkills(book=>book.Use(256));var before=ShockWebs.Snapshot();var random=NpcEquipmentRandom.Require(b);var world=NpcWorldAllocation.Capture();ulong expected=world.Seed;
        int count=ShockWebs.EmitShadow(b,aim,b.OwnedSkillRank(256),at);b.ChangeSkills(book=>book.Use(null));
        var emitted=ShockWebs.Snapshot().Shards.Where(s=>s.Id>before.NextId).ToArray();
        var values=ShadowSkillCasting.OwnValues(b,b.OwnedSkillRank(256),256);var scatter=ShockWebs.Scatter(b.X*2,b.Y*2,aim.X*2,aim.Y*2,ShockWebs.Count(values),(int)values.Eval("aurarangecalc"));
        Require(count==emitted.Length&&count==scatter.Length&&count>1,"Wrong own count/scatter or emitted no real projectiles");
        for(int i=0;i<count;i++)
        {
            uint low=SourceRandom.Roll(ref expected);var s=emitted[i];int dx=scatter[i].X-b.X*2,dy=scatter[i].Y-b.Y*2;var velocity=MissileVectors.VelocityFp8(dx,dy,LobProjectiles.Velocity);
            Require(s.Cast==before.NextCast+1&&s.Id==before.NextId+i+1&&s.Npc.Actor==b.Serial.Value&&s.Npc.Binding==OriginalNpcEquipment.Binding(b)
                &&s.Npc.Seed==SourceRandom.InitLow(unchecked((int)low))&&!s.Ground&&s.Min256==0&&s.Max256==0&&s.Potion==0
                &&s.X==b.X&&s.Y==b.Y&&s.Vx==velocity.X&&s.Vy==velocity.Y&&s.Remaining==LobProjectiles.Lifetime(dx,dy),"Own lob geometry/identity/world allocation mismatch");
        }
        Require(NpcWorldAllocation.Capture().Seed==expected&&NpcEquipmentRandom.Require(b)==random,"Scatter used actor RNG or allocated wrong number of child units");return emitted;
    }
    private static ShockShard Land(ShockShard s)
    {
        for(int i=0;i<300&&!s.Ground;i++){s=ShadowShockWebs.Advance(s,true,false);Require(s!=null,"Own field disappeared before landing");}
        Require(s is{Ground:true,Remaining:90,Vx:0,Vy:0},"Own lob did not land naturally");return s;
    }
    private static void Live(AssassinShadowBody b,PlayerMobile owner)
    {
        var target=Target(b);var at=TimedEffects.Now.AddMinutes(10);var saved=Capture(b);int mana=NecromancerEffects.ManaBalance256(owner),petMana=b.EquipmentNumbers.Read(8);
        CombatPotions.Apply(owner,3,at);Require(CombatPotions.BonusAt(owner,"magical-penetration",at)>0,"Potion exclusion fixture");
        try
        {
            int hp=SkillHealth.Balance256(target);var aim=target.Location;var first=Emit(b,aim,at)[0];Require(SkillHealth.Balance256(target)==hp,"Air emission damaged a target");
            // The retained cast rank and current own synergy/mastery enter the
            // ground creator;an owner's later ranks/gear never enter it.
            b.ChangeSkills(book=>book.Assign(261,4));var ground=Land(first);var graph=OriginalNpcEquipment.Graph(b);var values=ShadowSkillCasting.OwnValues(b,ground.Rank,256);
            int Enhance(int n)=>(int)((long)n+(long)n*graph.Read(330)/100);
            Require(ground.Min256==Enhance(values.Damage256(false))&&ground.Max256==Enhance(values.Damage256(true))&&ground.LightningMastery==graph.Read(330)&&ground.Npc.LightningPierce==graph.Read(335)&&ground.Potion==0,"Landing lost own damage or borrowed owner potion/mastery");
            target.MoveToWorld(new((int)Math.Round(ground.X),(int)Math.Round(ground.Y),ground.Z),b.Map);var actor=NpcEquipmentRandom.Require(b);
            int raw=SorceressSpellMissiles.Roll(ground.Min256,ground.Max256,ground.Npc.Seed,out ulong nextSeed);
            var mitigated=SkillCombat.MitigatePrepared(b,target,new(0,0,0,raw/256.0,0),out int absorb,pierceOverride:e=>e=="ltng"?ground.Npc.LightningPierce:0);Require(absorb==0,"Unexpected fixture absorption");
            var hit=ShadowShockWebs.Advance(ground,true,false);
            Require(SkillHealth.Balance256(target)==hp-NamedElements.To256(mitigated.Total)&&hit.Npc.Seed==nextSeed&&NpcEquipmentRandom.Require(b)==actor,"Ground did not commit exact own unit RNG/typed lightning packet");
            Require(!ShockWebs.Ready(ground.Cast,target.Serial.Value,ground.NextUtc.AddMilliseconds(999))&&ShockWebs.Ready(ground.Cast,target.Serial.Value,ground.NextUtc.AddMilliseconds(1000)),"NHD was not exactly25frames");
            hp=SkillHealth.Balance256(target);var held=ShadowShockWebs.Advance(hit,true,false);Require(SkillHealth.Balance256(target)==hp&&held.Npc.Seed==hit.Npc.Seed,"Same throw rerolled/hit through active lease");
            var second=Land(Emit(b,aim,at.AddMilliseconds(800))[0]);
            Require(second.X==ground.X&&second.Y==ground.Y&&!ShockWebs.Ready(ground.Cast,target.Serial.Value,second.NextUtc),"Independent throw fixture did not overlap within the first lease");
            target.MoveToWorld(new((int)Math.Round(second.X),(int)Math.Round(second.Y),second.Z),b.Map);
            hp=SkillHealth.Balance256(target);var independent=ShadowShockWebs.Advance(second,true,false);
            Require(SkillHealth.Balance256(target)<hp&&independent.Npc.Seed!=second.Npc.Seed&&second.Cast!=ground.Cast,"Independent throw incorrectly inherited the first throw's lease");
            var expiry=ShadowShockWebs.Advance(ground with{Remaining=1},true,false);Require(expiry==null,"Final field frame hit instead of expiring");
            var immune=Land(Emit(b,new(b.X+5,b.Y,b.Z),at.AddSeconds(3))[0]);target.MoveToWorld(new((int)Math.Round(immune.X),(int)Math.Round(immune.Y),immune.Z),b.Map);target.SetResistance(ResistanceType.Energy,100);hp=SkillHealth.Balance256(target);
            var absorbed=ShadowShockWebs.Advance(immune,true,false);Require(SkillHealth.Balance256(target)==hp&&!ShockWebs.Ready(immune.Cast,target.Serial.Value,immune.NextUtc)&&absorbed.Npc.Seed!=immune.Npc.Seed,"Lightning immunity skipped pre-damage lease or unit roll");target.SetResistance(ResistanceType.Energy,0);
            var offline=ShadowShockWebs.Advance(immune,false,false);Require(offline.Npc.Dormant&&offline.Remaining==89&&offline.NextUtc==immune.NextUtc.AddMilliseconds(40)&&SkillHealth.Balance256(target)==hp,"Offline field did not age harmlessly");
            PersistentMinions.Park(b);var parked=ShadowShockWebs.Advance(ground,true,false);Require(parked.Npc.Dormant&&parked.Remaining==89&&NpcEquipmentRandom.Require(b)==actor,"Parked source reset or damaged its field");b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
            var loaded=ShadowShockWebs.Advance(first,true,true);Require(loaded.Npc.Dormant&&loaded.Remaining==first.Remaining-1&&loaded.Npc.Seed==first.Npc.Seed,"Loaded field replayed damage or RNG");
            var dead=Target(b,1);try{dead.MoveToWorld(new((int)Math.Round(second.X),(int)Math.Round(second.Y),second.Z),b.Map);ShadowShockWebs.Advance(second,true,false);Require(dead.Deleted||!dead.Alive,"Typed field damage did not complete lethal return");}finally{if(!dead.Deleted){dead.Delete();}}
            // Drive the real shared queue too:an allocated NPC cast must
            // damage during its own ground lifetime and then leave no field.
            var queued=Emit(b,aim,at.AddSeconds(5));var predicted=Land(queued[0]);target.MoveToWorld(new((int)Math.Round(predicted.X),(int)Math.Round(predicted.Y),predicted.Z),b.Map);hp=SkillHealth.Balance256(target);
            ShockWebs.Tick(predicted.NextUtc,true);var during=ShockWebs.Snapshot().Shards.Where(s=>s.Cast==queued[0].Cast).ToArray();
            Require(SkillHealth.Balance256(target)<hp&&during.Any(s=>s.Npc.Seed!=queued.Single(q=>q.Id==s.Id).Npc.Seed),"Shared queue failed real NPC damage: hp="+SkillHealth.Balance256(target)+"/"+hp+" target="+target.Location+" fields="+Json(during));
            ShockWebs.Tick(queued.Max(s=>s.NextUtc).AddSeconds(20),true);
            Require(!ShockWebs.Snapshot().Shards.Any(s=>s.Cast==queued[0].Cast),"Shared scheduler failed absolute expiry");
            Require(NecromancerEffects.ManaBalance256(owner)==mana&&b.EquipmentNumbers.Read(8)==petMana&&NpcEquipmentRandom.Require(b)==actor&&ShadowLoadout.Originals(b).Select(i=>i.SavedText).SequenceEqual(saved.Items),"Field borrowed mana,actor RNG or changed original items");
        }
        finally{target.Delete();b.ChangeSkills(book=>book.Use(null));PersistentMinions.Park(b);}
    }
    private static void SoftHit(AssassinShadowBody b,PlayerMobile owner)
    {
        var ordinary=Target(b,5000);var soft=Target(b,5000);ordinary.Frozen=soft.Frozen=false;ordinary.CantWalk=soft.CantWalk=true;var at=TimedEffects.Now.AddMinutes(10);
        try
        {
            NpcElementSpellHit.Deliver(b,owner,ordinary,"ltng",3000*256,0,at,"Soft-hit control");NpcElementSpellHit.Deliver(b,owner,soft,"ltng",3000*256,0,at,"Soft-hit field",softHit:true);
            Require(SkillHealth.Balance256(ordinary)==SkillHealth.Balance256(soft)&&NativeMonsterReturn.Session(ordinary).LastMode==PreparedMonsterReturn.GetHit
                &&NativeMonsterReturn.Session(soft).LastMode==0&&NativeMonsterReturn.Session(soft).Refreshes>0,"Soft hit/control failed: hp="+SkillHealth.Balance256(ordinary)+"/"+SkillHealth.Balance256(soft)+" max="+SkillHealth.Maximum256(ordinary)+" modes="+NativeMonsterReturn.Session(ordinary).LastMode+"/"+NativeMonsterReturn.Session(soft).LastMode+" refresh="+NativeMonsterReturn.Session(soft).Refreshes+" frozen="+ordinary.Frozen+" hitclass="+NativeMonsterReturn.Session(ordinary).LastHitClass);
        }
        finally{ordinary.Delete();soft.Delete();}
    }
    private static void Atomic()
    {
        var held=ShockWebs.Snapshot();var sample=held.Shards.First(s=>s.Npc!=null);var bad=sample with{Id=held.NextId+1,Npc=sample.Npc with{Binding="bad"}};bool refused=false;
        try{ShockWebs.Restore(held with{Shards=held.Shards.Append(bad).ToArray()});}catch(InvalidDataException){refused=true;}
        Require(refused&&Json(ShockWebs.Snapshot())==Json(held),"Malformed NPC tail partially replaced shared player/NPC fields");
        refused=false;try{ShockWebs.Restore(held with{Leases=held.Leases.Append(new(1,0,TimedEffects.Now)).ToArray()});}catch(InvalidDataException){refused=true;}
        Require(refused&&Json(ShockWebs.Snapshot())==Json(held),"Malformed lease tail partially replaced shared fields");
    }
    private static void Check(bool reload)
    {
        _=ShadowShockWebs.Row();PlayerMobile owner;AssassinShadowBody[] bodies;BaseCreature target;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);target=(BaseCreature)World.FindMobile((Serial)saved.Target);bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(saved.Bodies[i]),"Reload lost original Shadow body/items/resources/wounds/RNG");}
            Require(saved.Shards.All(s=>ShockWebs.Snapshot().Shards.Contains(s))&&SkillHealth.Balance256(target)==saved.Health,"Saved air/ground fields or target HP changed");
            owner.MoveToWorld(new(6260,3480,0),Map.Trammel);foreach(var b in bodies){b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);}
            ShockWebs.Tick(saved.Shards.Max(s=>s.NextUtc).AddSeconds(20),true);
            Require(!ShockWebs.Snapshot().Shards.Any(s=>saved.Shards.Any(h=>h.Id==s.Id))&&SkillHealth.Balance256(target)==saved.Health,"Reload replayed NPC field damage or reset expiry");
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3480);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);
            bodies=new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}.Select(a=>AssassinShadowBody.CreateInitialized(owner,a,TimedEffects.Now)).ToArray();
            foreach(var b in bodies){b.ChangeSkills(book=>{book.Assign(256,3);book.Assign(251,6);book.Assign(261,1);book.Assign(271,1);});GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);}
            bodies[0].MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);target=Target(bodies[0]);target.MoveToWorld(new(owner.X+15,owner.Y,owner.Z),owner.Map);
        }
        var playerRoll=ShockWebs.Roll;try
        {
            ShockWebs.Roll=_=>throw new InvalidOperationException("NPC field borrowed player RNG");
            foreach(var b in bodies){b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Live(b,owner);}
            bodies[0].MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);SoftHit(bodies[0],owner);Emit(bodies[0],new(bodies[0].X+5,bodies[0].Y,bodies[0].Z),TimedEffects.Now.AddMinutes(20));PersistentMinions.Park(bodies[0]);Atomic();
        }
        finally{ShockWebs.Roll=playerRoll;}
        // Real allocated air and ground payloads live in the shared pool. A
        // restore deliberately suppresses uncertain NPC damage continuation;
        // timers retain their original deadlines rather than replaying casts.
        var pending=bodies.SelectMany((b,i)=>{b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);var air=Emit(b,new(b.X+5,b.Y,b.Z),TimedEffects.Now.AddMinutes(30+i));return new[]{air[0],Land(air[1])};}).ToArray();
        var pool=ShockWebs.Snapshot();ShockWebs.Restore(pool with{Shards=pool.Shards.Where(s=>s.Npc==null).Concat(pending).ToArray()});
        GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());
        File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,target.Serial.Value,SkillHealth.Balance256(target),bodies.Select(Capture).ToArray(),pending,generation)));
        Require(!AssassinCombat.Implemented(CombatAbility.ShadowWarrior)&&!AssassinCombat.Implemented(CombatAbility.ShadowMaster),"Field implementation enabled unfinished learned summons");
        Log.Information("Shadow Shock Web:both original species,shared lob/scatter and cast leases,own unit RNG/damage/mastery/pierce,no owner mana/potion,typed immunity/death,soft-hit positive recovery control,atomic shared restore and parked/offline/reload expiry;approved intrinsic cooldown/action is covered separately;learned Shadow route remains gated");
    }
}
