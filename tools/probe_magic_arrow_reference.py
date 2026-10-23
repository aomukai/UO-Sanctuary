#!/usr/bin/env python3
"""Compile unchanged missile conversion, including its source percentage helper."""
import argparse
import ast
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'


def build(check=False):
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text())
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes,blocks={},{}
    def source(path):
        raw=(STAGE/'d2moo-pinned-source'/path).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
        hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
    def routine(text,name):
        match=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert match,name
        end=text.index('{',match.start())+1;depth=1
        while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
        blocks[name]=text[match.start():end];return blocks[name]+'\n'
    text=source('source/D2Game/src/MISSILES/MissMode.cpp')
    roll=routine(text,'MISSMODE_RollDamageValue');fill=routine(text,'MISSMODE_FillDamageParams')
    add=routine(text,'MISSMODE_AddDamageValue');convert=routine(text,'MISSMODE_SrvDmg01_FireArrow_MagicArrow_ColdArrow')
    percent=routine(source('source/D2Game/src/MONSTER/MonsterUnique.cpp'),'MONSTERUNIQUE_CalculatePercentage')
    harness=ROOT/'tools/probe_weapon_missile_impact.py'
    module=ast.parse(harness.read_text())
    cpp=next(ast.literal_eval(n.value) for n in module.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='cpp' for t in n.targets))
    # The original impact fixture's small-range percentage service is replaced
    # here by the complete unchanged source routine, including early division.
    cpp=re.sub(r'^int MONSTERUNIQUE_CalculatePercentage[^\n]*\n','',cpp,flags=re.M)
    stats=sorted(set(re.findall(r'\bSTAT_\w+',roll+fill)))
    fields=sorted(set(re.findall(r'pDamage->(\w+)',fill+add+convert)))
    elements=sorted(set(re.findall(r'\bELEMTYPE_\w+',add)))
    cpp+='enum Stats{'+','.join(stats)+'};\nenum Elements{'+','.join(elements)+'};\n'
    cpp+='struct D2DamageStrc{'+''.join('int '+n+';' for n in fields)+'};\n'
    cpp+=r'''struct D2GameStrc{}game;
struct D2MissilesTxt{unsigned dwDmgCalc=0;}row;
bool validRow=true;int conversion=0;
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return validRow?&row:nullptr;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return &target;}
int MISSILE_EvaluateMissileFormula(D2UnitStrc*,D2UnitStrc*,unsigned,int,int){return conversion;}
int MISSILE_GetElemTypeFromMissileId(int){return ELEMTYPE_MAGIC;}
'''+percent+roll+fill+add+convert
    cpp+=r'''int main(){std::printf("{\"conversionCases\":[");bool first=true;
for(int physical:{0,1,255,256,257,1048576,1048577,16640000})for(int magic:{0,257,10000})for(int pct:{-1,0,1,5,7,50,99,100,103}){
 D2DamageStrc damage={};damage.dwPhysDamage=physical;damage.dwMagDamage=magic;conversion=pct;
 MISSMODE_SrvDmg01_FireArrow_MagicArrow_ColdArrow(&game,&missile,&target,&damage);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d]",physical,magic,pct,damage.dwPhysDamage,damage.dwMagDamage);}
std::printf("],\"impactCases\":[");first=true;
for(int raw:{257,1024,65536,1048577})for(int bonus:{-90,0,37,100})for(int critical:{0,1})for(int pct:{0,5,7,50,100,150})for(int mastery:{0,25})for(int kind:{0,1,2,3}){
 missile={};target={};target.dwUnitType=UNIT_MONSTER;target.kind=kind;conversion=pct;
 missile.stats[STAT_MINDAMAGE]=missile.stats[STAT_MAXDAMAGE]=raw;
 missile.stats[STAT_MAGICMINDAM]=257;missile.stats[STAT_MAGICMAXDAM]=513;missile.stats[STAT_PASSIVE_MAG_MASTERY]=mastery;
 missile.stats[STAT_FIREMINDAM]=missile.stats[STAT_FIREMAXDAM]=1024;missile.stats[STAT_PASSIVE_FIRE_MASTERY]=mastery;
 missile.stats[STAT_DAMAGEPERCENT]=bonus;missile.stats[STAT_ITEM_DEMONDAMAGE_PERCENT]=31;missile.stats[STAT_ITEM_UNDEADDAMAGE_PERCENT]=47;
 missile.stats[STAT_ITEM_DEADLYSTRIKE]=critical;D2DamageStrc damage={};
 MISSMODE_FillDamageParams(&missile,kind?&target:nullptr,&damage);
 MISSMODE_SrvDmg01_FireArrow_MagicArrow_ColdArrow(&game,&missile,kind?&target:nullptr,&damage);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d]",raw,bonus,critical,pct,mastery,kind,damage.dwPhysDamage,damage.dwMagDamage,damage.dwFireDamage);}
std::printf("],\"guards\":[");first=true;for(int guard:{0,1}){validRow=guard==0;conversion=50;D2DamageStrc damage={};damage.dwPhysDamage=257;damage.dwMagDamage=19;
 MISSMODE_SrvDmg01_FireArrow_MagicArrow_ColdArrow(&game,guard==0?nullptr:&missile,&target,&damage);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d]",guard,damage.dwPhysDamage,damage.dwMagDamage);}
std::printf("]}\n");}
'''
    path=STAGE/'magic-arrow-source-probe.cpp';binary=STAGE/'magic-arrow-source-probe'
    path.write_text(cpp);subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
    data=json.loads(subprocess.check_output([str(binary)]))
    def split(value,pct):
        if pct<=0:return value,0
        pct=min(100,pct);converted=(value//100)*pct if value>1048576 else value*pct//100
        converted=min(converted,value);return value-converted,converted
    for physical,magic,pct,after,added in data['conversionCases']:
        a,b=split(physical,pct);assert (after,added)==(a,magic+b)
    for raw,bonus,critical,pct,mastery,kind,physical,magic,fire in data['impactCases']:
        value=raw
        p=max(-90,bonus+(31 if kind==2 else 47 if kind==3 else 0))
        if kind:value+=int(value*p/100)
        value*=1+critical;a,b=split(value,pct)
        assert (physical,magic,fire)==(a,257+257*mastery//100+b,1024+1024*mastery//100)
    assert data['guards']==[[0,257,19],[1,257,19]]
    fixture=STAGE/'magic-arrow-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
    ref=ROOT/'tools/reference/d2r-named-equipment';tables={};pins={}
    manifest=json.loads((ref/'sources.json').read_text())['files']
    for name in ('skills','missiles'):
        p=ref/'json'/f'{name}.json';pins[name]=hashlib.sha256(p.read_bytes()).hexdigest()
        assert pins[name]==manifest['json/'+name+'.json']['sha256'];tables[name]=json.loads(p.read_text())
    skill=tables['skills']['6'];missile=tables['missiles']['magicarrow']
    assert skill['noammo']==1 and not skill.get('decquant') and not skill.get('srvstfunc')
    assert skill['SrcDam']==128 and missile['DmgCalc1']=='dl12' and missile['dParam1']==5 and missile['dParam2']==2 and missile['EType']=='mag'
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,sourceHashes=hashes,pinnedTableHashes=pins,
        compiledRoutines=list(blocks),blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},sharedHarness=dict(path=str(harness.relative_to(ROOT)),sha256=hashlib.sha256(harness.read_bytes()).hexdigest()),
        probe=dict(caseCounts={k:len(v) for k,v in data.items()},fixture=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),pinnedSkill=skill,pinnedMissile=missile,
        established=['srvdmg1 converts percentage-enhanced and critical physical damage after the complete unchanged impact reader. Existing magic receives its own mastery before conversion; converted physical is added afterward without a second magic mastery. Damage channels remain separate for mitigation and leech.',
        'Literal CalculatePercentage early-divides physical values above1048576 raw FP256 before multiplying by conversion percent. Small values multiply before division. Source caps positive percentages at100; nonpositive conversion does nothing.',
        'Pinned current missile dl12 means5+2*(rank-1) percent, not the historical1percent per rank. Skill physical bonus is1per rank and SrcDam128. Pinned noammo1 and absent decquant mean no arrow/bolt debit; the row has no server start function. Mana max(0,12-(rank-1))/8 reaches0 at rank13. Dispatcher payment and launch collection are still separate integration work.'],
        services=['Declared missile formula/table/owner lookup, stat access, victim classifications, bounded draw and empty nested monster-type bonus list. The actual percentage, impact reader, conversion and element addition bodies are compiled unchanged. No native input/payment/gear-stat collection or source seed algorithm is executed.'],
        limits=['Older source is supplementary evidence, not a current executable certificate. Full Magic Arrow remains unavailable pending bow launch stat collection, input/weapon timing/payment, item callbacks and full owner/client verification.','This bounded component supplies already-collected saved endpoints and conversion. Native gear/material/Enchant/aura/mastery composition and current precise collision/activation/order remain separate work.'])
    target=ROOT/'tools/reference/magic-arrow-engine-evidence.json';rendered=json.dumps(evidence,indent=2)+'\n'
    if check:assert target.read_text()==rendered,'Magic Arrow source evidence changed'
    else:target.write_text(rendered)
    print('Magic Arrow:216 conversion/1536 integrated impact/2 guard source cases pass; full skill remains pending.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
