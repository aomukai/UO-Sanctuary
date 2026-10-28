#!/usr/bin/env python3
"""Pin Purge and shared buff source; classify current PE leads as unverified."""
import argparse,hashlib,json,struct,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'hex-purge'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def inspect_current(inputs):
 ref=ROOT/'tools/reference/d2r-local-installation-evidence.json';j=json.loads(ref.read_text());inputs[str(ref.relative_to(ROOT))]=sha(ref)
 p=Path(j['installationPath'])/j['executable']['filename'];b=p.read_bytes();assert hashlib.sha256(b).hexdigest()==j['executable']['sha256']
 pe=struct.unpack_from('<I',b,0x3c)[0];assert b[pe:pe+4]==b'PE\0\0' and struct.unpack_from('<H',b,pe+4)[0]==0x8664
 opt=pe+24;assert struct.unpack_from('<H',b,opt)[0]==0x20b;base=struct.unpack_from('<Q',b,opt+24)[0];sections=[]
 for i in range(struct.unpack_from('<H',b,pe+6)[0]):
  off=opt+struct.unpack_from('<H',b,pe+20)[0]+i*40;size,rva,rawsize,raw=struct.unpack_from('<IIII',b,off+8);sections.append(dict(name=b[off:off+8].rstrip(b'\0').decode(),rva=rva,size=size,raw=raw,rawsize=rawsize))
 def offset(rva):
  s=next(s for s in sections if s['rva']<=rva<s['rva']+s['rawsize']);return s['raw']+rva-s['rva']
 # A hypothesis selected by a pointer-run/proximity probe,not a symbol or
 # executed dispatcher. Do not label any slot as a verified callback identity.
 table=0x163a750;raw=offset(table);words=struct.unpack_from('<41Q',b,raw);assert words[0]==0 and all(base+0x1000<=v<base+0x15ccdc0 for v in words[1:])
 exrva,exsize=struct.unpack_from('<II',b,opt+112+3*8);ex=offset(exrva);ranges=[struct.unpack_from('<III',b,q) for q in range(ex,ex+exsize,12)]
 outdir=STAGE/'current-raw';outdir.mkdir(exist_ok=True);artifacts=[];slots=[]
 for i in [17,28,36,37,39,40]:
  va=words[i];rva=va-base;start,end,unwind=next(x for x in ranges if x[0]==rva);code=b[offset(start):offset(start)+end-start]
  out=subprocess.check_output(['objdump','-d','-M','intel','--start-address='+hex(va),'--stop-address='+hex(base+end),str(p)],text=True)
  target=outdir/f'candidate-slot-{i}.asm';target.write_text(out);artifacts.append(target);assert '(bad)' in out
  slots.append(dict(slot=i,address=hex(va),rva=hex(rva),extent=end-start,bodySha256=hashlib.sha256(code).hexdigest(),badInstructionMarkers=out.count('(bad)'),meaningVerified=False))
 return dict(executableSha256=j['executable']['sha256'],readOnlyFileInspection=True,processMemoryRead=False,processInput=False,
  candidatePointerRunRva=hex(table),candidateSlots=slots,callbackIdentitiesVerified=False,modernBodiesExecuted=False,
  conclusion='The pinned file contains a plausible pointer run,but inspected on-disk extents decode with invalid instructions and no dispatcher link is established. Slot identities and callable semantics remain unverified. This does not prove event36or39and cannot certify their behavior.'),artifacts

