#!/usr/bin/env python3
"""Check an uninstalled C# corpse base-life draft with current-data source rows."""
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
namespace Sanctuary;
internal sealed record CorpseBaseLifeResult(bool Success,int Minimum,int Maximum,int Base256);
// Complete flags1 calculator lane. Table lookup/layout and actual corpse
// profile/level/difficulty snapshot remain separate native services.
internal static class CorpseBaseLife
{
    internal static CorpseBaseLifeResult Calculate(int[] monster,int[][] levelHp,bool expansion,int difficulty,int level)
    {
        ArgumentNullException.ThrowIfNull(monster);ArgumentNullException.ThrowIfNull(levelHp);
        if(monster.Length!=7||levelHp.Length==0){throw new ArgumentException("Invalid corpse-life table context");}
        if(level>levelHp.Length-1){level=levelHp.Length-1;}
        if(level<0||level>=levelHp.Length){return new(false,0,0,0);}
        difficulty=Math.Clamp(difficulty,0,2);
        int minimum=monster[1+2*difficulty],maximum=monster[2+2*difficulty];
        if(monster[0]==0)
        {
            int scale=levelHp[level][difficulty+(expansion?3:0)];
            minimum=SourcePercentage.Calculate(scale,minimum,100);
            maximum=SourcePercentage.Calculate(scale,maximum,100);
        }
        return new(true,minimum,maximum,checked((int)(((long)minimum+maximum)*128)));
    }
}
'''
PROGRAM = r'''
using System.Text.Json;
using Sanctuary;
string root=args[0];
using var doc=JsonDocument.Parse(File.ReadAllText(root+"/.local/class-skills/corpse-base-life-source-probe.json"));
var data=doc.RootElement;
int[][] Rows(string key)=>data.GetProperty(key).EnumerateArray().Select(r=>r.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).ToArray();
var monsters=Rows("monsterInputs");var levels=Rows("levelHp");int count=0;
foreach(var row in data.GetProperty("cases").EnumerateArray())
{
    int V(int i)=>row[i].GetInt32();
    var result=CorpseBaseLife.Calculate(monsters[V(0)],levels,V(1)!=0,V(2),V(3));
    if(result.Success!=(V(4)!=0)||result.Minimum!=V(5)||result.Maximum!=V(6)||result.Base256!=V(7))
    {throw new Exception("Complete corpse base-life differential mismatch: "+row.GetRawText()+" / "+JsonSerializer.Serialize(result));}
    var restored=JsonSerializer.Deserialize<CorpseBaseLifeResult>(JsonSerializer.Serialize(result));
    if(restored!=result){throw new Exception("Corpse base-life round-trip mismatch");}
    count++;
}
if(count!=52100){throw new Exception("Incomplete current-data corpse base-life coverage");}
Console.WriteLine("Uninstalled corpse base-life draft:52100 complete current-data source comparisons/JSON round-trips pass with shared SourcePercentage;521corpse-source rows, level/game/difficulty/noRatio lanes. Actual corpse snapshot and learned handlers unchanged.");
'''
PROJECT = '''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
<ItemGroup><Compile Include="../../../src/Sanctuary.Content/SourcePercentage.cs" /></ItemGroup>
</Project>
'''
parser = argparse.ArgumentParser()
parser.add_argument('--check', action='store_true')
args = parser.parse_args()
proof = json.loads((ROOT / 'tools/reference/corpse-base-life-source-evidence.json').read_text())
fixture = ROOT / proof['fixture']['path']
assert hashlib.sha256(fixture.read_bytes()).hexdigest() == proof['fixture']['sha256']
stage = STAGE / 'corpse-base-life-draft'
stage.mkdir(exist_ok=True)
for name, content in [('CorpseBaseLife.cs', CSHARP), ('Program.cs', PROGRAM), ('Probe.csproj', PROJECT)]:
    (stage / name).write_text(content.lstrip('\n'))
run = subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), 'run', '--project', str(stage / 'Probe.csproj'), '-c', 'Release', '--', str(ROOT)], capture_output=True, text=True, check=True)
assert not run.stderr and '52100 complete current-data source comparisons' in run.stdout, run.stdout + run.stderr
log = STAGE / 'corpse-base-life-draft-run.log'
log.write_text(run.stdout)
paths = [fixture, log, ROOT / 'src/Sanctuary.Content/SourcePercentage.cs', *[stage / name for name in ['CorpseBaseLife.cs', 'Program.cs', 'Probe.csproj']]]
evidence = dict(referenceVersion='3.3.93847', sourceEvidence='tools/reference/corpse-base-life-source-evidence.json',
    installed=False, handlerEnabled=False, completeComparisons=52100, jsonRoundTrips=52100,
    hashes={str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths},
    scope='Uninstalled C# flags1 HP calculator reuses existing SourcePercentage and matches complete older calculator/ratio outputs across521current selectable-corpse source rows.',
    limits=['Current text/declared layout plus older code composition does not certify a modern executable or binary-table ABI.',
            'Actual native profile/level/difficulty mapping, corpse capture/persistence, RNG, area filtering/damage, learned input and retirement remain separate. No installed assembly is changed.'])
path = ROOT / 'tools/reference/corpse-base-life-draft-evidence.json'
if args.check:
    assert json.loads(path.read_text()) == evidence
else:
    path.write_text(json.dumps(evidence, indent=2) + '\n')
print(run.stdout, end='')
