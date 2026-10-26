#!/usr/bin/env python3
"""Compare actual existing content endpoint suppliers with complete Volcano captures."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
OUT = STAGE / 'volcano-damage-proof'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(content, native, check=False):
    proof_path = ROOT / 'tools/reference/volcano-damage-source-evidence.json'
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
var volcano=definitions.Single(x=>x.SourceId==244);
Require(volcano.SourceKey=="Volcano"&&DruidCombat.Implemented(CombatAbility.Volcano),"Volcano candidate identity");
var physical=typeof(SkillValues).GetMethod("PhysicalDamage256",BindingFlags.Instance|BindingFlags.NonPublic);
var fire=typeof(DruidFirestorm).GetMethod("Endpoints",BindingFlags.Static|BindingFlags.NonPublic);
Require(physical!=null&&fire!=null,"Existing physical and fire endpoint suppliers");
using var json=JsonDocument.Parse(File.ReadAllText(args[0]));int packets=0,air=0;
foreach(var row in json.RootElement.EnumerateArray())
{
 int kind=row[0].GetInt32(),rank=row[1].GetInt32(),boulder=row[2].GetInt32(),fissure=row[3].GetInt32(),armageddon=row[4].GetInt32(),mastery=row[5].GetInt32();
 if(kind==1){air++;continue;} // No native debris actor is claimed by this existing-supplier check.
 var values=new SkillValues(volcano,key=>key=="Volcano"?rank:255,key=>key=="Molten Boulder"?boulder:key=="Eruption"?fissure:key=="Armageddon"?armageddon:0,99);
 int min=(int)physical.Invoke(values,new object[]{false}),max=(int)physical.Invoke(values,new object[]{true});
 var ends=((int Min,int Max))fire.Invoke(null,new object[]{values,mastery,new SlowMissilePayload(100,100)});
 Require(min==row[7].GetInt32()&&max==row[8].GetInt32()&&ends.Min==row[9].GetInt32()&&ends.Max==row[10].GetInt32(),$"Root/lava endpoints rank{rank}/hard{boulder},{fissure},{armageddon}/mastery{mastery}");
 Require(values.Mana256==25*256&&values.Eval("calc4")==2&&values.Eval("aurarangecalc")==12,"Current mana/emission/range formulas");packets++;
}
Require(packets==55080&&air==27540,"Complete source capture selection");
Console.WriteLine($"{packets} actual-content Volcano root/lava endpoint sets/all255ranks match complete source captures; {air} source-only air cases remain outside native scope. Supplier comparison only; world acceptance is recorded separately.");
''')
    with (STAGE / 'volcano-damage-build.log').open('w') as log:
        subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), 'build', str(OUT / 'check.csproj'), '-c', 'Release', '--nologo', '-o', str(OUT / 'out')], cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), str(OUT / 'out/check.dll'), str(fixture),
                          str(ROOT / 'data/Sanctuary/Balance/class-skills.json'), str(native)], capture_output=True, text=True)
    (STAGE / 'volcano-damage-run.log').write_text(run.stdout + run.stderr)
    assert run.returncode == 0, run.stdout + run.stderr
    inputs = [ROOT / p for p in ['src/Sanctuary.Content/SkillMechanics.cs', 'src/Sanctuary.Content/DruidFirestorm.cs',
                                 'src/Sanctuary.Content/DruidCombat.cs', 'data/Sanctuary/Balance/class-skills.json',
                                 'tools/check_volcano_damage.py', 'tools/probe_volcano_damage_reference.py',
                                 'tools/reference/volcano-damage-source-evidence.json']]
    inputs += [fixture, STAGE / 'volcano-damage-run.log', OUT / 'Program.cs', OUT / 'check.csproj', OUT / 'out/check.dll']
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=True, installed=False,
                    sourceEndpointSets=82620, actualContentEndpointSets=55080, sourceOnlyAirCases=27540,
                    contentAssemblySha256=sha(content), nativeAssemblyHashes={str(p.relative_to(native)): sha(p) for _, p in references[1:]},
                    hashes={str(p.relative_to(ROOT)): sha(p) for p in inputs},
                    established=[
                        'Existing actual content PhysicalDamage256 and DruidFirestorm.Endpoints match all55080root/lava source endpoint sets/all255ranks. Independent hard Boulder/Fissure/Armageddon inputs remain separate from deliberately255effective synergy ranks.',
                        'Pre-HitShift physical truncation, post-HitShift fire synergy and one fire-mastery application match. Mana25, range12 and emission interval2 evaluate through the actual existing formula interpreter. No new duplicate formula or endpoint supplier is needed.'
                    ], remaining=[
                        'Air debris has27540source-only zero packet controls; no native air actor is claimed by this supplier check.',
                        'Saved vent/debris/lava actors, actual damage/collision, modern NHD grouping, learned input/payment/local delay, current executable/client and final effects/icons remain separate.'
                    ])
    out = ROOT / 'tools/reference/volcano-damage-evidence.json'
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
