#!/usr/bin/env python3
"""Complete Blade Fury progressive selectors/start/do/expiry; callers remain scoped."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());rel='source/D2Game/src/SKILLS/SkillAss.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);text=raw.decode();routines={}
 def extract(name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  body=text[m.start():i];routines[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
 cpp=r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
#define D2_MAYBE_UNUSED(x) (void)(x)
constexpr int STATE_INFERNO=1,SKILLSFLAGINDEX_PROGRESSIVE=0,STAT_MANA=0,EVENTTYPE_REMOVESTATE=1,UNITFLAG_SKSRVDOFUNC=64;
unsigned gdwBitMasks[32];
struct D2GameStrc{int dwGameFrame=1000;void* pMemoryPool=nullptr;}game;
struct D2UnitStrc{int dwUnitId=7,dwUnitType=0;unsigned dwFlags=64;}owner,created;
struct D2SkillStrc{int param=0;}skill;
struct D2StatListStrc{int until=0;}state;
struct D2SkillsTxt{unsigned dwFlags[2]={};int nAuraState=-1,wAuraStat[1]={-1},wSrvMissile=0,wSrvMissileA=506,wSrvMissileB=508,wSrvMissileC=510,dwPrgCalc[3]={0,1,2},wStartMana=6;}row;
struct Tables{int nStatesTxtRecordCount=512,nItemStatCostTxtRecordCount=512;}tables;auto* sgptDataTables=&tables;
struct D2MissileStrc{int nSkill=0,nMissile=0,nSkillLevel=0,nTargetX=0,nTargetY=0;unsigned dwFlags=0;D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;};
int mana=0,calc=5,emits=0,made=0,payCalls=0,paid=0,rewinds=0,allocs=0;bool record=true,learned=true,active=false,allocation=true,aim=true,factory=true;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){assert(id==266);return record?&row:nullptr;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int id){assert(id==266);return learned?&skill:nullptr;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int s){assert(s==STATE_INFERNO);return active?&state:nullptr;}
int STATLIST_GetStatValue(D2StatListStrc*,int,int){assert(false);return 0;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int id,int rank){assert(formula==0&&id==266&&rank==3);return calc;}
int SKILLS_GetParam1(D2SkillStrc* p){return p->param;}void SKILLS_SetParam1(D2SkillStrc* p,int value){p->param=value;}
int D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int* x,int* y){*x=110;*y=120;return aim;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){assert(p->dwFlags==32&&p->pOwner==&owner&&p->pOrigin==&owner&&p->nMissile==506&&p->nSkill==266&&p->nSkillLevel==3&&p->nTargetX==110&&p->nTargetY==120);emits++;if(factory){made++;return &created;}return nullptr;}
bool STATES_CheckState(D2UnitStrc*,int s){assert(s==STATE_INFERNO);return active;}
void sub_6FCBD120(D2GameStrc*,D2UnitStrc*,int one){assert(one==1);rewinds++;}
void D2COMMON_10476(D2StatListStrc* p,int until){p->until=until;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int until,int,int){assert(type==EVENTTYPE_REMOVESTATE&&until==state.until);}
void D2GAME_SKILLMANA_Consume_6FD10A50(D2GameStrc*,D2UnitStrc*,int id,int rank){assert(id==266&&rank==3);payCalls++;if(mana>=320){mana-=320;paid++;}}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int s,int){assert(s==STAT_MANA);return mana;}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned flags,int until,int type,int id){assert(flags==2&&type==0&&id==7);allocs++;state.until=until;return allocation?&state:nullptr;}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int one){assert(one==1);}
void SKILLS_StatRemoveCallback_RemoveState(D2UnitStrc*,int,D2StatListStrc*);
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*fn)(D2UnitStrc*,int,D2StatListStrc*)){assert(fn==SKILLS_StatRemoveCallback_RemoveState);}
void STATLIST_SetState(D2StatListStrc*,int s){assert(s==STATE_INFERNO);}
void STATES_ToggleState(D2UnitStrc*,int s,int on){assert(s==STATE_INFERNO);active=on;}
'''
 for name in ['SKILLS_GetProgressiveSkillMissileId','SKILLS_EvaluateProgressiveSkillCalc','SKILLS_SrvDo048_BladeFury','SKILLS_SrvSt26_BladeFury','SKILLS_StatRemoveCallback_RemoveState']:cpp+=extract(name)
 cpp+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("{\"channel\":[");bool first=true;
for(int rec:{0,1})for(int has:{0,1})for(int missile:{0,1})for(int before:{0,1})for(int next:{999,1000,1001})for(int amount:{0,1535,1536})for(int allocate:{0,1})for(int target:{0,1})for(int make:{0,1})for(int formula:{0,1,5})for(int method:{0,1}){
 owner={};row={};row.wSrvMissileA=missile?506:-1;skill.param=next;state.until=1020;record=rec;learned=has;active=before;mana=amount;allocation=allocate;aim=target;factory=make;calc=formula;emits=made=payCalls=paid=rewinds=allocs=0;
 int result=method?SKILLS_SrvDo048_BladeFury(&game,&owner,266,3):SKILLS_SrvSt26_BladeFury(&game,&owner,266,3);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u]",first?"":",",rec,has,missile,before,next,amount,allocate,target,make,formula,method,result,skill.param,state.until,active,emits,made,payCalls,paid,mana,rewinds,allocs,owner.dwFlags);first=false;
}printf("],\"expiry\":[");first=true;
for(int before:{0,1})for(unsigned flags:{0u,64u,128u})for(int stat:{0,1}){
 active=before;owner.dwFlags=flags;SKILLS_StatRemoveCallback_RemoveState(&owner,STATE_INFERNO,stat?&state:nullptr);
 printf("%s[%d,%u,%d,%d,%u]",first?"":",",before,flags,stat,active,owner.dwFlags);first=false;
}printf("]}\n");}
'''
 p=STAGE/'blade-fury-channel-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'blade-fury-channel-sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:];data=json.loads(run.stdout);rows=data['channel'];assert len(rows)==6912 and len(data['expiry'])==12
 for before,flags,stat,after,result in data['expiry']:assert after==0 and result==flags|64
 for rec,has,missile,before,deadline,amount,allocate,aim,make,formula,method,result,param,until,active,emits,made,calls,paid,left,rewinds,allocs,flags in rows:
  valid=bool(rec and has and missile);do=method or before and deadline<=1000;eligible=valid and formula>1 if do else False
  attempt=bool(eligible and aim and deadline<1000);renew=bool(not method and valid and before);fresh=bool(not method and valid and not before and amount>=1536);freshOk=fresh and allocate
  success=(attempt if do else (renew or freshOk));call=bool(not method and before and attempt);payment=call and amount>=320
  expected=(int(success),1004 if attempt else 0 if freshOk else deadline,1007 if renew else 1021 if fresh else 1020,int(before or freshOk),int(attempt),int(attempt and make),int(call),int(payment),amount-320*payment,int(eligible and aim and before) if do else int(renew),int(fresh),0 if eligible else 64)
  assert (result,param,until,active,emits,made,calls,paid,left,rewinds,allocs,flags)==expected,(rec,has,missile,before,deadline,amount,allocate,aim,make,formula,method,(result,param,until,active,emits,made,calls,paid,left,rewinds,allocs,flags),expected)
 out=STAGE/'blade-fury-channel-source-probe.json';out.write_text(json.dumps(data,separators=(',',':'))+'\n')
 meta=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text());path=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';current=path.read_bytes();assert hashlib.sha256(current).hexdigest()==meta['files']['json/skills.json']['sha256'];skill=next(x for x in json.loads(current).values() if x.get('skill')=='Blade Fury');assert skill['srvstfunc']==26 and skill['srvdofunc']==48 and skill['Param4']==5 and skill['startmana']==6 and not skill.get('progressive')
 e=dict(referenceVersion='3.3.93847',nativeVerifiedByThisSourceProbe=False,legacyCommit=tree['sha'],sourceHashes={rel:hashlib.sha256(raw).hexdigest()},compiledRoutineHashes=routines,pinnedSkill=skill,fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=dict(channel=6912,expiry=12),total=6924),established=['Complete progressive selectors choose base srvmissilea and prgcalc1 because pinned Blade Fury has no progressive flag. Server missile is bladefragment1; client bladefragment2/3 variants are not silently selected by rank.','Fresh start only checks startmana6 availability, creates a21-frame inferno-state lease and resets Param1 to0; it does not itself debit6mana. Existing state renews to frame+7 before readiness/aim checks. Future Param1 returns success with rewind; equality enters do48 but strict Param1<frame prevents emission, returning0.','Do48 with prgcalc1=5 stores next=frame+4 after a missile creation attempt, making the earliest subsequent strict eligibility frame+5. Allocation failure still counts as creation attempt/success and updates Param1. Existing-state start calls the declared mana consumer only after such an attempt; direct do48 has no debit in its body.','Flag0x40 is cleared after skill/missile/duration checks but before target lookup. The independently compiled expiry callback clears inferno state and sets the server-do flag; its timer/animation/event scheduling is not driven by this matrix.'],limits=['Mana consumer is a declared effective-rank3 cost320 service. Generic start/do wrappers, their usemanaondo eligibility/debit, rank1 legacy quirk and current held input/payment cadence remain separate. Raw direct routine calls with no mana do not prove free production casts.','Aim, stat allocation/renewal/event storage, factory and animation rewind are explicit services. Only non-progressive/current base selector context executes; progressive aura branches assert. Exact SQ23/seqinput12 event scheduling, release/loss/cancellation, weapon packet/AR/collision and current executable acceptance remain open.','Historical hit52 is not bound by the pinned bladefragment1/2/3 server hit rows; do not infer radial child damage from that function name. Final effects/audio and talent/hotkey icons remain separately pending; no learned handler is enabled.'],sanitizers='ASan/UBSan; exit0 and empty runtime stderr')
 target=ROOT/'tools/reference/blade-fury-channel-source-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert target.read_text()==render
 else:target.write_text(render)
 print('6912 complete Blade Fury selector/start/do channel cases and12 expiry callback cases pass; generic payment, animation/input and missile/weapon integration remain separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
