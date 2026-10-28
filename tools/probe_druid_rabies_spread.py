#!/usr/bin/env python3
"""Execute complete older Rabies emitter/marker/child routines with declared services."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    pinpath = ROOT / 'tools/reference/druid-rabies-preflight.json'
    pin = json.loads(pinpath.read_text())
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == pin['legacyCommit']
    hashes, routines = {}, {}

    def routine(relative, name):
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = sha(path)
        body = function(raw.decode(), name)
        assert hashlib.sha256(body.encode()).hexdigest() == pin['completeRoutineHashes'][name]
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    row = pin['currentRows']['skills']['Rabies']
    assert [row[key] for key in ['ELen', 'ELevLen1', 'ELevLen2', 'ELevLen3']] == ['100', '10', '10', '10']
    cpp = r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <initializer_list>
#include <vector>
#define __fastcall
constexpr int EVENTTYPE_REMOVESTATE=10;
struct Seed{int owner=0;};
struct D2UnitStrc{int dwUnitType=0,dwUnitId=0,dwClassId=0,skill=238,level=1,targetX=0,targetY=0,current=10,remaining=0,x=31,y=47;bool dead=false;void* pDynamicPath=this;D2UnitStrc* owner=nullptr;Seed pSeed;};
struct D2GameStrc{int dwGameFrame=1000;void* pMemoryPool=nullptr;}game;
struct D2SkillsTxt{int wAuraTargetState=112;}skill;
struct D2MissilesTxt{int16_t wSubMissile[1]={516};int dwParam[2]={4,7};}missile;
struct Tables{int nStatesTxtRecordCount=200;}tables;auto* sgptDataTables=&tables;
struct D2DamageStrc{int dwPoisDamage=678,dwPoisLen=99;};
struct D2StatListStrc{int state=112,expires=0,type=1,id=3;};
struct D2MissileStrc{int nMissile=0,nRange=0,dwFlags=0,nSkill=0,nSkillLevel=0,nTargetX=0,nTargetY=0;D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;};
D2UnitStrc caster,host,recipient,emitter,child,plague;D2StatListStrc hostState,targetState;D2MissileStrc childArgs,plagueArgs;
int scene=0,rollX=0,rollY=0,draws=0,moves=0,collisions=0,retirements=0,childAttempts=0,childCreated=0,plagueAttempts=0,plagueCreated=0,allocations=0,posts=0,stats=0,eventFrame=0;
bool targetMarker=false;std::vector<int> trace;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){assert(id==238);return scene==5?nullptr:&skill;}
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int id){assert(id==515);return scene==4?nullptr:&missile;}
int MISSILE_GetSkill(D2UnitStrc* m){return m->skill;}int MISSILE_GetLevel(D2UnitStrc* m){return m->level;}
int MISSILE_GetTargetX(D2UnitStrc* m){return m->targetX;}int MISSILE_GetTargetY(D2UnitStrc* m){return m->targetY;}
void MISSILE_SetTargetX(D2UnitStrc* m,int value){m->targetX=value;}void MISSILE_SetTargetY(D2UnitStrc* m,int value){m->targetY=value;}
int MISSILE_GetCurrentFrame(D2UnitStrc* m){return m->current;}int MISSILE_GetRemainingFrames(D2UnitStrc* m){return m->remaining;}
D2UnitStrc* SUNIT_GetServerUnit(D2GameStrc*,int type,int id){assert(type==0&&id==1);return scene==1?nullptr:&caster;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc* m){return m?m->owner:nullptr;}
bool SUNIT_IsDead(D2UnitStrc* m){return m->dead;}
void MISSMODE_SrvDmgHitHandler(D2GameStrc*,D2UnitStrc* m,D2UnitStrc* unit,int value){assert(m==&emitter&&!unit&&value==1);retirements++;trace.push_back(1);}
void* UNITS_GetRoom(D2UnitStrc* m){assert(m==&host);return &host;}
int CLIENTS_GetUnitX(D2UnitStrc* m){return m->x;}int CLIENTS_GetUnitY(D2UnitStrc* m){return m->y;}
void D2Common_10229(void* path,D2UnitStrc* m,void* room,int x,int y){assert(path==emitter.pDynamicPath&&m==&emitter&&room==&host&&x==host.x&&y==host.y);moves++;trace.push_back(2);}
int ITEMS_RollLimitedRandomNumber(Seed* seed,int bound){assert(seed==&caster.pSeed&&bound==15);int value=draws++?rollY:rollX;trace.push_back(3);return value;}
void MISSMODE_HandleMissileCollision(D2GameStrc*,D2UnitStrc* m){assert(m==&emitter);collisions++;trace.push_back(5);}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc* unit,int state){assert(state==112);if(unit==&host)return scene==8?nullptr:&hostState;if(unit==&recipient)return targetMarker?&targetState:nullptr;std::abort();}
int D2COMMON_10473(D2StatListStrc* list){return list->expires;}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned flags,int expires,int type,int id){assert(flags==2&&type==1&&id==3);allocations++;trace.push_back(6);if(scene==12)return nullptr;targetState={112,expires,type,id};return &targetState;}
void STATLIST_SetState(D2StatListStrc* list,int state){assert(list==&targetState&&state==112);list->state=state;}
void sub_6FD10E50(){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)()){}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc* unit,D2StatListStrc* list,int flag){assert(unit==&recipient&&list==&targetState&&flag==1);targetMarker=true;posts++;trace.push_back(7);}
void STATES_ToggleState(D2UnitStrc* unit,int state,int flag){assert(unit==&recipient&&state==112&&flag==1);}
void D2COMMON_10476(D2StatListStrc* list,int frame){assert(list==&targetState);list->expires=frame;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc* unit,int type,int frame,int a,int b){assert(unit==&recipient&&type==EVENTTYPE_REMOVESTATE&&a==0&&b==0);eventFrame=frame;trace.push_back(8);}
void sub_6FCFE0E0(D2UnitStrc* source,D2StatListStrc* list,D2SkillsTxt* row,int id,int level){assert(source==&caster&&list==&targetState&&row==&skill&&id==238&&level==emitter.level);stats++;trace.push_back(9);}
int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc* unit,int id){assert(unit==&caster&&id==238);return scene==13?0:515;}
int SKILLS_GetElementalLength(D2UnitStrc* unit,int id,int level,int mode){assert(unit==&caster&&id==238&&mode==1);return 100+10*(level-1);}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){
 if(p->nMissile==516){assert(p->dwFlags==2&&p->pOwner==&caster&&p->pOrigin==&emitter&&p->nSkill==238&&p->nSkillLevel==emitter.level);childArgs=*p;childAttempts++;trace.push_back(4);if(scene==9)return nullptr;childCreated++;child={};child.owner=&caster;child.level=p->nSkillLevel;return &child;}
 assert(p->nMissile==515&&p->dwFlags==32768&&p->pOwner==&recipient&&p->pOrigin==&recipient&&p->nSkill==238&&p->nSkillLevel==emitter.level);plagueArgs=*p;plagueAttempts++;trace.push_back(10);if(scene==14)return nullptr;plagueCreated++;plague={};plague.owner=&recipient;plague.level=p->nSkillLevel;return &plague;
}
'''
    cpp += routine('source/D2Game/src/SKILLS/SkillDruid.cpp', 'sub_6FCFEDD0')
    for name in ['MISSMODE_SrvDo30_RabiesPlague', 'MISSMODE_SrvHit53_RabiesContagion', 'MISSMODE_SrvDmg11_RabiesContagion']:
        cpp += routine('source/D2Game/src/MISSILES/MissMode.cpp', name)
    cpp += r'''
int main(){bool first=true;printf("[");
 for(int rank:{1,3,20,28,60,255})for(int remain:{-1,0,1,9,10,100+10*(rank-1)-1,100+10*(rank-1),100+10*(rank-1)+1})
 for(int pulse:{0,1,3,4,5,7,8})for(int rx:{0,7,14})for(int ry:{0,7,14})for(int mode=0;mode<17;mode++){
 scene=mode;caster={};caster.dwUnitType=0;caster.dwUnitId=1;caster.pSeed.owner=1;caster.dead=scene==10;
 host={};host.dwUnitType=1;host.dwUnitId=2;host.dead=scene==2;recipient={};recipient.dwUnitType=1;recipient.dwUnitId=3;
 emitter={};emitter.dwUnitType=3;emitter.dwUnitId=4;emitter.dwClassId=515;emitter.owner=scene==7?nullptr:&host;emitter.level=rank;emitter.remaining=pulse;emitter.current=scene==3?-1:10;emitter.targetX=0;emitter.targetY=1;emitter.pDynamicPath=&emitter;if(scene==15)emitter.pDynamicPath=nullptr;
 child={};plague={};childArgs={};plagueArgs={};skill={};missile={};if(scene==6)missile.wSubMissile[0]=-1;
 hostState={112,1000+remain,1,2};targetMarker=scene==11;targetState={112,6000,1,3};
 rollX=rx;rollY=ry;draws=moves=collisions=retirements=childAttempts=childCreated=plagueAttempts=plagueCreated=allocations=posts=stats=eventFrame=0;trace.clear();
 int effect=MISSMODE_SrvDo30_RabiesPlague(&game,scene==16?nullptr:&emitter);int childDeadline=child.targetX;int hit=-1;D2DamageStrc damage;
 if(childCreated){hit=MISSMODE_SrvHit53_RabiesContagion(&game,&child,&recipient);MISSMODE_SrvDmg11_RabiesContagion(&game,&child,&recipient,&damage);}
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",rank,remain,pulse,rx,ry,scene,effect,moves,collisions,retirements,draws,childAttempts,childCreated,childArgs.nTargetX,childArgs.nTargetY,childDeadline,hit,damage.dwPoisDamage,damage.dwPoisLen,allocations,posts,stats,eventFrame,int(targetMarker),targetState.expires,plagueAttempts,plagueCreated,plagueArgs.nRange,plague.targetX,plague.targetY);first=false;
 for(size_t i=0;i<trace.size();i++)printf("%s%d",i?",":"",trace[i]);printf("]]");
 }printf("]\n");}
'''
    generated = BASE / 'druid-rabies-spread-source-probe.cpp'
    generated.write_text(cpp)
    compiler = BASE / 'druid-rabies-spread-compiler.log'
    with compiler.open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(generated.with_suffix(''))], stdout=log, stderr=subprocess.STDOUT, check=True)
    process = subprocess.run([str(generated.with_suffix(''))], capture_output=True)
    sanitizer = BASE / 'druid-rabies-spread-sanitizer.log'
    sanitizer.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-2000:]
    cases = json.loads(process.stdout)
    assert len(cases) == 51408
    for case in cases:
        rank, remaining, pulse, rx, ry, scene, effect, moves, collisions, retires, draws, attempts, created, x, y, deadline, hit, rate, length, alloc, posts, stats, event, marked, expiry, plague_attempts, plague_created, plague_range, source_type, source_id, trace = case
        early = scene in [4, 5, 6, 16]
        retired = not early and scene in [1, 2, 3, 7]
        active = not early and not retired
        spawn = active and pulse % 4 == 0
        child = spawn and scene != 9
        assert (effect, moves, collisions, retires, draws, attempts, created) == (1 if active else 2, int(active and scene != 15), int(active), int(retired), 2 if spawn else 0, int(spawn), int(child))
        assert (x, y) == ((rx - 7, ry - 7) if spawn else (0, 0))
        expected_deadline = 1000 + remaining if child and scene != 8 else 0
        assert deadline == expected_deadline
        valid = child and 10 <= deadline - 1000 <= 100 + 10 * (rank - 1)
        new_marker = valid and scene != 11
        assert hit == ((2 if valid else 1) if child else -1)
        assert (rate, length) == ((678, deadline - 1000) if valid else (0, 0) if child else (678, 99))
        posted = new_marker and scene != 12
        assert (alloc, posts, stats, event, marked, expiry) == (int(new_marker), int(posted), int(posted), deadline if posted else 0, int(posted or scene == 11), deadline if posted else 6000)
        plague_attempt = posted and scene != 13
        plague_success = plague_attempt and scene != 14
        assert (plague_attempts, plague_created, plague_range, source_type, source_id) == (int(plague_attempt), int(plague_success), deadline - 1000 if plague_attempt else 0, 0, 1 if plague_success else 0)
        expected_trace = ([1] if retired else [])
        if active:
            if scene != 15:
                expected_trace.append(2)
            if spawn:
                expected_trace += [3, 3, 4]
            expected_trace.append(5)
        if new_marker:
            expected_trace.append(6)
        if posted:
            expected_trace += [7, 8, 9]
        if plague_attempt:
            expected_trace.append(10)
        assert trace == expected_trace, (case, expected_trace)
    fixture = BASE / 'druid-rabies-spread-source-probe.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', sourcePreparation=True, nativeAccepted=False, currentExecutableParity=False,
        legacyCommit=tree['sha'], preflightSha256=sha(pinpath), contexts=len(cases), completeRoutineHashes=routines, sourceHashes=hashes,
        generatedSourceSha256=sha(generated), compilerLogSha256=sha(compiler), sanitizerLogSha256=sha(sanitizer),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture)),
        columns=['rank', 'remainingInfectionFrames', 'emitterRemainingFrames', 'rngX', 'rngY', 'scene', 'do', 'hostMoves', 'collisionCalls', 'retirements', 'rngCalls', 'childAttempts', 'childCreated', 'aimOffsetX', 'aimOffsetY', 'childDeadline', 'hit', 'poisonRate', 'poisonFrames', 'markerAllocations', 'markerPosts', 'auraApplications', 'markerEventFrame', 'targetMarked', 'targetMarkerDeadline', 'plagueAttempts', 'plagueCreated', 'plagueTimeout', 'plagueOriginalType', 'plagueOriginalId', 'orderedServices'],
        established=[
            'Four complete older routines execute51408bounded contexts under ASan/UBSan: marker/secondary plague creation, host-following emitter30, child hit53 and damage11. Ranks1,3,20,28,60,255and both lifetime boundaries are crossed with seven emitter phases and all nine low/mid/high independent random offsets.',
            'The emitter follows its living host,spawns only on remaining-frame multiples of4,uses the original caster seed for both[-7,7]offsets and assigns that caster as the child owner. It copies the host marker absolute deadline to child targetX. New host markers and secondary emitters inherit only the remaining lifetime; already-marked targets keep their existing marker deadline even while a valid child poison packet retains its original remaining duration.',
            'Missing original caster/host,dead host and negative emitter frame retire; an explicitly dead but present original caster does not. Missing rows/submissile/null emitter are early returns. Failed child/list/plague allocations and missing progressive missile have distinct ordered results; marker application is not rolled back after a missing or failed secondary emitter.',
            'Both hit and damage use the same10<=remaining<=currentLength bound. Outside it, child hit returns1and damage clears poison rate/length. Inside it,hit returns2even if the marker helper skips an existing marker or cannot allocate. Independent poison state/cure/replacement is not collapsed into this marker test.',
        ], declaredServices=[
            'State-list allocation/posting/expiry callbacks, aura application, owner lookup, missile creation, following-path relocation, collision and elemental-length formula are declared services. Event delivery,natural expiry,world movement,flight/footprint and actual poison ticks are not executed. Current aura fields are empty; the aura callback is counted,not a general stat engine. Emitter phase and infection time are independent supplied boundary inputs,not a composed missile-age schedule.',
            'The harness invokes hit53and damage11independently after a declared collision. The complete generic router is statically pinned but not executed here: its hit-result bit2gates damage creation/damage11. Invalid-window damage11calls are isolated zeroing controls,not proof that the real router calls it when hit53returns1. Compose the actual router before native contact acceptance.',
            'Minimal structures and synthetic scene failures are not original ABI or modern reachable-gameplay proofs. RNG supplies chosen legal values without real seed advancement. Newly created missile target storage defaults to0in the declared factory; the missing-host-marker control tests that declared default,not an accepted modern missing-list behavior.',
        ], remaining=[
            'Compose complete Rabies start57/do121,ordinary transformed weapon packet,skill poison packet and initial payment. Verify source min/max/mastery/synergy capture phases,poison creation/state replacement/cure,marker and emitter lifecycles in native world code with actual input and save/reload.',
            'Current executable path/seed/event/geometry,marker-versus-poison dispel behavior,caster death/logout/facet policy,initial miss/block/lost-target/payment and unsupported recipients remain uncertain. Preserve the running Hunger package. No Rabies handler,installation or final asset is enabled by this source probe; effects/icons remain separate.',
        ])
    path = ROOT / 'tools/reference/druid-rabies-spread-engine-evidence.json'
    text = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Rabies spread:51408contexts/4complete routines; caster/host/deadline/marker/child bounds prepared, no native-world claim.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
