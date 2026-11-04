#!/usr/bin/env python3
"""Compose unchanged old start27, temporary-stat posting and mode cleanup bodies.

This is an older-engine control, not a current D2R executable oracle. Allocation,
stat ownership and animation refresh are explicit services, not copied engines.
"""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    groups = {
        'source/D2Game/src/SKILLS/SkillAss.cpp': ['SKILLS_SrvSt27_DragonTail'],
        'source/D2Game/src/SKILLS/Skills.cpp': ['sub_6FD15470'],
        'source/D2Common/src/D2StatList.cpp': ['STATLIST_DeactivateTemporaryStates'],
        'source/D2Common/src/Units/Units.cpp': ['UNITS_ChangeAnimMode'],
    }
    blocks, hashes = {}, {}
    for rel, names in groups.items():
        raw = (STAGE / 'd2moo-pinned-source' / rel).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == rel)
        hashes[rel] = hashlib.sha256(raw).hexdigest()
        text = raw.decode()
        for name in names:
            match = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
            assert match
            end, depth = text.index('{', match.start()) + 1, 1
            while depth:
                depth += (text[end] == '{') - (text[end] == '}')
                end += 1
            blocks[name] = text[match.start():end]
    cpp = r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <vector>
#define __fastcall
#define __stdcall
using BOOL=int;
constexpr int TRUE=1,FALSE=0,UNIT_TILE=5,UNIT_MONSTER=1,MONMODE_NEUTRAL=0;
constexpr int STATLIST_TEMPONLY=4,STATLIST_NEWLENGTH=0x80000000,UNITFLAG_DOUPDATE=1;
constexpr int STAT_ATTACKRATE=1,STAT_PROGRESSIVE_TOHIT=2,DAMAGERESULTFLAG_SUCCESSFULHIT=1;
struct D2StatListStrc {uint32_t dwFlags=0; int dwStateNo=0,rate=0; D2StatListStrc* pPrevLink=nullptr; D2StatListStrc* pMyLastList=nullptr;};
struct D2UnitStrc {int dwUnitId=7,dwUnitType=0,dwAnimMode=12; uint32_t dwFlags=0; D2StatListStrc* pStatListEx=nullptr;};
struct D2GameStrc {};
struct D2DamageStrc {uint16_t wResultFlags=0;};
struct D2SkillsTxt {int dwParam[4]={50,20,6,-40}; uint8_t nSrcDam=0;};
D2UnitStrc owner,target; D2StatListStrc root; D2SkillsTxt row;
int allocationFails=0,hasRow=1,hasTarget=1,hit=1,freed=0,toggled=0,refresh=0,anim=0,rateUpdates=0,damageAlloc=0,rateSeen=0;
std::vector<D2StatListStrc*> allocated;
D2StatListStrc* STATLIST_AllocStatList(int,uint32_t flags,int,int kind,int id) {
 if(kind!=0 || id!=7)std::abort(); if(allocationFails)return nullptr;
 auto p=new D2StatListStrc; p->dwFlags=flags; allocated.push_back(p); return p;
}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc* unit,D2StatListStrc* list,int enabled) {
 if(unit!=&owner || enabled!=1)std::abort();
 list->pPrevLink=root.pMyLastList;root.pMyLastList=list;root.dwFlags|=STATLIST_NEWLENGTH;
}
void STATLIST_SetStatIfListIsValid(D2StatListStrc* p,int stat,int value,int layer) {if(stat!=STAT_ATTACKRATE || layer)std::abort();p->rate=value;}
int total(){int n=0;for(auto p=root.pMyLastList;p;p=p->pPrevLink)n+=p->rate;return n;}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){rateUpdates++;rateSeen=total();}
void STATES_ToggleState(D2UnitStrc*,int state,int enabled){if(state!=9 || enabled)std::abort();toggled++;}
bool STATLIST_IsExtended(D2StatListStrc* p){return false;}
void D2Common_STATLIST_FreeStatListImpl_6FDB7050(D2StatListStrc* p){
 auto link=&root.pMyLastList;while(*link && *link!=p)link=&(*link)->pPrevLink;
 if(*link!=p)std::abort();*link=p->pPrevLink;freed++;
}
void UNITROOM_RefreshUnit(D2UnitStrc*){refresh++;}
void UNITS_SetAnimStartFrame(D2UnitStrc*){anim++;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){if(id!=270)std::abort();return hasRow?&row:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return hasTarget?&target:nullptr;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int){if(stat!=STAT_PROGRESSIVE_TOHIT)std::abort();return 75;}
int SKILLS_GetToHitFactor(D2UnitStrc*,int,int){return 20;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int ar,int){if(ar!=95)std::abort();return hit;}
void sub_6FCF7CE0(D2GameStrc*,D2DamageStrc*,D2UnitStrc*,D2UnitStrc*,int,int){}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*,uint8_t src){if(src!=128)std::abort();damageAlloc++;}
'''
    for name in ('sub_6FD15470', 'STATLIST_DeactivateTemporaryStates', 'UNITS_ChangeAnimMode', 'SKILLS_SrvSt27_DragonTail'):
        cpp += '\n' + blocks[name] + '\n'
    cpp += r'''
