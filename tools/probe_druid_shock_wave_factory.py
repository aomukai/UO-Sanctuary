#!/usr/bin/env python3
"""Compose unchanged Shock Wave do8 and the complete source missile factory."""
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
    pinpath=ROOT/'tools/reference/druid-shock-wave-preflight.json';pin=json.loads(pinpath.read_text())
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==pin['legacyCommit']
    hashes={};routines={}
    def body(relative,name):
        path=BASE/'d2moo-pinned-source'/relative;raw=path.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=sha(path);text=function(raw.decode(),name);routines[name]=hashlib.sha256(text.encode()).hexdigest();return text+'\n'
    parent=ROOT/'tools/reference/druid-rabies-factory-engine-evidence.json';assert json.loads(parent.read_text())['legacyCommit']==tree['sha']
    scaffold=BASE/'druid-rabies-factory-source-probe.cpp';cpp=scaffold.read_text().split('int main()',1)[0]
    name='MISSILES_CreateMissileFromParams';assert function(cpp,name)+'\n'==body('source/D2Game/src/MISSILES/Missiles.cpp',name)
    original='int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return calc;}'
    assert original in cpp;cpp=cpp.replace(original,'int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){return formula==0?5:0;}')
    cpp=cpp.replace('void MISSILE_SetOwner(D2UnitStrc*,D2UnitStrc*){}','D2UnitStrc* lastCollider=nullptr;void MISSILE_SetOwner(D2UnitStrc*,D2UnitStrc* p){lastCollider=p;}')
    original='void sub_6FC6E8B0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){}';assert original in cpp
    cpp=cpp.replace(original,'std::vector<std::vector<int>> emitted;void sub_6FC6E8B0(D2GameStrc*,D2UnitStrc*,D2UnitStrc* u){emitted.push_back({u->x,u->y,u->path.x,u->path.y,u->path.velocity,u->total,u->remaining,u->activate,u->skill,u->rank,int(u->owner==&owner),int(lastCollider==&owner),u->missileFlags});}')
    for name in ['sub_6FD140D0','sub_6FD14120']:cpp+=body('source/D2Game/src/SKILLS/Skills.cpp',name)
    cpp+=body('source/D2Game/src/SKILLS/SkillAma.cpp','SKILLS_SrvDo008_MultipleShot_Teeth_ShockWave')
    cpp+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int dx=-8;dx<=8;dx++)for(int dy=-8;dy<=8;dy++)for(int rank:{1,20,255})for(int slowed:{0,1})for(int w:{0,1})for(int allocate:{0,1}){
 owner={};owner.pDynamicPath=&owner.path;owner.x=400;owner.y=700;selected=nullptr;aimX=owner.x+dx;aimY=owner.y+dy;emitted.clear();lastCollider=nullptr;
 skill={};skill.wSrvMissileA=skill.wSrvMissileB=511;skill.dwCalc[0]=0;skill.dwCalc[1]=1;skill.dwCalc[2]=2;row={};row.nVel=row.nMaxVel=20;row.wRange=14;row.nActivate=0;row.nCollideType=3;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_CANSLOW]|gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE]|gdwBitMasks[MISSILESFLAGINDEX_GETHIT]|gdwBitMasks[MISSILESFLAGINDEX_RETURNFIRE];
 hasSkill=hasRoom=hasRecord=coordinates=true;allocated=allocate;slow=slowed;weaponClass=w;draws=creates=repaths=0;
 int result=SKILLS_SrvDo008_MultipleShot_Teeth_ShockWave(&game,&owner,243,rank);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",dx,dy,rank,slowed,w,allocate,result,owner.dwFlags,creates,draws);first=false;bool next=false;
 for(auto& values:emitted){if(next)printf(",");next=true;printf("[");bool comma=false;for(auto value:values){if(comma)printf(",");comma=true;printf("%d",value);}printf("]");}printf("]]");}
 printf("]\n");}
