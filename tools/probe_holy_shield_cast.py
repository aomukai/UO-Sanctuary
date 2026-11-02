#!/usr/bin/env python3
"""Reuse complete do18/list helpers with Holy Shield's real start and outer payment wrappers."""
import hashlib,json,subprocess,sys
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/holy-shield-cast-source'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 prior={n:json.loads((ROOT/'tools/reference'/n).read_text()) for n in ('cold-armor-source-evidence.json','buff-payment-source-evidence.json')}
 cold=ROOT/'.local/class-skills/cold-armor-source/probe.cpp';buff=ROOT/'.local/class-skills/buff-payment-source/probe.cpp'
 for name,p in [('cold-armor-source-evidence.json',cold),('buff-payment-source-evidence.json',buff)]:assert sha(p)==prior[name]['artifacts'][str(p.relative_to(ROOT))]
 sources={};blocks={}
 for rel,names in [('source/D2Game/src/SKILLS/Skills.cpp',['sub_6FD10EC0','sub_6FD11C90','sub_6FD12950','D2GAME_SKILLS_Handler_6FD12BA0']),('source/D2Game/src/SKILLS/SkillSor.cpp',['SKILLS_CurseStateCallback_DefensiveBuff','SKILLS_SrvDo018_DefensiveBuff']),('source/D2Game/src/SKILLS/SkillPal.cpp',['SKILLS_SrvSt36_HolyShield'])]:
  p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==rel)
  sources[str(p.relative_to(ROOT))]=sha(p)
  for n in names:blocks[n]=function(raw.decode(),n)
 header=cold.read_text().split(blocks['sub_6FD10EC0'])[0]
 def replace(a,b):
  nonlocal header
  assert header.count(a)==1,a;header=header.replace(a,b,1)
 replace('#include <cstdio>','#include <cstdio>\n#include <iterator>')
 replace('uint32_t gdwBitMasks[2]={1,2};','uint32_t gdwBitMasks[32]={};')
 replace('int dwUnitType=0,dwClassId=0,dwUnitId=0,dwFlags=0;','int dwUnitType=0,dwClassId=0,dwUnitId=0,dwFlags=0,dwSeqFrame=0;void* pDynamicPath=nullptr;void* pInventory=nullptr;')
 replace('int dwCalc[4]={42,0,0,0};','int dwFlags[2]={},wSrvDoFunc=18,wItemEffect=0,wSrvMissile=-1,wSrvStartFunc=36,dwDelay=100,nAnim=0,nLineOfSight=0;int dwCalc[4]={42,0,0,0};')
 replace('int nItemStatCostTxtRecordCount=256,','int nMissilesTxtRecordCount=0;int missileRows[1]={};int* pMissilesTxt=missileRows;int nItemStatCostTxtRecordCount=256,')
 replace('int formula,int,int rank){if(formula==42)', 'int formula,int,int rank){if(formula==100)return 0;if(formula==42)')
 service=buff.read_text().split('constexpr int UNIT_PLAYER=0,SKILLSFLAGINDEX_INTOWN=0,',1)[1].split(blocks.get('SKILLS_SrvDo025_Enchant','int32_t __fastcall SKILLS_SrvDo025_Enchant'),1)[0]
 service='constexpr int SKILLSFLAGINDEX_INTOWN=0,'+service
 service=service.replace(',UNITFLAG_SKSRVDOFUNC=256','').replace('[1]={nullptr}','[37]={}')
 service=service.replace('bool sub_6FD10CE0(D2UnitStrc*){return true;}','bool manaReady=true;bool sub_6FD10CE0(D2UnitStrc*){return manaReady;}')
 header+=service+r'''
using SkillStartFunc=SkillDoFunc;
constexpr int SKILLSFLAGINDEX_PERIODIC=4,EVENTTYPE_PERIODICSKILLS=7;
constexpr int SKILLSLOS_NONE=0,SKILLSLOS_MISSILE_BARRIER=1,SKILLSLOS_PLAYER_PATH=2,SKILLSLOS_PLAYER_MONSTER=3,SKILLSLOS_PLAYER_FLYING=4,SKILLSLOS_RADIAL_BARRIER=5;
constexpr int COLLIDE_MISSILE_BARRIER=1,COLLIDE_MASK_PLAYER_PATH=2,COLLIDE_PLAYER=4,COLLIDE_MONSTER=8,COLLIDE_DOOR=16,COLLIDE_WALL=32;
bool hasShield=true;int shieldQueries=0;
int INVENTORY_GetEquippedShield(void*,void*){shieldQueries++;return hasShield;}
bool D2Common_11026(int,int,D2UnitStrc*,uint32_t){return true;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){}
'''
 cpp=header+'\n'.join(blocks.values())+r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;
