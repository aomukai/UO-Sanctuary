#!/usr/bin/env python3
"""Bind nested Sentinel reuse/birth snapshots and the parked Fury source gap."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from probe_current_item_affixes import function
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-sentinel'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 reused={}
 for name in ['assassin-trap-summon-engine-evidence','blade-sentinel-engine-evidence']:
  p=ROOT/'tools/reference'/f'{name}.json';d=json.loads(p.read_text());texts=[]
  for rel,h in d['sourceHashes'].items():q=BASE/'d2moo-pinned-source'/rel;assert sha(q)==h; raws=q.read_text();texts.append(raws)
  for n,h in d['compiledUnchangedRoutines'].items():
   found=[hashlib.sha256(function(t,n).encode()).hexdigest()for t in texts if re.search(r'^.*\b'+re.escape(n)+r'\([^;\n]*\)\s*\n\{',t,re.M)];assert found==[h],n
  f=d['fixture'];assert sha(ROOT/f['path'])==f['sha256'];reused[name]=dict(receipt=sha(p),fixture=f,routines=d['compiledUnchangedRoutines'])
 proof=json.loads((ROOT/'tools/reference/shadow-fury-callback-source-evidence.json').read_text());assert proof['contexts']==12
 for p,h in (proof['inputs']|proof['artifacts']).items():assert sha(ROOT/p)==h,p
 assert not(BASE/'shadow-fury-callback-source/sanitizer.log').read_text()
 traces=json.loads((BASE/'shadow-fury-callback-source/cases.json').read_text());assert next(t for t in traces if t[:3]==[1,1,1])[3]==26 and next(t for t in traces if t[:3]==[1000,1,1])[3]==12
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==27 and ': failed'not in log and ' ERR]'not in log
 for n in ['shadow-sentinel','blade-spawn','blade-missile','blade-lifecycle']:assert counts[n+'-check']==1 and counts[n+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-2.log').read_text()
 for kind,n in [('content',1),('integration',2)]:assert all(x in(STAGE/f'{kind}-build-{n}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snapshot=json.loads((STAGE/'source-snapshot-3.json').read_text());birth=json.loads((STAGE/'source-snapshot-1.json').read_text());assert native['copiedData']==snapshot['inputs']['copiedData']==birth['inputs']['copiedData']
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snapshot['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 for p,h in birth['inputs']['sourceInputs'].items():
  if p.startswith(('src/','shared/')):assert native['sourceInputs'][p]==h,p
 saved=json.loads((STAGE/'shadow-sentinel-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Blades'])==2
 for b in saved['Blades']:
  s=json.loads(b['State']);assert s['FirstMissile']['Creation']['MissileArmed']and not s['Creation']['MissileArmed']and s['Spawn']['Caster']in saved['Casters']and s['Owner']==saved['Owner']
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(x['implementation']=='handler-present'for x in status['skills'])==208 and status['completedClasses']==[]
 files=[p for pattern in ['source-snapshot-*.json','*-build-*.log','native-run-*.log','birth-*.json','birth-*.log','*-fixtures.json']for p in STAGE.glob(pattern)]
 files += [ROOT/p for p in ['tools/record_shadow_sentinel.py','tools/probe_shadow_fury_callback.py','tools/reference/shadow-fury-callback-source-evidence.json','tests/check_shadow_sentinel.py']]
 files += [BASE/'shadow-fury-callback-source'/n for n in ['probe-run-1.log','probe-run-2.log','compiler-failed-1.log','source-failed-1.cpp']]
 receipt=dict(parent='c11a4d826850c1ee4588ab51815435d9732c8d6b',referenceVersion='3.3.93847',handlers=208,completedClasses=[],learnedShadowsEnabled=False,intrinsicSentinelCastConnected=False,preparedNestedSentinelFactory=True,allNewSentinelBirthWeaponSnapshots=True,approvedRule='Snapshot any summon when summoned,including traps',sourceReuse=reused,newComposedFurySourceContexts=12,nativeMarkers=27,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=2,sourceSnapshot=3,artifacts=native['artifacts'],files={str(p.relative_to(ROOT)):sha(p)for p in sorted(set(files))},preservation=preservation(),
 diagnostic='Fury callback harness first needed its shared shot-vector declaration moved before the factory service and ANIMSEQ_EVENT_NONE supplied;failed compiler/source retained. New12contexts expose older pose-as-epoch rewind:an identical successful first shot ends at26frames with epoch1 but12with epoch1000. That unverified automatic action stays parked. Sentinel first native fixture unintentionally inherited every rank1talent from ClassSkillChecks.Player;fixture now resets allocated points before training only Shadow prerequisites,proving root Sentinel rank0 with own NPC rank. No further production fix was needed;final native2 passes.',
 scope='Actual original seed1 Warrior/Master create prepared Sentinel with own intrinsic rank/current level/origin;shared old summon factory and AI resolve root player for base/passives/weapon/roster. No owner/Shadow mana or Shadow attack RNG consumed. Existing root player critical policy is reused and not replaced by pending NPC unsigned proposal. New Sentinel births freeze root first-missile weapon and AR at creation under explicit summon snapshot rule;once-only arming consumes that saved packet at first AI. Optional fields preserve old/synthetic fallback. Pre-arm difficulty rescale preserves weapon packet while updating deferred base stats. Original pending birth packets survive2reloads while root removes its weapon. Retained live player spawn,following missile and lifecycle tests pass. No old compiler matrices rerun.',
 remaining=['Own Sentinel S2 startup/release/AI and current localdelay25 versus older NPC executor policy need separate admission','Fury callback/rewind current behavior remains unsupported;old composed model is game-epoch-dependent,not proof of modern D2R mechanics','NPC unsigned critical proposal and earlier Shock Web localdelay15 question remain nonblocking','Root/caster different-level end-to-end native factory case,arbitrary callback/stat mutation,current executable/PvP and full Shadow certification remain unverified','Death modifier42 and learned Shadow admission still pending'])
 p=ROOT/'tools/reference/shadow-sentinel-native-evidence.json';s=json.dumps(receipt,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Prepared Shadow Sentinel/root birth snapshot:27native markers/2reloads;Fury12composed controls flag unsupported rewind;208handlers/no class completion;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
