#!/usr/bin/env python3
"""Authenticate missing-owner/death composition and nonblocking protected references."""
import argparse,csv,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-owner-loss';PARENT='61e9a880bf145511a56c7750f0d5c7a0eddc0cb4'
def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-owner-loss-source-evidence.json')
    assert source['sourceBytes']==706 and source['callbackBytes']==933 and source['composedBytes']==1639 and source['sourceContexts']==48 and source['composedMissingOwnerContexts']==6
    assert source['sourceCodeExecutedInOfflineEmulator']and not source['processMemoryRead']and not source['targetProcessCodeExecuted']
    for name,h in source['artifacts'].items():assert e.sha(ROOT/name)==h,name
    capture=pin(ROOT/'.local/class-skills/warlock-runtime-reference/callees-pet-missing-owner-20261109.json')
    assert capture['directCallTargetsVerified']and capture['bytesRead']==1972 and capture['records'][0]['bytes']==706 and capture['records'][0]['allUnwindFragmentsCaptured']
    assert all(f['invalidInstructionMarkers']==0 for f in capture['records'][0]['fragments'])
    assert not capture['gameMemoryWritten']and not capture['processInput']and not capture['securitySettingsChanged']and not capture['ptraceAttach']
    for name,h in capture['artifacts'].items():assert e.sha(ROOT/name)==h,name
    access=[]
    for folder,slot,rva,size in [('warlock-do56-control-path-20261109',56,'0x4571d0',1207),('warlock-flame-wave-20261109',156,'0x44cfd0',648)]:
        d=pin(ROOT/'.local/class-skills/warlock-runtime-reference'/folder/'capture.json');r=d['records'][0]
        assert d['dispatcherIndexLinked']and d['immutableTableMatches']and d['bytesRead']==3616
        assert r['slot']==slot and r['rva']==rva and not r['allUnwindFragmentsCaptured']and all(not f['captured']and f['permissions']=='---s'for f in r['fragments'])
        assert sum(int(f['endRva'],16)-int(f['rva'],16)for f in r['fragments'])==size
        assert not any(d[k]for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
        for name,h in d['artifacts'].items():assert e.sha(ROOT/name)==h,name
        access.append(dict(slot=slot,rva=rva,bytes=size,codeRead=False,mechanicsVerified=False))
    data=pin(ROOT/'tools/reference/pet-ai-tables-data-evidence.json')
    for name,h in data['artifacts'].items():assert e.sha(ROOT/name)==h,name
    folder=ROOT/'.local/class-skills/generic-pet-search-services';raw=pin(folder/'monstats.bin');pin(folder/'monstats.txt')
    rows={int(r['*hcIdx']):r for r in csv.DictReader((folder/'monstats.txt').read_text().splitlines(),delimiter='\t')if r.get('*hcIdx','').isdigit()}
    assert len(raw)==4+751*508 and len(rows)==751
    for i in range(751):assert bool(raw[4+i*508+61]&128)==bool(int(rows[i]['killable']or 0))
    for name in ['GenericPetAiEntry.cs','GenericPetAiSelection.cs','GenericPetAiPreprocess.cs','GenericPetAiPostTarget.cs','GenericPetAiOverrides.cs','GenericPetInitialization.cs','GenericPetDecisions.cs','GenericPetOwnerSurvey.cs','GenericPetOwnerIterator.cs','GenericPetActions.cs','GenericPetTimedEvents.cs','GenericPetEventDispatch.cs','DeathMarkWorld.cs']:
        path='src/Sanctuary.Content/'+name;assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT),path
    path='tests/Sanctuary.Integration/GenericPetAiEntryChecks.cs';assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
    born=pin(STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-2.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind in ['content','integration']:
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
        log=pin(STAGE/f'{kind}-build-1.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'pet-command-target-check':1,'pet-command-target-reload-check':2,'pet-owner-loss-check':3})
    log=e.cohort(STAGE,expected,15);assert log.count('Owner loss:48current contexts/706bytes plus933callback')==3
    assert log.count('AI override original:index0 actual table callback')==6 and log.count('AI override original:index1 actual table callback')==3
    assert log.count('AI entry original:typed event->full prepared entry')==9 and log.count('Timed event root:original demon callback')==9
    pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log');saved=pin(STAGE/'pet-command-target-fixtures.json')
    assert saved['Generation']==2 and len(saved['Entries'])==3 and len(saved['Owners'])==2
    for i,entry in enumerate(saved['Entries']):assert entry['Command']['Control']==dict(Value0=-2147483648+i+6,Value1=-1-i-42,Value2=2147483647-i)
    first=STAGE/'pre-roadmap-1';e.cohort(first,expected,15);pin(first/'birth-native-checks.log');pin(first/'native-run-1.log')
    original=pin(first/'birth-inputs.json');oldSnapshot=pin(first/'source-snapshot-1.json')['inputs'];newSnapshot=json.loads((STAGE/'source-snapshot-2.json').read_text())['inputs']
    assert oldSnapshot['sourceInputs']==newSnapshot['sourceInputs']and oldSnapshot['projectInputs']==newSnapshot['projectInputs']
    assert [k for k in oldSnapshot['copiedData']if oldSnapshot['copiedData'][k]!=newSnapshot['copiedData'][k]]==['Data/Sanctuary/Balance/class-skill-status.json']
    oldStatus=pin(first/'class-skill-status.json');assert e.sha(first/'class-skill-status.json')==oldSnapshot['copiedData']['Data/Sanctuary/Balance/class-skill-status.json']
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());changed=[]
    for before,after in zip(oldStatus['skills'],status['skills']):
        if before!=after:
            changed.append(after['sourceId']);assert {k for k in after if before[k]!=after[k]}<={'implementation','remaining'}
            assert before['implementation']==after['implementation']or after['sourceId']==398 and before['implementation']=='pending'and after['implementation']=='blocked'
    assert changed==[373,375,376,377,398]
    for name in ['Sanctuary.Content.dll','Sanctuary.Integration.dll']:
        assert e.sha(first/name)==born['artifacts']['Assemblies/'+name]==original['artifacts']['Assemblies/'+name]
    installed=pin(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for name,h in installed['installedArtifacts'].items():assert e.sha(Path(installed['runtimeRoot'])/name)==h,name
    protected=pin(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for kind,relative,count in [('saves','Saves',38),('configuration','Configuration',20)]:
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative
        assert len(protected[kind])==count and {str(p.relative_to(folder)):e.sha(p)for p in folder.rglob('*')if p.is_file()}==protected[kind]
    client=pin(ROOT/'tools/reference/ring-cast-client-evidence.json');clientInputs=pin(ROOT/client['inputManifest']['path'])
    assert e.sha(ROOT/client['inputManifest']['path'])==client['inputManifest']['sha256']and client['patches']==len(clientInputs['patchHashes'])==43
    for group in ['patchHashes','sharedInputs']:
        for name,h in clientInputs[group].items():assert e.sha(ROOT/name)==h,name
    for name in ['docs/pet-owner-loss-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_owner_loss.py','tools/build_skill_roadmap.py']:pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],automaticAiEnabled=False,learnedEnabled=False,
        sourceBytes=706,callbackBytes=933,composedBytes=1639,sourceContexts=48,composedMissingOwnerContexts=6,killableFieldMatches=751,newReferenceBytesRead=9204,newDeathCaptureBytesRead=1972,protectedChecks=access,oldLargeOfflineMatricesRerun=0,
        nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=2,nativeRun=2,
        artifacts=born['artifacts'],preservation=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=43,installed=False),
        verified=['Complete706byte death orchestrator matches48source/native contexts,including6composed actual933byte callback1 missing-owner transitions. Killable/dead/type gates,detach/linked notifications,killer credit,mode initialization/target/facing preservation,flag rereads and explicit source-class transforms retain source order.',
            'Owned compiled/text killable field matches751rows at61bit128. Existing coordinate decoder,mode-request representation and secondary metadata reused. Bind connects special-AI missing-owner boundary to policy without inventing native death side effects.',
            'Original3species prepared full entry/default root and override cohort retains commands/control6updates/births/fractionalwounds over two reloads. Native death execution itself is not admitted.'],
        diagnostics=['First native cohort passed. Regenerating the roadmap then changed only class-skill-status report data(four blocker descriptions plus FlameWave pending->blocked);frozen-input checker refused stale data. Snapshot2/native2 validate updated data with identical C1/I1 DLLs and unchanged C#/project inputs;first exact artifacts retained pre-roadmap-1.'],limits=['Current summon do56andFlameWave do156are dispatcher-linked but fully protected. No inaccessible body was read. Optional existing-demon recast request is nonblocking;no unchanged repeat without relevant state change.',
            'Control allocation/header+10/Field14 startup remains unknown. Older allocator is only a locator reference,not a current default. No naive nativeDelete/Kill mapping is enabled.',
            'Owner detach/linked cleanup/credit/mode execution/notifications/transforms remain explicit native leaves. Global scheduler/live owner-store and remaining status/world/path/combat admission remain unfinished. No new learned route,class certificate,assets or deployment.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-owner-loss-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Owner loss:48source/native cases(6composed),751killable fields,15markers/two reloads;2protected gaps recorded;AI gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
