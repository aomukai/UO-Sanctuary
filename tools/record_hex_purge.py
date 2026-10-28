#!/usr/bin/env python3
"""Bind Purge passive timing and explicit plans without certifying explosions."""
import argparse,ast,hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/hex-purge'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
 old=ROOT/'tools/record_hex_siphon.py';node=next(n.value for n in ast.walk(ast.parse(old.read_text())) if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='names' for t in n.targets));names=ast.literal_eval(node)+['hex-purge-fixtures.json','burst-speed-fixtures.json'];artifacts=[STAGE/n for n in names]+[old]
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==156
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 for n in ['hex-purge','burst-speed','hex-siphon','reward-life','hex-bane','enchant','psychic-ward','cyclone-armor','class-skills','miasma-launch','fissure','miasma-packets','levitation','barbarian-masteries','warlock-foundation']:
  assert log.count(n+'-check: passed')==1 and log.count(n+'-reload-check: passed')==2,n
 assert log.count('burst-speed-result: passed')==1 and log.count('burst-speed-reload-result: passed')==2
 assert log.count('Hex Purge:6885current supplier controls,192source buff contexts;raw chance/charges/radius remain separate from modern event36/39')==3
 for n in ['fissure-live','arctic-mode-live','arctic-stream-live','recycler-recovery-live','recycler-world-live','recycler-casting-live']:assert log.count(n+'-result: passed')==3,n
 saved=json.loads((STAGE/'hex-purge-fixtures.json').read_text());assert len(saved)==2 and {x['Female'] for x in saved}=={True,False}
 assert all(x['Buff']['Rank']==21 and x['Buff']['Values']['AttackRate']==30 and x['Buff']['Values']['AttackPercent']==200 and x['TargetPlan']['Plan']==dict(Rank=21,Minimum256=46694,Maximum256=60518,DebuffFrames=155,ChanceRaw=80,InitialCharges=3,RadiusRaw=4) for x in saved)
 proof=ROOT/'tools/reference/hex-purge-source-evidence.json';source=json.loads(proof.read_text());assert source['buffCases']==192 and source['handlerEnabled']==False;artifacts.append(proof)
 for group in ['inputs','artifacts']:
  for name,digest in source[group].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
 inspection=source['currentFileInspection'];assert inspection['callbackIdentitiesVerified']==inspection['modernBodiesExecuted']==inspection['processMemoryRead']==inspection['processInput']==False and len(inspection['candidateSlots'])==6 and all(x['badInstructionMarkers']>0 and not x['meaningVerified'] for x in inspection['candidateSlots'])
 frozen_receipt('d63ae03','tools/reference/hex-siphon-native-evidence.json')
 obs=ROOT/'tools/reference/levitation-ethereal-battlebranch-observation.json';o=json.loads(obs.read_text());assert o['reportedLevitationHardRank']==1 and o['reportedLevitationEffectiveRank']==2 and o['reportedStrengthRequirement']==50 and o['sourceBaseCode']=='pax' and o['sourceBaseStrength']==62 and not o['mechanicalChangeApplied'];assert o['candidateControls']==dict(levitationPercent=-4,percentBeforeEthereal=50,etherealBeforePercent=50)
 for name,digest in o['sourceHashes'].items():assert sha(ROOT/name)==digest,name
 artifacts.append(obs)
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for name,digest in a['InputHashes'].items():assert sha(ROOT/name)==digest,name
  audits.append(a);artifacts.append(p)
 previous=json.loads(subprocess.check_output(['git','show','d63ae03:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==previous['Definitions'] and [len(a['InputHashes']) for a in audits]==[699,21]
 py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
 artifacts += [ROOT/n for n in ['tests/check_hex_purge.py','tools/record_hex_purge.py','tools/probe_hex_purge.py','tools/build_class_skills.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']]
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert len([s for s in status['skills'] if s['implementation']=='handler-present'])==183 and status['completedClasses']==[]
 purge=next(s for s in status['skills'] if s['talentId']=='warlock.hexpurge');assert purge['implementation']=='blocked' and purge['verification']=='partial-native-coverage'
 result=dict(referenceVersion='3.3.93847',component='Prepared Hex Purge skill-rate/AR buff and explicit raw target plans',installed=False,handlerEnabled=False,currentExecutableParity=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],
  verified=dict(passMarkers=156,retainedMarkers=147,worldBoots=3,actualWorldReloads=2,completeLegacyBuffContexts=192,nativeFormulaControlsPerBoot=6885,savedWarlockWitchBuffPlanPairs=2,burstSpeedChecks=6,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[699,21]),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='d63ae03',receipt='tools/reference/hex-siphon-native-evidence.json'),
  established=['Prepared Purge captures effective-rank attackrate andAR,buff length and mana. Rank1speed10/AR20/cost25mana/buff144seconds;speed caps30at rank21. The common skill-rate aggregator reaches all six existing owner composition sites separately from itemIAS/FCR. Native direct/sequence clocks,real Twitchthroe/Magefist,melee/bow/throw/twohand weapons,SC and movement exclusion pass. Original Warlock base clocks remain provisional;these checks establish modifier placement,not exact current frames.',
   'Raw unmastered suppliers pass6885all-rank independent hard Bane/EldritchBlast/SigilDeath controls. Initial charges1/2/3at ranks1/10/20;raw radius4has no unused Param8growth;raw chance floors to69at ranks1/2and70at3,and remains216at rank255with20hardSigilDeath. No chance clamp,geometric unit,roll,capture instant or depletion order is invented. Nonzero modern magic mastery is refused when requesting/storing an owner explosion plan;the independent passive buff has no magic supplier.',
   'Complete shared legacy do23/factory/passive writer execute192contexts and retain attackrate-to-other_animrate mirroring plus captured event descriptors. Current melee descriptor is domeleedamage. The new36/39bodies are not certified. Explicitly supplied eligible target plans persist without damage,mana debit,automatic application,detonation or consumption. Reapplication/charge reset and all cross-hex combinations are explicitly refused before mutation.',
   'Native fractional low-mana checks,once-only prepared payment,lower/equal-rank recast,weapon-conditioned contributions,bow/throw/twohand compatibility,expiry eligibility,death/respec/history clearing and atomic malformed restore pass. Actual trained Warlock/Witch with hard20Purge,Tarnhelm and hard1Bane/Blast save rank21/speed30/AR200and explicitly supplied plans46694..60518FP/155frames/raw chance80/3initial charges/raw radius4. Later hardSigil2makes the live supplier81without changing the stored plan80. Both actual reloads retain exact actors/gear/payload;inspection deadlines are parked after duration/expiry controls.',
   'The pinned executable SHA matches. A read-only PE pointer-run/PData investigation finds six unverified candidate extents whose on-disk bytes decode with invalid instructions. No dispatcher link or callback semantics are established;no process memory or input is accessed. This is an explicitly recorded lead,not modern executable proof.',
   'User now reports an ethereal Battlebranch/Poleaxe,Strength50at hard1/effective2Levitation. Pinned baseStrength62and a4percent reduction give50under both principal ethereal-order candidates. The observation is saved separately and supersedes the earlier no-example availability note;order/ease stacking remains unresolved and no mechanic gate is changed. All573named-item eligibility decisions remain unchanged.'],
  remaining=['Automatic event36qualification/target/rank behavior and event39explosion application,geometry,targets,damage/mastery,roll/charges/reapplication and source32special overrides remain unsupported. Target plans are inert preparation,not exploding combat actors. Modern cross-hex groups6/7remain unverified and refused.',
   'Warlock exact base animation/FCR/current input/mode/stat/seed/client and complete owner/PvP/lifecycle acceptance remain separate. The legacy other_animrate mirror does not certify modern other-mode behavior. Learned SC/right/quick input is disabled;no completed class,full regression or installation is claimed. Main installed4c5d3a0and original saves/configuration/client and previous accepted stages remain preserved.',
   'Identified UO flashes/cast audio remain placeholders. Final hex_enchant,warlock_psychic_cast_2,hexpurgedebuff,curse_hit,hex_purge_explosion,charge/depletion/expiry/cast/hit audio and talent/right/quick icons remain separately tracked.'])
 p=ROOT/'tools/reference/hex-purge-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Prepared Hex Purge:156native/3boots/2reloads,70Python;buff timing verified,explosions/current PE lead/learned casting remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
