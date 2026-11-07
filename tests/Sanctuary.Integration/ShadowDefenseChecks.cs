// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowDefenseChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowDefenseChecks));
    private sealed record Held(uint Actor,string Body,string[] Items,string Resources,NpcEquipmentSeed Random,int Life);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-defense-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("shadow-defense-check",false);Register("shadow-defense-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original Shadow defensive recipient and approved dual-claw Weapon Block",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Held Capture(AssassinShadowBody body)=>new(body.Serial.Value,body.SavedText,ShadowLoadout.Originals(body).Select(i=>i.SavedText).ToArray(),Json(GeneratedNpcLiveResources.State(body)),NpcEquipmentRandom.Require(body),SkillHealth.Balance256(body));
    private static void Numeric(AssassinShadowBody body)
    {
        var held=Capture(body);var graph=body.EquipmentNumbers;
        Require(NpcSkillItemTypes.Code(68)=="h2h"&&NpcSkillItemTypes.Code(69)=="orb"&&NpcSkillItemTypes.Code(-1)==null&&NpcSkillItemTypes.Code(int.MaxValue)==null,"Compiled skill type IDs were replaced with public line numbers");
        Require(ShadowEquipmentDefense.Composite(body)==13,"Original generated Shadow is not the expected two-claw fixture");
        int chance=graph.Stats.Where(v=>v.Stat==348).Max(v=>v.Value);Require(chance>0&&chance<100,"Missing real posted Shadow Weapon Block chance");
        foreach(int mode in new[]{1,2,15})foreach(bool avoid in new[]{false,true})foreach(bool block in new[]{false,true})for(int i=0;i<40;i++)
        {
            var random=NpcEquipmentRandom.Require(body);ulong seed=random.Seed;
            int expected=mode is 2 or 15?0:SourceRandom.Roll(ref seed)%100<chance?16:0;
            int actual=ShadowEquipmentDefense.SuppliedMode(body,mode,avoid,block);
            Require(actual==expected&&NpcEquipmentRandom.Require(body)==random with{Seed=seed},$"Shadow avoidance mismatch species={body.SourceMonsterId} mode={mode} chance={chance} expected={expected} actual={actual} seed={random.Seed} expectedSeed={seed} actualSeed={NpcEquipmentRandom.Require(body).Seed}");
        }
        var hand=ShadowLoadout.Binding(body).Primary;
        GeneratedNpcLiveResources.SetItemActive(body,hand,false);
        var before=NpcEquipmentRandom.Require(body);
        Require(ShadowEquipmentDefense.Composite(body)==0&&ShadowEquipmentDefense.SuppliedMode(body,1,false,true)==0
            &&NpcEquipmentRandom.Require(body)==before,"Detached claw retained Weapon Block or consumed RNG");
        GeneratedNpcLiveResources.SetItemActive(body,hand,true);
        Require(body.SavedText==held.Body&&ShadowLoadout.Originals(body).Select(i=>i.SavedText).SequenceEqual(held.Items)
            &&SkillHealth.Balance256(body)==held.Life,"Defense checks changed original equipment,birth or wounds");
    }
    private static void World(AssassinShadowBody body,PlayerMobile owner)
    {
        var enemy=new GraveyardSkeleton();enemy.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);enemy.Frozen=true;enemy.CantWalk=true;enemy.AIObject.AITimer.Stop();
        var before=Capture(body);body.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        try
        {
            Require(NpcOriginalItemProcs.Active(body)&&NpcItemSpellFlights.Attached(body,owner,owner.Map.MapID),"Qualified stationary Shadow did not attach its own world graph");
            Require(SourceBlocking.TryRate(body,out int shield,out _)&&shield==0,"ToBlock50 fabricated a Shadow shield");
            int chance=body.EquipmentNumbers.Stats.Where(v=>v.Stat==348).Max(v=>v.Value);
            for(int i=0;i<80;i++)
            {
                ulong seed=NpcEquipmentRandom.Require(body).Seed;
                bool expected=SourceRandom.Roll(ref seed)%100>=chance;
                Require(ClassStats.CheckHit(enemy,body,0,()=>0,guaranteed:true)==expected&&NpcEquipmentRandom.Require(body).Seed==seed,"Real guaranteed-accuracy path bypassed Shadow Weapon Block");
            }
            bool refused=false;int life=SkillHealth.Balance256(body);
            try{PreparedMonsterNativeDamage.RequireIncoming(owner,body,owner);}catch(NotSupportedException){refused=true;}
            Require(refused&&SkillHealth.Balance256(body)==life,"Friendly owner bypassed original guardian damage guard");
            PreparedMonsterNativeDamage.RequireIncoming(owner,body,enemy);
            foreach(bool missile in new[]{false,true})
            {
                life=SkillHealth.Balance256(body);var at=TimedEffects.Now;
                var receiver=new NpcIncomingProcChecks.Receiver(owner,enemy,body,at,missile,()=>Require(SkillHealth.Balance256(body)==life,"Incoming callbacks ran after HP"));
                var packet=new PreparedMonsterDamagePacket(256,256,0,0,1,HitFlags:32);
                var recipient=new PreparedMonsterRecipient(false,()=>false,()=>8,()=>false);
                if(missile){PreparedMonsterDamage.ReleaseMissile(enemy,body,packet,recipient,_=>0,receiver,at);}
                else{PreparedMonsterDamage.Release(enemy,body,packet,recipient,_=>0,receiver,at);}
                Require(SkillHealth.Balance256(body)==life-256&&receiver.Incoming==1,"Ordered Shadow incoming HP or callback count changed");
            }
            Require(body.SavedText==before.Body&&ShadowLoadout.Originals(body).Select(i=>i.SavedText).SequenceEqual(before.Items),"World recipient replaced original Shadow identity or items");
        }
        finally{enemy.Delete();PersistentMinions.Park(body);}
        int wounded=SkillHealth.Balance256(body);body.AdvanceRecovery(TimedEffects.Now.AddSeconds(20),false);
        Require(body.Map==Map.Internal&&SkillHealth.Balance256(body)==wounded&&!((IPersistentMinion)body).CanActivate,"Parking healed wounds or enabled unfinished Shadow AI");
    }
    private static void Check(bool reload)
    {
        int generation=0;PlayerMobile owner;AssassinShadowBody[] bodies;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)Server.World.FindMobile((Serial)saved.Owner);
            bodies=saved.Bodies.Select(h=>(AssassinShadowBody)Server.World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(saved.Bodies[i]),"Saved original defensive body/items/passives/events/wounds/RNG changed");}
            owner.MoveToWorld(new(6260,3480,0),Map.Trammel);
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3480);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);
            bodies=new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}.Select(ability=>AssassinShadowBody.CreateInitialized(owner,ability,TimedEffects.Now)).ToArray();
            foreach(var body in bodies)
            {
                if(body.Prepared.Capture.Ability==CombatAbility.ShadowWarrior){body.ChangeSkills(book=>book.Mirror(body.Prepared.Capture,263,0));}
                GeneratedNpcLiveResources.ActivateItemEvents(body);var old=body.SavedText;
                Require(!old.Contains("DefenseProfile"),"Optional defense field broke old body JSON");
                ShadowEquipmentDefense.Activate(body);SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)*2/3-1);
            }
        }
        foreach(var body in bodies){Numeric(body);World(body,owner);}
        PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());
        File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,bodies.Select(Capture).ToArray(),generation)));
        Log.Information("Shadow defense:both original species;960stationary/moving and160real accuracy checks,dual-claw detach,no fabricated shield,ordered melee/missile HP/friendship,parking and saved original graphs;AI/intrinsic casts/death42/learned routes remain gated");
    }
}
