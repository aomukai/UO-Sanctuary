#!/usr/bin/env python3
"""Compose the complete existing missile factory with Charged Bolt initialization."""
import argparse,hashlib,json,re,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/charged-strike-velocity-source'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    subprocess.run([sys.executable,str(ROOT/'tools/probe_guided_arrow_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
    parent=ROOT/'tools/reference/guided-arrow-engine-evidence.json';shared=json.loads(parent.read_text())
    scaffold=ROOT/'.local/class-skills/guided-arrow-source-probe.cpp'
    base=scaffold.read_text().split('int main()',1)[0]
    # Only declared service/scaffold fields change. Both tested source bodies
    # remain complete and unchanged.
    assert base.count('return 50;')==1
    base=base.replace('return 50;','return slowPercent;').replace('bool hasSkill=true,','int slowPercent=50,pathDistance=-1;\nbool hasSkill=true,')
    assert base.count('std::map<int,int> stats;};')==1
    base=base.replace('std::map<int,int> stats;};','std::map<int,int> stats;D2SeedStrc pSeed;};')
    relative='source/D2Game/src/SKILLS/SkillSor.cpp'
    source=ROOT/'.local/class-skills/d2moo-pinned-source'/relative;raw=source.read_bytes()
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text())
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==relative)
    text=raw.decode();match=re.search(r'^void __fastcall SKILLS_MissileInit_ChargedBolt\([^\n]+\)\s*\n\{',text,re.M);assert match
    end=text.index('{',match.start())+1;depth=1
    while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
    body=text[match.start():end]
    services='''
constexpr int PATHTYPE_CHARGEDBOLT=10;
int MISSILE_GetTotalFrames(D2UnitStrc* u){return u->total;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc* p){return p->x;}
void PATH_SetType(D2DynamicPathStrc* p,int type){p->flags=type;}
void PATH_SetNewDistance(D2DynamicPathStrc*,uint8_t distance){pathDistance=distance;}
'''
    main=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;std::printf("[");bool first=true;
for(int rank:{1,20,255})for(int canSlow:{0,1})for(int state:{0,1})for(int percent:{0,1,33,50,75,100}){
reset();slow=state;slowPercent=percent;pathDistance=-1;row.nVel=row.nMaxVel=12;row.wRange=98;row.nActivate=3;row.dwMissileFlags=canSlow;
D2MissileStrc p{};p.pOwner=&owner;p.dwFlags=33;p.nMissile=100;p.nSkill=24;p.nSkillLevel=rank;p.nX=41;p.nY=71;p.nTargetX=45;p.nTargetY=73;p.pInitFunc=SKILLS_MissileInit_ChargedBolt;
auto* result=MISSILES_CreateMissileFromParams(&game,&p);if(!first)std::printf(",");first=false;
std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",rank,canSlow,state,percent,result!=nullptr,created.path.velocity,created.total,created.remaining,pathDistance,created.skill,created.owner==&owner);
}std::printf("]\n");}
'''
    STAGE.mkdir(parents=True,exist_ok=True);cpp=STAGE/'probe.cpp';cpp.write_text(base+services+body+'\n'+main)
    binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(cpp),'-o',str(binary)],check=True)
    cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==72
    for rank,can_slow,state,percent,created,velocity,total,remaining,distance,skill,owner in cases:
        expected=(12*256*(percent if can_slow and state else 100)//100)*75//100
        assert (created,velocity,total,remaining,distance,skill,owner)==(1,expected,77,77,77,24,1)
    path=STAGE/'cases.json';path.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],compiledRoutines=['MISSILES_CreateMissileFromParams','SKILLS_MissileInit_ChargedBolt'],
        factoryBlockSha256=shared['blockHashes']['MISSILES_CreateMissileFromParams'],initializerBlockSha256=hashlib.sha256(body.encode()).hexdigest(),
        cases=72,evidence={str(p.relative_to(ROOT)):sha(p)for p in (Path(__file__).resolve(),parent,scaffold,source,cpp,path)},
        established=['Flags33 source factory uses caster Slow Missiles only for CanSlow, truncates speed scaling before the75percent factor; 12velocity gives2304FP8 normally and1152at50percent.',
            'The unchanged Charged Bolt initializer caps the98frame row to77total/current frames and path distance77 after factory creation.'],
        limits=['Declared allocation/room/path/stat/damage/seed services;this executes both complete source bodies,not the current D2R executable or full path computation.',
            'Velocity and initialization only;modern Slow Missiles damage ordering,source collision/activation and full Charged Strike/class/client fidelity remain separate.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/charged-strike-velocity-source-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Charged Strike velocity:72 complete factory/initializer compositions,normal/slow/CanSlow controls and77frame cap pass.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
