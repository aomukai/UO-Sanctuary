#!/usr/bin/env python3
"""Check an uninstalled corpse-search C# component against complete source flow."""
from pathlib import Path
import argparse
import hashlib
import json
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
CSHARP = r'''
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;
internal sealed record SentryCorpseCandidate(int Guid,int X,int Y,int Type=1,int Mode=12,bool Masked=false,bool Model=true,bool Selectable=true);
// Ordered-room/linked-list projection is a caller service. Do not sort by
// distance or apply AI104's later remembered-GUID/explosion-distance gate here.
internal static class AssassinSentryCorpses
{
    internal static SentryCorpseCandidate Select(bool caster,bool target,int rank,bool room,
        int enemyX,int enemyY,IEnumerable<SentryCorpseCandidate> ordered)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        if(!caster||!target||rank<=0||!room){return null;}
        foreach(var corpse in ordered)
        {
            if(corpse.Type!=1||corpse.Mode!=12||corpse.Masked||!corpse.Model||!corpse.Selectable){continue;}
            long dx=(long)corpse.X-enemyX,dy=(long)corpse.Y-enemyY;
            if(dx*dx+dy*dy<=100){return corpse;}
        }
        return null;
    }
}
'''
PROGRAM = r'''
using System.Text.Json;
using Sanctuary;
using var doc=JsonDocument.Parse(File.ReadAllText(args[0]+"/.local/class-skills/death-sentry-corpse-search-source-probe.json"));
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
Console.WriteLine("Uninstalled Death Sentry corpse search:46080 complete source comparisons and JSON round-trips plus2twenty-corpse ordering controls pass; native corpse/room mapping remains separate.");
'''
PROJECT = '''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
</Project>
'''
parser = argparse.ArgumentParser()
parser.add_argument('--check', action='store_true')
args = parser.parse_args()
proof = json.loads((ROOT / 'tools/reference/death-sentry-corpse-search-source-evidence.json').read_text())
fixture = ROOT / proof['fixture']['path']
assert hashlib.sha256(fixture.read_bytes()).hexdigest() == proof['fixture']['sha256']
stage = STAGE / 'death-sentry-corpse-search-draft'
stage.mkdir(exist_ok=True)
for name, content in [('AssassinSentryCorpses.cs', CSHARP), ('Program.cs', PROGRAM), ('Probe.csproj', PROJECT)]:
    (stage / name).write_text(content.lstrip('\n'))
run = subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), 'run', '--project', str(stage / 'Probe.csproj'), '-c', 'Release', '--', str(ROOT)], capture_output=True, text=True, check=True)
assert not run.stderr and '46080 complete source comparisons' in run.stdout, run.stdout + run.stderr
log = STAGE / 'death-sentry-corpse-search-draft-run.log'
log.write_text(run.stdout)
paths = [fixture, log, *[stage / name for name in ['AssassinSentryCorpses.cs', 'Program.cs', 'Probe.csproj']]]
evidence = dict(referenceVersion='3.3.93847', sourceEvidence='tools/reference/death-sentry-corpse-search-source-evidence.json',
    installed=False, handlerEnabled=False, completeComparisons=46080, jsonRoundTrips=46080, twentyCorpseControls=2,
    hashes={str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths},
    scope='Uninstalled C# ordered corpse-search component matches complete common predicate/wrapper/room-finder outputs under flags4098. Fixed10-unit inclusive radius is centered on the enemy; first eligible order is preserved.',
    limits=['Native room/sector traversal, source identity/frame mapping, corpse metadata/eligibility and approved loot rules remain separate services.',
            'No world actor, corpse capture/persistence, damage, learned input, modern executable equivalence or retirement is certified.'])
path = ROOT / 'tools/reference/death-sentry-corpse-search-draft-evidence.json'
if args.check:
    assert json.loads(path.read_text()) == evidence
else:
    path.write_text(json.dumps(evidence, indent=2) + '\n')
print(run.stdout, end='')
