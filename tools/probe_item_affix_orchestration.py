#!/usr/bin/env python3
"""Execute complete magic/rare outer orchestration with observed picker/property services."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'item-affix-orchestration-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={};bodies={}
    for rel,names in {'source/D2Game/src/ITEMS/Items.cpp':['sub_6FC4D5E0'],'source/D2Game/src/ITEMS/ItemsMagic.cpp':['D2GAME_RollRareItem_6FC53360']}.items():
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p)
        for n in names:bodies[n]=function(raw.decode(),n)
    cpp=r'''
#include <cstdint>
#include <cstdio>
#include <cassert>
#include <initializer_list>
#include <vector>
#define __fastcall
#define D2_ASSERT(x) assert(x)
constexpr int ITEMS_MAX_MODS=3,ITEMTYPE_JEWEL=1,IFLAG_IDENTIFIED=16;
struct D2SeedStrc{unsigned value=0;}seed;
struct D2UnitStrc{int prefix[3]={},suffix[3]={},rarePrefix=0,rareSuffix=0,flags=16;}item;
struct D2ItemDropStrc{int nPrefix[3]={},nSuffix[3]={};}drop;
struct D2MagicAffixTxt{int id=0;}records[400];
bool canRare=true,jewel=false,hasRecords=true;int format=100,nameMask=3,prefixLimit=3,suffixLimit=3,pc=0,sc=0,randoms=0;
std::vector<int>trace;
void T(std::initializer_list<int> x){trace.insert(trace.end(),x.begin(),x.end());}
bool ITEMS_CanBeRare(D2UnitStrc*){T({1});return canRare;}
unsigned ITEMS_GetItemFormat(D2UnitStrc*){T({2});return format;}
int sub_6FC53760(D2UnitStrc*,D2ItemDropStrc*){T({3});return 7;}
int16_t D2GAME_RollRareAffix_6FC53610(D2UnitStrc*,int p){T({4,p});return nameMask&(p?1:2)?p?11:22:0;}
void ITEMS_AssignRarePrefix(D2UnitStrc*p,int id){p->rarePrefix=id;T({5,1,id});}void ITEMS_AssignRareSuffix(D2UnitStrc*p,int id){p->rareSuffix=id;T({5,0,id});}
D2SeedStrc* ITEMS_GetItemSeed(D2UnitStrc*){return &seed;}
int ITEMS_GetItemType(D2UnitStrc*){T({6});return jewel?1:0;}
unsigned ITEMS_RollRandomNumber(D2SeedStrc*p){unsigned n=(p->value+randoms++)&15;T({7,(int)n});return n;}
unsigned ITEMS_RollLimitedRandomNumber(D2SeedStrc*p,int bound){auto n=ITEMS_RollRandomNumber(p)%bound;T({8,bound,(int)n});return n;}
int16_t Pick(int spawn,int force,int properties,int prefix,int preferred){T({9,spawn,force,properties,prefix,preferred});int&count=prefix?pc:sc;int limit=prefix?prefixLimit:suffixLimit;return count++<limit?(prefix?101:201)+count-1:0;}
int16_t ITEMS_RollMagicAffixes(D2UnitStrc*,int a,int b,int c,int d,int e){return Pick(a,b,c,d,e);}
int16_t ITEMS_RollMagicAffixesNew(D2UnitStrc*,int a,int b,int c,int d,int e,int z){assert(z==0);return Pick(a,b,c,d,e);}
void ITEMS_AssignPrefix(D2UnitStrc*p,int id,int index){p->prefix[index]=id;T({10,1,index,id});}void ITEMS_AssignSuffix(D2UnitStrc*p,int id,int index){p->suffix[index]=id;T({10,0,index,id});}
int ITEMS_GetPrefixId(D2UnitStrc*p,int i){return p->prefix[i];}int ITEMS_GetSuffixId(D2UnitStrc*p,int i){return p->suffix[i];}
void ITEMS_SetItemFlag(D2UnitStrc*p,int flag,int enabled){assert(flag==16&&enabled==0);p->flags&=~flag;T({11});}
D2MagicAffixTxt* DATATBLS_GetMagicAffixTxtRecord(int id){T({12,id});records[id].id=id;return id&&hasRecords?&records[id]:nullptr;}
void ITEMMODS_AssignProperty(int z,void*n,D2UnitStrc*,D2MagicAffixTxt*r,int a,int b){assert(z==0&&n==nullptr&&a==0&&b==0);T({13,r->id});}
void sub_6FC52410(D2UnitStrc*,D2ItemDropStrc*){T({14});}
'''+ '\n'.join(bodies.values())+r'''
void list(const std::vector<int>& a){printf("[");for(size_t i=0;i<a.size();i++)printf("%s%d",i?",":"",a[i]);printf("]");}
int main(){bool comma=false;printf("[");
for(int mode:{0,1})for(int can:{0,1})for(int version:{0,100})for(int names:{0,1,2,3})for(int j:{0,1})for(int p:{0,1,3})for(int s:{0,1,3})for(int draw:{0,1,7,15})for(int preferred:{-1,0,9})for(int preferredSuffix:{-1,0,9})for(int recordsPresent:{0,1}){
 item={};drop={};canRare=can;format=version;nameMask=names;jewel=j;prefixLimit=p;suffixLimit=s;seed.value=draw;pc=sc=randoms=0;hasRecords=recordsPresent;trace.clear();
 for(int i=0;i<3;i++){drop.nPrefix[i]=preferred+i;drop.nSuffix[i]=preferredSuffix+i;}D2UnitStrc*ptr=&item;int result=mode?D2GAME_RollRareItem_6FC53360(ptr,&drop):sub_6FC4D5E0(&ptr,&drop);
 if(comma)printf(",");comma=true;printf("[");list({mode,can,version,names,j,p,s,draw,preferred,preferredSuffix,recordsPresent});printf(",");list({result,item.flags,item.rarePrefix,item.rareSuffix,item.prefix[0],item.prefix[1],item.prefix[2],item.suffix[0],item.suffix[1],item.suffix[2],pc,sc,randoms});printf(",");list(trace);printf("]");
}printf("]\n");}
'''
    generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
    with (STAGE/'compiler.log').open('w') as out:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=out,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
    data=json.loads(run.stdout);assert len(data)==41472,len(data)
    # Independent final-state invariants; native control-flow/trace comparison
    # supplies the full service-order oracle rather than a second Python clone.
    for a,r,trace in data:
        mode,can,version,names,j,p,s,draw,preferred,preferredSuffix,recordsPresent=a
        if mode and not can:assert r==[0,16,0,0,0,0,0,0,0,0,0,0,0] and trace==[1]
        elif mode and version==0:assert r[0]==7 and trace==[1,2,3]
        elif mode and names!=3:assert r[0]==0 and trace==[1,2,4,1,4,0]
        elif mode:
            assert r[0]==int(p+s>0) and sum(v!=0 for v in r[4:10])<=6 and r[10]<=3 and r[11]<=3
            assert (trace[-1]==14)==bool(r[0]) and r[1]==(0 if r[0] else 16)
        else:
            assert r[0]==int((preferred>=0 and p>0) or s>0) and r[1]==(0 if r[0] else 16) and r[12]==0
    cases=STAGE/'cases.json';cases.write_text(json.dumps(data,separators=(',',':'))+'\n')
    e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableCodeParity=False,cases=41472,
        compiledUnchangedRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},inputs=inputs,sanitizers='ASan+UBSan clean',
        artifacts={str(q.relative_to(ROOT)):sha(q) for q in (generated,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',cases,Path(__file__).resolve())},
        established=['Complete magic outer caller preserves preferred-prefix negative/zero/positive branches,forced suffix after missing prefix,assignment order,identification clearing and final initializer only on success.',
                     'Complete rare outer caller checks rare eligibility/format,rolls both name components before validating them,selects3..6ordinary or3..4jewel affixes,caps3per side,retries the other side after empty pools and applies selected properties only afterward in prefix/suffix interleaved slot order.',
                     'Rare selection requests bAssignProperties0;magic requests1. Property RNG/effects therefore belong inside magic picker calls but after complete rare selection. No player GearRoll orchestration is substituted.'],
        services=['Names,picker,property-record lookup/property application,legacy-format rare path and final initializer are observed services. The supplied picker models available prefix/suffix counts and records every spawn/force/property/preferred argument;it is not full pool/weighted/property execution.',
                  'Outer RNG returns a bounded counter stream;the engine SourceRandom/current item seed allocation and name RNG remain independent dependencies. All items start with empty prefix/suffix slots and identified flag set. No native item or Shadow actor is produced.'],
        remaining=['Connect current full affix pools/name generation/property writing/final item initialization and quality fallback on an actual generated NPC item graph. Preserve unsupported properties explicitly rather than filtering.',
                   'Current executable parity,item-generation seed allocation,source-quality fallback,superior quality,normal/special bases,actual NPC inventory/combat/casting/lifecycle/input/saves and final assets remain open.'])
    out=ROOT/'tools/reference/item-affix-orchestration-source-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Item affix source evidence stale'
    else:out.write_text(encoded)
    print('Item affix orchestration:41472 complete magic/rare outer cases;service order/caps/retry/flags/property phases;ASan+UBSan clean;full generation remains separate.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
