#!/usr/bin/env python3
"""Bind learned Grizzly casting, retained world acceptance and optional full regression."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
import build_skill_roadmap as roadmap

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/grizzly-casting'
FROZEN='ac3c010'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def historical(name,commit=FROZEN):
    return subprocess.check_output(['git','show',commit+':'+name],cwd=ROOT)


def verify_frozen(link):
    raw=historical(link['receipt'],link['commit'])
    assert hashlib.sha256(raw).hexdigest()==link['sha256']
    data=json.loads(raw)
    for name,digest in data['artifacts'].items():
        value=(ROOT/name).read_bytes() if name.startswith('.local/') else historical(name,link['commit'])
        assert hashlib.sha256(value).hexdigest()==digest,name
    return data


def record(check=False,full_class=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
    artifacts=[STAGE/name for name in ['stats-inputs.json','stats-native-checks.log','casting-run.log','python-tests.log',
        'grizzly-casting-fixtures.json','grizzly-world-fixtures.json','grizzly-body-fixtures.json','first-attempt-left-binding.log']]
    for kind in ['content','integration']:
        path=STAGE/(kind+'-build.log');build=path.read_text()
        assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
        artifacts += [path,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==267
    assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    for name in ['grizzly-casting','grizzly-world','damage-rounding','grizzly-body','dire-wolf-casting','dire-wolf-world','dire-wolf-body',
                 'spirit-wolf-casting','spirit-wolf-world','spirit-wolf-body','barbs-casting','wolverine-casting','oak-casting']:
        assert log.count(name+'-check: passed')==1 and log.count(name+'-reload-check: passed')==2,name
    assert 'Learned Grizzly:40mana once-only SC,cancellation,allocation/payment rollback' in log
    assert log.count('grizzly-world-snapshot-check: passed')==2
    assert log.count('grizzly-world-live-check: passed')==log.count('grizzly-world-live-result: passed')==3
    for name in ['grizzly','dire-wolf','spirit-wolf','barbs','oak','wolverine']:
        for kind in ['right','quick']:
            assert log.count(name+'-'+kind+'-check: passed')==log.count(name+'-'+kind+'-result: passed')==3,name
    python=(STAGE/'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    saved=json.loads((STAGE/'grizzly-casting-fixtures.json').read_text())
    assert len(saved['Bears'])==len(saved['Orders'])==len(saved['States'])==1
    assert saved['Orders']==sorted(set(saved['Orders']))
    for text in saved['States']:
        state=json.loads(text)
        assert state['LearnedCast'] and state['Snapshot']['Birth']['Rank']==2 and state['Controller']['Mode']==1
        assert state['Controller']['Sequence'] is None and state['Controller']['NextUtc']==state['NextRecoveryUtc']
    frozen_path='tools/reference/grizzly-world-native-evidence.json';raw=historical(frozen_path)
    world_link=dict(commit=FROZEN,receipt=frozen_path,sha256=hashlib.sha256(raw).hexdigest(),package='.local/class-skills/grizzly-world')
    frozen=verify_frozen(world_link)
    assert frozen['verified']['passMarkers']==252 and frozen['verified']['naturalConnectedSessions']==3
    verify_frozen(frozen['frozenSequenceAcceptance']);prior_full=verify_frozen(frozen['frozenFullAcceptance'])
    assert prior_full['verified']['fullClassMarkers']==1184
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
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert status==roadmap.build()
    handlers=[x for x in status['skills'] if x['implementation']=='handler-present']
    assert len(handlers)==172 and sum(x['characterClass']=='Druid' for x in handlers)==16 and status['completedClasses']==[]
    assert next(x for x in status['skills'] if x['talentId']=='druid.summongrizzly')['implementation']=='handler-present'
    full_markers=full_saves=None
    if full_class:
        complete=inventory.validate_inventory(STAGE/'class-inputs.json')
        assert complete['artifacts']==inputs['artifacts'] and complete['copiedData']==inputs['copiedData']
        complete_log=(STAGE/'class-native-checks.log').read_text()
        assert ': failed' not in complete_log and ' ERR]' not in complete_log and complete_log.count('Shutting down')==3
        assert 'including two save/reload cycles.' in (STAGE/'full-run.log').read_text()
        assert complete_log.count('named-gear-check: passed')==1 and complete_log.count('named-gear-reload-check: passed')==2
        assert complete_log.count('first-loop-catalogue-check: passed')==1
        full_markers=complete_log.count(': passed');full_saves=complete_log.count('Writing world save snapshot done')
        assert full_markers==1184 and full_saves>=3
        for name,expected in [('full-named-item-runtime-audit.json',audits[0]),('full-first-loop-affix-audit.json',audits[1])]:
            path=STAGE/name;assert json.loads(path.read_text())==expected;artifacts.append(path)
        artifacts += [STAGE/name for name in ['class-inputs.json','class-native-checks.log','full-run.log']]
    artifacts += [ROOT/name for name in ['tools/record_grizzly_casting.py','tools/record_grizzly_body.py','tools/build_skill_roadmap.py',
        'tools/build_class_skills.py','tools/install_grizzly_casting.py','tools/record_grizzly_casting_installation.py','tests/check_grizzly_casting.py','tests/check_creeper_stats.py','data/Sanctuary/Balance/class-skill-status.json']]
    result=dict(referenceVersion='3.3.93847',installed=False,grizzlyHandlerEnabled=True,handlers=172,druidHandlers=16,completedClasses=[],goalComplete=False,
        verified=dict(passMarkers=267,boots=3,actualReloads=2,rightCasts=3,quickCasts=3,naturalConnectedSwats=3,pythonTests=70,
            namedDefinitions=573,inputFingerprints=[len(x['InputHashes']) for x in audits],fullClassMarkers=full_markers,
            fullClassBoots=3 if full_class else None,fullClassReloads=2 if full_class else None,fullClassSaveSnapshots=full_saves),
        frozenWorldAcceptance=world_link,inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},
        established=[
            'Learned Grizzly now uses the shared Druid SC/FCR/ground-fit target path,one40mana payment after successful allocation and independent cap1replacement. Null/failed/unsupported allocation and payment failure retain the previously paid bear and saved order;failed placement and canceled windups do not pay or replace.',
            'Native ground/live/self/town/deleted target handling,fractionally insufficient/exact mana,movement/out-and-back/map/get-hit/frozen/stop cancellation,actual Tarnhelm/Battle Command ranks,constant cap1and retained copied birth pass. Actual learned Spirit Wolf and Dire Wolf plus Raven/vine/spirit coexistence remain independent.',
            'Three real right-hold/release and three quick packets cast one paid bear each. Two actual reloads preserve the learned bear/birth/order/bindings while discarding unpaid player windups and queued NPC attacks. Replacement after reload keeps cap1. Retained world combat includes three connected timer-driven cached swats and mapped knockback/no stun.',
            'The first binding check found Grizzly missing from the native right-only guard despite source leftskill0/rightskill1. The guard now agrees with the pinned descriptor. Its failed log is retained. Existing registered internal Grizzly component checks now require the learned route rather than an obsolete disabled-handler condition.',
        ],remaining=[
            'Ground/sector/LOS/placement retry/path/target-order adapters,first-think/busy/global-event timing,full mode-stat refresh and defender/global RNG remain bounded native projections. Exact current executable transport,equipment/nonzero-sunder/item-only/oskill and Player/PvP recipients remain acceptance.',
            'Hidden post-allocation physical versus cached HP/Life Tap/return semantics and complete lethal HP/death/Thorns/arbitrary-event chronology remain explicit limits. These nonlethal learned/world checks do not certify every modern event case.',
            'Preserved rollout requires full acceptance and its own installation/smoke/evidence gate. Main remains101013buntil that succeeds. Final B7effects/audio and separate talent/right/quick icons remain placeholders.172handlers/Druid16is coverage,not class completion.',
        ])
    path=ROOT/'tools/reference/grizzly-casting-native-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==rendered
    else:path.write_text(rendered)
    print(f'Learned Grizzly:267markers/3boots/2reloads,right+quick,cap1/40mana;fullClass={full_markers};172handlers/Druid16,not installed.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');parser.add_argument('--full-class',action='store_true')
    args=parser.parse_args();record(args.check,args.full_class)
