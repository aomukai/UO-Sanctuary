#!/usr/bin/env python3
"""Correct the old vine harness's getter boundary without changing source bodies."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    old_path=ROOT/'tools/reference/creeper-vines-source-evidence.json'
    old=json.loads(old_path.read_text())
    parent=BASE/'creeper-vines-source-probe.cpp'
    assert sha(parent)==old['generatedSourceSha256']
    cpp=parent.read_text()
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
    assert tree['sha']==old['legacyCommit']
    hashes=dict(old['sourceHashes']);routines=dict(old['unchangedRoutineHashes'])
    for relative,digest in hashes.items():assert sha(BASE/'d2moo-pinned-source'/relative)==digest
    def replace(before,after):
        nonlocal cpp
        assert cpp.count(before)==1,before
        cpp=cpp.replace(before,after,1)
    replace('struct D2MissileDataStrc{int nOwnerType=0,dwOwnerGUID=-1;};',
        'struct D2MissileDataStrc{int nOwnerType=0,dwOwnerGUID=-1,nTotalFrames=125,nCurrentFrame=125;};')
    common='source/D2Common/src/Units/Missile.cpp'
    raw=(BASE/'d2moo-pinned-source'/common).read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==common)
    hashes[common]=hashlib.sha256(raw).hexdigest()
    getters=''
    for name in ['MISSILE_GetRemainingFrames','MISSILE_GetTotalFrames']:
        body=function(raw.decode(),name)
        routines[name]=hashlib.sha256(body.encode()).hexdigest();getters+=body+'\n'
    replace('int MISSILE_GetRemainingFrames(D2UnitStrc*p){return p->remaining;}int MISSILE_GetTotalFrames(D2UnitStrc*p){return p->total;}',getters)
    replace('parent.remaining=frame;','parent.md.nTotalFrames=20;parent.md.nCurrentFrame=20-frame;')
    replace('parent.total=total;parent.remaining=total-age;','parent.md.nTotalFrames=total;parent.md.nCurrentFrame=total-age;')
    replace('parent.remaining=parent.total-age;','parent.md.nCurrentFrame=parent.md.nTotalFrames-age;')
    generated=BASE/'creeper-vines-age-source-probe.cpp';generated.write_text(cpp)
    binary=generated.with_suffix('')
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(generated),'-o',str(binary)],check=True)
    process=subprocess.run([str(binary)],capture_output=True)
    sanitizer=BASE/'creeper-vines-age-sanitizer.log';sanitizer.write_bytes(process.stderr)
    assert process.returncode==0 and not process.stderr,process.stderr.decode()[-4000:]
    data=json.loads(process.stdout)
    assert [len(data[k]) for k in ['launch','head','trail','sequence']]==[1785,450,864,6]
    old_data=json.loads((BASE/'creeper-vines-source-probe.json').read_text())
    assert data['launch']==old_data['launch'] and data['head']==old_data['head']
    changed=0
    for row,prior in zip(data['trail'],old_data['trail']):
        total,age,victim,ending,repeat,own,enemy,returned,fill,flags,last,events,resets=row
        rejected=bool(victim and (repeat or own and not enemy))
        active=bool(victim and not rejected and age<total-15)
        assert fill==flags==int(active)
        assert last==(10 if victim or repeat else -1)
        assert events==int(not rejected) and returned==(2 if ending and not rejected else 1)
        assert resets==int(bool(ending and not rejected))
        changed+=row!=prior
    assert changed==102,changed
    assert data['sequence']==[[0,10,1,1,10],[15,10,1,0,10],[16,10,1,0,10],[17,20,1,1,20],[18,10,1,1,10],[19,10,1,0,10]]
    fixture=BASE/'creeper-vines-age-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
    paths=[Path(__file__).resolve(),old_path,parent,BASE/'creeper-vines-source-probe.json',generated,fixture,sanitizer]
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,nativeAccepted=False,
        sourceHashes=hashes,compiledRoutineHashes=routines,artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        contexts=dict(launch=1785,head=450,trail=864,sequence=6,changedTrailOutputs=changed),sanitizerClean=True,
        established=['The old launch/router/do26/hit50 source bodies are retained unchanged. Two complete source getters replace ambiguous scalar stubs; fixture setup supplies total lifetime and current countdown. Launch and head outputs stay identical when the scalar head input is correctly interpreted as elapsed age.',
            'Complete hit50/router now permits initial contact and suppresses the final15frames. Exactly102of864old bounded trail outputs change; LastCollide still updates before damage/hostility checks, repeated-last targets still skip, and null-target terminal routing remains distinct. Initial sequential contact now damages; repeated age15/16contacts still skip because the collider identity was already recorded.'],
        remaining=['Lookup/factory/formula/damage-fill/mitigation/event/room/collision and global queue services retain their declared limits. Total/age controls include expiry/out-of-window values and are not proof such callbacks occur naturally. Correct and compare native phase/contact call sites, saved fields, real poison and connected clocks separately.'])
    path=ROOT/'tools/reference/creeper-vines-age-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Creeper corrected source:1785launch/450head/864router/6history; complete elapsed getters change102old trail controls; ASan/UBSan clean.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
