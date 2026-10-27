#!/usr/bin/env python3
"""Bind live Grizzly AI112/combat, saved cancellation and retained consumers."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'grizzly-world'
FROZEN='bbcef80'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def historical(name,commit=FROZEN):
    return subprocess.check_output(['git','show',commit+':'+name],cwd=ROOT)


def record(check=False):
    inventory.STAGE=STAGE
    inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
    artifacts=[STAGE/name for name in ['stats-inputs.json','stats-native-checks.log','world-run.log',
        'python-tests.log','grizzly-body-fixtures.json','grizzly-world-fixtures.json','damage-rounding-checks.json','first-attempt-ordinary-clock.log']]
    for kind in ['content','integration']:
        path=STAGE/(kind+'-build.log');build=path.read_text()
        assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
        artifacts += [path,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==250
    assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    for name in ['grizzly-sequence','bear-smite','beast-mode-stream','grizzly-ai','grizzly-stats']:
        assert log.count(name+'-check: passed')==1,name
    assert '5742shared clock steps,576complete rate/planner contexts and576source/native mode14cached-packet contexts/23616phases pass' in log
    for name in ['grizzly-world','damage-rounding','grizzly-body','dire-wolf-body','dire-wolf-world','dire-wolf-casting',
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
    frozen_path='tools/reference/grizzly-sequence-native-evidence.json';frozen=json.loads(historical(frozen_path))
    assert frozen['verified']['passMarkers']==241 and frozen['verified']['nativeComposedPhases']==23616
    for name,digest in frozen['artifacts'].items():
        raw=(ROOT/name).read_bytes() if name.startswith('.local/') else historical(name)
        assert hashlib.sha256(raw).hexdigest()==digest,name
    full_link=frozen['frozenFullAcceptance'];full_raw=historical(full_link['receipt'],full_link['commit'])
    assert hashlib.sha256(full_raw).hexdigest()==full_link['sha256']
    full=json.loads(full_raw)
    assert full['verified']['fullClassMarkers']==1184 and full['verified']['fullClassSaveSnapshots']==35
    for name,digest in full['artifacts'].items():
        raw=(ROOT/name).read_bytes() if name.startswith('.local/') else historical(name,full_link['commit'])
        assert hashlib.sha256(raw).hexdigest()==digest,name
    assert log.count('grizzly-world-live-check: passed')==log.count('grizzly-world-live-result: passed')==3
    assert 'cached BearSmite callback10/end19,real knockback/zero stun' in log
    saved=json.loads((STAGE/'grizzly-world-fixtures.json').read_text())
    assert saved['Life']==20007 and saved['TargetLife']==20000*256
    controller=saved['State']['Controller']
    assert controller['Mode']==14 and controller['StrikeRequests']==0 and controller['Sequence']['Packet']['StunFrames']==0
    assert saved['State']['Snapshot']['Birth']['Rank']==1 and not saved['State'].get('LearnedCast',False)
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
    artifacts += [ROOT/name for name in ['tools/record_grizzly_world.py','tools/record_grizzly_body.py',
        'tools/probe_grizzly_sequence_reference.py','tools/probe_grizzly_rate_reference.py','tools/probe_grizzly_smite_sequence_reference.py',
        'tests/check_grizzly_world.py','tests/check_creeper_stats.py']]
    result=dict(referenceVersion='3.3.93847',installed=False,grizzlyHandlerEnabled=False,handlers=171,druidHandlers=15,
        completedClasses=[],goalComplete=False,
        verified=dict(nativeClockSteps=5742,nativeRatePlannerContexts=576,nativeComposedContexts=576,nativeComposedPhases=23616,naturalConnectedSessions=3,
            passMarkers=250,boots=3,actualReloads=2,pythonTests=70,namedDefinitions=573,inputFingerprints=[len(x['InputHashes']) for x in audits]),
        frozenSequenceAcceptance=dict(commit=FROZEN,receipt=frozen_path,sha256=hashlib.sha256(historical(frozen_path)).hexdigest(),
            package='.local/class-skills/grizzly-sequence',passMarkers=241),
        frozenFullAcceptance=full_link,
        inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},
        established=[
            'Saved live AI112controller consumes shared target/ground/path/motion/rate services with Grizzly B7metadata. A1physical attacks use their own action7clock and ordinary packet,without the internal BearSmite flat field. Owner live mode rank,copied birth and innate NPCrank1plus live Battle Command stay distinct.',
            'Native BearSmite prepares accuracy/block/damage/critical/mitigation once at sequence entry,commits cached total at callback10with base248,and reaches neutral on19. Changing target resistance and actor Enchant during windup does not reroll or rewrite it. The later250physical field is not extra HP loss. Real mapped knockback starts without stun. Three authenticated connected40ms timer sessions release the actual swat and damage/knockback.',
            'Block decision and presentation now have separate shared entry points. Existing TryBlockstill rolls/presents together;the prepared route retains a real skeleton queued action until release,then presents/interrupts it without health loss. Lost range,offline and recovery cancellation retain the consumed start seed and cannot replay a late hit.',
            'Two actual world reloads preserve copied birth,FP256injury and consumed start seed while discarding unpaid future attack work. Loaded skips do not heal or attack. Shared owner follow and atomic malformed-state refusal pass. All250focused markers across3boots/2reloads and70Python tests pass;fresh item fingerprints retain573named decisions. Older full1184-marker acceptance belongs to its unchanged frozen BearSmite package.',
            'The first attempt used an incorrect ordinary A1test golden. Complete sparse scheduler consumes frame-flag7at offset7with speed256,so entry25releases32and ends38. The corrected test retains the old failure log;this does not change the separate mode14callback10rule.',
        ],remaining=[
            'Learned40mana/cap1/coexistence/rollback/right+quick and preserved rollout remain pending. First-think25and slowed busy/neutral continuation reuse declared shared-beast entry policies;exact modern timing,full mode-list refresh and global queue/AI ordering remain acceptance. AI112requests walking;unmapped running animation explicitly refuses rather than borrowing wolf RN metadata.',
            'Native target/sector/tie/LOS/ground-fit/path and source-sized distance are explicit adapters. SourceBlockingretains its existing independent native defender-RNG supplier. Full current-executable seed/stat/equipment/nonzero-sunder/item-only transport and early immunity/absorption callbacks remain separate.',
            'Post-allocation physical can feed source event fields while cached total controls HP. Hidden modern packet/Life Tap/return semantics remain uncertain. Life Tap,absorb,HP,attempt/return,cold and knockback use declared native bridges;full lethal HP/death/Thorns chronology and arbitrary callbacks remain unfinished acceptance and are not certified by these nonlethal world cases.',
            'Final B7body/motion/attacks/contact/knockback/hurt/death/audio and separate talent/right/quick icons remain placeholders. Main stays101013bwith original build/saves/config/client preserved;no Grizzly handler or completed class is claimed.',
        ])
    path=ROOT/'tools/reference/grizzly-world-native-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==rendered
    else:path.write_text(rendered)
    print('Grizzly world:250markers/3boots/2reloads,3connected swats,70Python tests;learned casting pending,171handlers/Druid15.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
