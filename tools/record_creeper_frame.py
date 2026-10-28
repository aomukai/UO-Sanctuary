#!/usr/bin/env python3
"""Bind corrected Creeper frame composition, migration and retained combat acceptance."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/creeper-frame-correction'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False,full_class=False):
    inventory.STAGE=STAGE
    inputs=inventory.validate_inventory(STAGE/'inputs.json')
    retained=inventory.validate_inventory(STAGE/'stats-inputs.json')
    assert inputs['artifacts']==retained['artifacts'] and inputs['copiedData']==retained['copiedData']
    paths=[STAGE/name for name in ['inputs.json','stats-inputs.json','native-checks.log','stats-native-checks.log','run.log','retained-run.log','python-tests.log',
        'world-fixtures.json','ground-fixtures.json','poison-fixtures.json','casting-fixtures.json','body-fixtures.json','wolf-world-fixtures.json',
        'druid-fury-fixtures.json','shock-wave-fixtures.json','shock-wave-casting-fixtures.json','expiry-save-0.json','expiry-save-1.json',
        'druid-rabies-fixtures.json','druid-hunger-fixtures.json','druid-fire-claws-fixtures.json','druid-feral-fixtures.json','druid-maul-fixtures.json']]
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        paths += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==193
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    for name in ['creeper-stats','creeper-fields','creeper-body','creeper-world','creeper-ground','creeper-poison','creeper-casting']:
        assert log.count(name+'-check: passed')==1,name
    for name in ['creeper-body','creeper-world','creeper-ground','creeper-poison','creeper-casting']:
        assert log.count(name+'-reload-check: passed')==2,name
    for name in ['creeper-world-live','creeper-fields-live','creeper-poison-live']:
        assert log.count(name+'-result: passed')==3,name
    assert '7400complete getter/callback vectors and7375actual mat steps pass' in log
    assert 'elapsed0/9/18emission' in log and 'legacy/missing-profile JSON migration pass' in log
    for skill in ['rabies','hunger','fire-claws','feral','maul']:
        for kind in ['right','quick']:assert log.count(skill+'-'+kind+'-result: passed')==3
    profile='charged-motion-elapsed-age-contact-v2'
    for name in ['world-fixtures.json','ground-fixtures.json']:
        saved=json.loads((STAGE/name).read_text());g=saved['Ground'];fields=g['Fields']
        assert fields['Profile']==profile and g['Fault'] is None
        assert len(fields['Heads'])==3 and len(fields['Mats'])==6 and fields['Contacts']==3
        assert sum(m['LastCollision']!=0 for m in fields['Mats'])==3
        assert all(m['Total']==125 and m['Poison'] is not None for m in fields['Mats'])
        assert g['PoisonContext']['Profile']=='birth-owner-rabies-no-player-gear-provisional-v1'
    fury=(STAGE/'native-checks.log').read_text()
    assert ': failed' not in fury and ' ERR]' not in fury and fury.count(': passed')==72
    assert fury.count('Shutting down')==fury.count('Writing world save snapshot done')==3
    for name,count in [('druid-fury-stage-result',3),('druid-fury-snapshot-check',6),('druid-fury-reload-check',2)]:assert fury.count(name+': passed')==count
    for skill in ['fury','shock-wave']:
        for kind in ['left','right','quick']:assert fury.count(skill+'-'+kind+'-result: passed')==3
    assert 'three connected pending-series world saves' in (STAGE/'run.log').read_text()
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;audit=json.loads(p.read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name,digest in audit['InputHashes'].items():assert sha(ROOT/name)==digest,name
        paths.append(p);audits.append(audit)
    previous=json.loads(subprocess.check_output(['git','show','27a7a54:tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==previous['Definitions']
    py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
    for name in ['creeper-frame','creeper-vines-age']:
        p=ROOT/f'tools/reference/{name}-source-evidence.json';proof=json.loads(p.read_text());assert proof['referenceVersion']=='3.3.93847'
        paths.append(p)
        for name,digest in proof['artifacts'].items():assert sha(ROOT/name)==digest,name;paths.append(ROOT/name)
    frozen_receipt('27a7a54','tools/reference/druid-fury-native-evidence.json')
    paths += [ROOT/name for name in ['tools/record_creeper_frame.py','tools/probe_creeper_vines_age_reference.py','tests/check_creeper_frame.py',
        'tests/check_creeper_frame_fury.py','tests/check_creeper_frame_full.py','tests/check_creeper_stats.py',
        'data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']]
    full_markers=full_saves=None
    if full_class:
        full=inventory.validate_inventory(STAGE/'class-inputs.json');assert full['artifacts']==inputs['artifacts'] and full['copiedData']==inputs['copiedData']
        text=(STAGE/'class-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==3
        full_markers=text.count(': passed');full_saves=text.count('Writing world save snapshot done');assert full_markers==1184 and full_saves==35
        assert text.count('named-gear-check: passed')==1 and text.count('named-gear-reload-check: passed')==2
        assert 'including two save/reload cycles.' in (STAGE/'full-run.log').read_text()
        for name,expected in [('full-named-item-runtime-audit.json',audits[0]),('full-first-loop-affix-audit.json',audits[1])]:
            p=STAGE/name;assert json.loads(p.read_text())==expected;paths.append(p)
        paths += [STAGE/name for name in ['class-inputs.json','class-native-checks.log','full-run.log']]
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    present=[x for x in status['skills'] if x['implementation']=='handler-present']
    assert len(present)==182 and sum(x['characterClass']=='Druid' for x in present)==26 and status['completedClasses']==[]
    result=dict(referenceVersion='3.3.93847',installed=False,learnedHandlerEnabled=False,handlers=182,druidHandlers=26,completedClasses=[],goalComplete=False,currentExecutableParity=False,
        component='Corrected prepared Poison Creeper elapsed-frame composition and legacy field migration',profile=profile,
        verified=dict(passMarkers=72,retainedFuryShockMarkers=72,retainedDruidMarkers=193,boots=3,actualReloads=2,
            sourceFrameVectors=7400,actualMatSteps=7375,correctedRouterContexts=864,changedOldRouterContexts=102,
            actualFuryInputs=9,retainedShockWaveInputs=9,connectedPendingSeriesSaves=3,pendingSeriesBeforeAfterSaveChecks=6,
            retainedMeleeInputs=30,retainedFormInputs=15,creeperConnectedFollowFieldPoisonRuns=9,
            pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[len(a['InputHashes']) for a in audits],
            fullClassMarkers=full_markers,fullClassBoots=3 if full_class else None,fullClassReloads=2 if full_class else None,fullClassSaveSnapshots=full_saves),
        inputInventory=inputs,retainedInputInventory=retained,artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        frozenPredecessor=dict(commit='27a7a54',receipt='tools/reference/druid-fury-native-evidence.json'),
        established=[
            'Prepared head do26now receives elapsed20-countdown and emits at0/9/18before movement/decrement. Mat hit50now receives total-countdown after the generic decrement,allows early contact and excludes its final15frames. Actual C#predicates match7400complete getter/callback vectors;7375actual native mat steps cover all frames of four rank lifetimes. Corrected complete router proof retains hostility/LastCollide/ending behavior while changing102old ambiguous-stub controls.',
            'Current field profilev2accepts old explicitv1and missing-profile JSON without resetting positions,countdowns,hit histories,poison/RNG,counters or identities. Copy/restore upgrades only the profile; no child or damage is backfilled. Native raw-JSON independent registry migration,invalid atomic restore,worker serialization and two actual current-world reloads pass. Old pre-correction executable world saves are not separately bootstrapped by this suite.',
            'The193marker corrected/retained Druid suite passes3boots/2reloads,three connected follow/field/poison runs each,actual poison/old-facet/orphan/expiry behavior and30retained melee/15form inputs. Saved independent and embedded-origin groups retain corrected profile,three contacts,collider history and captured poison across both reloads. The identical package separately retains72Fury/Shock Wave markers,nine actual casts each and three connected pending-series saves.',
        ],remaining=[
            'Poison Creeper learned casting remains disabled for unresolved VINE_BEAST underground exposure and modern pet/owner damage transport. Birth-Rabies/no-player-gear poison capture is still explicitly provisional. This correction resolves two source/native frame-composition defects; it does not certify current executable timing,room/geometry/global list/event ordering or unrelated unsupported branches.',
            'Original-world installation and class completion remain separate. Final K9body/underground/surface,head/mat/contact/wither/audio and talent/right/quick icons remain identified placeholders in separate registers. Arctic Blast channeling and Solar formula-slot conflict remain individual gaps; earlier classes and Warlock remain in scope.'])
    path=ROOT/'tools/reference/creeper-frame-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Creeper frame correction:193corrected/retained Druid +72Fury/Shock'+('/1184full' if full_class else '')+';3boots/2reloads;7400source vectors/7375native mat steps;legacy profiles preserved;182handlers/Druid26,no class complete.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');p.add_argument('--full-class',action='store_true');a=p.parse_args();record(a.check,a.full_class)
