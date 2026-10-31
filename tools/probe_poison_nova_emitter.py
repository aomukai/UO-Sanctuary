#!/usr/bin/env python3
"""Compose Poison Nova's complete do22/ring emitter with the missile factory."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'poison-nova-emitter'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    routines={};sources={}
    def body(path,name):
        p=BASE/'d2moo-pinned-source'/path;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
        sources[path]=sha(p);text=function(raw.decode(),name);routines[name]=hashlib.sha256(text.encode()).hexdigest();return text
    parent=BASE/'guided-arrow-source-probe.cpp';cpp=parent.read_text().split('void reset(){')[0]
    assert body('source/D2Game/src/MISSILES/Missiles.cpp','MISSILES_CreateMissileFromParams') in cpp
    original='void sub_6FC6E8B0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){}';assert original in cpp
    cpp=cpp.replace(original,'std::vector<std::vector<int>> emitted;void sub_6FC6E8B0(D2GameStrc*,D2UnitStrc*,D2UnitStrc* u){emitted.push_back({u->dwClassId,u->x,u->y,u->path.x,u->path.y,u->path.velocity,u->remaining,u->total,u->skill,u->rank,int(u->owner==&owner)});}')
    cpp+='\nint progressive=118;int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc*,int){return progressive;}\nint DATATBLS_GetMissileVelocityFromMissilesTxt(int,int){return row.nVel;}\n'
    cpp+=body('source/D2Game/src/SKILLS/Skills.cpp','sub_6FD14170')+'\n'+body('source/D2Game/src/SKILLS/SkillSor.cpp','SKILLS_SrvDo022_NovaAttack')
    cpp+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int rank:{1,20,255})for(int slowed:{0,1})for(int allocate:{0,1})for(int skills:{0,1})for(int mid:{-1,118,1000}){
owner={};owner.pDynamicPath=&owner.path;owner.x=400;owner.y=700;row={};row.nVel=row.nMaxVel=12;row.wRange=30;row.nActivate=0;row.dwMissileFlags=0;skill={};skill.wSrvMissileA=118;
hasSkill=skills;hasRecord=hasRoom=coordinates=true;allocated=allocate;slow=slowed;progressive=mid;calc=0;creates=draws=0;emitted.clear();
int result=SKILLS_SrvDo022_NovaAttack(&game,&owner,92,rank);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",rank,slowed,allocate,skills,mid,result,creates,draws);first=false;bool comma=false;
for(auto& values:emitted){if(comma)printf(",");comma=true;printf("[");bool next=false;for(int value:values){if(next)printf(",");next=true;printf("%d",value);}printf("]");}printf("]]");}printf("]\n");}
'''
    p=STAGE/'emitter.cpp';p.write_text(cpp);binary=STAGE/'emitter'
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],capture_output=True);compiler=STAGE/'compiler.log';compiler.write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()
    run=subprocess.run([str(binary)],capture_output=True);san=STAGE/'sanitizer.log';san.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode();cases=json.loads(run.stdout);assert len(cases)==72
    normal=None
    for rank,slow,allocate,skills,mid,result,attempts,draws,emitted in cases:
        valid=bool(skills and mid==118);assert (result,attempts,draws,len(emitted))==(int(valid),64 if valid else 0,0,64 if valid and allocate else 0)
        if emitted:
            for shot in emitted:assert shot[:3]==[118,400,700] and shot[5:]==[2304,30,30,92,rank,1]
            aims=[shot[3:5] for shot in emitted];assert len({tuple(x) for x in aims})==64
            assert aims[0]==[430,700] and aims[16]==[400,730] and aims[32]==[370,700] and aims[48]==[400,670]
            if normal is None:normal=aims
            else:assert aims==normal
    fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    refs=ROOT/'tools/reference/d2r-named-equipment/json';skill=json.loads((refs/'skills.json').read_text())['92'];missile=json.loads((refs/'missiles.json').read_text())['poisonnova']
    assert skill['srvdofunc']==22 and skill['srvmissilea']=='poisonnova' and skill['calc1']==0 and not skill.get('SrcDam') and missile['*ID']==118
    assert [missile.get(k,0) for k in ['Vel','MaxVel','Range','CanSlow','VelLev','LevRange','Pierce','InitSteps']]==[12,12,30,0,0,0,0,0]
    out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerAccepted=False,currentExecutableParity=False,cases=72,validAllocationAttempts=768,createdMissiles=384,
        sourceHashes=sources,completeRoutineHashes=routines,inputs={str(f.relative_to(ROOT)):sha(f) for f in [parent,refs/'skills.json',refs/'missiles.json']},artifacts={str(f.relative_to(ROOT)):sha(f) for f in [p,compiler,san,fixture]},
        established=['Complete do22 calls the complete64-offset emitter and complete missile factory. Current Poison Nova has calc1zero,velocity12,range30 and no CanSlow. Both supplied slow states preserve2304FP8velocity;every valid cast attempts64allocations even when all allocations fail.',
        'The emitter uses its exact64integer source offsets,including cardinal(+30,0)/(0,+30)/(-30,0)/(0,-30). The complete factory records absolute origin,relative aim,owner,skill92/rank and30current/total frames;no Pierce draws occur. It is not a single expanding area-damage pulse.'],
        services=['Progressive missile selection and the table velocity lookup are explicit services constrained by pinned constant srvmissilea/Vel/VelLev. World/room/allocator/path/stat/damage services are inherited from the declared factory scaffold;creation does not execute the poison packet or contact.',
        'Next:compose complete source-zero poison rate/length/initial delivery and generic next-hit/contact,then saved native64-child allocation,source vectors/steps,poison clocks,lifecycle/payment/input/reloads. Exact modern next-hit identity and current path/client equivalence remain unverified. No runtime/data/class completion or installation is claimed.'])
    path=ROOT/'tools/reference/poison-nova-emitter-source-evidence.json';encoded=json.dumps(out,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Poison Nova emitter:72complete launch/ring/factory contexts,768attempts/384children;native poison integration pending.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
