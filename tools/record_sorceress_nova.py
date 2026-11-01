#!/usr/bin/env python3
"""Bind saved Nova/Frost Nova acceptance and retained Sorceress/Poison Nova scopes."""
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
STAGE=ROOT/'.local/class-skills/sorceress-nova'
BUILD=5
sha=inventory.sha


def historical(path):return subprocess.check_output(['git','show','263c811:'+path],cwd=ROOT)


def record(check=False):
    inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
    counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text()
    assert sum(counts.values())==222 and ' ERR]' not in log and ': failed' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'))
    from check_sorceress_nova import configuration
    config=configuration(True);assert len(config['extra_families'])==20 and len(config['extra_checks'])==6
    for name in config['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
    for name in config['extra_checks']:assert counts[name]==3
    for spell in ('fire-bolt','ice-bolt','ice-blast','glacial-spike','fire-ball','nova','frost-nova'):
        assert counts[spell+'-reload-result']==2
        for mode in ('right','quick'):
            assert counts[spell+'-'+mode+'-check']==counts[spell+'-'+mode+'-result']==3
            assert log.count(' '+spell+'-'+mode+': windup')==3
    for name in ('mage-glacial-live','glacial-player-live'):assert counts[name+'-result']==3
    for key in ('nova','frost-nova'):
        assert key+':765independent rank/hard contexts' in log
        saved=json.loads((STAGE/(key+'-fixtures.json')).read_text())
        assert saved['Generation']==2 and len(saved['Flights'])==64 and saved['Life']<12800000
        assert all(x['Owner']==saved['Owner'] and x['Frame']==6 and x['VelocityFp8']==3608 and x['AccelerationCounter']==1 for x in saved['Flights'])
        assert all(x['ColdFrames']==(200 if key=='frost-nova' else 0) and x['Frames']==(14 if key=='frost-nova' else 13) for x in saved['Flights'])
    full=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json';raw=json.loads(full.read_text())
    rows=[dict(key=key,fields={k:v for k,v in raw[key].items() if isinstance(v,(int,str)) and not k.startswith('*') and k not in ('lineNumber','Missile')}) for key in ('nova','frostnova')]
    data=ROOT/'data/Sanctuary/Balance/necromancer-summons.json'
    assert json.loads(data.read_text())==json.loads(historical(str(data.relative_to(ROOT))))+rows
    status=ROOT/'data/Sanctuary/Balance/class-skill-status.json';now=json.loads(status.read_text());before=json.loads(historical(str(status.relative_to(ROOT))))
    for talent in ('sorceress.nova','sorceress.frostnova'):
        updated=next(x for x in now['skills'] if x['talentId']==talent);old=next(x for x in before['skills'] if x['talentId']==talent)
        assert old['verification']=='build-only' and updated['verification']=='partial-native-coverage'
        old.update({k:updated[k] for k in ('verification','remaining')})
    assert before==now and not now['completedClasses']
    parent=ROOT/'tools/reference/fire-ball-native-evidence.json';acceleration=ROOT/'tools/reference/missile-acceleration-source-evidence.json'
    poison=ROOT/'tools/reference/poison-nova-emitter-source-evidence.json';emitter=ROOT/'tools/reference/sorceress-nova-emitter-source-evidence.json'
    for p in (parent,acceleration,poison):assert p.read_bytes()==historical(str(p.relative_to(ROOT)))
    evidence=json.loads(emitter.read_text());acc=json.loads(acceleration.read_text());fixture=ROOT/acc['fixture']['path']
    assert sha(fixture)==acc['fixture']['sha256'] and acc['fixture']['cases']==1800
    assert evidence['cases']==144 and evidence['validAllocationAttempts']==1536 and evidence['createdMissiles']==768
    for name,digest in {**evidence['inputs'],**evidence['artifacts']}.items():assert sha(ROOT/name)==digest,name
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==evidence['legacyCommit']==acc['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    reviewed={};sources=[]
    for relative,names in (
        ('source/D2Game/src/MISSILES/Missiles.cpp',('MISSILES_CreateMissileFromParams',)),
        ('source/D2Game/src/SKILLS/Skills.cpp',('sub_6FD14170',)),
        ('source/D2Game/src/SKILLS/SkillSor.cpp',('SKILLS_SrvDo022_NovaAttack',)),
        ('source/D2Common/src/Path/PathMisc.cpp',('sub_6FD5CEB0',))):
        p=ROOT/'.local/class-skills/d2moo-pinned-source'/relative;blob=p.read_bytes();sources.append(p)
        assert hashlib.sha1(b'blob '+str(len(blob)).encode()+b'\0'+blob).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        for name in names:reviewed[name]=hashlib.sha256(function(blob.decode(),name).encode()).hexdigest()
    for name,digest in evidence['completeRoutineHashes'].items():assert reviewed[name]==digest
    assert reviewed['sub_6FD5CEB0']==acc['compiledFunctionHash']
    prior=json.loads((ROOT/'.local/class-skills/sorceress-fire-ball/birth-inputs.json').read_text())
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
    assert '144 complete launch/ring/factory contexts,1536 attempts/768 children' in (STAGE/'emitter-recheck.log').read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    files=[Path(__file__).resolve(),ROOT/'tests/check_sorceress_nova.py',ROOT/'tools/probe_sorceress_nova_emitter.py',ROOT/'tools/build_class_skills.py',ROOT/'tools/build_skill_roadmap.py',
        ROOT/'tools/review_weapon_event_order.py',ROOT/'.local/class-skills/d2moo-source-tree.json',full,data,parent,emitter,acceleration,poison,fixture,*sources,
        *(ROOT/'docs'/name for name in ('sorceress-nova-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','nova-fixtures.json','frost-nova-fixtures.json','poison-nova-fixtures.json',
            'emitter-recheck.log','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
        *(ROOT/'tools/reference'/name for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
    result=dict(referenceVersion='3.3.93847',component='Saved player Nova/Frost Nova64ray paths',installed=False,graphicalClientVerified=False,completedClasses=[],
        verified=dict(nativeMarkers=222,boots=3,actualReloads=2,saves=3,independentRankHardContextsPerSkill=765,sourceFactoryShotsPerSkill=384,sharedAccelerationContextsPerSkill=1800,
            novaFrostRightQuickActions=12,retainedRightQuickActions=30,novaFrostPostLoadRings=4,fixtureFamilies=20,sharedChecks=6,auditMarkers=4,auditFingerprints=sizes,pythonTests=71,unchangedInstalledArtifacts=106),
        legacySource=dict(commit=tree['sha'],completeFunctions=reviewed,emitterCases=144,allocationAttempts=1536,createdRays=768,accelerationCases=1800,
            scope='Complete old do22/emitter/factory and acceleration execution with declared services;separate native source comparisons and mechanical/save/input tests. Modern executable parity is unverified.'),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['Nova/Frost Nova use64saved rays,source integer directions,4608FP8launch velocity,-1000deceleration every fifth active update,13/14frames and non-killing contact with LastCollision/cast-local160ms leases.',
            'Captured damage/rank/gear/mastery/independent damage slow,CanSlow0,cold length/typed potion/resistance/immunity,mana/town/windup/lifecycle and atomic restore pass. Frost chills rather than freezes;lightning potion cold additions create no cold length.',
            'Twelve actual inputs and four actual resumed partial-ring controls retain held wounds,mana,bindings and all64partial flight records. Accepted Sorceress and Poison Nova tests remain present. Existing martial velocity wrapper delegates the same source-compared kernel.'],
        remaining=['Cast-local NextHit identity,initial factory dispatch/seed/critical/event order,native terrain/room/path/collider/filter/recipient order and special/PvP behavior remain host adapters or reference gaps;no modern executable parity is claimed.',
            'Harmless restart/offline aging/cancellation retain existing host policy. Actual old-DLL world upgrade/unheld restart,ordinary cold shatter and graphical acceptance remain open. Effects/audio/tree/hotkey icons are separately pending.',
            'Static/passive/lightning lifecycle acceptance and Inferno held-input/payment/release remain separate unfinished work;the shared explosive wall-centre discrepancy is still open. No class certificate or installation is inferred.'])
    output=ROOT/'tools/reference/sorceress-nova-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Nova/Frost Nova:222native markers/3boots/2reloads,12real casts/4resumed rings,retained Sorceress/Poison Nova,four audits/71Python;no class certificate/deployment.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
