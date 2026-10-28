#!/usr/bin/env python3
"""Bind Fury combat/input/connected-save acceptance and optional complete regression."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/druid-fury'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False,full_class=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'inputs.json');retained=inventory.validate_inventory(STAGE/'stats-inputs.json')
    assert inputs['artifacts']==retained['artifacts'] and inputs['copiedData']==retained['copiedData']
    paths=[STAGE/name for name in ['inputs.json','native-checks.log','run.log','druid-fury-fixtures.json','shock-wave-fixtures.json','shock-wave-casting-fixtures.json',
        'expiry-save-0.json','expiry-save-1.json','stats-inputs.json','stats-native-checks.log','retained-run.log','python-tests.log',
        'druid-rabies-fixtures.json','druid-hunger-fixtures.json','druid-fire-claws-fixtures.json','druid-feral-fixtures.json','druid-maul-fixtures.json']]
    for kind in ['content','integration']:
        path=STAGE/(kind+'-build.log');assert all(x in path.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        paths += [path,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    text=(STAGE/'native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==72
    assert text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    for name,count in [('druid-fury-check',1),('druid-fury-reload-check',2),('druid-fury-stage-check',3),('druid-fury-stage-result',3),('druid-fury-snapshot-check',6),
        ('shock-wave-components-check',1),('shock-wave-components-reload-check',2),('shock-wave-casting-check',1),('shock-wave-casting-reload-check',2),('shock-wave-live-result',3)]:assert text.count(name+': passed')==count,name
    for skill in ['fury','shock-wave']:
        for kind in ['left','right','quick']:assert text.count(skill+'-'+kind+'-result: passed')==text.count(skill+'-'+kind+': windup')==3
    assert '1996source clock schedules/255rank-count-AR-mana comparisons pass' in text
    assert 'living/death/range retargeting,captured count/soft rank,empty acquisition,cancellation,reentry and real gear controls pass' in text
    assert 'three connected pending-series world saves' in (STAGE/'run.log').read_text()
    saved=json.loads((STAGE/'druid-fury-fixtures.json').read_text());assert saved['PaidRemaining']==4 and saved['Form']['Form']['Ability']==162
    assert saved['Mana']%256==239 and saved['Life']%256 in [111,239] and saved['TargetLife']<50000*256
    for boot in [0,1]:assert json.loads((STAGE/f'expiry-save-{boot}.json').read_text())['files']
    old=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in old and ' ERR]' not in old and old.count(': passed')==193
    assert old.count('Shutting down')==old.count('Writing world save snapshot done')==3
    assert 'pass3boots/2actual reloads' in (STAGE/'retained-run.log').read_text()
    for skill in ['rabies','hunger','fire-claws','feral','maul']:
        for kind in ['right','quick']:assert old.count(skill+'-'+kind+'-result: passed')==3
    for name in ['druid-form-interruption-check','druid-form-casting-check','shape-restriction-check','druid-form-attack-check']:assert old.count(name+': passed')==1
    for name in ['druid-form-interruption-reload-check','druid-form-casting-reload-check','shape-restriction-reload-check']:assert old.count(name+': passed')==2
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        path=STAGE/name;audit=json.loads(path.read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name,digest in audit['InputHashes'].items():assert sha(ROOT/name)==digest,name
        audits.append(audit);paths.append(path)
    previous=json.loads(subprocess.check_output(['git','show','f0d14e6:tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==previous['Definitions']
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    for label in ['engine','full-clock-engine','schedule-native']:
        path=ROOT/f'tools/reference/druid-fury-{label}-evidence.json';proof=json.loads(path.read_text());assert proof['referenceVersion']=='3.3.93847'
        paths.append(path)
        for name,digest in proof['artifacts'].items():assert sha(ROOT/name)==digest,name;paths.append(ROOT/name)
    assert json.loads((ROOT/'tools/reference/druid-fury-schedule-native-evidence.json').read_text())['dependencies']['Sanctuary.Content']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
    frozen_receipt('f0d14e6','tools/reference/druid-shock-wave-casting-native-evidence.json')
    paths += [ROOT/name for name in ['tools/record_druid_fury.py','tests/check_druid_fury.py','tests/check_druid_fury_retained.py','tests/check_druid_fury_full.py',
        'tools/reference/druid-fury-preflight.json','tools/reference/druid-fury-contact-review.json','tools/build_class_skills.py','tools/build_skill_roadmap.py',
        'data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']]
    full_markers=full_saves=None
    if full_class:
        full=inventory.validate_inventory(STAGE/'class-inputs.json');assert full['artifacts']==inputs['artifacts'] and full['copiedData']==inputs['copiedData']
        complete=(STAGE/'class-native-checks.log').read_text();assert ': failed' not in complete and ' ERR]' not in complete and complete.count('Shutting down')==3
        assert 'including two save/reload cycles.' in (STAGE/'full-run.log').read_text()
        full_markers,full_saves=complete.count(': passed'),complete.count('Writing world save snapshot done');assert full_markers==1184 and full_saves==35
        assert complete.count('named-gear-check: passed')==1 and complete.count('named-gear-reload-check: passed')==2
        for name,expected in [('full-named-item-runtime-audit.json',audits[0]),('full-first-loop-affix-audit.json',audits[1])]:
            path=STAGE/name;assert json.loads(path.read_text())==expected;paths.append(path)
        paths += [STAGE/name for name in ['class-inputs.json','class-native-checks.log','full-run.log']]
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[x for x in status['skills'] if x['implementation']=='handler-present']
    assert len(handlers)==182 and sum(x['characterClass']=='Druid' for x in handlers)==26 and status['completedClasses']==[]
    result=dict(referenceVersion='3.3.93847',learnedHandlerEnabled=True,installed=False,handlers=182,druidHandlers=26,completedClasses=[],goalComplete=False,currentExecutableParity=False,
        verified=dict(passMarkers=72,boots=3,actualReloads=2,actualFuryInputs=9,retainedShockWaveInputs=9,connectedPendingSeriesSaves=3,pendingSeriesBeforeAfterSaveChecks=6,
            nativeFurySchedules=1996,nativeFuryContacts=6986,rankCountArManaComparisons=255,retainedDruidMarkers=193,retainedBoots=3,retainedReloads=2,retainedMeleeInputs=30,retainedFormInputs=15,
            pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[len(x['InputHashes']) for x in audits],
            fullClassMarkers=full_markers,fullClassBoots=3 if full_class else None,fullClassReloads=2 if full_class else None,fullClassSaveSnapshots=full_saves),
        inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        frozenPredecessor=dict(commit='f0d14e6',receipt='tools/reference/druid-shock-wave-casting-native-evidence.json',scope='Installed predecessor full acceptance; not this new candidate'),
        established=[
            'Learned wolf-only start37/do13 pays pinned4mana once before any hit and captures2..5attempts. Actual native physical/typed/critical,existing Feral leech,ordinary NPC shield/miss/physical immunity,live effective rank with retained attempt count,insufficient-mana ordinary Attack and already-paid zero-mana continuation pass. Empty-ground acquisition,free no-target refusal,three/two cycling between living monsters,death/range replacement and sole-target continuation are checked.',
            'The new actual Fury scheduler matches1996primary-model schedules across every legal22..520advance with6986contact deadlines and final whole-cycle tails. Normal wolf hit recovery cancels this source-derived interruptible skill; Stop,movement,weapon change,unshift/human/bear restrictions and reentrant stop/replacement prevent late attacks. One actual attempt per late update preserves the declared shared native policy without an overdue burst.',
            'Nine real left/right/quick Fury casts deliver two contacts and one4mana payment across3boots/2reloads. Nine retained Shock Wave inputs and three automatic wave sessions pass. Before each actual world save,a real native connection keeps a paid unfinished Fury series alive with four attempts pending; six before/after-serialization assertions prevent the offline sweeper from weakening the fixture. Two real reloads retain fractional resources,form,bindings and target injury while discarding the old series without replay/refund.',
            'The identical package separately passes193retained Druid markers/3boots/2reloads,30real Rabies/Hunger/Fire Claws/Feral/Maul inputs and15form inputs. Both builds have zero warnings/errors,all70Python tests pass,and fresh675/21fingerprints preserve573named-item decisions. Older63-marker offline-fixture runs are archived but do not supply the connected pending-save acceptance.',
        ],remaining=[
            'Original-author normalized progress/rollback remains a supplementary model,not a current3.3executable trace. Live effective-rank/progression refresh,extra transformed AR applicability,modern first/contact/tail/seed/geometry/event/reentry/payment and complete graphical acceptance retain explicit limits. Native weapon/item/event pipeline scope is inherited and not silently promoted to exact current-engine parity.',
            'Interruptibility follows the current Fury row and older complete mode gate; current executable timing is not independently captured. Full owner death/history/facet/logout,foreign/item-only/PvP/hireling and unmapped-defense cases remain outside this focused gameplay certificate. Shared event callbacks and current respec/save transports retain their separate source/native limits.',
            'Original-world installation and class completion remain separate. Final wolf A1multi-swing/contact/miss/block/interruption/recovery/audio and talent/left/right/quick icons remain labelled placeholders in separate registers. No class or overall-goal completion is claimed.',
        ])
    target=ROOT/'tools/reference/druid-fury-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Fury:72focused/193retained'+('/1184full' if full_class else '')+';3boots/2reloads;9real Fury inputs;3connected pending-series saves;70Python;182handlers/Druid26; no class complete.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');p.add_argument('--full-class',action='store_true');a=p.parse_args();record(a.check,a.full_class)
