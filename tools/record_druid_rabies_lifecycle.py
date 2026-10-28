#!/usr/bin/env python3
"""Bind separate Rabies marker/emitter clocks, migration and actual reload acceptance."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/druid-rabies-lifecycle'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
    artifacts=[STAGE/name for name in ['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log','druid-rabies-lifecycle-fixtures.json','first-attempt-secondary-fixture-stats-native-checks.log']]
    for kind in ['content','integration']:
        path=STAGE/(kind+'-build.log');assert all(v in path.read_text() for v in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        artifacts += [path,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==45
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    for command,count in [('druid-rabies-packet-check',1),('druid-rabies-lifecycle-check',1),('druid-rabies-lifecycle-reload-check',2),('druid-rabies-lifecycle-snapshot-check',2)]:
        assert log.count(command+': passed')==count
    assert '1275 complete-source contexts / 1052130 callbacks pass' in log
    assert 'overlapping replacement emitters, retained old caster/rank and actual secondary poison pass' in log
    assert 'Sequence contains more than one element' in (STAGE/'first-attempt-secondary-fixture-stats-native-checks.log').read_text()
    fixture=json.loads((STAGE/'druid-rabies-lifecycle-fixtures.json').read_text());state=fixture['State']
    assert fixture['Reloads']==2 and len(state['Markers'])==len(state['Infections'])==2 and len(state['Seeds'])==2 and len(state['Contagions'])==2
    host=next(x for x in state['Markers'] if x['Host']==fixture['Host']);other=next(x for x in state['Markers'] if x['Host']==fixture['Other'])
    assert host['Caster']==fixture['NewCaster'] and host['Rank']==1 and other['Caster']==fixture['OldCaster'] and other['Rank']==3 and host['UntilUtc']==other['UntilUtc']
    assert any(x['PoisonMs']>0 and x['PoisonSource']==fixture['OldCaster'] for x in fixture['Poison'])
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    source_path=ROOT/'tools/reference/druid-rabies-lifecycle-engine-evidence.json';source=json.loads(source_path.read_text())
    assert source['sourcePreparation'] and not source['currentExecutableParity'] and source['emitterContexts']==1275 and source['childContexts']==5 and len(source['completeRoutineHashes'])==14
    assert sha(ROOT/source['fixture']['path'])==source['fixture']['sha256'];artifacts += [source_path,ROOT/source['fixture']['path']]
    for name,digest in source['artifacts'].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        path=STAGE/name;audit=json.loads(path.read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for name,digest in audit['InputHashes'].items():assert sha(ROOT/name)==digest,name
        audits.append(audit);artifacts += [path,ROOT/'tools/reference'/path.name]
    previous=json.loads(subprocess.check_output(['git','show','9e104e8:tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==previous['Definitions']
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[x for x in status['skills'] if x['implementation']=='handler-present']
    assert len(handlers)==179 and sum(x['characterClass']=='Druid' for x in handlers)==23 and status['completedClasses']==[]
    assert next(x for x in status['skills'] if x['talentId']=='druid.rabies')['implementation']!='handler-present'
    for commit,path in [('9e104e8','tools/reference/druid-rabies-infections-native-evidence.json'),('1fb5518','tools/reference/druid-hunger-native-evidence.json')]:frozen_receipt(commit,path)
    artifacts += [ROOT/path for path in ['src/Sanctuary.Content/DruidRabiesInfections.cs','src/Sanctuary.Content/DruidRabiesEmitterClock.cs',
        'tests/Sanctuary.Integration/DruidRabiesInfectionChecks.cs','tests/Sanctuary.Integration/DruidRabiesLifecycleChecks.cs','tests/check_druid_rabies_lifecycle.py',
        'tests/check_creeper_stats.py','tools/probe_druid_rabies_lifecycle.py','tools/review_druid_rabies_clocks.py','tools/reference/druid-rabies-clock-review.json','tools/record_druid_rabies_lifecycle.py']]
    result=dict(referenceVersion='3.3.93847',installed=False,rabiesHandlerEnabled=False,completeCurrentExecutableParity=False,
        handlers=179,druidHandlers=23,completedClasses=[],goalComplete=False,
        verified=dict(passMarkers=45,boots=3,actualReloads=2,saveSnapshots=3,sourceEmitterContexts=1275,sourceChildContexts=5,nativeClockCallbacks=1052130,
            retainedDamageComparisons=12240,retainedInfectionWindowComparisons=1296,pythonTests=70,namedDefinitions=573,inputFingerprints=[len(x['InputHashes']) for x in audits]),
        inputInventory=inputs,artifacts={str(path.relative_to(ROOT)):sha(path) for path in artifacts},
        frozenPredecessors=[dict(commit='9e104e8',receipt='tools/reference/druid-rabies-infections-native-evidence.json',scope='Earlier combined marker/emitter preparation'),
            dict(commit='1fb5518',receipt='tools/reference/druid-hunger-native-evidence.json',scope='Installed Hunger full regression, not acceptance of this new controller')],
        established=[
            'Saved marker112 and emitter515 now have independent identities and expiry. A marker can expire or be replaced while an old emitter remains. The old emitter retains caster/rank/packet supplier and reads the currently visible host marker deadline for each new child. Separate emitters can overlap on one host.',
            'Fourteen complete older routines execute1275emitter/5child contexts under ASan/UBSan,including the zeroing allocator,getters,setters,do30,basic and generic collision. The native emitter clock matches1052130callbacks across every effective rank and missing-caster/host-death controls. Negative-current retirement and zero missing-marker deadline follow those source bodies.',
            'Actual hostile NPC cases cover expired marker with live emitter, harmless missing-marker tail child, replacement by a different Druid, old rank3/caster retained while reading the new rank1marker deadline, and real secondary poison attributed to the original caster. The saved world begins with a negative-current old emitter, two fresh emitters and two distinct-caster markers; both actual reloads preserve the records and retire the old emitter without replaying emissions/damage.',
            'Persistence version1 writes separate marker arrays and migrates version0combined states. Active old emitters retain rank,age,deadline,children and seed; stopped old emitters keep their marker without being revived. Invalid duplicate marker input refuses atomically. Existing poison-cure/no-refresh/host-following/host-death/A-B-A/packet cases remain exercised.',
            'The first lifecycle fixture allowed an unintended early secondary infection, invalidating its single-emitter assumption. The other target now stays beyond projectile reach until the scheduled contact. Gameplay targeting/spread was retained, and the failed log is preserved. All70Python tests and573unchanged named definitions pass.',
        ],remaining=[
            'Connect learned S3bite/start57/do121,separate ordinary weapon and forced-success skill-poison packets,10mana transactions,true right/quick input and cancellation. Current initial miss/block payment,extra transformed AR,lost-range attachment and lethal/reentrant chronology remain explicitly uncertain.',
            'Original-caster death/history/logout/facet and host facet transitions retain explicit unsupported handling pending reference/user policy. Exact modern global unit/state-event ties,actual player/missile seed streams,Fog path geometry and Slow Missiles damage capture remain separate. This accepts the bounded source-derived lifecycle,not every current executable context.',
            'No Rabies handler,full-class regression,new installation or final imagegen asset is claimed. Installed Hunger and original saves/configuration/client remain preserved. Effects and talent/left/right/quick icons are tracked separately.',
        ])
    path=ROOT/'tools/reference/druid-rabies-lifecycle-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Rabies separate lifecycle:1275source contexts/1052130native callbacks,45markers/3boots/2reloads/70Python;179handlers/Druid23,not installed.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
