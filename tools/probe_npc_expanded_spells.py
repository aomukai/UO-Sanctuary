#!/usr/bin/env python3
"""Execute retained complete creator/getters/writer for original NPC proc spells."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-expanded-spells-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True)
 parent=ROOT/'tools/reference/frozen-orb-damage-source-evidence.json';prior=json.loads(parent.read_text());original=BASE/'frozen-orb-damage-source/probe.cpp'
 assert sha(original)==prior['artifacts'][str(original.relative_to(ROOT))];cpp=original.read_text().split('void setup(',1)[0]
 tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit'];bodies={}
 for rel,digest in prior['sourceHashes'].items():
  path=BASE/'d2moo-pinned-source'/rel;raw=path.read_bytes();assert sha(path)==digest and hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel)
  for name,h in prior['routineHashes'].items():
   try:b=function(raw.decode(),name)
   except AssertionError:continue
   assert hashlib.sha256(b.encode()).hexdigest()==h and function(cpp,name)==b;bodies[name]=b
 assert len(bodies)==16
 old='int icebolt=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){assert(formula==1);return icebolt*2;}'
 assert cpp.count(old)==1;cpp=cpp.replace(old,'int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){assert(formula==1);return 0;}')
 refs=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((refs/'sources.json').read_text());tables={};pins={}
 for name in ('skills','missiles'):
  p=refs/f'json/{name}.json';assert sha(p)==manifest['files'][f'json/{name}.json']['sha256'];pins[str(p.relative_to(ROOT))]=sha(p);tables[name]=json.loads(p.read_text())
 selected={sid:tables['skills'][str(sid)]for sid in (36,38,39,44,48,49,53)}
 cpp+='\nvoid setup(int sid){skill={};row={};skill.nSrcDam=0;skill.dwDmgSymPerCalc=-1;skill.dwEDmgSymPerCalc=1;skill.dwELenSymPerCalc=-1;switch(sid){\n'
 for sid,s in selected.items():
  key=s.get('srvmissile',s.get('srvmissilea'));m=tables['missiles'][key]
  assert m['Skill']==s['skill']and not any(s.get(k)for k in ('SrcDam','ELenSymPerCalc'))and not any(m.get(k)for k in ('SrcDamage','SrcMissDmg','MissileSkill','ApplyMastery'))
  cpp+=f'case {sid}:skill.nEType=ELEMTYPE_{s["EType"].upper()};skill.nToHitShift={s["HitShift"]};'
  for field,member in (('EMin','dwEMin'),('EMax','dwEMax'),('ELen','dwELen')):cpp+=f'skill.{member}={s.get(field,0)};'
  for i in range(5):cpp+=f'skill.dwEMinLev[{i}]={s.get("EMinLev"+str(i+1),0)};skill.dwEMaxLev[{i}]={s.get("EMaxLev"+str(i+1),0)};'
  for i in range(3):cpp+=f'skill.dwELevLen[{i}]={s.get("ELevLen"+str(i+1),0)};'
  cpp+='break;\n'
 cpp+='default:abort();}row.nSkill=sid;row.nSrcDamage=row.nSrcMissDmg=0;row.nElemType=ELEMTYPE_NONE;row.nHitShift=0;row.dwDmgSymPerCalc=row.dwElemDmgSymPerCalc=-1;row.dwMissileFlags=0;}\n'
 inputs=[]
 for sid in selected:
  for rank in range(1,64):
   for mastery in (-25,0,37,200):
    for noise in (0,99999):inputs.append((sid,rank,mastery,noise))
 original_graph=BASE/'valkyrie-resources-source/cases.json';originals=[r for r in json.loads(original_graph.read_text())if r['Variant']==0];proc_rows=[]
 for r in originals:
  stats=dict(r['Stages'][0][2]);procs=[(k,v)for k,v in stats.items()if v and k>>16 in (195,198,201)]
  for key,chance in procs:
   sid=(key&65535)>>6;rank=key&63;mastery=stats.get((329 if sid==36 else 331 if sid in (39,44)else 330)<<16,0)
   proc_rows.append(dict(Case=r['Case'],Key=key,Chance=chance,Input=len(inputs),FirePierce=stats.get(333<<16,0),ColdPierce=stats.get(335<<16,0),LightningPierce=stats.get(334<<16,0)))
   inputs.append((sid,rank,mastery,99999))
 assert len(originals)==57 and len(proc_rows)==38 and len(inputs)==3566
 cpp+='int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");int inputs[][4]={'+','.join('{'+','.join(map(str,a))+'}'for a in inputs)+'};\n'
 cpp+=r'''for(auto& a:inputs){int sid=a[0],rank=a[1],mastery=a[2],noise=a[3];owner={};weapon={};missile={};missileData={};setup(sid);
 owner.dwUnitType=UNIT_MONSTER;owner.dwClassId=357;weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missileData.nSkill=999;missileData.nLevel=rank;critical=true;critCalls=0;
 for(int& s:owner.stats)s=noise;owner.stats[STAT_PASSIVE_FIRE_MASTERY]=owner.stats[STAT_PASSIVE_COLD_MASTERY]=owner.stats[STAT_PASSIVE_LTNG_MASTERY]=mastery;
 D2UnitStrc origin;for(int& s:origin.stats)s=99999;D2MissileDamageDataStrc packet;
 MISSILE_CalculateDamageData(&packet,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&packet,rank);
 printf("%s[[%d,%d,%d,%d],[",first?"":",",sid,rank,mastery,noise);first=false;
'''
 fields,stats=prior['packetFields'],prior['storedStats']
 for i,n in enumerate(fields):cpp+=f'printf("{","if i else""}%d",packet.{n});\n'
 cpp+='printf("],[");\n'
 for i,n in enumerate(stats):cpp+=f'printf("{","if i else""}%d",missile.stats[{n}]);\n'
 cpp+='printf("],%d,%d]",critCalls,missile.stats[STAT_LEVEL]);}printf("]\\n");}\n'
 for n,b in bodies.items():assert function(cpp,n)==b
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';compile=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(p),'-o',str(binary)],capture_output=True);compiler=STAGE/'compiler.log';compiler.write_bytes(compile.stdout+compile.stderr);assert compile.returncode==0,compile.stderr.decode()
 run=subprocess.run([str(binary)],capture_output=True);san=STAGE/'sanitizer.log';san.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode();rows=json.loads(run.stdout);assert len(rows)==len(inputs)
 cases=[]
 for i,(args,packet,stored,critical,level)in enumerate(rows):
  sid,rank,mastery,noise=args;s=selected[sid];ends=[]
  for maximum in (False,True):
   prefix='EMax'if maximum else'EMin';n=s[prefix];left=rank-1
   for tier,length in enumerate((7,8,6,6,63),1):count=min(left,length);n+=count*s.get(prefix+'Lev'+str(tier),0);left-=count
   n<<=s['HitShift'];n+=int(n*mastery/100);ends.append(n)
  element=s['EType'];length=200+25*(rank-1)if sid==44 else 150+35*(rank-1)if sid==39 else 0;prefix={'fire':'Fire','cold':'Cold','ltng':'Light'}[element]
  expect=[ends[0]if n=='n'+prefix+'MinDamage'else ends[1]if n=='n'+prefix+'MaxDamage'else length if n=='nColdLength'else 0 for n in fields]
  sp={'fire':'FIRE','cold':'COLD','ltng':'LIGHT'}[element];saved=[ends[0]if n=='STAT_'+sp+'MINDAM'else ends[1]if n=='STAT_'+sp+'MAXDAM'else length if n=='STAT_COLDLENGTH'else 0 for n in stats]
  assert packet==expect and stored==saved and critical==0 and level==rank,(args,packet,expect,stored,saved)
  count=min(24,rank+2)if sid==38 else(rank+25)//5 if sid==53 else 64 if sid in (44,48)else 1
  cases.append(dict(Skill=sid,Rank=rank,Mastery=mastery,Noise=noise,Minimum256=ends[0],Maximum256=ends[1],ColdFrames=length,Emissions=count))
 fixture=STAGE/'cases.json';fixture.write_text(json.dumps(dict(Cases=cases,Originals=proc_rows),separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,cases=len(cases),numericMatrix=3528,originalBodies=57,originalProcLayers=38,sourceHashes=prior['sourceHashes'],routineHashes=prior['routineHashes'],packetFields=fields,storedStats=stats,inputs={str(q.relative_to(ROOT)):sha(q)for q in [Path(__file__).resolve(),parent,original,original_graph,ROOT/'tools/reference/valkyrie-resources-source-evidence.json',refs/'sources.json']},currentTable=pins,artifacts={str(q.relative_to(ROOT)):sha(q)for q in [p,compiler,san,fixture]},
  established=['Sixteen complete creator/getter/writer routines compose seven current spell rows at all six-bit nonzero proc ranks1..63,negative/zero/positive own mastery and unrelated owner/weapon/origin stat noise. Explicit Skill row owns endpoints;source0 excludes weapon/elemental/leech/critical payload,with no intrinsic critical call during creation. Ice Bolt and Frost Nova store their separate source cold lengths.',
  'The38original proc layers across57unchanged source item graphs use their own masteries/pierce and exact retained layer rank;there is no owner synergy/gear or +skill rank adjustment. The zero hard-formula service is an explicit Valkyrie context with only its four captured defensive/passive skills;unmaterialized item-granted skills remain refused.'],
  limits=['This executes older numeric source with pinned current tables,not modern binary proc delivery. Formula/stat/row/ratio and empty monster-type-copy services remain declared. Signed mastery controls are bounded to the tested matrix;the runtime exposes a bounded numeric domain.',
  'No spell emission,hit/HP,cold application,proc trigger order/history,child RNG allocation,source-removal,skill-list materialization,learned cast or assets are admitted by this proof. Existing player/Mage launchers and their separate source evidence must be reused with an explicit original-NPC context.'])
 target=ROOT/'tools/reference/npc-expanded-spells-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
 if check:assert target.read_text()==encoded
 else:target.write_text(encoded)
 print('NPC item spell damage:3566complete creator/getter/writer contexts,7spells/all63proc ranks/38original layers;ASan+UBSan clean;delivery remains separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');probe(p.parse_args().check)
