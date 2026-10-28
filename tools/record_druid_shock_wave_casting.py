#!/usr/bin/env python3
"""Bind learned Shock Wave, retained Druid checks and optional full regression."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/druid-shock-wave-casting'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False,full_class=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'inputs.json');retained=inventory.validate_inventory(STAGE/'stats-inputs.json')
    assert inputs['artifacts']==retained['artifacts'] and inputs['copiedData']==retained['copiedData']
    names=['inputs.json','native-checks.log','casting-run.log','shock-wave-fixtures.json','shock-wave-casting-fixtures.json',
        'expiry-save-0.json','expiry-save-1.json','stats-inputs.json','stats-native-checks.log','retained-run.log','python-tests.log',
        'druid-rabies-fixtures.json','druid-hunger-fixtures.json','druid-fire-claws-fixtures.json','druid-feral-fixtures.json','druid-maul-fixtures.json']
    paths=[STAGE/name for name in names]
    for kind in ['content','integration']:
        build=STAGE/(kind+'-build.log');assert all(x in build.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        paths += [build,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==39
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    for name,count in [('shock-wave-casting-check',1),('shock-wave-casting-reload-check',2),('shock-wave-casting-snapshot-check',3),
        ('shock-wave-components-check',1),('shock-wave-components-reload-check',2),('shock-wave-live-result',3),('shock-wave-expiry-stage-check',2)]:assert log.count(name+': passed')==count,name
    for kind in ['left','right','quick']:assert log.count('shock-wave-'+kind+'-result: passed')==log.count('shock-wave-'+kind+': windup')==3
    assert '259complete-source bear SC/FCR schedules pass' in log and '3825physical/17920stun source comparisons pass' in log
    assert '39markers/3boots/2actual reloads/9real left-right-quick casts' in (STAGE/'casting-run.log').read_text()
    saved=json.loads((STAGE/'shock-wave-casting-fixtures.json').read_text());waves=[x for x in saved['Waves']['Missiles'] if x['Owner']==saved['Owner']]
    assert saved['Form']['Form']['Ability']==164 and saved['Mana']%256==239 and saved['Life']%256 in [111,239] and saved['PendingMana']%256==239
    assert len(waves)==5 and all(x['Rank']==1 and x['Remaining']==14 and x['Min256']==2816 and x['Max256']==5632 for x in waves)
    component=json.loads((STAGE/'shock-wave-fixtures.json').read_text());assert component['ExpiredChecks']==2 and component['ExpiryOwner']==0
    assert len(component['State']['Leases'])==1 and any(x['Target']==component['Target'] for x in component['Stuns'])
    assert len([x for x in component['State']['Missiles'] if x['Owner']==component['Owner'] and x['Remaining']==13])==5
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
    previous=json.loads(subprocess.check_output(['git','show','6bac43c:tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==previous['Definitions']
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    frozen_receipt('6bac43c','tools/reference/druid-shock-wave-prepared-native-evidence.json')
    for name in ['druid-rabies-native-evidence.json','druid-rabies-away-native-evidence.json']:frozen_receipt('2a38d48','tools/reference/'+name)
    paths += [ROOT/name for name in ['tools/record_druid_shock_wave_casting.py','tests/check_shock_wave_casting.py','tests/check_shock_wave_retained.py','tests/check_shock_wave_full.py',
        'tools/reference/druid-shock-wave-modern-scope.json','tools/reference/druid-form-cast-clock-engine-evidence.json',
        'tools/build_skill_roadmap.py','tools/build_class_skills.py','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']]
    full_markers=full_saves=None
    if full_class:
        full=inventory.validate_inventory(STAGE/'class-inputs.json');assert full['artifacts']==inputs['artifacts'] and full['copiedData']==inputs['copiedData']
        text=(STAGE/'class-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==3
        assert 'including two save/reload cycles.' in (STAGE/'full-run.log').read_text()
        full_markers,full_saves=text.count(': passed'),text.count('Writing world save snapshot done');assert full_markers==1184 and full_saves==35
        for name,expected in [('full-named-item-runtime-audit.json',audits[0]),('full-first-loop-affix-audit.json',audits[1])]:
            path=STAGE/name;assert json.loads(path.read_text())==expected;paths.append(path)
        paths += [STAGE/name for name in ['class-inputs.json','class-native-checks.log','full-run.log']]
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[x for x in status['skills'] if x['implementation']=='handler-present']
    assert len(handlers)==181 and sum(x['characterClass']=='Druid' for x in handlers)==25 and status['completedClasses']==[]
    result=dict(referenceVersion='3.3.93847',learnedHandlerEnabled=True,installed=False,handlers=181,druidHandlers=25,completedClasses=[],goalComplete=False,currentExecutableParity=False,
        verified=dict(passMarkers=39,boots=3,actualReloads=2,actualShockWaveInputs=9,connectedNaturalWaveSessions=3,expiredSaveControls=2,
            physicalComparisons=3825,stunComparisons=17920,bearCastClockComparisons=259,retainedDruidMarkers=193,retainedBoots=3,retainedReloads=2,
            retainedMeleeInputs=30,retainedFormInputs=15,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[len(x['InputHashes']) for x in audits],
            fullClassMarkers=full_markers,fullClassBoots=3 if full_class else None,fullClassReloads=2 if full_class else None,fullClassSaveSnapshots=full_saves),
        inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        frozenPredecessors=[dict(commit='6bac43c',receipt='tools/reference/druid-shock-wave-prepared-native-evidence.json',scope='Saved component/source event composition'),
            dict(commit='2a38d48',receipt='tools/reference/druid-rabies-installation-evidence.json',scope='Preserved installed Rabies; not the new package')],
        established=[
            'Learned bear-only SC/FCR creates five physical/stun waves at release,using the shared ten-frame/speed152/action5 cast clock. Nine real left/right/quick inputs pass with one7mana release debit. No early waves/payment,duplicate release or unpaid saved-windup replay occurs. Actual Magefist improves SC timing; hard Maul synergy and Tarnhelm effective rank match source capture without soft synergy or charge ED/consumption.',
            'Successful do8 still pays when every individual missile factory fails. Stop,accepted movement,weapon change,unshift and skill-history cleanup discard pending cast records without removing already-launched waves. Insufficient/lost mana and unsupported modern Slow Missiles damage scaling refuse unpaid creation. Native release recheck/serialized payment and deleted-target cached-ground handling remain identified adapters.',
            'Saved launched wave endpoints/rank/RNG/history/windows,form,bindings and fractional life/mana survive two actual reloads. Parked inspection clocks are distinct from two actual short-lived waves serialized before shutdown and expired naturally during restart without damage replay. Three connected automatic flight sessions verify health-before-stun,damage,stun and expiry. Physical immunity/shield/source eligibility comparisons retain the prepared evidence.',
            'The identical candidate separately passes193retained Druid markers across3boots/2reloads,30actual Rabies/Hunger/Fire Claws/Feral/Maul inputs and15form inputs. Active bear SC retains the shared released2.4 hit-recovery protection. Both builds have zero warnings/errors;70Python tests and573named-item decisions pass with fresh audits. Existing installed saves/client/main are preserved until gated rollout.',
        ],remaining=[
            'Modern per-execution4frame delay grouping follows recorded released2.7/primary-testing inference,not an exact3.3executable certificate. Native terrain/Q12/room/candidate order,source RNG/event/Fog and complete graphical/PvP/foreign/item-only acceptance remain unverified.',
            'DAMAGE_VS_MONTYPE producer/consumer remains separately unsupported. Unestablished Slow Missiles damage scaling is explicitly rejected. Synchronous lethal/reentrant post-health stun chronology and unsupported owner death/history/offline/facet contexts retain explicit faults. No new owner-transition policy approval is inferred from Rabies.',
            'Original-world installation and complete-class acceptance remain separate. Final bear cast/wave/contact/stun/expiry effects/audio and talent/left/right/quick icons remain labelled placeholders in separate registers. No class/goal completion is claimed.',
        ])
    destination=ROOT/'tools/reference/druid-shock-wave-casting-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==encoded
    else:destination.write_text(encoded)
    print('Learned Shock Wave:39focused/193retained'+('/1184full' if full_class else '')+';3boots/2reloads;9real inputs;70Python;181handlers/Druid25; no class complete.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');p.add_argument('--full-class',action='store_true');a=p.parse_args();record(a.check,a.full_class)
