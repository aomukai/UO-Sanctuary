// SPDX-License-Identifier: GPL-3.0-or-later
// Retained source-vector consumers from the existing verified drafts.
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;
internal static class DeathSentrySourceGoldens
{
    internal static void Run(){Ai();Search();Packet();}
    private static void Ai()
    {
        
        string root=Core.BaseDirectory;
        using var doc=JsonDocument.Parse(File.ReadAllText(root+"/death-sentry-ai-source-probe.json"));
        int count=0;
        if(AssassinDeathSentryAi.Start(42)!=new DeathSentryAiState(-1,42)){throw new Exception("Source shared initialization mismatch");}
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();int mask=V(0),ownerQueries=0,corpseQueries=0,chanceQueries=0;
            bool Has(int bit)=>(mask&bit)!=0;
            var result=AssassinDeathSentryAi.Think(new(V(1),V(6)),
                ()=>{ownerQueries++;return Has(1)&&Has(2)&&!Has(4)&&!(V(7)!=0&&ownerQueries>1);},
                Has(8),Has(16),V(3),()=>V(2),()=> (Has(32),2,V(4)),
                ()=>{corpseQueries++;return (Has(64),7,V(5));},()=>{chanceQueries++;return Has(128);});
            if(result.State.LastCorpse!=V(8)||result.State.RemainingShots!=V(9)||(int)result.Decision!=V(10)
                ||ownerQueries!=V(11)||corpseQueries!=V(12)||chanceQueries!=V(13)||result.IdleFrames!=0)
            {throw new Exception("AI104 differential mismatch: "+row.GetRawText()+" / "+JsonSerializer.Serialize(result));}
            if(result.Decision is DeathSentryDecision.Explode or DeathSentryDecision.Lightning)
            {
                if(result.State.RemainingShots!=V(14)||result.State.LastCorpse!=V(15)
                    ||result.Target!=(result.Decision==DeathSentryDecision.Explode?7:2)){throw new Exception("Pre-UseSkill debit/target mismatch");}
            }
            var restored=JsonSerializer.Deserialize<DeathSentryAiState>(JsonSerializer.Serialize(result.State));
            if(restored!=result.State){throw new Exception("State round-trip mismatch");}
            count++;
        }
        if(count!=5484){throw new Exception("AI104 source coverage incomplete");}
        Console.WriteLine("Native Death Sentry AI104 draft:5484 complete source comparisons, owner/corpse/chance ordering and JSON round-trips pass. No actor or learned handler enabled.");
    }
    private static void Search()
    {
        
        using var doc=JsonDocument.Parse(File.ReadAllText(Core.BaseDirectory+"/death-sentry-corpse-search-source-probe.json"));
        int[][] orders=[[0,1,2],[0,2,1],[1,0,2],[1,2,0],[2,0,1],[2,1,0]];
        int count=0;
        foreach(var row in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();int caller=V(0),mode=V(3),mask=V(5),offset=V(6),variant=V(7);
            var candidates=new SentryCorpseCandidate[3];
            for(int i=0;i<3;i++)
            {
                candidates[i]=new(i+1,100+(i==0?2:i==1?8:10+offset),100,
                    Type:i==1&&variant==2?0:1,Mode:i==1&&variant==1?1:12,Masked:i==1&&variant==3,
                    Model:!(i==1&&variant==4),Selectable:(mask&(1<<i))!=0);
            }
            var order=orders[V(4)];var sequence=Enumerable.Range(0,3).Select(i=>candidates[mode==2?order[(i+2)%3]:order[i]]).ToArray();
            var result=AssassinSentryCorpses.Select((caller&1)!=0,(caller&2)!=0,V(1),V(2)!=0,100,100,sequence);
            if((result?.Guid??0)!=V(8)){throw new Exception("Complete corpse-search mismatch: "+row.GetRawText());}
            var restored=JsonSerializer.Deserialize<SentryCorpseCandidate[]>(JsonSerializer.Serialize(sequence));
            if(!restored.SequenceEqual(sequence)){throw new Exception("Corpse candidate snapshot mismatch");}
            count++;
        }
        int growth=0;
        foreach(var row in doc.RootElement.GetProperty("growth").EnumerateArray())
        {
            var units=Enumerable.Range(0,20).Select(i=>new SentryCorpseCandidate(i+1,100+i%10,100)).ToArray();
            if(row[0].GetInt32()!=0){Array.Reverse(units);}
            if(AssassinSentryCorpses.Select(true,true,1,true,100,100,units)?.Guid!=row[1].GetInt32()){throw new Exception("Twenty-corpse first-result mismatch");}
            growth++;
        }
        if(count!=46080||growth!=2){throw new Exception("Incomplete source corpse-search inventory");}
        Console.WriteLine("Native Death Sentry corpse search:46080 complete source comparisons and JSON round-trips plus2twenty-corpse ordering controls pass; native corpse/room mapping remains separate.");
    }
    private static void Packet()
    {
        
        string root=Core.BaseDirectory;
        using var doc=JsonDocument.Parse(File.ReadAllText(root+"/corpse-explosion-source-probe.json"));
        int count=0,monsterCount=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();if(V(6)==0){continue;}
            int mask=V(0),range=10+V(1)-1,life=V(2),bound=-1;
            int base256=(mask&8)!=0?CorpseExplosionPackets.MonsterBase256(life,life+3):life*256;
            var packet=CorpseExplosionPackets.Create(base256,50,V(3),range,40,80,50,n=>{bound=n;return n==0?0:V(4)==0?0:V(4)==1?n/2:n-1;});
            int distance=range/2+V(5)-1;var hit=packet.Recipient(distance*distance);
            if(bound!=V(9)||packet.OuterRange!=V(8)||hit.Physical256!=V(10)||hit.Fire256!=V(11))
            {throw new Exception("Corpse explosion differential mismatch: "+row.GetRawText()+" / "+JsonSerializer.Serialize(packet));}
            var restored=JsonSerializer.Deserialize<CorpseExplosionPacket>(JsonSerializer.Serialize(packet));
            if(restored!=packet){throw new Exception("Packet round-trip mismatch");}
            count++;if((mask&8)!=0){monsterCount++;}
        }
        if(count!=1080||monsterCount!=540){throw new Exception("Corpse explosion packet coverage incomplete");}
        // The source splits each half separately; odd total damage can lose one FP unit.
        var fractional=CorpseExplosionPackets.Create(25600,50,50,11,40,80,50,n=>n-1);
        if(fractional.Physical256!=10239||fractional.Fire256!=10239
            ||fractional.Recipient(25).Physical256!=10239||fractional.Recipient(26).Physical256!=0
            ||fractional.Recipient(26).Fire256!=10239||fractional.OuterRange!=6)
        {throw new Exception("Exclusive endpoint, separate half rounding or physical edge clip");}
        Console.WriteLine("Native corpse explosion packet draft:1080 complete applicable source comparisons (540 real monster-predicate contexts,540 declared nonmonster controls), shared SourcePercentage, exclusive random bound and separate physical/fire edge checks, JSON round-trips pass. No learned handler changed.");
    }
}
