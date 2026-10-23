#!/usr/bin/env python3
"""Bounded unchanged projectile critical helper and source pierce-loop proof."""
from pathlib import Path
import ast
import hashlib
import json
import re
import subprocess
import urllib.request

ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
hashes={};blocks={}
def source(path):
    raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();blob=next(v['sha'] for v in tree['tree'] if v['path']==path)
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==blob
    hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
def routine(text,name):
    m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
    end=text.index('{',m.start())+1;depth=1
    while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
    blocks[name]=text[m.start():end];return blocks[name]
skill=source('source/D2Common/src/D2Skills.cpp');miss=source('source/D2Common/src/Units/Missile.cpp');creation=source('source/D2Game/src/MISSILES/Missiles.cpp')
harness=ROOT/'tools/probe_throwing_mastery_reference.py';module=ast.parse(harness.read_text())
cpp=next(ast.literal_eval(n.value) for n in module.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='cpp' for t in n.targets))
cpp=cpp.replace('#define __stdcall','#define __fastcall\n#define __stdcall').replace('int types=0;D2StatStrc','int pSeed=0,types=0,stats[8]={};D2StatStrc')
cpp+='constexpr int STAT_PASSIVE_CRITICAL_STRIKE=6,STAT_ITEM_DEADLYSTRIKE=7,STAT_PIERCE_IDX=8;int rolls[4]={},calls=0,output=0;using D2SeedStrc=int;\n'
cpp+='int STATLIST_UnitGetStatValue(D2UnitStrc* p,int s,int){return p->stats[s];}\nint STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* p,int s,int){return p->stats[s];}\nint SEED_RollRandomNumber(int*){return rolls[calls++];}\nint ITEMS_RollRandomNumber(int*){return rolls[calls++];}\nvoid STATLIST_SetUnitStat(D2UnitStrc*,int,int value,int){output=value;}\n'
for text,name in [(skill,'D2Common_11024'),(skill,'SKILLS_GetWeaponMasteryBonus'),(miss,'MISSILE_HasBonusStats')]:cpp+=routine(text,name)+'\n'
start=creation.index('                int32_t nPierceIndex = 0;');end=creation.index('\n',creation.index('STATLIST_SetUnitStat(pMissile, STAT_PIERCE_IDX, nPierceIndex, 0);',start))
loop=creation[start:end];blocks['creationPierceLoop']=loop
cpp+='int pierce(int nPierceValue){if(!nPierceValue){return 0;}D2SeedStrc seed{};auto* pMissile=&item;\n'+loop+'\nreturn output;}\n'
cpp+=r'''int main(){used=throwable=typeMatch=true;row.nRange=2;row.nITypeA[0]=1;skill.pSkillsTxt=&row;item.types=2;bool first=true;
std::printf("{\"criticalCases\":[");
for(int generic:{0,20,100})for(int deadly:{0,35,100})for(int mastery:{0,29,100})for(int pattern=0;pattern<6;pattern++)for(int present:{0,1}){
 int patterns[6][3]={{0,0,0},{99,0,0},{99,99,0},{99,99,99},{20,35,29},{19,34,28}};
 for(int i=0;i<3;i++)rolls[i]=patterns[pattern][i];calls=0;actor.stats[6]=generic;actor.stats[7]=deadly;actor.layers[5][0]={1,mastery};
 int result=MISSILE_HasBonusStats(&actor,present?&item:nullptr);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d]",generic,deadly,mastery,pattern,present,result,calls);
}
std::printf("],\"pierceCases\":[");first=true;
for(int chance:{0,1,46,55,99,100,150})for(int draw:{0,1,45,46,54,55,98,99}){
 for(int& value:rolls)value=draw;calls=output=0;int result=pierce(chance);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d]",chance,draw,result,calls);
}std::printf("]}\n");}
'''
path=STAGE/'throwing-projectile-source-probe.cpp';path.write_text(cpp);binary=STAGE/'throwing-projectile-source-probe'
subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]))
patterns=[[0,0,0],[99,0,0],[99,99,0],[99,99,99],[20,35,29],[19,34,28]]
for generic,deadly,mastery,pattern,present,result,calls in data['criticalCases']:
    count=1;ok=patterns[pattern][0]<generic
    if not ok and deadly:
        ok=patterns[pattern][count]<deadly;count+=1
    if not ok and present and mastery:
        ok=patterns[pattern][count]<mastery;count+=1
    assert (result,calls)==(int(ok),count)
