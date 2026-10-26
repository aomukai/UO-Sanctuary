#!/usr/bin/env python3
"""Execute pinned Twister launch/initializer and legacy stun callback bodies.

Factory allocation, aim lookup and path building are recording services. This
probe establishes source composition, not a learned handler or modern stun rule.
"""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
LEGACY = '0c0bbd1e6bef9181a3da1c046523af622a925416'


def build(check=False):
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == LEGACY
    hashes, bodies = {}, {}
    primary = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((primary / 'sources.json').read_text())
    assert manifest['commit'] == 'fa1286949266498b294c40c2942521a7c0d7207a'
    data, pins = {}, {}
    for name in ['skills', 'missiles', 'skilldesc']:
        raw = (primary / f'json/{name}.json').read_bytes()
        pins[name] = hashlib.sha256(raw).hexdigest()
        assert pins[name] == manifest['files'][f'json/{name}.json']['sha256']
        data[name] = json.loads(raw)
    skill_row = next(r for r in data['skills'].values() if r.get('skill') == 'Twister')
    missile_row = next(r for r in data['missiles'].values() if r.get('Missile') == 'twister')
    desc_row = next(r for r in data['skilldesc'].values() if r.get('skilldesc') == 'twister')
    assert skill_row['srvdofunc'] == 118 and skill_row['Param1'] == 3
    assert missile_row['*ID'] == 477 and missile_row['pSrvDmgFunc'] == 9 and missile_row['Range'] == 50

    def routine(relative, name):
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(
            x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        source = raw.decode()
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', source, re.M)
        assert match, name
        end, depth = source.index('{', match.start()) + 1, 1
        while depth:
            depth += (source[end] == '{') - (source[end] == '}')
            end += 1
        body = source[match.start():end]
        bodies[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    cpp = r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
constexpr int UNITFLAG_SKSRVDOFUNC=0x100,PATHTYPE_CHARGEDBOLT=9;
struct D2GameStrc{}game;
struct D2DynamicPathStrc{int x=0,type=0,distance=0,builds=0;};
struct D2SeedStrc{uint32_t low=0;};
struct D2UnitStrc{int dwFlags=0,dwClassId=477,total=0,current=0;D2DynamicPathStrc* pDynamicPath=nullptr;D2SeedStrc pSeed{};}owner,missile;
struct D2SkillsTxt{int dwCalc[4]{},dwParam[12]{};}skill;
struct D2MissilesTxt{int dwDmgParam[2]{};}row;
struct D2DamageStrc{int dwStunLen=123,dwHitClass=7;}damage;
struct Tables{int nMissilesTxtRecordCount=999;}tables;Tables* sgptDataTables=&tables;
using Init=void(*)(D2UnitStrc*,int32_t);
struct D2MissileStrc{int dwFlags=0;D2UnitStrc* pOwner=nullptr;int nX=0,nY=0,nSkill=0,nSkillLevel=0,nMissile=0,nTargetX=0,nTargetY=0;Init pInitFunc=nullptr;int pInitArgs=0;};
struct Request{D2MissileStrc p;int total,current,seed,type,distance,builds;bool allocated;};
std::vector<Request> requests;
bool validSkill=true,validMissile=true,target=true;int missileId=477,count=3,aimX=300,frames=50,fail=-1;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return validSkill?&skill:nullptr;}
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return validMissile?&row:nullptr;}
int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc*,int){return missileId;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return count;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p?100:0;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p?200:0;}
bool D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc* p,int* x,int* y){if(!target||!p)return false;*x=aimX;*y=400;return true;}
void sub_6FD11420(D2GameStrc*,int,D2UnitStrc*,int,int,int,int,int,int,int){assert(false);}
int MISSILE_GetTotalFrames(D2UnitStrc* p){return p->total;}void MISSILE_SetTotalFrames(D2UnitStrc* p,int n){p->total=n;}
void MISSILE_SetCurrentFrame(D2UnitStrc* p,int n){p->current=n;}
void SEED_InitLowSeed(D2SeedStrc* p,int n){p->low=n;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc* p){return p->x;}
void PATH_SetType(D2DynamicPathStrc* p,int n){p->type=n;}
void PATH_SetNewDistance(D2DynamicPathStrc* p,uint8_t n){p->distance=n;}
void D2Common_10142(D2DynamicPathStrc* p,D2UnitStrc*,int n){assert(n==0);p->builds++;}
int MISSILE_GetSkill(D2UnitStrc*){return 240;}
'''
    cpp += routine('source/D2Game/src/SKILLS/SkillSor.cpp', 'SKILLS_MissileInit_ChargedBolt')
    cpp += r'''
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){
 bool allocated=(int)requests.size()!=fail;D2DynamicPathStrc path{};path.x=p->nTargetX;
 missile={};missile.total=missile.current=frames;missile.pDynamicPath=&path;
 if(allocated){assert(p->pInitFunc);p->pInitFunc(&missile,p->pInitArgs);}
 requests.push_back({*p,missile.total,missile.current,(int)missile.pSeed.low,path.type,path.distance,path.builds,allocated});
 return allocated?&missile:nullptr;}
