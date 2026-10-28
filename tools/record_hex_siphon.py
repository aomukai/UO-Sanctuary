#!/usr/bin/env python3
"""Bind prepared Siphon and real direct-kill resource consumers, not event36."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/hex-siphon'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
 names=['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log','hex-siphon-fixtures.json','reward-life-check.json','hex-bane-fixtures.json','enchant-checks.json','psychic-ward-fixtures.json','cyclone-armor-fixtures.json','class-skills-checks.json','fissure-fixtures.json','miasma-packet-fixtures.json','levitation-fixtures.json','barbarian-masteries.json','warlock-foundation-fixtures.json','arctic-mode-fixture.json','arctic-stream-fixtures.json','recycler-casting-fixtures.json','recycler-world-fixtures.json','recycler-body-fixtures.json','recycler-recovery-fixtures.json','body-fixtures.json','world-fixtures.json','ground-fixtures.json','poison-fixtures.json','casting-fixtures.json','wolf-world-fixtures.json']
 artifacts=[STAGE/n for n in names]
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==147
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 for n in ['hex-siphon','reward-life','hex-bane','enchant','psychic-ward','cyclone-armor','class-skills','miasma-launch','fissure','miasma-packets','levitation','barbarian-masteries','warlock-foundation']:
  assert log.count(n+'-check: passed')==1 and log.count(n+'-reload-check: passed')==2,n
 assert log.count('Hex Siphon:2295current formula controls;192source buff/560source kill contexts pinned,event36delivery supplied')==3
 for n in ['fissure-live','arctic-mode-live','arctic-stream-live','recycler-recovery-live','recycler-world-live','recycler-casting-live']:assert log.count(n+'-result: passed')==3,n
 saved=json.loads((STAGE/'hex-siphon-fixtures.json').read_text());assert len(saved)==2 and {x['Female'] for x in saved}=={True,False}
 assert all(x['Buff']['Rank']==21 and x['Buff']['Values']['AttackPercent']==200 and x['Buff']['Values']['LifeAfterKill']==x['Buff']['Values']['ManaAfterKill']==23 and x['Buff']['Values']['DebuffFrames']==155 and x['Debuff']['DamagePercent']==-33 for x in saved)
 proof=ROOT/'tools/reference/hex-siphon-source-evidence.json';source=json.loads(proof.read_text());assert source['buffCases']==192 and source['killCases']==560 and source['handlerEnabled']==False;artifacts.append(proof)
 for group in ['inputs','artifacts']:
  for name,digest in source[group].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
 frozen_receipt('c76acb3','tools/reference/hex-bane-native-evidence.json')
 for name,needle in [('map-fixture-diagnostic','No independent fixture position'),('item-fixture-diagnostic','That named definition is not available.')]:
  d=STAGE/name;assert needle in (d/'stats-native-checks.log').read_text();artifacts += list(d.iterdir())
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for name,digest in a['InputHashes'].items():assert sha(ROOT/name)==digest,name
  audits.append(a);artifacts.append(p)
 old=json.loads(subprocess.check_output(['git','show','c76acb3:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions']
 assert [len(a['InputHashes']) for a in audits]==[698,21]
 py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
 artifacts += [ROOT/n for n in ['tests/check_hex_siphon.py','tools/record_hex_siphon.py','tools/probe_hex_siphon.py','tools/build_class_skills.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']]
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert len([s for s in status['skills'] if s['implementation']=='handler-present'])==183 and status['completedClasses']==[]
 siphon=next(s for s in status['skills'] if s['talentId']=='warlock.hexsiphon');assert siphon['implementation']=='blocked' and siphon['verification']=='partial-native-coverage'
 result=dict(referenceVersion='3.3.93847',component='Prepared Hex Siphon enchantment, supplied debuff and direct-kill resources',installed=False,handlerEnabled=False,currentExecutableParity=False,goalComplete=False,
  handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=147,retainedMarkers=141,worldBoots=3,actualWorldReloads=2,completeLegacyBuffContexts=192,completeLegacyKillContexts=560,nativeResourceComparisons=60,nativeFormulaControlsPerBoot=2295,savedWarlockWitchBuffDebuffPairs=2,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[698,21]),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='c76acb3',receipt='tools/reference/hex-bane-native-evidence.json'),
  established=['Prepared Siphon captures effective-rankAR,whole-point life/mana-after-kill,independent hard Engorge/EldritchBlast synergies,buff/debuff duration,fractional mana and a constant-minus33target damagepercent. All255ranks pass2295native hard/soft controls. Rank1returns1life/1mana,costs12.5mana,adds20percentAR,lasts144seconds and supplies a6second debuff. Unused damage-scaling parameters do not replace the actual constant formula.',
   'Pinned itemstatcost uses standard kill callbacks28life/17mana. Complete unchanged routines execute560guard/type/fraction/cap contexts. Native FP health/mana helpers match60admitted player vectors. NamedCombat.Killed now restores item/Siphon points through those existing FP helpers,fixing loss/overflow of the final fraction at maximum while preserving direct-killer and once-only RewardLife eligibility. Positive/zero modifiers remain the admitted native policy;negative signed after-kill behavior is not widened.',
   'Actual direct weapon and spell-damage deaths restore Siphon; dependent kills may credit the owner for loot but restore no owner resources. Suppressed item-event scopes and NoKillAwards deaths give no return; repeated RewardLife.Process gives no duplicate. Actual eligible DeathsWeb/Lidless gear adds item returns and reaches exact caps. Pinned passive stat callbacks have no victim-hex requirement; these controls do not invent such a condition.',
   'Weapon-conditioned AR and item-kill stats reuse shared eligibility. Unequipping retains the captured timer but suppresses benefits and recast;bow remains eligible. A supplied eligible event creates a separate noncurse-minus33damagepercent state in the shared weapon-ED pool. Native AOS weapon damage is reduced to67from100; a physical spell remains100. Weaken adds to the same pool without replacing the hex. Expired buff/target states stop contributing.',
   'Shared WeaponHexes now refuses Bane/Siphon cross-casts and target overwrites in both directions,before payment/state mutation. Modern group6/7replacement is unresolved;no stacking/replacement rule is silently chosen. Bane retained controls pass after the eligibility refactor. No real attack automatically applies Siphon or Bane.',
   'Prepared once-only fractional payment,low-mana preservation,lower-rank refusal,equal-rank changed-Engorge rewrite,death/respec/history clearing and atomic malformed restores pass. Actual trained Warlock/Witch with hard20Siphon,hard2Engorge,hard1EldritchBlast and Tarnhelm retain rank21/23life/23mana/AR200/155debuff frames and separate-minus33target states across both real reloads. Inspected duration/expiry controls precede parked inspection deadlines. Post-load spell/dependent kill consumers pass;SC input is not claimed.',
   'The first native fixture anchor was outside map width;the corrected fixture reuses a known valid area. A later fixture tried unavailable DraculsGrasp;it now uses already eligible DeathsWeb instead. Source/DLLs/logs are retained for both diagnostics. Item eligibility was not loosened;all573decisions remain unchanged. RewardLife,Enchant and previous141markers pass again.'],
  remaining=['Automatic event36target/qualification/order,melee attempt versus successful missile-damage delivery,hextrigger and callback rank/refresh semantics remain gated. Prepared target receiver uses a declared shared-factory rank model,not proof of the modern callback body. Cross-hex groups6/7remain unsupported,with explicit refusal while both prepared components exist.',
   'Modern special kill-event overrides,weapon-conditioned stat details,owner/facet/logout/death/multicaster/PvP and full client/effect behavior remain acceptance work. The older standard kill callbacks establish the admitted arithmetic and stat routes,not current executable equivalence. Source null/monster/overmaximum cases do not expand the admitted native scope;negative modifiers remain unverified.',
   'Learned SC windup/release/payment/input remains disabled;183handlers/Warlock1and no completed class are unchanged. No full regression or installation is claimed. Main installed4c5d3a0and original saves/configuration/client remain preserved;earlier stages remain frozen.',
   'Identified UO cast/debuff flashes/audio remain placeholders. Final hex_enchant,warlock_psychic_cast_2,hexsyphondebuff,curse_hit,life/mana-return overlays,cast/hit/expiry audio and talent/right/quick icons remain separately tracked.'])
 p=ROOT/'tools/reference/hex-siphon-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Prepared Hex Siphon:147native/3boots/2reloads,70Python;direct-kill fractions verified,event36/hex groups/learned cast gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
