#!/usr/bin/env python3
"""Execute complete block/dodge/weapon-block bodies with actual source RNG."""
import hashlib,json,random,re,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'source-avoidance-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={}
 def source(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 dmg=source('source/D2Game/src/UNIT/SUnitDmg.cpp');seed=source('source/D2Common/include/D2Seed.h');items=source('source/D2Game/src/ITEMS/Items.cpp');head=source('source/D2Game/include/UNIT/SUnitDmg.h')
 flags={n:int(v,16)for n,v in re.findall(r'^\s*(BLOCKFLAG_\w+)\s*=\s*(0x[0-9A-Fa-f]+)',head,re.M)};assert {n:flags['BLOCKFLAG_'+n]for n in ['BLOCK','AVOID','DODGE','EVADE','WEAPONBLOCK']}=={'BLOCK':1,'AVOID':2,'DODGE':4,'EVADE':8,'WEAPONBLOCK':16}
 header=source('source/D2Common/include/D2StatList.h');raw=re.search(r'enum D2C_ItemStats\s*\{(.*?)\};',header,re.S).group(1);raw=re.sub(r'/\*.*?\*/|//[^\n]*','',raw,flags=re.S);ids={};value=-1
 for part in raw.split(','):
  if not part.strip():continue
  name,separator,number=part.strip().partition('=');value=int(number.strip(),0)if separator else value+1;ids[name.strip()]=value
 assert [ids['STAT_PASSIVE_'+n]for n in ['DODGE','AVOID','EVADE','WEAPONBLOCK']]==[338,339,340,348]
 bodies={n:function(s,n)for s,names in [(seed,['SEED_RollRandomNumber','SEED_RollLimitedRandomNumber']),(items,['ITEMS_RollRandomNumber','ITEMS_RollLimitedRandomNumber']),(dmg,['SUNITDMG_GetWeaponBlock','SUNITDMG_ApplyDodge','SUNITDMG_ApplyBlockOrDodge'])]for n in names}
 cpp=r'''
#include <algorithm>
#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <iterator>
#include <vector>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,PLRMODE_WALK=2,PLRMODE_RUN=3,MONMODE_WALK=2,MONMODE_RUN=15,BODYLOC_RARM=4,BODYLOC_LARM=5,STAT_PASSIVE_WEAPONBLOCK=348,STAT_PASSIVE_DODGE=338,STAT_PASSIVE_AVOID=339,STAT_PASSIVE_EVADE=340;
struct D2SeedStrc{union{struct{uint32_t nLowSeed,nHighSeed;};uint64_t lSeed;};};
struct D2UnitStrc{int dwUnitType=1,dwAnimMode=1;D2SeedStrc pSeed{};void*pInventory=nullptr;int hand=0;};
struct D2GameStrc{bool bExpansion=true;}game;struct D2StatStrc{uint16_t nLayer=0;int nValue=0;};
D2UnitStrc actor,target,hands[2];int shield=0,composite=0;std::array<int,368>stats{};std::vector<D2StatStrc>layers;
int UNITS_GetBlockRate(D2UnitStrc*u,bool){return u?shield:0;}
bool UNITS_IsInMovingMode(D2UnitStrc*u){return u&&(u->dwAnimMode==2||u->dwAnimMode==3||u->dwAnimMode==15);}
int STATLIST_UnitGetStatValue(D2UnitStrc*u,int stat,int){return u?stats[stat]:0;}
int STATLIST_CopyStats(D2UnitStrc*,int stat,D2StatStrc*out,size_t cap){assert(stat==348&&cap==32);int n=std::min<int>(layers.size(),cap);for(int i=0;i<n;i++)out[i]=layers[i];return n;}
D2UnitStrc*INVENTORY_GetItemFromBodyLoc(void*,int body){assert(body==4||body==5);auto*p=&hands[body==4?0:1];return p->hand?p:nullptr;}
bool ITEMS_CheckItemTypeId(D2UnitStrc*u,int type){return u&&type>0&&type<=8&&(u->hand&(1<<(type-1)));}
void COMPOSIT_GetWeaponClassId(D2UnitStrc*,void*,int*out,int,int){*out=composite;}
'''+ '\n'.join('constexpr int '+n+'='+str(v)+';'for n,v in flags.items())+'\n'+'\n'.join(b.replace('0x6AC690C5i64','0x6AC690C5ULL')for b in bodies.values())+r'''
int main(){int count;assert(scanf("%d",&count)==1);printf("[");for(int i=0;i<count;i++){
int a[12],n;unsigned long long before;for(int&v:a)assert(scanf("%d",&v)==1);assert(scanf("%llu%d",&before,&n)==2);layers.clear();
for(int j=0;j<n;j++){int layer,value;assert(scanf("%d%d",&layer,&value)==2);layers.push_back({uint16_t(layer),value});}
target={};target.dwUnitType=a[1];target.dwAnimMode=a[2];target.pSeed.lSeed=before;target.pInventory=a[9]?&target:nullptr;shield=a[3];composite=a[10];hands[0].hand=a[7];hands[1].hand=a[8];stats.fill(0);stats[338]=a[4];stats[339]=a[5];stats[340]=a[6];
auto*t=a[0]?&target:nullptr;int chance=SUNITDMG_GetWeaponBlock(t);int result=SUNITDMG_ApplyBlockOrDodge(&game,&actor,t,a[11]&1,(a[11]>>1)&1);
printf("%s[%d,%d,\"%llu\"]",i?",":"",chance,result,(unsigned long long)target.pSeed.lSeed);}printf("]\n");}
'''
 for n,b in bodies.items():assert function(cpp,n)==b.replace('0x6AC690C5i64','0x6AC690C5ULL'),n
 rng=random.Random(0x6fcc2610);cases=[]
 patterns=[[],[[1,60]],[[1,60],[0,20]],[[0,60],[1,20]],[[1,60],[0,-2]],[[65535,100]],[[i+1,i+1]for i in range(40)],[[0,0],[1,75],[0,0]],[[1,i+1]for i in range(40)],[[1,60]]*32+[[0,99]]]
 for i in range(16384):
  a=[int(i%31!=0),rng.choice([0,1,2]),rng.choice([0,1,2,3,4,15]),rng.choice([-5,0,1,28,75,120]),*[rng.choice([-2,0,1,29,75,100,120])for _ in range(3)],rng.randrange(256),rng.randrange(256),rng.randrange(2),rng.choice([0,13]),rng.randrange(4)]
  cases.append(dict(Args=a,Seed=str((rng.randrange(1<<32)<<32)|rng.randrange(1<<32)),Layers=patterns[i%len(patterns)]))
 numbers=[len(cases)]
 for c in cases:numbers+=c['Args']+[int(c['Seed']),len(c['Layers'])]+[v for pair in c['Layers']for v in pair]
 request=STAGE/'input.txt';request.write_text(' '.join(map(str,numbers))+'\n');path=STAGE/'source.cpp';path.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(path),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],input=request.read_bytes(),capture_output=True,timeout=60);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 output=STAGE/'output.json';output.write_bytes(run.stdout);rows=json.loads(run.stdout);assert len(rows)==len(cases)
 for c,r in zip(cases,rows):c['WeaponBlock']=r[0];c['Result']=r[1];c['After']=r[2]
 out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 proof=dict(referenceVersion='3.3.93847',currentExecutableParity=False,contexts=len(cases),completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},inputs=inputs,
  scope='Three complete older shield/dodge/weapon-block bodies plus four complete seed/item RNG functions,with compiler-only i64literal normalization. Supplied kind/mode,block rate,stat array,13slot inventory/hand-type and composite services;actual NPC world mode and native reaction are separate.',
  artifacts={str(p.relative_to(ROOT)):sha(p)for p in [path,binary,request,output,out,STAGE/'compiler.log',STAGE/'sanitizer.log',Path(__file__).resolve()]})
 (ROOT/'tools/reference/source-avoidance-source-evidence.json').write_text(json.dumps(proof,indent=2)+'\n');print('Source avoidance:16384contexts,seven complete routines,actual seed continuation;ASan/UBSan clean.')
if __name__=='__main__':main()
