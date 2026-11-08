#!/usr/bin/env python3
"""Bind own Fury capture/free resource adapter and corrected execution flags."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from probe_current_item_affixes import function
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-fury-packets'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 proof=json.loads((ROOT/'tools/reference/shadow-fury-packets-source-evidence.json').read_text());assert proof['contexts']==16 and len(proof['completeRoutines'])==6
 for p,h in (proof['inputs']|proof['artifacts']).items():assert sha(ROOT/p)==h,p
 assert not(BASE/'shadow-fury-packets-source/sanitizer.log').read_text()
 reused={}
 for name in ['blade-fury-damage-source-evidence','blade-fury-resource-source-evidence']:
  p=ROOT/'tools/reference'/(name+'.json');d=json.loads(p.read_text());f=d['fixture'];assert sha(ROOT/f['path'])==f['sha256'];texts=[]
  for rel,h in d['sourceHashes'].items():q=BASE/'d2moo-pinned-source'/rel;assert sha(q)==h; texts.append(q.read_text())
  bodies=d.get('compiledRoutineHashes',d.get('compiledUnchangedRoutines'))
  for symbol,h in bodies.items():assert [hashlib.sha256(function(t,symbol).encode()).hexdigest()for t in texts if re.search(r'^.*\b'+re.escape(symbol)+r'\([^;\n]*\)\s*\n\{',t,re.M)]==[h],symbol
  reused[name]=dict(receipt=sha(p),fixture=f,routines=bodies)
 before=json.loads((STAGE/'item-skill-execution-before.json').read_text());after=json.loads((ROOT/'data/Sanctuary/Balance/item-skill-execution.json').read_text());changes=[]
 for a,b in zip(before['Skills'],after['Skills']):
  if a!=b:assert a|{'UseManaOnDo':True}==b and not a['UseManaOnDo'] and b['Fields']['usemanaondo']==1;changes.append(b['Id'])
 assert changes==[25,266,402] and before|{'Skills':after['Skills']}==after
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==21 and ': failed'not in log and ' ERR]'not in log
 for family in ['shadow-fury-packets','item-execution-metadata','npc-item-skills','blade-fury-missile']:assert counts[family+'-check']==1 and counts[family+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-3.log').read_text()
 for kind,n in [('content',4),('integration',3)]:assert all(x in(STAGE/f'{kind}-build-{n}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snapshot=json.loads((STAGE/'source-snapshot-5.json').read_text());assert native['copiedData']==snapshot['inputs']['copiedData']
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snapshot['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 saved=json.loads((STAGE/'shadow-fury-packets-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Bodies'])==2 and all(b['Packet']['Damage']['SourcePoisonFrames']for b in saved['Bodies'])
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(x['implementation']=='handler-present'for x in status['skills'])==208 and status['completedClasses']==[]
 names=['source-snapshot-1.json','source-snapshot-2.json','source-snapshot-3.json','source-snapshot-4.json','source-snapshot-5.json','birth-inputs.json','birth-native-checks.log','birth-inputs-failed-1.json','birth-native-checks-failed-1.log','birth-inputs-pre-resource-pass.json','birth-native-checks-pre-resource-pass.log','native-run-1.log','native-run-2.log','native-run-3.log','content-build-1.log','content-build-2.log','content-build-3.log','content-build-4.log','integration-build-1.log','integration-build-2.log','integration-build-3.log','shadow-fury-packets-fixtures.json','item-skill-execution-before.json','item-skill-execution-metadata-before.json']
 files=[STAGE/n for n in names]+[BASE/n for n in ['shadow-fury-packets-source-run-1.log','shadow-fury-packets-source-run-2.log','shadow-fury-packets-source-run-3.log','shadow-fury-packets-source/compiler-failed-2.log','shadow-fury-packets-source/source-failed-2.cpp']]
 files += [ROOT/p for p in ['tools/record_shadow_fury_packets.py','tools/probe_shadow_fury_packets.py','tools/build_item_skill_execution.py','tools/reference/item-skill-execution-metadata.json','tools/reference/shadow-fury-packets-source-evidence.json','tests/check_shadow_fury_packets.py','docs/shadow-fury-mana-proposal.md']]
 receipt=dict(parent='1a100ab8b8622cc310ca361aee9de484a727d1e5',referenceVersion='3.3.93847',handlers=208,completedClasses=[],learnedShadowsEnabled=False,intrinsicFuryConnected=False,packetQualified=True,freeNpcResourceComponent=True,approval='Approve the Shadow Fury mana exception',newSourceContexts=16,completeNewSourceRoutines=proof['completeRoutines'],reusedSource=reused,correctedManaFlagSkills=changes,nativeMarkers=21,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=4,integrationBuild=3,sourceSnapshot=5,artifacts=native['artifacts'],files={str(p.relative_to(ROOT)):sha(p)for p in files},preservation=preservation(),
  diagnostic='Source harness first assumed an older receipt schema key,then needed cstdlib for aborting unused service stubs;both diagnostics retained. Initial native row guard exposed a real catalogue capitalization bug:UseManaOnDo read a missing field instead of current usemanaondo. Generator,reader validation and exactly3normalized booleans corrected;all raw rows unchanged. The next cohort passed. Approved no-mana NPC state adapter and focused zero-mana/lease/deadline/reentry tests were then added;final run passed.',
  scope='Both original Shadows:own primary weapon/body stats and hard synergy capture through existing Fury/Bow creator,typed source96,poison override/count,source double armor delta,creation flags and supplied critical. No actor/world RNG,emission,damage,charge consumption or mana spending during capture. Owner-stat isolation,secondary-hand exclusion,soft/hard synergy distinction,original primary restoration,borrowed-rank refusal,packet JSON and exact original actor/items/resources/wounds/seed/packet across2reloads pass. Approved free NPC resource adapter reuses full shared21/7frame lease,strict5frame emission deadline,released-bit/list distinction and expiry/reentry with actual zero owner/pet mana. All429raw metadata rows and flags25/266/402,original NPC item skills and player Fury missile impacts/saves pass.16new monster creator controls;old compiler matrices not rerun.',
  limits=['Critical selection remains an explicit supplied boolean. NPC channel clock/startup/rewind/automatic AI/world missile allocation,private RNG,AR/block/impact/events and released-flight persistence remain unconnected/unverified. This checkpoint does not enable or certify intrinsic Fury gameplay.',
   'Resource tests exercise the reused component through explicit emission/rewind/allocation services;they do not establish NPC animation or callback timing. Poison/mastery/pierce/bypass consumption at projectile impact remains separate.',
   'Corrected catalogue flags are verified for all3rows and shared consumers;this is not new full Plague Javelin/Abyss mechanical certification. Existing saves/installed build unchanged;no completed class/deployment.',
   'Final blade/channel/cast/impact/audio and talent/hotkey icons remain identified placeholders.'])
 out=ROOT/'tools/reference/shadow-fury-packets-native-evidence.json'
 if check:assert json.loads(out.read_text())==receipt;print('Own Fury packet/resource receipt matches.')
 else:out.write_text(json.dumps(receipt,indent=2)+'\n');print('Recorded own Fury packet/free NPC state:21native markers/two reloads,16source contexts;catalogue flags corrected;installed state preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
