#!/usr/bin/env python3
"""Compare our frozen/current compiled routing with its generated source flags."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-movement';PROBE=STAGE/'routing-probe'
SOURCE='''using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
var rows=new List<object>();
foreach(var file in args)
{
    var context=new AssemblyLoadContext(Guid.NewGuid().ToString(),true);
    context.Resolving+=(_,name)=>{
        foreach(var root in new[]{"/media/aomukai/FILES/Sanctuary/upstream/ModernUO/Distribution","/media/aomukai/FILES/Sanctuary/upstream/ModernUO/Distribution/Assemblies"})
        {var candidate=Path.Join(root,name.Name+".dll");if(File.Exists(candidate)){return context.LoadFromAssemblyPath(candidate);}}
        return null;
    };
    var assembly=context.LoadFromAssemblyPath(Path.GetFullPath(file));
    var type=assembly.GetType("Sanctuary.CombatAbility",true);var method=assembly.GetType("Sanctuary.ClassAbilities",true).GetMethod("CorpseTarget");
    var ids=new List<int>();foreach(var value in Enum.GetValues(type)){if((bool)method.Invoke(null,new[]{value})){ids.Add(Convert.ToInt32(value));}}
    rows.Add(new{Path=Path.GetFullPath(file),Sha256=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file))),CorpseTargetIds=ids});
    context.Unload();
}
Console.WriteLine(JsonSerializer.Serialize(rows));
'''
PROJECT='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>disable</Nullable></PropertyGroup></Project>\n'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_ids(text):
 ids={n:int(v) for n,v in re.findall(r'^    (\w+) = (\d+),$',text,re.M)}
 body=re.search(r'bool CorpseTarget\(CombatAbility value\) => value is ([^;]+);',text).group(1)
 return sorted(ids[n] for n in re.findall(r'CombatAbility\.(\w+)',body))
def build(check=False):
 PROBE.mkdir(exist_ok=True);cs=PROBE/'Program.cs';project=PROBE/'RoutingProbe.csproj';binary=PROBE/'bin/Debug/net10.0/RoutingProbe.dll';log=PROBE/'build.log'
 if check:assert cs.read_text()==SOURCE and project.read_text()==PROJECT
 else:
  cs.write_text(SOURCE);project.write_text(PROJECT)
  with log.open('w') as out:subprocess.run([str(ROOT/'.tools/dotnet/dotnet'),'build',str(project)],cwd=ROOT,stdout=out,stderr=out,check=True)
 paths=[ROOT/'.local/class-skills/find-potion/content/Sanctuary.Content.dll',STAGE/'diagnostics/generated-corpse-routing/content/Sanctuary.Content.dll',STAGE/'content/Sanctuary.Content.dll']
 rows=json.loads(subprocess.check_output([str(ROOT/'.tools/dotnet/dotnet'),str(binary),*map(str,paths)],cwd=ROOT,text=True))
 oldSource=subprocess.check_output(['git','show','bd5bad9:shared/ClassAbilities.cs'],cwd=ROOT,text=True);old=source_ids(oldSource);current=source_ids((ROOT/'shared/ClassAbilities.cs').read_text())
 skills=json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text());expected=sorted(s['ability'] for s in skills if s['sourceFields'].get('TargetCorpse'))
 assert len(old)==6 and 123 not in old and len(expected)==9
 assert rows[0]['CorpseTargetIds']==sorted(old+[123]) and rows[1]['CorpseTargetIds']==old and rows[2]['CorpseTargetIds']==current==expected
 artifacts=paths+[cs,project,binary,log,Path(__file__).resolve(),ROOT/'shared/ClassAbilities.cs',ROOT/'tools/build_class_skills.py',ROOT/'tests/test_class_skills.py',STAGE/'pre-build-inputs.json',ROOT/'tools/snapshot_skill_build.py']
 result=dict(component='Generated routing/build-input consistency correction',referenceVersion='3.3.93847',processMemoryRead=False,targetProcessCodeExecuted=False,ownProjectAssemblyCodeExecutedOffline=True,
  frozenCommit='bd5bad9',frozenSourceCorpseTargetIds=old,acceptedFrozenDllCorpseTargetIds=rows[0]['CorpseTargetIds'],freshPreFixDllCorpseTargetIds=rows[1]['CorpseTargetIds'],currentDllCorpseTargetIds=rows[2]['CorpseTargetIds'],pinnedSourceCorpseTargetIds=expected,
  artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},established=['Frozen Find Potion DLL behavior was accepted,but its manual corpse-routing edit had been removed from generated source by the unchanged template. Reflection independently confirms7compiled flags versus6frozen source flags;the subsequent fresh build exposed the missing quick-cast route.',
   'The generator now derives all9corpse-capability flags from owned skill rows. Current generated source and cleanly rebuilt DLL agree. Grim Ward/Engorge metadata does not enable their unsupported handlers. The new Python regression compares runtime routing metadata with pinned data independently of the template.',
   'A pre-build source/data snapshot is checked before/after clean content/integration rebuilds and against native copied inputs. This receipt supersedes any implication that the older post-build inventory alone certified which source bytes produced the old DLL. Its observed staged behavior and earlier native markers remain historical evidence.'],remaining=['Complete current native three-boot/two-reload regression and aggregate receipt required. This probe executes only our own ClassAbilities method in isolated load contexts;it does not load or execute the D2R reference executable or access the game process.'])
 out=ROOT/'tools/reference/skill-build-routing-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Routing consistency:old accepted DLL7/frozen source6;fresh pre-fix6;current clean DLL/source/pinned metadata9.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
