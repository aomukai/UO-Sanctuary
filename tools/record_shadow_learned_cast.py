#!/usr/bin/env python3
"""Record learned Shadow casting with retained source proofs and real input."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-learned-cast'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 clockpath=ROOT/'tools/reference/assassin-cast-clock-engine-evidence.json';clock=json.loads(clockpath.read_text());assert clock['contexts']==2590
 for rel,h in clock['artifacts'].items():assert sha(ROOT/rel)==h,rel
 assert json.loads((ROOT/'data/Sanctuary/Balance/assassin-cast-clock.json').read_text())==clock['animations']
 source=BASE/'d2moo-pinned-source/source/D2Game/src/SKILLS/SkillAss.cpp';b=source.read_bytes();tree=json.loads((BASE/'d2moo-source-tree.json').read_text());rel='source/D2Game/src/SKILLS/SkillAss.cpp'
 assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel)
 body=function(b.decode(),'SKILLS_SrvDo049_ShadowWarrior_Master');assert 'pGame->dwGameFrame + 20'in body
 rows=[]
 for r in json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text()):
  if r['sourceId']not in [268,279]:continue
  f=r['sourceFields'];expected=dict(srvdofunc=49,summon='shadowwarrior'if r['sourceId']==268 else'shadowmaster',pettype='shadowwarrior',petmax=1,summode='NU',anim='SC',seqtrans='SC',InTown=1,localdelay=15,leftskill=0,rightskill=1,minmana=1,manashift=7,mana=54 if r['sourceId']==268 else 70,lvlmana=1)
  assert all(f[k]==v for k,v in expected.items())and not f.get('srvstfunc');rows.append(dict(sourceId=r['sourceId'],fields=expected))
 assert len(rows)==2
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[r for r in status['skills']if r['implementation']=='handler-present'];assert len(handlers)==210 and sum(r['characterClass']=='Assassin'for r in handlers)==30 and status['completedClasses']==[]
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert ': failed'not in log and ' ERR]'not in log
 for name in ['shadow-cast','shadow-fury-cast','shadow-master-world']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 for name in ['shadow-cast-right','shadow-cast-quick']:assert counts[name+'-check']==3 and counts[name+'-result']==3
 assert sum(counts.values())==30 and 'pass across3boots/2reloads'in(STAGE/'native-run-4.log').read_text()
 for kind,n in [('content',3),('integration',5)]:assert all(x in(STAGE/f'{kind}-build-{n}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-9.json').read_text());assert native['copiedData']==snap['inputs']['copiedData']
 for rel,h in native['sourceInputs'].items():assert sha(ROOT/rel)==h,rel
 for rel,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][rel]==h,rel
 assert json.loads((STAGE/'shadow-cast-fixtures.json').read_text())['Generation']==2
 paths=['src/Sanctuary.Content/'+n+'.cs'for n in ['AssassinShadowCasting','AssassinShadowBody','ShadowGroundMotion','AssassinCombat','AssassinCastClock']]+['tests/Sanctuary.Integration/ShadowCastChecks.cs','tests/Sanctuary.Integration/AssassinCastClockChecks.cs','tests/check_shadow_learned_cast.py','tools/record_shadow_learned_cast.py','data/Sanctuary/Balance/class-skill-status.json']
 result=dict(referenceVersion='3.3.93847',handlers=210,assassinHandlers=30,completedClasses=[],learnedShadowsEnabled=True,currentExecutableParity=False,newCompilerMatrices=0,retainedScSourceContexts=2590,rows=rows,completeDo49Sha256=hashlib.sha256(body.encode()).hexdigest(),pinnedSkillAssSha256=sha(source),clockEvidenceSha256=sha(clockpath),nativeMarkers=30,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=3,integrationBuild=5,sourceSnapshot=9,artifacts=native['artifacts'],files={p:sha(ROOT/p)for p in paths},preservation=preservation(),
 scope='Trained Assassin learned SC/right and quick routes create the actual original Shadow controller. Live release rank/cost/placement checks,candidate qualification,once-only fractional payment,learned saved flag and shared one-pet Warrior/Master roster reuse existing Valkyrie transaction. Failed allocation/payment preserve the existing wounded pet and resources;actual failed allocation remains forward. Town initialization checks actual original ownership separately from combat permission,then parks normally without healing. Existing combat Attached town gate remains. Qualified native fist timers return1second without damage/RNG/allocation;legacy unqualified bodies keep their guard. Both real socket routes cause source-controller HP damage. Eight original creation cohort contexts (two species/four seeds),interruptions,underfunding,other-species replacement,backend-body isolation,parked original item/resource/wound/seed and paid-roster saves pass. Existing intrinsic Fury and Master world consumers retained.37obsolete public-unavailability assertions removed/reduced to their numeric/action invariants;new learned acceptance tests replace that blanket gate. No complete class claim.',
 diagnostic='I1 exposed two copied fixture type names;fixed to reuse existing mapped recipient. Native1 exposed town controller initialization reusing combat-only Attached and parked validation requiring a world map;ownership-only initialization plus parked-compatible qualification fix both. Native2 passed transactions/town/cohort but real socket combat reached the prior throwing native BeginMelee guard and terminated the disposable server. Qualified original controllers now reuse Valkyrie no-op1second timer,with direct no-mutation verification;unqualified legacy guard unchanged. Native3 passed;native4/C3/I5 adds the actual committed local-cooldown boundary and final whitespace cleanup. Failed logs/input manifests retained;installed server never changed.',
 remaining=['Unsupported original equipment/property/recipient cases still report/refuse intact without stripping items or charging failed learned casts','Full modern AI/geometry/callback/removal/PvP and nonzero immunity-pierce acceptance remain explicitly scoped','Final Shadow bodies/equipment,cast/attack/death/spell effects/audio and talent/hotkey icons remain separate placeholders','Amazon Slow Missiles and remaining Warlock skills remain on the class roadmap'])
 p=ROOT/'tools/reference/shadow-learned-cast-native-evidence.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Learned Shadows:210/240handlers;Assassin30/30;30native markers/two reloads,both real socket casts/combat;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
