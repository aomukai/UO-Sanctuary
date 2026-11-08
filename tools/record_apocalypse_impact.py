#!/usr/bin/env python3
"""Authenticate current resistance scaling and scoped actual Apocalypse fire impact."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/apocalypse-impact'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 source=json.loads((ROOT/'tools/reference/skill-resistance-current-evidence.json').read_text());assert source['cases']==142 and source['codePrefixBytes']==216 and source['inlineTableBytes']==40
 for p,h in source['artifacts'].items():assert sha(ROOT/p)==h,p
 cap=ROOT/'.local/class-skills/warlock-runtime-reference/apocalypse-callback-20261109-first.json';d=json.loads(cap.read_text())
 assert d['allUnwindFragmentsCaptured']and d['sourceReferencesVerified']and sum(int(f['endRva'],16)-int(f['rva'],16)for f in d['fragments'])==895
 for p,h in d['artifacts'].items():assert sha(ROOT/p)==h,p
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==15 and ': failed'not in log and ' ERR]'not in log
 assert counts['iron-golem-birth-check']==3
 for n in ['iron-golem-items','npc-resource-callbacks','death-mark-debuff','apocalypse-impact']:assert counts[n+'-check']==1 and counts[n+'-reload-check']==2
 assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3 and 'pass across3boots/2reloads'in(STAGE/'native-run-2.log').read_text()
 for kind,n in [('content',3),('integration',2)]:assert all(s in(STAGE/f'{kind}-build-{n}.log').read_text()for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-3.json').read_text());assert native['copiedData']==snap['inputs']['copiedData']
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 previous=json.loads((ROOT/'.local/class-skills/death-mark-interrupt/birth-inputs.json').read_text())['copiedData']
 assert {p for p,h in native['copiedData'].items()if previous.get(p)!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 assert sha(STAGE/'content/Sanctuary.Content.dll')==sha(STAGE/'native-1/content/Sanctuary.Content.dll')
 fixture=json.loads((STAGE/'apocalypse-impact-fixtures.json').read_text());assert fixture['Generation']==2 and fixture['Curse']['Ability']==84
 inventory=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present'for s in inventory['skills'])==211 and inventory['completedClasses']==[]
 for id in ['warlock.deathmark','warlock.apocalypse']:assert next(s for s in inventory['skills']if s['talentId']==id)['verification']=='partial-native-coverage'
 paths=['src/Sanctuary.Content/'+n+'.cs'for n in ['SourceSkillResistance','ApocalypseImpact','NamedElements','PaladinConviction','TaintedFireBallMissiles']]+[
  'tests/Sanctuary.Integration/ApocalypseImpactChecks.cs','tests/check_apocalypse_impact.py','tools/capture_apocalypse_callback.py','tools/probe_skill_resistance.py','tools/record_apocalypse_impact.py','tools/build_skill_roadmap.py']
 result=dict(referenceVersion='3.3.93847',handlers=211,warlockHandlers=2,completedClasses=[],learnedApocalypseEnabled=False,fullCurrentCallbackParity=False,nativeMarkers=15,markers=dict(counts),boots=3,reloads=2,saves=3,
  contentBuild=3,integrationBuild=2,sourceSnapshot=3,nativeRun=2,currentResistanceCases=142,pinnedPenaltyRanks=255,priorCompilerMatricesRerun=0,
  callback=dict(path=str(cap.relative_to(ROOT)),sha256=sha(cap),rva='0x450090',bytes=895,process=d['process'],bytesRead=d['bytesRead'],review='Complete current callback captured and reviewed;not fully emulated'),
  helper='tools/reference/skill-resistance-current-evidence.json',files={p:sha(ROOT/p)for p in paths},artifacts=native['artifacts'],preservation=preservation(),
  copiedDataChange='Only class-skill-status.json:record verified DeathMark dependency and current Apocalypse partial evidence;handler count unchanged.',
  scope='Prepared ordinary-NPC supplied enhanced fire component:actual HP loss,normal and one-fifth immunity scaling,immunity break before supplied item-pierce,rank cap40,zero/allocation refusal,unchanged native fire resistance/ordinary curse/later spell. Existing LowerResist pool is reduced before the per-hit stage. Original target/curse and absence of any lasting Apocalypse resistance reduction survive two actual reloads. Shared Paladin immunity primitive reused;existing Tainted ordinary-recipient guards reused without behavior change;DeathMark signed-reduction regression passes.',
  diagnostics='Native1initial impacts/source cases passed but reload fixture caster was not restored to its saved world pose. Native1source,binaries,log,inputs,fixtures archived. Native2restores only fixture pose/adds diagnostic detail;production DLL identical.',
  limits=['Full do163 area/filter/LOS and shared damage packet construction,roll identity/mastery/potion extras,launch/hit23 input/payment/cancellation remain unfinished.','Current temporary raw-stat-list factory and reentrant event exposure are not reproduced by a per-hit parameter;allocation acceptance is a supplied prepared context. Native scope excludes unqualified special reactions/PvP/pets through existing recipient guard.','No learned handler or class certificate;final effects/audio and talent/hotkey icons remain separate placeholders.'])
 path=ROOT/'tools/reference/apocalypse-impact-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print('Apocalypse impact:15 native markers/2 reloads;142 current resistance cases,255rank penalties,actual per-hit fire/immunity/pierce/lifecycle verified.211handlers;full cast gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
