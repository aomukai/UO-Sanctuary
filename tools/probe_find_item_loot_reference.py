#!/usr/bin/env python3
"""Compile the unchanged players1 NoDrop branch against current native TC rows."""
from pathlib import Path
import hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
blobs={x['path']:x['sha'] for x in tree['tree'] if x['type']=='blob'};hashes={};blocks={}
def source(p):
 raw=(STAGE/'d2moo-pinned-source'/p).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==blobs[p]
 hashes[p]=hashlib.sha256(raw).hexdigest();return raw.decode()
def routine(s,n):
 m=re.search(r'^.*\b'+re.escape(n)+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,n
 e=s.index('{',m.start())+1;d=1
 while d:d+=(s[e]=='{')-(s[e]=='}');e+=1
 blocks[n]=s[m.start():e];return blocks[n]
items=source('source/D2Game/src/ITEMS/Items.cpp');monster=source('source/D2Game/src/MONSTER/MonsterMode.cpp');skills=source('source/D2Game/src/SKILLS/Skills.cpp')
body=routine(items,'D2GAME_DropTC_6FC51360');routine(items,'sub_6FC52110');routine(monster,'sub_6FC631B0');routine(monster,'sub_6FC659B0');routine(skills,'sub_6FD12950');routine(skills,'D2GAME_SKILLS_Handler_6FD12BA0')
chance=body[body.index('                int32_t nNoDrop ='):body.index('\n            }\n\n            --tcArray',body.index('                int32_t nNoDrop ='))]
tables={};tableHashes={}
for suffix in ['', '-nightmare','-hell']:
 path=ROOT/('data/Sanctuary/Balance/creature-gold'+suffix+'.json');tableHashes[str(path.relative_to(ROOT))]=hashlib.sha256(path.read_bytes()).hexdigest()
 for row in json.loads(path.read_text())['classes']:
  assert row['picks']==1 and sum(e['weight'] for e in row['entries'])>0
  assert row['name'] not in tables or tables[row['name']]==row
  tables[row['name']]=row
rows=sorted(tables.values(),key=lambda x:x['name'])
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
using std::int32_t;using std::uint32_t;constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,STAT_MONSTER_PLAYERCOUNT=0;
struct Unit{int dwUnitType=1,pSeed=0;} monster;using D2UnitStrc=Unit;Unit* pMonster=&monster;Unit* pPlayer=nullptr;struct Game{} game;Game* pGame=&game;
struct TC{int nNoDrop,nProb;};struct Slot{int nPicks=1;};int draw=0,lastBound=0;
Unit* AIGENERAL_GetMinionOwner(Unit*){return nullptr;}int PARTY_GetLivingPartyMemberCountInSameLevel(Game*,Unit*){return 1;}int PLAYER_GetPlayerCount(Game*){return 1;}int STATLIST_UnitGetStatValue(Unit*,int,int){return 1;}
int ITEMS_RollLimitedRandomNumber(int*,int bound){lastBound=bound;return std::clamp(draw,0,bound-1);}
int Roll(TC* pTC,int a7){int nChance=pTC->nProb,nRand=0,nCounter=0;Slot tcArray[1];for(int once=0;once<1;once++){
'''+chance+'\nreturn nRand;}return -1;}\n'
cpp+='TC rows[]={'+','.join('{'+str(r['noDrop'])+','+str(sum(e['weight'] for e in r['entries']))+'}' for r in rows)+'};\n'
cpp+=r'''int main(){bool first=true;std::printf("[");for(int i=0;i<(int)(sizeof(rows)/sizeof(rows[0]));i++){auto& r=rows[i];for(int flag:{0,1})for(int value:{0,1,r.nProb-1,r.nProb,r.nNoDrop-1,r.nNoDrop,r.nProb+r.nNoDrop-1,999999}){draw=value;int result=Roll(&r,flag);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d]",i,flag,value,result,lastBound);}}std::printf("]\n");}
'''
path=STAGE/'find-item-loot-source-probe.cpp';path.write_text(cpp);binary=STAGE/'find-item-loot-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]))
for i,flag,draw,result,bound in data:
 row=rows[i];weight=sum(e['weight'] for e in row['entries']);noDrop=0 if flag else row['noDrop'];value=max(0,min(draw,weight+noDrop-1))-noDrop
 assert (result,bound)==(max(-1,value),weight+noDrop)
(STAGE/'find-item-loot-source-probe.json').write_text(json.dumps(dict(tables=rows,cases=data),indent=2)+'\n')
out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],status='Compiled players1 NoDrop branch; native existing weighted-selector preparation checks separate.',sourceHashes=hashes,probe=dict(cases=len(data),tables=len(rows),tableHashes=tableHashes,compiledBlockHash=hashlib.sha256(chance.encode()).hexdigest(),reviewedRoutineHashes={n:hashlib.sha256(s.encode()).hexdigest() for n,s in blocks.items()},dataPath='.local/class-skills/find-item-loot-source-probe.json'),established=['Find Item wrapper forwards a7=1 to ordinary corpse/source TC selection; TC loop suppresses NoDrop at every nested row while preserving entry weights. The selected quality parameter in do72 is ignored by the reviewed wrapper; do not infer forced quality from that draw.','Current ordinary native TC graphs all have one pick. Unchanged NoDrop source statements run against every distinct row, both flag values and lower/upper/NoDrop boundaries, with declared RNG/party/stat services. Native tests compare adjusted selection roll and exact RNG bound. Negative adjusted native rolls all mean no drop.','Reviewed start dispatcher consumes mana after successful non-UseManaOnDo start. Find Item start34 exists; failed do72 chance does not refund this successful start. Full dispatcher/payment service are reviewed/hashed, not compiled here.'],limits=['Players1/one-pick native foundation tables retained under existing world reference. Full quest/elite/negative-pick/multi-pick/TC-group/acquisition/MF/quality/corpse identity and modern executable acceptance remain pending.','Source block extraction compiles unchanged NoDrop statements inside a bounded driver, not the entire treasure function or its expansion prefix-layout/search. Existing native weighted selection remains its current foundation model; a synthetic prefix-layout attempt did not establish full source equivalence.','Native bypass is internal preparation only until Find Item is enabled; ordinary callers use defaultfalse and keep existing NoDrop behavior.'])
(ROOT/'tools/reference/find-item-loot-engine-evidence.json').write_text(json.dumps(out,indent=2)+'\n')
print('Source NoDrop cases:',len(data),'distinct TC rows:',len(rows))
