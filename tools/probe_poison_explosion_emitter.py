#!/usr/bin/env python3
"""Execute Poison Explosion do63, complete cloud emitter/factory and do3 wrapper."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'poison-explosion-emitter'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    routines={};sources={}
    def body(path,name):
        p=BASE/'d2moo-pinned-source'/path;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
        sources[path]=sha(p);text=function(raw.decode(),name);routines[name]=hashlib.sha256(text.encode()).hexdigest();return text
    parent=BASE/'guided-arrow-source-probe.cpp';cpp=parent.read_text().split('void reset(){')[0]
    assert body('source/D2Game/src/MISSILES/Missiles.cpp','MISSILES_CreateMissileFromParams') in cpp
    cpp=cpp.replace('MISSILES_CreateMissileFromParams(','FullFactory(')
    cpp='int failEvery=0;\n'+cpp
    cpp=cpp.replace('creates++;if(!allocated)return nullptr;','creates++;if(!allocated||(failEvery>0&&creates%failEvery==0))return nullptr;')
    cpp+=r'''
constexpr int STATE_CORPSE_NOSELECT=2;bool corpse=true;int noselect=0,refreshes=0,masks=0;
int D2COMMON_11021(D2UnitStrc*){return corpse;}
void STATES_ToggleState(D2UnitStrc*,int state,int value){if(state==STATE_CORPSE_NOSELECT&&value==1)noselect++;}
void UNITROOM_RefreshUnit(D2UnitStrc*){refreshes++;}
std::vector<std::vector<int>> emissions;
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc* g,D2MissileStrc* p){auto* u=FullFactory(g,p);emissions.push_back({p->dwFlags,p->nX,p->nY,p->nTargetX,p->nTargetY,p->nVelocity,p->nLoops,p->nSkill,p->nSkillLevel,u?u->total:-1,u?u->path.velocity:-1,int(p->pOwner==&owner),int(p->pOrigin==&target)});return u;}
int PATH_GetVelocity(D2DynamicPathStrc* p){return p->velocity;}
void COLLISION_SetMask(D2ActiveRoomStrc*,int,int,int mask){if(mask==0x40)masks++;}
'''
    mode='source/D2Game/src/MISSILES/MissMode.cpp'
    cpp+=body(mode,'MISSMODE_CreatePoisonCloudHitSubmissiles')+'\n'+body('source/D2Game/src/SKILLS/SkillNec.cpp','SKILLS_SrvDo063_PoisonExplosion')+'\n'+body(mode,'MISSMODE_SrvDo03_PoisonCloud_Blizzard_ThunderStorm_HandOfGod')
    cpp+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("{\"emissions\":[");
for(int rank:{1,20,255})for(int slowed:{0,1})for(int victim:{0,1})for(int valid:{0,1})for(int mid:{-1,217,1000})for(int record:{0,1})for(int allocation:{0,1,2}){
owner={};target={};target.x=410;target.y=720;selected=victim?&target:nullptr;corpse=valid;skill={};skill.wSrvMissileA=mid;row={};row.nVel=row.nMaxVel=0;row.wRange=60;row.nSubLoop=1;row.nSubStart=10;row.nSubStop=19;row.nCollision=1;row.dwParam[0]=2;row.dwParam[1]=4;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_CANSLOW];
hasSkill=true;hasRecord=record;hasRoom=coordinates=true;allocated=allocation!=0;failEvery=allocation==2?2:0;slow=slowed;noselect=refreshes=creates=0;emissions.clear();
int result=SKILLS_SrvDo063_PoisonExplosion(&game,&owner,83,rank);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",rank,slowed,victim,valid,mid,record,allocation,result,noselect,refreshes);first=false;
bool comma=false;for(auto& values:emissions){printf("%s[",comma?",":"");comma=true;bool next=false;for(int value:values){printf("%s%d",next?",":"",value);next=true;}printf("]");}printf("]]");}
printf("],\"do3\":[");first=true;for(int path:{0,1})for(int velocity:{0,96,192}){target={};target.pDynamicPath=path?&target.path:nullptr;target.path.velocity=velocity;masks=collisions=0;int result=MISSMODE_SrvDo03_PoisonCloud_Blizzard_ThunderStorm_HandOfGod(&game,&target);printf("%s[%d,%d,%d,%d,%d]",first?"":",",path,velocity,result,masks,collisions);first=false;}printf("]}\n");}
'''
    p=STAGE/'emitter.cpp';p.write_text(cpp);binary=STAGE/'emitter';run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],capture_output=True);compiler=STAGE/'compiler.log';compiler.write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()
    run=subprocess.run([str(binary)],capture_output=True);san=STAGE/'sanitizer.log';san.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode();cases=json.loads(run.stdout);assert len(cases['emissions'])==432
    xs=[0,1,2,2,2,2,2,1,0,-1,-2,-2,-2,-2,-2,-1];ys=[2,2,2,1,0,-1,-2,-2,-2,-2,-2,-1,0,1,2,2]
    attempts=created_count=0
    for rank,slow,target,valid,mid,record,allocation,result,noselect,refresh,shots in cases['emissions']:
        consumes=mid==217 and target and valid
        assert result==(int(bool(target and valid)) if mid==217 else 1) and noselect==refresh==int(bool(consumes))
        assert len(shots)==(8 if consumes and record else 0)
        for i,shot in enumerate(shots):
            alive=allocation==1 or allocation==2 and i%2==0
            assert shot==[23,410,720,xs[i*2],ys[i*2],256,0,83,rank,60 if alive else -1,(96 if slow else 192) if alive else -1,1,1]
            attempts+=1;created_count+=alive
    for path,velocity,result,masks,collisions in cases['do3']:assert result==1 and collisions==1 and masks==int(not path or velocity==0)
    fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    refs=ROOT/'tools/reference/d2r-named-equipment/json';skill=json.loads((refs/'skills.json').read_text())['83'];row=json.loads((refs/'missiles.json').read_text())['poisonexplosioncloud']
    assert skill['srvdofunc']==63 and skill['srvmissilea']=='poisonexplosioncloud' and row['*ID']==217
    assert [row.get(k,0) for k in ['pSrvDoFunc','Range','Size','Param1','Param2','CanSlow','CollideKill','LastCollide','NextHit']]==[3,60,2,2,4,1,0,1,0]
    out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerAccepted=False,currentExecutableParity=False,emissionCases=432,do3Cases=6,allocationAttempts=attempts,createdClouds=created_count,
        sourceHashes=sources,completeRoutineHashes=routines,inputs={str(f.relative_to(ROOT)):sha(f) for f in [parent,refs/'skills.json',refs/'missiles.json']},artifacts={str(f.relative_to(ROOT)):sha(f) for f in [p,compiler,san,fixture]},
        established=['Complete do63checks the selected corpse,toggles CORPSE_NOSELECT and refreshes it,then calls the complete cloud emitter with substep0/mainstep2/loops0. Eight main directions use flags0x17,Param1<<7=256raw velocity,and60frames;no outer ring or animation-loop extension is added.',
            'Complete factory produces192FP8velocity or96under supplied50percent Slow Missiles. All-failed and alternating-failure allocation cases continue all eight attempts. Origin,owner,skill83/rank and corpse coordinates are retained.',
            'Complete do3writes collision mask0x40only when no dynamic path or velocity is zero,then delegates the existing collision service. Generic dispatcher/collision/recipient order remains a separately declared service.'],
        remaining=['The older do63has a skill-id versus null-record guard typo;missing skill records are outside the compiled domain. Current pinned data is required by the native route rather than reproducing a null dereference.',
            'World room/path/stat/allocator,corpse validity predicate,packet creation and generic collision are supplied services. Native saved clouds,rate/length delivery,initial health,loot/payment/inputs/lifecycle/reloads and current executable/client equivalence still require acceptance.'])
    destination=ROOT/'tools/reference/poison-explosion-emitter-source-evidence.json';text=json.dumps(out,indent=2)+'\n'
    if check:assert destination.read_text()==text
    else:destination.write_text(text)
    print(f'Poison Explosion:432do63/emitter/factory contexts,6do3controls,{attempts}attempts/{created_count}clouds;native integration pending.')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
