#!/usr/bin/env python3
"""Bound ordinary bow timing against immutable engine and original IAS sources."""
from pathlib import Path
import argparse,hashlib,json,re,struct,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
check=argparse.ArgumentParser();check.add_argument('--check',action='store_true');check=check.parse_args().check
hashes={};blocks={}
def digest(raw):return hashlib.sha256(raw).hexdigest()
def verified(folder,tree,path):
 raw=(folder/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path),path
 hashes[path]=digest(raw);return raw.decode()
def routine(text,name):
 m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
 end=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 blocks[name]=text[m.start():end];return blocks[name]+'\n'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
folder=STAGE/'d2moo-pinned-source'
common=verified(folder,tree,'source/D2Common/src/Units/Units.cpp');unit=verified(folder,tree,'source/D2Game/src/UNIT/SUnit.cpp');skills=verified(folder,tree,'source/D2Game/src/SKILLS/Skills.cpp')
bonus=routine(common,'UNITS_GetFrameBonus');scheduler=routine(unit,'sub_6FCBCE70')
reviewed={name:digest(routine(skills,name).strip().encode()) for name in ['sub_6FD12950','D2GAME_SKILLS_Handler_6FD12BA0','sub_6FD11420']}
raw=(ROOT/'.local/research/AnimData.d2').read_bytes();animhash=digest(raw);assert animhash=='e3a54b9ab71df7ccdf3655bbba5868c9d53cd8716b0aad25e8c3d5621579cecb';pos=0;animations=[]
for bucket in range(256):
 count=struct.unpack_from('<i',raw,pos)[0];pos+=4
 for _ in range(count):
  row=raw[pos:pos+160];pos+=160;code=row[:8].rstrip(b'\0').decode()
  if code in ['AMA1BOW','AMA1XBW']:
   frames,speed=struct.unpack_from('<ii',row,8);events=[(i,v) for i,v in enumerate(row[16:]) if v];assert speed==256 and len(events)==1 and events[0][1]==2
   animations.append(dict(code=code,frames=frames,event=events[0][0],kind=2))
assert pos==len(raw) and len(animations)==2
cpp=r'''#include <algorithm>
#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#include <initializer_list>
#define __fastcall
#define __stdcall
#define D2_ASSERT(x) assert(x)
constexpr int UNIT_PLAYER=0,UNIT_TYPES_COUNT=6,NUMBER_OF_PLAYERCLASSES=7,NUM_WEAPON_CLASSES=14,WEAPONCLASS_HTH=0,PLRMODE_ATTACK1=4,PLRMODE_ATTACK2=5,PLRMODE_SPECIAL3=15,PLRMODE_SPECIAL4=16,EVENTTYPE_MODECHANGE=8,EVENTTYPE_ENDANIM=9;
struct D2AnimSeqTxt{};
struct Anim{int pFrameFlags[144]={};} anim;
struct D2UnitStrc{int dwUnitType=0,dwClassId=0,dwAnimMode=4,dwSeqSpeed=256,dwSeqFrameCount=0,wAnimSpeed=256,dwFrameCountPrecise=0,nSeqCurrentFramePrecise=0;void* pAnimSeq=nullptr;Anim* pAnimData=&anim;} owner;
struct D2GameStrc{int dwGameFrame=100;} game;
int weaponClass=1;
std::vector<std::array<int,4>> events;
void D2COMMON_11013_ConvertMode(D2UnitStrc*,int*,int*,int*,const char*,int){}
D2UnitStrc* D2Common_10434(D2UnitStrc*,int){return &owner;}
int ITEMS_GetWeaponClassId(D2UnitStrc*){return weaponClass;}
bool UNITS_CanDualWield(D2UnitStrc*){return false;}
void DATATBLS_GetSequenceEvent(D2AnimSeqTxt*,int,int*){assert(false);}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int kind,int ordinal){events.push_back({type,frame,kind,ordinal});}
'''+bonus+scheduler
cpp+='int main(){bool first=true;std::printf("[");\n'
for row in animations:
 family=1 if row['code'].endswith('BOW') else 2
 cpp+=f'''for(int rate:{{38,128,180,256,320,448}}){{weaponClass={1 if family==1 else 7};owner.wAnimSpeed=rate;owner.dwFrameCountPrecise={row['frames']}*256;anim={{}};anim.pFrameFlags[{row['event']}]=2;events.clear();sub_6FCBCE70(&game,&owner);int action=-1,end=-1;for(auto e:events){{if(e[0]==8){{assert(e[2]==2);action=e[1]-100;}}if(e[0]==9)end=e[1]-100;}}if(!first)std::printf(",");first=false;std::printf("[{family},%d,%d,%d,%d]",rate,UNITS_GetFrameBonus(&owner),action,end);}}\n'''
