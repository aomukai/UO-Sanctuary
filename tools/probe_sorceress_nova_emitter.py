#!/usr/bin/env python3
"""Compose Sorceress Nova/Frost Nova do22, emitter and complete missile factory."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'sorceress-nova-emitter'
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
    cpp=cpp.replace('velocity=0,flags=0;','velocity=0,flags=0,acceleration=0,maximum=0;')
    cpp=cpp.replace('void PATH_SetAcceleration(D2DynamicPathStrc*,int){}void PATH_SetMaxVelocity(D2DynamicPathStrc*,int){}','void PATH_SetAcceleration(D2DynamicPathStrc* p,int n){p->acceleration=n;}void PATH_SetMaxVelocity(D2DynamicPathStrc* p,int n){p->maximum=n;}')
    cpp=cpp.replace(original,'std::vector<std::vector<int>> emitted;void sub_6FC6E8B0(D2GameStrc*,D2UnitStrc*,D2UnitStrc* u){emitted.push_back({u->dwClassId,u->x,u->y,u->path.x,u->path.y,u->path.velocity,u->remaining,u->total,u->skill,u->rank,int(u->owner==&owner),u->path.acceleration,u->path.maximum});}')
    cpp+='\nint progressive=118;int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc*,int){return progressive;}\nint DATATBLS_GetMissileVelocityFromMissilesTxt(int,int){return row.nVel;}\n'
    cpp+=body('source/D2Game/src/SKILLS/Skills.cpp','sub_6FD14170')+'\n'+body('source/D2Game/src/SKILLS/SkillSor.cpp','SKILLS_SrvDo022_NovaAttack')
    cpp+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int skillId:{44,48})for(int rank:{1,20,255})for(int slowed:{0,1})for(int allocate:{0,1})for(int skills:{0,1})for(int validMid:{-1,1,1000}){
owner={};owner.pDynamicPath=&owner.path;owner.x=400;owner.y=700;row={};int mid=validMid==1?(skillId==44?119:90):validMid;row.nVel=row.nMaxVel=24;row.wAccel=-1000;row.wRange=skillId==44?14:13;row.nActivate=0;row.dwMissileFlags=0;skill={};skill.wSrvMissileA=mid;
hasSkill=skills;hasRecord=hasRoom=coordinates=true;allocated=allocate;slow=slowed;progressive=mid;calc=0;creates=draws=0;emitted.clear();
int result=SKILLS_SrvDo022_NovaAttack(&game,&owner,skillId,rank);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",skillId,rank,slowed,allocate,skills,mid,result,creates,draws);first=false;bool comma=false;
for(auto& values:emitted){if(comma)printf(",");comma=true;printf("[");bool next=false;for(int value:values){if(next)printf(",");next=true;printf("%d",value);}printf("]");}printf("]]");}printf("]\n");}
'''
    p=STAGE/'emitter.cpp';p.write_text(cpp);binary=STAGE/'emitter'
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(p),'-o',str(binary)],capture_output=True);compiler=STAGE/'compiler.log';compiler.write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()
    run=subprocess.run([str(binary)],capture_output=True);san=STAGE/'sanitizer.log';san.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode();cases=json.loads(run.stdout);assert len(cases)==144
    normal=None
    for sid,rank,slow,allocate,skills,mid,result,attempts,draws,emitted in cases:
        valid=bool(skills and mid==(119 if sid==44 else 90));assert (result,attempts,draws,len(emitted))==(int(valid),64 if valid else 0,0,64 if valid and allocate else 0)
        if emitted:
            for shot in emitted:assert shot[:3]==[119 if sid==44 else 90,400,700] and shot[5:]==[4608,14 if sid==44 else 13,14 if sid==44 else 13,sid,rank,1,-1000,6144]
            aims=[shot[3:5] for shot in emitted];assert len({tuple(x) for x in aims})==64
            assert aims[0]==[430,700] and aims[16]==[400,730] and aims[32]==[370,700] and aims[48]==[400,670]
            if normal is None:normal=aims
            else:assert aims==normal
    fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    refs=ROOT/'tools/reference/d2r-named-equipment/json';skills=json.loads((refs/'skills.json').read_text());missiles=json.loads((refs/'missiles.json').read_text())
    for sid,key,mid,frames in ((44,'frostnova',119,14),(48,'nova',90,13)):
        skill=skills[str(sid)];missile=missiles[key]
        assert skill['srvdofunc']==22 and skill['srvmissilea']==key and skill['calc1']==0 and not skill.get('SrcDam') and missile['*ID']==mid
        assert [missile.get(k,0) for k in ['Vel','MaxVel','Accel','Range','CanSlow','VelLev','LevRange','Pierce','InitSteps','CollideKill','NextHit','NextDelay']]==[24,24,-1000,frames,0,0,0,0,0,0,1,4]
    out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerAccepted=False,currentExecutableParity=False,cases=144,validAllocationAttempts=1536,createdMissiles=768,
        sourceHashes=sources,completeRoutineHashes=routines,inputs={str(f.relative_to(ROOT)):sha(f) for f in [parent,refs/'skills.json',refs/'missiles.json']},artifacts={str(f.relative_to(ROOT)):sha(f) for f in [p,compiler,san,fixture]},
        established=['Complete do22/emitter/factory for pinned Nova48/90 and Frost Nova44/119 attempts64 allocations per valid cast; both slow states retain4608FP8 launch velocity,negative1000acceleration and6144maximum. Range is13/14 respectively;source owner/skill/rank/origin and all64aims are retained.',
        'The exact64integer offsets match Poison Nova and Frozen Orb. Allocation failures still attempt all64children;missing skill/invalid progressive missile emits none. Factory performs no Pierce seed draws.'],
        services=['Progressive missile selection/table velocity,world/room/allocator/path/stat/damage services are explicit. Setter services capture acceleration/maximum;complete source acceleration kernel execution is separately frozen in missile-acceleration-source-evidence.json.',
        'This does not trace modern executable behavior or prove native scheduling,recipient geometry,NextHit identity or combat damage. No handler/class acceptance or deployment is inferred.'])
    path=ROOT/'tools/reference/sorceress-nova-emitter-source-evidence.json';encoded=json.dumps(out,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Sorceress Nova emitter:144 complete launch/ring/factory contexts,1536 attempts/768 children; native acceptance separate.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