for chance,draw,result,calls in data['pierceCases']:
    expected=4 if draw<chance else 0
    assert (result,calls)==(expected,4 if expected else int(chance!=0))
data_path=STAGE/'throwing-projectile-source-probe.json';data_path.write_text(json.dumps(data,indent=2)+'\n')
for text,name in [(miss,'MISSILE_CalculateDamageData'),(miss,'MISSILE_SetDamageStats')]:routine(text,name)
assert 'MISSILE_HasBonusStats(pOwner, pItem)' in blocks['MISSILE_CalculateDamageData']
assert 'pMissileDamageData->nFlags |= 2;' in blocks['MISSILE_CalculateDamageData']
assert 'STATLIST_SetUnitStat(pMissile, STAT_ITEM_DEADLYSTRIKE, 1, 0);' in blocks['MISSILE_SetDamageStats']
url='https://www.theamazonbasin.com/forums/index.php?/forums/topic/127527-d2r-updated-throw-barb/'
cache=STAGE/'throwing-firsthand-primary.html'
if not cache.exists():cache.write_bytes(urllib.request.urlopen(url,timeout=30).read())
text=re.sub(r'\s+',' ',re.sub('<[^>]+>',' ',cache.read_text(errors='replace')))
assert 'the crit chance is rolled when you throw the weapon' in text and 'only the initial crit does' in text
evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,
    status='Bounded source critical/pierce proof; modern quantity behavior uses released primary and firsthand historical D2R report, with exact pinned executable/RNG acceptance still open.',
    probe=dict(caseCounts={k:len(v) for k,v in data.items()},totalCases=sum(len(v) for v in data.values()),dataPath=str(data_path.relative_to(ROOT)),blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},sharedHarness=dict(path=str(harness.relative_to(ROOT)),sha256=hashlib.sha256(harness.read_bytes()).hexdigest())),
    historicalFirsthand=dict(url=url,author='DarkMaster',date='2023-05-13',cachePath=str(cache.relative_to(ROOT)),sha256=hashlib.sha256(cache.read_bytes()).hexdigest(),scope='Author reports own D2R play: mastery/Deadly Strike/oskill crits conserve and replenish at throw, empty-air throws qualify, pierced victims do not replenish again, ethereal weapons qualify. This is firsthand historical evidence, not a3.3.93847 instrumented capture.'),
    established=['The unchanged projectile helper always rolls generic Critical Strike first, even at zero chance, then nonzero Deadly Strike, then nonzero eligible weapon mastery. First success short-circuits. This differs from the ordinary melee helper order.','Creation calculates the critical flag once on damage data, then copies it as a boolean missile stat; impact reads that saved stat. Formula/collision/impact blocks are traced only, not fully compiled here.','Creation adds item and skill pierce and prerolls at most4 continuations. The unchanged loop is compiled for nonnegative chances; actual seed algorithm and owner-pierce seed remain explicit services. Existing native budget cap4 matches this bounded contract.'],
    limits=['Declared stat-layer, used-skill, item type and RNG services. Generic oskill Critical Strike remains unavailable in native gameplay; source isolation covers its branch without enabling it.','Modern conservation-roll ordering relative to critical/pierce, exact modern RNG algorithm, quantity-zero/ethereal destruction and full projectile snapshot/proc/client behavior remain separately unverified. One quantity recovery is supported by the historical reference convention, not independently instrumented here.','No modern executable is compiled; no claim of complete3.3.93847 equivalence.'])
(ROOT/'tools/reference/throwing-projectile-engine-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n');print('Throwing projectile bounded cases:',evidence['probe']['totalCases'])
