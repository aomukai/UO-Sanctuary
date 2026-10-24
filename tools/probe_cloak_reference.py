#!/usr/bin/env python3
"""Execute unchanged Cloak dispatch/callback with explicit formula, factory, search and AI services."""
import argparse,ast,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';path='source/D2Game/src/SKILLS/SkillAss.cpp';raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);blocks={}
 def routine(name):
  t=raw.decode();m=re.search(r'^\w[^;\n]*\b'+name+r'\([^;{]*?\)\s*\n\{',t,re.M);assert m,name;i=t.index('{',m.start())+1;d=1
  while d:d+=(t[i]=='{')-(t[i]=='}');i+=1
  b=t[m.start():i];blocks[name]=hashlib.sha256(b.encode()).hexdigest();return b+'\n'
 do=routine('SKILLS_SrvDo047_CloakOfShadows');callback=routine('SKILLS_AuraCallback_CloakOfShadows')
 scaffold=ROOT/'tools/probe_assassin_buff_reference.py';treeAst=ast.parse(scaffold.read_text());prefix=next(x.value for x in ast.walk(treeAst) if isinstance(x,ast.Constant) and isinstance(x.value,str) and x.value.startswith('#include <algorithm>'))
 prefix=prefix.replace('int nAuraState=157,','int wAuraTargetState=156,dwAuraRangeCalc=3;uint32_t dwAuraFilter=57347;int nAuraState=153,').replace('int nSkill=0,nSkillLevel=0,nStat=0,nDuration=0,nState=0;','int nSkill=0,nSkillLevel=0,nStat=0,nDuration=0,nState=0,nStatValue=0;')
 a=prefix.index('D2StatListStrc* sub_6FD10EC0(');prefix=prefix[:a]+r'''
struct D2AuraCallbackStrc{void* pArgs;D2UnitStrc* pOwner;D2GameStrc* pGame;};
struct D2SrvDo47Strc{int nSkillId=0,nSkillLevel=0,nDuration=0,nAuraTargetState=0,nAuraStat[6]={},nAuraStatCalcValue[6]={};};
constexpr int AISPECIALSTATE_DIMVISION=10,UNIT_MONSTER=1;
D2UnitStrc *caster=nullptr,*enemy=nullptr;int failed=0,canAi=0,ai=0,range=-1,filter=-1;
void sub_6FD10E50(D2UnitStrc*,int,D2StatListStrc*){}void sub_6FD0B250(D2UnitStrc*,int,D2StatListStrc*){}
D2StatListStrc* sub_6FD10EC0(D2CurseStrc* c){factory++;if(failed==(c->pTarget==caster?1:2))return nullptr;auto* p=new D2StatListStrc{c->nState,c->nSkillLevel,c->nSkill,game.dwGameFrame+c->nDuration,{},c->pStateRemoveCallback};if(c->nStat>=0)p->stats[c->nStat]=c->nStatValue;c->pTarget->lists[c->nState]=p;c->pTarget->active[c->nState]=true;return p;}
int sub_6FD15190(D2UnitStrc*,int){return canAi;}void* AIGENERAL_GetAiControlFromUnit(D2UnitStrc*){return nullptr;}
void AITHINK_ExecuteAiFn(D2GameStrc*,D2UnitStrc*,void*,int){ai++;}
int32_t SKILLS_AuraCallback_CloakOfShadows(D2AuraCallbackStrc*,D2UnitStrc*);
void sub_6FD0FE80(D2GameStrc* g,D2UnitStrc* owner,int,int,int r,uint32_t f,int32_t(*cb)(D2AuraCallbackStrc*,D2UnitStrc*),void* args,int check,const char*,int){range=r;filter=f;if(check!=1)std::abort();D2AuraCallbackStrc context{args,owner,g};cb(&context,enemy);}
'''
 cpp=prefix+callback+do+r'''
int main(){bool first=true;std::printf("[");
for(int old:{0,1})for(int mode:{0,1,2,3,4,5})for(int dead:{0,1})for(int allowed:{0,1})for(int rank:{1,20})for(int oldRank:{1,20}){
 D2UnitStrc owner,target;caster=&owner;enemy=&target;target.dwUnitType=1;target.dead=dead;owner.active[153]=old;owner.active[157]=owner.active[159]=true;if(old)owner.lists[153]=new D2StatListStrc{153,oldRank,264,999,{},nullptr};
 skill=D2SkillsTxt{};skill.nAuraState=mode==2?200:153;skill.wAuraTargetState=mode==3?200:156;skill.nPassiveStat[0]=5;skill.dwPassiveCalc[0]=1;skill.wAuraStat[0]=6;skill.dwAuraStatCalc[0]=2;for(int i=1;i<6;i++)skill.wAuraStat[i]=-1;
 guard=mode==1?1:0;failed=mode==4?1:mode==5?2:0;canAi=allowed;ai=factory=gfx=0;range=filter=-1;values[0]=200;values[1]=10;values[2]=-15;values[3]=30;
 int ok=SKILLS_SrvDo047_CloakOfShadows(&game,&owner,264,rank);auto* self=STATLIST_GetStatListFromUnitAndState(&owner,153);auto* curse=STATLIST_GetStatListFromUnitAndState(&target,156);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",old,mode,dead,allowed,rank,oldRank,ok,owner.active[153],owner.active[157],owner.active[159],self?self->stats[5]:0,self?self->expiry:0,curse?curse->stats[6]:0,curse?curse->expiry:0,factory,gfx,ai,range,filter);
 for(auto& e:owner.lists)delete e.second;for(auto& e:target.lists)delete e.second;
}std::printf("]\n");}
'''
 p=STAGE/'cloak-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==192
 for old,mode,dead,allowed,rank,oldRank,ok,active,quick,fade,defense,expiry,penalty,curseExpiry,factory,gfx,ai,radius,filter in rows:
  assert quick==fade==1
  applies=not old and mode not in (1,2,4)
  assert ok==int(applies and mode!=3) and active==int(old or applies)
  if applies:assert (defense,expiry,gfx)==(10,600,1)
  if applies and mode!=3:
   hit=not dead and mode!=5;assert (penalty,curseExpiry,ai,radius,filter)==((-15 if hit else 0),(600 if hit else 0),int(hit and allowed),30,57347)
 out=STAGE/'cloak-source-probe.json';out.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourcePath=path,sourceSha256=hashlib.sha256(raw).hexdigest(),compiledUnchangedRoutines=blocks,proofEnablesHandler=False,currentBuildConflict='The latest user3.3.93847 recheck retracts the earlier fresh-group bypass: living blinded targets prevent recasting; death/expiry releases it. Older do47 denies any existing owner state, so it supports caster-wide refusal but does not establish the observed early release after target death. Preserve the unchanged probe as historical source evidence, with current observations recorded separately in cloak-current-observations.json.',scaffold=dict(path=str(scaffold.relative_to(ROOT)),sha256=hashlib.sha256(scaffold.read_bytes()).hexdigest()),fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=len(rows)),established=['Full do47 denies an existing owner state before changing stats, leaves Quickness/Fade untouched, captures owner passive stats/rank/duration and dispatches the configured filter/radius once.', 'Target callback applies curse stats with original supplied duration independently of optional AI activation. A dead target or failed target factory does not undo the already successful owner cast.', 'Invalid target state is checked after owner attachment. Current pinned rows are valid; malformed synthetic rows illustrate guard order without inventing production data.'],services=['Formula outputs200frames/10defense/-15target/30radius are opaque service inputs. Native current rank formulas are verified separately.', 'Shared curse factory/statlist allocation, target search/filter and special-AI eligibility/dispatch are declared nested services. Separate full factory, aura-filter and AI-eligibility evidence must be retained; this probe does not certify them.', 'Exact current executable animation/light/AI/stat/event/room behavior remains acceptance work. No learned handler is enabled solely by this source proof.'])
 path=ROOT/'tools/reference/cloak-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert path.read_text()==render
 else:path.write_text(render)
 print('Cloak:192 complete source dispatch/callback cases with explicit factory/search/formula/AI services.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
