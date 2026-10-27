#!/usr/bin/env python3
"""Bind canonical Feral speed/leech, source charge, real input and reload evidence."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/druid-feral'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def record(check=False,full_class=False):
    inventory.STAGE=STAGE
    inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
    names=['stats-inputs.json','stats-native-checks.log','feral-run.log','python-tests.log','druid-feral-fixtures.json','druid-maul-fixtures.json',
        'druid-form-reaction-fixtures.json','druid-form-interruption-fixtures.json','druid-form-casting-fixtures.json','druid-form-world-fixtures.json','druid-form-expired-fixtures.json']
    artifacts=[STAGE/name for name in names]
    for kind in ['content','integration']:
        build=STAGE/(kind+'-build.log');text=build.read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        artifacts += [build,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==138
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'pass3boots/2actual reloads' in (STAGE/'feral-run.log').read_text()
    for name in ['druid-feral-check','druid-feral-live-result','druid-charge-model-check','druid-maul-check','druid-maul-live-result',
        'druid-form-reaction-check','druid-form-interruption-check','druid-form-casting-check','shape-restriction-check','druid-form-attack-check']:
        assert log.count(name+': passed')==1,name
    for name in ['druid-feral-reload-check','druid-feral-snapshot-check','druid-maul-reload-check','druid-maul-expiry-stage-check',
        'druid-form-reaction-reload-check','druid-form-interruption-reload-check','druid-form-casting-reload-check']:
        assert log.count(name+': passed')==2,name
    assert '1120native gain/cap/current-rank/expiry/channel comparisons' in log
    assert 'three difficulties,ordinary weapon hit and form cleanup pass' in log and 'zero-drain species,real skill-gear cap/downcap' in log
    for skill in ['feral','maul']:
        for kind in ['right','quick']:
            assert log.count(skill+'-'+kind+'-result: passed')==3
            assert log.count(skill+'-'+kind+': windup')==3
    saved=json.loads((STAGE/'druid-feral-fixtures.json').read_text());charge=saved['Charge']
    assert charge['Ability']==166 and charge['Count']==2 and charge['StoredRank']==1 and charge['DamagePercent']==0
    assert charge['LifeDrainMinimum']==charge['LifeDrainMaximum']==8 and charge['VelocityPercent']>0
    assert charge['AttackRate']==charge['OtherAnimationRate']==charge['StunFrames']==0
    assert saved['Form']['Form']['Ability']==162 and saved['Form']['Form']['LycanthropyRank']==20
    parent_raw,parent=frozen_receipt('c8c42b4','tools/reference/druid-maul-native-evidence.json')
    assert parent['verified']['passMarkers']==119 and parent['verified']['fullClassMarkers']==1184
    prepared_raw=subprocess.check_output(['git','show','3c79fd3:tools/reference/druid-feral-preparation.json'],cwd=ROOT)
    prepared=json.loads(prepared_raw);assert prepared['verified']['passMarkers']==21 and prepared['verified']['actualRightQuickInputs']==6
    for name,digest in prepared['artifacts'].items():assert sha(ROOT/name)==digest,name
    source=ROOT/'tools/reference/druid-charged-attacks-engine-evidence.json';assert json.loads(source.read_text())['contexts']==1120
    artifacts += [source]+[ROOT/'.local/class-skills'/('druid-charged-attacks'+suffix) for suffix in ['-source-probe.cpp','-source-probe.json','-compiler.log','-sanitizer.log']]
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        path=STAGE/name;audit=json.loads(path.read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for name,digest in audit['InputHashes'].items():assert sha(ROOT/name)==digest,name
        audits.append(audit);artifacts.append(path)
    baseline=json.loads(subprocess.check_output(['git','show','c8c42b4:tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==baseline['Definitions']
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    full_markers=full_saves=None
    if full_class:
        complete=inventory.validate_inventory(STAGE/'class-inputs.json')
        assert complete['artifacts']==inputs['artifacts'] and complete['copiedData']==inputs['copiedData']
        text=(STAGE/'class-native-checks.log').read_text()
        assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==3
        assert 'including two save/reload cycles.' in (STAGE/'full-run.log').read_text()
        full_markers=text.count(': passed');full_saves=text.count('Writing world save snapshot done')
        assert full_markers==1184 and full_saves==35
        for name,expected in [('full-named-item-runtime-audit.json',audits[0]),('full-first-loop-affix-audit.json',audits[1])]:
            path=STAGE/name;assert json.loads(path.read_text())==expected;artifacts.append(path)
        artifacts += [STAGE/name for name in ['class-inputs.json','class-native-checks.log','full-run.log']]
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[x for x in status['skills'] if x['implementation']=='handler-present']
    assert len(handlers)==177 and sum(x['characterClass']=='Druid' for x in handlers)==21 and status['completedClasses']==[]
    artifacts += [ROOT/name for name in ['tools/record_druid_feral.py','tests/check_druid_feral.py','tests/check_creeper_stats.py',
        'data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']]
    result=dict(referenceVersion='3.3.93847',installed=False,handlers=177,druidHandlers=21,completedClasses=[],goalComplete=False,currentExecutableParity=False,
        verified=dict(passMarkers=138,boots=3,actualReloads=2,chargeModelSourceComparisons=1120,actualFeralInputs=6,retainedMaulInputs=6,retainedFormInputs=15,
            savedFeralCount=2,savedLifeDrain=8,originalDurationFrames=500,savedChargeHeldForInspection=True,naturalExpiryMilliseconds=200,
            retainedMaulOfflineExpiry=True,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[len(x['InputHashes']) for x in audits],
            fullClassMarkers=full_markers,fullClassBoots=3 if full_class else None,fullClassReloads=2 if full_class else None,fullClassSaveSnapshots=full_saves),
        frozenPriorAcceptance=dict(commit='c8c42b4',receipt='tools/reference/druid-maul-native-evidence.json',sha256=hashlib.sha256(parent_raw).hexdigest(),passMarkers=119,fullClassMarkers=1184),
        frozenPreparedComponent=dict(commit='3c79fd3',receipt='tools/reference/druid-feral-preparation.json',sha256=hashlib.sha256(prepared_raw).hexdigest(),passMarkers=21,actualInputs=6),
        inputInventory=inputs,artifacts={str(path.relative_to(ROOT)):sha(path) for path in artifacts},
        established=[
            'Learned wolf-only Feral Rage uses the shared form A1/melee-only weapon scope and start56/do120contact transaction. Current3mana is paid once; hit/block/damage and drain are captured before the action event. Attack-specific calc1physical ED does not become persistent charge damage. Combat/weapon wear precede charge gain. The source model uses live rank for cap/metadata and count for aura formulas,with500frame refresh.',
            'Existing Feral charge velocity enters both player run/walk hooks once. Equal life-drain endpoints enter the existing WeaponLeech.Capture once,additively with gear/skill drains; unequal endpoints are refused. The first fresh Feral hit cannot heal from its future charge. Subsequent Feral and ordinary weapon hits heal from captured physical damage with the shared drain/difficulty reductions. Real Manald mana drain remains additive and separate.',
            'Canonical checks retain1120source-model comparisons and Maul/form/summon coverage. They verify three difficulty recipients,miss/block/immunity,zero-drain species,real Tarnhelm cap4to cap3,current metadata,ordinary Attack fallback,Stop/form cleanup,saved original form/count2/velocity/leech/fractional pools/bindings and both reloads. Normal500frame duration is checked before holding a saved charge for inspection. A separate200msnatural timer removes the actual saved state and both consumers while retaining the form; Maul offline-expiry controls remain separate.',
            'Six real Feral right/quick casts across three boots/two reloads each pay3mana,damage the selected target and gain exactly one charge without borrowing future leech. Six retained Maul and15retained form/interruption inputs continue to pass. Saved field/body presentation stays human as identified placeholders. Main installed build/configuration/client/saves remain preserved.',
        ],remaining=[
            'Exact current low-mana fallback,target-loss charge gain,reentrant reflect/death,event/RNG/geometry/cadence and form extraAR/reentry/other clocks remain unverified. Current native movement pacing is an existing adapter,not an exact D2Rmovement certificate. Item-only/foreign-class/PvP and unmapped recipients remain unsupported; do not infer full parity or a completed class.',
            'Fire Claws is next,using its pinned start58/do2/current two-hard-synergy preflight rather than do120charge gain. Remaining Druid and earlier-class gaps plus Warlock stay on the roadmap. Feral orb front/back/size,player wolf body/A1/contact/cast/expiry audio and separate talent/left/right/quick icons remain pending; generic Wrestle/sound0x238/flash0x375A are identified placeholders.',
        ])
    path=ROOT/'tools/reference/druid-feral-native-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==rendered
    else:path.write_text(rendered)
    print(f'Feral:138focused/3boots/2reloads/6real inputs,retainedMaul/forms,1120charge comparisons,70Python;fullClass={full_markers};177handlers/Druid21,not installed.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');p.add_argument('--full-class',action='store_true');args=p.parse_args();record(args.check,args.full_class)
