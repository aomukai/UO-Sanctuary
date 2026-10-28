#!/usr/bin/env python3
"""Bind prepared Shock Wave flight/stun acceptance without claiming learned casting."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/druid-shock-wave'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'inputs.json')
    paths=[STAGE/name for name in ['inputs.json','native-checks.log','prepared-run.log','shock-wave-fixtures.json','expiry-save-0.json','expiry-save-1.json','python-tests.log']]
    for kind in ['content','integration']:
        build=STAGE/(kind+'-build.log');assert all(x in build.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        paths += [build,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    text=(STAGE/'native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==15
    assert text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    for name,count in [('shock-wave-components-check',1),('shock-wave-components-reload-check',2),('shock-wave-live-result',3),('shock-wave-expiry-stage-check',2),('named-gear-check',1),('named-gear-reload-check',2),('first-loop-catalogue-check',1)]:assert text.count(name+': passed')==count,name
    assert '3825physical/17920stun source comparisons pass' in text
    assert '15markers/3boots/2actual reloads/3connected natural wave sessions/2expired-save controls' in (STAGE/'prepared-run.log').read_text()
    saved=json.loads((STAGE/'shock-wave-fixtures.json').read_text());assert saved['ExpiredChecks']==2 and saved['ExpiryOwner']==0
    waves=saved['State']['Missiles'];assert len(waves)==5 and len(saved['State']['Leases'])==1
    assert all(x['Owner']==saved['Owner'] and x['Remaining']==13 and x['Min256']==2560 and x['Max256']==5120 for x in waves)
    assert any(x['LastCollision']==saved['Target'] for x in waves) and saved['Life256']<50000*256
    assert any(x['Target']==saved['Target'] for x in saved['Stuns'])
    for boot in [0,1]:
        expiry=json.loads((STAGE/f'expiry-save-{boot}.json').read_text());assert expiry['owner']>0 and expiry['files']
    for label in ['engine','factory-engine','collision-engine','stun-engine']:
        path=ROOT/f'tools/reference/druid-shock-wave-{label}-evidence.json';proof=json.loads(path.read_text())
        assert proof['referenceVersion']=='3.3.93847';paths.append(path)
        for name,digest in proof['artifacts'].items():assert sha(ROOT/name)==digest,name;paths.append(ROOT/name)
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        path=STAGE/name;audit=json.loads(path.read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name,digest in audit['InputHashes'].items():assert sha(ROOT/name)==digest,name
        audits.append(audit);paths.append(path)
    previous=json.loads(subprocess.check_output(['git','show','2a38d48:tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==previous['Definitions']
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    for name in ['druid-rabies-native-evidence.json','druid-rabies-away-native-evidence.json']:frozen_receipt('2a38d48','tools/reference/'+name)
    paths += [ROOT/name for name in ['tools/record_druid_shock_wave_prepared.py','tools/reference/druid-shock-wave-preflight.json','tools/reference/druid-shock-wave-modern-scope.json','tests/check_shock_wave_prepared.py','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']]
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[x for x in status['skills'] if x['implementation']=='handler-present']
    assert len(handlers)==180 and sum(x['characterClass']=='Druid' for x in handlers)==24 and status['completedClasses']==[]
    result=dict(referenceVersion='3.3.93847',preparedController=True,learnedHandlerEnabled=False,installed=False,fullClassRegression=False,
        handlers=180,druidHandlers=24,completedClasses=[],goalComplete=False,currentExecutableParity=False,
        verified=dict(passMarkers=15,boots=3,actualReloads=2,actualConnectedWaveSessions=3,expiredSaveControls=2,physicalComparisons=3825,stunComparisons=17920,
            pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[len(x['InputHashes']) for x in audits]),
        inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        frozenInstalledPredecessor=dict(commit='2a38d48',receipt='tools/reference/druid-rabies-installation-evidence.json',scope='Preserved installed Rabies; not this new component'),
        established=[
            'Five linear waves share one persisted cast/target160mswindow and retain per-wave LastCollide,rank,source-zero physical endpoints,modifiers,RNG,position/steps and14-frame clock. Overlap tests skip delayed/recent recipients before choosing a second eligible monster. Different executions have independent windows; four-frame boundary and failed-allocation controls pass.',
            'Native health commits before stun,including real OnDamage observation. Ordinary physical immunity retains eligible stun; a real mapped shield blocks both health and stun. Complete-source3825physical and17920stun eligibility/cap/refresh contexts match the native arithmetic. Actual connected automatic flight/damage/stun/expiry passes on all three boots.',
            'Exact held wave/history/windows/seed/fractional life/stun survive two real reloads. Separate active waves are found in actual serialized Shock Wave payload bytes before each shutdown; their lifetime expires during restart and neither replays damage. Offline waves continue expiring; invalid restore is atomic and snapshots do not alias mutable step arrays.',
            'Captured50percent velocity scaling preserves endpoints. Unestablished modern Slow Missiles damage scaling is explicitly refused before allocation instead of silently ignored. Source cast-local grouping is the documented released2.7/primary-testing interpretation; native terrain/Q12/room ordering remain adapters.',
            'Both builds have zero warnings/errors,all70Python tests pass,and fresh674/21input fingerprints preserve573named-item decisions. Runtime tables import the pinned511row. Final travel/contact/stun placeholders are identified independently from talent/left/right/quick icons. Installed Rabies/main world/client remain unchanged.',
        ],remaining=[
            'Learned bear-only SC/FCR casting is not enabled. Next connect do8success/7mana after-factory payment,actual right/quick inputs,hard/soft Maul ranks/gear,cancellation/unshift/Stop/movement/lost mana and unpaid restart. Then refresh canonical coverage and run retained class/projectile regression before installation.',
            'DAMAGE_VS_MONTYPE item-stat producer/consumer remains separately unsupported; source-zero does not erase that source branch. Modern Slow Missiles damage-scale ordering,foreign/PvP/unmapped recipients,exact RNG/Fog/geometry/current executable behavior and full graphical acceptance remain open.',
            'Native synchronous death or reentrant owner/target changes after health commit retain an explicit fault for post-health stun chronology. Other unsupported caster death/history/offline/facet contexts age harmlessly with a stored fault; no new policy approval or exact D2R owner-transition certificate is inferred.',
            'Final bear cast/wave/contact/stun/expiry effects/audio and separate icons still need assets. No new learned talent,completed class,full-class acceptance,installation or overall-goal completion is claimed.',
        ])
    destination=ROOT/'tools/reference/druid-shock-wave-prepared-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==encoded
    else:destination.write_text(encoded)
    print('Prepared Shock Wave:15markers/3boots/2reloads/3connected sessions/2expired-save controls/70Python;180handlers/Druid24; learned casting and full regression next.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
