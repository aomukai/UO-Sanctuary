#!/usr/bin/env python3
"""Seal approved learned Fury input and its retained independent channels."""
import argparse
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers

ROOT=e.ROOT
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'blade-fury-held-input'
PARENT='65567581'


def record(check=False):
    prebuild=STAGE/'source-snapshot-4.json'
    wide=BASE/'blade-fury-held-input-retained'
    born=e.inventory(STAGE,wide,prebuild)
    parent_path=ROOT/'tools/reference/arctic-held-input-native-evidence.json'
    previous=e.load(parent_path)
    added=Counter({'blade-fury-held-input-check':1,'blade-fury-held-input-reload-check':2,
                   'fury-held-input-save-stage-check':3})
    added.update({f'fury-held-wire-{source}-result':3 for source in (0,1,9)})
    counts={'':210,'-retained':518,'-isolate':69,'-shared':300}
    saved_counts=[]
    for suffix,count in counts.items():
        stage=BASE/('blade-fury-held-input'+suffix)
        prior=BASE/('arctic-held-input'+suffix)/'birth-native-checks.log'
        assert e.sha(prior)==previous['files'][str(prior.relative_to(ROOT))]
        expected=markers(prior)+added
        log=e.cohort(stage,expected,count)
        assert 'pass across3boots/2reloads' in(stage/'run-2.log').read_text()
        data=e.load(stage/'birth-inputs.json');snapshot(prebuild,stage/'birth-inputs.json')
        assert data['artifacts']==born['artifacts'] and data['copiedData']==born['copiedData']
        for p,h in data['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
        sizes={}
        for prefix,field in [('inferno','Flights'),('arctic','Missiles'),('blade-fury','Flights')]:
            saved=e.load(stage/(prefix+'-held-input-fixtures.json'))
            assert saved['HeldAtFirstSave'] and saved['Generation']==saved['UnresumedSaves']==2
            assert saved['Life256']>0
            assert saved['Mana256']==(72*256 if prefix=='blade-fury' else 80*256-23*96)
            assert len(saved[field])==(8 if prefix=='blade-fury' else 20)
            assert all(f['Owner']==saved['Owner'] for f in saved[field])
            if prefix=='blade-fury':
                assert all(f['SkillRank']==1 and f['Ability']==207 and f['Frame']==0
                           and f['MechanicalProfile']=='source-player-missile-plus-current-fury-v1' for f in saved[field])
            else:assert all(f['Rank']==1 for f in saved[field])
            sizes[prefix]=len(saved[field])
        assert log.count('Fury candidate:6mana threshold/no entry debit;first blade frame12,then5frames')==1
        saved_counts.append(sizes)
    fury=BASE/'blade-fury-held-input-fury-retained'
    historical=BASE/'blade-fury-held-candidate-retained/birth-native-checks.log'
    proto_path=ROOT/'tools/reference/blade-fury-held-candidate-native-evidence.json'
    prototype=e.load(proto_path)
    assert e.sha(historical)==prototype['files'][str(historical.relative_to(ROOT))]
    e.cohort(fury,markers(historical),231)
    assert 'pass across3boots/2reloads' in(fury/'run-2.log').read_text()
    data=e.load(fury/'birth-inputs.json');snapshot(prebuild,fury/'birth-inputs.json')
    assert data['artifacts']==born['artifacts'] and data['copiedData']==born['copiedData']
    for p,h in data['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
    for name in ['content-build-2.log','integration-build-3.log']:
        assert all(s in(STAGE/name).read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    commands=e.load(STAGE/'verification-commands.json')
    assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
    assert 'Ran 71 tests' in(STAGE/'python-tests.log').read_text() and '\nOK\n' in(STAGE/'python-tests.log').read_text()
    client_path=ROOT/'tools/reference/blade-fury-held-input-client-evidence.json'
    client=e.load(client_path)
    assert client['patches']==43 and client['affectedPaths']==42 and client['buildErrors']==0 and client['buildWarnings']==44
    assert not client['graphicalInputVerified']
    for group in ['patchHashes','sharedInputs','artifacts']:
        for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
    old_client=e.load(ROOT/'tools/reference/arctic-held-input-client-evidence.json')
    assert len(old_client['patchHashes'])==42
    for p,h in old_client['patchHashes'].items():assert e.sha(ROOT/p)==h,p
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert sum(r['implementation']=='handler-present' for r in status['skills'])==205
    assert sum(r['implementation']=='handler-present' for r in status['skills'] if r['characterClass']=='Assassin')==25
    assert not status['completedClasses']
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
    failed=STAGE/'superseded-c1-i2'
    assert 'Component exposed unfinished learned input' in(failed/'fury-retained/birth-native-checks.log').read_text()
    assert 'KeyboardInterrupt' in(failed/'focused/run-1.log').read_text()
    files=[p for p in e.stage_files(STAGE,wide,BASE/'blade-fury-held-input-isolate',BASE/'blade-fury-held-input-shared',fury)
           if not p.name.startswith('receipt-')]
    files += [Path(__file__).resolve(),parent_path,proto_path,client_path,
              ROOT/'tests/check_blade_fury_held_input.py',ROOT/'tests/check_blade_fury_held_regression.py',
              ROOT/'tools/review_blade_fury_held_client.py',ROOT/'patches/ClassicUO/0043-blade-fury-held-input.patch']
    files += [ROOT/'docs'/name for name in ['blade-fury-approved-input-rule.md','blade-fury-held-input-checkpoint.md',
              'class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-effects.md','skill-icons.md']]
    files += [p for p in failed.rglob('*') if p.is_file()]
    files += [p for p in(STAGE/'failed-integration-1').iterdir() if p.is_file()]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,approved=True,approval='Approve this Blade Fury rule',
        approvalDate='2026-11-08',learnedBladeFuryHandler=True,installed=False,currentD2RInputTimingVerified=False,
        graphicalInputVerified=False,handlers=205,assassinHandlers=25,completedClasses=[],contentBuild=2,integrationBuild=3,
        sourceSnapshot=4,focusedChecks=210,retainedChecks=518,isolatedChecks=69,sharedChecks=300,furyComponentChecks=231,
        bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,rawSources=[0,1,9],threeIndependentConnectedSaveOwners=True,
        savedEmissionCounts=saved_counts,clientPatches=43,clientWarnings=44,clientErrors=0,pythonTests=71,verificationCommands=7,
        auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],
        scope=[
            'Approved fresh six-mana threshold/no entry debit,first blade frame12 then5frames,pinned per-blade costs,immediate release/fresh windup and finite resources reuse the original SQ23/resource/weapon-flight components.',
            'Shared protocol7 transport validates actual left/right/class quick bindings; duplicate press/replayed packet/stale heartbeat/unrelated release cannot double charge or restart ended input. Heartbeats renew aim/lease only.',
            'Fury emissions receive their own phase timestamps. Live rank/cost/aim affect only new packets. Movement,Stop,rebind,lease expiry and independent damage after release pass; held modes remain transient.',
            'Each four-cohort real save retains three separately connected original owners:20fire,20cold and8Fury packets. Their exact wounds/resources persist through two reloads; no held gesture replays. Inspection future epochs are explicit.',
            'The separate231-marker original Fury/sentry cohort retains complete source controls,prepared pending-release behavior,actual gear,mode/collision/payment/projectile reload and36sentry wire inputs.',
            'The43-patch client compiles and reverses to the exact pinned base then reapplies exactly. Previous42patches,installed106artifacts/38saves/20configs are preserved.'],
        limits=[
            'Input policy is an approved Sanctuary adaptation,not verified exact current D2R timing. Full modern AR/mastery/Slow Missiles damage ordering,source geometry/RNG/global events,full item/proc/owner/PvP remain bounded;unsupported learned Slow Missiles damage modification is refused.',
            'Actual rendered input,final poses/bladefragment1/2 travel/contact/expiry/audio and talent/hotkey icons remain unverified or missing; native weapon-flight presentation is an identified placeholder.',
            'Initial integration1had an extra test-fixture closing parenthesis. The original Fury regression exposed two obsolete unavailable-skill expectations after approval. I2isolated passed;three unfinished I2cohorts were explicitly stopped for the corrected final fixture package and are not counted. Their sources/binaries/logs remain archived.',
            'No deployment,save reset,complete-class certificate or overall goal completion. Continue remaining Assassin/Amazon/Warlock routes and acceptance.'])
    e.write('blade-fury-held-input-native-evidence.json',result,files,check)
    print('Learned Fury:210focused/518retained/69isolated/300shared/231original-Fury;three channels/raw0,1,9/three connected save owners;205handlers/Assassin25;no class certificate/deployment.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
