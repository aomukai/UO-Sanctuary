#!/usr/bin/env python3
"""Compose complete older buff do/list callbacks with the generic payment wrapper."""
import hashlib,json,shutil,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools'))
from review_weapon_event_order import function
S=ROOT/'.local/class-skills/buff-payment-source';S.mkdir(parents=True,exist_ok=True)
prior=ROOT/'tools/reference/energy-shield-cast-source-evidence.json';proof=json.load(open(prior));original=ROOT/'.local/class-skills/energy-shield-cast-source/probe.cpp';expected=proof['artifacts'][str(original.relative_to(ROOT))]
retained=S/'reused-energy-cast.cpp'
if not retained.exists():assert hashlib.sha256(original.read_bytes()).hexdigest()==expected;shutil.copy2(original,retained)
assert hashlib.sha256(retained.read_bytes()).hexdigest()==expected
cpp=retained.read_text().split('int main()',1)[0].replace('#include <cstdio>','#include <cstdio>\n#include <iterator>')
tree=json.load(open(ROOT/'.local/class-skills/d2moo-source-tree.json'));blocks={};hashes={}
for rel,names in [('source/D2Game/src/SKILLS/Skills.cpp',['D2GAME_SKILLS_Handler_6FD12BA0']),('source/D2Game/src/SKILLS/SkillSor.cpp',['SKILLS_SrvDo025_Enchant'])]:
 raw=(ROOT/'.local/class-skills/d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
 hashes[rel]=hashlib.sha256(raw).hexdigest()
 for n in names:blocks[n]=function(raw.decode(),n)
def replace(a,b):
 global cpp
 assert cpp.count(a)==1,a;cpp=cpp.replace(a,b,1)
replace('uint32_t gdwBitMasks[2]={1,2};','uint32_t gdwBitMasks[32]={};')
replace('int dwUnitType=0,dwClassId=0,dwUnitId=0;','int dwUnitType=0,dwClassId=0,dwUnitId=0,dwFlags=0,dwSeqFrame=0;void* pDynamicPath=nullptr;')
replace('int nPassiveStat[5]','int dwFlags[2]={},wSrvDoFunc=23,wItemEffect=1,wSrvMissile=-1,wSrvStartFunc=0,dwDelay=100,nAnim=0;int nPassiveStat[5]')
replace('int costs[256]={};','int nMissilesTxtRecordCount=0;int missileRows[1]={};int* pMissilesTxt=missileRows;int costs[256]={};')
replace('int formula,int,int){return','int formula,int,int){if(formula==100)return 0;return')
cpp+=r'''
constexpr int UNIT_PLAYER=0,SKILLSFLAGINDEX_INTOWN=0,SKILLSFLAGINDEX2_USEMANAONDO=1,SKILLSFLAGINDEX_DECQUANT=2,SKILLSFLAGINDEX_LOB=3,UNITFLAG_SKSRVDOFUNC=256,PLRMODE_TOWNNEUTRAL=0,PLRMODE_SEQUENCE=1;
struct D2SkillStrc{int id=58,rank=1;} used;
struct D2ActiveRoomStrc{} room;
using SkillDoFunc=int(*)(D2GameStrc*,D2UnitStrc*,int,int);
SkillDoFunc gpSkillSrvDoFnTable_6FD40A20[26]={},gpSkillSrvStartFnTable_6FD408B0[1]={nullptr};
int payments=0,delayCalls=0;
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return &used;}
int SKILLS_GetFlags(D2SkillStrc*){return 0;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return false;}
void UNITS_SetUsedSkill(D2UnitStrc*,D2SkillStrc*){}
void sub_6FC817D0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int){}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc* s,const char*,int){return s->id;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc* s,int){return s->rank;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){return &used;}
bool sub_6FD10CE0(D2UnitStrc*){return true;}
int CLIENTS_GetUnitX(D2UnitStrc*){return 10;}int CLIENTS_GetUnitY(D2UnitStrc*){return 20;}
int D2COMMON_10175_PathGetFirstPointX(void*){return 10;}int D2COMMON_10176_PathGetFirstPointY(void*){return 20;}
void sub_6FD11710(D2GameStrc*,int,D2UnitStrc*,int,int,int,int,int,int,int){}
void sub_6FD11420(D2GameStrc*,int,D2UnitStrc*,int,int,int,int,int,int,int){}
int D2GAME_SKILLMANA_Consume_6FD10A50(D2GameStrc*,D2UnitStrc*,int,int){payments++;return 1;}
void sub_6FD118C0(D2GameStrc*,D2UnitStrc*){}
void D2GAME_SKILLS_SetDelay_6FD11C00(D2GameStrc*,D2UnitStrc*,int){delayCalls++;}
'''
cpp+=blocks['SKILLS_SrvDo025_Enchant']+'\n'+blocks['D2GAME_SKILLS_Handler_6FD12BA0']
cpp+=r'''
int main(){for(int n=0;n<32;n++)gdwBitMasks[n]=1u<<n;gpSkillSrvDoFnTable_6FD40A20[23]=SKILLS_SrvDo023_Blaze_EnergyShield_SpiderLay;gpSkillSrvDoFnTable_6FD40A20[25]=SKILLS_SrvDo025_Enchant;
printf("[");bool first=true;
for(int ability:{52,58})for(int old:{0,1,10,20,30})for(int rank:{1,10,20})for(int pay:{0,1}){
owner.dwUnitType=0;owner.dwUnitId=202;used.id=ability;used.rank=rank;row={};row.wSrvDoFunc=ability==58?23:25;row.dwFlags[0]=1;row.nAuraState=ability==58?30:16;
row.wAuraEvent[0]=ability==58?1:-1;hasRow=true;selected=false;mon.dwMonStatsFlags=0;present=old>0;list={};list.state=row.nAuraState;list.skill=ability;list.rank=old;list.until=9999;list.owner=101;duration=3600;payments=delayCalls=0;
int ret=D2GAME_SKILLS_Handler_6FD12BA0(&game,&owner,ability,rank,pay,0,0);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",ability,old,rank,pay,ret,payments,list.rank,list.until);first=false;
}printf("]\n");}
'''
p=S/'probe.cpp';p.write_text(cpp)
subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(S/'probe')],check=True)
cases=json.loads(subprocess.check_output([str(S/'probe')]))
assert len(cases)==60
for ability,old,rank,pay,ret,payments,after,until in cases:
 accepted=old<=rank
 assert (ret,payments,after,until)==(int(accepted),int(accepted and pay),rank if accepted else old,3700 if accepted else 9999)
