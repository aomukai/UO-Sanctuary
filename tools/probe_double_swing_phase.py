#!/usr/bin/env python3
"""Execute unchanged do70 and its complete GUID search wrapper/callback."""
import argparse,hashlib,itertools,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'double-swing-phase'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def cases():
    groups=[[],[None],[0],[1],[1,1],[5,0,1],[1,0,5],[4294967295,2147483648,0],[None,4294967295,1]]
    groups += [list(p) for n in range(1,4) for p in itertools.permutations([0,1,5,4294967295],n)]
    def params(**kw):
        p=dict(frame=0,flags=0,present=True,anchor=1,range=3,prepare=1,rank=1,skill=133,report=False,count=777,filter=0x20003,x=0,y=0,candidates=[]);p.update(kw);return p
    for group,anchor,report,mask in itertools.product(groups,[0,1,5,2147483648,4294967295],[False,True],[0,0x20003,4294967295]):
        yield 'find',params(anchor=anchor,report=report,filter=mask,x=-3,y=7,candidates=group)
    for frame,present,anchor,reach,prepare,group,flags in itertools.product([0,255,256,511,512,768,-1],[False,True],[0,5,2147483648,4294967295],[0,3],[-7,0,1],groups[:9],[0,4294967295]):
        yield 'phase',params(frame=frame,present=present,anchor=anchor,range=reach,prepare=prepare,candidates=group,flags=flags)
    for frame,rank,skill in itertools.product([-2147483648,-2147483392,2147483647],[0,20,255],[133,406]):
        yield 'phase',params(frame=frame,rank=rank,skill=skill,candidates=[0,5,2147483648])
