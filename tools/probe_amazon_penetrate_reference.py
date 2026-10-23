#!/usr/bin/env python3
"""Compile bounded unchanged player attack-rating statements for Penetrate reuse."""
from pathlib import Path
import hashlib
import json
import subprocess

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'
source='source/D2Game/src/UNIT/SUnitDmg.cpp'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text())
assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
raw=(STAGE/'d2moo-pinned-source'/source).read_bytes()
blob=next(x['sha'] for x in tree['tree'] if x['path']==source)
assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==blob
text=raw.decode();start=text.index('int32_t __fastcall SUNITDMG_IsHitSuccessful(')
lines=[]
for token in ['nToHitPercent += nStatValue + STATLIST_UnitGetItemStatOrSkillStatValue(pAttacker, STAT_ITEM_TOHIT_PERCENT, 0);',
              'int32_t nToHit = nAttackRate + MONSTERUNIQUE_CalculatePercentage(nAttackRate, nToHitPercent, 100);']:
    position=text.index(token,start);lines.append(text[position:text.index('\n',position)])
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
constexpr int STAT_ITEM_TOHIT_PERCENT=0;
struct Unit{int percent;};
int STATLIST_UnitGetItemStatOrSkillStatValue(Unit* p,int,int){return p->percent;}
int MONSTERUNIQUE_CalculatePercentage(int v,int n,int d){return int(int64_t(v)*n/d);}
int rating(int nAttackRate,int passive,int nStatValue){Unit owner{passive};auto* pAttacker=&owner;int nToHitPercent=0;
'''+ '\n'.join(lines)+r'''
return nToHit;}
int main(){bool first=true;std::printf("[");
for(int basis:{1,217,1234,50000})for(int passive:{0,35,225,235,245})for(int other:{0,20,80,101,260,381}){
 if(!first)std::printf(",");first=false;
 std::printf("[%d,%d,%d,%d]",basis,passive,other,rating(basis,passive,other));
}std::printf("]\n");}
'''
path=STAGE/'amazon-penetrate-source-probe.cpp';path.write_text(cpp)
binary=STAGE/'amazon-penetrate-source-probe'
subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]))
assert len(data)==120
for basis,passive,other,value in data:
    assert value==basis+basis*(passive+other)//100
fixture=STAGE/'amazon-penetrate-source-probe.json'
fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
preflight=json.loads((ROOT/'tools/reference/amazon-penetrate-preflight.json').read_text())
evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],source=dict(path=source,sha256=hashlib.sha256(raw).hexdigest()),
    probe=dict(cases=len(data),dataPath=str(fixture.relative_to(ROOT)),dataSha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),unchangedStatements=lines,statementSha256=hashlib.sha256('\n'.join(lines).encode()).hexdigest()),
    pinnedSkill=preflight['currentSkill'],officialGuide=preflight['officialGuide'],modernFix=preflight['modernFix'],
    established=['Current passive grants unrestricted item_tohit_percent=ln12 with35 initial/10 each subsequent effective rank. No mana or active binding.',
        'Compiled ordinary player AR statements add skill/stat percentages and one percentage of base AR. Modern2.6 explicitly preserves Penetrate alongside Blessed Aim; the older stat-list overwrite bug must not be reproduced.'],
    services=['Item-or-skill stat lookup returns the supplied passive value; remaining percentages are an explicit summed input. This probe does not implement source stat-list aggregation or the modern aura fix.',
        'Percentage helper uses64-bit intermediate and integer division. Inputs are nonnegative, bounded and nonoverflowing; negative-percent rounding and large native integer overflow are outside this proof.'],
    limits=['No modern executable certificate. Exact source stat layers, seed, current hit-chance quantization, target defense/type/owner/client cases remain separate. Existing native floating hit chance is unchanged; this probe certifies bounded attack rating only.',
        'Weapon missile collision already reads live owner AR; the full Amazon launch collector still needs integration.'])
(ROOT/'tools/reference/amazon-penetrate-engine-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print('Penetrate:120 unchanged source attack-rating cases compiled and checked; modern Blessed Aim fix recorded separately.')
