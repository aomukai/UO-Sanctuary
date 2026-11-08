#!/usr/bin/env python3
"""Record own NPC kick capture; authenticate prior proofs without rerunning them."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/shadow-kick'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def sources():
 tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());routines={};receipts={}
 for name in ['dragon-kick-damage-source-evidence','dragon-talon-hands-source-evidence','dragon-kick-packet-source-evidence']:
  p=ROOT/'tools/reference'/(name+'.json');d=json.loads(p.read_text());receipts[str(p.relative_to(ROOT))]=sha(p);texts=[]
  assert d['legacyCommit']==tree['sha'] and d['referenceVersion']=='3.3.93847'
  for rel,h in d['sourceHashes'].items():
   raw=(ROOT/'.local/class-skills/d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha256(raw).hexdigest()==h
   assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel)
   texts.append(raw.decode())
  for symbol,h in d['compiledRoutines'].items():
   found=[]
   for text in texts:
    m=re.search(r'^.*\b'+re.escape(symbol)+r'\([^;\n]*\)\s*\n\{',text,re.M)
    if not m:continue
    end=text.index('{',m.start())+1;depth=1
    while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
    found.append(hashlib.sha256(text[m.start():end].encode()).hexdigest())
   assert found==[h],symbol;routines[symbol]=h
  fixture=ROOT/d['fixture']['path'];assert sha(fixture)==d['fixture']['sha256']
 assert len(routines)==7
 return dict(routines=routines,receipts=receipts,newCompilerMatrices=0)
def main(check=False):
 source=sources();log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log))
 assert sum(counts.values())==15 and ': failed'not in log and ' ERR]'not in log
 for family in ['shadow-kick','shadow-dragon-claw']:assert counts[family+'-check']==1 and counts[family+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-1.log').read_text()
 for kind in ['content','integration']:assert all(x in(STAGE/f'{kind}-build-1.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snapshot=json.loads((STAGE/'source-snapshot-1.json').read_text())
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snapshot['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 saved=json.loads((STAGE/'shadow-kick-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Bodies'])==2 and all(not b['Input']['Player']for b in saved['Bodies'])
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(x['implementation']=='handler-present'for x in status['skills'])==208 and status['completedClasses']==[]
 files=[STAGE/n for n in ['source-snapshot-1.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','content-build-1.log','integration-build-1.log','shadow-kick-fixtures.json']]
 files+=[ROOT/'tools/record_shadow_kick.py',ROOT/'tests/check_shadow_kick.py']
 receipt=dict(parent='945a387dd0311c463926a03c6d487d8c5ddeac53',referenceVersion='3.3.93847',handlers=208,assassinHandlers=28,completedClasses=[],learnedShadowsEnabled=False,intrinsicTalonConnected=False,
  authenticatedSource=source,nativeMarkers=15,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=1,
  artifacts=native['artifacts'],files={str(p.relative_to(ROOT)):sha(p)for p in files},preservation=preservation(),diagnostic='Both clean builds and the focused native run passed on their first attempt.',
  scope='Both original Shadows:prepared own NPC kick body level, original boots, weapon-damage suppression with nondamage attributes retained, primary restoration, actual saved actor-only physical RNG, no owner resource/HP/charge mutation and original item/resource/wound/RNG saves. Retained own Dragon Claw contacts and its source controls pass. Seven prior full routines and their existing fixtures authenticated without rerunning matrices.',
  limits=['No Talon startup allocation/cache, typed kick fill, repeated action scheduler, progressive delivery/consumption, world/AI Talon or learned Shadow admission yet.','Older flat kick double-add with boots and modern NPC input/timing/hand/charge/callback/removal/PvP boundaries remain uncertain.','Prepared kick emits no new visual or icon. Existing pending assets stay separately inventoried.'])
 out=ROOT/'tools/reference/shadow-kick-native-evidence.json'
 if check:assert json.loads(out.read_text())==receipt;print('Own NPC kick receipt matches.')
 else:out.write_text(json.dumps(receipt,indent=2)+'\n');print('Recorded own NPC kick:15 native markers/two reloads;seven prior routines reused;installed build and saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
