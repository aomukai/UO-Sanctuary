#!/usr/bin/env python3
"""Bind bounded Rabies infection preparation without claiming full emitter parity."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/druid-rabies-infections'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
    artifacts=[STAGE/name for name in ['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log','druid-rabies-infection-fixtures.json',
        'first-attempt-missing-rows-stats-native-checks.log','second-attempt-host-death-oracle-stats-native-checks.log']]
    for kind in ['content','integration']:
        path=STAGE/(kind+'-build.log');text=path.read_text()
        assert all(value in text for value in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        artifacts += [path,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==45
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    for command,count in [('druid-rabies-packet-check',1),('druid-rabies-infection-check',1),('druid-rabies-infection-reload-check',2),('druid-rabies-infection-snapshot-check',2)]:
        assert log.count(command+': passed')==count
    assert '12240 full-source damage comparisons / 1296 source infection windows' in log
    assert 'independent poison cure, no refresh, deadline inheritance, A-B-A contacts, host death, inactive expiry and atomic restore passed' in log
    assert "key 'rabiesplague' was not present" in (STAGE/'first-attempt-missing-rows-stats-native-checks.log').read_text()
    assert 'Host death erased emitted children or kept emitting' in (STAGE/'second-attempt-host-death-oracle-stats-native-checks.log').read_text()
    fixture=json.loads((STAGE/'druid-rabies-infection-fixtures.json').read_text())
    assert fixture['Reloads']==2 and len(fixture['State']['Infections'])==2 and len(fixture['State']['Contagions'])==1
    assert all(x['UntilUtc']==fixture['State']['Infections'][0]['UntilUtc'] for x in fixture['State']['Infections'])
    assert fixture['State']['Contagions'][0]['InfectionUntilUtc']==fixture['State']['Infections'][0]['UntilUtc']
    assert len(fixture['State']['Seeds'])==1 and any(x['PoisonMs']>0 for x in fixture['Poison'])
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        path=STAGE/name;audit=json.loads(path.read_text())
        assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for name,digest in audit['InputHashes'].items():assert sha(ROOT/name)==digest,name
        audits.append(audit);artifacts += [path,ROOT/'tools/reference'/path.name]
    previous=json.loads(subprocess.check_output(['git','show','81c9552:tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==previous['Definitions']
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers=[x for x in status['skills'] if x['implementation']=='handler-present']
    assert len(handlers)==179 and sum(x['characterClass']=='Druid' for x in handlers)==23 and status['completedClasses']==[]
    assert next(x for x in status['skills'] if x['talentId']=='druid.rabies')['implementation']!='handler-present'
    clock_path=ROOT/'tools/reference/druid-rabies-clock-review.json';clock=json.loads(clock_path.read_text())
    assert clock['staticReview'] and not clock['compiledExecution'] and not clock['currentExecutableParity'] and len(clock['completeRoutineHashes'])==9
    for commit,path in [('81c9552','tools/reference/druid-rabies-packets-native-evidence.json'),('1fb5518','tools/reference/druid-hunger-native-evidence.json')]:
        frozen_receipt(commit,path)
    artifacts += [ROOT/path for path in ['src/Sanctuary.Content/DruidRabiesInfections.cs','tests/Sanctuary.Integration/DruidRabiesInfectionChecks.cs',
        'tests/check_druid_rabies_infections.py','tests/check_creeper_stats.py','tools/build_class_skills.py','tools/review_druid_rabies_clocks.py',
        'tools/reference/druid-rabies-clock-review.json','tools/record_druid_rabies_infections.py']]
    result=dict(referenceVersion='3.3.93847',installed=False,rabiesHandlerEnabled=False,completeEmitterLifecycleParity=False,
        handlers=179,druidHandlers=23,completedClasses=[],goalComplete=False,
        verified=dict(passMarkers=45,boots=3,actualReloads=2,saveSnapshots=3,damageComparisons=12240,infectionWindowComparisons=1296,
            pythonTests=70,namedDefinitions=573,inputFingerprints=[len(x['InputHashes']) for x in audits]),
        inputInventory=inputs,artifacts={str(path.relative_to(ROOT)):sha(path) for path in artifacts},
        frozenPredecessors=[dict(commit='81c9552',receipt='tools/reference/druid-rabies-packets-native-evidence.json',scope='Prepared poison packet acceptance'),
            dict(commit='1fb5518',receipt='tools/reference/druid-hunger-native-evidence.json',scope='Installed Hunger full regression; not acceptance of this new controller')],
        established=[
            'Prepared saved infection112 persists independently from actual poison. Same-host reinfection retains original caster/rank/deadline; secondary hosts receive remaining original lifetime. Each emitted child captures retained rank with live caster hard synergy/mastery through the accepted poison helper. Ordinary elapsed-age0,4cadence follows the pinned getter/do30/generic decrement order.',
            'Existing path vectors, saved source steps, size1 footprint and poison mitigation/delivery are reused. LastCollision is most recent target identity, allowing A-B-A contacts. Marker posting precedes poison, and curing actual poison leaves the infection. Host death stops its emitter while already-emitted children and other infected hosts remain independent.',
            'Native cases exercise host following, initial/secondary expiry, no-refresh reinfection, poison cure, repeated contacts, valid10/invalid9frame contacts, immutable snapshots and atomic failed restore. Two real reloads retain owner/hosts, captured poison, paths, seeds and expiry without replaying overdue emission/damage. Three actual saves retain the other summon/poison/item consumers.',
            'The initial native run refused missing runtime missile keys. The generator now imports the two pinned Rabies missile rows. A subsequent host-death oracle wrongly required unchanged total child count despite a newly infected host emitting; it now checks survival of the original children. Both failed logs are retained; production propagation was not weakened.',
        ],remaining=[
            'IMPORTANT: split infection markers from emitter lifetimes before complete lifecycle acceptance. The current preparation removes its combined marker/emitter at marker deadline. Static source review shows do30ignores generic collision return and waits for current<0; an old emitter can briefly outlive a marker or observe a replacement marker. The negative-frame tail, multiple old/new emitters on one host, and missing-marker child deadline need composed source/native verification.',
            'Original-caster death/history/logout/facet and host facet transitions are explicitly recorded unsupported contexts. They age without replay while unresolved; this is not a silently approved D2R rule. The logout/facet preference question remains pending. Exact global unit/event ordering, player/missile seed streams, Fog path geometry and modern Slow Missiles damage capture remain unverified.',
            'Initial S3bite,start57/do121,two separate ordinary/skill-poison packets,10mana transactions,true learned/right/quick input,cancellation and current initial miss/block/range-loss/extraAR behavior remain unfinished. No Rabies handler,full-class regression,installation or final art is claimed. Effects and talent/left/right/quick icons remain separate placeholders.',
        ])
    path=ROOT/'tools/reference/druid-rabies-infections-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Rabies bounded infection preparation:45markers/3boots/2reloads/70Python; emitter-tail and learned casting still unfinished;179handlers/Druid23,not installed.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
