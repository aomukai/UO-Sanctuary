#!/usr/bin/env python3
"""Compile unchanged physical missile creation and percent initializer statements."""
from pathlib import Path
import hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
hashes={}
def source(path):
 raw=(STAGE/'d2moo-pinned-source'/path).read_bytes()
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
 hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
text=source('source/D2Common/src/Units/Missile.cpp')
begin=text.index('\t\tnMinDamage = nSkillSrcDamage * nBaseMinDamage / 128;')
end=text.index('\n\n',text.index('nMaxDamagePercent = STATLIST_',begin))
raw_block=text[begin:end]
begin=text.index('\tif (nMinDamagePercent <= nMaxDamagePercent)')
end=text.index('\n\n',text.index('pMissileDamageData->nMaxDamage =',begin))
percent_block=text[begin:end]
ama=source('source/D2Game/src/SKILLS/SkillAma.cpp');begin=ama.index('void __fastcall SKILLS_AddDamagePercentBonus(');end=ama.index('\n}\n',begin)+2
initializer=ama[begin:end]
cpp=r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
enum{STAT_DAMAGEPERCENT,STAT_ITEM_MINDAMAGE_PERCENT,STAT_ITEM_MAXDAMAGE_PERCENT};
struct D2UnitStrc{int stats[3]={};};struct Damage{int nMinDamage,nMaxDamage,nDamagePercent;};
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int s,int){return p->stats[s];}
void STATLIST_SetUnitStat(D2UnitStrc* p,int s,int value,int){p->stats[s]=value;}
'''+initializer+r'''
Damage create(int nSkillSrcDamage,int nBaseMinDamage,int nBaseMaxDamage,int nMinPhysDamage,int nMaxPhysDamage,int ownerPercent,int nBonus,int minimumPercent,int maximumPercent,int initial){
 D2UnitStrc owner{{ownerPercent,minimumPercent,maximumPercent}},missile{};auto* pOwner=&owner;auto* pMissile=&missile;
 Damage output{};auto* pMissileDamageData=&output;int nMinDamage=0,nMaxDamage=0,nDamagePercent=0,nMinDamagePercent=0,nMaxDamagePercent=0;
 SKILLS_AddDamagePercentBonus(pMissile,initial);
'''+raw_block+'\n'+percent_block+r'''
 return output;
}
int main(){bool first=true;std::printf("[");
for(int src:{64,96,128})for(int base:{1,3,100})for(int flat:{0,257})for(int owner:{-200,-90,0,37,200})for(int pair=0;pair<3;pair++)for(int initial:{0,120,240}){
 int pairs[3][2]={{0,0},{10,20},{30,0}};int lo=pairs[pair][0],hi=pairs[pair][1];
 auto result=create(src,base*256,(base+2)*256,flat,flat+256,owner,37,lo,hi,initial);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",src,base,flat,owner,lo,hi,initial,result.nMinDamage,result.nMaxDamage,result.nDamagePercent);
}std::printf("]\n");}
'''
path=STAGE/'weapon-launch-physical-probe.cpp';path.write_text(cpp);binary=STAGE/'weapon-launch-physical-probe'
subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==810
for src,base,flat,owner,lo,hi,initial,minimum,maximum,percent in data:
 assert minimum==src*base*256//128+flat and maximum==src*(base+2)*256//128+flat+256
 assert percent==max(-90,max(lo,hi)+initial+owner+37)
fixture=STAGE/'weapon-launch-physical-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',status='Bounded physical creation/initializer source proof only; launch collector and skill integration remain pending.',legacyCommit=tree['sha'],sourceHashes=hashes,
 probe=dict(cases=810,dataPath=str(fixture.relative_to(ROOT)),dataSha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),rawBlock=raw_block,percentBlock=percent_block,initializer=initializer),
 established=['SrcDam scales raw FP256 weapon endpoints before adding skill flat physical endpoints. Percentage damage remains a separate saved field.',
  'Creation adds the larger of min/max damage percentages, existing missile initializer percent, owner percent and the supplied stat/mastery bonus; clamps the saved percentage to-90 before collision. Current victim species ED is added later at impact, so clamping only at impact is not equivalent for a deeply negative launch percentage.',
  'The unchanged Guided Arrow initializer adds its supplied bonus to missile STAT_DAMAGEPERCENT before damage creation. It is a reusable existing source mechanism, not proof that modern Multiple Shot uses this exact callback.'],
 services=['Owner/item/stat/mastery and skill-flat values are explicitly supplied. No weapon collector, source PRNG or full damage allocator is compiled. Inputs are bounded and avoid integer overflow.'],
 limits=['Modern Multiple Shot calc5 placement remains a separately identified inference boundary. Released2.6 identifies it as Damage Synergy but the older do8 body does not read the new field; this probe does not invent a complete modern do8.',
  'Full material/typed poison/Enchant/aura/stat-layer/hand/owner/client collection and item callbacks/drain still need native implementation.'])
(ROOT/'tools/reference/weapon-launch-physical-evidence.json').write_text(json.dumps(e,indent=2)+'\n')
print('Weapon launch:810 unchanged raw physical creation, percentage clamp and initializer cases passed.')