cpp+='std::printf("]\\n");}\n'
p=STAGE/'magic-arrow-action-source.cpp';p.write_text(cpp);binary=STAGE/'magic-arrow-action-source';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);legacy=json.loads(subprocess.check_output([str(binary)]))
modern=ROOT/'.local/research/ias-calculator-b7d7910';commit=json.loads((modern/'commit.json').read_text());ctree=json.loads((modern/'tree.json').read_text());assert commit['sha']=='b7d791031b73b0c6e56a3659e9198578aed21261' and ctree['sha']==commit['commit']['tree']['sha']
for name in ['README.md','calculator.js','constants.js']:verified(modern,ctree,name)
calc=(modern/'calculator.js').read_text();constants=(modern/'constants.js').read_text()
m=re.search(r'function getStartingFrame\(weaponType\)\s*\{',calc);end=calc.index('{',m.start())+1;depth=1
while depth:depth+=(calc[end]=='{')-(calc[end]=='}');end+=1
starting=calc[m.start():end]
offset=re.search(r'let offset = skill\.isSequence[^;]+;',calc)[0]
at=calc.index('let firstHitLength = 256 *');end=calc.index('else firstHitLength = Math.ceil(firstHitLength) - offset;',at)+len('else firstHitLength = Math.ceil(firstHitLength) - offset;');cycle=calc[at:end]
js='''const skills=Object.fromEntries(['STANDARD','FEND','ZEAL','WHIRLWIND'].map(x=>[x,{}]));
const skill=skills.STANDARD,char={AMAZON:0,SORCERESS:1},character=0;
const wt=Object.fromEntries(['BOW','CROSSBOW','UNARMED','ONE_HANDED_SWINGING','TWO_HANDED_SWORD','ONE_HANDED_THRUSTING','TWO_HANDED_THRUSTING','TWO_HANDED'].map(x=>[x,{}]));
const trun=Math.trunc;
'''+starting+'\nlet result=[];\n'
for family,name in [(1,'BOW'),(2,'CROSSBOW')]:
 match=re.search(r'\b'+name+r': new WeaponType\([^\n]+\[\s*\[char.AMAZON, \[(\d+), (\d+)\]\]',constants);assert match,name
 frames,event=map(int,match.groups());shape=next(x for x in animations if x['code']==('AMA1BOW' if family==1 else 'AMA1XBW'));assert (frames,event)==(shape['frames'],shape['event'])
 js+=f'for(let speedIncrease of [38,128,180,256,320,448]){{const weaponType=wt.{name},framesPerDirection1={frames},startingFrame=getStartingFrame(weaponType);\n'+offset+'\n'+cycle+f'\nresult.push([{family},speedIncrease,startingFrame,firstHitLength]);}}\n'
js+='process.stdout.write(JSON.stringify(result)+"\\n");\n';p=STAGE/'magic-arrow-action-modern.js';p.write_text(js);current=json.loads(subprocess.check_output(['node',str(p)]))
assert len(legacy)==len(current)==12
rows=[]
for old,now in zip(legacy,current):
 family,rate,bonus,event,end=old;assert now[:3]==[family,rate,bonus] and bonus==0
 assert end==now[3]+1 and event>0 and event<=now[3]
 rows.append(old+[now[3]])
fixture=STAGE/'magic-arrow-action-source-probe.json';fixture.write_text(json.dumps(dict(rows=rows),separators=(',',':'))+'\n')
evidence=dict(referenceVersion='3.3.93847',talentId='amazon.magicarrow',legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=['UNITS_GetFrameBonus','sub_6FCBCE70'],blockHashes={k:digest(blocks[k].encode()) for k in ['UNITS_GetFrameBonus','sub_6FCBCE70']},reviewedWrapperHashes=reviewed,legacyAnimDataHash=animhash,animations=animations,originalModernCalculator=dict(repository='https://github.com/Warren1001/IAS_Calculator',commit=commit['sha'],functionHash=digest(starting.encode()),offsetHash=digest(offset.encode()),cycleBlockHash=digest(cycle.encode()),rows=12),fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=digest(fixture.read_bytes()),rows=12),established=['Ordinary human Amazon bow and crossbow A1 have zero starting-frame bonus. Legacy missile events are kind2, metadata14/event6 and20/event9. The unchanged scheduler yields twelve event/end rows at declared animation advances.','Original modern calculator STANDARD human bow metadata agrees with the cached legacy rows. Its unchanged ordinary cycle expression uses ceil(frameCount*256/advance)-1. The native repeat cycle uses that modern expression while its shot event uses the separately compiled source event clock. At advance256 cycles13/19 emit at6/9.','Reviewed no-start-function wrapper returns without start mana payment. Generic do wrapper emits its declared srvmissile and subsequently consumes mana once; pinned Magic Arrow noammo1 has no decquant. Source generic player missile ToHit0 versus the pinned display remains independently uncertain.'],limits=['Supplementary old engine and original calculator, not a3.3.93847 executable/client capture. Cached AnimData acquisition provenance remains incomplete; transformed/foreign-class/alternate/sequence modes are excluded.','Declared animation rate, metadata, ordinary weapon selection and empty event-list adapters isolate clocks. IAS/WSM/Fanaticism/chill, native input/cancellation/held pacing and resource/flight/item consumers require independent native checks.','Wrapper bodies are reviewed and hashed, not compiled by this probe. Exact current damage/mastery/AR/seed/stat inheritance, target-loss fallback, all proc/owner/client branches and full class acceptance remain open.'])
out=ROOT/'tools/reference/magic-arrow-action-engine-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
if check:assert out.read_text()==encoded,'Magic Arrow action evidence stale'
else:out.write_text(encoded)
print('Magic Arrow:12 compiled ordinary bow shot/end clocks and12 original modern repeat-cycle rows agree on their explicit one-frame boundary.')
