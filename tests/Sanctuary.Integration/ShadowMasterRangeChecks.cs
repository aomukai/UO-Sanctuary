// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowMasterRangeChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowMasterRangeChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-master-range-fixtures.json");
    private static void Require(bool v,string why){if(!v){throw new InvalidOperationException(why);}}
    private static void Reject(Action f){try{f();}catch(NotSupportedException){return;}throw new InvalidOperationException("Unqualified range context accepted");}
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true}){string name=reload?"shadow-master-range-reload-check":"shadow-master-range-check";ConsoleInputHandler.RegisterCommand(name,"Approved Master Sentinel range and natural world release",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static void NaturalMaster()
    {
        var p=BladeBodyChecks.Fresh(6480,30,3570);ClassSkillChecks.Train(p,"assassin.shadowmaster",20);
        var allocation=NpcWorldAllocation.Capture();AssassinShadowBody b;
        try{NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));b=AssassinShadowBody.CreateInitialized(p,CombatAbility.ShadowMaster,TimedEffects.Now);}
        finally{NpcWorldAllocation.Restore(allocation);}
        var t=new ShadowTailChecks.Witness{Actor=b.Serial.Value};var at=TimedEffects.Now.AddMinutes(20);
        BladeSentinelBody[] Blades()=>AssassinTrapRoster.For(p).Select(e=>World.FindMobile((Serial)e.Actor)).OfType<BladeSentinelBody>().ToArray();
        try
        {
            GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
            b.ChangeSkills(s=>{foreach(var entry in s.Snapshot().Entries.Where(e=>e.Id!=0&&e.Id!=257)){s.Assign(entry.Id,0,true);}s.Assign(257,12);});
            ShadowGroundMotion.Activate(b,p,at);t.SetHits(65000);t.MoveToWorld(new(b.X+6,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();
            bool selected=false;int mana=NecromancerEffects.ManaBalance256(p);
            for(int i=0;i<1000&&Blades().Length==0;i++)
            {
                IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);
                Require(b.WorldGround.State.Controller.Fault==null,"Natural Master Sentinel fault:"+b.WorldGround.State.Controller.Fault);
                selected|=ShadowActions.State(b)?.Cast.Skill==257;
            }
            Require(selected&&Blades().Length==1&&Blades()[0].Prepared.Spawn.Caster==b.Serial.Value&&Blades()[0].Prepared.Creation.Rank==b.OwnedSkillRank(257),"Natural Master did not select/release its own Sentinel");
            Require(b.Prepared.GroundMotion.Gaps?.Any(g=>g.Skill==257)!=true&&NecromancerEffects.ManaBalance256(p)==mana,"Approved Sentinel remained gated or spent owner mana");
            var state=b.WorldGround.State.Controller;b.WorldGround.Store(state with{NextThink=int.MaxValue});
            for(int i=0;i<100&&ShadowActions.State(b)!=null;i++){IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);}
            Require(ShadowActions.State(b)==null&&Blades().Length==1,"Natural Master Sentinel duplicated or failed recovery");
        }
        finally{ShadowActions.Cancel(b);foreach(var blade in Blades()){blade.Delete();}AssassinTrapRoster.Prune();t.Delete();PersistentMinions.Park(b);b.Delete();p.Internalize();}
    }
    private static void Check(bool reload)
    {
        int generation=reload?int.Parse(File.ReadAllText(Manifest))+1:0;
        using var ranges=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"Data/Sanctuary/Balance/skill-missile-ranges.json")));
        int literal=0,unresolved=0;
        foreach(var row in ranges.RootElement.GetProperty("rows").EnumerateArray())
        {
            int id=row.GetProperty("id").GetInt32();if(row.GetProperty("range").ValueKind==JsonValueKind.Number)
            {int n=row.GetProperty("range").GetInt32();Require(SkillMissileRanges.Range(id)==n&&SkillMissileRanges.PreparedAtRank(id,12)==n,"Literal range changed");literal++;}
            else{Reject(()=>SkillMissileRanges.Range(id));unresolved++;}
        }
        Require(literal==694&&unresolved==48&&SkillMissileRanges.Range(-1)==null&&SkillMissileRanges.PreparedAtRank(742,12)==null,"Range table/domain counts changed");
        int blade=ItemSkillExecution.MissileId("blade creeper");
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-master-ai-cases.json")));
        var fixture=source.RootElement[80].GetProperty("input");int compared=0;
        foreach(var pair in new[]{(0,10),(1,15),(3,25),(12,70),(20,110),(40,210),(63,325)})
        {
            int range=SkillMissileRanges.PreparedAtRank(blade,pair.Item1)??-1;Require(range==pair.Item2,"Pinned Blade Sentinel formula/lvl arithmetic differs");
            Require(ShadowMasterWorld.CandidateRange(257,blade,pair.Item1)==range,"Approved candidate did not use its own rank");
            foreach(int offset in new[]{-1,0,1})
            {
                int distance=(range-1)*(range-1)+offset;var p=new ShadowMasterAiChecks.Probe(fixture);p.Rows[261][15]=range;
                foreach(var u in p.Units.Values.Where(u=>u[0]>=100)){u[6]=distance;}p.S[1]=p.S[4]=distance;
                var c=p.C;var context=new ShadowMasterContext(c[0],c[1],c[2],c[3],c[4],c[5],c[6],c[7]!=0,c[8],p.Unit(c[9]));
                ShadowMasterAi.Think(context,new(c[11],c[12],c[13]),p);
                Require(p.Trace.Any(t=>t[0]==24&&t[2]==range)&&p.Trace.Any(t=>t[0]==7&&t[1]==261)==(offset<0),"Whole Master AI range boundary admitted/rejected wrong candidate");compared++;
            }
        }
        Reject(()=>SkillMissileRanges.PreparedAtRank(ItemSkillExecution.MissileId("miasmaboltcloud"),12));
        Reject(()=>ShadowMasterWorld.CandidateRange(261,blade,12));
        Reject(()=>ShadowMasterWorld.CandidateRange(257,ItemSkillExecution.MissileId("miasmaboltcloud"),12));
        NaturalMaster();
        File.WriteAllText(Manifest,generation.ToString());Require(generation<=2&&compared==21,"Range fixture generation/boundary count");
        Log.Information("Approved Master range:all694literal rows unchanged,48default expression gates retained,seven explicit Blade Sentinel rank goldens/21whole-policy strict boundaries pass. Natural original Master Sentinel selection/release/recovery passes. Other skills/missiles remain gated;modern NPC-AI interpretation is explicitly unverified.");
    }
}