'''
    cpp += routine('source/D2Game/src/SKILLS/SkillDruid.cpp', 'sub_6FCFE4C0')
    cpp += routine('source/D2Game/src/SKILLS/SkillDruid.cpp', 'SKILLS_SrvDo118_Twister_Tornado')
    cpp += routine('source/D2Game/src/MISSILES/MissMode.cpp', 'MISSMODE_SrvDmg09_Twister')
    cpp += r'''
int main(){bool first=true;printf("[");
 for(int sid:{240,245})for(bool unit:{false,true})for(bool sk:{false,true})for(int mid:{-1,477,999})
 for(int n:{-1,0,1,3,6})for(bool aim:{false,true})for(int f:{-1,0,1,2})for(int x:{100,65500})for(int length:{50,75,78,100}){
 validSkill=sk;missileId=mid;count=n;target=aim;fail=f;aimX=x;frames=length;owner={};requests.clear();
 int result=SKILLS_SrvDo118_Twister_Tornado(&game,unit?&owner:nullptr,sid,20);
 bool valid=sk&&mid>=0&&mid<999;assert(result==(valid&&n>0?1:0));
 assert(owner.dwFlags==(valid&&unit?UNITFLAG_SKSRVDOFUNC:0));
 assert(requests.size()==(size_t)(valid&&n>0&&unit&&aim?n:0));
 printf("%s[0,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",sid,unit,sk,mid,n,aim,f,x,length,result,owner.dwFlags);first=false;
 for(size_t i=0;i<requests.size();i++){auto&r=requests[i];auto&p=r.p;assert(p.dwFlags==33&&p.pOwner==&owner&&p.nX==100&&p.nY==200&&p.nSkill==sid&&p.nSkillLevel==20&&p.nMissile==mid&&p.nTargetX==x&&p.nTargetY==400&&p.pInitArgs==(int)i);
  if(r.allocated){assert(r.total==(length>=78?77:length)&&r.current==r.total&&r.seed==x+(int)i&&r.type==9&&r.distance==r.total&&r.builds==1);}
  printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",i?",":"",p.pInitArgs,r.allocated,r.total,r.current,r.seed,r.type,r.distance,r.builds);}
 printf("]]");}
 for(bool exists:{false,true})for(bool mr:{false,true})for(bool sk:{false,true})for(int param:{-5,0,6})for(int base:{10,20}){
 validMissile=mr;validSkill=sk;row.dwDmgParam[0]=param;skill.dwParam[1]=base;damage={};
 MISSMODE_SrvDmg09_Twister(&game,exists?&missile:nullptr,nullptr,&damage);
 assert(damage.dwStunLen==(exists&&mr?(param>0?param:sk?base:0):123));assert(damage.dwHitClass==(exists&&mr?96:7));
 printf(",[1,%d,%d,%d,%d,%d,%d,%d]",exists,mr,sk,param,base,damage.dwStunLen,damage.dwHitClass);}
 printf("]\n");}
'''
    path = STAGE / 'twister-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'twister-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-2000:]
    rows = json.loads(run.stdout)
    counts = [sum(r[0] == kind for r in rows) for kind in [0, 1]]
    assert counts == [7680, 48], counts
    fixture = STAGE / 'twister-source-probe.json'
    fixture.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', primaryDataCommit=manifest['commit'], pinnedInputHashes=pins,
                    legacyCommit=LEGACY, sourceHashes=hashes, routineHashes=bodies,
                    launchInitializerCases=counts[0], legacyStunCases=counts[1], sanitizers='ASan/UBSan clean',
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),
                    services=['Current row/progressive missile/formula selection and aim lookup are explicit inputs.',
                              'Factory allocation records a request and invokes the complete initializer; full factory, damage capture, rooms and global scheduling are not executed.',
                              'Path target-X, seed-low write, path type/distance and rebuild calls are recording services; actual charged-bolt path generation has separate source/native evidence.'],
                    established=['Complete do118/shared launcher/Charged Bolt initializer compose for both Twister and Tornado; successful rows request calc1 missiles with flags33 and independent ordinals.',
                                 'Null owner or unavailable aim yields no requests but the valid positive-count source path returns success. Allocation failure does not stop later requests.',
                                 'Initializer seeds from target-X plus ordinal, sets the charged-bolt path and caps total/current frames at77 only when initial frames are at least78.',
                                 'Legacy damage9 selects positive dParam1 or raw skill Param2, then sets hit class0x60. It does not evaluate the modern Arctic Blast stun synergy.'],
                    remaining=['No learned Twister/Tornado handler, native flight/collision/damage/stun/input/save acceptance or installation is claimed.',
                               'Use pinned current Twister calc2 and released2.4 Arctic Blast duration synergy instead of silently porting the older callback fallback. Exact modern callback/stun delivery and NHD grouping remain separate.'])
    output = ROOT / 'tools/reference/twister-source-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == text
    else:
        output.write_text(text)
    preflight = dict(referenceVersion='3.3.93847 — Reign of the Warlock', primaryDataCommit=manifest['commit'],
                     legacyCommit=LEGACY, handlerEnabled=False, installed=False, pinnedInputHashes=pins,
                     skill=skill_row, missile=missile_row, skillDescription=desc_row,
                     sourceEvidence='tools/reference/twister-source-evidence.json',
                     damageEvidence=['tools/reference/twister-damage-source-evidence.json', 'tools/reference/twister-damage-evidence.json'],
                     officialModernChange=dict(url='https://news.blizzard.com/en-us/article/23788293/diablo-ii-resurrected-patch-2-4-ladder-now-live',
                                               release='2.4 released notes, Twister',
                                               established='Twister gained an Arctic Blast synergy that increases stun duration by20percent per hard level. The pinned modern calc2 is10+2*ArcticBlast.blvl frames; the legacy raw Param2 fallback must not replace it.'),
                     reuse=[dict(component='DruidEmitterPatterns.InitializeCharged / ChargedBolts',
                                 reason='Twister and Tornado call the same complete Charged Bolt initializer as the prepared Firestorm/sentry routes. Twister has three charged paths, not Teeth-style fan rays.',
                                 evidence=['tools/reference/firestorm-source-evidence.json', 'tools/reference/charged-bolt-path-source-evidence.json', 'tools/reference/sentry-launch-source-evidence.json']),
                            dict(component='DruidGroundCasting / SkillCombat', reason='SC ground/live/deleted target aim, cancellation and once-only release payment can share the existing casting adapter, subject to dedicated acceptance.'),
                            dict(component='SkillValues.PhysicalDamage256', reason='Complete source creator/getter/writer captures and the actual content helper match9180contexts/all255ranks; preserve physical synergy truncation before HitShift7. Formula controls for modern stun are separate from stun delivery.'),
                            dict(component='SourceMissileSteps / missile collision / CombatStuns', reason='Reuse only after preserving Size2,50frames, source0 physical packet, damage9 stun/class, GetHit/ReturnFire/KnockBack flags and explicit modern NHD policy. No new correctness is inherited merely by naming these consumers.')],
                     remaining=['Full factory/lifecycle composition, native physical damage commitment, modern stun delivery, missile path/movement/collision/contact/defense handling and current NHD grouping.',
                                'Learned SC start/release/7mana payment, bindings, real input, item/synergy/rank controls, saved actors and actual reload/lifetime/owner/facet acceptance.',
                                'Native geometry/room/timer/seed/event/current-executable and ordinary-client limits remain explicit. Do not port the older stun fallback or invent an exact modern grouping.',
                                'Final twister/cast/contact/expiry/audio and talent/left/right/quick icons remain in the existing separate240-row inventories; identified placeholders may unblock mechanics.'],
                     next='Finish and install the separately tested Fissure candidate first. Continue Twister from this source-only composition, reusing charged paths and independently checking physical/stun consumers. Arctic Blast remains tied to the deferred player Inferno/channel work; Boulder terminal blast/ground is a separate current-build observation gap.')
    output = ROOT / 'tools/reference/twister-preflight.json'
    text = json.dumps(preflight, indent=2) + '\n'
    if check:
        assert output.read_text() == text
    else:
        output.write_text(text)
    print(f'Twister source: {counts[0]} launch/initializer and {counts[1]} legacy stun cases; ASan/UBSan clean; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
