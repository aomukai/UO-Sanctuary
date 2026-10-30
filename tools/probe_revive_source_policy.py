#!/usr/bin/env python3
"""Execute complete older Revive routines with explicit service/placement boundaries."""
import argparse,hashlib,itertools,json,re,subprocess
from pathlib import Path
from probe_warlock_runtime import ROOT,digest
STAGE=ROOT/'.local/class-skills/revive-source-policy';FIXTURE=ROOT/'.local/class-skills/revive-source-policy-source-probe.json'
def build(check=False):
 treePath=ROOT/'.local/class-skills/d2moo-source-tree.json';tree=json.loads(treePath.read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';paths=[Path(__file__).resolve(),treePath];routines={}
 def block(rel,name):
  p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;b=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);paths.append(p);s=b.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m;end=s.index('{',m.start())+1;depth=1
  while depth:depth+=(s[end]=='{')-(s[end]=='}');end+=1
  body=s[m.start():end];routines[name]=digest(body.encode());return body+'\n'
 cpp=r'''#include <algorithm>
#include <array>
#include <cassert>
#include <cstdint>
#include <iostream>
#include <vector>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,PCLASS_PALADIN=3,AISPECIALSTATE_REVIVED=7,MONSTATS2FLAGINDEX_REVIVE=8,MONMODE_NEUTRAL=1,STAT_LEVEL=12,STAT_MAXHP=7,STAT_HITPOINTS=6,EVENTTYPE_PERIODICSKILLS=8,EVENTTYPE_AITHINK=2,EVENTTYPE_MONUMOD=7,STATE_REVIVE=96,COLLIDE_MASK_MONSTER_PATH=0x3c01;
constexpr uint32_t UNITFLAG_CANBEATTACKED=4,UNITFLAG_ISVALIDTARGET=8,UNITFLAG_TARGETABLE=2,UNITFLAG_NOXP=0x04000000,UNITFLAG_NOTC=0x20000,UNITFLAG_ISREVIVE=0x80000000;
uint32_t gdwBitMasks[32];
struct D2UnitStrc{int dwUnitType=1,dwClassId=12,dwUnitId=202,dwNodeIndex=9,level=50,maximum=100*256,life=0,x=100,y=200,mode=12;bool right=false;uint32_t dwFlags=0;uint64_t pSeed=0;}caster,target;
struct D2GameStrc{int nGameType=1,dwGameType=0,nDifficulty=2,dwGameFrame=1000;}game;
struct D2SkillsTxt{uint8_t nPetType=0;int dwPetMax=1,dwCalc[2]={0,2};}row;
struct Tables{int nPetTypeTxtRecordCount=20;}tables;auto* sgptDataTables=&tables;
struct D2MonStats2Txt{uint32_t dwFlags=0;}secondary;
struct D2CoordStrc{int nX=0,nY=0;};struct D2ActiveRoomStrc{int id=0;}oldRoom{7},newRoom{3};
struct D2ModeChangeStrc{D2UnitStrc* target=nullptr;int mode=0;};
struct D2MonStatsInitStrc{int nMinHP=0,nMaxHP=0;};
struct D2DamageStrc{uint16_t wResultFlags=0;uint32_t dwHitFlags=0;int dwPhysDamage=0,dwDmgTotal=0;};
std::array<int,25> in{};std::vector<std::array<int,6>> trace;
int handle(D2UnitStrc* u){return !u?0:u==&caster?1:2;}
void T(int op,int unit,int a=0,int b=0,int c=0,int d=0){trace.push_back({op,unit,a,b,c,d});}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){T(0,id,in[0]);return in[0]?&row:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){int h=in[1]<0?0:2;T(1,h);return h?&target:nullptr;}
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int cls){assert(cls==12);T(2,cls,in[2]);return in[2]<0?nullptr:&secondary;}
bool SUNIT_IsDead(D2UnitStrc* u){T(3,handle(u),in[3]);return in[3];}
bool D2COMMON_11017_CheckUnitIfConsumeable(D2UnitStrc* u,int z){assert(z==0);T(4,handle(u),in[4]);return in[4];}
int sub_6FD15190(D2UnitStrc* u,int state){assert(state==7);T(5,handle(u),state,in[5]);return in[5];}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc* u){return u?u->maximum:0;}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc* a,D2UnitStrc* b,int z,D2DamageStrc* d){assert(a==b&&z==0);T(6,handle(a),d->wResultFlags,d->dwHitFlags,d->dwPhysDamage,d->dwDmgTotal);}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc* a,D2UnitStrc* b,D2DamageStrc* d){assert(a==b);T(7,handle(a),d->wResultFlags,d->dwHitFlags,d->dwPhysDamage,d->dwDmgTotal);}
int CLIENTS_GetUnitX(D2UnitStrc* u){return u->x;}int CLIENTS_GetUnitY(D2UnitStrc* u){return u->y;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &oldRoom;}int PATH_GetUnitCollisionPattern(D2UnitStrc*){return 11;}int UNITS_GetUnitSizeX(D2UnitStrc*){return 2;}
void COLLISION_ResetMaskWithPattern(D2ActiveRoomStrc* r,int x,int y,int pattern,unsigned mask){assert(mask==0x8000);T(8,2,r->id,x,y,pattern);}
D2ActiveRoomStrc* COLLISION_GetFreeCoordinates(D2ActiveRoomStrc* r,D2CoordStrc* p,int size,int mask,int z){assert(size==2&&mask==0x3c01&&z==1);T(32,2,r->id,size,mask,z);p->nX+=3;p->nY+=5;return in[6]?&newRoom:nullptr;}
void MONSTERUNIQUE_ToggleUnitFlag(D2UnitStrc* u,unsigned f,int on){T(9,handle(u),static_cast<int>(f),on);u->dwFlags=on?u->dwFlags|f:u->dwFlags&~f;}
void MONSTER_UpdateAiCallbackEvent(D2GameStrc*,D2UnitStrc* u){T(10,handle(u));}
void MONSTERMODE_GetModeChangeInfo(D2UnitStrc* u,int mode,D2ModeChangeStrc* p){assert(mode==1);p->target=u;p->mode=mode;}
int D2GAME_ModeChange_6FC65220(D2GameStrc*,D2ModeChangeStrc* p,int z){assert(z==1);T(11,handle(p->target),p->mode,z);if(in[22])p->target->mode=p->mode;return in[22];}
void sub_6FD154D0(D2UnitStrc* u){T(12,handle(u));}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc* u,int type,int z){assert(z==0);T(13,handle(u),type);}
void* UNITS_GetRightSkill(D2UnitStrc* u){return u->right?reinterpret_cast<void*>(1):nullptr;}
void SKILLS_SetRightActiveSkill(D2UnitStrc* u,int skill,int layer){T(14,handle(u),skill,layer);u->right=false;}
void sub_6FCBDFE0(D2GameStrc*,D2UnitStrc* u,D2ActiveRoomStrc* r,int x,int y,int a,int b){assert(a==0&&b==0);T(15,handle(u),r->id,x,y);u->x=x;u->y=y;}
int STATLIST_UnitGetStatValue(D2UnitStrc* u,int stat,int z){assert(stat==12&&z==0);return u?u->level:0;}
void DATATBLS_CalculateMonsterStatsByLevel(int cls,bool expansion,int difficulty,int level,int flag,D2MonStatsInitStrc* p){assert(cls==12&&expansion&&difficulty==2&&flag==1);T(28,2,cls,level,in[11],in[12]);p->nMinHP=in[11];p->nMaxHP=in[12];}
int ITEMS_RollLimitedRandomNumber(uint64_t* seed,int bound){assert(bound>0&&seed==&target.pSeed);*seed=0x6AC690C5ULL*static_cast<uint32_t>(*seed)+(*seed>>32);int result=static_cast<uint32_t>(*seed)%bound;T(29,2,bound,result);return result;}
void STATLIST_SetUnitStat(D2UnitStrc* u,int stat,int value,int z){assert(z==0);T(16,handle(u),stat,value);if(stat==7)u->maximum=value;else if(stat==6)u->life=value;else{assert(stat==12);u->level=value;}}
int32_t __fastcall MONSTERUNIQUE_CalculatePercentage(int32_t,int32_t,int32_t);
int D2GAME_SetSummonPassiveStats_6FD0C530(D2GameStrc*,D2UnitStrc* owner,D2UnitStrc* u,int skill,int rank,int item){assert(owner==&caster&&item==0);T(17,handle(u),skill,rank,item);if(in[23]){u->maximum+=MONSTERUNIQUE_CalculatePercentage(u->maximum,in[24],100);u->life=u->maximum;}return in[23];}
void AIGENERAL_SetOwnerData(D2GameStrc*,D2UnitStrc* u,int id,int kind,int a,int b){assert(a==0&&b==0);T(18,handle(u),id,kind);}
void AIUTIL_SetOwnerGUIDAndType(D2UnitStrc* u,D2UnitStrc* owner){T(19,handle(u),handle(owner));}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc* u,int type,int frame,int a,int b){assert(a==0&&b==0);T(20,handle(u),type,frame);}
void sub_6FCBDD30(D2UnitStrc* u,unsigned flags,int z){T(21,handle(u),flags,z);}
void sub_6FC61F00(D2UnitStrc* u){T(22,handle(u));}
void STATES_ToggleState(D2UnitStrc* u,int state,int enabled){T(23,handle(u),state,enabled);}
int SKILLS_EvaluateSkillFormula(D2UnitStrc* owner,int formula,int skill,int rank){int value=formula==1?in[19]:in[20];T(formula==1?30:31,handle(owner),skill,rank,value);return value;}
void D2GAME_BOSSES_AssignUMod_6FC6FF10(D2GameStrc*,D2UnitStrc* u,int mod,int z){T(24,handle(u),mod,z);}
void sub_6FC7D7A0(D2GameStrc*,D2UnitStrc* owner,D2UnitStrc* u,int type,int cap){T(25,handle(u),handle(owner),type,cap);}
void D2GAME_UpdateSummonAI_6FC401F0(D2GameStrc*,D2UnitStrc* u,int z,int node){assert(z==0);T(26,handle(u),node);}
void D2Common_10214(D2UnitStrc* u){T(27,handle(u));}
void __fastcall sub_6FD0DF40(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int32_t,int32_t);
'''
 cpp+=block('source/D2Game/src/MONSTER/MonsterUnique.cpp','MONSTERUNIQUE_CalculatePercentage')
 for name in ['SKILLS_SrvSt21_Revive','SKILLS_SrvDo058_Revive','sub_6FD0DF40']:cpp+=block('source/D2Game/src/SKILLS/SkillNec.cpp',name)
 cpp+=r'''
void reset(){trace.clear();caster={};target={};caster.dwUnitType=in[7];caster.dwClassId=in[8];caster.dwUnitId=101;caster.level=in[9];caster.dwNodeIndex=19;target.dwUnitType=in[1];target.level=in[10];target.right=in[14];target.pSeed=(666ULL<<32)|static_cast<uint32_t>(in[13]);game.dwGameFrame=in[21];row.nPetType=in[17];tables.nPetTypeTxtRecordCount=in[18];secondary.dwFlags=in[2]>0?256:0;}
void output(int result){std::cout<<'['<<result<<",["<<target.mode<<','<<target.maximum<<','<<target.life<<','<<target.level<<','<<target.x<<','<<target.y<<','<<target.right<<"],"<<target.dwFlags<<','<<target.pSeed<<",[";bool comma=false;for(auto a:trace){if(comma)std::cout<<',';comma=true;std::cout<<'[';for(int j=0;j<6;j++){if(j)std::cout<<',';std::cout<<a[j];}std::cout<<']';}std::cout<<"]]";}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;int mode,a,b;bool comma=false;std::cout<<'[';
while(std::cin>>mode>>a>>b){for(auto& v:in)std::cin>>v;if(comma)std::cout<<',';comma=true;reset();int result=0;
if(mode==0)result=SKILLS_SrvSt21_Revive(&game,&caster,in[15],in[16]);
else if(mode==1)result=SKILLS_SrvDo058_Revive(&game,&caster,in[15],in[16]);
else{sub_6FD0DF40(&game,a?&caster:nullptr,b?&target:nullptr,in[15],in[16]);result=1;}output(result);}
std::cout<<"]\n";}
'''
 source=STAGE/'source-probe.cpp';source.write_text(cpp);binary=STAGE/'source-probe'
 compiled=subprocess.run(['g++','-std=c++17','-O1','-g','-Wall','-Wextra','-Werror','-Wno-unused-parameter','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(source),'-o',str(binary)],capture_output=True)
 compilerLog=STAGE/'compiler.log';compilerLog.write_bytes(compiled.stdout+compiled.stderr);assert compiled.returncode==0,compiled.stderr.decode()[-3000:]
 def base():return [1,1,1,1,1,1,1,0,2,50,50,100,103,1,1,95,20,13,20,20,4500,1000,1,1,200]
 cases=[]
 for values in itertools.product([0,1],[-1,0,1,4],[-1,0,1],[0,1],[0,1],[-1,0,1]):
  p=base();p[:6]=values;cases.append(p)
 for placement,cl,tl,hp,seed,bonus in itertools.product([0,1],[0,1,50,99],[0,1,50,99],[1,5000,50000],[0,1,-1],[0,200,580]):
  p=base();p[6]=placement;p[9:14]=[cl,tl,hp,hp+3,seed];p[24]=bonus;cases.append(p)
 for role,pet,count,cap,rank,duration,right,mode,passive in itertools.product([(0,2),(0,3),(1,3)],[0,13,255],[0,20,256],[0,1,255],[1,20,255],[-1,0,1,4500],[0,1],[0,1],[0,1]):
  p=base();p[7:9]=role;p[17:21]=[pet,count,cap,duration];p[16]=rank;p[14]=right;p[22:24]=[mode,passive];cases.append(p)
 for skill in [0,95]:
  p=base();p[15]=skill;cases.append(p)
 owners=[]
 for caster,target,skill,duration,frame in itertools.product([0,1],[0,1],[0,95],[-1,0,1,4500],[0,1000]):
  p=base();p[15]=skill;p[20:22]=[duration,frame];owners.append([p,caster,target])
 requests=[(mode,1,1,p) for p in cases for mode in [0,1]]+[(2,a,b,p) for p,a,b in owners]
 inp=''.join(' '.join(map(str,[mode,a,b,*p]))+'\n' for mode,a,b,p in requests)
 run=subprocess.run([str(binary)],input=inp.encode(),capture_output=True);san=STAGE/'sanitizer.log';san.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 results=json.loads(run.stdout);assert len(results)==len(requests)
 fixture=dict(cases=[[p,results[2*i],results[2*i+1]] for i,p in enumerate(cases)],owners=[[p,a,b,result] for (p,a,b),result in zip(owners,results[2*len(cases):],strict=True)])
 encoded=json.dumps(fixture,separators=(',',':'))+'\n'
 if check:assert FIXTURE.read_text()==encoded
 else:FIXTURE.write_text(encoded)
 paths += [source,binary,san,compilerLog,FIXTURE,ROOT/'tools/reference/revive-species-data-evidence.json']
 proof=dict(referenceVersion='3.3.93847',component='Complete older Revive start/release/owner control flow',legacyCommit=tree['sha'],processMemoryRead=False,handlerEnabled=False,modernCodeEquivalenceClaimed=False,routineHashes=routines,
  verified=dict(castContexts=len(cases),ownerContexts=len(owners),completeRoutines=4,sanitizers='ASan/UBSan exit0,empty stderr'),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(paths))},
  services=['Metadata,dead/consumable/AI eligibility,collision/mode entry,base HP lookup,existing shared RNG,passive writes,owner/event/AI/cap effects are explicit services. Enter-neutral wraps the complete initializer/mode-change boundary without inventing its opaque payload. A supplied passive service may fail;the caller still continues. No actor allocation occurs.','Valid cast contexts use nonnull caster,nonnegative bounded HP and safe signed arithmetic. Source-invalid null-caster release,corrupt pointers and overflow outside this domain remain unsupported. Owner-only contexts cover null owner/target and skill0.'],
  remaining=['Native differential and approved no-expiration comparison are required. These older routines do not prove modern executable equivalence,in-place native actor reconstruction,complete passives/special abilities or learned casting.'])
 path=ROOT/'tools/reference/revive-source-policy-evidence.json';encoded=json.dumps(proof,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 print(json.dumps(proof['verified']))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
