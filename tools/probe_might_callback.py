#!/usr/bin/env python3
"""Execute complete Might basic-aura callback with reused complete state-list helper."""
import hashlib,json,re,subprocess,sys
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/might-callback-source'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 sources={};blocks={}
 def source(rel):
  p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);sources[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 for rel,names in [('source/D2Game/src/SKILLS/Skills.cpp',['sub_6FD10EC0']),('source/D2Game/src/SKILLS/SkillPal.cpp',['SKILLS_AuraCallback_BasicAura']),('source/D2Game/src/SKILLS/SkillNec.cpp',['sub_6FD0B3D0'])]:
  text=source(rel)
  for n in names:blocks[n]=function(text,n)
 enums=[]
 for rel,name in [('source/D2Common/include/D2StatList.h','D2C_ItemStats'),('source/D2Common/include/DataTbls/ItemsTbls.h','D2C_ItemStatCostTxtFlagIndices'),('source/D2Common/include/D2States.h','D2C_States')]:
  text=source(rel);m=re.search(r'enum '+name+r'\s*\{.*?\};',text,re.S);assert m,name;enums.append(m.group())
 prior=ROOT/'tools/reference/cold-armor-source-evidence.json';r=json.loads(prior.read_text());shared=ROOT/'.local/class-skills/cold-armor-source/probe.cpp';assert sha(shared)==r['artifacts'][str(shared.relative_to(ROOT))]
 header=shared.read_text().split(blocks['sub_6FD10EC0'])[0]
 def replace(a,b):
  nonlocal header
  assert header.count(a)==1,a;header=header.replace(a,b,1)
 replace('constexpr int STAT_CURSE_RESISTANCE=174,STATE_ATTRACT=57,EVENTTYPE_REMOVESTATE=1;', 'constexpr int EVENTTYPE_REMOVESTATE=1;')
 replace('constexpr int UNIT_PLAYER=0,UNITFLAG_SKSRVDOFUNC=256,STAT_MODIFIERLIST_SKILL=10,STAT_MODIFIERLIST_LEVEL=11;', 'constexpr int UNIT_PLAYER=0,UNITFLAG_SKSRVDOFUNC=256;')
 replace('uint32_t gdwBitMasks[2]={1,2};','uint32_t gdwBitMasks[32]={};')
 replace('struct Tables{','struct D2ItemStatCostTxt{unsigned dwItemStatFlags=0;int wMaxStat=0;} costs[512];\nstruct Tables{D2ItemStatCostTxt* pItemStatCostTxt=costs;int nCurseStates=0;int* pCurseStates=nullptr;')
 replace('nItemStatCostTxtRecordCount=256','nItemStatCostTxtRecordCount=512')
 replace('values[256]={}','values[512]={}')
 replace('void STATLIST_SetStatRemoveCallback(D2StatListStrc*,StatListRemoveCallback){}','StatListRemoveCallback selectedCallback=nullptr;void STATLIST_SetStatRemoveCallback(D2StatListStrc*,StatListRemoveCallback cb){selectedCallback=cb;}')
 header='\n'.join(enums)+'\n'+header
 header+=r'''
int32_t sub_6FD0B3D0(D2UnitStrc*,int32_t,int32_t);
struct D2BasicAuraParamStrc{int nSkillId=98,nSkillLevel=1,nDuration=51,nAuraState=33,nPassiveState=0,nAuraStat[6]={25},nAuraStatCalcValue[6]={},field_40=0;D2StatListStrc* pStatList=nullptr;StatListRemoveCallback pStateRemoveCallback=nullptr;};
struct D2AuraCallbackStrc{D2GameStrc* pGame;D2UnitStrc* pOwner;void* pArgs;};
void SKILLS_CurseStateCallback_BasicAura(D2UnitStrc*,int,D2StatListStrc*){__builtin_trap();}
int MONSTERS_GetHirelingTypeId(D2UnitStrc*){__builtin_trap();}
int STATLIST_GetUnitBaseStat(D2UnitStrc*,int,int){__builtin_trap();}
int STATLIST_GetStatValue(D2StatListStrc* p,int id,int){return p->values[id];}
int D2COMMON_10473(D2StatListStrc*){__builtin_trap();}
void D2COMMON_10476(D2StatListStrc*,int){__builtin_trap();}
bool STATES_CheckStateMaskCurableByStateId(int){__builtin_trap();}
void STATLIST_SetUnitStat(D2UnitStrc*,int,int,int){__builtin_trap();}
int SUNITDMG_GetColdEffect(D2GameStrc*,D2UnitStrc*){__builtin_trap();}
'''
 cpp=header+'\n'.join(blocks.values())+r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int kind:{0,1})for(int self:{0,1})for(int old:{0,1,20,30,255})for(int rank:{1,20,21,255})for(int duration:{0,51})for(int creator:{101,202})for(int zero:{0,1}){
owner={};target={};owner.dwUnitId=202;target.dwUnitId=303;D2UnitStrc* receiver=self?&owner:&target;receiver->dwUnitType=kind;
mon={};mon2={};present=old>0;list={};list.state=33;list.skill=98;list.rank=old;list.owner=creator;list.until=9999;list.values[25]=777;
writes=allocations=frees=refresh=gfx=events=rateupdates=0;selectedCallback=nullptr;for(int i=0;i<200;i++)states[i]={};
D2BasicAuraParamStrc args;args.nSkillLevel=rank;args.nDuration=duration;args.nAuraStatCalcValue[0]=zero?0:40+10*(rank-1);D2AuraCallbackStrc cb{&game,&owner,&args};
int result=SKILLS_AuraCallback_BasicAura(&cb,receiver);int callback=selectedCallback==SKILLS_CurseStateCallback_BasicAura?1:selectedCallback==sub_6FD10E50?2:0;
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",kind,self,old,rank,duration,creator,zero,result,list.rank,list.owner,list.until,list.values[25],writes,allocations,frees,events,args.field_40,rateupdates,callback);first=false;
}printf("]\n");}
'''
 for n,b in blocks.items():assert function(cpp,n)==b
 STAGE.mkdir(parents=True,exist_ok=True);p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe'
 subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr
 cases=json.loads(run.stdout);assert len(cases)==640
 for kind,self,old,rank,duration,creator,zero,result,after,owner,until,value,writes,allocations,frees,events,changed,rates,callback in cases:
  ok=old<=rank;reuse=bool(old and old==rank);allocated=ok and not reuse;bonus=0 if zero else 40+10*(rank-1)
  expected=(int(ok),rank if ok else old,creator if not allocated else 202,(100+duration if duration else 9999 if reuse else 0) if ok else 9999,
   (777 if reuse else 0) if ok and zero else bonus if ok else 777,(2+int(not zero)) if ok else 0,int(allocated),int(allocated and old>0),int(ok and duration>0),int(ok and not zero),int(ok),(1 if self else 2) if allocated else 0)
  assert (result,after,owner,until,value,writes,allocations,frees,events,changed,rates,callback)==expected
 out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 tables=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((tables/'sources.json').read_text());pins={}
 for n in ('skills','states','itemstatcost'):
  q=tables/'json'/(n+'.json');assert sha(q)==manifest['files']['json/'+n+'.json']['sha256'];pins[str(q.relative_to(ROOT))]=sha(q)
 row=next(x for x in json.loads((tables/'json/skills.json').read_text()).values() if x.get('skill')=='Might');state=json.loads((tables/'json/states.json').read_text())['might'];stat=json.loads((tables/'json/itemstatcost.json').read_text())['damagepercent']
 assert (row['*Id'],row['srvdofunc'],row['aurafilter'],row['Param3'],row['Param4'],row['perdelay'],row['mana'],state['*ID'],stat['*ID'],stat.get('direct',0))==(98,65,73731,40,10,50,0,33,25,0)
 result=dict(referenceVersion='3.3.93847',sourceCommit=tree['sha'],sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in blocks.items()},currentTables=pins,cases=len(cases),sanitizers='ASan/UBSan clean',
  established=['Complete unchanged basic-aura callback,shared list helper and stat-modifier function execute together for player/monster and self/ally recipients,source ranks/creators and finite/zero durations. Same-skill weaker states refuse before stat writes;equal retains creator and refreshes finite duration;higher/absent allocate with the current creator.',
   'Pinned Might state33/stat25is a non-direct stat. Its positive40+10perrank bonus is stored once in the list,then skill/rank metadata are written and animation rate refresh requested. No direct HP/elemental mutation or mana callback is inserted. Source damage consumers and periodic/filter wrappers remain separate evidence.',
   'A supplied zero stat skips that stat write;equal-rank lists retain their old value. Zero-payload controls are source-only boundaries,not a claim that valid positive-rank Might emits zero at zero mana.'],
  declaredServices=['Older source composition,not modern executable certification. Inventory-free list/store/allocation,formula inputs,actor metadata,events and animation refresh are controlled services reused from Cold Armor;no world search or current damage application executes here.',
   'Cleansing,healing,direct-stat,resistance/hireling,cold-rate and passive-state branches are not exercised;their supplied callbacks trap if unexpectedly reached. The removal callback is recorded,not invoked.'],
  artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),ROOT/'tools/review_weapon_event_order.py',prior,shared,p,binary,out,STAGE/'sanitizer.log')})
 encoded=json.dumps(result,indent=2)+'\n';output=ROOT/'tools/reference/might-callback-source-evidence.json'
 if '--check' in sys.argv:assert output.read_text()==encoded
 else:output.write_text(encoded)
 print('Might:640complete callback/list/modifier cases;rank/creator/expiry/non-direct stat25/zero-payload boundaries;ASan/UBSan clean.')
if __name__=='__main__':main()
