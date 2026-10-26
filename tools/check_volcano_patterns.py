#!/usr/bin/env python3
"""Compare candidate Volcano request plans to complete source and prior lob timing."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
OUT = STAGE / 'volcano-pattern-proof'
BASELINE = 'cfbce5b'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(content, native, check=False):
    evidence_paths = [ROOT / f'tools/reference/volcano-{suffix}.json' for suffix in ['engine-evidence', 'factory-source-evidence']]
    fixtures = []
    for path in evidence_paths:
        evidence = json.loads(path.read_text())
        fixture = ROOT / evidence['fixture']['path']
        assert sha(fixture) == evidence['fixture']['sha256']
        fixtures.append(fixture)
    prior = subprocess.check_output(['git', 'show', BASELINE + ':src/Sanctuary.Content/LobProjectiles.cs'], cwd=ROOT, text=True)
    match = re.search(r'    internal static int Lifetime\(int dx,int dy\)\s*\n    \{', prior)
    assert match
    end, depth = prior.index('{', match.start()) + 1, 1
    while depth:
        depth += (prior[end] == '{') - (prior[end] == '}')
        end += 1
    prior_body = prior[match.start():end]
    OUT.mkdir(exist_ok=True)
    references = [('Sanctuary.Content', content), ('Server', native / 'Server.dll'),
                  ('UOContent', native / 'Assemblies/UOContent.dll'), ('Logger', native / 'Logger.dll')]
    xml = ''.join('<Reference Include="' + name + '" HintPath="' + escape(str(path)) + '" />' for name, path in references)
    (OUT / 'check.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>disable</Nullable></PropertyGroup><ItemGroup>' + xml + '</ItemGroup></Project>')
    (OUT / 'PriorLob.cs').write_text('using System;internal static class PriorLob{private const int Velocity=2304;\n' + prior_body + '\n}\n')
    (OUT / 'Program.cs').write_text(r'''
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Sanctuary;
AssemblyLoadContext.Default.Resolving+=(context,name)=>
{
 foreach(string folder in new[]{args[3],Path.Join(args[3],"Assemblies")})
 {string path=Path.Join(folder,name.Name+".dll");if(File.Exists(path))return context.LoadFromAssemblyPath(path);}
 return null;
};
static void Require(bool value,string why){if(!value)throw new Exception(why);}
static int N(JsonElement row,int i)=>row[i].GetInt32();
static int P(object value,string name)=>(int)value.GetType().GetProperty(name).GetValue(value);
static uint U(object value,string name)=>(uint)value.GetType().GetProperty(name).GetValue(value);
static ulong L(object value,string name)=>(ulong)value.GetType().GetProperty(name).GetValue(value);
static void RoundTrip(object value){if(value!=null)Require(value.Equals(JsonSerializer.Deserialize(JsonSerializer.Serialize(value,value.GetType()),value.GetType())),"Immutable plan JSON resumption");}
var assembly=typeof(SkillMechanics).Assembly;
var type=assembly.GetType("Sanctuary.VolcanoPatterns",true);
var emit=type.GetMethod("Emit",BindingFlags.Static|BindingFlags.NonPublic);
var flight=type.GetMethod("Flight",BindingFlags.Static|BindingFlags.NonPublic);
var lob=assembly.GetType("Sanctuary.LobProjectiles",true).GetMethod("Lifetime",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(int),typeof(int)},null);
var options=new JsonSerializerOptions{PropertyNameCaseInsensitive=true};
var rows=JsonSerializer.Deserialize<SummonSource[]>(File.ReadAllText(args[2]),options);
var sources=(Dictionary<string,Dictionary<string,JsonElement>>)typeof(NecromancerSummons).GetField("_sources",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
sources.Clear();foreach(var row in rows)sources.Add(row.Key,row.Fields);
Require(NecromancerSummons.Source("bomb in air","Vel")==12,"Pinned existing Fire Blast velocity");
using var launch=JsonDocument.Parse(File.ReadAllText(args[0]));int emissions=0,sequences=0,flights=0,prior=0;
foreach(var row in launch.RootElement.GetProperty("emitter").EnumerateArray())
{
 if(N(row,0)!=0)continue;int config=N(row,2);int interval=config==1?3:config==2?1:2,range=config==1?4:config==2?1:12;
 var plan=emit.Invoke(null,new object[]{300,400,N(row,1),0x87654321U,20,interval,range});var requests=row[9];
 Require((plan==null)==(requests.GetArrayLength()==0),"Source emission gate");
 if(plan!=null){var p=requests[0];Require(P(plan,"X")==N(p,4)&&P(plan,"Y")==N(p,5)&&U(plan,"Memory")==row[7].GetUInt32()&&L(plan,"Seed")==row[8].GetUInt64(),"Complete source offsets/memory/seed");Require(P(plan,"Flags")==N(p,0)&&P(plan,"Missile")==N(p,1)&&P(plan,"Graphics")==N(p,8)&&P(plan,"Rank")==N(p,7),"Complete source child request");RoundTrip(plan);}emissions++;
}
uint memory=0x87654321U;
foreach(var row in launch.RootElement.GetProperty("sequence").EnumerateArray())
{
 var plan=emit.Invoke(null,new object[]{300,400,N(row,0),memory,20,2,12});var requests=row[4];
 Require((plan==null)==(requests.GetArrayLength()==0),"62-shot composed gate");
 if(plan!=null){memory=U(plan,"Memory");Require(L(plan,"Seed")==row[3].GetUInt64()&&P(plan,"X")==N(requests[0],4)&&P(plan,"Y")==N(requests[0],5),"Persistent emitter memory despite collision-seed perturbations");}
 Require(memory==row[2].GetUInt32(),"Separate memory remains stable without emission");sequences++;
}
using var factory=JsonDocument.Parse(File.ReadAllText(args[1]));
foreach(var row in factory.RootElement.EnumerateArray())
{
 if(N(row,0)!=1||N(row,4)!=0)continue;
 var plan=flight.Invoke(null,new object[]{300,400,300+N(row,2),400+N(row,3),N(row,1)});
 Require((plan!=null)==(N(row,7)!=0),"Complete factory moving-coordinate rejection");
 if(plan!=null){Require(P(plan,"X")==N(row,12)&&P(plan,"Y")==N(row,13)&&P(plan,"TargetX")==N(row,14)&&P(plan,"TargetY")==N(row,15)&&P(plan,"Total")==N(row,16)&&P(plan,"Remaining")==N(row,17)&&P(plan,"Velocity")==N(row,18)&&P(plan,"Rank")==N(row,20)&&P(plan,"CollisionMode")==N(row,9),"Complete source flight/current-versus-total/mode plan");RoundTrip(plan);}flights++;
}
for(int dx=-99;dx<=99;dx++)for(int dy=-99;dy<=99;dy++)
{Require((int)lob.Invoke(null,new object[]{dx,dy})==PriorLob.Lifetime(dx,dy),"Complete prior Fire Blast lifetime body");prior++;}
Require(emissions==906&&sequences==150&&flights==216&&prior==39601,"Complete comparison scope");
Console.WriteLine($"{emissions} emission contexts/{sequences} sequential frames/{flights} factory plans and JSON resumptions pass; {prior} complete-prior Fire Blast lifetime controls preserved. Plan comparisons only; world acceptance is recorded separately.");
''')
    with (STAGE / 'volcano-pattern-build.log').open('w') as log:
        subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), 'build', str(OUT / 'check.csproj'), '-c', 'Release', '--nologo', '-o', str(OUT / 'out')], cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), str(OUT / 'out/check.dll'), *map(str, fixtures),
                          str(ROOT / 'data/Sanctuary/Balance/necromancer-summons.json'), str(native)], capture_output=True, text=True)
    (STAGE / 'volcano-pattern-run.log').write_text(run.stdout + run.stderr)
    assert run.returncode == 0, run.stdout + run.stderr
    inputs = [ROOT / f'src/Sanctuary.Content/{name}.cs' for name in ['VolcanoPatterns', 'SourceRandom', 'LobProjectiles', 'MissileVectors', 'AmazonSlowMissiles']]
    inputs += [ROOT / 'tools/check_volcano_patterns.py', ROOT / 'data/Sanctuary/Balance/necromancer-summons.json', *evidence_paths, *fixtures]
    outputs = [OUT / name for name in ['check.csproj', 'PriorLob.cs', 'Program.cs', 'out/check.dll']]
    outputs += [STAGE / 'volcano-pattern-run.log']
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=True, installed=False,
                    status='This check covers request/flight plans only; saved actors and learned input have separate native acceptance.',
                    caseCounts=dict(emitter=906, sequentialFrames=150, factory=216, priorFireBlastLifetime=39601),
                    contentAssemblySha256=sha(content), nativeAssemblyHashes={str(p.relative_to(native)): sha(p) for _, p in references[1:]},
                    inputHashes={str(p.relative_to(ROOT)): sha(p) for p in inputs}, evidenceHashes={str(p.relative_to(ROOT)): sha(p) for p in outputs},
                    priorLobCommit=BASELINE, priorLobMethodSha256=hashlib.sha256(prior_body.encode()).hexdigest(),
                    established=[
                        'Actual candidate VolcanoPatterns reuses SourceRandom for inclusive range sampling and separate persistent low-seed memory. Complete source emitter requests and all150sequential frames/62emissions match including JSON round-trips.',
                        'Actual candidate flight plans reuse SlowMissilePayload factory velocity, MissileVectors and shared LobProjectiles lifetime arithmetic. Complete source factory coordinates, same-cell nudge, per-axis rejection, retained rank, collision mode0 and variable current-versus-total16 match216supplied contexts.',
                        'The shared lifetime helper accepts a supplied velocity. Existing Fire Blast two-argument calls retain the full prior method result for every integer delta pair[-99,+99], with current Vel12 loaded from the real runtime table.'
                    ], remaining=[
                        'No actual FP16 motion/room/landing or saved world actor is tested here. Flight vectors use the existing source-backed vector consumer; complete allocator/direction/step/current-executable composition remains separate.',
                        'Root/lava collision, modern NHD grouping, actual damage commitment, learned casting/resources/input/local delay and full native save/reload/regression/installation remain required.',
                        'Final effects/audio and talent/right/quick icons remain in their separate inventories; no class completion is claimed.'
                    ])
    out = ROOT / 'tools/reference/volcano-pattern-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == rendered
    else:
        out.write_text(rendered)
    print(run.stdout.strip())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--content', required=True, type=Path)
    parser.add_argument('--native', default=ROOT / 'upstream/ModernUO/Distribution', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    build(args.content.resolve(), args.native.resolve(), args.check)
