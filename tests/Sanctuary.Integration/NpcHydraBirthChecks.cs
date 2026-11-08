// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcHydraBirthChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcHydraBirthChecks));
    private sealed record Held(uint Actor,string Body,string[] Items,NpcEquipmentSeed Random,NpcHydraBirth Capture);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-hydra-birth-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool v,string why){if(!v){throw new InvalidOperationException(why);}}
    private static void Reject(Action f){try{f();}catch(NotSupportedException){return;}throw new InvalidOperationException("Unqualified NPC Hydra accepted");}
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true}){string name=reload?"npc-hydra-birth-reload-check":"npc-hydra-birth-check";ConsoleInputHandler.RegisterCommand(name,"Own NPC Hydra birth/start/item-frame factory seam;automatic world admission stays gated",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static Held Hold(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),NpcHydraCreation.Capture(b,3));
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"hydra-creation-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var input=row[0];int rank=input[4].GetInt32();if(input[0].GetInt32()!=3||input[1].GetInt32()!=0||rank is not(1 or 20)){continue;}
            int Stat(int id,int layer)=>id switch{329=>77,333=>17,189=>1,_=>0};
            var birth=NpcHydraCreation.Prepare(rank,99,Stat,id=>id==36?7:id==47?11:0);
            int made=0;
            for(int i=0;i<3;i++)
            {
                var head=row[2][i];if(head[0].GetInt32()==0){continue;}made++;
                Require(head[3].GetInt32()==1&&head[4].GetInt32()==1&&head[6].GetInt32()==1000+birth.DurationFrames&&head[8].GetInt32()==birth.Skill("HydraMissile")
                    &&head[9].GetInt32()==birth.Skill("Fire Bolt")&&head[10].GetInt32()==birth.Skill("Fire Ball")&&head[11].GetInt32()==birth.Passive("passive_fire_mastery")
                    &&head[12].GetInt32()==birth.Passive("passive_fire_pierce")&&head[13].GetInt32()==birth.Passive("item_pierce_fire_immunity"),"Complete nonhireling Hydra source capture/owner differs");
            }
            Require(row[1][3].GetInt32()==0&&row[3].GetArrayLength()==input[3].GetInt32()&&row[4].GetArrayLength()==0,"NPC Hydra incorrectly entered player FIFO");count++;
        }
        Require(count==128,"Missing reused nonhireling source contexts");
        Reject(()=>NpcHydraCreation.Prepare(64,99,(_,_)=>0,_=>0));
    }
    private static void Frame(AssassinShadowBody b,PlayerMobile p)
    {
        var target=new ShadowTailChecks.Witness();target.SetHits(65000);target.MoveToWorld(new(b.X+4,b.Y,b.Z),b.Map);target.AIObject.AITimer.Stop();
        var current=new ShadowTailChecks.Witness();current.MoveToWorld(new(b.X+3,b.Y,b.Z),b.Map);current.AIObject.AITimer.Stop();b.Combatant=current;
        var rng=NpcEquipmentRandom.Require(b);var world=NpcWorldAllocation.Capture();var hydras=Json(Hydras.Snapshot());string body=b.SavedText;var capture=NpcHydraCreation.Capture(b,3);
        int ownMana=NecromancerEffects.ManaBalance256(b),mana=NecromancerEffects.ManaBalance256(p);NecromancerEffects.SetMana256(b,0);NecromancerEffects.SetMana256(p,0);
        try
        {
            using var protection=NpcItemSpellCasts.PreserveCombatTarget(b);
            foreach(bool ground in new[]{false,true})foreach(int flag in new[]{0,1})foreach(int mask in Enumerable.Range(0,8))
            {
                var requests=new List<NpcHydraRequest>();int notified=0;var at=TimedEffects.Now;var point=target.Location;
                var frame=NpcItemSpellCasts.PreparedHydraFrame(b,at,r=>{requests.Add(r);return(mask&(1<<r.Index))!=0;},_=>notified++);
                frame.SetPosition(b.X,b.Y+1);frame.ServerDo=true;
                int result=ground?ItemEffectCasts.Position(frame,62,3,point.X,point.Y,flag):ItemEffectCasts.Target(frame,62,3,NpcItemTarget.Capture(target),flag);
                Require(result==(mask==0?0:1)&&notified==(mask==0?0:1)&&requests.Count==3,"NPC Hydra partial failure/notification result differs");
                for(int i=0;i<3;i++){var r=requests[i];Require(r.Capture==capture&&r.Capture.Caster==b.Serial.Value&&r.Capture.ResponsibleOwner==p.Serial.Value&&r.Index==i&&r.X==point.X*2+i-1&&r.Y==point.Y*2-(i==1?0:1)&&r.Mode==9&&r.Z==point.Z&&r.ExpiryUtc==at.AddSeconds(10),"NPC Hydra request lost actual caster,snapshot,source coordinates or expiry");}
                Require(frame.CurrentTarget==null&&frame.FirstPathPoint==(b.X,b.Y+1)&&frame.ServerDo&&b.Combatant==current,"NPC Hydra frame did not restore target/path/server-do/combat target");
            }
            int called=0;var invalid=NpcItemSpellCasts.PreparedHydraFrame(b,TimedEffects.Now,_=>{called++;return true;});
            Require(ItemEffectCasts.Position(invalid,62,3,0,0,1)==0&&ItemEffectCasts.Position(invalid,62,3,b.Map.Width+1,b.Y,1)==0&&called==0,"Failed start reached Hydra factory");
            var throwing=NpcItemSpellCasts.PreparedHydraFrame(b,TimedEffects.Now,_=>throw new NotSupportedException("fixture factory failure"));throwing.SetPosition(b.X,b.Y+2);throwing.ServerDo=true;Reject(()=>ItemEffectCasts.Target(throwing,62,3,NpcItemTarget.Capture(target),1));Require(throwing.CurrentTarget==null&&throwing.FirstPathPoint==(b.X,b.Y+2)&&throwing.ServerDo,"Throwing Hydra factory lost frame restoration");
            Require(NpcEquipmentRandom.Require(b)==rng&&NpcWorldAllocation.Capture()==world&&b.SavedText==body&&Json(Hydras.Snapshot())==hydras&&NecromancerEffects.ManaBalance256(b)==0&&NecromancerEffects.ManaBalance256(p)==0,"Prepared Hydra interface allocated actors/seeds,spent mana or changed source data");
            Require(NpcItemSpellDamage.Require(62).Do==144,"Automatic Hydra do route lost metadata");Reject(()=>NpcItemSpellDamage.Calculate(62,3,99,0));
        }
        finally{b.Combatant=null;target.Delete();current.Delete();NecromancerEffects.SetMana256(p,mana);NecromancerEffects.SetMana256(b,ownMana);}
    }
    private static void Check(bool reload)
    {
        Differential();PlayerMobile p;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));p=(PlayerMobile)World.FindMobile((Serial)s.Owner);generation=s.Generation+1;p.MoveToWorld(new(6530,3570,0),Map.Trammel);bodies=s.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){bodies[i].MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);Require(Json(Hold(bodies[i]))==Json(s.Bodies[i]),"Reload changed own original Hydra capture/items/RNG");}
        }
        else
        {
            p=BladeBodyChecks.Fresh(6530,30,3570);ClassSkillChecks.Train(p,"assassin.shadowwarrior",20);ClassSkillChecks.Train(p,"assassin.shadowmaster",20);bodies=new AssassinShadowBody[2];var world=NpcWorldAllocation.Capture();
            try{int i=0;foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}){int seed=ability==CombatAbility.ShadowMaster?9:1;NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,(uint)seed,SourceRandom.InitLow(seed)));bodies[i]=AssassinShadowBody.CreateInitialized(p,ability,TimedEffects.Now);bodies[i].MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);i++;}}
            finally{NpcWorldAllocation.Restore(world);}
        }
        foreach(var b in bodies){Frame(b,p);}
        var before=bodies.Select(b=>NpcHydraCreation.Capture(b,3)).ToArray();
        var claw=new Tekagi();ClassGear.Profiles.Convert(claw,99,GearRarity.Normal,new Random(173));p.AddToBackpack(claw);
        while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Owner strength");}
        while(p.RawDex<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Owner dexterity");}
        Require(p.EquipItem(claw),"Root claw equip");ClassGear.Refresh(p);
        for(int i=0;i<bodies.Length;i++){Require(NpcHydraCreation.Capture(bodies[i],3)==before[i],"Prepared Hydra borrowed changed root player equipment/stats");}
        p.AddToBackpack(claw);claw.Delete();ClassGear.Refresh(p);var held=bodies.Select(Hold).ToArray();foreach(var b in bodies){PersistentMinions.Park(b);}p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,held,generation)));
        Require(!AssassinCombat.Implemented(CombatAbility.ShadowWarrior)&&!AssassinCombat.Implemented(CombatAbility.ShadowMaster),"Prepared Hydra enabled learned Shadow");
        Log.Information("NPC Hydra prepared birth/item frame:128reused nonhireling source contexts;own stats/hard synergies and actual NPC owner,zero player FIFO registration,all partial-failure masks,target/ground/start14/do144,free resources,restoration and no automatic materialization;real Warrior1/Master9original gear/root swaps/saves pass. NPC-owned world consumer is verified separately.");
    }
}
