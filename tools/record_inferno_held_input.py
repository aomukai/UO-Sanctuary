#!/usr/bin/env python3
"""Bind approved held Inferno input, exact client stack and scoped preservation."""
import argparse
from collections import Counter
from pathlib import Path
import subprocess
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'inferno-held-input';WIDE=BASE/'inferno-held-input-retained'
PARENT='746930ca5a94c8e81d179627fd7bd0bed268e475'


def preservation():
    installed=e.load(ROOT/'tools/reference/creeper-frame-installation-evidence.json')
    assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert e.sha(Path(installed['runtimeRoot'])/p)==h,p
    saved=e.load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for kind,rel,count in [('saves','Saves',38),('configuration','Configuration',20)]:
        d=Path(installed['runtimeRoot'])/'.local/server'/rel
        assert len(saved[kind])==count and {str(p.relative_to(d)):e.sha(p)for p in d.rglob('*')if p.is_file()}==saved[kind]
    old=e.load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json')
    assert len(old['patchHashes'])==40
    for p,h in old['patchHashes'].items():assert e.sha(ROOT/p)==h,p
    assert [p for p,h in old['sharedInputs'].items()if e.sha(ROOT/p)!=h]==['shared/CombatProtocol.cs']
    return dict(installedArtifacts=106,saves=38,configuration=20,historicalClientPatches=40,
                intentionalSourceChanges=['shared/CombatProtocol.cs','patches/ClassicUO/0041-inferno-held-input.patch'],installed=False)


def record(check=False):
    born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-2.json')
    old=e.load(BASE/'npc-damage-to-mana/source-snapshot-4.json')['inputs'];now=e.load(STAGE/'source-snapshot-2.json')['inputs']
    assert now['sourceInputs'].keys()-old['sourceInputs'].keys()=={'src/Sanctuary.Content/PlayerInfernoInput.cs','tests/Sanctuary.Integration/InfernoHeldInputChecks.cs'}
    assert old['sourceInputs'].keys()<=now['sourceInputs'].keys() and old['projectInputs']==now['projectInputs']
    assert old['copiedData'].keys()==now['copiedData'].keys()
    assert {p for p,h in old['copiedData'].items()if now['copiedData'][p]!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    focused=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'inferno-held-input-check':1,'inferno-held-input-reload-check':2})
    focused.update({f'inferno-held-wire-{source}-result':3 for source in (0,1,9)})
    e.cohort(STAGE,focused,21)
    prior=BASE/'inferno-held-candidate-retained/birth-native-checks.log';receipt=e.load(ROOT/'tools/reference/inferno-held-candidate-native-evidence.json')
    assert e.sha(prior)==receipt['files'][str(prior.relative_to(ROOT))]
    retained=markers(prior);assert sum(retained.values())==185
    del retained['inferno-held-candidate-check'];del retained['inferno-held-candidate-reload-check']
    retained.update({k:v for k,v in focused.items()if k.startswith('inferno-held-')})
    e.cohort(WIDE,retained,194)
    for stage,run in [(STAGE,3),(WIDE,1)]:
        assert 'pass across3boots/2reloads' in(stage/f'native-run-{run}.log').read_text()
        saved=e.load(stage/'inferno-held-input-fixtures.json')
        assert saved['HeldAtFirstSave'] and saved['Generation']==saved['UnresumedSaves']==2 and len(saved['Flights'])==20
    for kind,run in [('content',2),('integration',1)]:
        text=(STAGE/f'{kind}-build-{run}.log').read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)>=5 and all(x['exitCode']==0 for x in commands)
    assert 'Ran 71 tests' in(STAGE/'python-tests.log').read_text() and '\nOK\n' in(STAGE/'python-tests.log').read_text()
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(x['implementation']=='handler-present'for x in status['skills'])==198 and status['completedClasses']==[]
    client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert client['patches']==41 and client['buildErrors']==0 and not client['graphicalInputVerified']
    for group in ['sharedInputs','patchHashes','artifacts']:
        for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
    prototype=e.load(ROOT/'tools/reference/inferno-held-candidate-native-evidence.json');assert not prototype['approved'] and prototype['candidateOnly']
    files=[p for p in e.stage_files(STAGE,WIDE)if p.name not in ('receipt-write.log','receipt-check.log')]
    for sub in ['failed-native-1','prior-focused-2','failed-client-build-1']:
        files += [p for p in(STAGE/sub).rglob('*')if p.is_file()and p.suffix in('.cs','.dll','.json','.log','.py','.patch')]
    for kind in ['content','integration']:files.append(STAGE/kind/f'Sanctuary.{kind.title()}.dll')
    files += [ROOT/p for p in ['tools/reference/inferno-held-input-client-evidence.json','tools/reference/inferno-held-candidate-native-evidence.json','patches/ClassicUO/0041-inferno-held-input.patch','tests/check_inferno_held_input.py','tests/check_iron_golem_birth.py','tools/review_inferno_held_client.py']]
    files += [ROOT/'docs'/p for p in ['inferno-approved-input-rule.md','inferno-held-input-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md','npc-kill-recovery-progress.md']]
    files.append(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',parent=PARENT,approved=True,approval='Approve this Inferno rule',approvalDate='2026-11-06',gameplayHandler=True,installed=False,
        handlers=198,completedClasses=[],sourceSnapshot=2,contentBuild=2,integrationBuild=1,focusedRun=3,retainedRun=1,auditRun=2,focusedMarkers=21,retainedMarkers=194,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,
        rankOne=dict(entryThresholdMana=6,invocationMana=.375,firstFlameFrames=12,renewalFrames=2,steadyManaPerSecond=4.6875,twoSecondDebits=22,twoSecondFlames=20),
        rawSources=[0,1,9],clientPatches=41,pythonTests=71,auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],
        scope=['Explicit learned left/right/quick press,aim and release reuse the existing source resource/sequence and saved stream host. Transport heartbeat renews only aim/lease and cannot restart a released,expired or resource-ended channel.',
               'Deterministic fractional threshold/payment,chunk-independent source cadence,duplicate frames/presses,fresh windup,finite mana,live gear and independent released damage are checked;actual socket input and natural flame damage run for all three input sources on each boot.',
               'Original20flights and exact wounds/mana survive2reloads;held intent does not replay. The accepted first save deliberately uses a fresh connected future-epoch press;natural socket cadence is verified separately.',
               'The182retained baseline markers remain. The historical3prototype markers need a separate connected save owner;their policy cases now live in the approved fixture instead of falsely holding two owners through one socket.'],
        limits=['This is an approved Sanctuary adaptation,not established exact D2R3.3.93847 input chronology. Arctic Blast/Blade Fury timing is not approved or enabled by this change.',
                'Graphical client input/rendering,complete class/world geometry/PvP/owner/event ordering and unsupported Slow Missiles damage modifiers remain unverified. Invalid/unavailable bindings and known unsupported input modifiers are refused.',
                'Final channel/stream/contact/expiry/audio and talent/left/right/quick icons remain identified placeholders. The installed working build/saves are unchanged.',
                'The archived native1failure is a competing save-owner fixture assumption;the client1failure used the wrong SDL key-event field. A Python run before the new audit export correctly rejected stale fingerprints;fresh audit and checks supersede it.'])
    e.write('inferno-held-input-native-evidence.json',result,files,check)
    print('Approved Inferno:21focused/194retained markers,two reloads each;41client patches;71Python;198handlers/no class complete;installed world preserved.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
