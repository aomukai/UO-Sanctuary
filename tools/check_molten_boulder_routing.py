#!/usr/bin/env python3
"""Compare the production Boulder hit plan against complete source routing."""
import hashlib
import json
import subprocess
from pathlib import Path
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
OUT = STAGE / 'molten-boulder-routing-proof'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build():
    source = json.loads((ROOT / 'tools/reference/molten-boulder-routing-source-evidence.json').read_text())
    fixture = ROOT / source['fixture']['path']
    assert sha(fixture) == source['fixture']['sha256']
    OUT.mkdir(parents=True, exist_ok=True)
    files = ['DruidEmitterPatterns', 'SourceRandom', 'MoltenBoulderPackets']
    includes = ''.join('<Compile Include="' + escape(str(ROOT / f'src/Sanctuary.Content/{name}.cs')) + '" Link="' + name + '.cs" />' for name in files)
    (OUT / 'check.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>disable</Nullable></PropertyGroup><ItemGroup>' + includes + '</ItemGroup></Project>')
    (OUT / 'Program.cs').write_text(r'''
using System.Text.Json;
using Sanctuary;
static int N(JsonElement e,int i)=>e[i].GetInt32();
static void Check(bool value,string why){if(!value){throw new Exception(why);}}
using var json=JsonDocument.Parse(File.ReadAllText(args[0]));int count=0;
foreach(var v in json.RootElement.EnumerateArray())
{
 int kind=N(v,0),victim=N(v,1),flags=N(v,2),repeat=N(v,3),terminal=N(v,4),owned=N(v,5),friendly=N(v,6),success=N(v,7);
 int? type=victim==0?null:victim==2?0:victim==3?4:1;
 var contact=new BoulderContactInput(true,true,owned!=0,type,true,(flags&2)!=0,true,0,7,1,455,300,400,20,2,5,0,64,success!=0);
 var p=MoltenBoulderPackets.Route(kind==0?453:kind==1?452:455,victim!=0,repeat!=0,owned!=0,friendly==0,terminal!=0,contact,300,400,320,405,20);
 int fills=(p.Contact?.FillDamage==true?1:0)+(p.DirectDamage?1:0);
 Check(p.Result==N(v,8)&&fills==N(v,9)&&(p.DirectDamage?1:0)==N(v,10),"Route/direct/fill order");
 Check((p.Contact?.Area==null?0:1)==N(v,11)&&p.Requests.Length==N(v,12),"Area/child requests");
 Check((p.HitEvent?1:0)==N(v,13)&&(p.ResetMask?1:0)==N(v,14),"Event/reset boundary");
 int lastType=p.WriteLastTarget?type.Value:repeat!=0?type??1:0;
 int lastId=p.WriteLastTarget||repeat!=0?10:-1;
 Check(lastType==N(v,15)&&lastId==N(v,16),"LastCollide update order");
 ulong seed=SourceRandom.InitLow(123);
 if(p.DirectDamage&&kind==1)
 {seed=MoltenBoulderPackets.Knockback(true,3,true,type.Value,true,true,(flags&1)!=0,(flags&2)!=0,1,50,64,seed).Seed;}
 Check(seed==v[17].GetUInt64(),"Composed damage14 seed");count++;
}
Check(count==1536,"Incomplete route contexts");
bool refused=false;try{MoltenBoulderPackets.Route(458,false,false,true,true,false,null,1,1,2,2,1);}catch(NotSupportedException){refused=true;}
Check(refused,"Unsupported missile accepted");
Console.WriteLine($"{count} complete source routing/LastCollide/knockback-seed contexts match production C#; unsupported row refused.");
''')
    with (STAGE / 'molten-boulder-routing-csharp-build.log').open('w') as log:
        subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), 'build', str(OUT / 'check.csproj'), '-c', 'Release', '--nologo', '-o', str(OUT / 'out')], stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(ROOT / '.tools/dotnet/dotnet'), str(OUT / 'out/check.dll'), str(fixture)], capture_output=True, text=True)
    (STAGE / 'molten-boulder-routing-csharp-run.log').write_text(run.stdout + run.stderr)
    assert run.returncode == 0, run.stderr
    inputs = [ROOT / f'src/Sanctuary.Content/{name}.cs' for name in files] + [ROOT / 'tools/check_molten_boulder_routing.py', ROOT / 'tools/reference/molten-boulder-routing-source-evidence.json']
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False, cases=1536,
        inputHashes={str(p.relative_to(ROOT)): sha(p) for p in inputs},
        fixtureHash=sha(fixture),
        established=['Production scoped hit plans match complete source router/callback contexts including LastCollide writes before hostility, direct versus area packets, terminal removal, emergence requests and composed damage14 seed.'],
        limits=source['limits'])
    (ROOT / 'tools/reference/molten-boulder-routing-evidence.json').write_text(json.dumps(evidence, indent=2) + '\n')
    print(run.stdout.strip())


if __name__ == '__main__':
    build()
