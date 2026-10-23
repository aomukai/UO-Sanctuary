#!/usr/bin/env python3
"""Compile pinned bow-ammunition selection and typed projectile scaling helpers."""
from pathlib import Path
import hashlib,json,re,subprocess

ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
hashes={};blocks={};reviewed={}
def source(path):
    raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();blob=next(v['sha'] for v in tree['tree'] if v['path']==path)
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==blob
    hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
def routine(text,name,destination=blocks):
    m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
    end=text.index('{',m.start())+1;depth=1
    while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
    destination[name]=text[m.start():end];return destination[name]+'\n'
skills=source('source/D2Game/src/SKILLS/Skills.cpp');ama=source('source/D2Game/src/SKILLS/SkillAma.cpp')
missile=source('source/D2Common/src/Units/Missile.cpp')
ammo=[routine(skills,n) for n in ['sub_6FD11340','sub_6FD118C0','sub_6FD119C0']]
start=routine(ama,'SKILLS_SrvSt04_Arrow_Bolt')
add=routine(missile,'MISSILE_AddStatsToDamage');scale=routine(missile,'MISSILE_CalculateFinalDamage')
fields=sorted(set(re.findall(r'pMissileDamageData->(\w+)',add+scale)))
stats=sorted(set(re.findall(r'\bSTAT_\w+',add)))
base_stats=['STAT_QUANTITY','STAT_DURABILITY','STAT_ITEM_MAGICARROW','STAT_ITEM_THROWABLE']
all_stats=base_stats+stats
cpp=r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#include <string>
#define __fastcall
#define __stdcall
constexpr int UNIT_PLAYER=0,ITEMTYPE_BOW=1,ITEMTYPE_CROSSBOW=2,BODYLOC_RARM=1,BODYLOC_LARM=2;
struct D2GameStrc{} game;
struct D2SkillStrc{int id=12;} skill;
struct D2UnitStrc{int dwUnitType=0,kind=0,ammo=0,stack=0,thrown=0,maxDur=20;void* pInventory=this;int stats[64]={};} owner,weapon,rightArm,leftArm;
bool hasWeapon=true,hasSkill=true,usable=true;int arms=2,cleanups=0,packets=0;
std::string trace;
'''
cpp+='enum Stats{'+','.join(all_stats)+'};\n'
cpp+='struct D2MissileDamageDataStrc{'+''.join('int '+name+'=0;' for name in fields)+'};\n'
cpp+=r'''D2UnitStrc* sub_6FC7C7B0(D2UnitStrc*){return hasWeapon?&weapon:nullptr;}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return hasSkill?&skill:nullptr;}
int ITEMS_GetAmmoType(D2UnitStrc* p){return p->ammo;}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc* p,const char*,int){return p->id;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int s,int){return p->stats[s];}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* p,int s,int){return p->stats[s];}
void STATLIST_SetUnitStat(D2UnitStrc* p,int s,int v,int){p->stats[s]=v;}
D2UnitStrc* INVENTORY_GetItemFromBodyLoc(void* inv,int hand){return !inv?nullptr:hand==BODYLOC_RARM?(arms&1?&rightArm:nullptr):(arms&2?&leftArm:nullptr);}
bool ITEMS_CheckItemTypeId(D2UnitStrc* p,int type){return p && p->kind==type;}
bool ITEMS_CheckIfStackable(D2UnitStrc* p){return p->stack;}
bool ITEMS_CheckIfThrowable(D2UnitStrc* p){return p->thrown;}
bool ITEMS_CanItemBeUsedForThrowSkill(D2UnitStrc* p){return usable && p->stats[STAT_QUANTITY]>0;}
void sub_6FC51310(D2GameStrc*,D2UnitStrc*){trace+='Q';}
void* SUNIT_GetClientFromPlayer(D2UnitStrc*,const char*,int){return nullptr;}
void D2GAME_PACKETS_SendPacket0x3E_6FC3EC20(void*,D2UnitStrc*,int,int,int,int){packets++;}
int STATLIST_GetMaxDurabilityFromUnit(D2UnitStrc* p){return p->maxDur;}
void sub_6FC80A30(D2GameStrc*,D2UnitStrc*){cleanups++;trace+='A';}
void sub_6FC80E10(D2GameStrc*,D2UnitStrc*){cleanups++;trace+='E';}
'''
cpp+='\n'.join(ammo)+start+add+scale
cpp+=r'''void reset(){owner={};weapon={};rightArm={};leftArm={};weapon.kind=1;weapon.ammo=1;rightArm.stack=leftArm.stack=1;hasWeapon=hasSkill=usable=true;skill.id=12;arms=2;cleanups=packets=0;trace.clear();}
int main(){std::printf("{\"ammoCases\":[");bool first=true;
for(int present:{0,1})for(int type:{0,1})for(int inventory:{0,1})for(int used:{0,1})for(int family:{0,1,2})for(int occupied:{0,1,2,3})for(int quantity:{0,1,3})for(int use:{0,1}){
reset();owner.dwUnitType=type;owner.pInventory=inventory?&owner:nullptr;hasWeapon=present;hasSkill=used;weapon.kind=family;weapon.ammo=family!=0;arms=occupied;usable=use;
rightArm.stats[STAT_QUANTITY]=leftArm.stats[STAT_QUANTITY]=quantity;rightArm.stats[STAT_DURABILITY]=leftArm.stats[STAT_DURABILITY]=5;
int eligible=sub_6FD119C0(&owner),started=SKILLS_SrvSt04_Arrow_Bolt(&game,&owner,12,1),startClean=cleanups;
cleanups=0;int consumed=sub_6FD118C0(&game,&owner);
if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",present,type,inventory,used,family,occupied,quantity,use,eligible,started,startClean,consumed,rightArm.stats[STAT_QUANTITY],leftArm.stats[STAT_QUANTITY],cleanups,packets,(occupied&1?rightArm:leftArm).stats[STAT_DURABILITY]);}
std::printf("],\"scaleCases\":[");first=true;
for(int factor:{0,32,64,96,128})for(int value:{0,1,2,3,127,128,255,256,257,4095})for(int overrideLength:{0,1,40}){
reset();D2MissileDamageDataStrc payload;
'''
for index,name in enumerate(fields):cpp+=f'payload.{name}={index+1};\n'
for index,name in enumerate(stats):cpp+=f'owner.stats[{name}]=value+{index};\n'
cpp+='owner.stats[STAT_SKILL_POISON_OVERRIDE_LENGTH]=overrideLength;\nMISSILE_AddStatsToDamage(&payload,&owner,8);\n'
cpp+='if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,[",factor,value,overrideLength);\n'
for index,name in enumerate(fields):cpp+=f'std::printf("{"," if index else ""}%d",payload.{name});\n'
cpp+='std::printf("],[");MISSILE_CalculateFinalDamage(&payload,factor);\n'
for index,name in enumerate(fields):cpp+=f'std::printf("{"," if index else ""}%d",payload.{name});\n'
cpp+='std::printf("]]");}std::printf("]}\\n");}\n'
path=STAGE/'bow-projectile-source-probe.cpp';path.write_text(cpp);binary=STAGE/'bow-projectile-source-probe'
subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]))
for present,kind,inventory,used,family,arms,qty,use,eligible,started,start_clean,consumed,right,left,cleanups,packets,dur in data['ammoCases']:
    candidate=bool(kind==0 and inventory and arms and family!=0)
    valid=kind!=0 or bool(present and inventory and used and arms and family!=0 and qty>0 and use)
    assert (eligible,started,start_clean)==(int(valid),int(valid),0 if valid else 2)
    # Selection itself doesn't recheck use-state, used skill or equipped weapon presence.
    selected=bool(candidate and present)
    if not present:
        selected=False  # null weapon fails bow check; arm candidates are not that weapon.
    assert consumed==(qty if selected else 0)
    assert (right,left)==(max(0,qty-1) if selected and arms&1 else qty,max(0,qty-1) if selected and not arms&1 else qty)
    assert (cleanups,packets,dur)==(int(selected),2 if selected else 0,20 if selected else 5)
# The exact unchanged function scales only fields present in its assignments.
scaled=set(re.findall(r'pMissileDamageData->(\w+)\s*= nSrcDamage',scale))
additions={
 'nFireMinDamage':('STAT_FIREMINDAM',8),'nFireMaxDamage':('STAT_FIREMAXDAM',8),
 'nLightMinDamage':('STAT_LIGHTMINDAM',8),'nLightMaxDamage':('STAT_LIGHTMAXDAM',8),
 'nMagicMinDamage':('STAT_MAGICMINDAM',8),'nMagicMaxDamage':('STAT_MAGICMAXDAM',8),
 'nColdMinDamage':('STAT_COLDMINDAM',8),'nColdMaxDamage':('STAT_COLDMAXDAM',8),
 'nColdLength':('STAT_COLDLENGTH',0),'nPoisonMinDamage':('STAT_POISONMINDAM',0),
 'nPoisonMaxDamage':('STAT_POISONMAXDAM',0),'nLifeDrainMinDamage':('STAT_LIFEDRAINMINDAM',0),
 'nManaDrainMinDamage':('STAT_MANADRAINMINDAM',0),'nBurningMin':('STAT_BURNINGMIN',0),
 'nBurningMax':('STAT_BURNINGMAX',0),'nBurnLength':('STAT_FIRELENGTH',0)}
for factor,value,override,before,after in data['scaleCases']:
    for index,(name,actual) in enumerate(zip(fields,before)):
        expected=index+1
        if name in additions:
            stat,shift=additions[name];expected+=(value+stats.index(stat))<<shift
        elif name=='nPoisonCount':expected+=0 if override>0 else value+stats.index('STAT_POISON_COUNT')
        elif name=='nPoisonLength':expected+=override if override>0 else value+stats.index('STAT_POISONLENGTH')
        assert actual==expected,(name,value,override,actual,expected)
    for name,a,b in zip(fields,before,after):assert b==(factor*a//128 if name in scaled else a),(name,factor,a,b)
(STAGE/'bow-projectile-source-probe.json').write_text(json.dumps(dict(fields=fields,**data),separators=(',',':'))+'\n')
for text,name in [(skills,'sub_6FD12950'),(skills,'D2GAME_SKILLS_Handler_6FD12BA0'),(missile,'MISSILE_CalculateDamageData'),(missile,'MISSILE_SetDamageStats')]:routine(text,name,reviewed)
events=source('source/D2Game/src/UNIT/SUnitDmg.cpp');routine(events,'SUNITDMG_ExecuteEvents',reviewed)
creation=source('source/D2Game/src/MISSILES/Missiles.cpp');routine(creation,'MISSILES_CreateMissileFromParams',reviewed)
mode=source('source/D2Game/src/MISSILES/MissMode.cpp');routine(mode,'MISSMODE_SetDamageFlags',reviewed)
routine(mode,'MISSMODE_FillDamageParams',reviewed)
misc_path=ROOT/'tools/reference/d2r-named-equipment/json/misc.json'
modern_ammo=[{k:v for k,v in row.items() if k in ('name','code','type','stackable','minstack','maxstack','spawnstack')}
    for row in json.loads(misc_path.read_text()).values() if row.get('code') in ('aqv','cqv')]
assert len(modern_ammo)==2 and all(row['maxstack']==500 for row in modern_ammo)
evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,
 modernAmmo=dict(tableHash=hashlib.sha256(misc_path.read_bytes()).hexdigest(),rows=modern_ammo),
 probe=dict(totalCases=sum(map(len,data.values())),caseCounts={k:len(v) for k,v in data.items()},compiledRoutines=list(blocks),blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},fields=fields,scaledFields=sorted(scaled),dataPath='.local/class-skills/bow-projectile-source-probe.json'),
 reviewedBlockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in reviewed.items()},
 established=[
 'Compiled start4 succeeds only when its ammo eligibility service succeeds; rejection calls quantity/equipment and durability cleanup. Compiled player eligibility checks used skill, weapon and inventory with right-arm priority. The retained basic Magic Arrow exception is outside this Multiple Shot fixture (skillId12).',
 'Compiled decrement selects one stack in right-arm then left-arm order for bow/crossbow. It subtracts one, clamps at zero, sends quantity, restores ammunition durability to its max if needed, then calls cleanup. The decrement return is the former quantity; the generic do wrapper does not multiply decrements by fan count.',
 'Reviewed start wrapper consumes mana after successful start4. Reviewed do wrapper calls do8 first and decrements once after a successful result; a failed target lookup inside do8 still returns success, while an invalid missile record fails. These whole wrappers are reviewed, not compiled by this fixture.',
 'Compiled final scaling covers elemental endpoints, cold duration, poison rate AND poison count, life/mana drain minima, burn endpoints and burn duration. Poison duration is not scaled; the reviewed impact reader divides duration only when poison count exceeds1. Multiplying only total damage by0.75 would lose this metadata.',
 'Reviewed physical creation scales base weapon endpoints by SrcDam/128 before publishing damage percent. Generic/mastery/deadly critical is already a launch flag, independently proved by throwing-projectile-engine-evidence.json. Modern Multiple Shot calc5 Guided Arrow hard-point synergy is not present in the older do8 body.',
 'Reviewed outer flag0x10000 becomes missile flag2 and damage hitflag128. ExecuteEvents suppresses attacker DOMISSILEDMG when128 is set; defender DAMAGEDBYMISSILE remains. This does not prove every individual on-striking/item effect is excluded.'
 ],limits=[
 'Declared inventory, use-state, skill identity, stats, packet and cleanup services. Native D2 equipped-quiver reload/destruction/client paths are not simulated; stale do-time ammo and start/do state changes remain acceptance work.',
 'Only six helper routines are compiled. Full damage creator/stat writer, mana wrappers, seed algorithm, event/proc dispatch and modern current calc5 addition are reviewed dependencies, not complete executable proof.',
 'No native runtime behavior or class handler is enabled by this source probe. Existing bow basic attacks currently bypass native OnFired and have no arrow/bolt debit; native NamedAmmunition covers throwing items. Reuse native Arrow/Bolt stacks for a documented port adapter, without inheriting unrelated UO lower-ammo-cost or recovery rules.',
 'Pinned aqv/cqv quivers have maxstack500/minstack150/spawnstack250. Native Arrow/Bolt supply/capacity and equipped-quiver inventory/reload projection require separate acceptance; this probe does not change native stack limits or existing crafting/saves.',
 'The final scaling helper is exercised directly at factor0; the reviewed full creator skips its source branch when source damage is0. Physical endpoints, launch/impact rolls and mitigation remain separate from this typed-stat helper.',
 'Travelling weapon missile snapshots/AR/block/Pierce/next-hit/owner/active-save/client and all other Amazon skill consumers remain implementation and acceptance work.'
 ])
(ROOT/'tools/reference/bow-projectile-engine-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print('Bow projectile source cases:',evidence['probe']['totalCases'])
