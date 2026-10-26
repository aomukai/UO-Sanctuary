#!/usr/bin/env python3
"""Compare the existing actual physical supplier with complete Tornado captures."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
OUT = STAGE / 'tornado-damage-proof'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(content, native, check=False):
    proof_path = ROOT / 'tools/reference/tornado-damage-source-evidence.json'
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
var tornado=definitions.Single(x=>x.SourceId==245);
Require(tornado.SourceKey=="Tornado"&&DruidCombat.Implemented(CombatAbility.Tornado),"Tornado candidate identity");
var physical=typeof(SkillValues).GetMethod("PhysicalDamage256",BindingFlags.Instance|BindingFlags.NonPublic);
Require(physical!=null,"Existing physical endpoint supplier");
using var json=JsonDocument.Parse(File.ReadAllText(args[0]));int count=0;
foreach(var row in json.RootElement.EnumerateArray())
{
 int rank=row[0].GetInt32(),cyclone=row[1].GetInt32(),twister=row[2].GetInt32(),hurricane=row[3].GetInt32();
 var values=new SkillValues(tornado,key=>key=="Tornado"?rank:255,key=>key=="Cyclone Armor"?cyclone:key=="Twister"?twister:key=="Hurricane"?hurricane:0,99);
 int min=(int)physical.Invoke(values,new object[]{false}),max=(int)physical.Invoke(values,new object[]{true});
 Require(min==row[6].GetInt32()&&max==row[7].GetInt32(),$"Physical truncation rank{rank}/hard{cyclone},{twister},{hurricane}");
 Require(values.Mana256==10*256&&values.Eval("calc1")==1&&values.Eval("calc4")==15&&values.Eval("aurarangecalc")==3,"Current mana/count/pulse/radius formulas");count++;
}
Require(count==27540,"Complete source packet scope");
Console.WriteLine($"{count} actual-content Tornado physical endpoint comparisons/all255ranks pass; supplier scope only; native actor acceptance is separate.");
''')
    with (STAGE / 'tornado-damage-build.log').open('w') as log:
        subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), 'build', str(OUT / 'check.csproj'), '-c', 'Release', '--nologo', '-o', str(OUT / 'out')], cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), str(OUT / 'out/check.dll'), str(fixture),
                          str(ROOT / 'data/Sanctuary/Balance/class-skills.json'), str(native)], capture_output=True, text=True)
    (STAGE / 'tornado-damage-run.log').write_text(run.stdout + run.stderr)
    assert run.returncode == 0, run.stdout + run.stderr
    inputs = [ROOT / name for name in ['src/Sanctuary.Content/SkillMechanics.cs', 'src/Sanctuary.Content/DruidCombat.cs',
              'data/Sanctuary/Balance/class-skills.json', 'tools/check_tornado_damage.py', 'tools/probe_tornado_damage_reference.py',
              'tools/reference/tornado-damage-source-evidence.json']]
    inputs += [fixture, STAGE / 'tornado-damage-run.log', OUT / 'Program.cs', OUT / 'check.csproj', OUT / 'out/check.dll']
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=True, installed=False, sourceEndpointComparisons=27540,
                    contentAssemblySha256=sha(content), nativeAssemblyHashes={str(path.relative_to(native)): sha(path) for _, path in references[1:]},
                    hashes={str(path.relative_to(ROOT)): sha(path) for path in inputs},
                    established=['The actual existing PhysicalDamage256 supplier matches27540complete source captures/all255ranks. Independent hard Cyclone Armor/Twister/Hurricane inputs retain pre-HitShift8 truncation; deliberately255effective synergy ranks do not replace hard points.',
                                 'Actual formula interpreter verifies10mana, one missile,15-frame periodic interval and radius3. No new physical formula implementation is needed.'],
                    remaining=['This supplier check does not establish actual periodic/direct damage commitment, charged path or modern hit-delay overlap.',
                               'Native saved actor, learned input/payment/timing, natural timers/lifecycle/reloads, full current-executable/client and final effects/icons remain separate.'])
    out = ROOT / 'tools/reference/tornado-damage-evidence.json'
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
