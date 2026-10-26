#!/usr/bin/env python3
"""Compare actual built content formulas/shared fire endpoints to source captures."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
OUT = STAGE / 'fissure-damage-proof'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(content, native, check=False):
    source_path = ROOT / 'tools/reference/fissure-damage-source-evidence.json'
    source = json.loads(source_path.read_text())
    fixture = ROOT / source['fixture']['path']
    assert sha(fixture) == source['fixture']['sha256'] and content.is_file()
    placement_path = ROOT / 'tools/reference/fissure-source-evidence.json'
    placement = json.loads(placement_path.read_text())
    placement_fixture = ROOT / placement['fixture']['path']
    assert sha(placement_fixture) == placement['fixture']['sha256']
    OUT.mkdir(parents=True, exist_ok=True)
    references = [('Sanctuary.Content', content), ('Server', native / 'Server.dll'),
                  ('UOContent', native / 'Assemblies/UOContent.dll'), ('Logger', native / 'Logger.dll')]
    xml = ''.join('<Reference Include="' + name + '" HintPath="' + escape(str(path)) + '" />' for name, path in references)
    (OUT / 'check.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>disable</Nullable></PropertyGroup><ItemGroup>' + xml + '</ItemGroup></Project>')
    (OUT / 'Program.cs').write_text(r'''
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sanctuary;
AssemblyLoadContext.Default.Resolving+=(context,name)=>
{
 foreach(string folder in new[]{args[2],Path.Join(args[2],"Assemblies")})
 {string path=Path.Join(folder,name.Name+".dll");if(File.Exists(path))return context.LoadFromAssemblyPath(path);}
 return null;
};
static void Require(bool value,string why){if(!value)throw new Exception(why);}
var options=new JsonSerializerOptions{PropertyNameCaseInsensitive=true};options.Converters.Add(new JsonStringEnumConverter());
var definitions=JsonSerializer.Deserialize<SkillDefinition[]>(File.ReadAllText(args[1]),options);
var source=(Dictionary<string,SkillDefinition>)typeof(SkillMechanics).GetField("_source",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
source.Clear();foreach(var definition in definitions)source.Add(definition.SourceKey,definition);
var fissure=definitions.Single(x=>x.SourceId==234);
Require(fissure.SourceKey=="Eruption"&&fissure.Name=="Fissure","Current content identity");
var endpoint=typeof(DruidFirestorm).GetMethod("Endpoints",BindingFlags.Static|BindingFlags.NonPublic);
Require(endpoint!=null,"Existing shared fire endpoint supplier");
using var json=JsonDocument.Parse(File.ReadAllText(args[0]));int count=0;
foreach(var row in json.RootElement.EnumerateArray())
{
 int kind=row[0].GetInt32();if(kind==0)continue;
 int rank=row[1].GetInt32(),firestorm=row[2].GetInt32(),volcano=row[3].GetInt32(),mastery=row[4].GetInt32();
 // Large soft ranks on synergy skills cannot substitute for their hard points.
 var values=new SkillValues(fissure,key=>key=="Eruption"?rank:255,key=>key=="Firestorm"?firestorm:key=="Volcano"?volcano:0,99);
 var actual=((int Min,int Max))endpoint.Invoke(null,new object[]{values,mastery,SlowMissilePayload.Normal});
 Require(actual==(row[8].GetInt32(),row[9].GetInt32()),$"Source fire endpoints kind{kind}/rank{rank}/hard{firestorm},{volcano}/mastery{mastery}");
 Require(values.Mana256==15*256&&values.Eval("calc1")==7&&values.Eval("calc2")==6,"Current mana/placement formulas");
 count++;
}
Require(count==18360,"Complete crack endpoint scope");
using var placement=JsonDocument.Parse(File.ReadAllText(args[3]));int samples=0;
foreach(var row in placement.RootElement.GetProperty("scatter").EnumerateArray())
{
 if(row[8].GetInt32()==0)continue;
 var point=Blizzards.Sample(row[0].GetInt32(),row[1].GetInt32(),row[2].GetInt32());
 Require(unchecked(point.X+row[0].GetInt32())==row[9].GetInt32()&&point.Y+400==row[10].GetInt32(),"Actual complete-assembly Blizzard placement");samples++;
}
Require(samples==3840,"Complete applicable Blizzard samples");
Console.WriteLine($"{samples}actual complete-assembly Blizzard sampler comparisons pass.");
Console.WriteLine($"{count}actual-content Fissure crack endpoint comparisons/all255ranks pass; existing shared fire supplier reused; no learned handler enabled.");
''')
    with (STAGE / 'fissure-damage-build.log').open('w') as log:
        subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), 'build', str(OUT / 'check.csproj'), '-c', 'Release', '--nologo', '-o', str(OUT / 'out')],
                       cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), str(OUT / 'out/check.dll'), str(fixture),
                          str(ROOT / 'data/Sanctuary/Balance/class-skills.json'), str(native), str(placement_fixture)], capture_output=True, text=True)
    (STAGE / 'fissure-damage-run.log').write_text(run.stdout + run.stderr)
    assert run.returncode == 0, run.stdout + run.stderr
    inputs = [ROOT / p for p in ['src/Sanctuary.Content/SkillMechanics.cs', 'src/Sanctuary.Content/DruidFirestorm.cs',
                                  'src/Sanctuary.Content/AmazonSlowMissiles.cs', 'src/Sanctuary.Content/Blizzards.cs', 'src/Sanctuary.Content/GroundMissileScatter.cs', 'src/Sanctuary.Content/SourceRandom.cs', 'data/Sanctuary/Balance/class-skills.json',
                                  'tools/probe_fissure_damage_reference.py', 'tools/check_fissure_damage.py', 'tools/reference/fissure-damage-source-evidence.json', 'tools/reference/fissure-source-evidence.json']]
    outputs = [fixture, placement_fixture, STAGE / 'fissure-damage-source-probe.cpp', STAGE / 'fissure-damage-source-probe',
               STAGE / 'fissure-damage-sanitizer.log', STAGE / 'fissure-damage-run.log',
               OUT / 'check.csproj', OUT / 'Program.cs', OUT / 'out/check.dll']
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False,
                    sourceEvidence='tools/reference/fissure-damage-source-evidence.json', comparedCases=18360, actualBlizzardSamples=3840,
                    build=dict(returnCode=0, log='.local/class-skills/fissure-damage-build.log', logHashOmitted='Variable restore/elapsed-time text; executed result and artifacts are hashed.'),
                    inputHashes={str(p.relative_to(ROOT)): sha(p) for p in inputs},
                    artifactHashes={str(path): sha(path) for _, path in references},
                    evidenceHashes={str(p.relative_to(ROOT)): sha(p) for p in outputs},
                    established=[
                        'Actual built Sanctuary.Content SkillValues, formula interpreter, existing DruidFirestorm.Endpoints and SlowMissilePayload.Normal match every18360current crack462/463 complete source capture across255ranks, independent hard points and mastery.',
                        'Actual current class definitions are supplied to SkillMechanics lookup; cross-skill soft ranks255do not inflate Firestorm/Volcano hard-only synergy. Actual Fissure mana15, range7 and interval6 formulas pass in each context.',
                        'Actual complete-assembly Blizzards.Sample also matches all3840eligible complete-source placement samples; the shared production math executes without a source-method extraction.',
                        'No new duplicate elemental formula or fire-mastery implementation is needed for Fissure.'
                    ], limits=source['limits'] + [
                        'Standalone fixture populates actual definition lookup via reflection and invokes the existing internal endpoint supplier. It does not initialize a server, equip gear or enable a learned skill.',
                        'Center zero endpoints are covered by the source fixture only; no native Fissure center/crack creation or modern hit-delay consumer is claimed.'
                    ], next='Connect verified fresh endpoint supplier to native crack creation, preserving cast rank and separate impact/LastCollide/modern next-hit lifecycles.')
    out = ROOT / 'tools/reference/fissure-damage-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print(run.stdout.strip())


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--content', type=Path, default=STAGE / 'fissure-preparation/content/Sanctuary.Content.dll')
    parser.add_argument('--native', type=Path, default=Path('/media/aomukai/FILES/Sanctuary/upstream/ModernUO/Distribution'))
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    build(args.content.resolve(), args.native.resolve(), args.check)
