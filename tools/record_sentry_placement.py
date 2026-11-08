#!/usr/bin/env python3
"""Record the user-approved shared bounded sentry placement rule."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/sentry-placement'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==21 and ': failed'not in log and ' ERR]'not in log
 for family in ['sentry-placement','shadow-sentry','sentry-cast']:assert counts[family+'-check']==1 and counts[family+'-reload-check']==2
 assert counts['sentry-learned-mechanics-check']==3 and 'pass across3boots/2reloads'in(STAGE/'native-run-2.log').read_text()
 for kind in ['content','integration']:assert all(x in(STAGE/f'{kind}-build-2.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snapshot=json.loads((STAGE/'source-snapshot-3.json').read_text())
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snapshot['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 saved=json.loads((STAGE/'sentry-placement-fixtures.json').read_text());assert saved['Generation']==2 and saved['Mana']==127 and len(saved['Traps'])==5 and all(t['Life']==12850 for t in saved['Traps'])
 assert len({t['State']['Creation']['Ability']for t in saved['Traps']})==5
 shadow=json.loads((STAGE/'shadow-sentry-fixtures.json').read_text());assert shadow['Generation']==2 and len(shadow['Bodies'])==2 and len(shadow['Traps'])==5
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(x['implementation']=='handler-present'for x in status['skills'])==208 and status['completedClasses']==[]
 proposal=ROOT/'docs/sentry-occupied-placement-proposal.md';assert 'Approved by the user,2026-11-08'in proposal.read_text()
 files=[STAGE/n for n in ['source-snapshot-3.json','birth-inputs.json','birth-native-checks.log','native-run-2.log','content-build-2.log','integration-build-2.log','sentry-placement-fixtures.json','shadow-sentry-fixtures.json','sentry-cast-fixture.json','birth-inputs-failed-1.json','birth-native-checks-failed-1.log','native-run-1.log']]
 files += [ROOT/'tools/record_sentry_placement.py',ROOT/'tests/check_sentry_placement.py',proposal]
 receipt=dict(parent='39f18f6b9bd5415ece66a5d5f57c5dc8ccfc9e17',referenceVersion='3.3.93847',handlers=208,assassinHandlers=28,completedClasses=[],learnedShadowsEnabled=False,
  approvedAdaptation='Requested tile,then native UO inner1/outer4tile bands;distance-squared/X/Y order;retained Z and per-candidate normal bounds/range/town/LOS/spawn checks. Startup and release share the helper;release resolves live target. Fully blocked failure has no mana debit,unit seed,allocation or FIFO replacement.',
  abilities=['Charged Bolt Sentry','Wake of Fire','Lightning Sentry','Wake of Inferno','Death Sentry'],candidateLimit=81,newSourceCompilerMatrices=0,
  nativeMarkers=21,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=2,sourceSnapshot=3,
  artifacts=native['artifacts'],files={str(p.relative_to(ROOT)):sha(p)for p in files},preservation=preservation(),
  diagnostic='Initial native pass verified new casts/natural AI but reload found mana drift in the new fixture,which constructed its player outside the existing creation-time regeneration guard. It now reuses SentryLearnedChecks.Fresh;fractional balance is checked exactly. Review also found Shadow startup checking LOS to the original requested target rather than the resolved candidate;fixed and explicit both-species LOS-wall tests added. Initial failed log/inputs retained.',
  scope='Native81-cell ordering/ties/bounds/retained Z,inner-band exhaustion,LOS wall and caster/candidate town rules;all five real player windups/live occupied-target releases/exact once-only payment/free fully blocked release;all five own casts from both original Shadows,world unit seed/no borrowed mana or actor RNG,resolved-candidate startup LOS,natural Warrior occupied-target AI;five original injured paid traps and both original Shadow/trap/FIFO states through two reloads. Existing free-ground five-trap firing/shared FIFO,blocked failure/cancellation,player low-mana/payment/bindings and paid cast persistence retained.',
  limits=['Exact D2R spawn-search order remains unrecovered and is explicitly replaced by this approved UO policy.','Other intrinsic/current-death/learned Shadow gates remain. Talon packet and46rewind controls from parent are preserved;its runtime repeated action remains next.','No new effect or icon identity;existing sentry casting/placement/emergence/impact/audio and talent/hotkey placeholders remain separately inventoried.'])
 out=ROOT/'tools/reference/sentry-placement-native-evidence.json'
 if check:assert json.loads(out.read_text())==receipt;print('Approved shared sentry placement receipt matches.')
 else:out.write_text(json.dumps(receipt,indent=2)+'\n');print('Recorded approved sentry placement:21 native markers/two reloads,all five player/both-Shadow casts/natural Warrior AI;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
