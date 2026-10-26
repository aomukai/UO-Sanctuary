#!/usr/bin/env python3
"""Compare actual reusable Boulder C# kernels to complete source callback fixtures."""
from pathlib import Path
from xml.sax.saxutils import escape
import argparse,hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';OUT=STAGE/'molten-boulder-packets-proof'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 source=json.loads((ROOT/'tools/reference/molten-boulder-source-evidence.json').read_text());fixture=ROOT/source['fixture']['path'];assert sha(fixture)==source['fixture']['sha256'];OUT.mkdir(parents=True,exist_ok=True)
 files=['DruidEmitterPatterns','SourceRandom','MoltenBoulderPackets'];includes=''.join('<Compile Include="'+escape(str(ROOT/f'src/Sanctuary.Content/{name}.cs'))+'" Link="'+name+'.cs" />' for name in files)
 (OUT/'check.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>disable</Nullable></PropertyGroup><ItemGroup>'+includes+'</ItemGroup></Project>')
 (OUT/'Program.cs').write_text(r'''
using System.Text.Json;
using Sanctuary;
static int N(JsonElement e,int i)=>e[i].GetInt32();
static void Require(bool value,string why){if(!value){throw new Exception(why);}}
static int[] Values(DruidEmitterRequest p,bool owner)=>[p.Flags,p.Missile,p.X,p.Y,p.AimX,p.AimY,p.RangeOverride??0,p.Rank,owner?1:0];
static void Compare(DruidEmitterRequest[] actual,JsonElement expected,bool owner,string why)
{Require(JsonSerializer.Serialize(actual.Select(x=>Values(x,owner)).ToArray())==expected.GetRawText(),why);}
using var json=JsonDocument.Parse(File.ReadAllText(args[0]));var rows=json.RootElement;int count=0;
foreach(var v in rows.GetProperty("emerge").EnumerateArray())
{
 int g=N(v,0);var p=MoltenBoulderPackets.Emerge(g!=1,g!=2,g==3?-1:g==5?0:452,300,400,N(v,1),N(v,2),20);
 Require(p.Result==N(v,3)&&!p.Collision,"Emergence result");Compare(p.Requests,v[4],g!=4,"Emergence attempts");count++;
}
foreach(var v in rows.GetProperty("maker").EnumerateArray())
{
 int g=N(v,0);var p=MoltenBoulderPackets.Trail(g!=2,g!=3,g!=1,g!=5,g==4?-1:g==6?0:455,N(v,1),300,400,20,N(v,2));
 Require(p.Result==N(v,3)&&p.Collision==(N(v,4)!=0),"Maker process");Compare(p.Requests,v[5],true,"Maker attempts");count++;
}
foreach(var v in rows.GetProperty("disc").EnumerateArray())
{Compare(MoltenBoulderPackets.Disc(N(v,0),N(v,1),N(v,2),300,400,20),v[3],true,"Disc requests");count++;}
foreach(var v in rows.GetProperty("contact").EnumerateArray())
{
 int g=N(v,0),flags=N(v,1);var c=new BoulderContactInput(g!=1,g!=2,g!=3,g==4?null:g==5?0:1,g!=6,(flags&2)!=0,g!=7,N(v,2),N(v,3),N(v,4),455,300,400,20,2,5,0,64,N(v,5)!=0);
 var p=MoltenBoulderPackets.Contact(c);Require(p.Result==N(v,6)&&p.FillDamage==(N(v,7)!=0),"Contact result/fill ordering");
 int[][] area=p.Area==null?[]:[[p.Area.Radius,257,514,(int)p.Area.HitFlags,p.Area.ResultFlags]];
 Require(JsonSerializer.Serialize(area)==v[8].GetRawText(),"Area flags/radius");Compare(p.Requests,v[9],true,"Contact disc requests");count++;
}
foreach(var v in rows.GetProperty("knockback").EnumerateArray())
{
 int g=N(v,0),flags=N(v,2);var p=MoltenBoulderPackets.Knockback(g!=1,g==2?1:3,g!=1&&g!=5,N(v,1),g!=3,g!=4,(flags&1)!=0,(flags&2)!=0,N(v,3),N(v,4),64,SourceRandom.InitLow(unchecked((int)v[5].GetUInt32())));
 Require(p.ResultFlags==N(v,6)&&p.Seed==v[7].GetUInt64(),"Complete damage14 flags/RNG");count++;
}
foreach(var v in rows.GetProperty("field").EnumerateArray())
{
 int g=N(v,0);var p=MoltenBoulderPackets.Animate(g!=1,g!=2,N(v,1),N(v,2),SourceRandom.InitLow(unchecked((int)v[3].GetUInt32())),12,36,1);
 Require(p.Result==N(v,4)&&p.Collision==(N(v,5)!=0)&&p.SetMask==(N(v,6)!=0)&&p.Animation256==N(v,7)&&p.Seed==v[8].GetUInt64(),"Complete do5 animation/mask/RNG");count++;
}
Require(count==1081,"Incomplete source scope");Console.WriteLine($"{count}complete Boulder callback/RNG comparisons pass; shared request range overrides retained; no native controller/learned handler enabled.");
''')
 with (STAGE/'molten-boulder-packets-build.log').open('w') as log:subprocess.run([str(ROOT/'.tools/dotnet/dotnet'),'build',str(OUT/'check.csproj'),'-c','Release','--nologo','-o',str(OUT/'out')],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(ROOT/'.tools/dotnet/dotnet'),str(OUT/'out/check.dll'),str(fixture)],capture_output=True,text=True);(STAGE/'molten-boulder-packets-run.log').write_text(run.stdout+run.stderr);assert run.returncode==0,run.stderr
 inputs=[ROOT/f'src/Sanctuary.Content/{name}.cs' for name in files]+[ROOT/'tools/probe_molten_boulder_reference.py',ROOT/'tools/reference/molten-boulder-source-evidence.json',ROOT/'tools/check_molten_boulder_packets.py']
 evidence=[fixture,STAGE/'molten-boulder-source-probe.cpp',STAGE/'molten-boulder-source-probe',STAGE/'molten-boulder-source-sanitizer.log',STAGE/'molten-boulder-packets-build.log',STAGE/'molten-boulder-packets-run.log',OUT/'check.csproj',OUT/'Program.cs',OUT/'out/check.dll']
 e=dict(referenceVersion='3.3.93847',handlerEnabled=False,installed=False,caseCounts=source['caseCounts'],totalCases=1081,sourceEvidence='tools/reference/molten-boulder-source-evidence.json',inputHashes={str(p.relative_to(ROOT)):sha(p) for p in inputs},evidenceHashes={str(p.relative_to(ROOT)):sha(p) for p in evidence},established=['Actual production request/contact/knockback/server-field animation kernels match1081complete source callback contexts. Missing services, zero versus negative IDs, supplied range overrides, captured packet flags, selected species flags and exact RNG mutations remain distinct.','DruidEmitterRequest now has an optional RangeOverride; existing Firestorm plans retain null/default behavior and their retained source comparisons are rerun separately.'],limits=source['limits'],next='Compose generic hit/factory/InitSteps/field-animation producer and damage paths, then connect saved native Boulder movement/contacts/control/lifetimes and learned casting. No handler count increase follows from these pure kernels.')
 p=ROOT/'tools/reference/molten-boulder-packets-evidence.json';text=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==text
 else:p.write_text(text)
 print(run.stdout.strip())
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
