#!/usr/bin/env python3
"""Execute complete older pet cap-set/append bodies with current skeleton caps."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'skeleton-roster-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    relative='source/D2Game/src/PLAYER/PlayerPets.cpp';source=BASE/'d2moo-pinned-source'/relative;raw=source.read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
    parent=BASE/'golem-roster-source-probe.cpp';receipt=json.loads((ROOT/'tools/reference/golem-roster-source-evidence.json').read_text());assert sha(parent)==receipt['generatedSourceSha256']
    cpp=parent.read_text().split('int main(){')[0];append=function(raw.decode(),'sub_6FC7DA40');cap=function(raw.decode(),'sub_6FC7D150');assert append in cpp
    refs=ROOT/'tools/reference/d2r-named-equipment/json';skills=json.loads((refs/'skills.json').read_text());pets=json.loads((refs/'pettype.json').read_text());current=[]
    for id,kind in [('70','skeleton'),('80','skeletonmage')]:
        row=skills[id];assert row['pettype']==kind and row['petmax']=='(lvl < 4) ?lvl:(2+lvl/3)'
        pet=next(x for x in pets.values() if x.get('pet type')==kind);assert not pet.get('group');current.append({k:row[k] for k in ['*Id','skill','pettype','petmax']})
    cpp+='''
constexpr int PETTYPE_SINGLE=-1,PETTYPE_HIREABLE=-2;
struct D2PlayerDataStrc{void*pPlayerPets=&game;}data;
D2PlayerDataStrc*UNITS_GetPlayerData(D2UnitStrc*){return &data;}
D2PetListStrc*PLAYERPETS_GetPetListFromPetType(void*,int type){assert(type==4);return active;}
void sub_6FC7D260(D2GameStrc*g,D2UnitStrc*,int id,int kill){PLAYERPETS_RemovePetFromList(g,active,id,kill);}
'''+cap+'''
int main(){printf("[");bool first=true;candidate.dwUnitId=99999;
for(int rank=1;rank<=255;rank++){int maximum=rank<4?rank:2+rank/3;
for(int count:{0,maximum-1,maximum,maximum+1,maximum+3,99}){
nodes.clear();removed.clear();D2PetListStrc list{100,count,nullptr};active=&list;
D2PetDataStrc*tail=nullptr;for(int i=0;i<count;i++){auto*n=allocate();n->nUnitGUID=1000-i;if(tail)tail->pNext=n;else list.pPetData=n;tail=n;}
sub_6FC7D150(&game,&owner,4,maximum);int result=sub_6FC7DA40(&game,&owner,&list,&candidate,&info);assert(result==1);
printf("%s[%d,%d,%d,[",first?"":",",rank,count,list.nMax);first=false;
for(unsigned i=0;i<removed.size();i++)printf("%s%d",i?",":"",removed[i]);printf("],[");bool sep=false;
for(auto*n=list.pPetData;n;n=n->pNext){printf("%s%d",sep?",":"",n->nUnitGUID);sep=true;}printf("]]");}}printf("]\\n");}
'''
    generated=STAGE/'probe.cpp';generated.write_text(cpp);binary=STAGE/'probe'
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(generated),'-o',str(binary)],capture_output=True)
    (STAGE/'compiler.log').write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
    rows=json.loads(run.stdout);assert len(rows)==1530
    for rank,count,maximum,removed,queue in rows:
        assert maximum==(rank if rank<4 else 2+rank//3)
        old=list(range(1000,1000-count,-1));n=max(0,count+1-maximum)
        assert removed==old[:n] and queue==old[n:]+[99999]
    fixture=STAGE/'cases.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceFile=relative,sourceSha256=sha(source),
        completeRoutines={n:hashlib.sha256(b.encode()).hexdigest() for n,b in [('sub_6FC7D150',cap),('sub_6FC7DA40',append)]},currentSkills=current,
        inputs={str(p.relative_to(ROOT)):sha(p) for p in [parent,refs/'skills.json',refs/'pettype.json']},cases=1530,
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [generated,fixture,STAGE/'compiler.log',STAGE/'sanitizer.log']},
        scope='Complete unchanged cap-setter and FIFO append;allocation/removal/player lookup are explicit services. All255current ranks and six count contexts use reversed GUID order.',
        established=['Cap changes remove head entries until within maximum;append at capacity removes the next head and appends the new candidate at the tail. List order,not GUID,selects replacements.',
            'Current Raise Skeleton and Raise Skeletal Mage have separate ungrouped pet types and identical rank caps1..87. Their source registration sets the cap before appending.'],
        remaining=['Actor death/network/allocation failure,current executable equivalence and original historical birth order are outside these supplied services. Native integration and persistence require separate evidence.'])
    path=ROOT/'tools/reference/skeleton-roster-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Skeleton roster:1530complete cap-set/append cases;255current ranks,separate families and non-GUID FIFO;clean sanitizers.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
