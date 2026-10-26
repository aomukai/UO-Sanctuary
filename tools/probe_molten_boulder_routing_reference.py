#!/usr/bin/env python3
"""Compose complete Boulder callbacks with the complete generic hit router."""
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
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_molten_boulder_reference.py'), '--check'],
                   check=True, stdout=subprocess.DEVNULL)
    old = json.loads((ROOT / 'tools/reference/molten-boulder-source-evidence.json').read_text())
    raw = (ROOT / 'tools/reference/d2r-named-equipment/json/missiles.json').read_bytes()
    assert hashlib.sha256(raw).hexdigest() == old['pinnedInputHashes']['missiles']
    rows = json.loads(raw)
    for name, hit, damage, last, explode, kill in [
        ('moltenboulderemerge', 48, 0, 1, 1, 0),
        ('moltenboulder', 47, 14, 0, 1, 1),
        ('moltenboulderfirepath', 0, 0, 0, 0, 0)]:
        row = rows[name]
        assert [int(row.get(k) or 0) for k in ['pSrvHitFunc', 'pSrvDmgFunc', 'LastCollide', 'AlwaysExplode', 'CollideKill']] == [hit, damage, last, explode, kill]
        assert row['CollideType'] == 8
        assert all(not row.get(k) for k in ['ToHit', 'NextHit', 'NextDelay', 'CollideFriend', 'Pierce', 'Explosion'])
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    hashes, blocks = {}, {}

    def routine(relative, name):
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        text = raw.decode()
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match, name
        end = text.index('{', match.start()) + 1
        depth = 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        body = text[match.start():end]
        blocks[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    cpp = (STAGE / 'molten-boulder-source-probe.cpp').read_text().split('void PrintCreates()', 1)[0]
    cpp = cpp.replace('#include <vector>', '#include <vector>\n#include <map>\n#include <iterator>')
    cpp = cpp.replace('uint32_t gdwBitMasks[2]={1,2};', '''
using BOOL=int;constexpr int FALSE=0,D2UnitInvalidGUID=-1;
constexpr int MISSMODE_NOCOLLIDE=0,MISSMODE_PLAYERKILL=1,MISSMODE_MONSTERKILL=2;
constexpr int STATE_JUSTHIT=1,EVENTTYPE_REMOVESTATE=1,UNITEVENT_HITBYMISSILE=1;
enum{MISSILESFLAGINDEX_EXPLOSION=2,MISSILESFLAGINDEX_PIERCE,MISSILESFLAGINDEX_LASTCOLLIDE};
enum{STAT_SKILL_PIERCE,STAT_ITEM_PIERCE,STAT_PIERCE_IDX,STAT_TOHIT,STAT_ITEM_DAMAGETARGETAC,STAT_ARMORCLASS};
uint32_t gdwBitMasks[32];struct D2StatListStrc{};
struct D2MissileDataStrc{int nOwnerType=0,dwOwnerGUID=-1;};''')
    cpp = cpp.replace('struct D2GameStrc{}game;', 'struct D2GameStrc{int dwGameFrame=1000;void* pMemoryPool=nullptr;}game;')
    cpp = cpp.replace('int dwUnitType=UNIT_MISSILE,dwClassId=452,', 'int dwUnitType=UNIT_MISSILE,dwClassId=452,dwUnitId=10,dwMissileMode=8,')
    cpp = cpp.replace('D2SeedStrc pSeed{};}actor', 'D2SeedStrc pSeed{};D2GameStrc* pGame=&game;D2MissileDataStrc md;D2MissileDataStrc* pMissileData=&md;std::map<int,int> stats;}actor')
    cpp = cpp.replace('struct D2MissilesTxt{', 'struct D2MissilesTxt{uint32_t dwMissileFlags=0;int nNextHit=0,nNextDelay=0,nCollideFriend=0,nCollideKill=0,nToHit=0,nAlwaysExplode=1,wSrvHitFunc=47,wSrvDmgFunc=14;')
    # Full source router receives real Boulder callbacks; the services below
    # explicitly record the remaining event, damage-commit and world boundaries.
    cpp += r'''
bool hostile=true,justHit=false;int resets=0,hitEvents=0,direct=0;std::vector<int> directFlags;
D2MissilesTxt* DATATBLS_GetMissilesTxtRecord(int){return &row;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int s,int){return p->stats[s];}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* p,int s,int){return p->stats[s];}
void STATLIST_SetUnitStat(D2UnitStrc* p,int s,int v,int){p->stats[s]=v;}
bool STATES_CheckState(D2UnitStrc*,int){return justHit;}
bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){return hostile;}
bool SUNITDMG_IsHitSuccessful(D2UnitStrc*,D2UnitStrc*,int,int){return true;}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int,int,int){return nullptr;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){}
void STATLIST_SetState(D2StatListStrc*,int){}
void MISSMODE_ToggleStateOff(D2UnitStrc*,int,D2StatListStrc*){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)(D2UnitStrc*,int,D2StatListStrc*)){}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){}
void STATES_ToggleState(D2UnitStrc*,int,int){}
void SUNITEVENT_Trigger(D2GameStrc*,int,D2UnitStrc*,D2UnitStrc*,void*){hitEvents++;}
void MISSMODE_SetDamageFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* d){direct++;directFlags.push_back(d->wResultFlags);}
int MONSTERS_GetHirelingTypeId(D2UnitStrc*){return 0;}
void COLLISION_ResetMaskWithSize(D2ActiveRoomStrc*,int,int,int,unsigned){resets++;}
'''
    common = 'source/D2Common/src/Units/Missile.cpp'
    cpp += routine(common, 'MISSILE_SetOwner') + routine(common, 'MISSILE_CheckUnitIfOwner')
    mode = 'source/D2Game/src/MISSILES/MissMode.cpp'
    router = routine(mode, 'MISSMODE_SrvDmgHitHandler')
    actual = {'MISSMODE_SrvDmgHitHandler', 'MISSMODE_SrvHit47_MoltenBoulder',
              'MISSMODE_SrvHit48_MoltenBoulderEmerge', 'MISSMODE_SrvDmg14_MoltenBoulder'}
    for name in sorted(set(re.findall(r'\bMISSMODE_Srv(?:Hit|Dmg)\w+', router)) - actual):
        damage = 'SrvDmg' in name
        cpp += ('void' if damage else 'int32_t') + ' ' + name + '(D2GameStrc*,D2UnitStrc*,D2UnitStrc*' + (',D2DamageStrc*' if damage else '') + '){assert(false);' + ('' if damage else 'return 0;') + '}\n'
    cpp += router
    cpp += r'''
void Prepare(int kind){Reset(123);actor.pMissileData=&actor.md;row.wSrvHitFunc=kind==0?48:kind==1?47:0;row.wSrvDmgFunc=kind==1?14:0;row.nCollideKill=kind==1;row.nAlwaysExplode=kind!=2;row.wHitSubMissile[0]=kind==0?452:455;row.dwMissileFlags=kind==0?gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE]:0;hostile=true;justHit=false;resets=hitEvents=direct=0;directFlags.clear();}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int kind:{0,1,2})for(int victim:{0,1,2,3})for(int flags:{0,1,2,3})for(int repeat:{0,1})for(int terminal:{0,1})for(int owned:{0,1})for(int friendly:{0,1})for(int success:{0,1}){
 Prepare(kind);target.dwUnitType=victim==2?UNIT_PLAYER:victim==3?4:UNIT_MONSTER;species.dwFlags=flags;
 hasOwner=owned;hostile=!friendly;areaSuccess=success;
 if(repeat){actor.md.nOwnerType=target.dwUnitType;actor.md.dwOwnerGUID=target.dwUnitId;}
 int result=MISSMODE_SrvDmgHitHandler(&game,&actor,victim?&target:nullptr,terminal);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%llu]",first?"":",",kind,victim,flags,repeat,terminal,owned,friendly,success,result,fills,direct,(int)areas.size(),(int)creates.size(),hitEvents,resets,actor.md.nOwnerType,actor.md.dwOwnerGUID,(unsigned long long)actor.pSeed.lSeed);first=false;
}printf("]\n");}
'''
    path = STAGE / 'molten-boulder-routing-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined',
                    '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'molten-boulder-routing-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    assert len(data) == 1536
    for kind, victim, flags, repeat, terminal, owned, friendly, success, result, fills, direct, area, creates, events, resets, last_type, last_id, seed in data:
        rejected = bool(victim and (kind == 0 and repeat or owned and friendly))
        large = victim == 1 and flags & 2
        if rejected:
            assert (result, fills, direct, area, creates, events, resets) == (1, 0, 0, 0, 0, 0, 0)
            continue
        if kind == 0:
            expected = (2, 0, 0, 0, 1, 1, 1)
        elif kind == 1:
            is_area = bool(owned and large)
            is_direct = bool(owned and victim and (not large or not success))
            removal = bool(not owned or is_area or terminal)
            expected = (2 if removal else 1, int(is_area) + int(is_direct), int(is_direct), int(is_area), 18 if is_area else 0, 1, int(removal))
        else:
            expected = (2 if terminal else 1, int(bool(victim)), int(bool(victim)), 0, 0, int(bool(victim or terminal)), int(bool(terminal)))
        assert (result, fills, direct, area, creates, events, resets) == expected, (kind, victim, flags, repeat, terminal, owned, friendly, success, expected, [result, fills, direct, area, creates, events, resets])
    fixture = STAGE / 'molten-boulder-routing-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False,
        legacyCommit=tree['sha'], sourceHashes=hashes, routineHashes=blocks,
        callbackEvidence='tools/reference/molten-boulder-source-evidence.json', callbackHashes=old['routineHashes'],
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(data)),
        established=[
            'Complete generic hit router executes actual emergence48, rolling47/damage14 and no-hit-function ground routes; actual LastCollide identity read/write helpers execute. Other table entries assert if selected.',
            'Emergence hit creates rolling452 and removes the emergence missile, including a terminal null-victim call. Missing owner still records a failed child-factory attempt.',
            'Rolling hits on non-LARGE monsters request direct damage and continue despite CollideKill1, because hit47 replaces the generic result. LARGE monsters request area damage and18ground children; successful area request removes without direct damage, while failed area request also requests direct damage.',
            'Rolling terminal null-victim calls remove without an area explosion or flame disc in this reconstructed source. Wall/expiry animation does not establish blast damage; modern differences remain explicitly unverified.',
            'Only emergence453 has LastCollide. It rejects the immediately recorded same target before event or damage; hostility rejection follows LastCollide write. Rolling452 and ground455 have no LastCollide and can request direct damage on repeated updates. Ground455 also has AlwaysExplode0: an empty nonterminal call returns before the hit event. Terminal removal clears the collision mask.'
        ], limits=[
            'Hit events, packet filling, area search/commit, generic SetDamageFlags, child factory and world room services are recording fixtures. Full damage mitigation/knockback eligibility and modern event/item behavior are not established.',
            'This probe calls the hit router directly; actual path movement, saved-step collider order, generic update lifetime and field animation producer remain separate.',
            'Current pinned data and older reconstructed code are distinguished. Native actor, learned casting, persistence, final effects/audio/icons remain unfinished.'
        ], sanitizers='ASan/UBSan exit0 and empty runtime stderr required')
    out = ROOT / 'tools/reference/molten-boulder-routing-source-evidence.json'
    if check:
        assert json.loads(out.read_text()) == evidence
    else:
        out.write_text(json.dumps(evidence, indent=2) + '\n')
    print(f'Molten Boulder: {len(data)} complete hit-router/callback contexts pass with clean sanitizers.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
