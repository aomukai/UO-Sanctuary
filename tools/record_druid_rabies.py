#!/usr/bin/env python3
"""Bind learned Rabies and retained form/weapon/poison acceptance to the exact package."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/druid-rabies'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False,full_class=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
    names=['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log','druid-rabies-fixtures.json','druid-hunger-fixtures.json',
        'druid-fire-claws-fixtures.json','druid-feral-fixtures.json','druid-maul-fixtures.json','druid-form-reaction-fixtures.json',
        'druid-form-interruption-fixtures.json','druid-form-casting-fixtures.json','druid-form-world-fixtures.json','druid-form-expired-fixtures.json']
    artifacts=[STAGE/name for name in names]
    for kind in ['content','integration']:
        path=STAGE/(kind+'-build.log');assert all(v in path.read_text() for v in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        artifacts += [path,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==193
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'pass3boots/2actual reloads' in (STAGE/'run.log').read_text()
    for name in ['druid-rabies-packet-check','druid-rabies-check','druid-form-basic-block-check','druid-hunger-model-check','druid-hunger-check',
        'druid-fire-claws-scalars-check','druid-fire-claws-check','druid-feral-check','druid-feral-live-result','druid-maul-check','druid-maul-live-result',
        'druid-charge-model-check','druid-form-reaction-check','druid-form-interruption-check','druid-form-casting-check','shape-restriction-check','druid-form-attack-check']:
        assert log.count(name+': passed')==1,name
    for name in ['druid-rabies-reload-check','druid-rabies-snapshot-check','druid-hunger-reload-check','druid-fire-claws-reload-check','druid-feral-reload-check','druid-maul-reload-check']:
        assert log.count(name+': passed')==2,name
    assert 'two hit stages,two packets,10mana,start miss/block,ordinary miss/block,poison immunity/cure,no-refresh' in log
    for skill in ['rabies','hunger','fire-claws','feral','maul']:
        for kind in ['right','quick']:assert log.count(skill+'-'+kind+'-result: passed')==log.count(skill+'-'+kind+': windup')==3
    saved=json.loads((STAGE/'druid-rabies-fixtures.json').read_text())
    assert saved['Form']['Form']['Ability']==162 and saved['Form']['Form']['LycanthropyRank']==20
    assert saved['Charge']['Count']==1 and saved['Charge']['LifeDrainMinimum']==4
    assert saved['Poison']['PoisonSource']==saved['Owner'] and saved['Poison']['PoisonMs']==4000 and saved['Poison']['PoisonClock']['Rate256']==48
    assert len(saved['Infections']['Markers'])==len(saved['Infections']['Infections'])==1 and saved['Infections']['Markers'][0]['Host']==saved['Target']
    assert saved['Infections']['Markers'][0]['UntilUtc']==saved['Infections']['Infections'][0]['UntilUtc']
    for commit,path in [('d72d844','tools/reference/druid-rabies-lifecycle-native-evidence.json'),('1fb5518','tools/reference/druid-hunger-native-evidence.json')]:frozen_receipt(commit,path)
    for name in ['bite','factory','spread','router','lifecycle']:
        path=ROOT/f'tools/reference/druid-rabies-{name}-engine-evidence.json';source=json.loads(path.read_text());fixture=ROOT/source['fixture']['path']
        assert sha(fixture)==source['fixture']['sha256'];artifacts += [path,fixture]
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        path=STAGE/name;audit=json.loads(path.read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for name,digest in audit['InputHashes'].items():assert sha(ROOT/name)==digest,name
        audits.append(audit);artifacts.append(path)
    previous=json.loads(subprocess.check_output(['git','show','d72d844:tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==previous['Definitions']
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    full_markers=full_saves=None
    if full_class:
        complete=inventory.validate_inventory(STAGE/'class-inputs.json');assert complete['artifacts']==inputs['artifacts'] and complete['copiedData']==inputs['copiedData']
        text=(STAGE/'class-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==3
        assert 'including two save/reload cycles.' in (STAGE/'full-run.log').read_text()
        full_markers,full_saves=text.count(': passed'),text.count('Writing world save snapshot done');assert full_markers==1184 and full_saves==35
        for name,expected in [('full-named-item-runtime-audit.json',audits[0]),('full-first-loop-affix-audit.json',audits[1])]:
            path=STAGE/name;assert json.loads(path.read_text())==expected;artifacts.append(path)
        artifacts += [STAGE/name for name in ['class-inputs.json','class-native-checks.log','full-run.log']]
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[x for x in status['skills'] if x['implementation']=='handler-present']
    assert len(handlers)==180 and sum(x['characterClass']=='Druid' for x in handlers)==24 and status['completedClasses']==[]
    artifacts += [ROOT/name for name in ['tools/record_druid_rabies.py','tests/check_druid_rabies.py','tests/check_creeper_stats.py','tools/build_class_skills.py','tools/build_skill_roadmap.py',
        'data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json','tools/reference/druid-rabies-away-deviation.json','tools/reference/druid-rabies-away-native-evidence.json']]
    result=dict(referenceVersion='3.3.93847',installed=False,handlers=180,druidHandlers=24,completedClasses=[],goalComplete=False,currentExecutableParity=False,
        verified=dict(passMarkers=193,boots=3,actualReloads=2,actualRabiesInputs=6,retainedHungerFireFeralMaulInputs=24,retainedFormInputs=15,
            retainedPacketComparisons=12240,retainedInfectionWindows=1296,nativeS3Schedules=28800,pythonTests=70,unchangedNamedDefinitions=573,
            inputFingerprints=[len(x['InputHashes']) for x in audits],fullClassMarkers=full_markers,fullClassBoots=3 if full_class else None,fullClassReloads=2 if full_class else None,fullClassSaveSnapshots=full_saves),
        inputInventory=inputs,artifacts={str(path.relative_to(ROOT)):sha(path) for path in artifacts},
        frozenPredecessors=[dict(commit='d72d844',receipt='tools/reference/druid-rabies-lifecycle-native-evidence.json',scope='Separate marker/emitter and migration acceptance'),
            dict(commit='1fb5518',receipt='tools/reference/druid-hunger-native-evidence.json',scope='Installed Hunger full regression; not this new package')],
        established=[
            'Learned wolf-only Rabies uses the existing normalized S3contact/length clock. Complete older start57gate determines initial accuracy/block before pinned10mana payment. Effect121then rolls and drains a distinct ordinary transformed weapon hit before posting/checking independent infection and rolling/committing a forced-success source-zero poison packet. Actual rank1/rank3,ordinary miss/block,poison immunity/cure and no-refresh repeat bite pass.',
            'Weapon critical,item/typed damage,wear and physical leech remain in the first packet; existing Feral charges are not gained,consumed or refreshed. Skill poison re-evaluates after ordinary weapon events. Native tests inspect marker-before-poison-roll and ordinary HP commitment,retain full new poison duration without extending old marker,and verify one start payment with no second debit even if mana later falls to0.',
            'Stop,movement,weapon change,unshift/restriction and saved restart prevent pending-bite replay. Human/bear casts refuse. Approved insufficient-mana behavior uses ordinary Attack. Shared S3range selection now uses transformed melee reach for both Rabies and Hunger; an actual equipped bow exercises the melee bite and both range adapters. Six actual Rabies right/quick casts pass over three boots/two reloads,with24retained Hunger/Fire/Feral/Maul inputs and15form inputs.',
            'Saved learned bindings,form,Feral charge,fractional owner/target life,mana,poison and independent marker/emitter identities remain unchanged through both reloads. The fixture alone shifts inspection timestamps by the standard retention interval; this does not extend live skill duration. Named573decisions and all70Python tests remain unchanged/passing.',
        ],remaining=[
            'Modern initial miss/block payment and failed-start presentation are not instrumented; the complete older free-failed-start gate is the explicit source-derived baseline while modern table10mana overrides the older rank1minimum-mana quirk. Extra transformed AR that uses Rabies rank in the old ordinary helper is not silently added to existing native form AR; its modern applicability remains unverified.',
            'Lost-contact remote marker attachment is explicitly refused and reported in session.RabiesFault,retaining already-paid mana. Lethal or reentrant first-packet context changes end the supported native path with an explicit fault. Postdeath marker/corpse chronology and modern release-rank/event sequencing remain unverified,not invented. Item-only,foreign/PvP/unmapped recipients remain unsupported.',
            'Approved original-caster logout/facet suppression keeps timers running and resumes only future activity; separate5-marker/3-boot/2-reload/2-real-disconnect policy acceptance uses this identical gameplay/data. Legacy Fault diagnostics still label those contexts unverified. Original-caster death/history and host facet transitions,exact global event ties,actual source RNG streams,Fog geometry,modern Slow Missiles damage and complete client/body acceptance remain open. Final effects/audio and separate talent/left/right/quick icons remain placeholders. No class/goal completion or installation is claimed.',
        ])
    path=ROOT/'tools/reference/druid-rabies-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print(f'Rabies:193focused/3boots/2reloads/6actual inputs/70Python;fullClass={full_markers};180handlers/Druid24,not installed.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');parser.add_argument('--full-class',action='store_true');args=parser.parse_args();record(args.check,args.full_class)
