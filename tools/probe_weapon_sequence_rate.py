#!/usr/bin/env python3
"""Compare unchanged engine attack-rate and original D2R calculator skill layers."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'
CALC=ROOT/'.local/research/ias-calculator-b7d7910'
CALC_COMMIT='b7d791031b73b0c6e56a3659e9198578aed21261'


def block(text,name,javascript=False):
    pattern=(r'\bfunction\s+' if javascript else r'^.*\b')+name+r'\([^;\n]*\)\s*\n?\s*\{'
    match=re.search(pattern,text,re.M);assert match,name
    end,depth=text.index('{',match.start())+1,1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[match.start():end]


def build(check=False):
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    path='source/D2Common/src/Units/Units.cpp';raw=(STAGE/'d2moo-pinned-source'/path).read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
    routine=block(raw.decode(),'UNITS_UpdateAttackAnimRateAndVelocity')
    cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __forceinline
#define __fastcall
constexpr int STAT_ITEM_FASTERATTACKRATE=0,STAT_ATTACKRATE=1,BODYLOC_RARM=0,BODYLOC_LARM=1,ITEMTYPE_WEAPON=1,PLRMODE_SEQUENCE=1,STATEMASK_MELEEONLY=0;
struct Anim{int dwAnimSpeed=256;} anim;
struct D2UnitStrc{void* pInventory=nullptr;Anim* pAnimData=&anim;int dwUnitType=0,dwAnimMode=0,dwSeqSpeed=0,wAnimSpeed=0;} owner;
int ias=0,rate=100;
void UNITS_MergeDualWieldWeaponStatLists(D2UnitStrc*,int){}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,int,int){return ias;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){return rate;}
bool UNITS_CanDualWield(D2UnitStrc*){return false;}
D2UnitStrc* INVENTORY_GetItemFromBodyLoc(void*,int){return nullptr;}
bool ITEMS_CanBeEquipped(D2UnitStrc*){return false;}
bool ITEMS_CheckItemTypeId(D2UnitStrc*,int){return false;}
bool STATES_CheckStateMaskOnUnit(D2UnitStrc*,int){return false;}
int D2Common_11043(D2UnitStrc*){return 0;}
int D2Clamp(int x,int lo,int hi){return std::clamp(x,lo,hi);}
'''+routine+r'''
int main(){bool first=true;std::printf("{\"rates\":[");
for(int item:{0,10,20,40,120,1000})for(int stat:{-100,0,15,70,100,140,175,300})for(int mode:{0,1,2})for(int base:{128,256}){
ias=item;rate=stat;owner.dwUnitType=mode==2?1:0;owner.dwAnimMode=mode?1:0;anim.dwAnimSpeed=base;
UNITS_UpdateAttackAnimRateAndVelocity(&owner);
if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",item,stat,mode,base,owner.dwSeqSpeed,owner.wAnimSpeed);
}std::printf("]}\n");}
'''
    cpp_path=STAGE/'weapon-sequence-rate-probe.cpp';cpp_path.write_text(cpp)
    binary=STAGE/'weapon-sequence-rate-probe'
    subprocess.run(['g++','-std=c++20','-O2',str(cpp_path),'-o',str(binary)],check=True)
    data=json.loads(subprocess.check_output([str(binary)]));assert len(data['rates'])==288
    for ias,rate,mode,base,seq,anim in data['rates']:
        expected=base*max(15,min(175,rate+120*ias//(120+ias)-(30 if mode==1 else 0)))//100
        assert seq==anim==expected
    commit=json.loads((CALC/'commit.json').read_text());calc_tree=json.loads((CALC/'tree.json').read_text())
    assert commit['sha']==CALC_COMMIT and calc_tree['sha']==commit['commit']['tree']['sha']
    calc_hashes={}
    for name in ['README.md','calculator.js','constants.js']:
        content=(CALC/name).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(content)).encode()+b'\0'+content).hexdigest()==next(x['sha'] for x in calc_tree['tree'] if x['path']==name)
        calc_hashes[name]=hashlib.sha256(content).hexdigest()
    calc=(CALC/'calculator.js').read_text();functions={name:block(calc,name,True) for name in ['calculateSIAS','getSequence']}
    # Original author functions run unchanged with explicitly hidden UI skills;
    # only skill identity, player/mercenary and named slow flags vary.
    js=r'''const container=Object.fromEntries(['FANATICISM','BURST_OF_SPEED','WEREWOLF','FRENZY','MAUL','PURGE','CLEAVE','MIRRORED_BLADES','HOLY_FREEZE','MARK_OF_BEAR','LETHARGY'].map(x=>[x,x]));
const speed={},tableVariable=0,character=0,wereform=0;
const skills={STANDARD:{},JAB:{isSequence:true},IMPALE:{isSequence:true},DOUBLE_THROW:{isSequence:true},DOUBLE_SWING:{isSequence:true},DRAGON_TAIL:{},DODGE:{}};
const wt={ONE_HANDED_THRUSTING:1,TWO_HANDED_THRUSTING:2,UNARMED:3};const char={DESERT_MERCENARY:9};
const number={SLOWED_BY:{value:0}};const constants={checkbox:{MARK_OF_BEAR:{checked:false},DECREPIFY:{checked:false},CHILLED:{checked:false},LETHARGY:{checked:false}}};
let skill=skills.STANDARD,player=true;function isElementHidden(){return true;}function isCharacterSelected(){return player;}function log(){}
'''+functions['calculateSIAS']+'\n'+functions['getSequence']+r'''
let sias=[],sequences=[];
for(let name of ['STANDARD','JAB','IMPALE','DOUBLE_THROW'])for(let pc of [0,1])for(let slow of [0,40,100])for(let decrep of [0,1])for(let chill of [0,1]){
skill=skills[name];player=!!pc;number.SLOWED_BY.value=slow;constants.checkbox.DECREPIFY.checked=!!decrep;constants.checkbox.CHILLED.checked=!!chill;
sias.push([name,pc,slow,decrep,chill,calculateSIAS()]);
}
for(let name of ['JAB','IMPALE'])for(let type of [1,2,3]){skill=skills[name];sequences.push([name,type,getSequence(type)]);}
skill=skills.DOUBLE_THROW;sequences.push(['DOUBLE_THROW',1,getSequence(1)]);
process.stdout.write(JSON.stringify({sias,sequences})+'\n');
'''
    js_path=STAGE/'weapon-sequence-modern-probe.js';js_path.write_text(js)
    modern=json.loads(subprocess.check_output(['node',str(js_path)]));assert len(modern['sias'])==96 and len(modern['sequences'])==7
    for name,player,slow,decrep,chill,result in modern['sias']:
        assert result==-slow-50*decrep-50*chill-(30 if player and name in ['JAB','DOUBLE_THROW'] else 0)
    assert modern['sequences']==[['JAB',1,18],['JAB',2,21],['JAB',3,13],['IMPALE',1,21],['IMPALE',2,24],['IMPALE',3,13],['DOUBLE_THROW',1,12]]
    data.update(modern);fixture=STAGE/'weapon-sequence-rate-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes={path:hashlib.sha256(raw).hexdigest()},
        compiledRoutine='UNITS_UpdateAttackAnimRateAndVelocity',blockHash=hashlib.sha256(routine.encode()).hexdigest(),
        modernOriginalAuthor=dict(repository='https://github.com/Warren1001/IAS_Calculator',commit=CALC_COMMIT,date=commit['commit']['committer']['date'],
            fileHashes=calc_hashes,functionHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in functions.items()},
            scope='Original calculator implementation and author firsthand Jab validation notes; README credits D2R3.3 sequence dump. Supplementary current-family evidence, not a3.3.93847 executable oracle.'),
        probe=dict(engineRows=288,modernSkillRateRows=96,modernSequenceMetadataRows=7,fixture=str(fixture.relative_to(ROOT))),
        established=[
            'Unchanged engine routine applies item IAS diminishing return, adds attackrate, subtracts30 for player sequence mode, clamps15..175, then multiplies base animation speed. Monster sequence mode does not receive the player penalty.',
            'Original author calculateSIAS adds player sequence-30 and Impale+30 in the same additive skill-rate layer before final EIAS clamp. Released2.4 and current Impale Param9=30 corroborate the modern addition. It does not mean multiply the final rounded duration by0.7 or the final speed by1.3.',
            'Original current-family getSequence agrees with legacy Jab1HT18/2HT21, Impale1HT21/2HT24, DoubleThrow12. Unarmed13 is recorded metadata, not enabled eligibility.'
        ],limits=[
            'Engine dual-weapon merge/selection and melee-only base-speed override services are declared inactive; their stat graph, current paired IAS/WSM bugs and native hand mapping are not certified.',
            'Modern functions use declared hidden UI skills and named slow flags; no full DOM calculator or live gameplay trace. Skill curve formulas and other skill layers are not adopted by this probe.',
            'No Impale handler or class certificate enabled by this proof; source start7 special wear/precomputed damage and three-field target slow still require native integration.'
        ])
    target=ROOT/'tools/reference/weapon-sequence-rate-evidence.json';rendered=json.dumps(evidence,indent=2)+'\n'
    if check:assert target.read_text()==rendered,'Weapon sequence rate evidence changed'
    else:target.write_text(rendered)
    print('Weapon sequence rate:288 engine rows,96 original modern skill-rate rows,7 sequence metadata rows passed.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
