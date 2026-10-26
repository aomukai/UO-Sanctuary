#!/usr/bin/env python3
"""Compare the actual reusable C# Druid emitter kernel to complete source bodies."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from xml.sax.saxutils import escape
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';OUT=STAGE/'firestorm-patterns-proof'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def build(check=False):
 source=json.loads((ROOT/'tools/reference/firestorm-source-evidence.json').read_text());fixture=ROOT/source['fixture']['path'];assert sha(fixture)==source['fixture']['sha256']
 OUT.mkdir(parents=True,exist_ok=True)
 (OUT/'check.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>disable</Nullable></PropertyGroup><ItemGroup><Compile Include="'+escape(str(ROOT/'src/Sanctuary.Content/DruidEmitterPatterns.cs'))+'" Link="DruidEmitterPatterns.cs" /></ItemGroup></Project>')
 (OUT/'Program.cs').write_text(r'''
using System.Text.Json;
using Sanctuary;
static int N(JsonElement e,int i)=>e[i].GetInt32();
static int[] Values(DruidEmitterRequest p)=>[p.Flags,p.Missile,p.X,p.Y,p.AimX,p.AimY,p.Charged?1:0,p.Ordinal,p.Loops,p.Rank];
static void Require(bool valid,string why){if(!valid){throw new Exception(why);}}
static void Compare(DruidEmitterRequest[] plans,JsonElement expected,string label)
{Require(JsonSerializer.Serialize(plans.Select(Values).ToArray())==expected.GetRawText(),label);}
using var document=JsonDocument.Parse(File.ReadAllText(args[0]));var data=document.RootElement;int launch=0,initial=0,maker=0;
foreach(var v in data.GetProperty("launch").EnumerateArray())
{
 var aim=new DruidEmitterAim(200,200,N(v,2),N(v,3),N(v,4)!=0,N(v,5)!=0?208:null,N(v,5)!=0?196:null);
 var plans=DruidEmitterPatterns.Launch(N(v,0),N(v,1)!=0,aim,458,7);
 Require(N(v,6)==1,"Source launcher result");Compare(plans,v[7],"Launcher "+launch++);
}
foreach(var v in data.GetProperty("initializer").EnumerateArray())
{
 var p=DruidEmitterPatterns.InitializeCharged(N(v,0),N(v,1),N(v,2));
 Require(p.Frames==N(v,3)&&(p.SetCurrentFrame??0)==N(v,4)&&p.Seed==v[5].GetUInt32()&&p.Distance==N(v,6)&&N(v,7)==1,"Initializer "+initial++);
}
foreach(var v in data.GetProperty("maker").EnumerateArray())
{
 int guard=N(v,0);var p=DruidEmitterPatterns.Maker(guard!=3,guard!=2,guard==1?0:457,N(v,1),N(v,2),200,200,1,N(v,3));
 Require(p.Result==N(v,4)&&p.ProcessCollision==(N(v,5)!=0),"Maker process "+maker);
 Compare(p.Child==null?[]:[p.Child],v[6],"Maker child "+maker++);
}
Require(launch==256&&initial==45&&maker==160,"Fixture scope");
// Production limits are explicit rather than allocating unbounded source counts.
int rejected=0;foreach(var action in new Action[]{()=>DruidEmitterPatterns.Launch(257,true,new(200,200,204,198,true),458,1),()=>DruidEmitterPatterns.InitializeCharged(0,204,0),()=>DruidEmitterPatterns.Launch(3,true,new(200,200,204,198,true,208,null),458,1)})
{try{action();}catch(NotSupportedException){rejected++;}}
Require(rejected==3,"Unsupported context controls");
Console.WriteLine($"{launch}launch/{initial}initializer/{maker}maker complete source comparisons and3unsupported-context controls pass. No world actor/learned skill enabled.");
''')
 with (STAGE/'firestorm-patterns-build.log').open('w') as log:subprocess.run([str(ROOT/'.tools/dotnet/dotnet'),'build',str(OUT/'check.csproj'),'-c','Release','--nologo','-o',str(OUT/'out')],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(ROOT/'.tools/dotnet/dotnet'),str(OUT/'out/check.dll'),str(fixture)],capture_output=True,text=True);(STAGE/'firestorm-patterns-run.log').write_text(run.stdout+run.stderr);assert run.returncode==0,run.stderr
 e=dict(referenceVersion='3.3.93847',handlerEnabled=False,sourceEvidence='tools/reference/firestorm-source-evidence.json',caseCounts=dict(launch=256,initializer=45,maker=160,unsupported=3),inputHashes={str(p.relative_to(ROOT)):sha(p) for p in [ROOT/'src/Sanctuary.Content/DruidEmitterPatterns.cs',ROOT/'tools/probe_firestorm_reference.py',ROOT/'tools/reference/firestorm-source-evidence.json',ROOT/'tools/check_druid_emitter_patterns.py']},evidenceHashes={str(p.relative_to(ROOT)):sha(p) for p in [fixture,STAGE/'firestorm-source-probe.cpp',STAGE/'firestorm-source-probe',STAGE/'firestorm-source-sanitizer.log',STAGE/'firestorm-patterns-build.log',STAGE/'firestorm-patterns-run.log',OUT/'check.csproj',OUT/'Program.cs',OUT/'out/check.dll']},established=['Actual production C# shared emitter requests, charged initializer and changed-cell child requests match every applicable complete older source fixture. Direct/fan target queries and child-before-terminal-collision order remain distinct.','Kernel can be reused by Firestorm/Twister/Tornado without copying ChargedBolts.Cast or its Sorceress-specific owner/damage controller.'],limits=source['conflicts']+source['limits'],next='Compose actual Firestorm emitters and source damage/field lifecycle, then player SC/payment/cooldown/input. This pure plan does not claim these consumers are implemented.')
 path=ROOT/'tools/reference/firestorm-patterns-evidence.json';text=json.dumps(e,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print(run.stdout.strip())
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
