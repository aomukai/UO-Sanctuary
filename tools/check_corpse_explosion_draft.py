#!/usr/bin/env python3
"""Check an uninstalled do55 packet draft using the existing SourcePercentage.

No native learned handler or installed assembly is changed by this probe.
"""
from pathlib import Path
import argparse, hashlib, json, subprocess
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'
CSHARP = r'''
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;
internal sealed record CorpseExplosionPacket(int Physical256,int Fire256,int InnerSquared,int OuterRange)
{
    internal (int Physical256,int Fire256) Recipient(int squaredDistance)
        =>(squaredDistance>InnerSquared?0:Physical256,Fire256);
}
// The complete do55 packet order with base-life, random and elemental services
// explicit. Corpse eligibility/consumption, area enumeration and defenses are
// separate consumers; this component is not a learned skill implementation.
internal static class CorpseExplosionPackets
{
    internal static int MonsterBase256(int minimum,int maximum)=>checked((int)(((long)minimum+maximum)*128));
    internal static CorpseExplosionPacket Create(int maximum256,int casterLevel,int corpseLevel,int range,
        int minimumPercent,int maximumPercent,int firePercent,Func<int,int> roll)
    {
        ArgumentNullException.ThrowIfNull(roll);
        if(maximum256<0||casterLevel<0||corpseLevel<0||range<0||minimumPercent<0||maximumPercent<minimumPercent)
        {throw new NotSupportedException("Unsupported corpse-explosion packet context");}
        int minimum=SourcePercentage.Calculate(minimumPercent,maximum256,100);
        int maximum=SourcePercentage.Calculate(maximumPercent,maximum256,100);
        int offset=roll(maximum-minimum);
        if(offset<0||offset>=maximum-minimum&&offset!=0){throw new ArgumentOutOfRangeException(nameof(roll));}
        int amount=checked(minimum+offset);
        if(corpseLevel!=0&&casterLevel<corpseLevel){amount=SourcePercentage.Calculate(amount,casterLevel,corpseLevel);}
        firePercent=Math.Clamp(firePercent,0,100);
        int fire=firePercent>0?SourcePercentage.Calculate(amount,firePercent,100):0;
        int physical=firePercent>0?SourcePercentage.Calculate(amount,100-firePercent,100):amount;
        int half=range/2;return new(physical,fire,checked(half*half),(range+1)/2);
    }
}
'''
PROGRAM = r'''
using System.Text.Json;
using Sanctuary;
string root=args[0];
using var doc=JsonDocument.Parse(File.ReadAllText(root+"/.local/class-skills/corpse-explosion-source-probe.json"));
int count=0,monsterCount=0;
foreach(var row in doc.RootElement.EnumerateArray())
{
    int V(int i)=>row[i].GetInt32();if(V(6)==0){continue;}
    int mask=V(0),range=10+V(1)-1,life=V(2),bound=-1;
    int base256=(mask&8)!=0?CorpseExplosionPackets.MonsterBase256(life,life+3):life*256;
    var packet=CorpseExplosionPackets.Create(base256,50,V(3),range,40,80,50,n=>{bound=n;return n==0?0:V(4)==0?0:V(4)==1?n/2:n-1;});
    int distance=range/2+V(5)-1;var hit=packet.Recipient(distance*distance);
    if(bound!=V(9)||packet.OuterRange!=V(8)||hit.Physical256!=V(10)||hit.Fire256!=V(11))
    {throw new Exception("Corpse explosion differential mismatch: "+row.GetRawText()+" / "+JsonSerializer.Serialize(packet));}
    var restored=JsonSerializer.Deserialize<CorpseExplosionPacket>(JsonSerializer.Serialize(packet));
    if(restored!=packet){throw new Exception("Packet round-trip mismatch");}
    count++;if((mask&8)!=0){monsterCount++;}
}
if(count!=1080||monsterCount!=540){throw new Exception("Corpse explosion packet coverage incomplete");}
// The source splits each half separately; odd total damage can lose one FP unit.
var fractional=CorpseExplosionPackets.Create(25600,50,50,11,40,80,50,n=>n-1);
if(fractional.Physical256!=10239||fractional.Fire256!=10239
    ||fractional.Recipient(25).Physical256!=10239||fractional.Recipient(26).Physical256!=0
    ||fractional.Recipient(26).Fire256!=10239||fractional.OuterRange!=6)
{throw new Exception("Exclusive endpoint, separate half rounding or physical edge clip");}
Console.WriteLine("Uninstalled corpse explosion packet draft:1080 complete applicable source comparisons (540 real monster-predicate contexts,540 declared nonmonster controls), shared SourcePercentage, exclusive random bound and separate physical/fire edge checks, JSON round-trips pass. No learned handler changed.");
'''
PROJECT = r'''
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
<ItemGroup><Compile Include="../../../src/Sanctuary.Content/SourcePercentage.cs" /></ItemGroup></Project>
'''
parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
proof=json.loads((ROOT/'tools/reference/corpse-explosion-source-evidence.json').read_text())
fixture=ROOT/proof['fixture']['path'];assert hashlib.sha256(fixture.read_bytes()).hexdigest()==proof['fixture']['sha256']
stage=STAGE/'corpse-explosion-draft';stage.mkdir(exist_ok=True)
for name,content in [('CorpseExplosionPackets.cs',CSHARP),('Program.cs',PROGRAM),('Probe.csproj',PROJECT)]:
    (stage/name).write_text(content.lstrip('\n'))
run=subprocess.run([str(ROOT/'.tools/dotnet/dotnet'),'run','--project',str(stage/'Probe.csproj'),'-c','Release','--',str(ROOT)],capture_output=True,text=True,check=True)
assert not run.stderr and '1080 complete applicable source comparisons' in run.stdout,run.stdout+run.stderr
log=STAGE/'corpse-explosion-draft-run.log';log.write_text(run.stdout)
paths=[fixture,log,ROOT/'src/Sanctuary.Content/SourcePercentage.cs',*[stage/name for name in ['CorpseExplosionPackets.cs','Program.cs','Probe.csproj']]]
evidence=dict(referenceVersion='3.3.93847',sourceEvidence='tools/reference/corpse-explosion-source-evidence.json',installed=False,handlerEnabled=False,applicableComparisons=1080,monsterComparisons=540,nonmonsterServiceControls=540,jsonRoundTrips=1080,
    hashes={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths},
    scope='Uninstalled C# do55 packet draft reuses existing SourcePercentage and matches complete legacy producer/callback outputs for supported current NPC312 fire contexts, including level scaling, exclusive random bounds and physical edge clip.',
    limits=['Base-life lookup, corpse eligibility/consumption, seed allocation/advancement, area enumeration and defenses remain separate. Nonmonster outputs are supplied-predicate controls, not real selectable corpses.','No native actor, learned handler, current executable equivalence, saved world or retirement is certified; the draft is outside Sanctuary.Content.'])
p=ROOT/'tools/reference/corpse-explosion-draft-evidence.json'
if args.check:assert json.loads(p.read_text())==evidence
else:p.write_text(json.dumps(evidence,indent=2)+'\n')
print(run.stdout,end='')
