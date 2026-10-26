#!/usr/bin/env python3
"""Compare actual existing content endpoint suppliers with complete Armageddon captures."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
OUT = STAGE / 'armageddon-damage-proof'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(content, native, check=False):
    proof_path = ROOT / 'tools/reference/armageddon-damage-source-evidence.json'
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
var armageddon=definitions.Single(x=>x.SourceId==249);
Require(armageddon.SourceKey=="Armageddon"&&DruidCombat.Implemented(CombatAbility.Armageddon),"Armageddon candidate identity");
var summons=JsonSerializer.Deserialize<SummonSource[]>(File.ReadAllText(args[3]),options);
var fields=(Dictionary<string,Dictionary<string,JsonElement>>)typeof(NecromancerSummons).GetField("_sources",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
fields.Clear();foreach(var row in summons)fields.Add(row.Key,row.Fields);
var endpoint=typeof(DruidArmageddon).GetMethod("Endpoints",BindingFlags.Static|BindingFlags.NonPublic);
Require(endpoint!=null,"Existing Armageddon physical/fire/own-row endpoint composition");
using var json=JsonDocument.Parse(File.ReadAllText(args[0]));int packets=0;
foreach(var row in json.RootElement.EnumerateArray())
{
 int kind=row[0].GetInt32(),rank=row[1].GetInt32(),boulder=row[2].GetInt32(),firestorm=row[3].GetInt32(),volcano=row[4].GetInt32(),mastery=row[5].GetInt32();
 var values=new SkillValues(armageddon,key=>key=="Armageddon"?rank:255,key=>key=="Molten Boulder"?boulder:key=="Firestorm"?firestorm:key=="Volcano"?volcano:0,99);
 object result=endpoint.Invoke(null,new object[]{values,kind==0?577:623,mastery,SlowMissilePayload.Normal});
 int V(string key)=>(int)result.GetType().GetProperty(key).GetValue(result);
 Require(V("PhysicalMin")==row[7].GetInt32()&&V("PhysicalMax")==row[8].GetInt32()&&V("FireMin")==row[9].GetInt32()&&V("FireMax")==row[10].GetInt32(),$"Control/ground endpoints rank{rank}/hard{boulder},{firestorm},{volcano}/mastery{mastery}");
 Require(values.Mana256==35*256&&values.Param(4)==6&&values.Eval("aurarangecalc")==8,"Current mana/emission/range formulas");packets++;
}
foreach(int rank in new[]{1,20,255})foreach(int hard in new[]{0,1,20})
{
 var values=new SkillValues(armageddon,key=>rank,key=>key=="Eruption"?hard:0,99);
 Require(values.Eval("auralencalc")==250+50*hard,"Hard-only Fissure duration at retained rank");
}
Require(packets==55080,"Complete source capture selection");
Console.WriteLine($"{packets} actual-content Armageddon control/ground endpoint sets/all255ranks match complete source captures;9hard-duration controls pass. Supplier comparison only; world acceptance is separate.");
''')
    with (STAGE / 'armageddon-damage-build.log').open('w') as log:
        subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), 'build', str(OUT / 'check.csproj'), '-c', 'Release', '--nologo', '-o', str(OUT / 'out')], cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), str(OUT / 'out/check.dll'), str(fixture),
                          str(ROOT / 'data/Sanctuary/Balance/class-skills.json'), str(native), str(ROOT / 'data/Sanctuary/Balance/necromancer-summons.json')], capture_output=True, text=True)
    (STAGE / 'armageddon-damage-run.log').write_text(run.stdout + run.stderr)
    assert run.returncode == 0, run.stdout + run.stderr
    inputs = [ROOT / p for p in ['src/Sanctuary.Content/SkillMechanics.cs', 'src/Sanctuary.Content/DruidFirestorm.cs',
                                 'src/Sanctuary.Content/DruidCombat.cs', 'src/Sanctuary.Content/DruidArmageddon.cs', 'data/Sanctuary/Balance/class-skills.json', 'data/Sanctuary/Balance/necromancer-summons.json',
                                 'tools/check_armageddon_damage.py', 'tools/probe_armageddon_damage_reference.py',
                                 'tools/reference/armageddon-damage-source-evidence.json']]
    inputs += [fixture, STAGE / 'armageddon-damage-run.log', OUT / 'Program.cs', OUT / 'check.csproj', OUT / 'out/check.dll']
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=True, installed=False,
                    sourceEndpointSets=55080, actualContentEndpointSets=55080, durationControls=9,
                    contentAssemblySha256=sha(content), nativeAssemblyHashes={str(p.relative_to(native)): sha(p) for _, p in references[1:]},
                    hashes={str(p.relative_to(ROOT)): sha(p) for p in inputs},
                    established=[
                        'Actual Armageddon Endpoints composes existing PhysicalDamage256, DruidFirestorm.Endpoints, NecromancerSummons.Formula and FireGround.MissileDamage256. All55080control/ground source captures/all255ranks match, including independent hard Firestorm/Boulder/Volcano inputs, mastery controls and unrelated item-stat source controls.',
                        'Control physical synergy truncates before HitShift8; control fire after HitShift8/mastery once; own ground fire uses7percent hard synergies before HitShift2 without base mastery. Existing interpreter preserves35mana,6-frame interval, radius8 and hard-only Fissure duration250+50points across9controls.'
                    ], remaining=[
                        'This endpoint-only candidate check uses declared numeric mastery/hard-point services and an unslowed payload. It is separate from real gear, Slow Missiles, actual emission/terminal/ground health and defense acceptance.',
                        'Native saved actors/weather/casting/payment/input/natural timers/lifecycle/reloads, full class regression, matching client/installation and modern executable/client limits remain pending. No class certificate or final assets are enabled.'])
    out = ROOT / 'tools/reference/armageddon-damage-evidence.json'
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
