#!/usr/bin/env python3
"""Bind native NPC return policy, source RNG/traces and accepted mode consumers."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/monster-return-native'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False,full_class=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
 _,prior=frozen_receipt('16ce299','tools/reference/monster-ordered-release-native-evidence.json');assert prior['verified']['passMarkers']==190 and prior['verified']['fullClassMarkers']==1184
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==194
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 before=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',(ROOT/'.local/class-skills/monster-ordered-release/stats-native-checks.log').read_text()));after=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',log))
 assert sum(before.values())==190 and after-before==Counter({'monster-return-check':1,'monster-return-result':1,'monster-return-reload-check':2}) and not before-after
 marker='NPC return:60488complete-source policies/exact traces,6480typed selections,zero-one-two defender RNG;native Stun-forced GH/typed packet/lethal/deferred entry pass';assert log.count(marker)==1
 assert log.count('NPC return:saved FP life/typed fixture retain result;process-local hit-class cursor and accepted in-flight GH clock reset without replay')==2
 saved=json.loads((STAGE/'focused-fixtures/monster-return-fixtures.json').read_text());assert saved['Life256']==99984 and saved['Packet']['HitClass']==35
 assert [saved['Packet'][k] for k in ['Magic256','Fire256','Lightning256','Cold256']]==[1,2,3,4]
 sourcepath=ROOT/'tools/reference/monster-return-source-evidence.json';source=json.loads(sourcepath.read_text());artifacts.append(sourcepath)
 assert source['variants']['differentCases']==156 and not source['currentExecutableParity']
 for name in ['classic','ubers']:
  v=source['variants'][name];assert v['cases']==30244 and v['classCalls']==3240 and v['recoveryDraws']==[29236,672,336] and v['fractionalFloorDraws']==126
 for group in ['inputs','artifacts']:
  for n,h in source[group].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 for n in ['classic-sanitizer.log','ubers-sanitizer.log']:assert not (ROOT/'.local/class-skills/monster-return'/n).read_bytes()
 monpath=ROOT/'tools/reference/d2r-named-equipment/json/monstats.json';mon=json.loads(monpath.read_text());artifacts.append(monpath)
 assert [mon[n]['*hcIdx'] for n in ['diablo','diabloclone','uberdiablo','sandleaper1']]==[243,333,705,78]
 full=None
 if full_class:
  full=inventory.validate_inventory(STAGE/'class-inputs.json');assert full['artifacts']==inputs['artifacts'] and full['copiedData']==inputs['copiedData']
  text=(STAGE/'class-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==1184 and text.count('Shutting down')==3 and text.count('Writing world save snapshot done')==35
  assert 'including two save/reload cycles.' in (STAGE/'full-run.log').read_text()
  artifacts += [STAGE/n for n in ['class-inputs.json','class-native-checks.log','full-run.log']]
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text()) and a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  if full_class:
   fp=STAGE/('full-'+name);assert a==json.loads(fp.read_text());artifacts.append(fp)
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','16ce299:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions'] and [len(a['InputHashes']) for a in audits]==[717,21]
 py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
 main=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==183 and status['completedClasses']==[]
 for n in ['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log']:artifacts.append(STAGE/n)
 artifacts += list((STAGE/'focused-fixtures').glob('*fixtures.json'))
 for n in ['tools/record_monster_return.py','tests/check_monster_return.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json','tools/reference/creeper-frame-installation-evidence.json']:artifacts.append(ROOT/n)
 result=dict(referenceVersion='3.3.93847',component='Native prepared NPC return/recovery policy and accepted mode consumer',installed=False,handlerEnabled=False,currentExecutableParity=False,fullActorAccepted=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=194,retainedMarkers=190,worldBoots=3,actualWorldReloads=2,completeSourcePolicyContexts=60488,contextsPerFeatureVariant=30244,typedSelections=6480,recoveryRngPerVariant=[29236,672,336],fractionalThresholdDrawCasesPerVariant=126,variantDifferences=156,nativeZeroDamageStunGetHit=True,naturalAcceptedGetHitExpiry=True,acceptedClockReplacement=True,typedChannelsRetained=True,nativeLethalAndDeferredDeath=True,savedLife256=99984,savedLifeRemainder256=112,processLocalHistoryResets=True,fullClassMarkers=1184 if full_class else None,fullClassBoots=3 if full_class else None,fullClassReloads=2 if full_class else None,fullClassSaveSnapshots=35 if full_class else None,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[717,21],unchangedMainInstallationArtifacts=106),inputInventory=inputs,fullInputInventory=full,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='16ce299',receipt='tools/reference/monster-ordered-release-native-evidence.json'),
 established=['Native policy/output and exact service traces match60488complete-source contexts across classic/Uber feature variants. Typed hit class rotates cold/fire/lightning/poison with6480actual selections;the entire final class selects the recovery divisor. Integer FP thresholds and zero/one/two defender RNG draws retain source order,including126fractional-threshold draw controls per variant. Stun bypasses ordinary recovery suppression. Missing-room,town,dead-mode,uninterruptible,block species and sand-leaper ordering remain exact within supplied services.',
  'Prepared packets now retain magic/fire/lightning/cold/poison channels. Return receives the actual packet by reference,so hit-class/result changes survive into the release result and cached native death consumer. The default native entry shares one process-local hit-class cursor,which resets on process restart. Counter overflow is explicitly unsupported rather than assigned invented wrap behavior.',
  'Actual encounter fixtures prove zero-damage Stun forces an accepted GH entry without a recovery reroll or health loss. BeginPreparedGetHit reuses the unchanged native interruption/animation/clock consumer after source mode acceptance;ordinary Apply retains its old selection. A supplied12second clock is accepted and replaced by the source-requested320ms entry,which expires naturally. Typed payload/class history reach the release result;lethal policy consumes native death once while uninterruptible policy sets delay and leaves zero-HP native life awaiting the real actor contract.',
  'Returned99984FP life/remainder112and typed fixture results survive two actual reloads without replay. Process-local cursor and unsaved in-flight GH clock reset. All190predecessor markers remain exact;both builds,70Python tests,573named decisions and106installed artifacts retain their recorded acceptance. Full-class regression is separately recorded when present.'],
 remaining=['This is the complete NPC policy with mandatory room/state/row/base-ID/stat/RNG/mode services,not a functioning Goatman actor or a player return branch. Source mode requests still require actual acceptance,animation/FHR clocks,knockback/block/network/AI consumers,death-delay completion and lifecycle/persistence mapping. The native fixture explicitly accepts GH and supplies its clock;it does not prove every real source mode transition.',
  'Global modern hit-class history across all existing consumers remains unverified. Existing ordinary CreatureHitRecovery and player paths have not been migrated to this source policy;its aggregate chance projection is retained. Real actor mode/metadata/RNG storage,proc/cold/damage-credit/drain/absorb/regeneration and contact gates still need integration.',
  'Modern current helper/dispatcher linkage,extended passives/event16/CrushingBlow/BloodOath/sunder,new Frenzy/Cleave,learned Goatman casting/body/AI and owner/facet/logout/death/respec/history remain unfinished. No installation or class completion is claimed. Final get-hit/Stun/Berserk/body effects/audio and separate talent/hotkey icons remain pending with identified placeholders.'])
 p=ROOT/'tools/reference/monster-return-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('NPC return:194focused/full='+str(result['verified']['fullClassMarkers'])+';60488source policies,native accepted GH/lethal/deferred entry and saved FP life;70Python.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');p.add_argument('--full-class',action='store_true');args=p.parse_args();record(args.check,args.full_class)
