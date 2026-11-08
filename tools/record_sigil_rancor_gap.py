#!/usr/bin/env python3
"""Pin the bounded current Confuse61 access gap without rereading the process."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 cap=ROOT/'.local/class-skills/warlock-runtime-reference/warlock-confuse-61-20261108/capture.json';c=json.loads(cap.read_text());r=c['records'][0]
 assert c['referenceVersion']=='3.3.93847'and c['dispatcherIndexLinked']and c['immutableTableMatches']
 assert r['slot']==61 and r['rva']=='0x454b50'and not r['allUnwindFragmentsCaptured']and len(r['fragments'])==5
 assert all(f['permissions']=='---s'and not f['captured']for f in r['fragments'])
 assert c['bytesRead']==3616 and not any(c[k]for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
 for rel,h in c['artifacts'].items():assert sha(ROOT/rel)==h,rel
 primary=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';skill=json.loads(primary.read_text())['396']
 statefile=ROOT/'tools/reference/d2r-named-equipment/json/states.json';state=json.loads(statefile.read_text())['sigilrancor']
 assert skill['srvdofunc']==157 and skill['calc1']==75 and skill['auralencalc']==12 and skill['aurafilter']==2
 assert skill['aurastat1']=='damagepercent'and skill['aurastat2']=='attackrate'and state['*ID']==193 and state['group']==8 and not state.get('curse',0)
 old=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src/SKILLS/SkillNec.cpp';text=old.read_text()
 body=text[text.index('int32_t __fastcall SKILLS_SrvDo061_Confuse'):text.index('//D2Game.0x6FD0C2E0')]
 assert 'for (int32_t i = 1; i < 6; ++i)'in body and 'dwAiCurseDiv'in body and 'UNIT_ALIGNMENT_EVIL'in body
 assert 'dwCalc[0]'not in body
 result=dict(referenceVersion='3.3.93847',skillId=396,name='Sigil Rancor',currentDispatcherLinked=True,currentReceiverVerified=False,learnedHandlerEnabled=False,
  process=c['process'],codeRva=r['rva'],unreadableFragments=r['fragments'],bytesRead=c['bytesRead'],forcedReads=False,processInput=False,protectionChanges=False,gameMemoryWritten=False,
  primary={k:skill[k]for k in ['srvdofunc','aurafilter','auratargetstate','auralencalc','aurarangecalc','aurastat1','aurastatcalc1','aurastat2','aurastatcalc2','calc1','calc3','mana','manashift']},state=state,
  legacyDifference='Complete older Confuse61 initializes secondary stats from index1,always applies Confuse to eligible evil monsters and divides the AI duration by difficulty. That body cannot establish current primary damagepercent,calc1=75 chance,allied-recipient or noncurse-state193 behavior.',
  unresolved=['When/how 75 percent AI chance rolls;RNG owner/reapplication behavior','Primary damagepercent writes,eligible allied/hostile recipients and immunity restrictions','Whether 12-frame noncurse recipient/AI duration is divided by difficulty','State193/group8 replacement,ordinary Confuse/Attract interactions and AI reset/removal'],
  unaffected='Shared do157 launch/tracking,all3 Rancor marker tiers,do14 period6/function61 identity and approved temporary-actor lifecycle can be prepared independently. Do not enable learned Rancor or attach Lethargy receiver.',
  artifacts={str(p.relative_to(ROOT)):sha(p)for p in [cap,primary,statefile,old,Path(__file__).resolve(),ROOT/'tools/capture_warlock_skill_do.py']},legacyBodySha256=hashlib.sha256(body.encode()).hexdigest())
 p=ROOT/'tools/reference/sigil-rancor-reference-gap.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Rancor:current do61 dispatcher linked;all5 fragments protected;75%/primary stats/noncurse AI edges remain unverified. No repeated process read.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
