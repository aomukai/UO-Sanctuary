#!/usr/bin/env python3
"""Bind current Shadow initializer inputs to the owned text and compiled records."""
import json,struct
from pathlib import Path
from corroborate_valkyrie_initializer import ROOT,STAGE,rows,offsets,sha
OUT=ROOT/'.local/class-skills/shadow-initializer-source'
def main():
 OUT.mkdir(parents=True,exist_ok=True)
 receipt=STAGE/'extraction.json';extracted=json.loads(receipt.read_text());assert extracted['referenceVersion']=='3.3.93847'
 for name,entry in extracted['files'].items():assert sha(STAGE/name)==entry['sha256']
 primary=rows('monstats');secondary=rows('monstats2');properties=rows('monprop')
 b=(STAGE/'monstats.bin').read_bytes();c=(STAGE/'monstats2.bin').read_bytes()
 assert struct.unpack_from('<I',b)[0]==len(primary)==751 and len(b)==4+751*508
 assert struct.unpack_from('<I',c)[0]==len(secondary)==623 and len(c)==4+623*296
 b,c=b[4:],c[4:];low=[{k.lower():v for k,v in r.items()}for r in primary]
 fields={}
 for name,fmt,start in [('level','<H',252),('minhp','<H',258),('maxhp','<H',264),('ac','<H',270),('toblock','<B',245),
                       ('resdm','<h',406),('resma','<h',412),('resfi','<h',418),('resli','<h',424),('resco','<h',430),('respo','<h',436)]:
  fields[name]=[]
  for d,suffix in enumerate(['','(n)','(h)']):
   found=offsets(b,508,fmt,[int(r[name+suffix]or 0)for r in low]);assert found==[start+d*struct.calcsize(fmt)],(name,found)
   fields[name].append(found[0])
 assert offsets(b,508,'<I',[int(r['damageregen']or 0)for r in low])==[500]
 for field,offset,bit in [('noratio',60,2),('inventory',63,0)]:
  assert all(bool(b[i*508+offset]&(1<<bit))==bool(int(r[field]or 0))for i,r in enumerate(low))
 property_ids={r['Id']:i for i,r in enumerate(properties)}
 assert offsets(b,508,'<H',[property_ids[r['MonProp']]if r['MonProp']else 65535 for r in primary])==[78]
 assert offsets(b,508,'<B',[int(r['Align']or 0)for r in primary])==[135]
 order=['hd','tr','lg','ra','la','rh','lh','sh','s1','s2','s3','s4','s5','s6','s7','s8'];choices=[]
 for row in secondary:
  r={k.lower():v for k,v in row.items()};choices.append(bytes(len([x for x in r[k+'v'].strip('"').split(',')if x])for k in order))
 assert [o for o in range(281)if all(c[i*296+o:i*296+o+16]==v for i,v in enumerate(choices))]==[21]
 assert offsets(c,296,'<B',[int(r['TotalPieces']or 0)for r in secondary])==[236]
 # Names/IDs come from the pinned extracted skill catalogue.
 skill_text=ROOT/'tools/reference/d2r-named-equipment/json/skills.json'
 manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text())
 assert sha(skill_text)==manifest['files']['json/skills.json']['sha256']
 skill_ids={r['skill']:int(i)for i,r in json.loads(skill_text.read_text()).items()if 'skill'in r}
 mode_ids={k:i for i,k in enumerate(['DT','NU','WL','GH','A1','A2','BL','SC','S1','S2','S3','S4','DD','KB','SQ','RN'])}
 sequences={};slot_checks=0
 for i,r in enumerate(primary):
  for n in range(8):
   name=r['Skill'+str(n+1)];sid=skill_ids[name]if name else -1
   mode=r['Sk'+str(n+1)+'mode'];mid=0 if not name or not mode else mode_ids.get(mode,14)
   assert struct.unpack_from('<h',b,i*508+452+2*n)[0]==sid
   assert struct.unpack_from('<b',b,i*508+468+n)[0]==mid
   assert b[i*508+492+n]==int(r['Sk'+str(n+1)+'lvl']or 0)
   seq=struct.unpack_from('<h',b,i*508+476+2*n)[0]
   if name and mode and mode not in mode_ids:
    assert sequences.setdefault(mode,seq)==seq
   else:assert seq==-1
   slot_checks+=1
 bonuses=[int(r['MonsterSkillBonus'])for r in rows('difficultylevels')];assert bonuses==[0,3,7]
 profiles=[]
 for name,ability,monster in [('shadowwarrior','ShadowWarrior',417),('shadowmaster','ShadowMaster',418)]:
  r=primary[monster];assert r['Id']==name;s=next(i for i,row in enumerate(secondary)if row['Id']==r['MonStatsEx']);v=low[monster]
  def three(field):return[int(v[field+suffix]or 0)for suffix in ['', '(n)','(h)']]
  initial=[dict(id=struct.unpack_from('<h',b,monster*508+452+2*n)[0],rank=b[monster*508+492+n],mode=struct.unpack_from('<b',b,monster*508+468+n)[0],sequence=struct.unpack_from('<h',b,monster*508+476+2*n)[0])for n in range(7)]
  assert [x['id']for x in initial]==[259,266,274,260,275,269,0] and all(x['rank']==1 for x in initial)
  assert [x['mode']for x in initial]==[14]*6+[5] and [x['sequence']for x in initial]==[49,56,49,49,53,49,-1]
  equipment=[r for r in rows('monequip')if r['monster']==name];assert len(equipment)==(4 if monster==417 else 19)and all(int(r['oninit']or 0)==0 for r in equipment)
  profile=dict(ability=ability,monster=monster,alignment=int(v['align']),level=three('level'),noRatio=bool(int(v['noratio'])),inventory=bool(int(v['inventory'])),componentChoiceCounts=list(choices[s]),totalPieces=int(secondary[s]['TotalPieces']),monsterProperty=65535,monsterPropertyCount=len(properties),initialSkills=initial,onInitEquipment=False,minimumLife=three('minhp'),maximumLife=three('maxhp'),defense=three('ac'),block=three('toblock'),resistances=[three(f)for f in ['resdm','resma','resfi','resli','resco','respo']],damageRegeneration=int(v['damageregen']),difficultySkillBonus=bonuses)
  assert struct.unpack_from('<H',b,monster*508+78)[0]==65535 and profile['alignment']==1 and profile['noRatio']and profile['inventory']
  assert profile['minimumLife']==profile['maximumLife']==[376]*3 and profile['defense']==[196]*3 and profile['block']==[50]*3
  assert profile['componentChoiceCounts']==[0,1]+[0]*14 and profile['resistances']==[[40]*3]+[[0]*3]*5
  profiles.append(profile)
 runtime=ROOT/'data/Sanctuary/Balance/shadow-initializer.json';runtime.write_text(json.dumps(profiles,indent=2)+'\n')
 proof=dict(referenceVersion='3.3.93847',executableSha256=extracted['executableSha256'],compiledTextMatches=dict(primaryRows=751,secondaryRows=623,scalarOffsets=fields,damageRegenerationOffset=500,noRatioBit=[60,2],inventoryBit=[63,0],skillSlots=slot_checks,skillIdsOffset=452,skillModesOffset=468,skillSequencesOffset=476,skillRanksOffset=492,consistentSequenceNames=len(sequences)),shadows=profiles,artifacts={str(p.relative_to(ROOT)):sha(p)for p in [Path(__file__).resolve(),receipt,skill_text,runtime,*[STAGE/n for n in extracted['files']]]},limits=['Current owned table inputs only;whole older initializer execution is separate. Sequence IDs/modes are stored metadata,not animation/event or current skill assignment certification. No current outer spawn,AI or learned cast is admitted.'])
 (ROOT/'tools/reference/shadow-initializer-data-evidence.json').write_text(json.dumps(proof,indent=2)+'\n')
 print('Shadow initializer tables:751primary/623secondary rows;6008skills/modes/ranks/sequence slots;both fixed376HP,40physical resistance,196AC,50block,one component choice and seven initial skills.')
if __name__=='__main__':main()
