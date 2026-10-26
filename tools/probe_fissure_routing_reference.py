#!/usr/bin/env python3
"""Execute full legacy Fissure hit routing; keep modern hit-delay identity open."""
import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_molten_boulder_routing_reference.py'), '--check'], check=True, stdout=subprocess.DEVNULL)
    parent = json.loads((ROOT / 'tools/reference/molten-boulder-routing-source-evidence.json').read_text())
    raw = (ROOT / 'tools/reference/d2r-named-equipment/json/missiles.json').read_bytes()
    manifest = json.loads((ROOT / 'tools/reference/d2r-named-equipment/sources.json').read_text())
    assert hashlib.sha256(raw).hexdigest() == manifest['files']['json/missiles.json']['sha256']
    rows = json.loads(raw)
    for name in ['erruption crack 1', 'erruption crack 2']:
        row = rows[name]
        assert [int(row.get(k) or 0) for k in ['pSrvHitFunc', 'pSrvDmgFunc', 'LastCollide', 'ToHit', 'CollideKill', 'NextHit', 'NextDelay', 'AlwaysExplode', 'CollideType']] == [0, 8, 1, 0, 0, 1, 5, 0, 3]
        assert not any(row.get(k) for k in ['CollideFriend', 'Pierce', 'Explosion'])
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    hashes, bodies = {}, {}

    def routine(relative, name):
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(v['sha'] for v in tree['tree'] if v['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        text = raw.decode()
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match, name
        end, depth = text.index('{', match.start()) + 1, 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        body = text[match.start():end]
        bodies[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    base = (STAGE / 'molten-boulder-routing-source-probe.cpp').read_text().split('void Prepare(', 1)[0]

    def replace(before, after):
        nonlocal base
        assert base.count(before) == 1, before
        base = base.replace(before, after, 1)

    replace('int dwUnitType=UNIT_MISSILE,dwClassId=452,', 'int dwFlags=24;int dwUnitType=UNIT_MISSILE,dwClassId=452,')
    replace('wSrvHitFunc=47,wSrvDmgFunc=14;', 'wSrvHitFunc=47,wSrvDmgFunc=14,wProgOverlay=32;')
    replace('void MISSMODE_SrvDmg08_EruptionCrack(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){assert(false);}',
            'int overlays=0;void UNITS_SetOverlay(D2UnitStrc* p,int n,int flag){assert(p==&target&&n==32&&flag==0);overlays++;}\n'
            + routine('source/D2Game/src/MISSILES/MissMode.cpp', 'MISSMODE_SrvDmg08_EruptionCrack'))
    replace('D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int,int,int){return nullptr;}',
            'D2StatListStrc stat;bool stateAllocated=true;int stateAllocs=0,stateDelay=0,stateToggles=0,stateEvents=0;D2StatListStrc* STATLIST_AllocStatList(void*,unsigned flags,int delay,int,int){assert(flags==2);stateAllocs++;stateDelay=delay;return stateAllocated?&stat:nullptr;}')
    replace('void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){}',
            'void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int event,int delay,int,int){assert(event==EVENTTYPE_REMOVESTATE&&delay==1005);stateEvents++;}')
    replace('void STATES_ToggleState(D2UnitStrc*,int,int){}',
            'void STATES_ToggleState(D2UnitStrc*,int state,int value){assert(state==STATE_JUSTHIT&&value==1);stateToggles++;}')
    base += r'''
constexpr int UNITFLAG_ISVALIDTARGET=8,UNITFLAG_CANBEATTACKED=16;
struct D2MissileUnitFindArgStrc{D2GameStrc* pGame;D2UnitStrc* pMissile;D2UnitStrc* pOwner;D2MissilesTxt* pMissilesTxtRecord;};
'''
    base += routine('source/D2Game/src/MISSILES/MissMode.cpp', 'MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster')
    base += r'''
void Prepare(){Reset(123);actor.pMissileData=&actor.md;actor.dwMissileMode=3;actor.dwClassId=462;actor.dwUnitId=9;target.dwUnitId=10;target.dwUnitType=UNIT_MONSTER;
 row.wSrvHitFunc=0;row.wSrvDmgFunc=8;row.nNextHit=1;row.nNextDelay=5;row.nCollideKill=0;row.nAlwaysExplode=0;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE];
 hostile=true;justHit=false;resets=hitEvents=direct=overlays=stateAllocs=stateDelay=stateToggles=stateEvents=0;directFlags.clear();stateAllocated=true;}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("{\"route\":[");
for(int victim:{0,1,2})for(bool repeat:{false,true})for(bool nhd:{false,true})for(bool friendly:{false,true})for(bool owned:{false,true})for(bool terminal:{false,true})for(bool allocation:{false,true}){
 Prepare();hasOwner=owned;hostile=!friendly;justHit=nhd;stateAllocated=allocation;target.dwUnitType=victim==2?UNIT_PLAYER:UNIT_MONSTER;if(repeat){actor.md.nOwnerType=target.dwUnitType;actor.md.dwOwnerGUID=target.dwUnitId;}
 int ret=MISSMODE_SrvDmgHitHandler(&game,&actor,victim?&target:nullptr,terminal);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",victim,repeat,nhd,friendly,owned,terminal,allocation,ret,fills,direct,overlays,hitEvents,resets,stateAllocs,stateDelay,stateToggles,stateEvents,actor.md.nOwnerType,actor.md.dwOwnerGUID,(int)creates.size());first=false;
}printf("],\"eligibility\":[");first=true;
for(int type:{0,1,3,4})for(int flags:{0,8,16,24})for(bool repeat:{false,true})for(bool nhd:{false,true})for(bool friendly:{false,true})for(bool owned:{false,true}){
 Prepare();hasOwner=owned;hostile=!friendly;justHit=nhd;target.dwUnitType=type;target.dwFlags=flags;if(repeat){actor.md.nOwnerType=type;actor.md.dwOwnerGUID=target.dwUnitId;}
 D2MissileUnitFindArgStrc arg{&game,&actor,owned?&owner:nullptr,&row};int ret=MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster(&target,&arg);
 printf("%s[%d,%d,%d,%d,%d,%d,%d]",first?"":",",type,flags,repeat,nhd,friendly,owned,ret);first=false;
}printf("],\"history\":[");Prepare();first=true;
// Explicitly clear the external NHD service each time to isolate LastCollide.
for(int id:{10,10,20,10,10}){justHit=false;target.dwUnitId=id;int before=direct;int ret=MISSMODE_SrvDmgHitHandler(&game,&actor,&target,0);
 printf("%s[%d,%d,%d,%d]",first?"":",",id,ret,direct-before,actor.md.dwOwnerGUID);first=false;
}printf("]}\n");}
'''
    path = STAGE / 'fissure-routing-source-probe.cpp'
    path.write_text(base)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'fissure-routing-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    assert {k: len(v) for k, v in data.items()} == dict(route=192, eligibility=256, history=5)
    for victim, repeat, nhd, friendly, owned, terminal, allocation, ret, fills, direct, overlays, events, resets, allocations, delay, toggles, state_events, last_type, last_id, creates in data['route']:
        rejected = victim != 0 and (repeat or nhd or owned and friendly)
        active = victim != 0 and not rejected
        assert (ret, fills, direct, overlays, events, resets, allocations, delay, toggles, state_events, creates) == (
            2 if terminal and not rejected else 1, int(active), int(active), int(active), int(active or terminal and not rejected),
            int(terminal and not rejected), int(active), 1005 if active else 0, int(active and allocation), int(active and allocation), 0)
        if victim and not (repeat or nhd):
            assert (last_type, last_id) == (0 if victim == 2 else 1, 10)
    for unit_type, flags, repeat, nhd, friendly, owned, ret in data['eligibility']:
        assert ret == int(unit_type in (0, 1) and flags == 24 and not repeat and not nhd and not (owned and friendly))
    assert data['history'] == [[10, 1, 1, 10], [10, 1, 0, 10], [20, 1, 1, 20], [10, 1, 1, 10], [10, 1, 0, 10]]
    fixture = STAGE / 'fissure-routing-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
                    sourceHashes={**parent['sourceHashes'], **hashes}, routineHashes={**parent['routineHashes'], **bodies},
                    reusedRouterEvidence='tools/reference/molten-boulder-routing-source-evidence.json', pinnedMissileHash=hashlib.sha256(raw).hexdigest(),
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=453),
                    sanitizers='ASan/UBSan exit0 and empty runtime stderr',
                    established=[
                        'Full generic legacy router runs current crack no-hit-function/damage8, LastCollide1, ToHit0, CollideKill0, NextHit1/NextDelay5 routes, with complete LastCollide getter/setter and unit eligibility callback.',
                        'Immediate same-last-target and externally supplied next-hit exclusion return before last-target writes/events/damage. Hostility rejection follows the LastCollide write. Alternating valid victims can replace the last target, allowing the earlier victim again when external NHD permits.',
                        'Eligible hits attempt a legacy removal-state allocation at gameFrame+5 before hit event, overlay and recorded damage commitment. Allocation failure skips state installation but does not cancel that hit. This is legacy order evidence, not a modern key implementation.',
                        'No AR roll is selected; no child missile is created. Eligible direct hits persist, while a terminal update clears the field footprint and removes the missile. Empty nonterminal calls do not emit hit events.'
                    ], limits=[
                        'Legacy STATE_JUSTHIT is a controlled eligibility service. The known official2.7revision means this global legacy state must not be ported as the modern Fissure identity. Exact modern crack/cast/owner/skill grouping and timing remain open.',
                        'Packet fill, damage flags/mitigation/health, state/event allocation, room/collision and owner lookup are explicit services. Spatial collider order, actual generic update/path, modern event/proc and native learned casting do not execute here.',
                        'Current crack routing values are supplied from the pinned preflight; current data and older reconstruction are distinct. No native handler or final effects/icons are enabled.'
                    ], next='Connect source-backed crack last-victim/eligibility and damage consumers while preserving modern next-hit uncertainty; finish native placement/lifetimes/persistence and casting acceptance.')
    out = ROOT / 'tools/reference/fissure-routing-source-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print('453complete legacy Fissure routing/eligibility/history contexts pass; modern NHD identity remains open.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
