#!/usr/bin/env python3
"""Check an uninstalled AI104 C# decision draft against the complete source fixture.

This does not compile into Sanctuary.Content or enable a learned talent.
"""
from pathlib import Path
import argparse, hashlib, json, subprocess
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'
CSHARP = r'''
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;
internal enum DeathSentryDecision { None=0,Die=1,Idle=2,Explode=3,Lightning=4 }
internal sealed record DeathSentryAiState(int RemainingShots,int LastCorpse);
internal sealed record DeathSentryAiResult(DeathSentryAiState State,DeathSentryDecision Decision,int IdleFrames=0,int Target=0);
// Complete AI104 decision component. Lookup, target/corpse search, chance and
// native mode/event services remain explicit, as in the source differential.
internal static class AssassinDeathSentryAi
{
    // The shared source initializer stores the current game frame, not zero
    // or a fabricated corpse sentinel. Native frame/identity mapping is separate.
    internal static DeathSentryAiState Start(int gameFrame)=>new(-1,gameFrame);
    internal static DeathSentryAiResult Think(DeathSentryAiState state,Func<bool> ownerValid,
        bool row,bool skill,int rank,Func<int> shotCount,Func<(bool Has,int Id,int Distance)> selectEnemy,
        Func<(bool Has,int Id,int Distance)> selectCorpse,Func<bool> chance,int rangeBase=10,int rangePerRank=1,
        int activeDistance=16,int idleFrames=0)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(ownerValid);
        ArgumentNullException.ThrowIfNull(shotCount);ArgumentNullException.ThrowIfNull(selectEnemy);
        ArgumentNullException.ThrowIfNull(selectCorpse);ArgumentNullException.ThrowIfNull(chance);
        DeathSentryAiResult Result(DeathSentryDecision decision,int target=0)=>new(state,decision,decision==DeathSentryDecision.Idle?idleFrames:0,target);
        bool Lost(bool debit)
        {
            if(!ownerValid()){return true;}
            if(state.RemainingShots<0)
            {
                if(!row||!skill){return true;}
                state=state with{RemainingShots=shotCount()};
            }
            if(state.RemainingShots<=0){return true;}
            if(debit){state=state with{RemainingShots=state.RemainingShots-1};}
            return false;
        }
        if(Lost(false)){return Result(DeathSentryDecision.Die);}
        if(!skill||rank<=0){return Result(DeathSentryDecision.None);}
        var enemy=selectEnemy();if(!enemy.Has){return Result(DeathSentryDecision.Idle);}
        var corpse=selectCorpse();int maxDistance=row?rangeBase+(rank-1)*rangePerRank:0;
        if(corpse.Has&&corpse.Id!=state.LastCorpse&&corpse.Distance<maxDistance/2)
        {
            if(Lost(true)){return Result(DeathSentryDecision.Die);}
            state=state with{LastCorpse=corpse.Id};return Result(DeathSentryDecision.Explode,corpse.Id);
        }
        if(enemy.Distance<activeDistance&&chance())
        {
            if(Lost(true)){return Result(DeathSentryDecision.Die);}
            return Result(DeathSentryDecision.Lightning,enemy.Id);
        }
        return Result(DeathSentryDecision.Idle);
    }
}
'''
PROGRAM = r'''
using System.Text.Json;
using Sanctuary;
string root=args[0];
using var doc=JsonDocument.Parse(File.ReadAllText(root+"/.local/class-skills/death-sentry-ai-source-probe.json"));
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
Console.WriteLine("Uninstalled Death Sentry AI104 draft:5484 complete source comparisons, owner/corpse/chance ordering and JSON round-trips pass. No actor or learned handler enabled.");
'''
PROJECT = r'''
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
</Project>
'''
parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
proof=json.loads((ROOT/'tools/reference/death-sentry-ai-source-evidence.json').read_text())
fixture=ROOT/proof['fixture']['path']
assert hashlib.sha256(fixture.read_bytes()).hexdigest()==proof['fixture']['sha256']
stage=STAGE/'death-sentry-ai-draft';stage.mkdir(exist_ok=True)
for name,content in [('AssassinDeathSentryAi.cs',CSHARP),('Program.cs',PROGRAM),('Probe.csproj',PROJECT)]:
    (stage/name).write_text(content.lstrip('\n'))
run=subprocess.run([str(ROOT/'.tools/dotnet/dotnet'),'run','--project',str(stage/'Probe.csproj'),'-c','Release','--',str(ROOT)],capture_output=True,text=True,check=True)
assert not run.stderr and '5484 complete source comparisons' in run.stdout,run.stdout+run.stderr
log=STAGE/'death-sentry-ai-draft-run.log';log.write_text(run.stdout)
paths=[fixture,log,*[stage/name for name in ['AssassinDeathSentryAi.cs','Program.cs','Probe.csproj']]]
evidence=dict(referenceVersion='3.3.93847',sourceEvidence='tools/reference/death-sentry-ai-source-evidence.json',installed=False,handlerEnabled=False,completeComparisons=5484,jsonRoundTrips=5484,initializationComparisons=1,
    hashes={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths},
    scope='Uninstalled C# AI104 decision component matches complete legacy source traces, including remaining shots, remembered corpse, owner/corpse/chance query counts and pre-UseSkill state.',
    limits=['No native actor, actual search, mode/sequence consumer, corpse damage, persistence, player input, current executable equivalence or retirement is certified.','The candidate is a draft outside the content assembly; promote only with native integration, lifecycle and saved-world checks.'])
p=ROOT/'tools/reference/death-sentry-ai-draft-evidence.json'
if args.check:assert json.loads(p.read_text())==evidence
else:p.write_text(json.dumps(evidence,indent=2)+'\n')
print(run.stdout,end='')
