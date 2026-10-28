#!/usr/bin/env python3
"""Execute existing Fend clock helpers against the prepared Fury source vectors."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from xml.sax.saxutils import escape

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'druid-fury-clock-native'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    sourceproof=ROOT/'tools/reference/druid-fury-clock-engine-evidence.json';source=json.loads(sourceproof.read_text())
    for name,digest in source['artifacts'].items():assert sha(ROOT/name)==digest,name
    vectors=BASE/'druid-fury-clock-source-probe.json';assert len(json.loads(vectors.read_text())['preparedSchedules'])==1308
    content=BASE/'druid-shock-wave-casting/content/Sanctuary.Content.dll'
    accepted=json.loads((ROOT/'tools/reference/druid-shock-wave-casting-native-evidence.json').read_text());assert sha(content)==accepted['inputInventory']['artifacts']['Assemblies/Sanctuary.Content.dll']
    native=Path('/media/aomukai/FILES/Sanctuary/upstream/ModernUO/Distribution')
    references={'Sanctuary.Content':content,'Server':native/'Server.dll','UOContent':native/'Assemblies/UOContent.dll','Logger':native/'Logger.dll'}
    if check:
        accepted=json.loads((ROOT/'tools/reference/druid-fury-clock-native-evidence.json').read_text())
        for name,digest in accepted['artifacts'].items():assert sha(ROOT/name)==digest,name
        for name,path in references.items():assert sha(path)==accepted['nativeDependencies'][name] and sha(STAGE/'out'/(name+'.dll'))==sha(path),name
        output=subprocess.check_output([str(ROOT/'.tools/dotnet/dotnet'),str(STAGE/'out/FuryClock.dll'),str(vectors)],cwd=ROOT,text=True)
        assert output==(STAGE/'run.log').read_text();print(output.strip());return
    STAGE.mkdir(exist_ok=True)
    project=STAGE/'FuryClock.csproj';project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup>'+''.join('<Reference Include="'+name+'" HintPath="'+escape(str(path))+'" />' for name,path in references.items())+'</ItemGroup></Project>\n')
    program=STAGE/'Program.cs';program.write_text(r'''using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Sanctuary;

public static class Program
{
    public static int Main(string[] args)
    {
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var ceiling=typeof(AmazonFend).GetMethod("Ceiling",flags)??throw new MissingMethodException("Ceiling");
        var rollback=typeof(AmazonFend).GetMethod("Rollback",flags)??throw new MissingMethodException("Rollback");
        int Ceil(int frames,int advance)=>(int)ceiling.Invoke(null,new object[]{frames,advance});
        int Roll(int start,int advance,int elapsed)=>(int)rollback.Invoke(null,new object[]{start,advance,elapsed,70});
        using var vectors=JsonDocument.Parse(File.ReadAllText(args[0]));int count=0,contacts=0;
        foreach(var row in vectors.RootElement.GetProperty("preparedSchedules").EnumerateArray())
        {
            int advance=row.GetProperty("advance").GetInt32(),hits=row.GetProperty("count").GetInt32(),start=0,at=0,end=0;
            var expected=row.GetProperty("events");var starts=row.GetProperty("starts");
            for(int i=0;i<hits;i++)
            {
                if(start!=starts[i].GetInt32()){throw new InvalidOperationException("Native rollback frame");}
                int action=Ceil(7-start,advance);end=at+Ceil(13-start,advance)-1;at+=action;
                if(at!=expected[i].GetInt32()){throw new InvalidOperationException("Native effect clock");}
                start=Roll(start,advance,action);contacts++;
            }
            if(end!=row.GetProperty("end").GetInt32()||end<at){throw new InvalidOperationException("Native final cycle");}
            count++;
        }
        if(count!=1308||contacts!=4578){throw new InvalidOperationException("Incomplete source/native contexts");}
        Console.WriteLine("Fury prepared clock:1308schedules/4578contacts match primary source vectors via actual native Fend Ceiling/Rollback; no Fury gameplay handler or current executable certificate.");
        return 0;
    }
}
''')
    dotnet=ROOT/'.tools/dotnet/dotnet';buildlog=STAGE/'build.log'
    with buildlog.open('w') as log:subprocess.run([str(dotnet),'build',str(project),'-o',str(STAGE/'out')],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,check=True)
    text=buildlog.read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    runlog=STAGE/'run.log'
    with runlog.open('w') as log:subprocess.run([str(dotnet),str(STAGE/'out/FuryClock.dll'),str(vectors)],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,check=True)
    assert '1308schedules/4578contacts match' in runlog.read_text()
    copied=[STAGE/'out'/(name+'.dll') for name in references]
    for name,path in references.items():assert sha(STAGE/'out'/(name+'.dll'))==sha(path),name
    paths=[Path(__file__).resolve(),sourceproof,vectors,project,program,buildlog,runlog,STAGE/'out/FuryClock.dll',STAGE/'out/FuryClock.deps.json',STAGE/'out/FuryClock.runtimeconfig.json',content,*copied]
    result=dict(referenceVersion='3.3.93847',preparedComponent=True,handlerEnabled=False,installed=False,currentExecutableParity=False,
        nativeAssemblySha256=sha(content),nativeSourceCommit='8949002',nativeHelpers=['AmazonFend.Ceiling','AmazonFend.Rollback'],contexts=dict(schedules=1308,contacts=4578,distinctAdvances=327),
        nativeDependencies={name:sha(path) for name,path in references.items()},artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        established=[
            'An isolated net10console invokes the actual accepted content assembly helpers by reflection,without editing/rebuilding canonical content or integration. Every prepared2..5hit Fury schedule matches primary original-author vectors/shared current-animation arithmetic. Both action and final full-cycle timing use the real shared native Ceiling and rate-scaled Rollback70helpers.',
            'Generated driver,project,build/run logs and exact content DLL are hash-bound. Build and run exit0 with zero warnings/errors. The original world,client,canonical runtime data and running Shock Wave full regression remain untouched.',
        ],remaining=[
            'The console driver supplies action7/end13/current progress and source count; it is not a connected player or full Fury series. Native combat/input/payment/rank capture/retargeting/interruptions,outer dispatch,save/reload and full class acceptance remain next.',
            'This retains the supplementary original-author model limits. Exact current executable chronology,live progress changes and low-rank outer behavior are not certified. Final effects/audio and separate icons remain pending.',
        ])
    target=ROOT/'tools/reference/druid-fury-clock-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print(runlog.read_text().strip())


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
