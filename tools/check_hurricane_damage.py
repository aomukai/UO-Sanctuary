#!/usr/bin/env python3
"""Compare existing actual Hurricane endpoints, cold length and weather formulas with source captures."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
OUT = STAGE / 'hurricane-damage-proof'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(content, native, check=False):
    proof_path = ROOT / 'tools/reference/hurricane-damage-source-evidence.json'
    proof = json.loads(proof_path.read_text())
    fixture = ROOT / proof['fixture']['path']
    assert sha(fixture) == proof['fixture']['sha256']
    OUT.mkdir(exist_ok=True)
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
var hurricane=definitions.Single(x=>x.SourceId==250);
Require(hurricane.SourceKey=="Hurricane"&&!DruidCombat.Implemented(CombatAbility.Hurricane),"Prepared Hurricane remains disabled");
using var json=JsonDocument.Parse(File.ReadAllText(args[0]));int count=0;
foreach(var row in json.RootElement.EnumerateArray())
{
 int rank=row[0].GetInt32(),twister=row[1].GetInt32(),tornado=row[2].GetInt32(),mastery=row[3].GetInt32();
 if(mastery!=0)continue; // Compare the existing pre-mastery supplier; gear/pierce remains separate.
 foreach(int cyclone in new[]{0,1,20})
 {
  var values=new SkillValues(hurricane,key=>key=="Hurricane"?rank:255,key=>key=="Twister"?twister:key=="Tornado"?tornado:key=="Cyclone Armor"?cyclone:0,99);
  Require(values.Damage256(false)==row[6].GetInt32()&&values.Damage256(true)==row[7].GetInt32(),$"Cold endpoints rank{rank}/hard{twister},{tornado}");
  Require(values.DurationFrames==row[8].GetInt32()&&values.DurationFrames==50,"Cold length50 frames");
  Require(values.Mana256==30*256&&values.Eval("auralencalc")==250+cyclone*50&&values.Eval("aurarangecalc")==9&&values.Param(4)==20,"Current mana/duration/radius/event formulas");
 }
 count++;

}
Require(count==9180,"Complete source packet scope");
Console.WriteLine($"{count} actual-content Hurricane pre-mastery endpoint/length comparisons (each with3duration controls)/all255ranks pass; supplier scope only; native actor acceptance is separate.");
''')
    with (STAGE / 'hurricane-damage-build.log').open('w') as log:
        subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), 'build', str(OUT / 'check.csproj'), '-c', 'Release', '--nologo', '-o', str(OUT / 'out')], cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), str(OUT / 'out/check.dll'), str(fixture),
                          str(ROOT / 'data/Sanctuary/Balance/class-skills.json'), str(native)], capture_output=True, text=True)
    (STAGE / 'hurricane-damage-run.log').write_text(run.stdout + run.stderr)
    assert run.returncode == 0, run.stdout + run.stderr
    inputs = [ROOT / name for name in ['src/Sanctuary.Content/SkillMechanics.cs', 'src/Sanctuary.Content/DruidCombat.cs',
              'data/Sanctuary/Balance/class-skills.json', 'tools/check_hurricane_damage.py', 'tools/probe_hurricane_damage_reference.py',
              'tools/reference/hurricane-damage-source-evidence.json']]
    inputs += [fixture, STAGE / 'hurricane-damage-run.log', OUT / 'Program.cs', OUT / 'check.csproj', OUT / 'out/check.dll']
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False, sourceEndpointComparisons=9180, durationFormulaComparisons=27540,
                    contentAssemblySha256=sha(content), nativeAssemblyHashes={str(path.relative_to(native)): sha(path) for _, path in references[1:]},
                    hashes={str(path.relative_to(ROOT)): sha(path) for path in inputs},
                    established=['Actual existing Damage256 and DurationFrames match9180complete source pre-mastery endpoint/cold-length captures across all255ranks. Effective255synergy ranks do not replace hard Twister/Tornado0/1/20 points.',
                                 'Each retained capture tests hard Cyclone Armor0/1/20:27540actual interpreter duration controls preserve250+50frames per hard point. Mana30, radius9, periodic interval20 and cold length50 pass without adding a formula implementation.'],
                    remaining=['Declared nonzero outgoing cold mastery/source roll/packet composition remains in source evidence; this native supplier check deliberately selects mastery0. Real gear/pierce/health/cold-state consumers remain separate.',
                               'No Hurricane handler is enabled. Saved weather, learned casting/payment/input/natural timers/reloads, current-executable/client parity and final effects/icons remain unimplemented or unverified.'])
    out = ROOT / 'tools/reference/hurricane-damage-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print(run.stdout.strip())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--content', required=True, type=Path)
    parser.add_argument('--native', default=ROOT / 'upstream/ModernUO/Distribution', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    build(args.content.resolve(), args.native.resolve(), args.check)
