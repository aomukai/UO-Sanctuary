#!/usr/bin/env python3
"""Execute the ordinary launch wrapper/factory and generic do1 update body."""
import argparse,ast,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'miasma-launch'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs={};routines={};artifacts=[]
    def body(rel,name):
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
        inputs[str(p.relative_to(ROOT))]=sha(p);s=function(raw.decode(),name);routines[name]=hashlib.sha256(s.encode()).hexdigest();return s
    abi=STAGE/'factory-abi-base.cpp'
    if not abi.exists():abi.write_bytes((BASE/'arctic-stream/factory-abi-base.cpp').read_bytes())
    assert sha(abi)=='727393fffa0479434a8c4ac3de5ee47b78b260acf44552919899cd33076438b4';artifacts.append(abi)
    cpp=abi.read_text().split('int main()',1)[0];factory=body('source/D2Game/src/MISSILES/Missiles.cpp','MISSILES_CreateMissileFromParams');assert factory in cpp
    cpp=cpp.replace(factory,'#define MISSILES_CreateMissileFromParams CoreFactory\n'+factory+'\n#undef MISSILES_CreateMissileFromParams')
    old='struct D2UnitStrc{';assert cpp.count(old)==1;cpp=cpp.replace(old,'struct MissileMemory{int nOwnerType=0,dwOwnerGUID=-1;};\n'+old+'MissileMemory memory;MissileMemory* pMissileData=&memory;')
    old='created.pDynamicPath=&created.path;created.dwUnitType';assert cpp.count(old)==1;cpp=cpp.replace(old,'created.pDynamicPath=&created.path;created.pMissileData=&created.memory;created.dwUnitType')
    old='void MISSILE_SetOwner(D2UnitStrc*,D2UnitStrc*){}';assert cpp.count(old)==1;cpp=cpp.replace(old,'void MISSILE_SetOwner(D2UnitStrc*,D2UnitStrc*);')
    old='int STATLIST_GetStatValue(D2StatListStrc*,int,int){return 50;}';assert cpp.count(old)==1;cpp=cpp.replace(old,'int slowPercent=100;int STATLIST_GetStatValue(D2StatListStrc*,int,int){return slowPercent;}')
    cpp+='\nconstexpr int UNIT_MONSTER_UNUSED=1,D2UnitInvalidGUID=-1;\nD2MissilesTxt* DATATBLS_GetMissilesTxtRecord(int){return hasRecord?&row:nullptr;}\n'
    cpp+=body('source/D2Common/src/Units/Missile.cpp','MISSILE_SetOwner')+'\n'
    cpp+='int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc*){return aimX;}int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc*){return aimY;}\nint sub_6FD118C0(D2GameStrc*,D2UnitStrc*){std::abort();}\n'
    cpp+='D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc* g,D2MissileStrc* p){captured=*p;return CoreFactory(g,p);}\n'
    cpp+=body('source/D2Game/src/SKILLS/Skills.cpp','sub_6FD11420')+'\n'
    cpp+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int rank:{1,20,255})for(int scene=0;scene<5;scene++)for(int delta:{-100,-99,0,99,100})for(int axis:{0,1})for(int pct:{0,33,50,100})for(int allocation:{0,1})for(int gate:{0,1,2}){
 owner={};owner.pDynamicPath=&owner.path;owner.dwUnitType=UNIT_PLAYER;owner.dwUnitId=91;owner.x=400;owner.y=700;target={};target.x=400+(axis==0?delta:0);target.y=700+(axis==1?delta:0);
 aimX=target.x;aimY=target.y;selected=scene==1?&target:nullptr;int explicitX=scene==2?target.x:0,explicitY=scene==2?target.y:0;
 if(scene==1||scene==2){aimX=450;aimY=750;}if(scene==3){aimX=0;aimY=700;}if(scene==4){aimX=400;aimY=700;}
 row={};row.nVel=row.nMaxVel=20;row.nVelLev=row.wLevRange=0;row.wRange=50;row.nActivate=0;row.nCollideType=3;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE]|gdwBitMasks[MISSILESFLAGINDEX_GETHIT]|gdwBitMasks[MISSILESFLAGINDEX_CANSLOW];
 hasRecord=gate!=1;hasRoom=gate!=2;allocated=allocation;slow=true;slowPercent=pct;creates=draws=repaths=0;captured={};
 auto* u=sub_6FD11420(&game,697,&owner,395,rank,0,0,explicitX,explicitY,0);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",rank,scene,delta,axis,pct,allocation,gate,u!=nullptr,creates,draws);first=false;
 if(u)printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",captured.dwFlags,u->x,u->y,u->path.x,u->path.y,u->path.velocity,u->total,u->remaining,u->activate,u->skill,u->rank,u->pMissileData->nOwnerType,u->pMissileData->dwOwnerGUID);
 printf("]]");}printf("]\n");}'''
    def execute(text,name):
        p=STAGE/(name+'.cpp');p.write_text(text);compiler=p.with_suffix('.compiler.log');sanitizer=p.with_suffix('.sanitizer.log')
        with compiler.open('w') as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(p.with_suffix(''))],stdout=log,stderr=subprocess.STDOUT,check=True)
        run=subprocess.run([str(p.with_suffix(''))],capture_output=True);sanitizer.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
        artifacts.extend([p,compiler,sanitizer]);return json.loads(run.stdout)
    factory_cases=execute(cpp,'miasma-factory');assert len(factory_cases)==3600
    for rank,scene,delta,axis,pct,allocation,gate,made,attempts,draws,shot in factory_cases:
        x=400+(delta if axis==0 else 0);y=700+(delta if axis==1 else 0)
        if scene==3:x=0;y=700
        if scene==4:x=400;y=700
        velocity=(20*256*pct//100)*75//100
        if velocity and (x,y)==(400,700):x+=1;y+=1
        valid=x!=0 and y!=0 and gate==0 and (not velocity or abs(x-400)<100 and abs(y-700)<100)
        assert (made,attempts,draws)==(int(valid and allocation),int(valid),0)
        if made:assert shot==[33,400,700,x,y,velocity,50,50,50,395,rank,0,91]
    old=ROOT/'tools/probe_poison_collision_reference.py';collision=body('source/D2Game/src/MISSILES/MissMode.cpp','MISSMODE_HandleMissileCollision')
    node=next(x.value for x in ast.parse(old.read_text()).body if isinstance(x,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='cpp' for t in x.targets))
    def compose(n):
        if isinstance(n,ast.Constant) and isinstance(n.value,str):return n.value
        if isinstance(n,ast.Name) and n.id=='body':return collision
        if isinstance(n,ast.BinOp) and isinstance(n.op,ast.Add):return compose(n.left)+compose(n.right)
        raise ValueError('Unsupported bounded collision harness expression')
    text=compose(node);assert collision in text;inputs[str(old.relative_to(ROOT))]=sha(old)
    collision_cases=execute(text,'miasma-basic-update');assert len(collision_cases)==5120
    output=BASE/'miasma-launch-source-probe.json';output.write_text(json.dumps(dict(factory=factory_cases,collision=collision_cases),separators=(',',':'))+'\n');artifacts.append(output)
    directory=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((directory/'sources.json').read_text());p=directory/'json/missiles.json';assert sha(p)==manifest['files']['json/missiles.json']['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p)
    current=json.loads(p.read_text())['miasmabolt'];assert (current['Vel'],current['MaxVel'],current['Range'],current['pSrvDoFunc'],current['pSrvHitFunc'])==(20,20,50,1,41)
    artifacts.append(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,inputs=inputs,routines=routines,
        cases=dict(factory=3600,basicUpdate=5120),artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},
        established=['Complete direct wrapper with zero launch offset selects explicit coordinates, current selected-target coordinates or the cursor path. It requests flags33 and no homing target pointer. Zero coordinates fail before the factory.',
            'Complete generic factory applies CanSlow percentage before75percent velocity conversion, preserves50-frame lifetime/activation and initializes LastCollide owner metadata through its complete setter. Same-position aim is offset only when velocity is nonzero. The100-unit aim guard is also conditional on nonzero velocity. Allocation fails cleanly and draws no RNG in this declared scope.',
            'Complete generic do1 body moves before decrementing lifetime. Movement failure dispatches an ending callback without decrement; expiry dispatches after the frame write and before contact scans. Live path flags and saved steps are read after movement. First contact dispatches the supplied handler; targetless wall dispatch returns removal2 regardless of the callback result.'],
        services=['Factory allocation, rooms, path setters/repath, damage supplier and stat services are declared. Current damage producers have their own preceding source/native acceptance. Native path/Q12/room geometry and actual modern callback dispatch are not executed by this harness.',
            'Generic update uses the existing declared one-step collision services and callback return7 across5120cases. Native additional controls exercise post-move reads, frame-before-hit ordering and ordered two-step selection; full source candidate/room iteration remains separate.'],
        remaining=['New Miasma hit41/cloud-do41 bodies, actual cast/input/mana, world projectile persistence, cloud creation/geometry/hit identity and modern mastery/pierce timing remain to connect. This numerical request/update component does not register a learned handler.'])
    p=ROOT/'tools/reference/miasma-launch-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Miasma launch:3600complete wrapper/factory/owner cases and5120generic update cases; clean ASan/UBSan, no learned handler.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
