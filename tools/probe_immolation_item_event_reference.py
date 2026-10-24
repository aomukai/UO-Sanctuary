#!/usr/bin/env python3
"""Distinguish hit09's missile-stat clear from the attacker's item-freeze event."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess,sys
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
subprocess.run([sys.executable,str(ROOT/'tools/probe_immolation_routing_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
def routine(rel,name):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(i['sha'] for i in tree['tree'] if i['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();text=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;e=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
 body=text[m.start():e];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
flags=routine('source/D2Game/src/MISSILES/MissMode.cpp','MISSMODE_SetDamageFlags');trigger=routine('source/D2Game/src/UNIT/SUnitEvent.cpp','SUNITEVENT_Trigger');freeze=routine('source/D2Game/src/SKILLS/SkillItem.cpp','SKILLITEM_EventFunc14_Freeze');events=routine('source/D2Game/src/UNIT/SUnitDmg.cpp','SUNITDMG_ExecuteEvents')
assert 'SUNITEVENT_Trigger(pGame, UNITEVENT_DOMISSILEDMG, pAttacker, pDefender, pDamage);' in events
base=(STAGE/'immolation-routing-source-probe.cpp').read_text().split('int main()',1)[0]
base=base.replace('struct D2UnitStrc{','struct D2UnitEventStrc;struct D2UnitStrc{D2UnitEventStrc* pSrvUnitEventsList=nullptr;')
base=base.replace('int dwPhysDamage=0;','int dwPhysDamage=0,dwFrzLen=0,dwHitClass=0,nHitClassActiveSet=0,dwPiercePct=0;')
base=base.replace('nCollideFriend=0;','nCollideFriend=0,nKnockBack=0;')
old='void MISSMODE_SetDamageFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){flagCalls++;}';assert old in base;base=base.replace(old,'void MISSMODE_SetDamageFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*);')
base+=r'''
using D2C_UnitEventTypes=int;
constexpr int UNITEVENTFLAG_IN_CALLBACK=1,UNITEVENTFLAG_SHOULD_BE_FREED=2,STATE_UNINTERRUPTABLE=2;
constexpr int DAMAGERESULTFLAG_SUCCESSFULHIT=1,DAMAGERESULTFLAG_GETHIT=4,DAMAGERESULTFLAG_SOFTHIT=0x4000,DAMAGERESULTFLAG_KNOCKBACK=8,DAMAGERESULTFLAG_EVADE=0x100,DAMAGERESULTFLAG_AVOID=0x200,DAMAGERESULTFLAG_BLOCK=0x400;
constexpr int MISSILESFLAGINDEX_GETHIT=3,MISSILESFLAGINDEX_SOFTHIT=4,DAMAGEHITFLAG_32=32,DAMAGEHITFLAG_128=128,STAT_DAMAGE_FRAMERATE=100,STAT_LEVEL=101;
struct D2UnitEventStrc{int nUnitEvent=6,nFlags=0,nQueueNo=1,nGUID1=134<<16,nGUID2=0;D2UnitEventStrc* pNext=nullptr;int(*pCallback)(D2GameStrc*,int,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*,int,int)=nullptr;};
D2UnitEventStrc event;bool weaponSource=true;int eventCalls=0,rawFreeze=0,ownerDelivered=0;
void SUNITEVENT_FreeEvent(D2GameStrc*,D2UnitStrc*,D2UnitEventStrc*){}
unsigned SUNITDMG_ApplyBlockOrDodge(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int){return 0;}
int MISSILE_GetHitClass(D2UnitStrc*){return 10;}int MISSILE_GetFlags(D2UnitStrc*){return weaponSource?1:0;}
void D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70(D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,D2DamageStrc*);
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){}
'''+trigger+'\n'+freeze+r'''
void SUNITDMG_ExecuteEvents(D2GameStrc* g,D2UnitStrc* attacker,D2UnitStrc* defender,int,D2DamageStrc* d){
 if(d->dwFrzLen){rawFreeze=d->dwFrzLen;ownerDelivered=attacker==&owner;return;}
 if(d->dwHitFlags&32){eventCalls++;SUNITEVENT_Trigger(g,6,attacker,defender,d);}
}
'''+flags+r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;std::printf("[");
for(int weapon:{0,1})for(int own:{0,1})for(int ownerFreeze:{0,1})for(int missileFreeze:{0,1})for(int roll:{0,99}){
 parent={};owner={};target={};parent.pGame=&game;parent.pMissileData=&parent.md;parent.freeze=missileFreeze;owner.dwUnitType=0;target.dwUnitType=1;owner.stats[134]=ownerFreeze;owner.stats[STAT_LEVEL]=99;target.stats[STAT_LEVEL]=1;event={};event.pCallback=SKILLITEM_EventFunc14_Freeze;owner.pSrvUnitEventsList=&event;row={};row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_GETHIT];hasRecord=hasSkill=true;hasOwner=own;terrain=0;weaponSource=weapon;randomValue=roll;randomCalls=eventCalls=rawFreeze=ownerDelivered=clears=0;created.clear();
 MISSMODE_SrvHit09_ImmolationArrow(&game,&parent,&target);D2DamageStrc damage;damage.dwPhysDamage=256;MISSMODE_SetDamageFlags(&game,&parent,&target,&damage);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",weapon,own,ownerFreeze,missileFreeze,roll,parent.freeze,owner.stats[134],eventCalls,rawFreeze,ownerDelivered,randomCalls);
}std::printf("]\n");}
'''
path=STAGE/'immolation-item-event-source-probe.cpp';path.write_text(base);binary=STAGE/'immolation-item-event-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==32
for weapon,own,ownerFreeze,missileFreeze,roll,after,ownerAfter,calls,frames,delivered,draws in data:
 proc=bool(weapon and own and ownerFreeze);assert after==0 and ownerAfter==ownerFreeze and calls==int(bool(weapon and own))
 assert(frames,delivered,draws)==((2*(100-roll)+25,1,1) if proc else(0,0,0))
fixture=STAGE/'immolation-item-event-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
item=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/itemstatcost.json').read_text())['item_freeze']
e=dict(referenceVersion='3.3.93847',talentId='amazon.immolationarrow',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=['MISSMODE_SetDamageFlags','SUNITEVENT_Trigger','SKILLITEM_EventFunc14_Freeze'],blockHashes=blocks,reviewedNotCompiled=['SUNITDMG_ExecuteEvents'],pinnedItemFreeze={k:v for k,v in item.items() if v!=''},probe=dict(cases=32,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete hit09 clears missile freeze, but does not clear the attacker equipment stat. Complete SetDamageFlags uses the stored owner for its successful weapon-source event path.','Complete event dispatcher passes its event-owning unit as the callback attacker. Complete item event14 reads that attacker stat134 and emits raw freeze length despite the missile stat having been cleared. Source level99/target1 ranged cases produce225 or27frames at declared rolls0/99.','Current itemstatcost134 explicitly registers event14 for domissiledamage. Native Immolation must retain the existing attacker item-freeze consumer; suppression inferred from the missile-stat clear is withdrawn. Source0 field control has no weapon-source event.'],limits=['The reviewed complete ExecuteEvents body establishes owner DOMISSILEDMG dispatch, but its body is a declared successful-weapon-event service in this probe. The final resistance/state/death application and item registration/all modes are separate services, not claimed as a full engine run.','Blocking, random streams, current modern item-event timing/ownership and complete item/proc acceptance remain separate. The corrective native test must distinguish zero ordinary chill length from preserved real Iceblink freeze.'])
out=ROOT/'tools/reference/immolation-item-event-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Immolation item-event evidence stale'
else:out.write_text(encoded)
print('Immolation:32 owner-versus-missile item-freeze event cases; attacker freeze event retained.')
