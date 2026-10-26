#!/usr/bin/env python3
"""Compare the shared production sampler to complete source and prior Blizzard."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
OUT = STAGE / 'ground-missile-scatter-proof'
BASELINE = 'd482b76'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def method(text):
    match = re.search(r'    public static Point2D Sample\([^\n]*\)\s*\n    \{', text)
    assert match
    end = text.index('{', match.start()) + 1
    depth = 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[match.start():end]


def build(check=False):
    source_path = ROOT / 'tools/reference/fissure-source-evidence.json'
    source = json.loads(source_path.read_text())
    fixture = ROOT / source['fixture']['path']
    assert sha(fixture) == source['fixture']['sha256']
    prior = subprocess.check_output(['git', 'show', BASELINE + ':src/Sanctuary.Content/Blizzards.cs'], cwd=ROOT, text=True)
    old_method = method(prior)
    current_method = method((ROOT / 'src/Sanctuary.Content/Blizzards.cs').read_text())
    OUT.mkdir(parents=True, exist_ok=True)
    includes = ''.join('<Compile Include="' + escape(str(ROOT / f'src/Sanctuary.Content/{name}.cs')) + '" Link="' + name + '.cs" />'
                       for name in ['SourceRandom', 'GroundMissileScatter'])
    (OUT / 'check.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>disable</Nullable></PropertyGroup><ItemGroup>' + includes + '</ItemGroup></Project>')
    (OUT / 'BlizzardMethods.cs').write_text('using System;using Sanctuary;\npublic readonly record struct Point2D(int X,int Y);\npublic static class PriorBlizzard{\n' + old_method + '\n}\npublic static class CurrentBlizzard{\n' + current_method + '\n}\n')
    (OUT / 'Program.cs').write_text(r'''
using System.Text.Json;
using Sanctuary;
static int N(JsonElement e,int i)=>e[i].GetInt32();
static void Require(bool value,string why){if(!value)throw new Exception(why);}
using var json=JsonDocument.Parse(File.ReadAllText(args[0]));var data=json.RootElement;
int scatter=0,center=0,baseline=0;
foreach(var row in data.GetProperty("scatter").EnumerateArray())
{
 int x=N(row,0),elapsed=N(row,1),range=N(row,2),interval=N(row,3),mask=N(row,4),bits=N(row,5);bool allocate=N(row,6)!=0;
 bool eligible=interval!=0&&elapsed%interval==0;
 Require(N(row,8)==(eligible?1:0),"Eligibility");
 if(eligible)
 {
  var p=GroundMissileScatter.Sample(x,elapsed,range);var current=CurrentBlizzard.Sample(x,elapsed,range);var old=PriorBlizzard.Sample(x,elapsed,range);
  Require(p.Seed==row[12].GetUInt64()&&p.X+x==N(row,9)&&p.Y+400==N(row,10),"Complete source RNG/coordinates");
  Require(current==old&&current==new Point2D(p.X,p.Y),"Existing Blizzard producer");
  bool attempts=(bits&mask)==0;
  Require(N(row,7)==(attempts&&allocate?1:0)&&N(row,11)==mask&&row[13].GetArrayLength()==(attempts?1:0),"Landing/allocation boundary");
  if(attempts)Require(row[13][0].GetRawText()==$"[3,462,{x+p.X},{400+p.Y},234,20,1]","Child request");
 }
 else Require(row[12].GetUInt64()==0x123456789ABCDEF0UL&&row[13].GetArrayLength()==0,"No ineligible draw");
 scatter++;
}
foreach(var row in data.GetProperty("center").EnumerateArray())
{
 int guard=N(row,0),elapsed=N(row,1),interval=N(row,3);
 bool eligible=guard is 0 or 7 && interval!=0&&elapsed%interval==0;
 Require(N(row,8)==(eligible?1:0),"Composed center gate");
 if(eligible)
 {
  var p=GroundMissileScatter.Sample(300,elapsed,7);
  Require(p.X+300==N(row,9)&&p.Y+400==N(row,10)&&p.Seed==row[13].GetUInt64(),"Composed do25/placement");
 }
 center++;
}
// Full prior production method, not a newly written expected sampler. These
// extra inputs prove preservation only, not source parity for invalid ranges.
foreach(int x in new[]{int.MinValue,-300,-1,0,300,int.MaxValue})
foreach(int elapsed in new[]{int.MinValue,-1,0,1,6,78,int.MaxValue})
foreach(int range in new[]{int.MinValue,-1,0,1,2,7,32767,1073741824,1073741825,int.MaxValue})
{Require(CurrentBlizzard.Sample(x,elapsed,range)==PriorBlizzard.Sample(x,elapsed,range),"Prior full-domain Blizzard controls");baseline++;}
Require(scatter==7200&&center==5184&&baseline==420,"Fixture scope");
Console.WriteLine($"{scatter}complete-helper/{center}do25 sampler contexts and{baseline}prior-Blizzard controls pass; no Fissure handler enabled.");
''')
    with (STAGE / 'ground-missile-scatter-build.log').open('w') as log:
        subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), 'build', str(OUT / 'check.csproj'), '-c', 'Release', '--nologo', '-o', str(OUT / 'out')],
                       cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), str(OUT / 'out/check.dll'), str(fixture)], capture_output=True, text=True)
    (STAGE / 'ground-missile-scatter-run.log').write_text(run.stdout + run.stderr)
    assert run.returncode == 0, run.stdout + run.stderr
    inputs = [ROOT / f'src/Sanctuary.Content/{name}.cs' for name in ['SourceRandom', 'GroundMissileScatter', 'Blizzards']]
    inputs += [ROOT / 'tools/probe_fissure_reference.py', source_path, ROOT / 'tools/check_ground_missile_scatter.py']
    outputs = [fixture, STAGE / 'fissure-source-probe.cpp', STAGE / 'fissure-source-probe', STAGE / 'fissure-source-sanitizer.log',
               STAGE / 'ground-missile-scatter-run.log',
               OUT / 'check.csproj', OUT / 'BlizzardMethods.cs', OUT / 'Program.cs', OUT / 'out/check.dll']
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False,
                    sourceEvidence='tools/reference/fissure-source-evidence.json',
                    caseCounts=dict(sharedHelper=7200, composedCenter=5184, priorBlizzard=420),
                    build=dict(returnCode=0, log='.local/class-skills/ground-missile-scatter-build.log', logHashOmitted='Contains variable restore and elapsed-time text; compiled artifact and executed result remain hashed.'),
                    priorBlizzardCommit=BASELINE, priorMethodHash=hashlib.sha256(old_method.encode()).hexdigest(),
                    currentMethodHash=hashlib.sha256(current_method.encode()).hexdigest(),
                    inputHashes={str(p.relative_to(ROOT)): sha(p) for p in inputs},
                    evidenceHashes={str(p.relative_to(ROOT)): sha(p) for p in outputs},
                    established=[
                        'Actual production GroundMissileScatter and SourceRandom execute against complete source helper/do25 samples, including post-draw collision/allocation failures and exact64-bit seed.',
                        'Blizzards.Sample now delegates to that shared kernel. Both complete prior/current method bodies are extracted byte-for-byte and compiled with only a Point2D value fixture.420overflow/invalid-range/ordinary controls preserve the prior result.',
                        'Current positive range7 Fissure and positive Blizzard bounds share placement arithmetic only; masks, lifetimes, damage, identity and timing remain separate.'
                    ], limits=source['services'] + [
                        'The extracted Blizzard consumer method is compiled here, not the complete native world. Full content build/native Blizzard regression is required before installation.',
                        'Prior clamped zero/negative/overflow-bound behavior is preserved as compatibility evidence; it is not claimed to equal the source helper outside supported positive range controls.'
                    ], next='Compose current Fissure factory rows, damage and crack LastCollide/modern next-hit identity, then connect native actors and learned casting.')
    out = ROOT / 'tools/reference/ground-missile-scatter-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print(run.stdout.strip())


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
