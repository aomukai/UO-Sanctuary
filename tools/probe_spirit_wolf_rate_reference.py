#!/usr/bin/env python3
"""Execute complete nonweapon attack-rate writer, A1 predicate and initial scheduler."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'


def build(check=False):
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes={};routines={}
    def source(path):
        raw=(BASE/'d2moo-pinned-source'/path).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
        hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
    def routine(text,name):
        m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
        end=text.index('{',m.start())+1;depth=1
        while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
        block=text[m.start():end];routines[name]=hashlib.sha256(block.encode()).hexdigest();return block+'\n'
    units=source('source/D2Common/src/Units/Units.cpp');game=source('source/D2Game/src/UNIT/SUnit.cpp')
    ids=source('source/D2Common/include/DataTbls/MonsterIds.h')
    monster=source('source/D2Game/src/MONSTER/Monster.cpp')
    assert 'STATLIST_SetUnitStat(pUnit, STAT_ATTACKRATE, 100, 0);' in monster
    cpp=r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __forceinline inline
#define __fastcall
#define __stdcall
#define D2_ASSERT(x) assert(x)
using BOOL=int;
constexpr int TRUE=1,FALSE=0,UNIT_PLAYER=0,UNIT_MONSTER=1,PLRMODE_SEQUENCE=18,NUMBER_OF_PLRMODES=20,NUMBER_OF_MONMODES=16;
constexpr int STAT_ITEM_FASTERATTACKRATE=0,STAT_ATTACKRATE=1,BODYLOC_RARM=0,BODYLOC_LARM=1,ITEMTYPE_WEAPON=0,STATEMASK_MELEEONLY=0,SKILLSFLAG_USEATTACKRATE=1;
constexpr int EVENTTYPE_MODECHANGE=1,EVENTTYPE_ENDANIM=2;
struct Anim{int dwAnimSpeed=176;int pFrameFlags[144]={};}anim;
struct D2AnimSeqTxt{};
struct D2SkillsTxt{unsigned dwFlags[2]={};int nSkillId=0;};struct D2SkillStrc{D2SkillsTxt* pSkillsTxt=nullptr;};
struct D2UnitStrc{int dwUnitType=1,dwClassId=420,dwAnimMode=4,dwSeqSpeed=0,wAnimSpeed=0,dwSeqFrameCount=0,dwFrameCountPrecise=13*256,nSeqCurrentFramePrecise=0;
void* pInventory=nullptr;D2AnimSeqTxt* pAnimSeq=nullptr;Anim* pAnimData=&anim;}owner;
struct D2AnimUnkStrc{int bCanUseVelocityModifierByPassiveSkill,bCanUseVelocityModifier,bCanUseSkillAttackRate,bCanUseAttackRate,bAllowAnimSpeed;};
struct D2GameStrc{int dwGameFrame=1000;}game;
struct Event{int type,frame,kind,param;};std::vector<Event>events;
int faster=0,attack=100,alternate=0,meleeOnly=0,merges=0;
void UNITS_MergeDualWieldWeaponStatLists(D2UnitStrc*,int one){assert(one==1);merges++;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,int stat,int){assert(stat==STAT_ITEM_FASTERATTACKRATE);return faster;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int){assert(stat==STAT_ATTACKRATE);return attack;}
bool UNITS_CanDualWield(D2UnitStrc*){return false;}
D2UnitStrc* INVENTORY_GetItemFromBodyLoc(void*,int){assert(false);return nullptr;}
bool ITEMS_CanBeEquipped(D2UnitStrc*){assert(false);return false;}bool ITEMS_CheckItemTypeId(D2UnitStrc*,int){assert(false);return false;}
bool STATES_CheckStateMaskOnUnit(D2UnitStrc*,int){return meleeOnly;}
int D2Common_11043(D2UnitStrc*){return alternate;}
template<class T>T D2Clamp(T x,T a,T b){return std::clamp(x,a,b);}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){assert(false);return nullptr;}
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int){assert(false);return nullptr;}
int UNITS_GetFrameBonus(D2UnitStrc*){return 0;}
void DATATBLS_GetSequenceEvent(D2AnimSeqTxt*,int,int*){assert(false);}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int kind,int param){events.push_back({type,frame,kind,param});}
'''+ids.replace('#pragma once','')+'\n'
    for name in ['gaPlayerModesAnimModulators','gaMonModesAnimModulators','gaMonModesAnimModulatorsExpansion']:
        m=re.search(r'^(?:static )?D2AnimUnkStrc '+name+r'\[[^]]+\]\s*=\s*\{',units,re.M);assert m,name
        block=units[m.start():units.index('};',m.start())+2];routines[name]=hashlib.sha256(block.encode()).hexdigest();cpp+=block+'\n'
    cpp+=routine(units,'UNITS_CanAnimModeUseAttackRate')+routine(units,'UNITS_UpdateAttackAnimRateAndVelocity')+routine(game,'sub_6FCBCE70')
    cpp+=r'''
int main(){bool first=true;printf("[");
for(int base:{176,256,32767})for(int ias:{-119,-10,0,20,100,10000})for(int rate:{-100,0,14,15,50,100,175,250})for(int melee:{0,1})for(int alt:{0,128}){
owner={};anim={};anim.dwAnimSpeed=base;anim.pFrameFlags[9]=1;faster=ias;attack=rate;meleeOnly=melee;alternate=alt;merges=0;events.clear();
assert(UNITS_CanAnimModeUseAttackRate(UNIT_MONSTER,4,&owner,420));UNITS_UpdateAttackAnimRateAndVelocity(&owner);sub_6FCBCE70(&game,&owner);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",base,ias,rate,melee,alt,owner.wAnimSpeed,owner.dwSeqSpeed,merges);first=false;
bool sep=false;for(auto e:events){printf("%s[%d,%d,%d,%d]",sep?",":"",e.type,e.frame-1000,e.kind,e.param);sep=true;}printf("]]");
}printf("]\n");}
'''
    path=BASE/'spirit-wolf-rate-source-probe.cpp';path.write_text(cpp);binary=path.with_suffix('')
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(binary)],check=True)
    result=subprocess.run([str(binary)],capture_output=True);(BASE/'spirit-wolf-rate-sanitizer.log').write_bytes(result.stderr)
    assert result.returncode==0 and not result.stderr,result.stderr.decode()[-4000:]
    rows=json.loads(result.stdout);assert len(rows)==576
    trunc=lambda n,d:abs(n)//abs(d)*(1 if (n>=0)==(d>=0) else -1)
    for base,ias,rate,melee,alt,speed,sequence,merges,events in rows:
        effective=trunc(120*ias,ias+120) if ias else 0
        expected=max(0,min(32767,max(15,min(175,rate+effective))*(alt if melee and alt else base)//100))
        assert speed==sequence==expected and merges==1
        assert events and events[-1][0]==2
    fixture=BASE/'spirit-wolf-rate-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],installed=False,handlerEnabled=False,
        sourceHashes=hashes,unchangedRoutineHashes=routines,generatedSourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(rows)),
        sanitizers='ASan/UBSan exit0; empty runtime stderr.',
        established=[
            'Complete A1attack-rate predicate, non-dual-wield writer and complete initial event scheduler execute with current wolf13frames/action9. Source monster initialization writes base attackrate100. Item IAS uses signed120*x/(x+120), then adds total attackrate and clamps15..175; resulting animation/sequence speed clamps0..32767.',
            'Both mode tables choose the A1attack-rate route. Complete writer covers signed IAS-119/-10/0/20/100/10000, total rates-100/0/14/15/50/100/175/250, bases176/256/32767and melee-only alternate-base0/128. Zero alternate falls back to ordinary animation metadata.',
            'Initial scheduler preserves complete ordered action/end requests and high-speed skipped action cases. It creates queued events at attack entry. The rate writer itself does not modify those events.'
        ],limits=[
            'Unit identity, supplied stats, inventory/non-dual-wield/melee-only state and point animation row are declared services. Mode conversion, full monster initialization, summon equipment/IAS stat transport, global RNG/event queue and exact current D2R timing remain separate.',
            'The native actor should capture live rate at attack entry. Mid-swing stat changes do not by themselves establish a reschedule operation; current-executable mid-swing behavior and other animation-mode rates remain uncertain.',
            'Negative IAS singularity-120and values below it are not supported by the native helper. Weapon dual-wield and player sequence penalties are not wolf inheritance rules.'
            ,'The zero-speed scheduler branch is reviewed but not executed in this positive-base matrix.'
        ])
    output=ROOT/'tools/reference/spirit-wolf-rate-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Spirit Wolf attack rate:576complete source predicate/writer/initial event compositions pass; native binding and current timing remain separate.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