'''
    path=BASE/'druid-shock-wave-factory-source-probe.cpp';path.write_text(cpp)
    compiler=BASE/'druid-shock-wave-factory-compiler.log';sanitizer=BASE/'druid-shock-wave-factory-sanitizer.log'
    with compiler.open('w') as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(path.with_suffix(''))],stdout=log,stderr=subprocess.STDOUT,check=True)
    result=subprocess.run([str(path.with_suffix(''))],capture_output=True);sanitizer.write_bytes(result.stderr)
    assert result.returncode==0 and not result.stderr,result.stderr.decode()[-3000:]
    cases=json.loads(result.stdout);assert len(cases)==6936
    vectors=json.loads((BASE/'druid-shock-wave-fan-source-probe.json').read_text())
    fans={(x[0],x[1]):x[7] for x in vectors};assert len(fans)==289
    for dx,dy,rank,slow,weapon,allocate,result,flags,attempts,draws,shots in cases:
        assert (result,flags,attempts,draws)==(1,64,5,0)
        expected=[]
        if allocate:
            for x,y,*_ in fans[dx,dy]:
                if x==y==0:x=y=1
                expected.append([400,700,400+x,700+y,1920 if slow else 3840,14,14,14,243,rank,1,1,0])
        assert shots==expected,(dx,dy,rank,slow,weapon,allocate,shots,expected)
    fixture=path.with_suffix('.json');fixture.write_text(json.dumps(cases,indent=2)+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,nativeAccepted=False,currentExecutableParity=False,
        preflightSha256=sha(pinpath),sourceHashes=hashes,completeRoutineHashes=routines,
        inheritedScaffold=dict(evidence=str(parent.relative_to(ROOT)),sha256=sha(parent)),
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [path,compiler,sanitizer,fixture,ROOT/'tools/reference/druid-shock-wave-engine-evidence.json',BASE/'druid-shock-wave-fan-source-probe.json']},
        verified=dict(fanFactoryContexts=6936,allocationAttempts=34680,createdWaves=17340,completeBodies=4,sanitizers='ASan/UBSan clean'),
        established=[
            'Complete do8 calls the complete missile factory five times per current Shock Wave cast. Both weapon selectors yield511. Allocation failures do not turn do8 into failure,while no child is recorded. Skill/rank and original owner survive all successful factory calls.',
            'Current20velocity becomes3840FP8path velocity after the75percent factory factor; the declared50percent Slow Missiles service gives1920. Lifetime14and activation marker14are independent from the five-wave count. Same-point aim nudges(+1,+1)inside the actual factory after the fan helpers; empty-ground click direction is retained.',
            'Each created wave starts at the caster and carries the actual factory-initialized last-collider owner identity. No pierce rolls occur for this row. Original source routines are unchanged and hash-bound to the pinned tree.',
        ],services=[
            'World/room availability, allocation, stat lists, path setters, owner storage and50percent Slow Missiles value are explicit services inherited from the separately accepted factory scaffold. Final factory service records the completed wave. It does not simulate geometric flight or collision.',
            'The complete source-zero damage creator/writer is executed in the separate Shock Wave engine matrix; factory damage services remain declared here. This is actual fan-to-factory composition,not a complete world/damage simulation.',
        ],remaining=[
            'Execute full source collision/history/NextHit/hit callbacks and final damage/stun event flow,then connect saved native waves,bear SC/UseAttackRate,input,7mana,cancellation,ReturnFire and item-stat transport. Modern same-skill delay scope and exact geometry/owner/client behavior remain explicit.',
            'Final directional wave/cast/contact/stun art/audio and separate talent/left/right/quick icons are still pending. No learned handler,installation,class completion or current-executable parity is claimed.',
        ])
    destination=ROOT/'tools/reference/druid-shock-wave-factory-engine-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==encoded
    else:destination.write_text(encoded)
    print('Shock Wave fan+factory:6936contexts/34680allocation attempts/17340waves; saved native controller pending.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
