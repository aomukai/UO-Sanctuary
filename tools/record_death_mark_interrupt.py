#!/usr/bin/env python3
"""Bind live neutral-action interruption,issued missiles and real saved A2 mode."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/death-mark-interrupt'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log))
 assert sum(counts.values())==29 and ': failed'not in log and ' ERR]'not in log
 assert counts['iron-golem-birth-check']==counts['death-mark-interrupt-save-check']==3
 for name in ['iron-golem-items','npc-resource-callbacks','death-mark-native','death-mark-interrupt','tainted-casting']:
  assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 for name in ['death-mark-native','death-mark-interrupt']:assert counts[name+'-check-result']==1 and counts[name+'-reload-check-result']==2
 assert counts['tainted-casting-reload-result']==2
 assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-2.log').read_text()
 for kind in ['content','integration']:assert all(s in(STAGE/f'{kind}-build-2.log').read_text()for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-2.json').read_text())
 assert native['copiedData']==snap['inputs']['copiedData']==json.loads((ROOT/'.local/class-skills/death-mark-native/birth-inputs.json').read_text())['copiedData']
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 assert sha(STAGE/'content/Sanctuary.Content.dll')==sha(STAGE/'native-1/content/Sanctuary.Content.dll')
 archived=(STAGE/'native-1/birth-native-checks.log').read_text();assert archived.count(': passed')==26 and ': failed'not in archived
 f=json.loads((STAGE/'death-mark-interrupt-fixtures.json').read_text());assert f['Generation']==2 and f['Command']['Mode']==5
 for name in ['death-mark-native','tainted-casting']:assert json.loads((STAGE/(name+'-fixtures.json')).read_text())['Generation']==2
 cases=ROOT/'.local/class-skills/tainted-fireball-cast-source/cases.json';assert len(json.loads(cases.read_text()))==401
 captures={}
 for name,rv,count in [('callees-death-mark-pet-services-20261108.json','0x425550',204),('services-shadow-skill-entry-20261107.json','0x3dda90',1752),('services-shadow-skill-entry-20261107.json','0x3de170',346)]:
  path=ROOT/'.local/class-skills/warlock-runtime-reference'/name;d=json.loads(path.read_text());r=next(r for r in d['records']if r['rva']==rv)
  assert r['allUnwindFragmentsCaptured']and r['bytes']==count
  artifacts={p:h for p,h in d['artifacts'].items()if rv[2:] in p}
  for p,h in artifacts.items():assert sha(ROOT/p)==h,p
  captures[rv]=dict(bytes=count,capture=str(path.relative_to(ROOT)),artifacts=artifacts)
 paths=['src/Sanctuary.Content/'+n+'.cs'for n in ['DeathMarkWorld','TaintedFireBallCasting','GoatmanBerserkEffects']]+[
  'tests/Sanctuary.Integration/DeathMarkInterruptChecks.cs','tests/check_death_mark_interrupt.py','tools/record_death_mark_interrupt.py']
 result=dict(referenceVersion='3.3.93847',handlers=211,warlockHandlers=2,completedClasses=[],learnedDeathMarkEnabled=False,automaticGenericPetAiEnabled=False,
  nativeMarkers=29,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=2,sourceSnapshot=2,nativeRun=2,priorCompilerMatricesRerun=0,retainedTaintedAnimationCases=401,
  captures=captures,sourceEquivalence='Retained current idle/mode-entry disassembly reviewed;existing managed mode/state and A2 services reused. No new full mode-executor emulation or event-allocator parity claim.',
  files={p:sha(ROOT/p)for p in paths},artifacts=native['artifacts'],preservation=preservation(),
  scope='Registered real Tainted A2 starts mode5,unchanged mode retains pending action,Death Mark neutral transition cancels unissued action and natural end returns mode1. Already-issued missile retains original packet/origin/owner and still damages with original NPC credit after warp. Shared mode hook removes Goatman TEMPONLY Berserk while retaining ordinary timed Berserk. No healing,birth rebuild or payment. Before each save,after socket-detach cleanup,actual pending source mode5 is restored with an explicit future action-clock fixture. Actual reload drops transient action and normalizes only registered Tainted A2mode to1,preserving original body,birth,wounds,command/selector/map/nextrequest. Original native bridge and standalone Tainted registered/unregistered casting controls remain passing.',
  persistence='No production timer/expiry is held. Future2minute start exists only in the unissued transient-save fixture;no persisted skill effect expiry is extended. First26marker pass was archived before strengthening pre-save mode coverage;production DLL is identical in both runs.',
  limits=['Automatic GenericPet decisions/movement/combat services and shared live event consumption remain unfinished. Stored next-AI request is not automatically replayed.','Full current mode-admission/failure/raw-event-list branches remain explicit;this completes the existing managed mode1/A2 projection and uses established TEMPONLY removal.','Original controller birth/path producer,startup/payment/localdelay/cancellation/input still required before learned DeathMark or demon casting.','Final effects/audio and talent/hotkey icons remain separate placeholders.'])
 path=ROOT/'tools/reference/death-mark-interrupt-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print('Death Mark interruption:29 native markers/2 actual reloads;pending A2 cancelled,issued missiles survive,TEMPONLY state clears,and real saved mode5 does not replay.211handlers;automatic AI/learned route gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
