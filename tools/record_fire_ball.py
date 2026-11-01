#!/usr/bin/env python3
"""Bind saved Fire Ball acceptance without certifying native terrain or a class."""
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
STAGE=ROOT/'.local/class-skills/sorceress-fire-ball'
BUILD=5
sha=inventory.sha


def historical(path):return subprocess.check_output(['git','show','c31b252:'+path],cwd=ROOT)


def record(check=False):
    inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
    counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text()
    assert sum(counts.values())==185 and ' ERR]' not in log and ': failed' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'))
    from check_fire_ball import configuration
    config=configuration(True);assert len(config['extra_families'])==17 and len(config['extra_checks'])==6
    for name in config['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
    for name in config['extra_checks']:assert counts[name]==3
    for spell in ('fire-bolt','ice-bolt','ice-blast','glacial-spike','fire-ball'):
        assert counts[spell+'-reload-result']==2
        for mode in ('right','quick'):
            assert counts[spell+'-'+mode+'-check']==counts[spell+'-'+mode+'-result']==3
            assert log.count(spell+'-'+mode+': windup')==3
    for name in ('mage-glacial-live','glacial-player-live'):assert counts[name+'-result']==3
    assert 'Fire Ball:765independent rank/hard suppliers' in log
    saved=json.loads((STAGE/'fire-ball-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Flight']['Owner']==saved['Owner']
    assert saved['Flight']['ColdFrames']==0 and saved['Flight']['VelocityFp8']==3840 and saved['Flight']['Frames']==50
    assert saved['Life']<12800000 and saved['PeerLife']==saved['Life']
    full=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json';raw=json.loads(full.read_text())['fireball']
    row=dict(key='fireball',fields={k:v for k,v in raw.items() if isinstance(v,(int,str)) and not k.startswith('*') and k not in ('lineNumber','Missile')})
    data=ROOT/'data/Sanctuary/Balance/necromancer-summons.json'
    assert json.loads(data.read_text())==json.loads(historical(str(data.relative_to(ROOT))))+[row]
    assert raw['*ID']==62 and raw['pSrvHitFunc']==1 and raw['sHitPar1']==0 and raw['ResultFlags']==5 and raw['HitFlags']==2 and raw['EType']=='fire'
    status=ROOT/'data/Sanctuary/Balance/class-skill-status.json';now=json.loads(status.read_text());before=json.loads(historical(str(status.relative_to(ROOT))))
    updated=next(x for x in now['skills'] if x['talentId']=='sorceress.fireball');old=next(x for x in before['skills'] if x['talentId']=='sorceress.fireball')
    assert old['verification']=='build-only' and updated['verification']=='partial-native-coverage'
    old.update({k:updated[k] for k in ('verification','remaining')});assert before==now and not now['completedClasses']
    parent=ROOT/'tools/reference/glacial-spike-native-evidence.json';callback=ROOT/'tools/reference/exploding-arrow-callback-engine-evidence.json'
    for p in (parent,callback):assert p.read_bytes()==historical(str(p.relative_to(ROOT)))
    evidence=json.loads(callback.read_text());probe=ROOT/evidence['probe']['path']
    assert sha(probe)==evidence['probe']['sha256'] and evidence['probe']['parentCases']==256 and evidence['probe']['childAreaCases']==4320
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==evidence['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    reviewed={};sources=[]
    for relative,names in (
        ('source/D2Game/src/MISSILES/MissMode.cpp',('MISSMODE_SrvHit01_Fireball_ExplodingArrow_FreezingArrowExplosion','MISSMODE_GetDamageValue')),
        ('source/D2Game/src/SKILLS/Skills.cpp',('sub_6FD10200','sub_6FD10140'))):
        p=ROOT/'.local/class-skills/d2moo-pinned-source'/relative;blob=p.read_bytes();sources.append(p)
        assert hashlib.sha1(b'blob '+str(len(blob)).encode()+b'\0'+blob).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        for name in names:reviewed[name]=hashlib.sha256(function(blob.decode(),name).encode()).hexdigest()
    for name in evidence['blockHashes'].keys()&reviewed.keys():assert reviewed[name]==evidence['blockHashes'][name]
    prior=json.loads((ROOT/'.local/class-skills/sorceress-glacial-spike/birth-inputs.json').read_text())
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][name]==prior['artifacts'][name]
    assert tested['copiedData'].keys()==prior['copiedData'].keys()
    assert {p for p in prior['copiedData'] if prior['copiedData'][p]!=tested['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json','Data/Sanctuary/Balance/necromancer-summons.json'}
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
    files=[Path(__file__).resolve(),ROOT/'tests/check_fire_ball.py',ROOT/'tools/build_class_skills.py',ROOT/'tools/build_skill_roadmap.py',
        ROOT/'tools/review_weapon_event_order.py',ROOT/'.local/class-skills/d2moo-source-tree.json',full,data,parent,callback,probe,*sources,
        *(ROOT/'docs'/name for name in ('sorceress-fire-ball-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','fire-ball-fixtures.json','glacial-spike-fixtures.json',
            'audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
        *(ROOT/'tools/reference'/name for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
    result=dict(referenceVersion='3.3.93847',component='Player Fire Ball saved hit1 area path',installed=False,graphicalClientVerified=False,completedClasses=[],
        verified=dict(nativeMarkers=185,boots=3,actualReloads=2,saves=3,independentRankHardSuppliers=765,fireBallRightQuickActions=6,retainedRightQuickActions=24,
            fireBallPostLoadAreaFlights=2,mageGlacialLiveSessions=3,legacyPlayerGlacialLiveSessions=3,fixtureFamilies=17,sharedChecks=6,auditMarkers=4,auditFingerprints=sizes,pythonTests=71,unchangedInstalledArtifacts=106),
        legacySource=dict(commit=tree['sha'],reviewedFunctions=reviewed,reusedExecutedCallbackReceipt=str(callback.relative_to(ROOT)),parentCases=256,childAreaCases=4320,
            scope='Complete hit1/copy callback execution is historical with explicit services; current native tests use Sanctuary adapters. Damage reader review is static.',modernExecutableVerified=False),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['Pinned skill47/missile62/hit1,50frames/velocity20,one captured raw damage roll copied across recipients;contact,active expiry and wall termination dispatch the explosion.',
            'Rank/hard synergies,mana,actual Eschuta/mastery changes,typed resistance/immunity and contact-time potion,Slow Missiles,lifecycle,ordinary usable corpses,six real inputs and two actual resumed area flights pass.',
            'Shared copied-area services retain player Glacial and Mage fixed10 regressions. Only exact fireball source row and Fire Ball verification/remaining annotations change runtime data.'],
        remaining=['KNOWN native wall-center discrepancy:half-cell LOS collision plus rounded next-position center can explode on the far side of a wall. The regression records this adapter behavior;D2 wall parity is not established.',
            'Common12tile aim/LOS admission,native room/radius/filter/candidate order and footprint,initial dispatch/zero aim/creator RNG,modern hit1/router/pierce/owner/event/PvP behavior remain open.',
            'Harmless restart/offline aging/cancellation are existing host policies. Actual old-DLL upgrade/unheld restart and full graphical acceptance remain unverified;effects/audio/icons stay pending.',
            'Nova/Frost Nova/Static/passive acceptance and Inferno held-input/payment/release remain separate unfinished work. No class certificate is inferred.'])
    output=ROOT/'tools/reference/fire-ball-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Fire Ball:185native markers/3boots/2reloads,six real casts,retained areas,four audits/71Python;wall-center gap explicit;no class certificate/deployment.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