(S/'cases.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(S/'probe-san')],check=True)
with (S/'sanitizer.log').open('wb') as log:assert json.loads(subprocess.check_output([str(S/'probe-san')],stderr=log))==cases
assert not (S/'sanitizer.log').read_bytes()
print('Buff payment:60 complete older generic handler/do23/do25/list compositions;failed weaker callbacks never invoke payment service;ASan/UBSan clean.')

sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for name,expected in proof['routineHashes'].items():assert hashlib.sha256(function(cpp,name).encode()).hexdigest()==expected
for name,body in blocks.items():assert function(cpp,name)==body
current=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';manifest=json.load(open(current.parent.parent/'sources.json'));assert sha(current)==manifest['files']['json/skills.json']['sha256']
rows=[r for r in json.load(open(current)).values() if r.get('skill') in ('Energy Shield','Enchant')]
assert len(rows)==2 and all(r.get('srvstfunc',0)==0 and not r.get('srvmissile') and not r.get('usemanaondo') for r in rows)
routines=dict(proof['routineHashes']);routines.update({n:hashlib.sha256(b.encode()).hexdigest() for n,b in blocks.items()})
source_hashes=dict(proof['sourceHashes']);source_hashes.update(hashes)
result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=source_hashes,routineHashes=routines,currentTable={str(current.relative_to(ROOT)):sha(current)},cases=len(cases),sanitizers='ASan/UBSan clean',established=['Complete generic handler composes with complete do23/do25/shared list/passive routines. A weaker same-skill state refuses and returns before the mana-consume service;equal/stronger/absent states succeed and invoke it exactly once when the outer payment flag is supplied.'],declaredServices=['Older engine,not modern executable proof. Ordinary learned player/self casts with no missile/start callback,charges,quantity or delay. Used skill,room/current in-town flags,formula/list/event/animation and mana-consume call counting are explicit services. This proves branch/call qualification,not the complete mana helper or outer player-mode dispatch.'],artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),prior,retained,p,S/'probe',S/'probe-san',S/'cases.json',S/'sanitizer.log')})
encoded=json.dumps(result,indent=2)+'\n';out=ROOT/'tools/reference/buff-payment-source-evidence.json'
if '--check' in sys.argv:assert out.read_text()==encoded
else:out.write_text(encoded)
