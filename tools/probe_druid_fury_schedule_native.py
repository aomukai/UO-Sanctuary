#!/usr/bin/env python3
"""Execute the new Fury schedule against every legal pinned author-model vector."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from xml.sax.saxutils import escape

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'druid-fury-schedule-native'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    proofpath=ROOT/'tools/reference/druid-fury-full-clock-engine-evidence.json';proof=json.loads(proofpath.read_text())
    for name,digest in proof['artifacts'].items():assert sha(ROOT/name)==digest,name
    vectors=BASE/'druid-fury-full-clock-source-probe.json';content=BASE/'druid-fury/content/Sanctuary.Content.dll'
    native=Path('/media/aomukai/FILES/Sanctuary/upstream/ModernUO/Distribution');references={'Sanctuary.Content':content,'Server':native/'Server.dll','UOContent':native/'Assemblies/UOContent.dll','Logger':native/'Logger.dll'}
    dotnet=ROOT/'.tools/dotnet/dotnet';target=ROOT/'tools/reference/druid-fury-schedule-native-evidence.json'
    if check:
        old=json.loads(target.read_text())
        for name,digest in old['artifacts'].items():assert sha(ROOT/name)==digest,name
        for name,path in references.items():assert sha(path)==sha(STAGE/'out'/(name+'.dll'))==old['dependencies'][name],name
        output=subprocess.check_output([str(dotnet),str(STAGE/'out/FurySchedule.dll'),str(vectors)],cwd=ROOT,text=True);assert output==(STAGE/'run.log').read_text();print(output.strip());return
    STAGE.mkdir(exist_ok=True);project=STAGE/'FurySchedule.csproj';project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup>'+''.join('<Reference Include="'+name+'" HintPath="'+escape(str(path))+'" />' for name,path in references.items())+'</ItemGroup></Project>\n')
    program=STAGE/'Program.cs';program.write_text(r'''using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Sanctuary;
public static class Program
{
    public static int Main(string[] args)
    {
        var type=typeof(AmazonFend).Assembly.GetType("Sanctuary.DruidFury",true);
        var method=type.GetMethod("Schedule",BindingFlags.Static|BindingFlags.NonPublic)??throw new MissingMethodException("Schedule");
        using var data=JsonDocument.Parse(File.ReadAllText(args[0]));int compared=0,contacts=0,guards=0;
        foreach(var row in data.RootElement.GetProperty("preparedSchedules").EnumerateArray())
        {
            var actual=((int[] Events,int End))method.Invoke(null,new object[]{row.GetProperty("advance").GetInt32(),row.GetProperty("count").GetInt32()});
            var events=row.GetProperty("events");if(actual.Events.Length!=events.GetArrayLength()||actual.End!=row.GetProperty("end").GetInt32()){throw new InvalidOperationException("Fury final cycle/count");}
            for(int i=0;i<actual.Events.Length;i++){if(actual.Events[i]!=events[i].GetInt32()){throw new InvalidOperationException("Fury event deadline");}contacts++;}compared++;
        }
        foreach(var input in new[]{(0,2),(21,2),(521,2),(256,1),(256,6)})
        {
            try{method.Invoke(null,new object[]{input.Item1,input.Item2});throw new InvalidOperationException("Invalid Fury clock accepted");}
            catch(TargetInvocationException e)when(e.InnerException is NotSupportedException or ArgumentOutOfRangeException){guards++;}
        }
        if(compared!=1996||contacts!=6986||guards!=5){throw new InvalidOperationException("Incomplete Fury schedule probe");}
        Console.WriteLine("Actual DruidFury.Schedule:1996schedules/6986contacts/5invalid guards pass across all499legal advances; combat/input/reload acceptance remains pending.");return 0;
    }
}
''')
    buildlog=STAGE/'build.log';runlog=STAGE/'run.log'
    with buildlog.open('w') as log:subprocess.run([str(dotnet),'build',str(project),'-o',str(STAGE/'out')],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,check=True)
    assert all(x in buildlog.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    with runlog.open('w') as log:subprocess.run([str(dotnet),str(STAGE/'out/FurySchedule.dll'),str(vectors)],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,check=True)
    assert '1996schedules/6986contacts/5invalid guards pass' in runlog.read_text()
    copied=[STAGE/'out'/(name+'.dll') for name in references]
    for name,path in references.items():assert sha(path)==sha(STAGE/'out'/(name+'.dll')),name
    paths=[Path(__file__).resolve(),proofpath,vectors,project,program,buildlog,runlog,content,STAGE/'out/FurySchedule.dll',STAGE/'out/FurySchedule.deps.json',STAGE/'out/FurySchedule.runtimeconfig.json',*copied]
    paths += [ROOT/'src/Sanctuary.Content'/name for name in ['DruidFury.cs','AmazonFend.cs','DruidFormRates.cs']]
    result=dict(referenceVersion='3.3.93847',learnedHandlerPresent=True,gameplayAccepted=False,installed=False,currentExecutableParity=False,
        nativeMethod='DruidFury.Schedule',contexts=dict(schedules=1996,contacts=6986,invalidGuards=5,advances=499,minimumAdvance=22,maximumAdvance=520),
        dependencies={name:sha(path) for name,path in references.items()},artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        established=['An isolated net10console invokes the actual new internal DruidFury.Schedule from the candidate assembly and compares every legal22..520advance/two-to-five-contact series with the pinned original-author preparation. Final cycle and all6986contact deadlines match; five invalid advance/count controls refuse. Driver/dependencies/source/build/run are hash-bound with zero-warning build and exit0.'],
        remaining=['This covers the actual pure Fury clock,not a connected player or weapon contact. Source-derived interruptibility,4mana,AR/block/typed damage/charges/drain/procs,retargeting,reentry,real L/R/quick input,cancellation and save/reload remain pending. Modern author-model and exact current dispatch limits remain explicit. Main retains the separately accepted installed Shock Wave package; final effects/icons remain separate.'])
    target.write_text(json.dumps(result,indent=2)+'\n');print(runlog.read_text().strip())


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
