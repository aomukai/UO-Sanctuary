#!/usr/bin/env python3
"""Execute complete older Static Field wrapper/callback/percentage with explicit services."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'static-field-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';blocks={};sources={}
    def body(rel,name):
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
        s=function(raw.decode(),name);blocks[name]=hashlib.sha256(s.encode()).hexdigest();sources[rel]=sha(p);return s
    header=BASE/'d2moo-pinned-source/source/D2Common/include/D2Combat.h';raw=header.read_bytes();rel='source/D2Common/include/D2Combat.h'
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
    import re
    enum=raw.decode().split('enum D2C_ElemTypes',1)[1].split('};',1)[0]
    assert re.findall(r'ELEMTYPE_\w+',enum).index('ELEMTYPE_LTNG')==2;sources[rel]=sha(header)
    cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <initializer_list>
#include <vector>
#define __fastcall
struct D2GameStrc{int nDifficulty=0;bool bExpansion=true;};
struct D2UnitStrc{int life=0,maximum=256000,resistance=0;};
struct D2StaticFieldCallbackArgStrc{int nMinDamage=0,nDamagePct=0,nStaticFieldMin=0,nElementalLength=0,nElementalType=0;};
struct D2SkillsTxt{int dwCalc[2]={0,1};int dwAuraRangeCalc=2,dwAuraFilter=0x8783,nEType=2;};
struct D2DifficultyLevelsTxt{int dwStaticFieldMin=0;};
struct D2AuraCallbackStrc{D2GameStrc* pGame;D2UnitStrc* pOwner;void* pArgs;};
struct D2DamageStrc{int nLightningDamage=0,dwHitClass=0,nHitClassActiveSet=0;uint16_t wResultFlags=0;};
constexpr int STAT_HITPOINTS=0,STAT_LIGHTRESIST=1;
bool skillValid=true,difficultyValid=true;int minimum=0,rankNow=1,enumerations=0,callbackResult=-1,rawFirst=-1,rawSecond=-1,eventCalls=0,damageCalls=0,finalDamage=0,radius=-1,filter=-1;
std::vector<int> order;D2SkillsTxt skill;D2DifficultyLevelsTxt difficulty;D2UnitStrc target;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return skillValid?&skill:nullptr;}
D2DifficultyLevelsTxt* DATATBLS_GetDifficultyLevelsTxtRecord(int){return difficultyValid?&difficulty:nullptr;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int code,int,int rank){return code==0?25:code==1?minimum:5+rank-1;}
int SKILLS_GetElementalLength(D2UnitStrc*,int,int,int){return 0;}
int STATLIST_UnitGetStatValue(D2UnitStrc* u,int stat,int){return stat==STAT_HITPOINTS?u->life:u->resistance;}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc* u){return u->maximum;}
void sub_6FD11E40(D2UnitStrc*,D2DamageStrc* p,int type,int damage,int length,int* stat,int* outType){if(stat){rawFirst=damage;*stat=STAT_LIGHTRESIST;}else{rawSecond=damage;}if(outType){*outType=type;}p->nLightningDamage=damage;order.push_back(stat?1:2);}
'''
    cpp+=body('source/D2Game/src/MONSTER/MonsterUnique.cpp','MONSTERUNIQUE_CalculatePercentage')
    cpp+=r'''
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,D2DamageStrc* p){eventCalls++;order.push_back(3);if(p->dwHitClass!=13||p->nHitClassActiveSet!=1||p->wResultFlags!=0x4001){__builtin_trap();}}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc* t,D2DamageStrc* p){damageCalls++;order.push_back(4);finalDamage=MONSTERUNIQUE_CalculatePercentage(p->nLightningDamage,std::max(0,100-t->resistance),100);}
int __fastcall SKILLS_AuraCallback_StaticField(D2AuraCallbackStrc*,D2UnitStrc*);
void sub_6FD0FE80(D2GameStrc* g,D2UnitStrc* owner,int,int,int range,int mask,int(*callback)(D2AuraCallbackStrc*,D2UnitStrc*),void* args,int,const char*,int){enumerations++;radius=range;filter=mask;D2AuraCallbackStrc c{g,owner,args};callbackResult=callback(&c,&target);}
'''
    cpp+=body('source/D2Game/src/SKILLS/SkillSor.cpp','SKILLS_SrvDo020_StaticField')+'\n'+body('source/D2Game/src/SKILLS/SkillSor.cpp','SKILLS_AuraCallback_StaticField')
    cpp+=r'''
int main(){printf("[");bool comma=false;D2UnitStrc owner;
for(int rank:{1,20,255})for(int floor:{0,33,50})for(int expansion:{0,1})for(int life:{0,1,255,256,257,511,512,1023,1024,128017,84481,128257,256017,1048577,256000000})for(int resistance:{-100,-50,0,50,100})for(int minDamage:{0,1,256})for(int valid=0;valid<4;valid++){
D2GameStrc game{0,bool(expansion)};difficulty.dwStaticFieldMin=floor;skillValid=valid&1;difficultyValid=valid&2;target={life,std::max(life,256000),resistance};minimum=minDamage;enumerations=eventCalls=damageCalls=finalDamage=0;callbackResult=rawFirst=rawSecond=radius=filter=-1;order.clear();
int result=SKILLS_SrvDo020_StaticField(&game,&owner,42,rank);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",comma?",":"",rank,floor,expansion,life,target.maximum,resistance,minDamage,valid,result,enumerations,callbackResult,rawFirst,rawSecond,eventCalls,damageCalls,finalDamage,radius,filter);comma=true;bool first=true;for(int n:order){printf("%s%d",first?"":",",n);first=false;}printf("]]");}printf("]\n");}
'''
    p=STAGE/'probe.cpp';p.write_text(cpp);compiler=STAGE/'compiler.log';san=STAGE/'sanitizer.log'
    result=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(p),'-o',str(STAGE/'probe')],capture_output=True);compiler.write_bytes(result.stdout+result.stderr);assert result.returncode==0,result.stderr.decode()
    result=subprocess.run([str(STAGE/'probe')],capture_output=True);san.write_bytes(result.stderr);assert result.returncode==0 and not result.stderr,result.stderr.decode();cases=json.loads(result.stdout);assert len(cases)==16200
    delivered=negative=0
    for rank,floor,expansion,life,maximum,resistance,minimum,valid,returned,enumerated,callback,raw1,raw2,events,damage,final,radius,mask,order in cases:
        assert returned==enumerated==int(valid==3)
        if valid!=3:assert callback==-1 and order==[];continue
        assert radius==rank+4 and mask==0x8783
        eligible=life>>8>=1 and (not expansion or not floor or (life>>8)>((maximum>>8)*floor//100))
        assert callback==int(eligible) and events==damage==int(eligible)
        if not eligible:assert raw1==raw2==-1 and order==[];continue
        delivered+=1;assert order==[1,2,3,4] and raw1>=minimum and raw2>=0
        if resistance<0:negative+=1;assert raw2<=raw1
        else:assert raw2==raw1
    fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    fields=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/skills.json').read_text())['42'];assert fields['aurafilter']==0x8783 and fields['EType']=='ltng' and fields['srvdofunc']==20 and fields['calc1']=='par4' and fields['Param4']==25 and fields['Param3']==0 and fields['mana']==9 and fields['aurarangecalc']=='ln12'
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],modernExecutableVerified=False,cases=len(cases),delivered=delivered,negativeResistanceCases=negative,
        completeFunctionHashes=blocks,sourceHashes=sources,artifacts={str(x.relative_to(ROOT)):sha(x) for x in (p,compiler,san,fixture)},
        established=['Complete old do20 validates skill/difficulty,prepares fixed percentage/minimum/range/element and expansion-only difficulty floor,then enumerates its aura callback.',
            'Complete callback gates on whole HP/floor before delivery,prepares whole-HP damage and source minimum,precompensates negative defender resistance,and calls typed preparation twice before events then missile damage with flags0x4001/hitclass13.',
            'Complete source percentage helper preserves large-value early-division behavior. Source floor gating and negative compensation are separate from Sanctuary final-floor clamping/common typed mitigation.'],
        services=['Aura enumeration selects one declared target;formula/skill/difficulty/stat/typed-preparation/event/final-mitigation services are explicit. The final resistance service uses the complete source percentage helper,not the full engine damage receiver.',
            'No modern executable equivalence,current aura geometry,PvP/Sunder/item-pierce/absorption/MDR/event/floor-order equivalence or class completion is claimed.'])
    out=ROOT/'tools/reference/static-field-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print(f'Static Field:{len(cases)}complete old wrapper/callback/percentage cases;{delivered}deliveries/{negative}negative-resistance paths;modern gaps explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
