#!/usr/bin/env python3
"""Execute complete older passive refresh with explicit current formula services."""
import hashlib,json,struct,subprocess
from pathlib import Path
from probe_shadow_initializer import routine
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-passives-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);inputs={}
 def pin(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_bytes()
 source=BASE/'d2moo-pinned-source/source/D2Common/src/D2Skills.cpp';raw=pin(source);tree=json.loads(pin(BASE/'d2moo-source-tree.json'))
 assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']=='source/D2Common/src/D2Skills.cpp')
 body=routine(raw.decode(),'SKILLS_RefreshSkill');extraction=json.loads(pin(BASE/'defiler-compiled-formula/extraction.json'));data=pin(BASE/'defiler-compiled-formula/skills.bin');code=pin(BASE/'defiler-compiled-formula/skillscode.bin')
 assert sha(BASE/'defiler-compiled-formula/skills.bin')==extraction['files']['skills.bin']['sha256']and sha(BASE/'defiler-compiled-formula/skillscode.bin')==extraction['files']['skillscode.bin']['sha256']
 rows=json.loads(pin(ROOT/'tools/reference/d2r-named-equipment/json/skills.json'));costs=json.loads(pin(ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json'));stats={r['Name']:r['Id']for r in costs};metadata=[]
 for id in [252,263]:
  row=rows[str(id)];base=4+id*748;fields=[]
  for slot in range(14):
   stat=struct.unpack_from('<h',data,base+186+slot*2)[0];calc=struct.unpack_from('<i',data,base+216+slot*4)[0]
   assert stat==stats.get(row.get('passivestat'+str(slot+1)),-1)and(calc>=0)==('passivecalc'+str(slot+1)in row)
   fields.append(dict(stat=stat,calc=calc,text=row.get('passivecalc'+str(slot+1))))
  for n in range(6):assert struct.unpack_from('<i',data,base+440+4*n)[0]==row.get('Param'+str(n+1),0)
  metadata.append(dict(skill=id,fields=fields))
 cpp=r'''
#include <algorithm>
#include <cassert>
#include <cstdio>
#include <map>
#include <vector>
#include <array>
#include <cstdint>
#define __stdcall
#define TRUE 1
#define ARRAY_SIZE(a) (sizeof(a)/sizeof(a[0]))
enum{STAT_MODIFIERLIST_SKILL=350,STAT_MODIFIERLIST_LEVEL=351};
struct D2SkillsTxt{short nPassiveState=152,nAuraState=0,nPassiveIType=68,nPassiveStat[5]={342,343,344,-1,-1};int dwPassiveCalc[5]={0,1,2,0,0};}row;
struct D2SkillStrc{}skill;
struct D2StatListStrc{std::map<std::pair<int,int>,int>v;};using D2StatListExStrc=D2StatListStrc;
struct D2UnitStrc{void*pMemoryPool=nullptr;int dwUnitType=1,dwUnitId=1;}actor;
struct Tables{int nStatesTxtRecordCount=512,nItemStatCostTxtRecordCount=368;}tables;Tables*sgptDataTables=&tables;
D2StatListStrc*list=nullptr;bool present=true;int level=0,allocs=0,frees=0,gfx=0,writes=0;
D2SkillsTxt*DATATBLS_GetSkillsTxtRecord(int){return &row;}
D2SkillStrc*SKILLS_GetHighestLevelSkillFromSkillId(D2UnitStrc*,int){return present?&skill:nullptr;}
bool STATES_CheckState(D2UnitStrc*,int){assert(false);return false;}
D2StatListStrc*STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int s){assert(s==row.nPassiveState);return list;}
void D2Common_10474(D2UnitStrc*,D2StatListExStrc*){}
void STATLIST_FreeStatList(D2StatListStrc*p){assert(p==list);delete list;list=nullptr;frees++;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*,int b){assert(b==1);return level;}
D2StatListStrc*STATLIST_AllocStatList(void*,int a,int b,int type,int id){assert(a==0&&b==0&&type==1&&id==1);allocs++;return new D2StatListStrc;}
void STATLIST_SetState(D2StatListStrc*,int s){assert(s==row.nPassiveState);}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*p,int flag){assert(flag==1);list=p;}
int STATLIST_GetStatValue(D2StatListStrc*p,int stat,int layer){return p->v[{stat,layer}];}
void STATLIST_SetStat(D2StatListStrc*p,int stat,int value,int layer){p->v[{stat,layer}]=value;writes++;}
void STATES_ToggleGfxStateFlag(D2UnitStrc*,int s,int flag){assert(s==row.nPassiveState&&flag==1);gfx++;}
// Declared formula service: reuse Sanctuary's existing pinned shared curves.
// The older dm helper has different rounding at some Weapon Block ranks and
// is deliberately not smuggled into this lifecycle comparison.
int Formula(int id,int slot,int r){if(id==252){if(slot==0)return 30+10*(r-1);if(slot==1)return 35+4*(r-1);return 25*std::min(100,110*r/(r+6))/100;}return r==20?58:20+45*std::min(100,110*r/(r+6))/100;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int token,int id,int r){return Formula(id,token,r);}
'''+body+r'''
int main(){printf("[");bool first=true;
for(int id:{252,263})for(int rank=0;rank<=99;rank++)for(int cached:{-1,1,rank>0?rank:1,99})for(int own:{0,1}){
row.nPassiveState=id==252?152:155;for(auto&v:row.nPassiveStat)v=-1;if(id==252){row.nPassiveStat[0]=342;row.nPassiveStat[1]=343;row.nPassiveStat[2]=344;}else row.nPassiveStat[0]=348;
if(cached>=0){list=new D2StatListStrc;for(int i=0;i<5&&row.nPassiveStat[i]>=0;i++)list->v[{row.nPassiveStat[i],68}]=Formula(id,i,cached);list->v[{350,0}]=id;list->v[{351,0}]=cached;}
level=rank;present=own;allocs=frees=gfx=writes=0;SKILLS_RefreshSkill(&actor,id);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",id,rank,cached,own,allocs,frees,gfx,writes);first=false;bool f=true;
if(list){for(auto [k,v]:list->v)if(v!=0){printf("%s[%d,%d,%d]",f?"":",",k.first,k.second,v);f=false;}delete list;list=nullptr;}printf("]]");
}printf("]\n");}
'''
 path=STAGE/'source.cpp';path.write_text(cpp)
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(path),'-o',str(STAGE/'probe')],stdout=log,stderr=log,check=True)
 with(STAGE/'sanitizers.log').open('w')as log:run=subprocess.run([str(STAGE/'probe')],stdout=subprocess.PIPE,stderr=log,check=True)
 assert not(STAGE/'sanitizers.log').read_text();cases=json.loads(run.stdout);assert len(cases)==1600
 out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 for p in [ROOT/'tools/reference/weapon-block-current-tooltip.json',ROOT/'tools/reference/weapon-block-rank20-deviation.json',ROOT/'tools/reference/diminishing-reference-conflict.json',ROOT/'tools/reference/shadow-skill-metadata-data-evidence.json',BASE/'d2moo-pinned-source/source/D2Game/src/ITEMS/ItemMode.cpp',BASE/'d2moo-pinned-source/source/D2Game/src/SKILLS/Skills.cpp',Path(__file__).resolve()]:pin(p)
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceContexts=1600,currentPassiveStatMatches=28,currentPassiveCalcPresenceMatches=28,currentParameterMatches=12,metadata=metadata,completeRefreshSha256=hashlib.sha256(body.encode()).hexdigest(),currentExecutablePassiveParityVerified=False,
  declaredServices=['NPC intrinsic presence/effective rank;existing or absent passive list;successful allocation;numeric stat/state/post/free and gfx services;existing shared linear/two-stage diminishing curves with the approved WeaponBlock20=58exception. No Fog decoder or old dm-rounding parity is claimed.'],
  limits=['Complete older per-skill refresh lifecycle only;no current executable refresh/callback parity. Current14slot records terminate after3Claw/1WeaponBlock writes,so older5slot iteration has identical active slots for these two rows.','All/element skill callbacks are connected;NPC single-skill107 deliberately has no immediate passive refresh. Oskill97materialization,foreign/charged skills and transformed class/tree callback contexts remain gated. The rank22WeaponBlock58%reading doesnot resolve all current rounding ranks.'],
  inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in [path,out,STAGE/'probe',STAGE/'compiler.log',STAGE/'sanitizers.log',STAGE/'compiler-failed-1.log',STAGE/'source-failed-1.cpp']})
 (ROOT/'tools/reference/shadow-passives-source-evidence.json').write_text(json.dumps(result,indent=2)+'\n');print('Shadow passives:1600whole older refresh contexts;28current slots/calc presences;ASan/UBSan clean;current executable/rounding parity remains qualified.')
if __name__=='__main__':main()