int main(){printf("[");bool comma=false;D2GameStrc game;
for(int permanent:{0,35})for(int oldTemp:{0,-20})for(int hasState:{0,1})for(int fail:{0,1})
for(int record:{0,1})for(int tgt:{0,1})for(int successful:{0,1})for(int transition:{0,1,2}){
 owner={};root={};root.dwFlags=STATLIST_NEWLENGTH;owner.pStatListEx=&root;allocated.clear();
 D2StatListStrc stable,previous;stable.rate=permanent;root.pMyLastList=&stable;
 if(oldTemp){previous.dwFlags=STATLIST_TEMPONLY;previous.rate=oldTemp;previous.dwStateNo=hasState?9:0;previous.pPrevLink=&stable;root.pMyLastList=&previous;}
 allocationFails=fail;hasRow=record;hasTarget=tgt;hit=successful;freed=toggled=refresh=anim=rateUpdates=damageAlloc=rateSeen=0;
 int started=SKILLS_SrvSt27_DragonTail(&game,&owner,270,1),during=total();
 int same=UNITS_ChangeAnimMode(&owner,12),afterSame=total();
 int changed=transition==0?0:UNITS_ChangeAnimMode(&owner,transition==1?0:7),after=total();
 if(comma)printf(",");comma=true;
 printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",permanent,oldTemp,hasState,fail,record,tgt,successful,transition,started,during,same,afterSame,changed,after,freed,toggled,refresh,anim,rateUpdates,damageAlloc,rateSeen);
 for(auto p:allocated)delete p;
}printf("]\n");}
'''
    path = STAGE / 'dragon-tail-lifetime-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-fsanitize=address,undefined', '-fno-pie', '-no-pie', str(path), '-o', str(binary)], check=True)
    rows = json.loads(subprocess.check_output([str(binary)]))
    assert len(rows) == 384
    for p, old, state, fail, rec, tgt, hit, transition, *actual in rows:
        posted = rec and not fail
        during = p + old - (40 if posted else 0)
        assert actual == [int(bool(rec and tgt and hit)), during, 0, during, int(bool(transition)), p if transition else during,
                          int(bool(old)) + int(bool(posted)) if transition else 0, int(bool(old and state and transition)),
                          2 if transition else 1, int(bool(transition)), int(bool(posted)), int(bool(rec and tgt and hit)), during if posted else 0]
    fixture = path.with_suffix('.json')
    fixture.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', talentId='assassin.dragontail', handlerEnabled=False,
                    legacyCommit=tree['sha'], sourceHashes=hashes,
                    compiledRoutines={k: hashlib.sha256(v.encode()).hexdigest() for k, v in blocks.items()},
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),
                                 cppSha256=hashlib.sha256(path.read_bytes()).hexdigest(), cases=len(rows), sanitizers='ASan+UBSan'),
                    established=[
                        'Complete older start27 posts Param4=-40 before target/hit validation. Missing target and failed accuracy do not remove a successfully allocated modifier.',
                        'Complete posting allocates flags4 (TEMPONLY), posts the list, writes ATTACKRATE and refreshes rate once. Allocation failure posts nothing.',
                        'Complete mode change does not remove temporary lists when the mode number is unchanged. A different mode deactivates every temporary list, toggles an attached state off, preserves permanent stats and resets the animation start frame.',
                        'A skill-local KK clock can carry the -40 adjustment without publishing a persistent owner stat. Cancellation/completion must discard that action clock; unrelated attacks must use their own timing.'
                    ],
                    services=[
                        'Stat allocation, linked-list ownership, ordinary-list free/unlink and state toggling are explicit bounded services. Extended temporary stat lists, missing extended root, tile actors and global stat-collector correctness are outside this fixture.',
                        'Target/hit/kick allocation are declared services. Modern charged hit/consumption/payment follow the separate approved Sanctuary decision, not this old start27 implementation.',
                        'Mode refresh and animation start are counted boundaries. This is not the current executable mode dispatcher, client animation, event queue or native cancellation/input proof.'
                    ],
                    decision='tools/reference/dragon-tail-sanctuary-decision.json',
                    next='Compose the approved modern contact and payment policy with the existing prepared kick packets, charge effects and learned KK action; keep remaining native/event/input/overflow limits explicit.')
    out = ROOT / 'tools/reference/dragon-tail-lifetime-source-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == encoded, 'Dragon Tail lifetime evidence stale'
    else:
        out.write_text(encoded)
    print('Dragon Tail lifetime: 384 complete start/post/mode/temporary-cleanup contexts pass with ASan+UBSan; current executable and native learned integration remain separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
