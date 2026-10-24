// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class SentryComponentChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SentryComponentChecks));
    private sealed record Saved(SentryCreationStats Creation,SentryAiState Ai,SentrySequenceState Sequence,int Minimum,int Maximum,int Shots,int Bolts);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sentry-component-fixtures.json");
    public static void Configure(){Register("sentry-component-check",Check);Register("sentry-component-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Sentry captured creation, AI101 and sequence components; no learned input",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Differential()
    {
        using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"sentry-ai-source-probe.json"))))
        {
            int count=0;
            foreach(var row in doc.RootElement.EnumerateArray())
            {
                int V(int i)=>row[i].GetInt32();int mask=V(0);bool B(int bit)=>(mask&bit)!=0;int formulas=0,rolls=0;
                var result=AssassinSentryAi.Think(new(V(1)),B(1),B(2),B(4),B(8),B(16),()=>{formulas++;return V(2);},B(32),V(3),()=>{rolls++;return B(64);},B(128));
                Require(result.State.RemainingShots==V(4) && (int)result.Decision==V(5) && result.IdleFrames==V(6) && result.ClearInferno==(V(7)!=0),"Full AI101 differential "+count);
                Require(formulas<=1 && rolls<=1 && (rolls==0 || B(32)&&V(3)<25),"AI called irrelevant formula/chance service");count++;
            }
            Require(count==12288,"AI case inventory");
        }
        using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"sentry-sequence-source-probe.json"))))
        {
            int count=0;
            foreach(var row in doc.RootElement.EnumerateArray())
            {
                int V(int i)=>row[i].GetInt32();var next=AssassinSentrySequence.Advance(new(V(1),V(2)),V(0));
                Require(next==new SentrySequenceState(V(3),V(4),V(5)),"Complete sequence differential "+count);count++;
            }
            Require(count==2754,"Sequence case inventory");
            var state=new SentrySequenceState();int frames=0,fires=0;
            while(!AssassinSentrySequence.Complete(state)){if(state.Action==1){fires++;Require(frames==2,"Sequence entry2 fire phase");}state=AssassinSentrySequence.Advance(state);frames++;}
            Require(frames==15&&fires==1,"Pinned15-entry sequence phase count");
        }
        using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"sentry-damage-source-probe.json"))))
        {
            var fields=doc.RootElement.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();int min=Array.IndexOf(fields,"nLightMinDamage"),max=Array.IndexOf(fields,"nLightMaxDamage");int count=0;
            foreach(var row in doc.RootElement.GetProperty("cases").EnumerateArray())
            {
                int V(int i)=>row[i].GetInt32();
                if(V(5)==0)
                {
                    var stats=new SentryCreationStats(V(0),99,99,25600,100,0,MonsterDifficulty.Normal,
                        [new("item_pierce_light_immunity",0),new("passive_ltng_pierce",0),new("passive_ltng_mastery",V(3))],
                        [new("BoltSentry",V(0)),new("Fire Trauma",V(1)),new("Lightning Sentry",V(2))]);
                    var ends=AssassinSentryCreation.Lightning(stats);
                    Require(ends.Min==row[6][min].GetInt32()&&ends.Max==row[6][max].GetInt32(),"Full sentry damage getter/creator differential "+count);
                }
                Require(row[7].GetInt32()==0,"Sentry pure skill damage rolled critical");count++;
            }
            Require(count==1728,"Damage case inventory");
        }
        Require(!AssassinCombat.Implemented(CombatAbility.ChargedBoltSentry),"Component enabled unfinished learned handler");
        using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"sentry-launch-source-probe.json"))))
        {
            int count=0;
            foreach(var row in doc.RootElement.EnumerateArray())
            {
                int V(int i)=>row[i].GetInt32();int queries=0;var spawned=new System.Collections.Generic.List<SentryBoltSpawn>();
                bool success=AssassinSentryLaunch.Dispatch(V(0)!=0,V(1)>=0&&V(1)<1000,V(2),V(5),ordinal=>
                {
                    int n=queries++;if(V(3)==1&&n%2!=0){return null;}
                    return new Point2D(V(3)==2?0:126+n,V(3)==3?0:140-n);
                },spawn=>{if(V(4)!=0&&spawn.Ordinal%2!=0){return false;}spawned.Add(spawn);return true;});
                Require(success==(V(6)!=0)&&queries==V(7)&&spawned.Count==row[8].GetArrayLength(),"Complete do17 dispatch differential "+count);
                for(int i=0;i<spawned.Count;i++)
                {
                    var expected=row[8][i];var actual=spawned[i];
                    Require(actual.Ordinal==expected[0].GetInt32()&&actual.Aim==new Point2D(expected[1].GetInt32(),expected[2].GetInt32())
                        &&actual.Seed==expected[4].GetUInt32()&&actual.Frames==expected[5].GetInt32()&&actual.Distance==expected[6].GetInt32(),"Full charged-bolt initializer differential "+count);
                }
                count++;
            }
            Require(count==1152,"Launch case inventory");
        }
        Log.Information("Sentry components:12288 full AI decisions,2754 exact sequence frames,1728 full damage creator/getter rows and1152 complete launch/initializer cases pass; native actor/firing/input remain separate");
    }
    private static void Train(PlayerMobile owner,CombatAbility ability,int rank)
    {string id=SkillMechanics.For(ability).TalentId;while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}}
    private static void Check()
    {
        Differential();var p=BladeBodyChecks.Fresh(6080);p.Name="SentryComponentFixture";
        try
        {
            var naked=AssassinSentryCreation.Capture(p);Require(naked.Level==75&&naked.CasterLevel==99&&naked.Rank==1&&naked.Life256==25600,"Sentry retained helper level rather than Blade caster overwrite");
            Require(naked.Passives.Length==3&&naked.Skills.Any(x=>x.Name=="BoltSentry"&&x.Value==1)&&AssassinSentryCreation.Shots(naked)==5&&AssassinSentryCreation.Bolts(naked)==5,"Captured actor skills and base shot/bolt counts");
            Train(p,CombatAbility.ShockWeb,16);Train(p,CombatAbility.LightningSentry,20);Train(p,CombatAbility.FireBlast,15);
            while(p.RawStr<60){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}
            var eye=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Griffon's Eye").Id,99,new Random(173),true);
            p.AddToBackpack(eye);Require(p.EquipItem(eye),"Actual Griffon mastery/pierce/allskills supplier");ClassGear.Refresh(p);
            var captured=AssassinSentryCreation.Capture(p);var ends=AssassinSentryCreation.Lightning(captured);
            Require(captured.Rank==2&&captured.Level==76&&captured.Passive("passive_ltng_mastery")>0&&captured.Passive("passive_ltng_pierce")>0,"Real soft rank/mastery/pierce capture");
            Require(captured.Skill("Shock Field")==16&&captured.Skill("Lightning Sentry")==20&&captured.Skill("Fire Trauma")==15
                &&AssassinSentryCreation.Shots(captured)==10&&AssassinSentryCreation.Bolts(captured)==10,"Hard-only captured skills and integer shot/bolt thresholds");
            p.AddToBackpack(eye);ClassGear.Refresh(p);Require(AssassinSentryCreation.Lightning(captured)==ends,"Owner gear change rewrote captured stats");
            foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
            {
                var next=AssassinSentryCreation.Rescale(captured,difficulty);var expected=ValkyrieCreation.Base(99,2,(int)difficulty,true,AssassinSentryCreation.SourceSpawn(difficulty));
                Require(next.Level==expected.Level&&next.Defense==expected.Defense&&next.Attack==expected.Attack&&next.Passives==captured.Passives&&next.Skills==captured.Skills&&AssassinSentryCreation.Lightning(next)==ends,"Difficulty rescale mutated rank/passive/skill/damage context");
            }
            bool refused=false;try{AssassinSentryCreation.Prepare(SkillMechanics.Values(p,CombatAbility.ChargedBoltSentry),99,MonsterDifficulty.Normal,name=>name=="item_pierce_light_immunity"?1:0);}catch(NotSupportedException){refused=true;}
            Require(refused,"Unsupported inherited sunder silently accepted");
            var saved=new Saved(captured,new(4),new(512,3328,1),ends.Min,ends.Max,10,10);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));RoundTrip(saved);
        }
        finally{p.Delete();}
    }
    private static void RoundTrip(Saved saved)
    {
        var copy=JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(saved));var ends=AssassinSentryCreation.Lightning(copy.Creation);
        Require(ends==(saved.Minimum,saved.Maximum)&&AssassinSentryCreation.Shots(copy.Creation)==saved.Shots&&AssassinSentryCreation.Bolts(copy.Creation)==saved.Bolts
            &&copy.Ai==saved.Ai&&copy.Sequence==saved.Sequence,"Serialized creation/AI/sequence context changed");
    }
    private static void Reload(){Differential();RoundTrip(JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)));}
}
