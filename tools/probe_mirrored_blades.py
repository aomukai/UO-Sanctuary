#!/usr/bin/env python3
"""Pin Mirrored Blades suppliers and current SQ25 without inventing do166."""
import argparse,csv,json
from pathlib import Path
from player_weapon_sequences import ROOT,BASE,read,sha
STAGE=BASE/'mirrored-blades'
def build(check=False):
 STAGE.mkdir(exist_ok=True);rows,inputs,executable=read(25)
 cleave,_,_=read(24);assert cleave==json.loads((ROOT/'data/Sanctuary/Balance/cleave-sequence.json').read_text())
 def pin(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_text()
 manifest=json.loads(pin(ROOT/'tools/reference/d2r-named-equipment/sources.json'));tables={}
 for n in ['skills','skilldesc','allstrings-eng']:
  p=ROOT/f'tools/reference/d2r-named-equipment/json/{n}.json';assert sha(p)==manifest['files'][f'json/{n}.json']['sha256'];tables[n]=json.loads(pin(p))
 skill=tables['skills']['392'];desc=next(r for r in tables['skilldesc'].values() if r.get('skilldesc')=='mirrored blades')
 assert skill['srvstfunc']==68 and skill['srvdofunc']==166 and skill['seqnum']==25 and skill['Param11']==1 and skill.get('Param12',0)==0 and skill.get('Param8',0)==0
 assert desc['dsc2calca1']=='clc5-1' and desc['desccalca5']=='clc6'
 with (ROOT/'.local/research/d2r-3.3.93847/skills.txt').open(encoding='utf-8-sig',newline='') as f:owned=next(r for r in csv.DictReader(f,delimiter='\t') if r.get('skill')=='Mirrored Blades')
 pin(ROOT/'.local/research/d2r-3.3.93847/skills.txt')
 for k in ['srvstfunc','srvdofunc','seqnum','calc3','calc5','calc6','calc7','calc8','calc9','Param11','Param12','DmgSymPerCalc']:assert str(skill[k]).strip('"')==owned[k]
 assert owned['*calc5 desc']=='Amount of attacks' and 'after the first one' in owned['*calc6 desc'] and 'Crushing blow' in owned['*calc7desc']
 with (BASE/'cleave/skilldesc.txt').open(encoding='utf-8-sig',newline='') as f:owneddesc=next(r for r in csv.DictReader(f,delimiter='\t') if r.get('skilldesc')=='mirrored blades')
 pin(BASE/'cleave/skilldesc.txt');extraction=json.loads(pin(BASE/'cleave/skilldesc-extraction.json'));assert sha(BASE/'cleave/skilldesc.txt')==extraction['sha256']
 for k in ['dsc2calca1','desccalca5','ddam calc1']:assert str(desc[k])==owneddesc[k]
 for r in rows:
  assert r['RawAuxiliary']==256 and r['Points']==r['Frames'] and all(x[0]==0 and x[1]==10 and x[3]==0 and x[4] in [0,1] for x in r['Rows']) and sum(x[4] for x in r['Rows'])==1
 page=pin(ROOT/'.local/research/warlock-community/mirrored-blades.html');assert '3.3.93847' in page and 'reached at level 15' in page and '108%' in page
 pin(ROOT/'.local/research/warlock-community/warlock-skills-notes.txt')
 data=ROOT/'data/Sanctuary/Balance/mirrored-blades-sequence.json';probe=BASE/'mirrored-blades-source-probe.json'
 def write(p,obj):
  body=json.dumps(obj,indent=2)+'\n'
  if check:assert p.read_text()==body,p
  else:p.write_text(body)
 write(data,rows);write(probe,dict(classicDescriptorsMatched=322,sequence=25,weaponProfiles=rows,legacyStart68IsNull=True,modernBodyVerified=False))
 result=dict(referenceVersion='3.3.93847',legacyCommit='0c0bbd1e6bef9181a3da1c046523af622a925416',executableSha256=executable,readOnlyFileInspection=True,processMemoryRead=False,processInput=False,classicDescriptorsMatched=322,sequence=25,profileCount=14,handlerEnabled=False,modernBodyVerified=False,skill=skill,description=desc,
  currentDataminedReference=dict(url='https://diabloforfans.com/skill/mirrored-blades',accessed='2026-10-29',statedGameVersion='3.3.93847',localFile='.local/research/warlock-community/mirrored-blades.html',scope='Primary extractor report corroborates total attack count,duplicate damage and ordinary item-event eligibility;not execution of the current game body.'),
  inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in [data,probe,Path(__file__).resolve()]},
  established=['Owned skill392and skilldesc agree with cached fields. Total attacks min(2+rank/5,5),duplicates total-minus1:five total at effective15. Primary scale100;Param11restricts calc6to later attacks,dm12range50..120gives60at1and108at20. AR100+15per extra rank;dm34speed10..30;mana(10+rank-1)/2. calc1/2/4are0. DmgSymPerCalc references absent Param8and evaluates0;no synergy is invented.',
   'Owned calc7/8/9labels explicitly identify duplicate Crushing Blow melee/ranged/player divisors. Thresholds are ranks6/11/16,separate from attack-count ranks5/10/15. Raw values8/16/20at1and20/40/50at16are not automatic target/elite/PvP selection. Param12zero does not by itself establish missile identity or hit-delay dispatch.',
   'The current pinned executable SQ25has14weapon-dependent16..21point profiles and one melee event each,all CAST10pose rows/raw auxiliary256. All322classic descriptors corroborate layout. The SQevent does not mean one separate scheduled event per mirror;do166must implement distribution.'],
  remaining=['Modern start68/do166and mirror emission/target/search,melee/bow/throw/ammunition,hit and item-event chronology remain unverified. Older start68is null. Current-file data and primary extractor notes are not full executable proof. No learned route is enabled.',
   'Whole packet scaling placement/rounding,poison/cold/length,mastery/critical,Crushing Blow elite/PvP/controlled overrides and exact proc order remain unsupported. Prepared scalar percentages and supplied raw-divisor arithmetic do not select these contexts or execute a multi-attack skill.',
   'Older primary notes say potion throwing works;current table excludes tpot. Potion eligibility remains uncertain. Older notes place the mirror cap at20;current formula/tooltip and pinned-version extractor put it at15. Current data wins for the prepared count.'])
 write(ROOT/'tools/reference/mirrored-blades-source-evidence.json',result)
 print('Mirrored Blades: current SQ25 and 322 classic descriptors; five attacks at rank15, separate duplicate damage/divisors; modern start/do remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
