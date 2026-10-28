#!/usr/bin/env python3
"""Bind launch/update preparation and the actual footprint regression fix."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/miasma-launch'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
    names=['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log','fissure-fixtures.json','miasma-packet-fixtures.json','levitation-fixtures.json','barbarian-masteries.json',
        'warlock-foundation-fixtures.json','arctic-mode-fixture.json','arctic-stream-fixtures.json','recycler-casting-fixtures.json',
        'recycler-world-fixtures.json','recycler-body-fixtures.json','recycler-recovery-fixtures.json','body-fixtures.json',
        'world-fixtures.json','ground-fixtures.json','poison-fixtures.json','casting-fixtures.json','wolf-world-fixtures.json']
    artifacts=[STAGE/n for n in names]
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==126
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    for n in ['miasma-launch','fissure','miasma-packets','levitation','barbarian-masteries','warlock-foundation']:
        assert log.count(n+'-check: passed')==1 and log.count(n+'-reload-check: passed')==2,n
    assert log.count('Miasma launch:3600complete wrapper/factory/owner and5120complete update comparisons pass;15active footprints retained,2reference rows remain unsupported')==3
    assert log.count('Fissure:441complete-source footprint cell comparisons pass')==1
    for n in ['fissure-live','arctic-mode-live','arctic-stream-live','recycler-recovery-live','recycler-world-live','recycler-casting-live']:assert log.count(n+'-result: passed')==3,n
    proof=ROOT/'tools/reference/miasma-launch-source-evidence.json';source=json.loads(proof.read_text());assert source['cases']==dict(factory=3600,basicUpdate=5120) and source['handlerEnabled']==False;artifacts.append(proof)
    for group in ['inputs','artifacts']:
        for name,digest in source[group].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
    _,previous=frozen_receipt('4e9195b','tools/reference/miasma-packets-native-evidence.json')
    diagnostic=STAGE/'footprint-diagnostic';assert 'Unmapped missile footprint producer' in (diagnostic/'stats-native-checks.log').read_text()
    assert sha(diagnostic/'Sanctuary.Content.dll')==previous['inputInventory']['artifacts']['Assemblies/Sanctuary.Content.dll']
    artifacts += list(diagnostic.iterdir())
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name,digest in a['InputHashes'].items():assert sha(ROOT/name)==digest,name
        audits.append(a);artifacts.append(p)
    old=json.loads(subprocess.check_output(['git','show','4e9195b:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions']
    assert [len(a['InputHashes']) for a in audits]==[692,21]
    py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
    artifacts += [ROOT/n for n in ['tests/check_miasma_launch.py','tools/record_miasma_launch.py','tools/probe_miasma_launch.py',
        'tools/build_class_skills.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']]
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[s for s in status['skills'] if s['implementation']=='handler-present'];assert len(handlers)==183 and status['completedClasses']==[]
    assert len([s for s in handlers if s['characterClass']=='Warlock'])==1
    result=dict(referenceVersion='3.3.93847',component='Prepared Miasma launch/update plans and footprint regression fix',installed=False,handlerEnabled=False,currentExecutableParity=False,goalComplete=False,
        handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=126,retainedMarkers=114,worldBoots=3,actualWorldReloads=2,
            fullSourceLaunchComparisonsPerBoot=3600,fullSourceUpdateComparisonsPerBoot=5120,existingFootprintSourceCells=441,retainedLiveFissureRuns=3,
            activeFootprintProducers=15,referenceOnlyRows=2,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[692,21]),
        inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='4e9195b',receipt='tools/reference/miasma-packets-native-evidence.json'),
        established=['A native diagnostic reproduces the previous packet projection regression: adding two Collision1 Miasma reference rows caused the closed footprint-source audit to reject all footprint queries. The fix explicitly classifies the two unsupported reference rows while preserving the fifteen existing producers and strict rejection of unknown rows. Reference rows cannot supply Covers results, register transients or enter active snapshots.',
            'Existing Fissure controls pass again, including 441 complete-source footprint cell comparisons, shared native field projections, ordinary casting/landing/damage/recovery/lifecycle and three connected natural damage/expiry runs. Its saved actors resume across both actual reloads. Existing Warlock, Barbarian and prepared Druid checks remain green.',
            'MiasmaLaunch prepares zero-offset ordinary direct-missile requests using explicit coordinates, current selected coordinates or cursor coordinates. Native plans match 3600 complete legacy wrapper/factory cases with current row values: flags33, velocity20 with integer slow-before75percent conversion, lifetime/activation50, self-aim adjustment conditional on velocity and the conditional100-unit aim guard. Allocation failures are checked. The complete source owner setter runs in the harness; no native projectile ownership object is created by the plan.',
            'BasicMissileUpdate exposes movement, frame writes, room/path queries, saved steps, masks, candidate selection and final hit callbacks as explicit services. It matches 5120 complete generic-update cases; additional native controls verify post-move flags, frame-before-hit ordering, ordered two-step selection, targetless wall removal and missing-room handling. No generic damage handler is substituted for Miasma hit41.'],
        remaining=['This turn prepares launch/update plans, not saved moving world bolts. Actual factory allocation, native geometry/Q12/source-cell adapters, projectile persistence and integration with Miasma hit41/cloud-do41 remain to implement. The footprint fix does not enable Miasma clouds as field producers.',
            'Learned Miasma cast/input/payment, cloud geometry and creation, first/last damage frames, hit-window identity, owner/facet/logout and modern mastery/pierce timing remain individually gated. Optional maximum-range-expiry observation remains pending; no answer is inferred.',
            'No complete-class certificate, full regression or installation is claimed. Main4c5d3a0working package and original saves/configuration/client remain preserved; old accepted stages and the red diagnostic remain frozen.',
            'No new final visual is generated by request/update plans. Miasma cast/bolt/impact/cloud/expiry/audio and talent/left/right/quick icons remain in their separate registers; earlier class and Levitation gaps stay in the goal.'])
    p=ROOT/'tools/reference/miasma-launch-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==text
    else:p.write_text(text)
    print('Miasma launch/footprint fix:126 checks/3 boots/2 reloads,70 Python;183 handlers unchanged, learned Miasma gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
