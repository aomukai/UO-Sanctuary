// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcIncomingLethalChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcIncomingLethalChecks));
    private sealed record Held(uint Owner,uint Retired,uint Enemy,uint Item,int Life,int Mana,int Penalty,NecromancerCurse Curse);
    private sealed record Saved(Held[] Actors,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-incoming-lethal-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-incoming-lethal-check",Check);Register("npc-incoming-lethal-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"On-struck lethal receiver and retired-caster curse",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static bool GraphAbsent(uint actor)=>!JsonSerializer.Deserialize<NpcProcState[]>(NpcItemProcs.SnapshotJson).Any(s=>s.Actor==actor);
    private static void Check()
    {
        var rows=new System.Collections.Generic.List<Held>();
        foreach(bool missile in new[]{false,true})
        {
            var p=TaintedFireBallChecks.Owner();var b=WarlockTaintedBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now.AddDays(3));var enemy=FireBoltChecks.Target(p,2);
            NpcEquipmentRandom.Set(b,99);var graph=NpcItemProcs.Create(b);var item=NpcIncomingProcChecks.Add(p,graph,72,100);NecromancerEffects.SetMana256(p,0);SkillHealth.SetBalance256(b,256);
            ulong expected=99;SourceRandom.Roll(ref expected);int packets=NativeMonsterReturn.Session(b).Packets;bool observed=false;int deaths=0;var deathHook=ClassLootHooks.BeforeDeath;
            ClassLootHooks.BeforeDeath=actor=>{if(actor==b){deaths++;Require(NecromancerEffects.Curse(enemy)is{Ability:CombatAbility.Weaken,Rank:5},"Native death preceded the incoming curse");}deathHook?.Invoke(actor);};
            PreparedMonsterDamageResult result;
            try
            {
                var at=TimedEffects.Now;var receiver=new NpcIncomingProcChecks.Receiver(p,enemy,b,at,missile,()=>
                {
                    observed=true;Require(b.Alive&&!b.Deleted&&SkillHealth.Balance256(b)==256&&NpcEquipmentRandom.State(b).Seed==expected
                        &&NecromancerEffects.Curse(enemy)is{Ability:CombatAbility.Weaken,Rank:5} curse&&curse.Source==b.Serial.Value,"Lethal on-struck callback role/RNG/phase before HP");
                });
                var packet=new PreparedMonsterDamagePacket(512,512,0,0,5,HitFlags:32);var recipient=new PreparedMonsterRecipient(false,()=>false,()=>8,()=>false);
                result=missile?PreparedMonsterDamage.ReleaseMissile(enemy,b,packet,recipient,_=>throw new InvalidOperationException("No stun"),receiver,at)
                    :PreparedMonsterDamage.Release(enemy,b,packet,recipient,_=>throw new InvalidOperationException("No stun"),receiver,at);
                Require(observed&&result.DeathModeAttempted&&(result.Packet.ResultFlags&2)!=0&&(!b.Alive||b.Deleted)&&deaths==1
                    &&NativeMonsterReturn.Session(b).Packets==packets+1&&NecromancerEffects.ManaBalance256(p)==0,"Lethal receiver did not commit/free/death once or charged mana");
            }
            finally{ClassLootHooks.BeforeDeath=deathHook;}
            uint retired=b.Serial.Value;var held=NecromancerEffects.Curse(enemy);int penalty=NecromancerEffects.DamageStatPercent(enemy);Require(penalty<0,"Curse has no actual attacker damage penalty");
            b.Corpse?.Delete();if(!b.Deleted){b.Delete();}
            Require(World.FindMobile((Serial)retired)==null&&NpcItemProcs.Get(b)==null&&GraphAbsent(retired)&&NpcEquipmentRandom.State(b)==null
                &&!item.Deleted&&item.Parent==p.Backpack&&NecromancerEffects.Curse(enemy)==held&&NecromancerEffects.DamageStatPercent(enemy)==penalty,"Retired caster leaked graph/RNG,deleted original carrier or removed target-bound curse");
            held=held with{UntilUtc=TimedEffects.Now.AddDays(1)};NecromancerEffects.Profiles.Store(enemy,NecromancerEffects.Profiles.Find(enemy)with{Curse=held});
            rows.Add(new(p.Serial.Value,retired,enemy.Serial.Value,item.Serial.Value,SkillHealth.Balance256(enemy),NecromancerEffects.ManaBalance256(p),penalty,held));enemy.Internalize();p.Internalize();
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(rows.ToArray())));
        Log.Information("NPC incoming lethal:2actual melee/missile fatal receivers;one pre-health curse/RNG,one death/free;explicit caster retirement clears graph/seed,keeps original item and target-bound damage penalty;held originals staged");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2&&saved.Actors.Length==2,"Wrong lethal save cohort");
        foreach(var row in saved.Actors)
        {
            var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var enemy=(BaseCreature)World.FindMobile((Serial)row.Enemy);
            Require(World.FindMobile((Serial)row.Retired)==null&&GraphAbsent(row.Retired)&&!NpcEquipmentRandom.Snapshot().Any(s=>s.Actor==row.Retired)
                &&World.FindItem((Serial)row.Item)is{Deleted:false} item&&item.Parent==p.Backpack&&SkillHealth.Balance256(enemy)==row.Life
                &&NecromancerEffects.ManaBalance256(p)==row.Mana&&NecromancerEffects.Curse(enemy)==row.Curse&&NecromancerEffects.DamageStatPercent(enemy)==row.Penalty,"Reload resurrected retired caster,changed original item/resources or lost target-bound curse/actual penalty");
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
}
