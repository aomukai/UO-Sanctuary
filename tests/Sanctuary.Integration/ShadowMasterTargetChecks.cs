// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowMasterTargetChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowMasterTargetChecks));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("shadow-master-target-check","Complete original Master target/tactic policy and actual owned NPC action boundary",_=>Core.LoopContext.Post(()=>{try{Check();Log.Information("shadow-master-target-check: passed");}catch(Exception e){Log.Error(e,"shadow-master-target-check: failed");}}));
    private static int[] ScanValues(ShadowMasterScan s)=>[s.Nearest,s.NearestSquared,s.Near,s.NearestOwner,s.OwnerSquared,s.NearOwner,s.Within32,s.Traps,s.LastSpecial];
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-master-target-cases.json")));int scans=0,tactics=0;
        foreach(var row in doc.RootElement.GetProperty("scans").EnumerateArray())
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");var raw=input.GetProperty("units").EnumerateArray().Select(r=>r.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).ToArray();
            var units=raw.Where(r=>r[0]!=0).ToDictionary(r=>r[0]);var trace=new List<int[]>();int[] Read(ShadowMasterUnit t)=>units[t.Id];void Add(params int[] r)=>trace.Add(r);
            var q=new ShadowMasterQueries(t=>{Add(1,t?.Id??0);return t!=null&&Read(t)[2]!=0;},t=>{Add(2,t.Id);return Read(t)[7]!=0;},
                t=>{Add(3,t.Id);var r=Read(t);return r[3]==0?null:new(r[6],r[4]!=0,r[5]!=0);},t=>Read(t)[8]!=0,
                t=>{Add(5,t.Id);return Read(t)[9]!=0;},(who,t)=>{Add(6,who,t.Id);return Read(t)[who==2?10:11];},
                t=>{Add(7,t.Id);return Read(t)[12]!=0;},t=>{Add(8,t.Id);return Read(t)[13]!=0;},(t,mask)=>{Add(9,t.Id,mask);return(Read(t)[14]&mask)!=0;});
            var targets=raw.Select(r=>r[0]==0?null:new ShadowMasterUnit(r[0],r[1])).ToArray();
            var actual=ShadowMasterTargets.Scan(1,input.GetProperty("owner").GetBoolean()?2:0,targets,q);
            Require(Json(ScanValues(actual))==expected.GetProperty("scan").GetRawText()&&Json(trace)==expected.GetProperty("trace").GetRawText(),"Complete Master callback/query-order mismatch at "+scans);
            trace.Clear();var special=targets.Select(t=>ShadowMasterTargets.Special(1,t,q)?1:0).ToArray();
            Require(Json(special)==expected.GetProperty("special").GetRawText()&&Json(trace)==expected.GetProperty("specialTrace").GetRawText(),"Complete Master priority/query-order mismatch at "+scans);scans++;
        }
        foreach(var row in doc.RootElement.GetProperty("tactics").EnumerateArray())
        {
            var r=row.GetProperty("input").EnumerateArray().Select(v=>v.GetInt32()).ToArray();var expected=row.GetProperty("expected");var trace=new List<int[]>();void Add(params int[] v)=>trace.Add(v);
            int actual=ShadowMasterTactics.Use(1,r[0]!=0?2:0,r[1]==0?null:new(r[1],1),r[2],r[6]!=0,r[7],r[8],
                id=>{Add(10,id);return r[3]==0?null:new(id,6,r[4],0,1,0,0,false,false,false,r[5]);},
                s=>{Add(11,s.Id);return s.Range==1;},(t,flags)=>{Add(12,t?.Id??0,flags);return r[9];},
                (s,t,x,y)=>{Add(13,s.Mode,s.Id,t?.Id??0,x,y);return r[10];});
            Require(actual==expected.GetProperty("result").GetInt32()&&Json(trace)==expected.GetProperty("trace").GetRawText(),"Complete Master tactic/query-order mismatch at "+tactics);tactics++;
        }
        Require(scans==613&&tactics==256,"Incomplete Master target/tactic source controls");OwnAction();
        Log.Information("Master targeting:613complete callback/special contexts and256tactic contexts,ordered queries,all six allied traps,strict distance ties/bounds,independent owner/Shadow counters,last special,all elite masks and actual own NPC action;full AI106/world scan/movement remain gated");
    }
    private static void OwnAction()
    {
        var owner=BladeBodyChecks.Fresh(6260,24,3480);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);ClassSkillChecks.Train(owner,"assassin.chargedboltsentry",1);
        var body=AssassinShadowBody.CreateInitialized(owner,CombatAbility.ShadowMaster,TimedEffects.Now);
        try
        {
            body.ChangeSkills(book=>book.Assign(261,3));GeneratedNpcLiveResources.ActivateItemEvents(body);ShadowEquipmentDefense.Activate(body);body.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);NecromancerEffects.SetMana256(owner,0);
            var at=TimedEffects.Now.AddMinutes(10);var aim=new Point3D(body.X+3,body.Y,body.Z);int lookups=0,actions=0;var random=NpcEquipmentRandom.Require(body);
            ShadowAiSkill Lookup(int id)
            {
                lookups++;var own=body.OwnedSkills.Find(id);return own==null?null:new(id,6,own.Mode,3,body.OwnedSkillRank(id),0,0,false,false,false,2);
            }
            int Action(ShadowAiSkill s,ShadowMasterUnit target,int x,int y)
            {actions++;Require(target==null&&s.Mode==9&&x==aim.X*2&&y==aim.Y*2,"Master tactic changed own mode/ground aim");return ShadowActions.BeginSentry(body,null,new(x/2,y/2,aim.Z),at,s.Id)?1:0;}
            int Use(ShadowMasterUnit target,int id)=>ShadowMasterTactics.Use((int)body.Serial.Value,(int)owner.Serial.Value,target,id,false,aim.X*2,aim.Y*2,Lookup,s=>s.Range==1,(_,_)=>throw new InvalidOperationException("Ranged tactic requested unavailable Master locomotion"),Action);
            Require(Use(new((int)owner.Serial.Value,0),261)==0&&Use(new((int)body.Serial.Value,1),261)==0&&lookups==0&&actions==0,"Master tactic targeted itself/owner or consulted a skill before that rejection");
            Require(Use(null,65535)==0&&lookups==1&&actions==0,"Missing own skill fell back to player spell");
            Require(Use(null,261)==1&&actions==1&&ShadowActions.State(body)?.Cast.Skill==261,"Master tactic did not start actual own skill action");
            ShadowActions.Tick(at.AddSeconds(2),true,body.Serial.Value);var trap=(AssassinSentryBody)World.FindMobile((Serial)AssassinTrapRoster.For(owner).Single().Actor);
            Require(trap.Prepared.Creation.Rank==body.OwnedSkillRank(261)&&trap.Prepared.Creation.Rank!=SkillMechanics.Rank(owner,CombatAbility.ChargedBoltSentry)
                &&trap.SummonMaster==owner&&NecromancerEffects.ManaBalance256(owner)==0&&NpcEquipmentRandom.Require(body)==random,"Master tactic borrowed owner rank,mana or actor RNG");
            Require(ShadowActions.State(body)==null&&body.OwnedSkills.Used==null&&!AssassinCombat.Implemented(CombatAbility.ShadowMaster),"Prepared tactic enabled/replayed unfinished Master brain/learned cast");
        }
        finally{ShadowActions.Cancel(body);foreach(var e in AssassinTrapRoster.For(owner)){World.FindMobile((Serial)e.Actor)?.Delete();}PersistentMinions.Park(body);body.Delete();owner.Delete();AssassinTrapRoster.Prune();}
    }
}