def build(check=False):
 STAGE.mkdir(exist_ok=True);inputs={};routines={};ref=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((ref/'sources.json').read_text());tables={}
 for n in ['skills','states','itemstatcost']:
  p=ref/f'json/{n}.json';assert sha(p)==manifest['files'][f'json/{n}.json']['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p);tables[n]=json.loads(p.read_text())
 s=tables['skills']['389'];assert s['srvdofunc']==23 and s['Param1']==39 and s['auraevent1']=='domeleedamage' and all(s['auraeventfunc'+str(i)]==36 for i in range(1,4))
 assert s['passivestat1']=='attackrate' and s['passivestat2']=='item_tohit_percent' and s['aurarangecalc']=='par7/100' and s['ResultFlags']==32
 assert [(n,tables['itemstatcost'][n]['*ID']) for n in ['attackrate','other_animrate','item_tohit_percent']]==[('attackrate',68),('other_animrate',69),('item_tohit_percent',119)]
 assert tables['states']['hexpurge']['group']==6 and tables['states']['hexpurgedebuff']['group']==7 and not tables['states']['hexpurgedebuff'].get('curse')
 tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 def body(rel,name):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
  inputs[str(p.relative_to(ROOT))]=sha(p);b=function(raw.decode(),name);routines[name]=hashlib.sha256(b.encode()).hexdigest();return b
 old=ROOT/'tools/reference/hex-bane-source-evidence.json';proof=json.loads(old.read_text());inputs[str(old.relative_to(ROOT))]=sha(old)
 p=BASE/'hex-bane/bane-buff.cpp';assert sha(p)==proof['artifacts'][str(p.relative_to(ROOT))];inputs[str(p.relative_to(ROOT))]=sha(p);cpp=p.read_text().split('int main(){printf(',1)[0]
 for a,b in [('nAuraState=198','nAuraState=202'),('nPassiveStat[5]={52,53,119,-1,-1}','nPassiveStat[5]={68,119,-1,-1,-1}'),('dwPassiveCalc[5]={1,2,3,0,0}','dwPassiveCalc[5]={1,2,0,0,0}')]:assert a in cpp;cpp=cpp.replace(a,b)
 for rel,names in [('source/D2Game/src/SKILLS/Skills.cpp',['sub_6FD10EC0']),('source/D2Game/src/SKILLS/SkillDruid.cpp',['sub_6FCFE200']),('source/D2Game/src/SKILLS/SkillSor.cpp',['SKILLS_SrvDo023_Blaze_EnergyShield_SpiderLay'])]:
  for n in names:assert body(rel,n) in cpp
 cpp+=r'''int main(){printf("[");bool first=true;
for(int oldRank:{0,1,20,21})for(int rank:{1,20,21})for(int type:{0,1})for(int mode=0;mode<8;mode++){
 D2StatListStrc old;old.state=202;old.skill=389;old.rank=oldRank;old.stats[68]=77;old.stats[69]=88;old.stats[119]=99;
 D2UnitStrc target;target.dwUnitType=type;target.list=oldRank?&old:nullptr;
 guard=mode;mon.dwMonStatsFlags=mode==3?1:0;mon2.dwFlags=mode==5?0:1;skill={};skill.nAuraState=mode==7?256:202;
 values[0]=3600+300*(rank-1);values[1]=std::min(30,10+rank-1);values[2]=20+9*(rank-1);freed=events=unregs=0;registrations.clear();
 int ok=SKILLS_SrvDo023_Blaze_EnergyShield_SpiderLay(&game,&target,389,rank);auto* active=target.list;
 bool descriptors=registrations.size()==3&&registrations[0]==std::array<int,4>{1,36,rank,202}&&registrations[1]==std::array<int,4>{2,36,rank,202}&&registrations[2]==std::array<int,4>{3,36,rank,202};
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",oldRank,rank,type,mode,ok,active?active->rank:0,active?active->expire:0,active?active->stats[68]:0,active?active->stats[69]:0,active?active->stats[119]:0,freed,events,unregs,int(descriptors));first=false;
}printf("]\n");}'''
 p=STAGE/'buff.cpp';p.write_text(cpp);compiler=p.with_suffix('.compiler.log');sanitizer=p.with_suffix('.sanitizer.log')
 with compiler.open('w') as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(p.with_suffix(''))],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(p.with_suffix(''))],capture_output=True);sanitizer.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:];cases=json.loads(run.stdout);assert len(cases)==192
 for old,rank,type,mode,ok,current,expiry,attack,other,rating,freed,events,unregs,descriptors in cases:
  invalid=mode in [6,7] or mode==1 or type==1 and mode in [2,3,4,5];success=not invalid and not old>rank
  assert (ok,current,expiry,attack,other,rating,freed,events,unregs,descriptors)==((1,rank,400+3600+300*(rank-1),min(30,10+rank-1),min(30,10+rank-1),20+9*(rank-1),int(old>0 and old!=rank),1,1,1) if success else (0,old,500 if old else 0,77 if old else 0,88 if old else 0,99 if old else 0,0,0,0,0))
 out=BASE/'hex-purge-source-probe.json';out.write_bytes(run.stdout);artifacts=[p,compiler,sanitizer,out,Path(__file__).resolve()]
 inspection,raw=inspect_current(inputs);artifacts+=raw
 research=ROOT/'.local/research/warlock-community/warlock-skills-notes.txt';assert 'Attack Speed given is EIAS.' in research.read_text();inputs[str(research.relative_to(ROOT))]=sha(research)
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,inputs=inputs,routines=routines,skill=s,buffCases=192,currentFileInspection=inspection,artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},
  established=['Current389uses do23,passive attackrate68andAR119. Effective-rank speed min(30,10+rank-1),AR20+9per extra rank,buff3600+300per extra rank,mana25+rank-1. Full shared do23/factory/passive writer executes192contexts;attackrate also mirrors to other_animrate69in this older source. Formula/stat/registration services are declared and new callback36is not compiled.',
   'Current melee descriptor is domeleedamage,where Bane/Siphon use domeleeattack. Target callback39is a parameter for the new consumer. Debuff150+5per hardEldritchBlast;raw chance floor((6900+50*(rank-1)+100*hardSigilDeath)/100);initial charges min(1+floor(rank/10),3);raw radius4with no Param8growth. Magic endpoints use10percent per hardBane+hardEldritchBlast. No radius unit,roll clamping,capture timing or charge-consumption sequence is selected.',
   'Primary supplied research reports EIAS,successful-hit application and possible same-hit apply/consume. Its exact executable build is unstated. The native buff uses the existing skill-rate layer separately from itemIAS/FCR;actual modern callback acceptance remains separate.'],
  remaining=['Modern callbacks36/39,automatic application,explosion target/geometry/roll/damage/charges/reapplication,cross-hex groups and magic mastery timing remain unverified. Explicit target plans are preparation,not automatic detonations.',
   'Pinned executable file hash matches. Static pointer-run/PData inspection yields only unverified candidate slots;on-disk function extents decode with invalid instructions and no dispatcher link. No process memory or input is accessed. Do not use these candidates as modern callback proof.',
   'Warlock base native animation profiles remain provisional. Modifier placement checks do not certify exact current frames,mode/input/client or full owner/PvP/lifecycle behavior. User has no Hex trained;no points/respec requested.'])
 target=ROOT/'tools/reference/hex-purge-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==encoded
 else:target.write_text(encoded)
 print('Hex Purge:192complete shared buff contexts;current PE lead explicitly unverified,event36/39 remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
