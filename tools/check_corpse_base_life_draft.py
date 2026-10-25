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
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Sanctuary;
internal sealed record CorpseBaseLifeResult(bool Success,int Minimum,int Maximum,int Base256);
internal sealed record CorpseBaseMonster(string Source,bool NoRatio,int[] Minimum,int[] Maximum);
internal sealed record CorpseBaseLevel(int Level,int[] Classic,int[] Expansion);
// Current table lookup is immutable after loading. Unknown/legacy corpse
// sources stay unsupported; no native HitsMax fallback is permitted.
internal sealed class CorpseBaseCatalogue
{
    private readonly Dictionary<string,int[]> _monsters=new(StringComparer.OrdinalIgnoreCase);
    private readonly int[][] _levels;
    internal CorpseBaseCatalogue(CorpseBaseMonster[] monsters,CorpseBaseLevel[] levels)
    {
        if(monsters is not{Length:521} || levels is not{Length:111}){throw new InvalidDataException("Incomplete corpse base catalogue");}
        _levels=new int[levels.Length][];
        foreach(var row in levels)
        {
            if(row==null || row.Level<0 || row.Level>=_levels.Length || _levels[row.Level]!=null || !Cells(row.Classic) || !Cells(row.Expansion))
            {throw new InvalidDataException("Invalid corpse base level row");}
            _levels[row.Level]=row.Classic.Concat(row.Expansion).ToArray();
        }
        foreach(var row in monsters)
        {
            if(row==null || string.IsNullOrWhiteSpace(row.Source) || !Cells(row.Minimum) || !Cells(row.Maximum))
            {throw new InvalidDataException("Invalid corpse base monster row");}
            // Retain authored minimum>maximum cells; the source calculator
            // averages them, including minionspawner1's Normal row.
            int[] values={row.NoRatio?1:0,row.Minimum[0],row.Maximum[0],row.Minimum[1],row.Maximum[1],row.Minimum[2],row.Maximum[2]};
            if(!_monsters.TryAdd(row.Source,values)){throw new InvalidDataException("Duplicate corpse base source");}
        }
    }
    private static bool Cells(int[] values)=>values is{Length:3} && values.All(x=>x>=0);
    internal bool TryCalculate(string source,bool expansion,int difficulty,int level,out CorpseBaseLifeResult result)
    {
        result=new(false,0,0,0);
        if(string.IsNullOrEmpty(source) || !_monsters.TryGetValue(source,out var monster)){return false;}
        result=CorpseBaseLife.Calculate(monster,_levels,expansion,difficulty,level);
        return result.Success;
    }
}
// Complete flags1 calculator lane shared by the validated packaged lookup.
// Actual corpse profile/level/difficulty capture remains a native service.
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
var options=new JsonSerializerOptions{PropertyNameCaseInsensitive=true};
string package=root+"/.local/class-skills/corpse-base-catalogue/";
var monsterRows=JsonSerializer.Deserialize<CorpseBaseMonster[]>(File.ReadAllText(package+"corpse-base-monsters.json"),options)!;
var levelRows=JsonSerializer.Deserialize<CorpseBaseLevel[]>(File.ReadAllText(package+"corpse-base-levels.json"),options)!;
var catalogue=new CorpseBaseCatalogue(monsterRows,levelRows);
var ids=data.GetProperty("ids").EnumerateArray().Select(x=>x.GetString()!).ToArray();
foreach(var row in data.GetProperty("cases").EnumerateArray())
{
    int V(int i)=>row[i].GetInt32();
    var result=CorpseBaseLife.Calculate(monsters[V(0)],levels,V(1)!=0,V(2),V(3));
    bool found=catalogue.TryCalculate(ids[V(0)],V(1)!=0,V(2),V(3),out var lookedUp);
    if(found!=result.Success || lookedUp!=result){throw new Exception("Packaged catalogue lookup differs: "+row.GetRawText());}
    if(result.Success!=(V(4)!=0)||result.Minimum!=V(5)||result.Maximum!=V(6)||result.Base256!=V(7))
    {throw new Exception("Complete corpse base-life differential mismatch: "+row.GetRawText()+" / "+JsonSerializer.Serialize(result));}
    var restored=JsonSerializer.Deserialize<CorpseBaseLifeResult>(JsonSerializer.Serialize(result));
    if(restored!=result){throw new Exception("Corpse base-life round-trip mismatch");}
    count++;
}
if(count!=52100){throw new Exception("Incomplete current-data corpse base-life coverage");}
if(catalogue.TryCalculate(null!,true,0,1,out _) || catalogue.TryCalculate("unsupported-corpse-source",true,0,1,out _))
{throw new Exception("Unknown source invented corpse base life");}
void Reject(CorpseBaseMonster[] m,CorpseBaseLevel[] l)
{try{_ = new CorpseBaseCatalogue(m,l);}catch(InvalidDataException){return;}throw new Exception("Malformed catalogue accepted");}
Reject(monsterRows[..^1],levelRows);Reject(monsterRows,levelRows[..^1]);
var duplicate=monsterRows.ToArray();duplicate[1]=duplicate[0];Reject(duplicate,levelRows);
var missing=levelRows.ToArray();missing[1]=missing[0];Reject(monsterRows,missing);
var malformed=monsterRows.ToArray();malformed[0]=malformed[0] with{Minimum=new[]{-1,0,0}};Reject(malformed,levelRows);
var badLevels=levelRows.ToArray();badLevels[0]=badLevels[0] with{Expansion=new[]{1,2}};Reject(monsterRows,badLevels);
// Loaded records cannot mutate the retained mechanical context.
monsterRows[0].Minimum[0]=int.MaxValue;levelRows[1].Expansion[0]=int.MaxValue;
var control=CorpseBaseLife.Calculate(monsters[0],levels,true,0,1);
if(!catalogue.TryCalculate(ids[0],true,0,1,out var immutable) || immutable!=control){throw new Exception("Loaded table alias changed combat data");}
Console.WriteLine("Uninstalled corpse base-life draft:52100 complete current-data source comparisons/JSON round-trips and52100packaged native lookup comparisons pass with shared SourcePercentage; unknown-source, malformed/missing/duplicate rows and immutable-load controls pass. Actual corpse snapshot and learned handlers unchanged.");
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
subprocess.run(['python3', str(ROOT / 'tools/build_corpse_base_catalogue.py')], check=True, capture_output=True, text=True)
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
paths += list(sorted((STAGE / 'corpse-base-catalogue').glob('*.json'))) + [ROOT / 'tools/build_corpse_base_catalogue.py']
evidence = dict(referenceVersion='3.3.93847', sourceEvidence='tools/reference/corpse-base-life-source-evidence.json',
    installed=False, handlerEnabled=False, completeComparisons=52100, jsonRoundTrips=52100,
    packagedLookupComparisons=52100, rejectedMalformedCatalogues=6, unknownSourcesRejected=True, immutableLoadedData=True,
    hashes={str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths},
    scope='Uninstalled C# flags1 HP calculator and immutable packaged source/level lookup reuse SourcePercentage and match complete older calculator/ratio outputs across521current selectable-corpse source rows.',
    limits=['Current text/declared layout plus older code composition does not certify a modern executable or binary-table ABI.',
            'Actual native profile/level/difficulty mapping, corpse capture/persistence, RNG, area filtering/damage, learned input and retirement remain separate. No installed assembly is changed.'])
path = ROOT / 'tools/reference/corpse-base-life-draft-evidence.json'
if args.check:
    assert json.loads(path.read_text()) == evidence
else:
    path.write_text(json.dumps(evidence, indent=2) + '\n')
print(run.stdout, end='')