def main(check=False):
    STAGE.mkdir(exist_ok=True)
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    sources={};routines={}
    def body(relative,name):
        p=BASE/'d2moo-pinned-source'/relative;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        sources[relative]=sha(p);text=function(raw.decode(),name);routines[name]=hashlib.sha256(text.encode()).hexdigest();return text
    skills='source/D2Game/src/SKILLS/Skills.cpp';bar='source/D2Game/src/SKILLS/SkillBar.cpp'
    for relative,anchors in [
        ('source/D2Common/include/Units/Units.h',['UNITFLAG_SKSRVDOFUNC = 0x00000040','int32_t dwSeqFrame;']),
        ('source/D2Game/include/SKILLS/Skills.h',['int32_t unk0x04;','int32_t unk0x0C;','int32_t unk0x10;','int32_t unk0x14;'])]:
        p=BASE/'d2moo-pinned-source'/relative;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        assert all(anchor in raw.decode() for anchor in anchors)
        sources[relative]=sha(p)
    cpp=r'''#include <cstdint>
#include <cstdio>
#include <iostream>
#include <vector>
#include <cstring>
#define __fastcall
struct D2GameStrc{};
struct D2UnitStrc{int32_t dwUnitId,slot,dwSeqFrame;uint32_t dwFlags;};
struct D2AuraCallbackStrc{D2GameStrc* pGame;D2UnitStrc* pOwner;int32_t nCounter;void* pArgs;};
struct D2_6FD10880_Strc{D2UnitStrc* unk0x00;int32_t unk0x04;D2UnitStrc* unk0x08;int32_t unk0x0C,unk0x10,unk0x14;};
constexpr uint32_t UNITFLAG_SKSRVDOFUNC=0x40;
std::vector<std::vector<int64_t>> trace;
std::vector<D2UnitStrc*> eligible;
D2UnitStrc* target=nullptr;
int reach=0,prepared=0;
int slot(D2UnitStrc* p){return p?p->slot:0;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){trace.push_back({1,slot(target)});return target;}
int32_t UNITS_GetMeleeRange(D2UnitStrc*){trace.push_back({2,reach});return reach;}
void UNITS_SetTargetUnitForDynamicUnit(D2UnitStrc*,D2UnitStrc* p){trace.push_back({4,slot(p)});target=p;}
int32_t SKILLS_SrvSt32_Conversion_Bash_Stun_Concentrate_BearSmite(D2GameStrc*,D2UnitStrc*,int32_t skill,int32_t rank){trace.push_back({5,skill,rank,slot(target),prepared});return prepared;}
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc* p,int32_t zero){trace.push_back({6,slot(p),zero});}
void sub_6FD0FE80(D2GameStrc* game,D2UnitStrc* owner,int32_t x,int32_t y,int32_t range,uint32_t filter,int32_t(*callback)(D2AuraCallbackStrc*,D2UnitStrc*),void* args,int32_t zero,const char*,int32_t){
 auto a=(D2_6FD10880_Strc*)args;trace.push_back({3,x,y,range,filter,(uint32_t)a->unk0x10});
 D2AuraCallbackStrc cb{game,owner,0,args};for(auto p:eligible)callback(&cb,p);
}
int32_t sub_6FD10880(D2AuraCallbackStrc*,D2UnitStrc*);
'''
    for relative,name in [(skills,'sub_6FD10880'),(skills,'sub_6FD107F0'),(skills,'sub_6FD15460'),(bar,'SKILLS_SrvDo070_DoubleSwing')]:cpp+=body(relative,name)+'\n'
    cpp+=r'''int main(){D2GameStrc game;bool first=true;printf("[");
int family,frame,present,rank,skill,report,count,x,y,n;uint32_t flags,anchor,filter;
while(std::cin>>family>>frame>>flags>>present>>anchor>>reach>>prepared>>rank>>skill>>report>>count>>filter>>x>>y>>n){
 D2UnitStrc actor{999,999,frame,flags},initial{(int32_t)anchor,1,0,0};std::vector<D2UnitStrc> units(n);eligible.clear();trace.clear();target=present?&initial:nullptr;
 for(int i=0;i<n;i++){int exists;uint32_t id;std::cin>>exists>>id;units[i]={(int32_t)id,i+2,0,0};eligible.push_back(exists?&units[i]:nullptr);}
 int result=family?SKILLS_SrvDo070_DoubleSwing(&game,&actor,skill,rank):slot(sub_6FD107F0(&game,&actor,x,y,reach,filter,(int32_t)anchor,report?&count:nullptr));
 printf("%s[%d,%u,%d,%d,[",first?"":",",result,actor.dwFlags,slot(target),count);first=false;
 for(size_t i=0;i<trace.size();i++){printf("%s[",i?",":"");for(size_t j=0;j<trace[i].size();j++)printf("%s%lld",j?",":"",(long long)trace[i][j]);printf("]");}printf("]]");
}printf("]\n");}
'''
    path=STAGE/'source.cpp';path.write_text(cpp)
    compiler=STAGE/'source-compiler.log'
    with compiler.open('w') as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(STAGE/'source')],check=True,stdout=log,stderr=subprocess.STDOUT)
    params=list(cases());lines=[]
    for family,p in params:
        values=[int(family=='phase'),p['frame'],p['flags'],int(p['present']),p['anchor'],p['range'],p['prepare'],p['rank'],p['skill'],int(p['report']),p['count'],p['filter'],p['x'],p['y'],len(p['candidates'])]
        for value in p['candidates']:values.extend([int(value!=None),value or 0])
        lines.append(' '.join(map(str,values)))
    run=subprocess.run([str(STAGE/'source')],input=('\n'.join(lines)+'\n').encode(),capture_output=True);sanitizer=STAGE/'source-sanitizer.log';sanitizer.write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
    results=json.loads(run.stdout);assert len(results)==len(params)
    rows=[[family,p,result] for (family,p),result in zip(params,results)];encoded=json.dumps(rows,separators=(',',':'))+'\n'
    fixture=BASE/'double-swing-phase-source-probe.json';out=ROOT/'tools/reference/double-swing-phase-source-evidence.json'
    proof=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,handlerEnabled=False,
        cases=len(rows),byFamily={f:sum(row[0]==f for row in rows) for f in ['find','phase']},sourceHashes=sources,completeRoutineHashes=routines,
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__).resolve(),path,compiler,sanitizer]},
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(encoded.encode()).hexdigest()),
        limits=['Complete legacy do70/search wrapper/callback execute with explicit target/aura enumeration/start32/durability services;modern start68,actual native target filtering/room/hand/packet timing and learned input/payment remain unverified.',
            'The selector chooses the smallest unsigned GUID greater than its anchor,otherwise wraps to the smallest eligible GUID. A lone original target can be returned. Optional count is written only on the preferred branch. Duplicate/null and negative-frame controls include synthetic cases.'])
    text=json.dumps(proof,indent=2)+'\n'
    if check:assert fixture.read_text()==encoded and out.read_text()==text
    else:fixture.write_text(encoded);out.write_text(text)
    print('Double Swing release/GUID selection:',len(rows),'complete legacy cases;',proof['byFamily'],';ASan/UBSan clean;modern start remains gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
