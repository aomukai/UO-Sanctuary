#!/usr/bin/env python3
"""Bind shared Grizzly sequence/rate and cached-packet callback acceptance."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'grizzly-sequence'
FROZEN='5aa659b'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def historical(name):
    return subprocess.check_output(['git','show',FROZEN+':'+name],cwd=ROOT)


def record(check=False):
    inventory.STAGE=STAGE
    inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
    artifacts=[STAGE/name for name in ['stats-inputs.json','stats-native-checks.log','sequence-run.log',
        'python-tests.log','grizzly-body-fixtures.json','damage-rounding-checks.json']]
    for kind in ['content','integration']:
        path=STAGE/(kind+'-build.log');build=path.read_text()
        assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
        artifacts += [path,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==241
    assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    for name in ['grizzly-sequence','bear-smite','beast-mode-stream','grizzly-ai','grizzly-stats']:
        assert log.count(name+'-check: passed')==1,name
    assert '5742shared clock steps,576complete rate/planner contexts and576source/native mode14cached-packet contexts/23616phases pass' in log
    for name in ['damage-rounding','grizzly-body','dire-wolf-body','dire-wolf-world','dire-wolf-casting',
                 'spirit-wolf-body','spirit-wolf-world','spirit-wolf-casting','barbs-casting','wolverine-casting','oak-casting']:
        assert log.count(name+'-check: passed')==1 and log.count(name+'-reload-check: passed')==2,name
    for name in ['dire-wolf','spirit-wolf','barbs','oak','wolverine']:
        for kind in ['right','quick']:
            assert log.count(name+'-'+kind+'-result: passed')==3,name
    python=(STAGE/'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    for tag,count in [('grizzly-sequence',5742),('grizzly-rate',576),('grizzly-smite-sequence',23616)]:
        path=ROOT/('tools/reference/'+tag+'-engine-evidence.json');source=json.loads(path.read_text())
        assert source['referenceVersion']=='3.3.93847' and source['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
        fixture=ROOT/source['fixture']['path'];generated=BASE/(tag+'-source-probe.cpp');sanitizer=BASE/(tag+'-sanitizer.log')
        assert len(json.loads(fixture.read_text()))==count
        assert sha(fixture)==source['fixture']['sha256'] and sha(generated)==source['fixture']['generatedSourceSha256']
        assert sanitizer.read_text()=='' and (BASE/(tag+'-compiler.log')).read_text()==''
        for relative,digest in source['sourceHashes'].items():
            assert sha(BASE/'d2moo-pinned-source'/relative)==digest,relative
        artifacts += [path,fixture,generated,sanitizer,BASE/(tag+'-compiler.log')]
    frozen_path='tools/reference/bear-smite-native-evidence.json';frozen=json.loads(historical(frozen_path))
    assert frozen['verified']['fullClassMarkers']==1184 and frozen['verified']['fullClassBoots']==3 and frozen['verified']['fullClassReloads']==2
    assert frozen['verified']['fullClassSaveSnapshots']==35 and frozen['verified']['passMarkers']==240
    for name,digest in frozen['artifacts'].items():
        raw=(ROOT/name).read_bytes() if name.startswith('.local/') else historical(name)
        assert hashlib.sha256(raw).hexdigest()==digest,name
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        path=STAGE/name;audit=json.loads(path.read_text())
        assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for relative,digest in audit['InputHashes'].items():
            assert sha(ROOT/relative)==digest,relative
        audits.append(audit);artifacts += [path,ROOT/'tools/reference'/name]
    baseline=json.loads(historical('tools/reference/named-item-runtime-audit.json'))
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==baseline['Definitions']
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers=[x for x in status['skills'] if x['implementation']=='handler-present']
    assert len(handlers)==171 and sum(x['characterClass']=='Druid' for x in handlers)==15 and status['completedClasses']==[]
    assert next(x for x in status['skills'] if x['talentId']=='druid.summongrizzly')['implementation']!='handler-present'
    artifacts += [ROOT/name for name in ['tools/record_grizzly_sequence.py','tools/record_grizzly_body.py',
        'tools/probe_grizzly_sequence_reference.py','tools/probe_grizzly_rate_reference.py','tools/probe_grizzly_smite_sequence_reference.py',
        'tests/check_grizzly_sequence.py','tests/check_creeper_stats.py']]
    result=dict(referenceVersion='3.3.93847',installed=False,grizzlyHandlerEnabled=False,handlers=171,druidHandlers=15,
        completedClasses=[],goalComplete=False,
        verified=dict(nativeClockSteps=5742,nativeRatePlannerContexts=576,nativeComposedContexts=576,nativeComposedPhases=23616,
            passMarkers=241,boots=3,actualReloads=2,pythonTests=70,namedDefinitions=573,inputFingerprints=[len(x['InputHashes']) for x in audits]),
        frozenFullAcceptance=dict(commit=FROZEN,receipt=frozen_path,sha256=hashlib.sha256(historical(frozen_path)).hexdigest(),
            package='.local/class-skills/bear-smite',fullClassMarkers=1184,boots=3,actualReloads=2,saveSnapshots=35),
        inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},
        established=[
            'Native inner mode14controller reuses the parameterized sentry clock and source attack-rate helper. It prepares one immutable packet at entry,consumes existing action before advance,marks do only when the supplied release marks it,and ends before any extra action. No player sequence penalty or owner IAS inheritance is introduced.',
            'All5742clock steps,576rate/initial-planner cases and576composed contexts/23616phases match the complete source composition for mode/clock/dispatch/flags,cached packet and start RNG. The sparse planner is tested as a helper only;actual mode14uses infinite per-frame callbacks. The reference fixture also records HP/events,but those fields are not claimed as native release acceptance.',
            'Native component JSON roundtrips,invalid saved state,failed start and cancellation/no late replay pass. These are not body save/offline policy checks. Retained saved Grizzly/wolf/spirit/damage/input consumers pass241markers across3boots/2actual reloads. Full1184-marker acceptance stays with the unchanged earlier BearSmite/mitigation package.',
        ],remaining=[
            'Connect actual Grizzly world targeting/AI112movement,ordinary physical hits and sequence release range3/HP/deferred absorb/events/knockback. Native start block presentation currently lives in SourceBlocking.TryBlock;review/defer it at the appropriate release boundary before claiming source prepared-block timing.',
            'Connect controller to saved body,interruptions/death/offline/history/facet handling,full mode-stat refresh,learned40mana/cap1/coexistence/rollback/right+quick and preserved rollout. Exact modern first-think/busy/geometry/path/global-event/seed,full equipment/nonzero-sunder/item-only and lethal chronology remain explicit acceptance.',
            'Final B7effects/audio and separate talent/right/quick icons remain pending. Main remains101013bwith original build/saves/config/client preserved;no Grizzly handler or completed class is claimed.',
        ])
    path=ROOT/'tools/reference/grizzly-sequence-native-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==rendered
    else:path.write_text(rendered)
    print('Grizzly sequence:5742clock/576rate/23616composed phases,241markers/3boots/2reloads,70Python tests;world/release HP pending,171handlers/Druid15.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
