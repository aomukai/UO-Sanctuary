#!/usr/bin/env python3
"""Check the portable event25 numeric consumer against complete source vectors."""
from pathlib import Path
import argparse,hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
PROGRAM=r'''
using System.Text.Json;
using Sanctuary;
using var doc=JsonDocument.Parse(File.ReadAllText(args[0]));int count=0;
foreach(var row in doc.RootElement.EnumerateArray())
{
 int V(int i)=>row[i].GetInt32();if(V(0)!=0){continue;}
 var r=CycloneArmorPackets.Absorb(new(V(1),V(2),V(6)),V(3),V(4),V(5));
 if(r.Applied!=(V(7)!=0)||r.Pool.Remaining256!=V(8)||r.Fire256!=V(9)||r.Cold256!=V(10)||r.Lightning256!=V(11)
  ||r.Removed!=(V(12)!=0)||r.DisplayChanged!=(V(14)!=0)||r.Pool.DisplayPercent!=V(15))throw new Exception("Source event25 mismatch");
 if(JsonSerializer.Deserialize<CycloneArmorHit>(JsonSerializer.Serialize(r))!=r)throw new Exception("Packet JSON round-trip");count++;
}
if(count!=9072)throw new Exception("Incomplete applicable event25 vectors");
var ordered=CycloneArmorPackets.Absorb(new(300,1024),256,128,200);
if(ordered.Fire256!=0||ordered.Cold256!=84||ordered.Lightning256!=200||!ordered.Removed)throw new Exception("Fractional shared-pool/channel order");
foreach(int kind in new[]{0,1,2,3,4})
{
 bool refused=false;try{CycloneArmorPackets.Absorb(new(kind==0?-1:100,kind==1?-1:100),kind==2?-1:1,kind==3?-1:1,kind==4?-1:1);}catch(NotSupportedException){refused=true;}
 if(!refused)throw new Exception("Negative state/damage accepted");
}
Console.WriteLine("9072source packet comparisons/JSON plus fractional-order and negative-state controls passed; no native handler enabled.");
'''
def build(check=False):
 subprocess.run(['python3',str(ROOT/'tools/probe_cyclone_armor_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 stage=STAGE/'cyclone-armor-packets';stage.mkdir(exist_ok=True)
 (stage/'Program.cs').write_text(PROGRAM)
 paths=['src/Sanctuary.Content/CycloneArmorPackets.cs','src/Sanctuary.Content/SourcePercentage.cs']
 project='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="Program.cs"/>'+''.join(f'<Compile Include="{ROOT/p}"/>' for p in paths)+'</ItemGroup></Project>'
 (stage/'check.csproj').write_text(project)
 with (STAGE/'cyclone-armor-packets-build.log').open('w') as log:subprocess.run([str(ROOT/'.tools/dotnet/dotnet'),'build',str(stage/'check.csproj'),'-c','Release','-o',str(stage/'out')],check=True,stdout=log,stderr=subprocess.STDOUT)
 run=subprocess.run([str(ROOT/'.tools/dotnet/dotnet'),str(stage/'out/check.dll'),str(STAGE/'cyclone-armor-source-probe.json')],check=True,capture_output=True,text=True);(STAGE/'cyclone-armor-packets-check.log').write_text(run.stdout+run.stderr)
 def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
 e=dict(referenceVersion='3.3.93847',handlerEnabled=False,scope='Portable numeric event25 consumer; native buff/casting/producer integration remains separate.',sourceEvidence='tools/reference/cyclone-armor-source-evidence.json',comparisons=9072,jsonRoundTrips=9072,fractionalOrderControls=1,negativeControls=5,inputHashes={p:sha(ROOT/p) for p in paths},fixtureSha256=sha(STAGE/'cyclone-armor-source-probe.json'),limits=['This check compiles the actual portable C# files but not the complete content assembly or a native incoming damage event.','Source/native state lifetime/recast rank priority, actor persistence, client self input, payment and complete incoming producer routing remain to connect.','The older directed gfx-percentage comparison is kept as source context; modern display behavior remains uncertain.'])
 p=ROOT/'tools/reference/cyclone-armor-packets-evidence.json';text=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==text
 else:p.write_text(text)
 print(run.stdout.strip())
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
