#!/usr/bin/env python3
"""Bind the supported Levitation context to its actual native package."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/levitation-mastery'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
    names=['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log','levitation-fixtures.json','barbarian-masteries.json',
        'warlock-foundation-fixtures.json','arctic-mode-fixture.json','arctic-stream-fixtures.json','recycler-casting-fixtures.json',
        'recycler-world-fixtures.json','recycler-body-fixtures.json','recycler-recovery-fixtures.json','body-fixtures.json',
        'world-fixtures.json','ground-fixtures.json','poison-fixtures.json','casting-fixtures.json','wolf-world-fixtures.json']
    artifacts=[STAGE/n for n in names]
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==111
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    for n in ['levitation','barbarian-masteries','warlock-foundation']:
        assert log.count(n+'-check: passed')==1 and log.count(n+'-reload-check: passed')==2,n
    assert log.count('levitation-validation-check: passed')==log.count('levitation-validation-result: passed')==3
    for n in ['arctic-mode-live','arctic-stream-live','recycler-recovery-live','recycler-world-live','recycler-casting-live']:assert log.count(n+'-result: passed')==3,n
    saved=json.loads((STAGE/'levitation-fixtures.json').read_text());assert len(saved)==2 and {x['Female'] for x in saved}=={True,False}
    proof=ROOT/'tools/reference/levitation-mastery-reference.json';source=json.loads(proof.read_text());assert source['skill']['*Id']==383;artifacts.append(proof)
    for name,digest in source['inputs'].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
    frozen_receipt('723ce4b','tools/reference/warlock-foundation-native-evidence.json')
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name,digest in a['InputHashes'].items():assert sha(ROOT/name)==digest,name
        audits.append(a);artifacts.append(p)
    old=json.loads(subprocess.check_output(['git','show','723ce4b:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions']
    assert [len(a['InputHashes']) for a in audits]==[688,21]
    py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
    artifacts += [ROOT/n for n in ['tests/check_levitation_mastery.py','tools/record_levitation_mastery.py','tools/prepare_levitation_mastery.py',
        'tools/build_class_skills.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']]
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[s for s in status['skills'] if s['implementation']=='handler-present'];assert len(handlers)==183 and status['completedClasses']==[]
    warlock=[s for s in handlers if s['characterClass']=='Warlock'];assert len(warlock)==1 and warlock[0]['talentId']=='warlock.levitationmastery' and warlock[0]['verification']=='partial-native-coverage'
    result=dict(referenceVersion='3.3.93847',component='Levitation Mastery supported normal-weapon context',installed=False,currentExecutableParity=False,goalComplete=False,
        handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=111,retainedMarkers=99,worldBoots=3,actualWorldReloads=2,
            savedWarlockWitchPlayers=2,automaticSoftRankEquipmentValidation=3,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[688,21]),
        inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},frozenPredecessor=dict(commit='723ce4b',receipt='tools/reference/warlock-foundation-native-evidence.json'),
        established=['Actual trained Warlock and Witch use effective-rank AR, additive physical damage and mastery critical through shared weapon consumers. Hard cap20, soft rank21, item-layer filtering, unarmed/broken/unequipped exclusion, respec and inactive history are checked. No active cast or hotkey binding is enabled for the passive.',
            'First native Ripsaw equip succeeds at68Strength/48Dexterity with rank2and fails one point below either boundary or without the +skill helm. Native getters use the current equip recipient; another player and NPC cannot inherit a previous owner reduction. Normal backpack tooltip explicitly labels owner requirements; Composite Bow24/34and Grand Scepter36math match reported boundaries.',
            'Automatic native equipment validation moves Ripsaw to the backpack after loss of the required soft rank on all3boots. Actual native weapon hits double physical damage only, never fourfold or elemental damage. Bow critical follows generic-zero draw then applicable mastery; explicit throw uses its separate layer. Existing Barbarian mastery combat/rank/gear/reload checks pass again.',
            'Warlock/Witch gear, hard/soft ranks and actual attack/damage endpoints survive two real world reloads. Existing prepared Druid stream/mode/recycler and formula foundations remain covered. A first test setup left Tarnhelm worn before a naked class-history switch; fixing the setup resolves that diagnostic without changing history rules.'],
        remaining=['Ethereal/item-ease combinations are unsupported and explicitly refused for trained-Warlock weapon equip; actual Steeldriver/Ghostflame refusal is checked. This is a temporary usability restriction, not completed D2R parity. Resolve modern modifier order before removing it.',
            'Innate two-hand/offhand permissions, Eldritch-specific throwing rules, full missile acceptance, modern critical rounding instrumentation and required-level overrides are separate. Rogue Bow/Rusthandle named effects remain unavailable; tests do not bypass their gates.',
            'No complete-class certificate, full-suite regression, main installation or modern executable equivalence is claimed. Main4c5d3a0working package and original saves/configuration/client remain preserved; accepted older stages remain frozen.',
            'Final levitated-weapon/class appearance/animation/audio and passive talent icon remain separately inventoried placeholders. Earlier class gaps and the remaining Warlock talents stay in the goal.'])
    p=ROOT/'tools/reference/levitation-mastery-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==text
    else:p.write_text(text)
    print('Levitation:111native checks/3boots/2reloads,70Python;183handlers/Warlock1, no completed class or installation.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
