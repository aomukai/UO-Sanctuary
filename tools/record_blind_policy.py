#!/usr/bin/env python3
"""Bind blind policy differential evidence; no active controller acceptance."""
import argparse
from collections import Counter
import json
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/blind-policy'
sha=inventory.sha


def record(check=False):
    inventory.STAGE=STAGE
    tested=inventory.validate_inventory(STAGE/'audit-inputs.json')
    snapshot(STAGE/'pre-build-1.json',STAGE/'audit-inputs.json')
    parent=ROOT/'tools/reference/dim-vision-cast-native-evidence.json'
    previous=json.loads(parent.read_text());ref=previous['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    baseline=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in baseline['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in tested['sourceInputs'].items() if p.endswith('.cs')}
    added=set(after)-set(before)
    assert added=={'src/Sanctuary.Content/BlindAiPolicy.cs','tests/Sanctuary.Integration/BlindPolicyChecks.cs'}
    assert set(before)<=set(after) and all(after[p]==h for p,h in before.items())
    assert tested['copiedData']==baseline['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==tested['artifacts'][name]==baseline['artifacts'][name]
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-1.log').read_text()
        assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    assert markers(STAGE/'audit-native-checks.log')==Counter({'blind-policy-check':1,'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    log=(STAGE/'audit-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==1 and 'Writing world save snapshot done' not in log
    assert 'Blind policy:12288complete tick/tactics and112no-target wrapper comparisons pass' in log
    for name,count in [('named-item-runtime-audit.json',850),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/dim-vision-cast'/name).read_text())
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert len(a['InputHashes'])==count and a['ContentAssemblySha256']==tested['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in a['InputHashes'].items():assert sha(ROOT/path)==digest,path
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    source_path=ROOT/'tools/reference/dim-vision-ai-source-evidence.json';source=json.loads(source_path.read_text())
    assert (source['contexts'],source['targetWrapperContexts'])==(12288,224)
    assert 'sub_6FCCF9D0' in source['compiledUnchangedRoutines'] and 'sub_6FCCF9D0' not in source['reviewedUnexecutedRoutines']
    for group in ['inputs','artifacts']:
        for path,digest in source[group].items():assert sha(ROOT/path)==digest,path
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    files=[parent,source_path,Path(__file__),*[ROOT/p for p in added],ROOT/'tools/probe_dim_vision_ai.py',ROOT/'tests/check_blind_policy.py',ROOT/'tests/export_runtime_audits.py']
    files += [ROOT/p for p in ['docs/blind-policy-checkpoint.md','docs/necromancer-ai-curse-source-review.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
        'tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    files += [STAGE/p for p in ['audit-inputs.json','audit-native-checks.log','run-1.log','content-build-1.log','integration-build-1.log','pre-build-1.json','python-tests.log']]
    result=dict(referenceVersion='3.3.93847',component='Blind target-mode1 decision/tactics foundation',installed=False,activeWorldBehaviorChanged=False,goalComplete=False,completedClasses=[],
        verified=dict(nativeTickTacticsComparisons=12288,nativeNoTargetComparisons=112,sourceTargetWrapperContexts=224,focusedMarkers=5,boots=1,saves=0,actualReloads=0,
            pythonTests=71,namedDefinitions=573,auditFingerprints=[850,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories=dict(audit=dict(path=str((STAGE/'audit-inputs.json').relative_to(ROOT)),sha256=sha(STAGE/'audit-inputs.json'))),
        addedSourceHashes={p:after[p] for p in sorted(added)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'New unused C#BlindAiPolicy matches12288complete source tick/tactics outputs and draw chronology;all112no-target rows of the224source wrapper contexts match without invoking percentage. Source target-present wrapper rows are not counted as extra native verification.',
            'One focused disposable boot passes5markers including shared Thorns/named/affix consumers.71Python/573named and clean builds pass;850/21fingerprints. All predecessor C#files,runtime data,staged kernel and106installed artifacts unchanged.'],
        remaining=[
            'This pure component is not connected to live skill behavior. It owns no actor RNG store,clock,target acquisition,mode/path service or saved controller;full Dim Vision AI/skill/class remains unfinished.',
            'Movement requests are not completed moves. Source distance3offsets/draws are compared;distance5geometry,actual world/mode/collision,frame cadence/removal,shared actor RNG ownership and save/reload integration remain acceptance work. Table/dispatcher/idle/mode entry review stays static-only.',
            'No full512world regression or save/reload claim for this unused-helper increment. Latest active learned-cast behavior remains the prior Dim Vision scope. No deployment,new Mobile,save field,handler or assets;effects/icons remain separately tracked.'])
    destination=ROOT/'tools/reference/blind-policy-native-evidence.json';render=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==render
    else:destination.write_text(render)
    print('Blind policy foundation:12288tick/tactics+112no-target native comparisons,5focused markers;not connected to live AI.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
