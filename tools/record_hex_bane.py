#!/usr/bin/env python3
"""Bind Bane's prepared native consumers without certifying modern event36."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/hex-bane'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
 names=['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log','python-tests-before-audit.log','hex-bane-fixtures.json','enchant-checks.json','psychic-ward-fixtures.json','cyclone-armor-fixtures.json','class-skills-checks.json','fissure-fixtures.json','miasma-packet-fixtures.json','levitation-fixtures.json','barbarian-masteries.json','warlock-foundation-fixtures.json','arctic-mode-fixture.json','arctic-stream-fixtures.json','recycler-casting-fixtures.json','recycler-world-fixtures.json','recycler-body-fixtures.json','recycler-recovery-fixtures.json','body-fixtures.json','world-fixtures.json','ground-fixtures.json','poison-fixtures.json','casting-fixtures.json','wolf-world-fixtures.json']
 artifacts=[STAGE/n for n in names]
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==141
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 for n in ['hex-bane','enchant','psychic-ward','cyclone-armor','class-skills','miasma-launch','fissure','miasma-packets','levitation','barbarian-masteries','warlock-foundation']:
  assert log.count(n+'-check: passed')==1 and log.count(n+'-reload-check: passed')==2,n
 assert log.count('Hex Bane:6120current hard-synergy/whole-point controls;192source do23contexts pinned, event36delivery supplied')==3
 for n in ['fissure-live','arctic-mode-live','arctic-stream-live','recycler-recovery-live','recycler-world-live','recycler-casting-live']:assert log.count(n+'-result: passed')==3,n
 saved=json.loads((STAGE/'hex-bane-fixtures.json').read_text());assert len(saved)==2 and {x['Female'] for x in saved}=={True,False}
 assert all(x['Buff']['Rank']==21 and x['Buff']['Values']['AttackPercent']==200 and x['Buff']['Values']['DebuffFrames']==155 and x['Debuff']['DefensePercent']==-50 and x['Debuff']['AttackPercent']==-25 for x in saved)
 proof=ROOT/'tools/reference/hex-bane-source-evidence.json';source=json.loads(proof.read_text());assert source['cases']==192 and source['handlerEnabled']==False;artifacts.append(proof)
 for group in ['inputs','artifacts']:
  for name,digest in source[group].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
 frozen_receipt('eab6456','tools/reference/psychic-ward-native-evidence.json')
 diag=STAGE/'history-fixture-diagnostic';assert 'History retained Bane' in (diag/'stats-native-checks.log').read_text();artifacts += list(diag.iterdir())
 stale=(STAGE/'python-tests-before-audit.log').read_text();assert 'Stale named runtime evidence' in stale and 'Stale runtime/input evidence' in stale
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for name,digest in a['InputHashes'].items():assert sha(ROOT/name)==digest,name
  audits.append(a);artifacts.append(p)
 old=json.loads(subprocess.check_output(['git','show','eab6456:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions']
 assert [len(a['InputHashes']) for a in audits]==[696,21]
 py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
 artifacts += [ROOT/n for n in ['tests/check_hex_bane.py','tools/record_hex_bane.py','tools/probe_hex_bane.py','tools/build_class_skills.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']]
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert len([s for s in status['skills'] if s['implementation']=='handler-present'])==183 and status['completedClasses']==[]
 bane=next(s for s in status['skills'] if s['talentId']=='warlock.hexbane');assert bane['implementation']=='blocked' and bane['verification']=='partial-native-coverage'
 result=dict(referenceVersion='3.3.93847',component='Prepared Hex Bane enchantment and supplied noncurse debuff',installed=False,handlerEnabled=False,currentExecutableParity=False,goalComplete=False,
  handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=141,retainedMarkers=135,worldBoots=3,actualWorldReloads=2,completeLegacyBuffContexts=192,nativeFormulaControlsPerBoot=6120,savedWarlockWitchBuffDebuffPairs=2,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[696,21]),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='eab6456',receipt='tools/reference/psychic-ward-native-evidence.json'),
  established=['Prepared Hex Bane stores whole-point magic minimum/maximum,AR,buff/debuff lengths and fractional mana using pinned385fields. All255ranks with independent hard Consume/HexPurge/MirroredBlades/EldritchBlast contexts and noisy soft ranks pass6120native controls. Damage uses35percent per point from Param10,not unused Param8=50;rank1magic4-8,mana6,buff144seconds,debuff6seconds,AR20,defense-minus30,targetARminus25.',
   'Native equipped-weapon damage and additive AR consume the captured enchantment. Actual AOS melee/ranged and shared weapon receivers receive magic once; ordinary prepared weapon strike doubles only physical on critical. Removing the weapon keeps the timed state but suppresses weapon-conditioned benefits and prevents recasting. A bow remains eligible. Current h2h casting exclusion is explicit. No random draw is added by Bane on inactive weapon-damage paths. Unsupported nonzero magic mastery is refused at capture and contribution.',
   'Supplied eligible reactions create a separate noncurse debuff. Target defense/AR enter existing pools,coexisting with ordinary curses. Ineligible input does nothing. This prepared receiver uses a declared shared-factory rank/refresh model; it does not establish how modern event36calls or alters that model. Automatic real attacks do not apply a debuff in this package.',
   'Complete shared do23/factory/passive writer execute192source contexts with clean ASan/UBSan. Lower same-skill rank rejects,equal refreshes expiry and rewrites passive values, higher replaces; three descriptors register36with captured rank. Native once-only prepared fractional payment,low-mana preservation,lower-rank refusal,equal-rank changed-synergy rewrite,expiry eligibility,death/respec/history clearing and atomic malformed restore pass. SC/input payment timing is not inferred from this direct prepared entry.',
   'Actual trained Warlock/Witch with20hard Bane,one-point hard damage/length synergies and Tarnhelm retain captured rank21/AR200/target-defense-minus50/targetARminus25 across both real reloads. Inspected source durations/expiry are checked before fixture deadlines are parked for the long retained suite. Saved exact values,actors and real gear remain stable;post-load native damage consumers pass.',
   'The first lifecycle fixture left its sword equipped,so class-history switching correctly refused. The fixture now removes it before requesting a switch;the diagnostic source/DLLs/logs are retained. Python checks initially rejected the previous item-audit fingerprints,then all70pass after fresh candidate exports. Fresh696/21fingerprints preserve all573named decisions. Enchant and the retained135checks pass unchanged.'],
  remaining=['New event36body,actual melee-attempt/miss versus successful missile-damage delivery,hextrigger overrides,target filtering/ordering and actual callback rank/refresh semantics remain unverified. Primary user-supplied research reports a distinction between melee and missiles,but its exact executable build is unstated. Supplied receiver tests are not automatic combat acceptance.',
   'Current groups6and7contain the three weapon hexes and their enemy debuffs;none are curse-mask states. The older do23/factory does not establish current cross-hex group replacement. User has no Hex trained and uses summons,sigils and BladeWarp prerequisites;no points/respec requested. Do not invent cross-hex coexistence or replacement.',
   'Nonzero modern magic mastery/double-dip and complete owner/death/logout/facet/multicaster/PvP/other-skill override behavior remain separate. Current prepared native lifecycle follows existing Sanctuary buff conventions without claiming modern equivalence. Learned SC/windup/release/input and full client acceptance remain gated. No class certificate,full regression or installation is claimed.',
   'Identified UO shield/debuff flashes and cast audio are placeholders. Final hex_enchant,warlock_psychic_cast_2,hexbanedebuff,curse_hit,hit/expiry/audio and talent/right/quick icons remain separately tracked. Main installed4c5d3a0and original saves/configuration/client remain preserved;earlier stages remain frozen.'])
 p=ROOT/'tools/reference/hex-bane-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Prepared Hex Bane:141native/3boots/2reloads,70Python;183handlers unchanged,event36/hex groups/learned cast remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
