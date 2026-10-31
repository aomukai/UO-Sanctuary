#!/usr/bin/env python3
"""Execute the complete older FIFO append helper; pin the shared current golem pet type."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    relative='source/D2Game/src/PLAYER/PlayerPets.cpp';p=BASE/'d2moo-pinned-source'/relative;raw=p.read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
    body=function(raw.decode(),'sub_6FC7DA40');reviews={name:hashlib.sha256(function(raw.decode(),name).encode()).hexdigest() for name in ['sub_6FC7D150','sub_6FC7D7A0']}
    reference=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((reference/'sources.json').read_text());pins={};rows=[]
    for name in ['skills','pettype']:
        path=reference/'json'/(name+'.json');assert sha(path)==manifest['files']['json/'+name+'.json']['sha256'];pins[name]=sha(path)
    skills=json.loads((reference/'json/skills.json').read_text())
    for id in ['75','85','90','94']:
        row=skills[id];assert row['pettype']=='golem' and row['petmax']==1;rows.append({k:row[k] for k in ['skill','*Id','pettype','petmax','summon']})
    pet=next(row for row in json.loads((reference/'json/pettype.json').read_text()).values() if row.get('pet type')=='golem')
    assert not pet.get('group') and pet['lineNumber']==3
    cpp=r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <memory>
#include <vector>
#define __fastcall
#define D2_ASSERT(x) assert(x)
struct D2GameStrc{void*pMemoryPool=nullptr;}game;
struct D2UnitStrc{int dwUnitId=99;}owner,candidate;
struct D2PetInfoStrc{int nSeed=123,wName=45,unk0x06=6,nHirelingId=7;}info;
struct D2PetDataStrc{int nUnitGUID=0;D2PetInfoStrc petInfo;D2PetDataStrc*pNext=nullptr;};
struct D2PetListStrc{int nMax=1,nCurrent=0;D2PetDataStrc*pPetData=nullptr;};
std::vector<std::unique_ptr<D2PetDataStrc>> nodes;std::vector<int>removed;int fallback,refreshes,rejected;
D2PetDataStrc*allocate(){nodes.push_back(std::make_unique<D2PetDataStrc>());return nodes.back().get();}
#define D2_CALLOC_STRC_POOL(pool,type) allocate()
D2PetListStrc*active;
void sub_6FC7DF40(D2GameStrc*,D2UnitStrc*){refreshes++;active->nMax=fallback;}
void sub_6FC7CF50(D2GameStrc*,int id){rejected=id;}
void PLAYERPETS_RemovePetFromList(D2GameStrc*,D2PetListStrc*p,int id,int kill){assert(kill==1&&p->pPetData&&p->pPetData->nUnitGUID==id);removed.push_back(id);p->pPetData=p->pPetData->pNext;p->nCurrent--;}
'''+body+r'''
int main(){printf("[");bool first=true;
for(int maximum:{0,1,2,5})for(int count=0;count<=maximum;count++)for(int exists:{0,1})for(int recovered:{0,1}){
nodes.clear();removed.clear();refreshes=0;rejected=0;fallback=recovered;D2PetListStrc list{maximum,count,nullptr};active=&list;
D2PetDataStrc*tail=nullptr;for(int i=0;i<count;i++){auto*n=allocate();n->nUnitGUID=10+i;if(tail)tail->pNext=n;else list.pPetData=n;tail=n;}
int result=sub_6FC7DA40(&game,&owner,&list,exists?&candidate:nullptr,&info);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",maximum,count,exists,recovered,result,list.nMax,list.nCurrent,refreshes,rejected);first=false;
for(unsigned i=0;i<removed.size();i++)printf("%s%d",i?",":"",removed[i]);printf("],[");bool sep=false;
for(auto*n=list.pPetData;n;n=n->pNext){printf("%s[%d,%d,%d,%d,%d]",sep?",":"",n->nUnitGUID,n->petInfo.nSeed,n->petInfo.wName,n->petInfo.unk0x06,n->petInfo.nHirelingId);sep=true;}printf("]]");}printf("]\n");}
'''
    generated=BASE/'golem-roster-source-probe.cpp';generated.write_text(cpp);binary=generated.with_suffix('')
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(generated),'-o',str(binary)],check=True)
    process=subprocess.run([str(binary)],capture_output=True);(BASE/'golem-roster-sanitizer.log').write_bytes(process.stderr)
    assert process.returncode==0 and not process.stderr,process.stderr.decode()[-3000:]
    cases=json.loads(process.stdout);assert len(cases)==48
    for maximum,count,exists,recovered,result,limit,current,refresh,reject,removed,queue in cases:
        effective=maximum or recovered;assert limit==effective and refresh==int(maximum==0)
        if effective==0:assert result==0 and current==0 and reject==(99 if exists else -1) and removed==[] and queue==[]
        else:
            ids=list(range(10,10+count));expected_removed=[]
            if count==effective:expected_removed=[ids.pop(0)]
            expected=[[id,123,45,6,7] for id in ids]+[[99 if exists else -1,123,45,6,7]]
            assert result==1 and current==len(expected) and removed==expected_removed and queue==expected
    fixture=BASE/'golem-roster-source-probe.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceFile=relative,sourceSha256=sha(p),
        executedRoutineSha256=hashlib.sha256(body.encode()).hexdigest(),staticReviewedRoutineHashes=reviews,currentTableHashes=pins,currentSkills=rows,currentPetType=pet,
        generatedSourceSha256=sha(generated),fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=sha(fixture),cases=48),sanitizers='ASan+UBSan exit0 with empty runtime stderr',
        established=['All four current golems share pettype=golem and petmax1. Complete older append helper removes the head at capacity,appends new entries at the tail and copies pet info. Empty maximum refresh/rejection and null-pet controls are included;only positive owned candidates apply to the native golem consumer.',
            'The statically reviewed registration routine sets the cap before adding;the cap setter removes old head entries while over maximum. Pet destruction/group/client/allocation operations are supplied services rather than full actor execution.'],
        remaining=['Native actor cleanup,original-item consumption,payment/placement failure,parked/lifecycle/save/birth-order handling and category coexistence need actual integration tests. Historical untracked native FIFO cannot be reconstructed;any deterministic fallback must be identified.',
            'No learned Iron Golem or class completion is enabled by this source proof. Complete current executable,death/explosion events,casting and combat parity remain separate.'])
    path=ROOT/'tools/reference/golem-roster-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Golem roster:48 complete older FIFO append contexts pass ASan+UBSan;four current skills share cap1/typegolem;native integration remains separate.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
