#!/usr/bin/env python3
"""Record the Ice Blast freeze-only projectile increment and explicit limits."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import subprocess
import sys

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/sorceress-ice-blast'
BUILD=4
sha=inventory.sha


def historical(path):return subprocess.check_output(['git','show','611032c:'+path],cwd=ROOT)


def record(check=False):
    inventory.STAGE=STAGE
    tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
    counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text()
    assert sum(counts.values())==142 and ' ERR]' not in log and ': failed' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'))
    from check_ice_blast import configuration
    config=configuration(True);assert len(config['extra_families'])==14 and len(config['extra_checks'])==6
    for name in config['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
    for name in config['extra_checks']:assert counts[name]==3
    for spell in ('fire-bolt','ice-bolt','ice-blast'):
        assert counts[spell+'-reload-result']==2
        for mode in ('right','quick'):
            assert counts[spell+'-'+mode+'-check']==counts[spell+'-'+mode+'-result']==3
            assert log.count(spell+'-'+mode+': windup')==3
    assert 'Ice Blast:765independent rank/hard suppliers' in log
    assert log.count('Freeze state:12096 application,1296 removal,100 callback and224 length source/native cases passed')==3
    saved=json.loads((STAGE/'ice-blast-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Control']['Freeze']['Source']==saved['Owner']
    assert saved['Flight']['ColdFrames']==75 and saved['Flight']['VelocityFp8']==2304
    full=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json'
    raw=json.loads(full.read_text())['iceblast']
    expected=dict(key='iceblast',fields={k:v for k,v in raw.items() if isinstance(v,(int,str)) and not k.startswith('*') and k not in ('lineNumber','Missile')})
    summons=ROOT/'data/Sanctuary/Balance/necromancer-summons.json'
    assert json.loads(summons.read_text())==json.loads(historical(str(summons.relative_to(ROOT))))+[expected]
    assert raw['*ID']==91 and raw['pSrvDmgFunc']==4 and 'pSrvHitFunc' not in raw
    status=ROOT/'data/Sanctuary/Balance/class-skill-status.json';now=json.loads(status.read_text());before=json.loads(historical(str(status.relative_to(ROOT))))
    updated=next(x for x in now['skills'] if x['talentId']=='sorceress.iceblast')
    old=next(x for x in before['skills'] if x['talentId']=='sorceress.iceblast')
    assert old['verification']=='build-only' and updated['verification']=='partial-native-coverage'
    old.update({k:updated[k] for k in ('verification','remaining')});assert before==now and not now['completedClasses']
    cpp=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src/MISSILES/MissMode.cpp'
    source_receipt=ROOT/'tools/reference/bone-spear-source-evidence.json'
    source_reference=json.loads(source_receipt.read_text());assert sha(cpp)==source_reference['inputs'][str(cpp.relative_to(ROOT))]
    text=cpp.read_text();start=text.index('void __fastcall MISSMODE_SrvDmg04_IceBlast(');end=text.index('\n}',start)+2
    callback=text[start:end]
    assert callback[callback.index('{'):]=="{\n    pDamage->dwFrzLen = pDamage->dwColdLen;\n    pDamage->dwColdLen = 0;\n}"
    parent=ROOT/'tools/reference/ice-bolt-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
    previous=json.loads((ROOT/'.local/class-skills/sorceress-ice-bolt/birth-inputs.json').read_text())
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][name]==previous['artifacts'][name]
    for kind in ('content','integration'):
        build=(STAGE/f'{kind}-build-{BUILD}.log').read_text()
        assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
    audited=inventory.validate_inventory(STAGE/'audit-inputs.json')
    assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text()
    sizes=[]
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
    for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):
        assert message in checks
    assert 'Traceback' not in checks
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    files=[Path(__file__).resolve(),ROOT/'tests/check_ice_blast.py',ROOT/'tools/build_class_skills.py',ROOT/'tools/build_skill_roadmap.py',full,cpp,source_receipt,parent,
        *(ROOT/'docs'/name for name in ('sorceress-ice-blast-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','ice-blast-fixtures.json',
            'audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
        *(ROOT/'tools/reference'/name for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
    result=dict(referenceVersion='3.3.93847',component='Player Ice Blast saved freeze-only projectile',installed=False,graphicalClientVerified=False,completedClasses=[],
        verified=dict(nativeMarkers=142,boots=3,actualReloads=2,saves=3,independentRankHardSuppliers=765,iceBlastRightQuickActions=6,retainedFireIceRightQuickActions=12,
            iceBlastPostLoadFlights=2,fixtureFamilies=14,sharedChecks=6,auditMarkers=4,auditFingerprints=sizes,pythonTests=71,unchangedInstalledArtifacts=106),
        sourceDmg4Review=dict(scope='Complete unchanged older callback,static review only;shared freeze execution is separate.',
            legacyCommit=source_reference['legacyCommit'],sha256=hashlib.sha256(callback.encode()).hexdigest(),modernExecutableVerified=False),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['Ice Blast reuses saved player flight with pinned damage callback4,length converted to freeze without duplicate chill,single contact/no splash,captured rank/gear/hard synergies and ordinary fractional mana.',
            'Normal/Nightmare/Hell,resistance/immunity/typed potion,actual Nightwing/hard Glacial,length/thaw,three elite cold fallbacks,Slow Missiles and lifecycle controls pass.',
            'A lethal projectile tags its corpse shattered/unusable by corpse skills. Controlled class-drop loot survives the rest of death and normal pickup. Native UO inventory suppression is preserved.',
            'Six actual right/quick casts and two real post-load travelling freeze hits retain exact original held flight,wounds,freeze source/deadline,mana/bindings. All prior Fire/Ice/Sorceress scopes and complete shared freeze-state checks remain in the same run.'],
        remaining=['Exact modern executable/first dispatch/creator RNG,room/terrain/candidate order/unmapped footprint,late-gear/pierce and owner-removal timing remain open;harmless offline/restart aging/cancellation are existing host adapters.',
            'Unmapped uninterruptible/hireling/player recipients,PvP and full event chronology,actual old-DLL upgrade/unheld mid-flight restart and graphical acceptance remain unverified. Ordinary Ice Bolt chill shatter is separate from confirmed freeze tagging.',
            'Glacial Spike requires its distinct hit13 explosion/per-recipient freeze contract. Other Sorceress/passive scopes remain partial;Inferno held-input/payment/release stays individually deferred. Effects/audio/icons remain separately pending;no class certificate.'])
    output=ROOT/'tools/reference/ice-blast-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Ice Blast:142native markers/3boots/2reloads,six real freeze casts,four audits/71Python;no class certificate or deployment.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
