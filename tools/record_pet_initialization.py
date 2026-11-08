#!/usr/bin/env python3
"""Authenticate initializer/root/original-actor composition and next references."""
import argparse,json,re
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-initialization'

def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text()) if path.suffix=='.json' else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-initialization-source-evidence.json')
    assert source['referenceVersion']=='3.3.93847' and source['sourceContexts']==29
    assert source['composedBytes']==sum(source['components'].values())==2522
    assert source['sourceCodeExecutedInOfflineEmulator'] and not source['processMemoryRead'] and not source['targetProcessCodeExecuted']
    for name,digest in source['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    born=pin(STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-1.json')
    snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for name,digest in born['sourceInputs'].items():assert e.sha(ROOT/name)==digest,name
    for kind in ['content','integration']:
        path=STAGE/kind/f'Sanctuary.{kind.title()}.dll'
        assert e.sha(path)==born['artifacts']['Assemblies/'+path.name]
        log=(STAGE/(kind+'-build-1.log')).read_text();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        pin(STAGE/(kind+'-build-1.log'))
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
                      'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
                      'pet-command-target-check':1,'pet-command-target-reload-check':2,
                      'pet-initialization-check':1,'pet-initialization-reload-check':2})
    log=e.cohort(STAGE,expected,15);assert log.count('Initialization:29composed current-code contexts')==1
    pin(STAGE/'birth-native-checks.log')
    saved=pin(STAGE/'pet-command-target-fixtures.json')
    assert saved['Generation']==2 and len(saved['Owners'])==2 and len(saved['Entries'])==3
    assert [entry['Command']['Type4c'] for entry in saved['Entries']]==[2,1,2]
    for index,entry in enumerate(saved['Entries']):
        assert entry['Command']['Handle']==entry['Body']
        assert entry['Command']['Target48']==(saved['Owners'][1] if index==1 else saved['Target'])
    # New discovery is pinned separately from the accepted prepared property
    # services. It must not silently turn into a native numeric certificate.
    captures=[];assembly=[]
    live=ROOT/'.local/class-skills/warlock-runtime-reference'
    for name,rva,size in [('callees-pet-initializer-property-20261109.json','0x27acb0',124),
                          ('callees-pet-initializer-maxlife-20261109.json','0x24fa20',92)]:
        capture=pin(live/name);row=capture['records'][0]
        assert capture['referenceVersion']=='3.3.93847' and capture['directCallTargetsVerified']
        assert row['rva']==rva and row['bytes']==size and row['allUnwindFragmentsCaptured']
        assert capture['bytesRead']<=8192 and capture['bytesRead']==2*sum(r['size']for r in capture['reads'])
        assert all(r['stable'] and 'r' in r['permissions'] and (r['kind']!='code' or 'x' in r['permissions'])for r in capture['reads'])
        assert not any(capture[k]for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
        text=''
        for n,h in capture['artifacts'].items():
            assert e.sha(ROOT/n)==h,n
            if n.endswith('.asm') and any(f.get('sha256')==capture['artifacts'].get(n[:-4]+'.bin')for f in row['fragments']):text+=(ROOT/n).read_text()
        assert '(bad)' not in text;assembly.append(text);captures.append(capture)
    assert captures[0]['process']==captures[1]['process']
    assert sum(c['bytesRead']for c in captures)==1552
    assert re.search(r'mov\s+edx,0x6\b',assembly[0]) and re.search(r'mov\s+edx,0x7\b',assembly[1])
    assert re.search(r'sar\s+ecx,0x8',assembly[0]) and re.search(r'sar\s+ebx,0x8',assembly[0])
    assert re.search(r'imul\s+eax,ebx,0x64',assembly[0]) and re.search(r'idiv\s+ecx',assembly[0])
    costs=pin(ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json')
    assert next(r['Id']for r in costs if r['Name']=='hitpoints')==6
    assert next(r['Id']for r in costs if r['Name']=='maxhp')==7
    installed=pin(ROOT/'tools/reference/creeper-frame-installation-evidence.json')
    assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():assert e.sha(Path(installed['runtimeRoot'])/name)==digest,name
    protected=pin(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for kind,relative,count in [('saves','Saves',38),('configuration','Configuration',20)]:
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative
        assert len(protected[kind])==count
        assert {str(p.relative_to(folder)):e.sha(p)for p in folder.rglob('*')if p.is_file()}==protected[kind]
    client=pin(ROOT/'tools/reference/ring-cast-client-evidence.json');clientInputs=pin(ROOT/client['inputManifest']['path'])
    assert e.sha(ROOT/client['inputManifest']['path'])==client['inputManifest']['sha256']
    assert client['patches']==len(clientInputs['patchHashes'])==43
    for group in ['patchHashes','sharedInputs']:
        for name,digest in clientInputs[group].items():assert e.sha(ROOT/name)==digest,name
    for name in ['docs/pet-initialization-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
                 'docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_initialization.py']:
        pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],learnedEnabled=False,automaticAiEnabled=False,
                composedCurrentBytes=2522,composedContexts=29,retainedGetterContexts=35,largeHistoricalMatricesRerun=0,
                nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,
                artifacts=born['artifacts'],preservation=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=43,installed=False),
                verified=['Current decoder/getter/owner survey/finishing compose in source order:cache command,copy control,clear raw command,property lookups,player-only owner iteration;new scratch starts empty.',
                          'The existing root uses the produced decision frame;event removal precedes combat,command finishing precedes publishing the three control words. Finishing reuses the qualified setter and clears before null/invalid selections.',
                          'Actual original Goatman/Tainted/Defiler command objects initialize/consume/restore before and after two reloads without moving,healing or recapturing bodies. Original birth/wound/mana/typed-command/Warlock-Witch identity checks retained.'],
                nextPropertyReference=dict(sourceBytes=216,bytesRead=1552,currentRva='0x27acb0',maximumRva='0x24fa20',statIds=[6,7],meaning='Whole-life percentage:shift each FP8stat by8 independently,then signed100*currentWhole/maximumWhole;zero whole maximum returns0.',nativeSupplierEnabled=False,sourceNumericBridgeVerified=False,process=captures[0]['process']),
                limits=['AI-control words,properties,owner membership/order,live survey row/validity/path services remain explicit caller obligations;native producer/state/event scheduling and full automatic AI not enabled.',
                        'Source vector copy is qualified only for the initializer-reachable empty temporary with initialized inline destination;managed scratch does not certify raw allocator behavior and is not persisted.',
                        'New life-percentage helper/max-life code is fully captured and its stat IDs checked;actual source/native numeric and life-stat producer comparison is next. Do not use a tick count for cfg64/68.',
                        'Existing raw+8survey lookup uncertainty remains. Learned spawn/cap/payment/input and full special-recipient scopes remain gated. Effects/audio and talent/hotkey icons stay separate.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-initialization-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Initialization:29composed controls/15native markers,two original-demon reloads;next life-percent reference216bytes captured;213handlers/AI gated.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
