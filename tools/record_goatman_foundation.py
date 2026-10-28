#!/usr/bin/env python3
"""Bind Goatman birth preparation, current tooltip and bounded source routines."""
import argparse,ast,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/goatman-foundation'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
 old=ROOT/'tools/record_hex_siphon.py';node=next(n.value for n in ast.walk(ast.parse(old.read_text())) if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='names' for t in n.targets));names=ast.literal_eval(node)+['hex-purge-fixtures.json','burst-speed-fixtures.json','cleave-fixtures.json','mirrored-blades-fixtures.json','goatman-foundation-fixtures.json'];artifacts=[STAGE/n for n in names]+[old,STAGE/'fixture-copy-diagnostic.log']
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==165
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 assert log.count('goatman-foundation-check: passed')==1 and log.count('goatman-foundation-reload-check: passed')==2
 assert log.count('Goatman:30600raw contexts,3570Fog/7650base/3456do56source controls;current user tooltip matches,GenericPet and learned actor remain gated')==3
 saved=json.loads((STAGE/'goatman-foundation-fixtures.json').read_text());assert len(saved)==2 and {x['Female'] for x in saved}=={True,False}
 for s in saved:
  b=s['Birth'];v=b['Suppliers'];assert (b['OwnerLevel'],b['PetLevel'],b['BaseLife256'],b['MaximumLife256'],b['MinimumDamage256'],b['MaximumDamage256'])==(47,37,44800,161280,24499,39347)
  assert (v['Rank'],v['AttackSpeed'],v['AttackBonus'],v['DisplayAttackBonus'],v['DefenseBonus'],v['DamagePercent'],v['FlatDamage'],v['PetMaximum'],v['Mana256'])==(2,32,605,505,120,190,3,3,3968)
 proof=ROOT/'tools/reference/goatman-source-evidence.json';source=json.loads(proof.read_text());assert (source['formulaCases'],source['baseStatCases'],source['creationCases'])==(3570,7650,3456) and not source['handlerEnabled'] and not source['currentExecutableParity'];artifacts.append(proof)
 assert not source['reviewedPhysicalRoutine']['executedHere'] and 'alignment is excluded only' in source['sanitizers']
 for group in ['inputs','artifacts']:
  for name,digest in source[group].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
 obs=ROOT/'tools/reference/goatman-tooltip-observation.json';copy=ROOT/'.local/class-skills/goatman-tooltip-observation.json';assert sha(obs)==sha(copy);artifacts += [obs,copy]
 o=json.loads(obs.read_text());assert o['ownerLevel']==47 and o['difficulty']=='Nightmare' and o['tooltip']==dict(life=752,minimumDamage=114,maximumDamage=176,attackSpeedPercent=34,attackRatingBonus=585,defenseBonus=160,manaCost=16.5)
 frozen_receipt('68573f0','tools/reference/mirrored-blades-native-evidence.json')
 previous=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',(ROOT/'.local/class-skills/mirrored-blades/stats-native-checks.log').read_text()));current=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',log))
 assert sum(previous.values())==162 and current-previous==Counter({'goatman-foundation-check':1,'goatman-foundation-reload-check':2}) and not previous-current
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for name,digest in a['InputHashes'].items():assert sha(ROOT/name)==digest,name
  audits.append(a);artifacts.append(p)
 previous=json.loads(subprocess.check_output(['git','show','68573f0:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==previous['Definitions'] and [len(a['InputHashes']) for a in audits]==[706,21]
 py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
 main=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(main['installedArtifacts'])==106
 for name,digest in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/name)==digest,name
 artifacts += [ROOT/n for n in ['tests/check_goatman_foundation.py','tools/record_goatman_foundation.py','tools/probe_goatman_foundation.py','tools/build_class_skills.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json','tools/reference/creeper-frame-installation-evidence.json']]
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert len([s for s in status['skills'] if s['implementation']=='handler-present'])==183 and status['completedClasses']==[]
 skill=next(s for s in status['skills'] if s['talentId']=='warlock.summongoatman');assert skill['implementation']=='blocked' and skill['verification']=='partial-native-coverage'
 result=dict(referenceVersion='3.3.93847',component='Goatman prepared birth/source projection with current user tooltip',installed=False,handlerEnabled=False,currentExecutableParity=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],
  verified=dict(passMarkers=165,retainedMarkers=162,worldBoots=3,actualWorldReloads=2,completeFogContexts=3570,completeBaseStatContexts=7650,completeDo56Contexts=3456,nativeRawContextsPerBoot=30600,threeDifficultyProjectionsPerBoot=91800,expandedBaseStatComparisonsPerBoot=3825,currentUserTooltipFields=7,savedWarlockWitchActorSupplierSets=2,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[706,21],unchangedMainInstallationArtifacts=106),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='68573f0',receipt='tools/reference/mirrored-blades-native-evidence.json'),
  established=['Exact current unclosed Goatman passivecalc3and a separately balanced control run3570complete unchanged Fog compiler/evaluator contexts. Both give10*(Goatman rank+DemonicMastery rank),with one nested callback. A narrowly matched SkillValuesadapter now evaluates this form while preserving the original source string. The generic parser remains strict;no global auto-close or change to unclosed stat-call behavior is introduced. Current user damage independently corroborates the prepared result at rank4/mastery17.',
   'Complete unchanged shared do56Golem runs3456nonnull-owner guard/allocation/pettype/mode/cap contexts with declared allocation/passive/resist/client/AI services. Complete base stats runs7650rank/owner/difficulty/expansion contexts;3825expansion vectors match native pet-level/level-AR/level-AC additions per boot. Old passive writer has5slots while current Goatman uses12,so the do56proof does not certify the modern writer or GenericPet AI.',
   'Prepared immutable birth captures current mana/localdelay,HPpercent,speed,velocity,raw tohit/defense/ED/flat damage,hard-only shared petmax,physical resistance bonus,Crushing Blow and NPC unlock ranks. Duplicate physical-sunder slots5/7remain separate supplied fields with no aggregation rule. Independent mastery effective/hard contexts prevent soft points from raising the cap. No passive/callback/AI is automatically installed on a world pet.',
   'Current user Nightmare level47with Goatman4/1,Mastery17/16and BloodOath6/5matches192640FP life=752.5(display752),physical endpoints29363..45235FP(display114..176),speed34,display AR585,defensebonus160and4224FPmana=16.5. The underlying passivecalc2supplier is685rather than585;the tooltip uses a different formula. Ordinary no-item/no-strength endpoints add flat7before210percentED. This is a stat/tooltip projection,not a real hit or critical/skill-mode observation.',
   'Actual trained Warlock/Witch use admitted Tarnhelm gear with hard1Goatman/hard16Mastery/hard5Oath. The item yields effective2Goatman/17Mastery/6Oath,not the users additional Goatman staffmods. Supplied Nightmare projections retain630life,24499..39347FPphysical endpoints,speed32,rawAR605/display505,defensebonus120,mana15.5and hard-only cap3across two actual world reloads. Only owners/gear/inspection DTOs persist;no cast is paid,pet spawned or in-flight skill claimed.',
   'Initial packed Fog bytecode alignment and invalid null-owner contexts are retained as diagnostics. Only Fog alignment checking is excluded for its unchanged packed casts;other ASan/UBSan checks remain enabled. The final shared-cast matrix admits nonnull owners. A test harness nested-file copy failure occurred before server startup;the observation fixture now uses a flat unique name. Neither source bodies nor production caller guards were weakened.',
   'All162earlier native marker counts are retained exactly. Both builds are clean,all70Python tests pass,all573named-item eligibility decisions are unchanged and106installed artifacts remain byte-identical. Main4c5d3a0,original saves/configuration/client and frozen predecessor stages remain preserved.'],
  remaining=['Learned SC/right/quick casting,world actor/allocation/placement,shared demon pool replacement and GenericPet AI remain unfinished. NPC Stun/Berserk/Frenzy/Cleave unlock at effective2/3/4/5;new Frenzy start67and Cleave start68/do164are separate source gaps. Do not invent AI parameter meanings or omit unlocked skills while calling the summon complete.',
   'Modern extended passive writer,duplicate sunder aggregation,event16ordinary-hit reaction,passive BloodOath/resistance sharing,regen/base creation/randomization and full NPC skill/critical/aura/damage/stat/event order remain unverified. The prepared raw fields and no-item/no-strength damage projection do not establish these services.',
   'Owner difficulty-transition percentage preservation,facet/logout/death/respec/history persistence,pet pool lifecycle,modern placement/room/target and current-client acceptance remain required for the eventual actor. No learned handler,class completion,full regression or installation is claimed;183handlers/Warlock1remain unchanged.',
   'Prepared suppliers create no body or visual. Final Khazra body/equipment/animations,warlock_shadow_cast_2,Warlock_Skill_Cast_Khazra_03,Stun/Berserk/Frenzy/Cleave effects/contact/audio and talent/right/quick icons remain separate asset work.'])
 p=ROOT/'tools/reference/goatman-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Goatman prepared:165native/3boots/2reloads,70Python;current tooltip/source birth verified,GenericPet/extended passives/learned actor remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
