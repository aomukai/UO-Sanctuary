#!/usr/bin/env python3
"""Bind approved Rabies away-policy acceptance to the unchanged full-tested content."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/druid-rabies-away'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inputs=json.loads((STAGE/'inputs.json').read_text())
    for name,digest in inputs['sourceInputs'].items():assert sha(ROOT/name)==digest,name
    for name,digest in inputs['copiedData'].items():assert sha(ROOT/'data/Sanctuary'/name.removeprefix('Data/Sanctuary/'))==digest,name
    native=json.loads((ROOT/'tools/reference/druid-rabies-native-evidence.json').read_text())
    assert native['verified']['passMarkers']==193 and native['verified']['fullClassMarkers']==1184
    assert inputs['copiedData']==native['inputInventory']['copiedData']
    for name,digest in native['inputInventory']['artifacts'].items():
        if name!='Assemblies/Sanctuary.Integration.dll':assert inputs['artifacts'][name]==digest,name
    for name,digest in native['inputInventory']['sourceInputs'].items():
        if name.endswith('.cs'):assert inputs['sourceInputs'][name]==digest,name
    for kind in ['content','integration']:
        name='Sanctuary.'+kind.title()+'.dll';assert sha(STAGE/kind/name)==inputs['artifacts']['Assemblies/'+name]
    build=(STAGE/'integration-build.log').read_text();assert all(x in build for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    log=(STAGE/'native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==5
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('rabies-away: disconnect-ready')==2 and log.count('Disconnected. [0 Online]')==2
    assert 'real facet departure/return,two actual disconnects/reloads,clock-only absence,future-only resumption,original expiry and independent poison pass' in log
    assert '5markers/3boots/2actual reloads/2real disconnects' in (STAGE/'run.log').read_text()
    fixture=json.loads((STAGE/'rabies-away-fixtures.json').read_text());assert fixture['Reloads']==2
    assert all(fixture['State'][key]==[] for key in ['Markers','Infections','Contagions'])
    assert 0<fixture['Life256']<50000*256 and not any(x['PoisonMs']>0 for x in fixture['Poison'])
    decision=ROOT/'tools/reference/druid-rabies-away-deviation.json';assert json.loads(decision.read_text())['userDecision']=='Stop spreading; timers continue'
    paths=[STAGE/name for name in ['inputs.json','native-checks.log','run.log','integration-build.log','rabies-away-fixtures.json','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll']]
    paths += [decision,Path(__file__).resolve(),ROOT/'tests/check_rabies_away.py',ROOT/'tests/Sanctuary.RabiesPolicy/Sanctuary.Integration.csproj',ROOT/'tests/Sanctuary.RabiesPolicy/RabiesAwayChecks.cs']
    result=dict(referenceVersion='3.3.93847',approvedSanctuaryDeviation=True,gameplayChanged=False,installed=False,currentExecutableParity=False,
        verified=dict(passMarkers=5,boots=3,actualReloads=2,actualDisconnects=2,facetDepartureAndReturn=True,independentPoison=True,zeroWarningBuild=True),
        inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        established=[
            'A real attached client establishes the connected emission baseline. Moving the caster to Felucca suppresses emission/contact and leaves seed,marker deadline and existing poison unchanged,while emitter/child ages advance. Returning to Trammel performs only the next future eligible emission,not missed events.',
            'The fixture invokes native NetState.Disconnect with real draining clients twice and waits for actual network teardown; it never substitutes NetState=null or an actions override. Offline ticks age/retire existing children and do not emit or reset poison. The30second transport bound accommodates the native20second drain and is separate from the explicitly advanced skill clock.',
            'Two actual world reloads preserve age,deadline,seed and poison. Connected return skips loaded overdue work and resumes the next eligible event. A later offline expiry removes all markers/emitters/children at original deadlines. Existing real poison still damages its victim and expires independently; owner transitions preserve mana.',
            'This separate integration project shares canonical fixtures but adds policy checks without replacing the full-class integration package. Gameplay assembly,native runtime and all runtime data are byte-identical to the193focused/1184full accepted candidate. Current propagation guards already implement the approved policy; legacy diagnostic Fault strings still call offline/facet contexts unverified.',
        ],remaining=[
            'Prepared controller policy acceptance uses a parked one-hour inspection clock and explicit frame ticks,not an hour-long graphical play session. Learned Rabies inputs/payment/bite/poison are established by the separate193-check scope.',
            'Original caster death,deletion,class/history changes,host facet changes and exact current D2R event/geometry/seed/ABI behavior remain separate uncertainties. This user-approved policy is not asserted as D2R behavior. Final effects/icons remain pending.',
        ])
    path=ROOT/'tools/reference/druid-rabies-away-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Rabies approved away policy:5markers/3boots/2actual reloads/2real disconnects; unchanged full-tested gameplay/data.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
