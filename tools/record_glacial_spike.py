#!/usr/bin/env python3
"""Bind saved player Glacial Spike and retained Mage/player area regressions."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import subprocess
import sys

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from review_weapon_event_order import function
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/sorceress-glacial-spike'
BUILD=2
sha=inventory.sha


def historical(path):return subprocess.check_output(['git','show','2385327:'+path],cwd=ROOT)


def record(check=False):
    inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
    counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text()
    assert sum(counts.values())==168 and ' ERR]' not in log and ': failed' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'))
    from check_glacial_spike import configuration
    config=configuration(True);assert len(config['extra_families'])==16 and len(config['extra_checks'])==6
    for name in config['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
    for name in config['extra_checks']:assert counts[name]==3
    for spell in ('fire-bolt','ice-bolt','ice-blast','glacial-spike'):
        assert counts[spell+'-reload-result']==2
        for mode in ('right','quick'):
            assert counts[spell+'-'+mode+'-check']==counts[spell+'-'+mode+'-result']==3
            assert log.count(spell+'-'+mode+': windup')==3
    for name in ('mage-glacial-live','glacial-player-live'):assert counts[name+'-result']==3
    assert 'Glacial Spike:765independent rank/hard suppliers' in log
    saved=json.loads((STAGE/'glacial-spike-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Control']['Freeze']['Source']==saved['PeerControl']['Freeze']['Source']==saved['Owner']
    assert saved['Flight']['ColdFrames']==0 and saved['Flight']['VelocityFp8']==3072 and saved['Flight']['Frames']==40
    data=ROOT/'data/Sanctuary/Balance/necromancer-summons.json';assert data.read_bytes()==historical(str(data.relative_to(ROOT)))
    full=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json';raw=json.loads(full.read_text())['glacialspike']
    row=next(x for x in json.loads(data.read_text()) if x['key']=='glacialspike')
    assert row['fields']=={k:v for k,v in raw.items() if isinstance(v,(int,str)) and not k.startswith('*') and k not in ('lineNumber','Missile')}
    assert raw['*ID']==96 and raw['pSrvHitFunc']==13 and raw['ResultFlags']==5 and raw['HitFlags']==2 and raw['EType']=='frze'
    status=ROOT/'data/Sanctuary/Balance/class-skill-status.json';now=json.loads(status.read_text());before=json.loads(historical(str(status.relative_to(ROOT))))
    updated=next(x for x in now['skills'] if x['talentId']=='sorceress.glacialspike');old=next(x for x in before['skills'] if x['talentId']=='sorceress.glacialspike')
    assert old['verification']=='build-only' and updated['verification']=='partial-native-coverage'
    old.update({k:updated[k] for k in ('verification','remaining')});assert before==now and not now['completedClasses']
    parent=ROOT/'tools/reference/ice-blast-native-evidence.json';mage=ROOT/'tools/reference/mage-glacial-native-evidence.json'
    for p in (parent,mage):assert p.read_bytes()==historical(str(p.relative_to(ROOT)))
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    reviewed={};sources=[]
    for relative,names in (
        ('source/D2Game/src/MISSILES/MissMode.cpp',('MISSMODE_SrvHit13_GlacialSpike_HellMeteorDown','MISSMODE_GetDamageValue')),
        ('source/D2Game/src/SKILLS/Skills.cpp',('sub_6FD10200','sub_6FD10140'))):
        p=ROOT/'.local/class-skills/d2moo-pinned-source'/relative;blob=p.read_bytes();sources.append(p)
        assert hashlib.sha1(b'blob '+str(len(blob)).encode()+b'\0'+blob).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        for name in names:reviewed[name]=hashlib.sha256(function(blob.decode(),name).encode()).hexdigest()
    assert reviewed['MISSMODE_SrvHit13_GlacialSpike_HellMeteorDown']==json.loads(mage.read_text())['reviewedNotExecuted']['MISSMODE_SrvHit13_GlacialSpike_HellMeteorDown']
    prior=json.loads((ROOT/'.local/class-skills/sorceress-ice-blast/birth-inputs.json').read_text())
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][name]==prior['artifacts'][name]
    assert tested['copiedData'].keys()==prior['copiedData'].keys()
    assert {p for p in prior['copiedData'] if prior['copiedData'][p]!=tested['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for kind in ('content','integration'):
        build=(STAGE/f'{kind}-build-{BUILD}.log').read_text();assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
    audited=inventory.validate_inventory(STAGE/'audit-inputs.json');assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text();sizes=[]
    for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
        audit=json.loads((STAGE/name).read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in audit['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(historical('tools/reference/'+name));identity=('InputHashes','ContentAssemblySha256','ContentAssemblyPath')
        assert {k:v for k,v in audit.items() if k not in identity}=={k:v for k,v in old.items() if k not in identity}
        if 'ContentAssemblyPath' in audit:assert (ROOT/audit['ContentAssemblyPath']).resolve()==(STAGE/'content/Sanctuary.Content.dll').resolve()
        sizes.append(len(audit['InputHashes']))
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    checks=(STAGE/f'checks-{BUILD}.log').read_text()
    for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert message in checks
    assert 'Traceback' not in checks
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    files=[Path(__file__).resolve(),ROOT/'tests/check_glacial_spike.py',ROOT/'tools/build_skill_roadmap.py',
        ROOT/'tools/review_weapon_event_order.py',ROOT/'.local/class-skills/d2moo-source-tree.json',full,data,parent,mage,*sources,
        *(ROOT/'docs'/name for name in ('sorceress-glacial-spike-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','glacial-spike-fixtures.json','mage-glacial-fixtures.json',
            'audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
        *(ROOT/'tools/reference'/name for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
    result=dict(referenceVersion='3.3.93847',component='Player Glacial Spike saved hit13 area path',installed=False,graphicalClientVerified=False,completedClasses=[],
        verified=dict(nativeMarkers=168,boots=3,actualReloads=2,saves=3,independentRankHardSuppliers=765,glacialRightQuickActions=6,retainedFireIceBlastRightQuickActions=18,
            glacialPostLoadAreaFlights=2,mageGlacialLiveSessions=3,legacyPlayerGlacialLiveSessions=3,fixtureFamilies=16,sharedChecks=6,auditMarkers=4,auditFingerprints=sizes,pythonTests=71,unchangedInstalledArtifacts=106),
        reviewedNotExecuted=dict(legacyCommit=tree['sha'],functions=reviewed,scope='Complete older hit13/damage reader/copied-area entry and defense static review;shared service execution and current native acceptance are separate.',modernExecutableVerified=False),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['Player Glacial uses pinned40frame/16velocity/hit13 and saved shared flight. Contact,active expiry and wall termination explode;inactive/loaded aging does not replay damage.',
            'One captured raw damage packet is copied across recipients with independent mitigation/freeze. Impact-time owner hard Blizzard at retained Glacial rank is separate from damage/gear snapshot;actual mid-flight training/Nightwing controls pass.',
            'Natural terminal damage,wall occlusion,mixed immune/resisted potion lanes,Slow Missiles,lifecycle,lethal frozen corpses,six real inputs and two actual resumed area flights retain exact held wounds/freezes/mana/bindings.',
            'Existing Mage fixed10/controller/criticals and direct player area compatibility remain independently exercised,including three Mage and three legacy player live sessions. Gameplay data is unchanged;only Glacial verification/remaining annotations advance.'],
        remaining=['Common12tile aim/LOS admission,native room/terrain/center/radius/filter/order and unmapped footprint,first dispatch/zero aim/creator RNG,modern hit13/router/pierce/owner/event timing and special/PvP recipients remain open.',
            'Harmless restart/offline aging/cancellation are existing host adapters. Actual old-DLL upgrade/unheld restart,full graphical acceptance and ordinary cold-chill shatter remain unverified. Effects/audio/icons stay separately pending.',
            'Fire Ball,remaining Nova/Frost Nova/Static/passive contracts and Inferno held-input/payment/release remain separate unfinished work. No class certificate is inferred.'])
    output=ROOT/'tools/reference/glacial-spike-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Glacial Spike:168native markers/3boots/2reloads,six real casts,retained Mage/player areas,four audits/71Python;no class certificate or deployment.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
