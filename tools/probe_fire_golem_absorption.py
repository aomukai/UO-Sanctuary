#!/usr/bin/env python3
"""Execute complete resistance/absorb and percentage bodies for Fire Golem stat pools."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'fire-golem-absorption-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());inputs={}
    def source(rel):
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
    damage=source('source/D2Game/src/UNIT/SUnitDmg.cpp');unique=source('source/D2Game/src/MONSTER/MonsterUnique.cpp')
    bodies={'MONSTERUNIQUE_CalculatePercentage':function(unique,'MONSTERUNIQUE_CalculatePercentage'),'SUNITDMG_ApplyResistancesAndAbsorb':function(damage,'SUNITDMG_ApplyResistancesAndAbsorb')}
    cpp=r'''#include <algorithm>
#include <cassert>
#include <cstddef>
#include <cstdint>
#include <cstdio>
#define __fastcall
constexpr int STAT_DAMAGERESIST=1,STAT_MAGICRESIST=2,STAT_FIRE=3,STAT_MAXFIRE=4,STAT_ABSPCT=5,STAT_ABSFLAT=6,STAT_PIERCE=7;
constexpr int STATE_SANCTUARY=1,DIFFMODE_NIGHTMARE=1,DIFFMODE_HELL=2;
struct D2UnitStrc{int stats[9]={};};struct D2DamageStrc{int value=0,dwAbsLife=0;};
struct D2GameStrc{int bExpansion=1,nDifficulty=0;};struct Difficulty{int dwResistPenalty=0;};
struct D2DamageInfoStrc{D2DamageStrc*pDamage;D2UnitStrc*pAttacker,*pDefender;D2GameStrc*pGame;Difficulty*pDifficultyLevelsTxt;int bDefenderIsMonster=1,nDamageReduction[1]={};};
struct D2DamageStatTableStrc{int nOffsetInDamageStrc=offsetof(D2DamageStrc,value),nResStatId=STAT_FIRE,nPierceStatId=STAT_PIERCE,nMaxResStatId=STAT_MAXFIRE,nDamageReductionType=0,nAbsorbPctStatId=STAT_ABSPCT,nAbsorbStatId=STAT_ABSFLAT;};
int STATLIST_UnitGetStatValue(D2UnitStrc*p,int stat,int){return p->stats[stat];}bool STATES_CheckState(D2UnitStrc*,int){return false;}bool MONSTERS_IsUndead(D2UnitStrc*){return false;}
int D2Clamp(int v,int low,int high){return std::clamp(v,low,high);}
'''+bodies['MONSTERUNIQUE_CalculatePercentage']+'\n'+bodies['SUNITDMG_ApplyResistancesAndAbsorb']+r'''
int main(){bool first=true;printf("[");
for(int innate:{0,36,40,58,88,100})for(int item:{0,15,25})for(int flat:{0,1,15})for(int value:{0,1,257,25600,1048577})for(int resist:{-100,0,50,83,100,142})for(int mdr:{0,256}){
if(value>0&&mdr>value)continue;D2UnitStrc a,d;D2GameStrc game;Difficulty difficulty;D2DamageStrc packet;D2DamageStatTableStrc table;
d.stats[STAT_FIRE]=resist;d.stats[STAT_ABSPCT]=innate+item;d.stats[STAT_ABSFLAT]=flat;packet.value=value;D2DamageInfoStrc info{&packet,&a,&d,&game,&difficulty,1,{mdr}};
SUNITDMG_ApplyResistancesAndAbsorb(&info,&table,0);printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",value,resist,mdr,innate,item,flat,packet.value,packet.dwAbsLife);first=false;
}printf("]\n");}
'''
    STAGE.mkdir(parents=True,exist_ok=True);src=STAGE/'probe.cpp';src.write_text(cpp);run=subprocess.run(['g++','-std=c++20','-O1','-g','-fwrapv','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(src),'-o',str(STAGE/'probe')],capture_output=True);(STAGE/'compiler.log').write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode();rows=json.loads(run.stdout);assert len(rows)==2916
    (STAGE/'cases.json').write_text(json.dumps(rows,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(rows),functions={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},inputs={**inputs,str(Path(__file__).relative_to(ROOT)):sha(Path(__file__))},artifacts={str(p.relative_to(ROOT)):sha(p) for p in [src,STAGE/'cases.json',STAGE/'compiler.log',STAGE/'sanitizer.log']},
        established=['Complete unchanged reducer/percentage bodies execute2916declared monster contexts. Innate and own-item percentage values share one stat pool,capped40before flat absorption. Raw fire immunity prevents both damage and absorption;fractional and large-value division are retained.',
            'Value/MDR controls exclude negative residuals,whose existing Sanctuary zero clamp is a separate existing adapter. World/stat/pierce/Sanctuary services remain declared;full current executable/event/initializer equivalence is not asserted.'],remaining=['Native Fire Golem resolver/AOS/prepared/health/reload integration remains to be verified. Historical adapter migration and full outgoing elemental/aura/death behavior remain separate.'])
    path=ROOT/'tools/reference/fire-golem-absorption-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Fire absorption:2916complete source reducer contexts;shared40percent cap before flat;native acceptance separate.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
