#!/usr/bin/env python3
"""Bind the approved Flight package to its tested inputs and retained behavior."""
import argparse
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation

ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'dragon-flight-action';PARENT='5265307422c89255c4dc523bab85b24a151c0b87'

def record(check=False):
    wide=BASE/'dragon-flight-action-broader';isolated=BASE/'dragon-flight-action-isolate'
    born=e.inventory(STAGE,wide,STAGE/'source-snapshot-1.json')
    flight=Counter({'dragon-flight-check':1,'dragon-flight-reload-check':2,'dragon-flight-mechanics-check':3})
    flight.update({f'dragon-flight-{mode}{suffix}':3 for mode in ['left','right','quick'] for suffix in ['-check','-result']})
    landing=Counter({'dragon-flight-landing-check':1,'dragon-flight-landing-reload-check':2,'player-teleport-check':1,'player-teleport-reload-check':2,'leap-geometry-check':3,'player-teleport-reload-result':2,'player-teleport-save':3})
    landing.update({f'player-teleport-{mode}{suffix}':3 for mode in ['left','right','quick'] for suffix in ['-check','-result']})
    previous=e.load(ROOT/'tools/reference/dragon-claw-action-native-evidence.json')
    for old,stage,count in [('dragon-claw-action',STAGE,170),('dragon-claw-action-broader',wide,386)]:
        log=BASE/old/'birth-native-checks.log';assert e.sha(log)==previous['files'][str(log.relative_to(ROOT))]
        e.cohort(stage,markers(log)+landing+flight,count)
    base=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    e.cohort(isolated,base+flight,33)
    for stage in [STAGE,wide,isolated]:
        snapshot(STAGE/'source-snapshot-1.json',stage/'birth-inputs.json');tested=e.load(stage/'birth-inputs.json')
        assert tested['artifacts']==born['artifacts'] and tested['copiedData']==born['copiedData']
        for p,h in tested['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
        saved=e.load(stage/'dragon-flight-fixtures.json')
        assert saved['Mana']==saved['PaidMana']==185*256+17 and saved['UnpaidMana']==200*256+17
        assert saved['Charge']['Count']==2 and saved['PendingHealth']==50000*256 and saved['TargetLife']<saved['PendingHealth']
        assert len({saved['Owner'],saved['Unpaid'],saved['Paid']})==3
    for name in ['content-build-3.log','integration-build-4.log']:
        assert all(x in(STAGE/name).read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    assert 'Ran 71 tests' in(STAGE/'python-tests.log').read_text() and '\nOK\n' in(STAGE/'python-tests.log').read_text()
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==6 and all(c['exitCode']==0 for c in commands)
    for name in ['flight-source-check.log','mode-source-check.log','free-cell-source-check.log']:assert 'ASan+UBSan clean' in(STAGE/name).read_text()
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'preservation-before.json')
    client=e.load(ROOT/'tools/reference/blade-fury-held-input-client-evidence.json');assert len(client['patchHashes'])==43
    for group in ['patchHashes','sharedInputs']:
        for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']
    assert sum(r['implementation']=='handler-present' for r in status['skills'])==208
    assert sum(r['implementation']=='handler-present' and r['characterClass']=='Assassin' for r in status['skills'])==28
    result=dict(referenceVersion='3.3.93847',parent=PARENT,approval='Approve this Dragon Flight rule',handlerEnabled=True,installed=False,handlers=208,assassinHandlers=28,completedClasses=[],contentBuild=3,integrationBuild=4,sourceSnapshot=1,focusedMarkers=170,broaderMarkers=386,isolatedMarkers=33,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,pythonTests=71,verificationCommands=6,sourceReproductions=3,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=43,artifacts=born['artifacts'],copiedData=born['copiedData'],scope='Approved two-stage fixed9/14/23Flight,commit15mana,free failed landing,body/boot/typed/poison/charged kick,own-transfer preservation,14pre/post interruptions,arrival death,late-tick catch-up,raw input and original paid/unpaid stage reload cancellation. Retains Teleport,Leap,dragon/claw/charge/trap checks.',limits='Modern executable/payment/charge/stat parity,ground SearchEnemyNear,full item/proc/owner/PvP,half-cell/room/footprint/geometry/event/RNG/client equivalence and final effects/icons remain explicit. No deployment or class/goal completion.')
    files=e.stage_files(STAGE,wide,isolated)+[Path(__file__).resolve(),ROOT/'tests/check_dragon_flight.py',ROOT/'tools/prepare_dragon_flight_reference.py',ROOT/'tools/reference/dragon-flight-preflight.json']
    files += [ROOT/'docs'/name for name in ['dragon-flight-action-proposal.md','dragon-flight-approved-action-rule.md','dragon-flight-action-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
    e.write('dragon-flight-action-native-evidence.json',result,files,check)
    print('Flight accepted:170focused/386retained/33isolated;71Python;208handlers/Assassin28;installed build/saves preserved.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
