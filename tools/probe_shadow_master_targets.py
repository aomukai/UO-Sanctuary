#!/usr/bin/env python3
"""Compile the three whole pinned Master target/tactic routines with query services."""
import hashlib,itertools,json,random,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-master-target-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 rel='source/D2Game/src/AI/AiThink.cpp';source=BASE/'d2moo-pinned-source'/rel;raw=source.read_bytes()
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel)
 names=['AITHINK_IsTargetKillableByShadowMaster','AITHINK_TargetCallback_ShadowMaster','sub_6FCECBA0'];bodies={n:function(raw.decode(),n)for n in names}
 cpp=r'''
#include <cassert>
#include <climits>
#include <cstdint>
#include <iostream>
#include <map>
#include <vector>
#define __fastcall
using WORD=uint16_t;
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,MONSTATSFLAGINDEX_NPC=0,MONSTATSFLAGINDEX_KILLABLE=1;
constexpr unsigned gdwBitMasks[]={1,2};
constexpr int MONTYPEFLAG_SUPERUNIQUE=2,MONTYPEFLAG_CHAMPION=4,MONTYPEFLAG_UNIQUE=8,UNITFLAG_CANBEATTACKED=1;
constexpr int MONSTER_WAKEOFDESTRUCTION=410,MONSTER_CHARGEBOLTSENTRY=411,MONSTER_LIGHTNINGSENTRY=412,MONSTER_BLADECREEPER=413,MONSTER_INFERNOSENTRY=415,MONSTER_DEATHSENTRY=416;
struct D2GameStrc{} game;struct D2AiTickParamStrc{} tick;
struct D2MonStatsTxt{unsigned dwMonStatsFlags;int nBaseId;};
struct D2UnitStrc{int id=0,dwUnitType=0,dead=0,row=0,npc=0,kill=0,dwClassId=0,aligned=0,dwFlags=0,enemy=0,ownerDistance=0,selfDistance=0,boss=0,prime=0,types=0;D2MonStatsTxt stats;};
struct D2ShadowMasterAiCallbackArgStrc{D2UnitStrc*unk0x00=nullptr,*unk0x04=nullptr;int unk0x08=INT_MAX,unk0x0C=0;D2UnitStrc*unk0x10=nullptr;int unk0x14=INT_MAX,unk0x18=0,unk0x1C=0,unk0x20=0;D2UnitStrc*unk0x24=nullptr;};
struct D2SkillStrc{int id=0,mode=0,range=0;} skill;
std::map<int,D2UnitStrc> units;std::vector<std::vector<int>>trace;int present,runResult,useResult;
void add(std::initializer_list<int>x){trace.emplace_back(x);}int id(D2UnitStrc*u){return u?u->id:0;}
void printTrace(){std::cout<<'[';bool first=true;for(auto&r:trace){if(!first)std::cout<<',';first=false;std::cout<<'[';for(size_t i=0;i<r.size();i++){if(i)std::cout<<',';std::cout<<r[i];}std::cout<<']';}std::cout<<']';}
bool SUNIT_IsDead(D2UnitStrc*u){add({1,id(u)});return u&&u->dead;}
D2MonStatsTxt*MONSTERMODE_GetMonStatsTxtRecord(int klass){auto&u=units.at(klass);add({3,u.id});return u.row?&u.stats:nullptr;}
bool STATLIST_AreUnitsAligned(D2UnitStrc*,D2UnitStrc*u){add({2,id(u)});return u->aligned;}
bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc*u){add({5,id(u)});return u->enemy;}
int AITHINK_GetSquaredDistance(D2UnitStrc*a,D2UnitStrc*b){add({6,id(a),id(b)});return a->id==2?b->ownerDistance:b->selfDistance;}
bool MONSTERS_IsBoss(D2GameStrc*,D2UnitStrc*u){add({7,id(u)});return u->boss;}
bool MONSTERS_IsPrimeEvil(D2UnitStrc*u){add({8,id(u)});return u->prime;}
int MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc*u,int mask){add({9,id(u),mask});return u->types&mask;}
D2SkillStrc*SKILLS_GetSkillById(D2UnitStrc*,int requested,int mode){assert(mode==-1);add({10,requested});return present?&skill:nullptr;}
int D2Common_11016(D2UnitStrc*,D2SkillStrc*s){add({11,s->id});return s->range;}
int AITACTICS_RunToTargetUnitWithFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*t,int flags){add({12,id(t),flags});return runResult;}
int SKILLS_GetSkillMode(D2SkillStrc*s){return s->mode;}
int AITACTICS_UseSkill(D2GameStrc*,D2UnitStrc*,int mode,int sid,D2UnitStrc*t,int x,int y){add({13,mode,sid,id(t),x,y});return useResult;}
'''+ '\n'.join(bodies.values())+r'''
int main(){int cases;std::cin>>cases;std::cout<<'[';for(int c=0;c<cases;c++){
 int owner,n;std::cin>>owner>>n;units.clear();units[1].id=1;units[2].id=2;std::vector<int>order;
 for(int i=0;i<n;i++){D2UnitStrc u;std::cin>>u.id>>u.dwUnitType>>u.dead>>u.row>>u.npc>>u.kill>>u.dwClassId>>u.aligned>>u.dwFlags>>u.enemy>>u.ownerDistance>>u.selfDistance>>u.boss>>u.prime>>u.types;int base=u.dwClassId;u.dwClassId=u.id;u.stats={unsigned((u.npc?1:0)|(u.kill?2:0)),base};if(u.id)units[u.id]=u;order.push_back(u.id);}
 trace.clear();D2ShadowMasterAiCallbackArgStrc arg;arg.unk0x00=owner?&units.at(2):nullptr;
 for(int key:order)AITHINK_TargetCallback_ShadowMaster(&game,&units.at(1),key?&units.at(key):nullptr,&arg);
 if(c)std::cout<<',';std::cout<<"{\"scan\":["<<id(arg.unk0x04)<<','<<arg.unk0x08<<','<<arg.unk0x0C<<','<<id(arg.unk0x10)<<','<<arg.unk0x14<<','<<arg.unk0x18<<','<<arg.unk0x1C<<','<<arg.unk0x20<<','<<id(arg.unk0x24)<<"],\"trace\":";printTrace();trace.clear();
 std::cout<<",\"special\":[";bool first=true;for(int key:order){if(!first)std::cout<<',';first=false;std::cout<<AITHINK_IsTargetKillableByShadowMaster(&units.at(1),key?&units.at(key):nullptr);}std::cout<<"],\"specialTrace\":";printTrace();std::cout<<'}';
 }std::cout<<"]\n";int count;std::cin>>count;std::cout<<'[';for(int c=0;c<count;c++){
 int hasOwner,target,combat,x,y;std::cin>>hasOwner>>target>>skill.id>>present>>skill.mode>>skill.range>>combat>>x>>y>>runResult>>useResult;
 trace.clear();int result=sub_6FCECBA0(&game,hasOwner?&units.at(2):nullptr,&units.at(1),WORD(skill.id),combat,target?&units.at(target):nullptr,x,y,&tick);
 if(c)std::cout<<',';std::cout<<"{\"result\":"<<result<<",\"trace\":";printTrace();std::cout<<'}';
 }std::cout<<"]\n";}
'''
 def unit(ident=100,**v):
  d=dict(kind=1,dead=0,row=1,npc=0,kill=1,base=5,aligned=0,attackable=1,enemy=1,owner=64,self=64,boss=0,prime=0,types=0);d.update(v)
  return [ident,*d.values()]
 contexts=[]
 for kind,dead,row,npc,kill,boss,prime,types in itertools.product([0,1],[0,1],[0,1],[0,1],[0,1],[0,1],[0,1],[0,2,4,8]):
  contexts.append(dict(owner=True,units=[unit(kind=kind,dead=dead,row=row,npc=npc,kill=kill,boss=boss,prime=prime,types=types)]))
 rng=random.Random(106)
 for own in [False,True]:
  contexts.extend([dict(owner=own,units=[unit(0),unit(1),unit(2,kind=0),*[unit(100+i,base=base,aligned=1,attackable=0,enemy=0)for i,base in enumerate([410,411,412,413,415,416])]]),
   dict(owner=own,units=[unit(100,self=100,owner=101),unit(101,self=100,owner=100,types=4),unit(102,self=1024,owner=100,types=8),unit(103,self=1025,owner=0,types=2)])])
  for _ in range(48):
   contexts.append(dict(owner=own,units=[unit(100+i,kind=rng.choice([0,1]),dead=int(rng.random()<.15),row=int(rng.random()>.1),npc=int(rng.random()<.1),kill=int(rng.random()>.1),base=rng.choice([5,410,411,412,413,415,416]),aligned=int(rng.random()<.25),attackable=int(rng.random()>.1),enemy=int(rng.random()>.15),owner=rng.choice([0,99,100,101,1023,1024,1025,4096]),self=rng.choice([0,99,100,101,1023,1024,1025,4096]),boss=int(rng.random()<.1),prime=int(rng.random()<.1),types=rng.choice([0,2,4,8,16]))for i in range(8)]))
 # Keep all tactic pointer targets in the final map;their metadata is irrelevant.
 contexts.append(dict(owner=True,units=[unit(100)]))
 tactics=[list(v)for v in itertools.product([0,1],[0,1,2,100],[0,1],[0,1],[0,1],[0,1])]
 tactics=[[owner,target,261,present,9,melee,combat,108,208,run,use]for owner,target,present,melee,combat,run in tactics for use in [0,1]]
 lines=[str(len(contexts))]
 for c in contexts:lines.extend([f"{int(c['owner'])} {len(c['units'])}",*[' '.join(map(str,u))for u in c['units']]])
 lines.append(str(len(tactics)));lines.extend(' '.join(map(str,t))for t in tactics)
 code=STAGE/'source.cpp';code.write_text(cpp);binary=STAGE/'source';subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(code),'-o',str(binary)],check=True)
 result=subprocess.run([str(binary)],input='\n'.join(lines)+'\n',text=True,capture_output=True);(STAGE/'sanitizer.log').write_text(result.stderr);assert result.returncode==0 and not result.stderr,result.stderr[-2000:]
 scans,actions=[json.loads(s)for s in result.stdout.splitlines()];assert len(scans)==len(contexts) and len(actions)==len(tactics)
 output=dict(scans=[dict(input=c,expected=r)for c,r in zip(contexts,scans)],tactics=[dict(input=c,expected=r)for c,r in zip(tactics,actions)])
 # Independent corner controls,not only a self-described compiled receipt.
 c=next(r for r in output['scans']if len(r['input']['units'])==9 and not r['input']['owner']);assert c['expected']['scan'][7]==6 and c['expected']['scan'][0]==2 and c['expected']['scan'][6]==1
 c=next(r for r in output['scans']if len(r['input']['units'])==4 and r['input']['owner']);assert c['expected']['scan']==[100,100,2,103,0,3,3,0,102]
 path=STAGE/'cases.json';path.write_text(json.dumps(output,separators=(',',':'))+'\n')
 receipt=dict(referenceVersion='3.3.93847',olderSourceTree=tree['sha'],scanContexts=len(contexts),tacticContexts=len(tactics),completeRoutineHashes={n:hashlib.sha256(s.encode()).hexdigest()for n,s in bodies.items()},inputs={str(p.relative_to(ROOT)):sha(p)for p in [source,Path(__file__).resolve()]},artifacts={str(p.relative_to(ROOT)):sha(p)for p in [code,path,STAGE/'sanitizer.log']},scope='Complete older AI106 callback,special-monster predicate and tactic admission. Ordered query/tactic traces,owner/self ties,trap counting,elite classification and source distance boundaries are compared by the native C# host. Room enumeration,world metadata/movement/action suppliers and full AI106 selection remain separate;no current-executable parity or learned activation is claimed.')
 (ROOT/'tools/reference/shadow-master-target-source-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n');print(f"Whole Master target/tactic controls:{len(contexts)}scan/{len(tactics)}tactic contexts;ASan/UBSan clean.")
if __name__=='__main__':main()