gpSkillSrvDoFnTable_6FD40A20[18]=SKILLS_SrvDo018_DefensiveBuff;gpSkillSrvStartFnTable_6FD408B0[36]=SKILLS_SrvSt36_HolyShield;
printf("[");bool first=true;
for(int inventory:{0,1})for(int shield:{0,1})for(int ready:{0,1})for(int old:{0,1,10,20,30})for(int rank:{1,10,20})for(int bypass:{0,1})for(int lose:{0,1}){
owner={};owner.dwUnitId=202;owner.pInventory=inventory?&owner:nullptr;hasShield=shield;manaReady=ready;used.id=117;used.rank=rank;hasRow=true;
row={};row.nAuraState=101;row.dwFlags[0]=1;for(int i=0;i<3;i++)row.wAuraEvent[i]=-1;for(int i=0;i<6;i++)row.wAuraStat[i]=-1;row.wAuraStat[0]=1;
for(int i=0;i<200;i++){states[i]={};active[i]=false;}states[101].wGroup=0;active[10]=true;
present=old>0;list={};list.state=101;list.skill=117;list.rank=old;list.until=9999;list.owner=202;duration=750;
payments=delayCalls=allocations=frees=writes=clears=events=shieldQueries=0;
int start=sub_6FD12950(&game,&owner,117,rank,bypass);int before=payments;if(lose)owner.pInventory=nullptr;
int effect=start?D2GAME_SKILLS_Handler_6FD12BA0(&game,&owner,117,rank,1,0,0):-1;
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",inventory,shield,ready,old,rank,bypass,lose,start,effect,before,payments,list.rank,list.until,allocations,clears,active[10]);first=false;
}printf("]\n");}
'''
 for n,b in blocks.items():assert function(cpp,n)==b,n
 STAGE.mkdir(parents=True,exist_ok=True);p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe'
 subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
 result=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(result.stderr);assert result.returncode==0 and not result.stderr
 cases=json.loads(result.stdout);assert len(cases)==480
 for inv,shield,ready,old,rank,bypass,lose,start,effect,before,after,newrank,until,allocated,clears,coldactive in cases:
  began=bool(inv and shield and (ready or bypass));accepted=bool(began and old<=rank)
  expected=(int(began),int(accepted) if began else -1,int(began and not bypass),int(began and not bypass),rank if accepted else old,850 if accepted else 9999,int(accepted and old!=rank),0,1)
  assert (start,effect,before,after,newrank,until,allocated,clears,coldactive)==expected
 out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 tables=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((tables/'sources.json').read_text());tablehashes={}
 for n in ('skills','states'):
  q=tables/'json'/(n+'.json');assert sha(q)==manifest['files']['json/'+n+'.json']['sha256'];tablehashes[str(q.relative_to(ROOT))]=sha(q)
 row=next(r for r in json.loads((tables/'json/skills.json').read_text()).values() if r.get('*Id')==117);state=json.loads((tables/'json/states.json').read_text())['holyshield']
 assert (row['*Id'],row['srvstfunc'],row['srvdofunc'],row.get('usemanaondo',0),row.get('srvmissile',''),row['InTown'],state['*ID'],state.get('group',0))==(117,36,18,0,'',1,101,0)
 record=dict(referenceVersion='3.3.93847',sourceCommit=tree['sha'],sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in blocks.items()},currentTables=tablehashes,cases=len(cases),sanitizers='ASan/UBSan clean',
  established=['Complete start wrapper plus actual shield gate admits valid inventory/shield after mana readiness;ordinary success calls mana consumption at start. The bypass flag skips readiness/payment as explicitly controlled,not as granted-skill permission proof.',
   'Complete generic do wrapper with nonnull start36and UseManaOnDo0does not consume again. Complete do18/shared list with ungrouped state101refuses a weaker same-skill list,refreshes equal rank and replaces higher rank. A weaker effect can fail after a successful already-paid start.',
   'Losing the shield between start and do does not itself stop this do18body;shield-dependent defense/block/Smite consumers remain separate. Group1cold state is not cleared by ungrouped Holy Shield.'],
  declaredServices=['Pinned older engine plus current table route,not modern executable or outer animation/mode/input proof. Real mana arithmetic is a call-count service;list/store,formula,room,flags and inventory access are controlled. Numeric formula750is an input and does not execute current rank-duration arithmetic.',
   'No LOS branch,periodic skill,event subscription/priority,allocation failure,charges,item-only permission,delay or graphical client is exercised. Unused collision/mode constants are opaque service scaffolding,not a current geometry mapping.'],
  artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),ROOT/'tools/review_weapon_event_order.py',ROOT/'tools/reference/cold-armor-source-evidence.json',ROOT/'tools/reference/buff-payment-source-evidence.json',cold,buff,p,binary,out,STAGE/'sanitizer.log')})
 encoded=json.dumps(record,indent=2)+'\n';output=ROOT/'tools/reference/holy-shield-cast-source-evidence.json'
 if '--check' in sys.argv:assert output.read_text()==encoded
 else:output.write_text(encoded)
 print('Holy Shield:480 complete start36/do18/outer/list compositions;start payment,weaker refusal,equal/higher rank,shield loss and no cold-group clear;ASan/UBSan clean.')
if __name__=='__main__':main()
