#!/usr/bin/env python3
"""Compile the complete immutable missile damage creator with explicit stat services."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'


def build(check=False):
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    path='source/D2Common/src/Units/Missile.cpp';raw=(STAGE/'d2moo-pinned-source'/path).read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
    text=raw.decode();blocks={}
    def routine(name):
        m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
        end=text.index('{',m.start())+1;depth=1
        while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
        blocks[name]=text[m.start():end];return blocks[name]+'\n'
    creator=routine('MISSILE_CalculateDamageData');add=routine('MISSILE_AddStatsToDamage');scale=routine('MISSILE_CalculateFinalDamage')
    all_code=creator+add+scale;stats=sorted(set(re.findall(r'\bSTAT_\w+',all_code)));fields=sorted(set(re.findall(r'pMissileDamageData->(\w+)',all_code)))
    elements=['ELEMTYPE_NONE']+sorted(set(re.findall(r'\bELEMTYPE_\w+',creator))-{'ELEMTYPE_NONE'})
    flag_names=sorted(set(re.findall(r'\bMISSILESFLAGINDEX_\w+',creator)))
    cpp=r'''#include <cstdint>
#include <cstdio>
#include <cstring>
#include <initializer_list>
#define __fastcall
#define __stdcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_MISSILE=3,UNIT_ITEM=4,ITEMTYPE_BLUNT=1;
struct MissileData{int nSkill=6;}missileData;
struct D2UnitStrc{int dwUnitType=0,dwClassId=0;void* pInventory=this;MissileData* pMissileData=&missileData;int stats[128]={};}owner,weapon,missile;
struct D2SkillsTxt{int nSrcDam=128,nEType=0;}skill;
struct D2MissilesTxt{int nSkill=6,nSrcDamage=0,nSrcMissDmg=0,nElemType=0,nHoly=0;unsigned dwMissileFlags=0;}row;
bool critical=false;int skillMin=0,skillMax=0;unsigned gdwBitMasks[32]={};
D2MissilesTxt* DATATBLS_GetMissilesTxtRecord(int){return &row;}
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int){return &skill;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return p->stats[stat];}
D2UnitStrc* D2Common_10434(D2UnitStrc*,int){return &weapon;}
D2UnitStrc* UNITS_GetEquippedWeaponFromMonster(D2UnitStrc*){return &weapon;}
bool MISSILE_HasBonusStats(D2UnitStrc*,D2UnitStrc*){return critical;}
bool ITEMS_CheckWeaponIfTwoHanded(D2UnitStrc*){return true;}
int SKILLS_GetWeaponMasteryBonus(D2UnitStrc*,D2UnitStrc*,void*,int){return 0;}
short ITEMS_GetStrengthBonus(D2UnitStrc*){return 0;}
short ITEMS_GetDexBonus(D2UnitStrc*){return 100;}
bool ITEMS_CheckIfThrowable(D2UnitStrc*){return false;}
int INVENTORY_GetWieldType(D2UnitStrc*,void*){return 1;}
bool ITEMS_CheckItemTypeId(D2UnitStrc*,int){return false;}
int MONSTERS_GetHirelingTypeId(D2UnitStrc*){return 0;}
void UNITS_MergeDualWieldWeaponStatLists(D2UnitStrc*,int){}
int SKILLS_GetMinPhysDamage(D2UnitStrc*,int,int,int){return skillMin;}
int SKILLS_GetMaxPhysDamage(D2UnitStrc*,int,int,int){return skillMax;}
int SKILLS_GetMinElemDamage(D2UnitStrc*,int,int,int){return 0;}
int SKILLS_GetMaxElemDamage(D2UnitStrc*,int,int,int){return 0;}
int SKILLS_GetElementalLength(D2UnitStrc*,int,int,int){return 0;}
int MISSILE_GetMinDamage(D2UnitStrc*,D2UnitStrc*,int,int){return 0;}
int MISSILE_GetMaxDamage(D2UnitStrc*,D2UnitStrc*,int,int){return 0;}
int MISSILE_GetMinElemDamage(D2UnitStrc*,D2UnitStrc*,int,int){return 0;}
int MISSILE_GetMaxElemDamage(D2UnitStrc*,D2UnitStrc*,int,int){return 0;}
int MISSILE_GetElementalLength(int,D2UnitStrc*,int,int){return 0;}
int MISSILE_CalculateMasteryBonus(D2UnitStrc*,int,int){return 0;}
'''
    cpp+='enum Stats{'+','.join(stats)+'};\nenum Elements{'+','.join(elements)+'};\nenum Flags{'+','.join(flag_names)+'};\n'
    cpp+='struct D2MissileDamageDataStrc{'+''.join('int '+field+'=0;' for field in fields)+'};\n'+add+scale+creator
    # Source stat endpoints/rates are supplied without applying source damage.
    supplied={'STAT_FIREMINDAM':10,'STAT_FIREMAXDAM':31,'STAT_LIGHTMINDAM':12,'STAT_LIGHTMAXDAM':49,'STAT_MAGICMINDAM':1,'STAT_MAGICMAXDAM':2,
        'STAT_COLDMINDAM':7,'STAT_COLDMAXDAM':13,'STAT_COLDLENGTH':15,'STAT_POISONMINDAM':257,'STAT_POISONMAXDAM':513,'STAT_POISONLENGTH':101,'STAT_POISON_COUNT':2,
        'STAT_LIFEDRAINMINDAM':7,'STAT_MANADRAINMINDAM':5,'STAT_DEXTERITY':42,'STAT_ITEM_MINDAMAGE_PERCENT':5,'STAT_ITEM_MAXDAMAGE_PERCENT':15,
        'STAT_ITEM_DEMONDAMAGE_PERCENT':11,'STAT_ITEM_UNDEADDAMAGE_PERCENT':13}
    cpp+=r'''int main(){bool first=true;for(int i=0;i<32;i++){gdwBitMasks[i]=1u<<i;}std::printf("[");
for(int lo:{1,9,127})for(int src:{0,64,96,128})for(int s:{0,256,5120})for(int pct:{-200,0,37,150})for(int crit:{0,1}){
 owner={};weapon={};missile={};weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;skill.nSrcDam=src;skill.nEType=ELEMTYPE_NONE;
 skillMin=s;skillMax=s+512;critical=crit;owner.stats[STAT_MINDAMAGE]=lo;owner.stats[STAT_MAXDAMAGE]=lo+5;owner.stats[STAT_DAMAGEPERCENT]=pct;
'''
    for key,value in supplied.items():cpp+=f'owner.stats[{key}]={value};\n'
    cpp+='D2MissileDamageDataStrc damage;MISSILE_CalculateDamageData(&damage,&owner,nullptr,&missile,1);\n'
    cpp+='if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,[",lo,src,s,pct,crit);\n'
    for i,field in enumerate(fields):cpp+=f'std::printf("{"," if i else ""}%d",damage.{field});\n'
    cpp+='std::printf("]]");}std::printf("]\\n");}\n'
    p=STAGE/'bow-launch-source-probe.cpp';binary=STAGE/'bow-launch-source-probe';p.write_text(cpp)
    subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==288
    for lo,src,s,pct,critical,values in data:
        value=dict(zip(fields,values));assert value['nMinDamage']==s+lo*256*src//128 and value['nMaxDamage']==s+512+(lo+5)*256*src//128
        assert value['nDamagePercent']==max(-90,pct+57 if src else 0)
        assert value['nDemonDamagePercent']==(11 if src else 0) and value['nUndeadDamagePercent']==(13 if src else 0)
        assert bool(value['nFlags']&2)==bool(src and critical)
    fixture=STAGE/'bow-launch-source-probe.json';fixture.write_text(json.dumps(dict(fields=fields,cases=data),separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHash=hashlib.sha256(raw).hexdigest(),compiledRoutines=list(blocks),blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},
        sourceInputs=supplied,probe=dict(cases=len(data),fixture=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),
        established=['The complete unchanged creator scales weapon endpoints by SrcDam before adding skill physical endpoints; the damage percentage is saved separately and floored at-90. Saved species percentages are not scaled by SrcDam. Critical is a per-created-missile flag from its declared selection service.',
        'SrcDam0 does not collect owner weapon stats, critical, species bonuses or typed/rate/count/duration/drain fields. It retains skill physical endpoints. SrcDam64/96/128 collect owner stats and the unchanged final scaling helper preserves poison aggregate length while scaling rate and count.',
        'Declared owner is a player with one nonthrowing, nonblunt bow, Dexterity42 and100percent weapon Dexterity contribution. Item damage percentages5/15 select15; generic owner percent is varied separately. Elemental skill damage is0 in this matrix.'],
        services=['Unit/table/stat/weapon lookup, skill endpoint calculation, weapon/mastery characteristics, critical selection and dual stat merge are declared services. The complete damage creator and typed addition/scaling bodies execute unchanged. Real gear, current seed, proc/stat inheritance and current executable are not simulated.'],
        limits=['Only ordinary single-bow/player/physical-skill cases are exercised. Source monster/hireling/origin/throwing/half-two-hand, elemental skill and immunity-bypass/nested type branches are compiled but not certified by this matrix.','Sanctuary material/potion additions and current ranged Enchant/aura rules need separate native consumer checks; they are not supplied as invented D2 source stats.'])
    target=ROOT/'tools/reference/bow-launch-engine-evidence.json';rendered=json.dumps(evidence,indent=2)+'\n'
    if check:assert target.read_text()==rendered,'Bow launch source evidence changed'
    else:target.write_text(rendered)
    print('Bow launch:288 complete compiled creator cases pass; native gear collection remains separate.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
