#!/usr/bin/env python3
"""Record own NPC Talon action/contact/AI/persistence qualification."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from probe_current_item_affixes import function
from record_shadow_kick_packets import source as packet_source
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-talon'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 proof=json.loads((ROOT/'tools/reference/shadow-talon-source-evidence.json').read_text());assert proof['contexts']==20 and len(proof['completeRoutineHashes'])==4
 for p,h in (proof['inputs']|proof['artifacts']).items():assert sha(ROOT/p)==h,p
 assert not(BASE/'shadow-talon-source/sanitizer.log').read_text()
 old=packet_source();assert len(old['reused']['routines'])==17 and old['newRewindControls']==46
 units=(BASE/'d2moo-pinned-source/source/D2Common/src/Units/Units.cpp').read_text()
 eligibility=function(units,'UNITS_CanAnimModeUseAttackRate');clock=json.loads((ROOT/'tools/reference/dragon-talon-clock-source-evidence.json').read_text())
 eligibleHash=hashlib.sha256(eligibility.encode()).hexdigest();assert clock['reviewedOlderRoutineHashes']['UNITS_CanAnimModeUseAttackRate']==eligibleHash
 modes=re.search(r'gaPlayerModesAnimModulators\[NUMBER_OF_PLRMODES\] = \{(.*?)\};',units,re.S).group(1)
 entries=[[int(v.strip())for v in m.split(',')]for m in re.findall(r'\{([^{}]+)\}',modes)];assert entries[12]==[0,0,0,1,0]
 row=next(s for s in json.loads((ROOT/'data/Sanctuary/Balance/item-skill-execution.json').read_text())['Skills']if s['Id']==255);assert row['Fields'].get('UseAttackRate',0)==0
 assert all(row['Fields'].get(k,0)==0 for k in ['passive','aura','periodic'])
 monster=(BASE/'d2moo-pinned-source/source/D2Game/src/MONSTER/MonsterMode.cpp').read_text();skills=(BASE/'d2moo-pinned-source/source/D2Game/src/SKILLS/Skills.cpp').read_text()
 reviewed={name:hashlib.sha256(function(text,name).encode()).hexdigest()for text,name in [(monster,'sub_6FC64790'),(skills,'sub_6FD13410'),(skills,'sub_6FD13260'),(units,'UNITS_StopSequence')]}
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==24 and ': failed'not in log and ' ERR]'not in log
 for family in ['shadow-talon','shadow-kick-packets','shadow-dragon-claw','shadow-ice','shadow-sentry']:assert counts[family+'-check']==1 and counts[family+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-3.log').read_text()
 for kind,n in [('content',2),('integration',3)]:assert all(x in(STAGE/f'{kind}-build-{n}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snapshot=json.loads((STAGE/'source-snapshot-4.json').read_text())
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snapshot['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 built=json.loads((STAGE/'source-snapshot-3.json').read_text())
 assert {p:h for p,h in snapshot['inputs']['sourceInputs'].items()if not p.startswith('tests/')}=={p:h for p,h in built['inputs']['sourceInputs'].items()if not p.startswith('tests/')}
 assert snapshot['inputs']['copiedData']==built['inputs']['copiedData']
 saved=json.loads((STAGE/'shadow-talon-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Bodies'])==2 and all(b['Charges']for b in saved['Bodies'])
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(x['implementation']=='handler-present'for x in status['skills'])==208 and status['completedClasses']==[]
 files=[STAGE/n for n in ['source-snapshot-3.json','source-snapshot-4.json','birth-inputs.json','birth-native-checks.log','native-run-3.log','content-build-2.log','integration-build-3.log','shadow-talon-fixtures.json','birth-inputs-failed-1.json','birth-native-checks-failed-1.log','native-run-1.log','birth-inputs-failed-2.json','birth-native-checks-failed-2.log','native-run-2.log']]
 files += [ROOT/p for p in ['tools/record_shadow_talon.py','tools/probe_shadow_talon.py','tools/reference/shadow-talon-source-evidence.json','tests/check_shadow_talon.py','docs/dragon-talon-approved-action-rule.md']]
 receipt=dict(parent='e24f42f356ce8e609bf10980f782b7b2f2d0dd61',referenceVersion='3.3.93847',handlers=208,assassinHandlers=28,completedClasses=[],learnedShadowsEnabled=False,intrinsicTalonConnected=True,
  composedSourceControls=20,completeComposedRoutines=proof['completeRoutineHashes'],retainedRewindControls=46,reusedPacketSource=old,kickAttackRateReview=dict(routineHash=eligibleHash,convertedPlayerMode=12,modeRow=entries[12],optionalSkillFlag=0),nativeMarkers=24,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=3,sourceSnapshot=4,contentUnchangedSinceSnapshot=3,
  artifacts=native['artifacts'],files={str(p.relative_to(ROOT)):sha(p)for p in files},preservation=preservation(),ordinaryCallbackReview=dict(routineHashes=reviewed,scope='Reviewed complete NPC callback:nonnull normal used-skill flags0 calls generic execution once;ordinary nonsequence StopSequence clears action marker only. Wrapper periodic follow-up is a no-op for this nonpassive/nonaura row. Other skillflags and current executable parity are unqualified;not a new compiled matrix.'),
  diagnostic='Initial native preflight refused the row because a new guard incorrectly required UseAttackRate1. The actual pinned row has no flag;the transformed player KK mode independently admits attack rate. Guard corrected to0 against source/data. The second native run passed functionality but its reload fixture attempted to reactivate an already-restored ground profile;fixture now retains it. Both failed logs/inputs retained. Production content is unchanged since snapshot3;both final builds are clean.',
  scope='Both original Shadows:own NPC A1->KK/start24 cached full packet,do42 immediate second packet then percent-rewound follow-ups/final recovery,source hand/rate/own RNG and no mana debit. Approved accuracy-only charged guarantee with shield retention and per-successful-contact consume-one,all-six shared callback order/effects,live charge expiry with cached inline damage retained,no unused inline RNG,normal final knockback,world request/full occupancy,natural Warrior AI,target death/escape/interruption/offline/park/item change/no replay and exact original saved actor/items/resources/wounds/RNG/charges. Retained packet,Dragon Claw,Ice/all-six and all five shared-placement sentry consumers pass.17 prior packet routines authenticated without rerunning matrices;20new four-routine composed controls plus46retained complete ordinary rewind cases.',
  limits=['Modern exact NPC input/rate/stat mutation/charge/hand/event/geometry/PvP/removal behavior remains scoped. Approved per-successful-kick charge policy replaces obsolete all-charge consumption and applies to follow-up contacts;this is not current executable proof.','Natural Warrior selection and both-species direct world requests are checked;unrestricted natural Master skill selection/loadouts retain their existing gaps.','Other Dragon Tail/Flight/Fury/Sentinel/current death/learned Shadow dependencies and separate player mixed-finisher audit remain.','Existing final kick/charge/elemental effect/audio and talent/hotkey icon placeholders remain separately inventoried.'])
 out=ROOT/'tools/reference/shadow-talon-native-evidence.json'
 if check:assert json.loads(out.read_text())==receipt;print('Own NPC Talon action receipt matches.')
 else:out.write_text(json.dumps(receipt,indent=2)+'\n');print('Recorded own NPC Talon:24 native markers/two reloads,20new composed controls/46retained rewind cases;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
