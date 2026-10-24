#!/usr/bin/env python3
"""Run complete physical/elemental spell-roll wrappers with declared endpoint/RNG/channel services."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
 def source(path):
  raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^\w[^;\n]*\b'+name+r'\([^;{]*?\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  block=text[m.start():i];blocks[name]=hashlib.sha256(block.encode()).hexdigest();return block+'\n'
 skills=source('source/D2Game/src/SKILLS/Skills.cpp');unit=source('source/D2Game/src/UNIT/SUnitDmg.cpp');physical=routine(skills,'D2GAME_RollPhysicalDamage_6FD14EC0');element=routine(skills,'D2GAME_RollElementalDamage_6FD14DD0');roll=routine(unit,'SUNITDMG_RollDamage')
 cpp=r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#include <cassert>
#define __fastcall
struct D2UnitStrc{int pSeed=0;};struct D2DamageStrc{int dwPhysDamage=0,dwMagDamage=0;};struct D2SkillsTxt{int nEType=3;}skill;
int minimum=0,maximum=0,mastery=0,draw[2],spans[2],calls=0;bool missing=false;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return missing?nullptr:&skill;}
int SKILLS_GetMinPhysDamage(D2UnitStrc*,int,int,int mode){assert(mode==0);return minimum;}
int SKILLS_GetMaxPhysDamage(D2UnitStrc*,int,int,int mode){assert(mode==0);return maximum;}
int SKILLS_GetMinElemDamage(D2UnitStrc*,int,int,int mode){assert(mode==1);return minimum+minimum*mastery/100;}
int SKILLS_GetMaxElemDamage(D2UnitStrc*,int,int,int mode){assert(mode==1);return maximum+maximum*mastery/100;}
int SKILLS_GetElementalLength(D2UnitStrc*,int,int,int mode){assert(mode==1);return 0;}
int ITEMS_RollLimitedRandomNumber(int*,int span){assert(calls<2 && draw[calls]>=0 && draw[calls]<span);spans[calls]=span;return draw[calls++];}
void sub_6FD11E40(D2UnitStrc*,D2DamageStrc* d,int type,int amount,int length,int a,int b){assert(type==3 && !length && !a && !b);d->dwMagDamage+=amount;}
'''+physical+element+roll+r'''
int main(){D2UnitStrc owner;bool first=true;std::printf("[");
for(int rank:{1,20,21,255})for(int bonus:{0,25})for(int a:{0,1,2,3})for(int b:{0,1,2,3}){
 int mins[4]={256,7168,7680,186368},maxs[4]={768,10112,10752,219392};int k=rank==1?0:rank==20?1:rank==21?2:3;minimum=mins[k];maximum=maxs[k];mastery=bonus;
 int phySpan=maximum-minimum,magMin=minimum+minimum*mastery/100,magMax=maximum+maximum*mastery/100,magSpan=magMax-magMin;
 draw[0]=a==0?0:a==1?1:a==2?phySpan/2:phySpan-1;draw[1]=b==0?0:b==1?1:b==2?magSpan/2:magSpan-1;calls=0;D2DamageStrc damage;SUNITDMG_RollDamage(&owner,253,rank,&damage);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d]",rank,bonus,draw[0],draw[1],damage.dwPhysDamage,damage.dwMagDamage,calls,spans[0],spans[1]);
}std::printf("]\n");}
'''
 p=STAGE/'psychic-hammer-damage-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==128 and all(x[6]==2 for x in rows)
 out=STAGE/'psychic-hammer-damage-source-probe.json';out.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,compiledUnchangedRoutines=blocks,fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=len(rows)),established=['Full spell roll wrapper calls physical then elemental. Physical adds to its own channel; magic delivery is a declared channel service.', 'Both wrappers use a limited draw over maximum-minus-minimum, excluding the upper FP256 endpoint. Physical endpoint mode0 and elemental endpoint/length mode1 are asserted.', 'Independent draws and endpoint mastery round-trip across128 supplied positive endpoint/range cases. Native pinned-rank formulas are checked separately.'],services=['Endpoint formula/mastery evaluation, limited RNG and magic channel delivery are explicit services. These are not a newly compiled full skill evaluator, source seed generator or complete damage/stat/event receiver.', 'Current rank1/20/21/255 endpoint values are supplied inputs, not derived by this legacy probe. Shared native formula goldens remain independent checks.'])
 path=ROOT/'tools/reference/psychic-hammer-damage-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert path.read_text()==render
 else:path.write_text(render)
 print('Psychic Hammer:128 complete source spell-roll wrapper cases; endpoints, RNG and magic writer declared separately.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
