// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcRetainedTargetChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcRetainedTargetChecks));
    private sealed record Held(uint Owner,uint Body,uint Original,uint Corpse,uint Ally,uint Item,bool Retained,int Life,int Mana,ulong Seed,NpcProcState Graph,NecromancerCurse Curse);
    private sealed record Saved(Held[] Actors,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-retained-target-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-retained-target-check",Check);Register("npc-retained-target-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Retained monster identity through reflected death and item curse",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Refused(Action action,string why)
    {bool refused=false;try{action();}catch(NotSupportedException){refused=true;}Require(refused,why);}
    private static void Check()
    {
        var rows=new System.Collections.Generic.List<Held>();
        foreach(bool retained in new[]{true,false})
        {
            var p=TaintedFireBallChecks.Owner();var b=WarlockTaintedBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now.AddDays(3));
            var enemy=FireBoltChecks.Target(p,2);var ally=FireBoltChecks.Target(p,3);var outside=FireBoltChecks.Target(p,25);
            NpcEquipmentRandom.Set(b,99);var graph=NpcItemProcs.Create(b);var item=NpcIncomingProcChecks.Add(p,graph,72,100);
            NecromancerEffects.SetMana256(p,0);SkillHealth.SetBalance256(enemy,256);
            NecromancerEffects.Profiles.Store(enemy,new(Curse:new(CombatAbility.IronMaiden,p.Serial.Value,1,TimedEffects.Now.AddMinutes(5),p.X,p.Y)));
            var frame=NpcItemTarget.Capture(enemy);var originalPoint=enemy.Location;Require(frame.Point(p.Map)==originalPoint,"Living target point");
            var capture=ClassLootHooks.RetainOriginal;var died=ClassLootHooks.CreatureDied;Corpse corpse=null;int deaths=0;bool observed=false;
            ClassLootHooks.RetainOriginal=(who,c)=>who==enemy&&!retained?false:capture?.Invoke(who,c)==true;
            ClassLootHooks.CreatureDied=(who,c)=>{died?.Invoke(who,c);if(who==enemy){corpse=c as Corpse;deaths++;}};
            int life=SkillHealth.Balance256(b),packets=NativeMonsterReturn.Session(b).Packets;ulong seed=99;SourceRandom.Roll(ref seed);
            try
            {
                var at=TimedEffects.Now;var receiver=new NpcIncomingProcChecks.Receiver(p,enemy,b,at,false,()=>
                {
                    observed=true;Require((!enemy.Alive||enemy.Deleted)&&corpse is{Deleted:false}&&deaths==1
                        &&SkillHealth.Balance256(b)==life&&NpcEquipmentRandom.State(b).Seed==seed
                        &&NecromancerEffects.Curse(ally)is{Ability:CombatAbility.Weaken,Rank:5} curse&&curse.Source==b.Serial.Value
                        &&NecromancerEffects.Curse(outside)==null,"Reflection/retained target curse did not precede receiving HP exactly once");
                });
                var result=PreparedMonsterDamage.Release(enemy,b,new(512,512,0,0,5),new(false,()=>false,()=>8,()=>false),
                    _=>throw new InvalidOperationException("No stun"),receiver,at);
                Require(observed&&!result.DeathModeAttempted&&SkillHealth.Balance256(b)==life-512
                    &&NativeMonsterReturn.Session(b).Packets==packets+1&&NecromancerEffects.ManaBalance256(p)==0,"Reflected source death aborted/doubled receiver HP/free or spent mana");
            }
            finally{ClassLootHooks.RetainOriginal=capture;ClassLootHooks.CreatureDied=died;}
            Require(retained?ReviveOriginals.Find(corpse)==enemy&&enemy.Map==Map.Internal:enemy.Deleted&&World.FindMobile(enemy.Serial)==null,"Native reflected death did not take requested retained/deleted original path");
            Require(SourceCorpseIdentity.Find(frame.Id)?.Body==corpse&&frame.Point(p.Map)==corpse.Location,"Source corpse did not preserve the original logical target");
            // The frame uses the current logical corpse location, not stale
            // pre-hit coordinates, the caster location or a nearest enemy.
            corpse.MoveToWorld(new(p.X+20,p.Y,p.Z),p.Map);ally.MoveToWorld(new(p.X+21,p.Y,p.Z),p.Map);NecromancerEffects.Profiles.Clear(ally);
            Require(frame.Point(p.Map)==corpse.Location&&NpcItemSpellCasts.CastCurse(b,frame,CombatAbility.Weaken,5,TimedEffects.Now,0)
                &&NecromancerEffects.Curse(ally)is{Ability:CombatAbility.Weaken}&&NecromancerEffects.Curse(outside)!=null,"Logical target cast did not follow its source corpse position");
            Refused(()=>frame.Point(Map.Felucca),"Cross-facet target context accepted");
            var removed=FireBoltChecks.Target(p,2);var missing=NpcItemProcs.RequireIncoming(removed,b,new(256,256,0,0,5));removed.Delete();
            ulong before=NpcEquipmentRandom.State(b).Seed;int hp=SkillHealth.Balance256(b);
            Refused(()=>NpcItemProcs.Received("damagedinmelee",removed,b,new(256,256,0,0,5),TimedEffects.Now,missing),"Removed target silently invented a ground fallback");
            Require(NpcEquipmentRandom.State(b).Seed==before&&SkillHealth.Balance256(b)==hp,"Unsupported removed target consumed proc RNG or receiver HP");
            bool mismatch=false;try{NpcItemProcs.Received("damagedinmelee",enemy,b,new(256,256,0,0,5),TimedEffects.Now,missing);}catch(InvalidOperationException){mismatch=true;}Require(mismatch,"Mismatched logical target frame accepted");
            var curse=NecromancerEffects.Curse(ally)with{UntilUtc=TimedEffects.Now.AddDays(1)};NecromancerEffects.Profiles.Store(ally,NecromancerEffects.Profiles.Find(ally)with{Curse=curse});
            corpse.BeginDecay(TimeSpan.FromDays(2));rows.Add(new(p.Serial.Value,b.Serial.Value,enemy.Serial.Value,corpse.Serial.Value,ally.Serial.Value,item.Serial.Value,retained,
                SkillHealth.Balance256(b),NecromancerEffects.ManaBalance256(p),NpcEquipmentRandom.State(b).Seed,graph.Capture(),curse));
            outside.Delete();PersistentMinions.Suspend(p);ally.Internalize();corpse.Internalize();p.Internalize();
        }
        File.WriteAllText(Manifest,Json(new Saved(rows.ToArray())));
        Log.Information("NPC retained target:2actual Iron Maiden deaths,retained/deleted originals reuse source corpse identity;one defender proc before HP,live corpse-position cast,no mana,removed/mismatched/facet guards;original graph/item/curse cases staged");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2&&saved.Actors.Length==2,"Wrong retained-target save cohort");
        foreach(var row in saved.Actors)
        {
            var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var b=(WarlockTaintedBody)World.FindMobile((Serial)row.Body);var ally=(BaseCreature)World.FindMobile((Serial)row.Ally);var corpse=(Corpse)World.FindItem((Serial)row.Corpse);
            Require((row.Retained?ReviveOriginals.Find(corpse)?.Serial.Value==row.Original:World.FindMobile((Serial)row.Original)==null)
                &&SourceCorpseIdentity.Find(checked((int)row.Original))?.Body==corpse&&b.ControlMaster==p
                &&World.FindItem((Serial)row.Item)is{Deleted:false} item&&item.Parent==p.Backpack
                &&SkillHealth.Balance256(b)==row.Life&&NecromancerEffects.ManaBalance256(p)==row.Mana&&NpcEquipmentRandom.State(b).Seed==row.Seed
                &&Json(NpcItemProcs.Get(b).Capture())==Json(row.Graph)&&NecromancerEffects.Curse(ally)==row.Curse&&NecromancerEffects.DamageStatPercent(ally)<0,"Reload changed original dead/corpse identity,carrier graph/resources or target-bound effect");
        }
        File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
    }
}
