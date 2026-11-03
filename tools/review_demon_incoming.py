#!/usr/bin/env python3
"""Pin demon return capabilities and the unverified Blood Boil boundary."""
import argparse,csv,hashlib,json
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def review(check=False):
 inputs={}
 def pin(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_text()
 owned=json.loads(pin(ROOT/'tools/reference/d2r-local-sentry-model-evidence.json'));tables={}
 for key in ('monstats','monstats2'):
  p=ROOT/owned['files'][key]['path'];assert sha(p)==owned['files'][key]['sha256']
  tables[key]={r['Id']:r for r in csv.DictReader(pin(p).lstrip('\ufeff').splitlines(),delimiter='\t')}
 identity={r['Source']:r for r in json.loads(pin(ROOT/'data/Sanctuary/Balance/monster-return-identities.json'))};profiles=[]
 for key,id,velocity in (('wargoatman',742,8),('warbighead',743,6)):
  a=tables['monstats'][key];b=tables['monstats2'][a['MonStatsEx']];row=identity[key]
  modes=sum((1<<bit) for bit,k in ((3,'mGH'),(6,'mBL'),(13,'mKB')) if b[k]=='1')
  expected=dict(Source=key,Class=id,Base=id,HitClass=3,ModeFlags=8192,Velocity=velocity,Killable=True,Town=True)
  assert int(a['*hcIdx'])==id and int(b['*hcIdx'])==id and a['BaseId']==key and int(a['Velocity'])==velocity
  assert a['killable']==a['inTown']=='1' and int(b['HitClass'])==3 and b['mGH']=='0' and b['mBL']=='' and b['mKB']=='1'
  assert modes==8192 and row==expected;profiles.append(expected)
 # This is a metadata reuse review,not a newly compiled damage/return oracle.
 prior=json.loads(pin(ROOT/'tools/reference/monster-return-source-evidence.json'))
 assert prior['referenceVersion']=='3.3.93847' and not prior['currentExecutableParity'] and prior['variants']['differentCases']==156
 originals=[]
 for path,digest in prior['inputs'].items():
  if '/d2moo-pinned-source/' in path:
   p=ROOT/path;assert sha(p)==digest;originals.append(pin(p))
 for name,digest in prior['compiledUnchangedRoutineHashes'].items():
  matches=[]
  for text in originals:
   try:matches.append(function(text,name))
   except (ValueError,AssertionError):pass
  assert any(hashlib.sha256(block.encode()).hexdigest()==digest for block in matches),name
 foundation=json.loads(pin(ROOT/'tools/reference/warlock-foundation-preflight.json'))
 raw=ROOT/'.local/research/d2r-3.3.93847/skills.txt';assert sha(raw)==foundation['licensedSkillSha256']
 row=next(r for r in csv.DictReader(pin(raw).lstrip('\ufeff').splitlines(),delimiter='\t') if r['skill']=='Blood Boil')
 assert row['*Id']=='380' and row['srvdofunc']=='160' and row['pettype']=='demon' and row['requirespettype']=='1'
 observation=ROOT/'.local/class-skills/blood-boil-reference/preflight.json';gap=json.loads(pin(observation))
 assert gap['candidateSlot']==160 and gap['candidateRva']=='0x44e2c0' and not gap['processMemoryRead'] and not gap['dispatcherLinkVerified'] and not gap['semanticsVerified']
 pin(Path(__file__).resolve())
 result=dict(referenceVersion='3.3.93847',currentExecutableParity=False,handlerEnabled=False,component='Shared demon incoming return metadata',
  profiles=profiles,reuses='tools/reference/monster-return-source-evidence.json',newCompiledOracle=False,
  bloodBoil=dict(skill=380,doFunction=160,candidateRva=gap['candidateRva'],executionVerified=False,observation=gap),inputs=inputs,
  established=['Owned current monstats/monstats2 and the existing return identity agree for Goatman742 and Tainted743:own class/base,hitclass3,killable/town flags,velocities8/6,no get-hit or block mode,knockback available.',
   'The shared demon profile previously omitted hcIdx and mode metadata,so the ordered incoming receiver refused it. Reuse existing pinned identity capabilities;do not inherit Gargoyle/Succubus UO block/recovery capabilities or implement a second damage formula.'],
  limitations=['Metadata agreement is not full current executable parity. Existing return proof and unchanged pinned original routine bodies are bound. Historical ignored dependencies are not all present,so its whole artifact inventory is not revalidated and no new complete oracle is claimed.',
   'Blood Boil is a distinct new do160 routine. Its candidate table/slot shape has no proven dispatcher link;the observed candidate page is unreadable. No process code was sampled and no execution,health cost,proc cadence or target-selection behavior is invented. Continue unaffected incoming mechanics.'])
 target=ROOT/'tools/reference/demon-incoming-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==encoded
 else:target.write_text(encoded)
 print('Demon incoming:two owned return identities reused;Blood Boil do160 execution remains explicitly unverified.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');review(p.parse_args().check)
