#!/usr/bin/env python3
"""Record original Shadow Tail using existing complete source controls."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from probe_current_item_affixes import function
from record_shadow_kick_packets import source as packet_source
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-tail'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def sources():
 tree=json.loads((BASE/'d2moo-source-tree.json').read_text());receipts={};routines={};fixtures={}
 for name in ['source','clock-source','rate-source','lifetime-source','burst-source','action-source']:
  p=ROOT/'tools/reference'/('dragon-tail-'+name+'-evidence.json');d=json.loads(p.read_text());receipts[str(p.relative_to(ROOT))]=sha(p)
  inputs=d.get('inputs')or{'.local/class-skills/d2moo-pinned-source/'+k:v for k,v in d.get('sourceHashes',{}).items()};texts=[]
  for rel,h in inputs.items():
   p=ROOT/rel;assert sha(p)==h,rel
   if 'd2moo-pinned-source/'in rel:
    raw=p.read_bytes();r=rel.split('d2moo-pinned-source/',1)[1];assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==r)
    if p.suffix=='.cpp':texts.append(raw.decode())
  for symbol,h in d.get('compiledRoutines',d.get('compiledUnchangedRoutineHashes',d.get('reviewedUnchangedRoutineHashes',{}))).items():
   found=[hashlib.sha256(function(t,symbol).encode()).hexdigest()for t in texts if re.search(r'^.*\b'+re.escape(symbol)+r'\([^;\n]*\)\s*\n\{',t,re.M)]
   assert found==[h],symbol;routines[symbol]=h
  if 'fixture'in d:
   f=d['fixture'];assert sha(ROOT/f['path'])==f['sha256'];fixtures[f['path']]=dict(sha256=f['sha256'],cases=f['cases'])
  for rel,h in d.get('artifacts',{}).items():assert sha(ROOT/rel)==h,rel
 return dict(receipts=receipts,completeRoutines=routines,retainedFixtures=fixtures,packetSource=packet_source(),newCompilerMatrices=0)
def main(check=False):
 auth=sources();log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==24 and ': failed'not in log and ' ERR]'not in log
 for family in ['shadow-tail','shadow-talon','shadow-kick-packets','shadow-ice','shadow-thunder']:assert counts[family+'-check']==1 and counts[family+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-2.log').read_text()
 for kind,n in [('content',1),('integration',2)]:assert all(x in(STAGE/f'{kind}-build-{n}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snapshot=json.loads((STAGE/'source-snapshot-3.json').read_text());built=json.loads((STAGE/'source-snapshot-1.json').read_text())
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snapshot['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 assert {p:h for p,h in snapshot['inputs']['sourceInputs'].items()if not p.startswith('tests/')}=={p:h for p,h in built['inputs']['sourceInputs'].items()if not p.startswith('tests/')}
 assert snapshot['inputs']['copiedData']==built['inputs']['copiedData']
 saved=json.loads((STAGE/'shadow-tail-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Bodies'])==2 and all(b['Charges']for b in saved['Bodies'])
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(x['implementation']=='handler-present'for x in status['skills'])==208 and status['completedClasses']==[]
 names=['source-snapshot-1.json','source-snapshot-2.json','source-snapshot-3.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','native-run-2.log','birth-inputs-initial-pass.json','birth-native-checks-initial-pass.log','content-build-1.log','integration-build-1.log','integration-build-2.log','shadow-tail-fixtures.json']
 files=[STAGE/n for n in names]+[ROOT/p for p in ['tools/record_shadow_tail.py','tests/check_shadow_tail.py','tools/reference/dragon-tail-sanctuary-decision.json']]
 receipt=dict(parent='ac4201c9c4162fdf51c7e404b1afc98e08a31c54',referenceVersion='3.3.93847',handlers=208,assassinHandlers=28,completedClasses=[],learnedShadowsEnabled=False,intrinsicTailConnected=True,
  authenticatedSource=auth,nativeMarkers=24,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=2,sourceSnapshot=3,contentUnchangedSinceSnapshot=1,
  artifacts=native['artifacts'],files={str(p.relative_to(ROOT)):sha(p)for p in files},preservation=preservation(),
  diagnostic='Initial run passed. Added independent native damage-notification ordering checks because summed HP alone did not distinguish reversed parent/fire delivery. Integration rebuilt and same focused cohort passed again;no production change or source matrix rerun.',
  scope='Both original Shadows use own start27 cached source128 full kick,action-local -40 attack rate and do50 copy of mitigated physical before progressive contact,then separately resisted fire area with mapped knockback even at zero fire. No Talon physical ED or hit-class overwrite. Native callbacks prove parent before area;0/50/100 physical resistance,immunity,range,miss,charged shield,all six emissions/consume-one,world requests,natural Warrior AI,cancel/no replay and exact original saved actors/items/resources/wounds/RNG/charges pass. Shared kick contact preflight is reused by Talon;retained Talon/packet/Ice/Thunder cohorts pass.',
  limits=['Exact modern NPC timing,mutation,event ordering,geometry,removal/PvP and other skillflags remain unverified. Older start/do control flow is explicitly combined with the approved charged accuracy-only/block/consume-one policy;not current executable proof.',
   'Action-local speed penalty follows existing Tail preparation policy rather than persisting an obsolete temporary stat list. Ordinary NPC startup is free and refuses miss/block;player payment/input rules are not imported.',
   'Area recipients without qualified native damage/knockback adapters are logged and skipped. Unrestricted natural Master loadouts and selection retain their existing gaps.',
   'Own Flight/Fury/Sentinel/current death/learned Shadow gates and separate player mixed-finisher audit remain. Final effects/audio and talent/hotkey icons are separate placeholders.'])
 out=ROOT/'tools/reference/shadow-tail-native-evidence.json'
 if check:assert json.loads(out.read_text())==receipt;print('Own NPC Tail receipt matches.')
 else:out.write_text(json.dumps(receipt,indent=2)+'\n');print('Recorded own NPC Tail:24 native markers/two reloads;complete existing source controls reused;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
