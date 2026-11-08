#!/usr/bin/env python3
"""Bind approved intrinsic S2 routes to native/world/AI and preservation evidence."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-sentinel-action'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 reused={}
 for n in ['shadow-animation-source-evidence','assassin-trap-summon-engine-evidence','blade-sentinel-engine-evidence']:
  p=ROOT/'tools/reference'/f'{n}.json';d=json.loads(p.read_text())
  for rel,h in (d.get('inputs',{})|d.get('artifacts',{})).items():assert sha(ROOT/rel)==h,rel
  for rel,h in d.get('sourceHashes',{}).items():assert sha(BASE/'d2moo-pinned-source'/rel)==h,rel
  if'fixture'in d:assert sha(ROOT/d['fixture']['path'])==d['fixture']['sha256']
  reused[n]=sha(p)
 review=json.loads((ROOT/'tools/reference/shadow-death-modifier-source-review.json').read_text())
 for rel,h in review['inputs'].items():assert sha(ROOT/rel)==h,rel
 assert review['reviewToolSha256']==sha(ROOT/'tools/review_shadow_death_modifier.py')
 approval=json.loads((STAGE/'approval-state.json').read_text());assert approval['approved']=='Approve no additional NPC cooldown'and approval['skills']==[256,257]
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==24 and ': failed'not in log and ' ERR]'not in log
 for n in ['shadow-sentinel-action','shadow-sentinel','shadow-sentry','blade-spawn','shadow-shock-web']:assert counts[n+'-check']==1 and counts[n+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-4.log').read_text()
 for kind,n in [('content',3),('integration',5)]:assert all(x in(STAGE/f'{kind}-build-{n}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-6.json').read_text());content=json.loads((STAGE/'source-snapshot-4.json').read_text());assert native['copiedData']==snap['inputs']['copiedData']==content['inputs']['copiedData']
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 for p,h in content['inputs']['sourceInputs'].items():
  if p.startswith(('src/','shared/')):assert native['sourceInputs'][p]==h,p
 saved=json.loads((STAGE/'shadow-sentinel-action-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Bodies'])==2
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(x['implementation']=='handler-present'for x in status['skills'])==208 and status['completedClasses']==[]
 files=[p for pattern in ['source-snapshot-*.json','*-build-*.log','native-run-*.log','birth-*.json','birth-*.log','*-fixtures.json','approval-state.json']for p in STAGE.glob(pattern)]
 files += [ROOT/p for p in ['tools/record_shadow_sentinel_action.py','tests/check_shadow_sentinel_action.py','docs/shadow-npc-cooldown-proposal.md','tools/review_shadow_death_modifier.py','tools/reference/shadow-death-modifier-source-review.json']]
 receipt=dict(parent='634131861b5050f5948455ddf25fbb184f00a5d8',referenceVersion='3.3.93847',handlers=208,completedClasses=[],learnedShadowsEnabled=False,intrinsicSentinelActionConnected=True,intrinsicShockWebActionConnected=True,automaticSentinelConsumer=True,automaticShockWebConsumer=True,approvedRule=approval['approved'],approvedSkills=approval['skills'],reusedSource=reused,newCompilerMatrices=0,nativeMarkers=24,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=3,integrationBuild=5,sourceSnapshot=6,artifacts=native['artifacts'],files={str(p.relative_to(ROOT)):sha(p)for p in sorted(set(files))},preservation=preservation(),
 diagnostic='First native run reached the stun cancellation fixture,but generic CombatStuns.Apply did not supply a duration for the unmapped generic Shadow profile;the fixture now explicitly supplies a resolved25frame prepared duration,testing cancellation without claiming generic eligibility. Prepared-only native2 passed. User then approved no extra NPC cooldown for256/257;automatic consumers and Shock Web S2 were added. Integration3 exposed a fixture helper named World shadowing Server.World;renamed WorldAi. C3/I4/native3 passed the approved Sentinel AI and both consumers. The same world/natural AI control was then extended to Shock Web;final C3/I5/native4 passes all3boots/2reloads. Earlier logs retained;native2 summary retained but detailed birth log was replaced by final cohort.',
 scope='Both original seed1 Shadow species use own S2 attack-rate plan/start0 and generic NPC do44/do43 once-only release. Sentinel ground/moving/deleted target and live-rank contexts,root blade birth snapshot,zero player/NPC mana and actor attack RNG,ten cancellation paths,failed range and exact original actor/items/resources/wounds/RNG saves. Shock Web releases original actual shards at S2 marker,retains own identity/rank/private world stream and free resources with no repeat;existing damage/field/leases/save tests retained. Both explicit world consumers/recovery and natural Warrior selection/release/recovery pass for both skills. Shared approved no-extra localdelay policy admits both intrinsic consumers only;player/item policies unchanged. No old compiler matrices rerun.',
 remaining=['Natural Master selection and remaining unqualified modern missile-range contexts are not certified by Warrior/explicit consumer tests','Fury current callback/rewind behavior and NPC unsigned-critical proposal remain unresolved;do not adopt old epoch-dependent rewind','Modifier42 unique flag and delayed nova now mapped by read-only source review;implementation/compiled/native proof remain next','Generic Shadow stun metadata eligibility is separate from the supplied resolved-duration cancellation control','Learned Shadow Warrior/Master admission,full class certification,exact current executable/PvP and arbitrary callback mutation parity remain unfinished'])
 p=ROOT/'tools/reference/shadow-sentinel-action-native-evidence.json';s=json.dumps(receipt,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Approved own Sentinel/Shock Web S2:24native markers/2reloads;world and natural Warrior AI for both skills pass;208handlers/no class completion;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
