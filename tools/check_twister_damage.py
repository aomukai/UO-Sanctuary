#!/usr/bin/env python3
"""Compare the actual content physical endpoint supplier to captured Twister source."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
OUT = STAGE / 'twister-damage-proof'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(content, native, check=False):
    proof_path = ROOT / 'tools/reference/twister-damage-source-evidence.json'
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
var twister=definitions.Single(x=>x.SourceId==240);
Require(twister.SourceKey=="Twister"&&!DruidCombat.Implemented(CombatAbility.Twister),"Twister source-only scope");
var physical=typeof(SkillValues).GetMethod("PhysicalDamage256",BindingFlags.Instance|BindingFlags.NonPublic);
Require(physical!=null,"Existing physical endpoint supplier");
using var json=JsonDocument.Parse(File.ReadAllText(args[0]));int count=0;
foreach(var row in json.RootElement.EnumerateArray())
{
 int rank=row[1].GetInt32(),tornado=row[2].GetInt32(),hurricane=row[3].GetInt32();
 // Soft synergy ranks are deliberately unrelated to the hard-point inputs.
 var values=new SkillValues(twister,key=>key=="Twister"?rank:255,key=>key=="Tornado"?tornado:key=="Hurricane"?hurricane:0,99);
 int min=(int)physical.Invoke(values,new object[]{false}),max=(int)physical.Invoke(values,new object[]{true});
 Require(min==row[6].GetInt32()&&max==row[7].GetInt32(),$"Physical truncation/rank{rank}/hard{tornado},{hurricane}");
 Require(values.Mana256==7*256&&values.Eval("calc1")==3,"Current mana/count formulas");count++;
}
Require(count==9180,"Complete source packet scope");int stun=0;
for(int rank=1;rank<=255;rank++)foreach(int hard in new[]{0,1,20})
{
 var values=new SkillValues(twister,key=>key=="Twister"?rank:255,key=>key=="Arctic Blast"?hard:0,99);
 Require(values.Eval("calc2")==10+2*hard,"Modern hard-only Arctic Blast duration formula");stun++;
}
Console.WriteLine($"{count} actual-content Twister source endpoint comparisons/all255ranks and {stun} modern duration formula controls pass; no learned handler enabled.");
''')
    with (STAGE / 'twister-damage-build.log').open('w') as log:
        subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), 'build', str(OUT / 'check.csproj'), '-c', 'Release',
                        '--nologo', '-o', str(OUT / 'out')], cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), str(OUT / 'out/check.dll'), str(fixture),
                          str(ROOT / 'data/Sanctuary/Balance/class-skills.json'), str(native)], capture_output=True, text=True)
    (STAGE / 'twister-damage-run.log').write_text(run.stdout + run.stderr)
    assert run.returncode == 0, run.stdout + run.stderr
    inputs = [ROOT / p for p in ['src/Sanctuary.Content/SkillMechanics.cs', 'src/Sanctuary.Content/DruidCombat.cs',
                                  'data/Sanctuary/Balance/class-skills.json', 'tools/check_twister_damage.py',
                                  'tools/probe_twister_damage_reference.py', 'tools/reference/twister-damage-source-evidence.json']]
    inputs += [fixture, STAGE / 'twister-damage-run.log', OUT / 'Program.cs', OUT / 'check.csproj', OUT / 'out/check.dll']
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False,
                    sourceEndpointComparisons=9180, modernDurationFormulaControls=765,
                    contentAssemblySha256=sha(content), nativeAssemblyHashes={str(p.relative_to(native)): sha(p) for _, p in references[1:]},
                    hashes={str(p.relative_to(ROOT)): sha(p) for p in inputs},
                    established=['The actual existing PhysicalDamage256 supplier matches all9180complete source captures/all255ranks, preserving pre-HitShift physical synergy truncation and independent hard Tornado/Hurricane points.',
                                 'The actual formula interpreter yields10+2*hard Arctic Blast frames for all255effective Twister ranks with0/1/20hard points despite255soft Arctic Blast rank. This is a formula check, not modern stun application acceptance.'],
                    remaining=['Native saved charged flight, collision, physical damage commitment and modern stun delivery are not implemented by this proof.',
                               'Learned SC/payment/bindings/input, exact modern NHD grouping, source/native/current-executable/client acceptance and final effects/icons remain separate.'])
    path = ROOT / 'tools/reference/twister-damage-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print(run.stdout.strip())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--content', required=True, type=Path)
    parser.add_argument('--native', default=ROOT / 'upstream/ModernUO/Distribution', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    build(args.content.resolve(), args.native.resolve(), args.check)
