#!/usr/bin/env python3
"""Compose current Shock Wave callback7 with complete legacy hit routing/history."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    pinpath=ROOT/'tools/reference/druid-shock-wave-preflight.json';pin=json.loads(pinpath.read_text())
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==pin['legacyCommit']
    hashes={};bodies={}
    def body(relative,name):
        path=BASE/'d2moo-pinned-source'/relative;raw=path.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=sha(path);text=function(raw.decode(),name);bodies[name]=hashlib.sha256(text.encode()).hexdigest();return text+'\n'
    common='source/D2Common/src/Units/Missile.cpp';mode='source/D2Game/src/MISSILES/MissMode.cpp'
    old=BASE/'druid-rabies-router-source-probe.cpp';parent=ROOT/'tools/reference/druid-rabies-router-engine-evidence.json'
    assert sha(old)==json.loads(parent.read_text())['generatedSourceSha256']
    first=body(common,'MISSILE_SetOwner');cpp=old.read_text().split(first,1)[0]
    assert 'int main()' not in cpp and 'enum D2C_MissilesTxtFlagIndices' in cpp
    cpp=cpp.replace('#include <map>','#include <map>\n#include <vector>')
    cpp=cpp.replace('dwClassId=516','dwClassId=511').replace('skill=238','skill=243').replace('assert(id==516)','assert(id==511)').replace('assert(id==238)','assert(id==243)')
    cpp=cpp.replace('int nOwnerType=0,dwOwnerGUID=-1;','int nOwnerType=0,dwOwnerGUID=-1;')
    cpp=cpp.replace('std::map<int,int> stats;','bool justHit=false;std::map<int,int> stats;')
    cpp=cpp.replace('nNextHit=0,nNextDelay=0','nNextHit=1,nNextDelay=4').replace('wSrvHitFunc=53,wSrvDmgFunc=11','wSrvHitFunc=0,wSrvDmgFunc=7')
    cpp=cpp.replace('nCollideFriend=0;}row;','nCollideFriend=0;int dwDmgParam[1]={0};}row;')
    cpp=cpp.replace('int wAuraTargetState=112;','int dwParam[8]={40,15};')
    cpp=cpp.replace('int dwPhysDamage=0,dwPoisDamage=0,dwPoisLen=0;','int dwPhysDamage=0,dwPoisDamage=0,dwPoisLen=0,dwStunLen=0;unsigned dwHitClass=0;')
    cpp=cpp.replace('bool STATES_CheckState(D2UnitStrc*,int){return false;}','bool STATES_CheckState(D2UnitStrc* p,int s){assert(s==STATE_JUSTHIT);return p->justHit;}')
    cpp=cpp.replace('state==112','state==STATE_JUSTHIT').replace('&&state==112','&&state==STATE_JUSTHIT')
    cpp=cpp.replace('damage->dwPoisDamage=678;damage->dwPoisLen=99;','damage->dwPhysDamage=678;damage->dwHitClass=64;')
    cpp=cpp.replace('rateSeen=damage->dwPoisDamage;lengthSeen=damage->dwPoisLen;','rateSeen=damage->dwPhysDamage;lengthSeen=damage->dwStunLen;')
    cpp+=first+body(common,'MISSILE_CheckUnitIfOwner')+body(mode,'MISSMODE_SrvDmg07_Warcry_ShockWave')
    router=body(mode,'MISSMODE_SrvDmgHitHandler')
    for name in sorted(set(re.findall(r'\bMISSMODE_Srv(?:Hit|Dmg)\w+',router))-{'MISSMODE_SrvDmgHitHandler','MISSMODE_SrvDmg07_Warcry_ShockWave'}):
        damage='SrvDmg' in name
        cpp+=('void' if damage else 'int32_t')+' '+name+'(D2GameStrc*,D2UnitStrc*,D2UnitStrc*'+(',D2DamageStrc*' if damage else '')+'){std::abort();}\n'
    cpp+=router+r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int rank:{1,20,255})for(int just:{0,1})for(int repeat:{0,1})for(int own:{0,1})for(int hostile:{0,1})for(int victim:{0,1})for(int ending:{0,1})for(int failure:{0,1}){
parent={};owner={};target={};row={};marker={};parent.pMissileData=&parent.md;parent.rank=rank;owner.dwUnitType=UNIT_PLAYER;owner.dwUnitId=1;target.dwUnitType=UNIT_MONSTER;target.dwUnitId=10;target.justHit=just;
row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE];if(repeat){parent.md.nOwnerType=UNIT_MONSTER;parent.md.dwOwnerGUID=10;}
hasOwner=own;friendly=!hostile;marked=false;allocationFails=failure;fills=flags=resets=events=allocations=posts=0;rateSeen=lengthSeen=markedAtFill=-1;
int result=MISSMODE_SrvDmgHitHandler(&game,&parent,victim?&target:nullptr,ending);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",rank,just,repeat,own,hostile,victim,ending,failure,result,fills,flags,resets,events,allocations,posts,rateSeen,lengthSeen,markedAtFill,parent.md.nOwnerType,parent.md.dwOwnerGUID,marker.expires);first=false;
}printf("]\n");}
'''
    generated=BASE/'druid-shock-wave-collision-source-probe.cpp';generated.write_text(cpp)
    compiler=BASE/'druid-shock-wave-collision-compiler.log';sanitizer=BASE/'druid-shock-wave-collision-sanitizer.log'
    with compiler.open('w') as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(generated),'-o',str(generated.with_suffix(''))],stdout=log,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(generated.with_suffix(''))],capture_output=True);sanitizer.write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
    cases=json.loads(run.stdout);assert len(cases)==384
    for rank,just,repeat,owner,hostile,victim,end,failure,result,fills,flags,resets,events,allocations,posts,physical,stun,leased,last_type,last_guid,deadline in cases:
        rejected=bool(victim and (just or repeat or owner and not hostile))
        routed=not rejected and bool(victim or end);damage=routed and bool(victim)
        expected=1 if rejected or not routed or not end else 2
        assert (result,fills,flags,resets,events)==(expected,int(damage),int(damage),int(expected==2),int(routed))
        assert (allocations,posts)==(int(damage),int(damage and not failure))
        assert (physical,stun,leased)==((678,40+15*(rank-1),int(not failure)) if damage else (-1,-1,-1))
        assert (last_type,last_guid)==((1,10) if repeat or victim and not just else (0,-1))
        assert deadline==(1004 if damage and not failure else 6000)
    fixture=generated.with_suffix('.json');fixture.write_text(json.dumps(cases,indent=2)+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,nativeAccepted=False,currentExecutableParity=False,
        preflightSha256=sha(pinpath),sourceHashes=hashes,completeRoutineHashes=bodies,
        inheritedScaffold=dict(evidence=str(parent.relative_to(ROOT)),sha256=sha(parent)),contexts=len(cases),
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [generated,compiler,sanitizer,fixture]},
        established=[
            'Complete hit router,LastCollide setter/checker and damage7 compose384current Shock Wave control contexts with ASan/UBSan clean. Current hit callback0is not replaced by another skill callback. Ending/null-contact/current no-AR/no-Pierce/no-CollideKill paths retain the actual source decisions.',
            'Historical NextHit rejection precedes LastCollide mutation. Otherwise the target identity is stored before owner hostility. A valid contact allocates the four-frame lease before the hit-by-missile event,damage fill,damage7and final damage-flags service. Failed lease allocation does not suppress that current contact damage.',
            'Damage7supplies raw40+15*(rank-1)stun after physical fill and before damage-flags dispatch. Waves continue after ordinary contact; explicit ending removes them. An absent-owner control can still reach damage7but the separately executed actual damage-flags routine refuses ownerless health delivery.',
        ],services=[
            'Damage fill/final flags,hostility,stat-list allocation/post/expiry events,room/collision masks and hit event subscribers are explicit boundaries. Current physical creation/roll/flags are established separately; this program does not invent a health/stun service.',
            'The unchanged older router uses global JUSTHIT. This proves order and four-frame timing only. Modern per-execution grouping is a separate evidence-based adapter,not a claim that the old global flag is correct for3.3.93847.',
        ],remaining=[
            'Verify the full final health/stun event consumer,including zero physical damage,unique chance,boss/stationary/hireling caps and replacement. Then connect saved native linear waves,modern cast-local windows,bear SC/FCR,input/resources/cancellation and actual reloads.',
            'Mode3 candidate filtering is established by the retained complete finder proof; native spatial ordering/terrain and exact current event/RNG/geometry/owner/client remain explicit. Final effects/audio and talent/left/right/quick icons remain separate placeholders.',
        ])
    path=ROOT/'tools/reference/druid-shock-wave-collision-engine-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Shock Wave collision:384complete router/history/damage7contexts; final event health/stun and native controller next.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
