#!/usr/bin/env python3
"""Execute unchanged Psychic Hammer target guards and dispatch with explicit damage/type services."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 path='source/D2Game/src/SKILLS/SkillAss.cpp';raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);blocks={}
 def routine(name):
  text=raw.decode();m=re.search(r'^\w[^;\n]*\b'+name+r'\([^;{]*?\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  block=text[m.start():i];blocks[name]=hashlib.sha256(block.encode()).hexdigest();return block+'\n'
 start=routine('SKILLS_SrvSt22_PsychicHammer');dispatch=routine('SKILLS_SrvDo033_PsychicHammer')
 cpp=r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,STAT_HITPOINTS=0;
struct D2ActiveRoomStrc{bool town=false;};struct D2GameStrc{};
struct D2UnitStrc{int dwUnitType=1;D2ActiveRoomStrc room;int pSeed=0;bool hire=false,boss=false,unique=false;int life=256;};
struct D2SkillsTxt{int dwCalc[4]={0,1,2,3};}skill;
struct D2DamageStrc{unsigned short wResultFlags=0;};
D2UnitStrc* target=nullptr;bool missing=false;int selected=-1,evalRank=0,draws=0,rolls=0,events=0,applied=0,stage=0,eventFlags=0,flags=0;int chances[4]={100,55,32,32};
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return missing?nullptr:&skill;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return target;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc* p){return &p->room;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc* p){return p->town;}
void SUNITDMG_RollDamage(D2UnitStrc*,int,int,D2DamageStrc* d){rolls++;stage=1;d->wResultFlags=64;}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,D2DamageStrc* d){events++;eventFlags=d->wResultFlags;stage=stage==1?2:-1;}
int MONSTERS_GetHirelingTypeId(D2UnitStrc* p){return p && p->hire;}
bool MONSTERS_IsBoss(int,D2UnitStrc* p){return p && p->boss;}
bool MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc* p,unsigned){return p && p->unique;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int,int rank){selected=calc;evalRank=rank;return chances[calc];}
unsigned ITEMS_RollRandomNumber(int* seed){draws++;return *seed;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int,int){return p->life;}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* d){applied++;flags=d->wResultFlags;stage=stage==2?3:-1;}
'''+start+dispatch+r'''
int main(){D2GameStrc game;bool first=true;std::printf("[");
for(int invalid:{0,1,2,3,4,5})for(int type:{0,1,2,3,4,5,6,7,8})for(int rank:{0,1,20})for(int roll:{0,31,32,54,55,99})for(int hp:{0,255,256}){
 D2UnitStrc owner,enemy;owner.dwUnitType=0;owner.pSeed=roll;enemy.dwUnitType=type==0?0:1;enemy.hire=type==1 || type==6 || type==7 || type==8;enemy.boss=type==2 || type==5 || type==6 || type==8;enemy.unique=type==3 || type==5 || type==7 || type==8;enemy.life=hp;target=invalid==1?nullptr:&enemy;missing=invalid==2;owner.room.town=invalid==3;enemy.room.town=invalid==4;if(invalid==5)enemy.dwUnitType=2;
 selected=-1;evalRank=draws=rolls=events=applied=stage=eventFlags=flags=0;
 int guard=SKILLS_SrvSt22_PsychicHammer(&game,&owner,253,rank);int ok=SKILLS_SrvDo033_PsychicHammer(&game,&owner,253,rank);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",invalid,type,rank,roll,hp,guard,ok,selected,evalRank,draws,rolls,events,applied,stage,eventFlags,flags);
}std::printf("]\n");}
'''
 p=STAGE/'psychic-hammer-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==2916
 for invalid,kind,rank,roll,hp,guard,ok,selected,evalRank,draws,rolls,events,applied,stage,eventFlags,flags in rows:
  assert guard==int(invalid in (0,2));assert ok==int(invalid==0)
  if ok:
   calc=3 if kind in (0,1,6,7,8) else 2 if kind in (2,5) else 1 if kind==3 else 0;chance=[100,55,32,32][calc]
   assert (selected,evalRank,draws,rolls,events,applied,stage,eventFlags,flags)==(calc,max(1,rank),1,1,1,1,3,64,65|(8 if roll<chance else 0)|(2 if hp<256 else 0))
  else:assert (selected,evalRank,draws,rolls,events,applied,stage,eventFlags,flags)==(-1,0,0,0,0,0,0,0,0)
 out=STAGE/'psychic-hammer-source-probe.json';out.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourcePath=path,sourceSha256=hashlib.sha256(raw).hexdigest(),compiledUnchangedRoutines=blocks,fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=len(rows)),established=['Both caster and target town checks and player/monster target-type guards precede damage; invalid skill row has its own do guard.', 'Damage roll and event execution occur before successful-hit and knockback flags. Player/hireling uses calc4, species boss calc3, unique typeflag8 calc2, ordinary calc1; formula rank clamps to1 after damage rolling.', 'Positive supplied chance draws once and uses a strict modulo100 comparison. Less than one whole life sets flag2 before missile-damage execution; no physical attack-rating roll or travelling projectile is present in these functions.'],services=['Damage roll/event/missile receivers, target lookup, town/type/boss/unique/hireling predicates, HP and RNG are explicit services. Supplied formula outputs100/55/32/32 are opaque routing inputs, not rank goldens.', 'The source bodies are unchanged. Full damage/event/stat/seed/collision/knockback mode implementation, modern executable behavior and native learned skill integration are separate work.'],handlerEnabled=False)
 path=ROOT/'tools/reference/psychic-hammer-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert path.read_text()==render
 else:path.write_text(render)
 print(f'Psychic Hammer:{len(rows)} complete start/do source cases with explicit nested services; no learned handler enabled.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
