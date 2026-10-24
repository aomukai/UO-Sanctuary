#!/usr/bin/env python3
"""Compile friendly player-count and noRatio spawn table helpers, not a game initializer."""
import argparse, hashlib, json, re, subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]; STAGE=ROOT/'.local/class-skills'; REF=ROOT/'tools/reference/d2r-named-equipment'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 hashes={};blocks={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
  hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  b=text[m.start():i];blocks[name]=hashlib.sha256(b.encode()).hexdigest();return b+'\n'
 monster=source('source/D2Game/src/MONSTER/Monster.cpp');tables=source('source/D2Common/src/DataTbls/MonsterTbls.cpp');header=source('source/D2Common/include/DataTbls/MonsterTbls.h')
 flags=re.search(r'enum D2C_MonStatsTxtFlagIndices\s*\{.*?\};',header,re.S)[0];assert 'MONALIGN_EVIL = 0' in header
 init=routine(monster,'MONSTER_InitializeStatsAndSkills');assert 'nLevel, 7, &monStatsInit' in init and 'STATLIST_SetUnitStat(pUnit, STAT_TOHIT' not in init
 reviewed={'MONSTER_InitializeStatsAndSkills':blocks.pop('MONSTER_InitializeStatsAndSkills')}
 raw=(REF/'json/monstats.json').read_bytes();pin=hashlib.sha256(raw).hexdigest();assert pin==json.loads((REF/'sources.json').read_text())['files']['json/monstats.json']['sha256']
 row=next(x for x in json.loads(raw).values() if x.get('Id')=='bladecreeper');assert row['*hcIdx']==413 and row['noRatio']==1 and row['Align']==1 and row['Velocity']==12
 for suffix in ('','(N)','(H)'):
  assert row[('minHP' if not suffix else 'MinHP')+suffix]==row[('maxHP' if not suffix else 'MaxHP')+suffix]==100 and row['AC'+suffix]==100
  assert not row.get('A1TH'+suffix,0)
 cpp=r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <initializer_list>
#define __fastcall
#define __stdcall
using BOOL=int;constexpr int TRUE=1,FALSE=0,MONALIGN_EVIL=0;
'''+flags+r'''
uint32_t gdwBitMasks[32];
struct D2MonStatsTxt{uint32_t dwMonStatsFlags=0;int nAlign=1;int nMinHP[3]={100,100,100},nMaxHP[3]={100,100,100},nAC[3]={100,100,100},nExp[3]={},nA1TH[3]={},nA1MinD[3]={},nA1MaxD[3]={},nA2TH[3]={},nA2MinD[3]={},nA2MaxD[3]={},nS1TH[3]={},nS1MinD[3]={},nS1MaxD[3]={},nElMinD[3][3]={},nElMaxD[3][3]={},nElDur[3][3]={};}mon;
struct D2MonLvlTxt{int dwHP[6]={},dwAC[6]={},dwXP[6]={},dwTH[6]={},dwDM[6]={};};
struct Tables{int nMonLvlTxtRecordCount=111;D2MonLvlTxt pMonLvlTxt[111];}tables;auto* sgptDataTables=&tables;
struct D2MonStatsInitStrc{int nMinHP=0,nMaxHP=0,nAC=0,nExp=0,nTH=0,nA1MinD=0,nA1MaxD=0,nA2MinD=0,nA2MaxD=0,nS1MinD=0,nS1MaxD=0,nElMinD=0,nElMaxD=0,nElDur=0;};
struct D2DifficultyLevelsTxt{int dwMonsterSkillBonus=0;}difficulties[3]={{0},{3},{7}};
struct D2MonsterDataStrc{D2MonStatsTxt* pMonstatsTxt=&mon;}data;
struct D2UnitStrc{D2MonsterDataStrc* pMonsterData=&data;}unit;
struct D2GameStrc{uint8_t nDifficulty=0;}game;
struct D2ActiveRoomStrc{}room;
struct D2PlayerCountBonusStrc{int nPlayerCount=0,nHp=0,nExperience=0,nMonsterSkillBonus=0,nDifficulty=0;};
int players=1,playerQueries=0,hasData=1,hasRecord=1;
D2MonsterDataStrc* MONSTERUNIQUE_GetMonsterData(D2UnitStrc*){return hasData?&data:nullptr;}
int PLAYER_GetPlayerCount(D2GameStrc*){playerQueries++;return players;}
D2DifficultyLevelsTxt* DATATBLS_GetDifficultyLevelsTxtRecord(int d){assert(d>=0&&d<3);return &difficulties[d];}
D2MonStatsTxt* DATATBLS_GetMonStatsTxtRecord(int id){assert(id==413);return hasRecord?&mon:nullptr;}
int DATATBLS_ApplyRatio(int,int,int){assert(false);return 0;}
'''
 for text,name in [(monster,'MONSTER_GetHpBonus'),(monster,'MONSTER_GetExperienceBonus'),(monster,'MONSTER_GetPlayerCountBonus'),(tables,'DATATBLS_CalculateMonsterStatsByLevel')]:cpp+=routine(text,name)
 cpp+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=uint32_t(1)<<i;mon.dwMonStatsFlags=gdwBitMasks[MONSTATSFLAGINDEX_NORATIO];
printf("{\"playerCounts\":[");bool first=true;
for(int a:{0,1,2})for(int p:{0,1,2,8,9,64})for(int d:{0,1,2})for(int valid:{0,1}){
 mon.nAlign=a;players=p;game.nDifficulty=d;hasData=valid;playerQueries=0;D2PlayerCountBonusStrc b;b.nHp=99;
 MONSTER_GetPlayerCountBonus(&game,&b,&room,&unit);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",a,p,d,valid,playerQueries,b.nPlayerCount,b.nHp,b.nExperience,b.nMonsterSkillBonus,b.nDifficulty);first=false;
}
printf("],\"spawnStats\":[");first=true;
for(int d:{0,1,2})for(int expansion:{0,1})for(int l:{-1,0,1,77,110,999})for(int valid:{0,1}){
 hasRecord=valid;D2MonStatsInitStrc s;int ok=DATATBLS_CalculateMonsterStatsByLevel(413,expansion,d,l,7,&s);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",d,expansion,l,valid,ok,s.nMinHP,s.nMaxHP,s.nAC,s.nExp,s.nTH);first=false;
}printf("]}\n");}
'''
 p=STAGE/'blade-spawn-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('')
 subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
 result=subprocess.run([str(binary)],capture_output=True);(STAGE/'blade-spawn-sanitizer.log').write_bytes(result.stderr);assert result.returncode==0 and not result.stderr,result.stderr.decode()
 output=json.loads(result.stdout);assert len(output['playerCounts'])==108 and len(output['spawnStats'])==72
 for align,p,d,valid,queries,count,hp,xp,skill,diff in output['playerCounts']:
  n=max(p,1);evil=valid and align==0
  bonus=0 if not evil else (50*(n-1) if n<9 else 50*n-100)
  experience=0 if not evil else (50*(n-1) if n<9 else 10*n+260)
  assert [queries,count,hp,xp,skill,diff]==[int(evil),n if evil else int(valid),bonus,experience,[0,3,7][d] if valid else 0,d if valid else 0]
 for d,expansion,level,valid,ok,minimum,maximum,ac,xp,attack in output['spawnStats']:
  success=int(valid and level>=0);assert [ok,minimum,maximum,ac,xp,attack]==[success,100*success,100*success,100*success,0,0]
 fixture=STAGE/'blade-spawn-source-probe.json';fixture.write_text(json.dumps(output,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],talentId='assassin.bladesentinel',sourceHashes=hashes,compiledUnchangedRoutines=blocks,reviewedOnlyRoutines=reviewed,pinnedMonstatsSha256=pin,fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),playerCountCases=108,noRatioSpawnCases=72),sanitizers='ASan/UBSan, zero runtime stderr',established=['Complete player-count helper and both bonus getters execute with evil/non-evil alignments, six player counts, all difficulties and missing-data controls. Non-evil actors receive playerCount1 with zero HP/XP bonuses and do not query connected-player count.','Complete spawn table helper executes flags7 and the current noRatio blade row for both game-table choices, all three difficulties, valid/negative/over-limit levels and missing-record controls. HP100/100 andAC100 are literal; no ratio service is called; weapon attack output is untouched0 because flags7 exclude that lane.','Current row413 has Align1/noRatio1/base velocity12 and equalHP100 bounds in all difficulties. The reviewed full initializer uses flags7, rolls within those equal bounds, applies the zero friendly HP bonus and does not explicitly write TOHIT. Shared summon base/passive and do44 later level overwrite have separate existing compiled evidence.'],services=['Player-count and difficulty-table lookup are explicit services; skill bonuses0/3/7 are synthetic controls for field transfer, not asserted current difficulty data.','NoRatio flags7 only: unrelated ratio/weapon/elemental paths are present unchanged but not executed. Record/layout shims are declared, not a full engine ABI.','Full monster initialization, spawn flags/components, seed allocation/HP-roll draw order, world placement and equipment generation are reviewed/separate, not compiled here. Actual native seed allocation remains a declared supplier; no exact current executable certificate.'],reuseEvidence=['tools/reference/valkyrie-creation-engine-evidence.json','tools/reference/assassin-trap-summon-engine-evidence.json','tools/reference/actor-motion-engine-evidence.json'])
 target=ROOT/'tools/reference/blade-spawn-engine-evidence.json';rendered=json.dumps(evidence,indent=2)+'\n'
 if check:assert target.read_text()==rendered
 else:target.write_text(rendered)
 print('Blade spawn:108 complete player-count and72 noRatio flags7 cases pass ASan/UBSan; full initializer remains separately reviewed.')
if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
