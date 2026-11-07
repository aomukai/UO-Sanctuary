#!/usr/bin/env python3
"""Bind learned Valkyrie row/SC metadata to existing complete source evidence."""
import argparse,hashlib,json
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
def review(check=False):
 inputs={}
 def load(p):inputs[str(p.relative_to(ROOT))]=hashlib.sha256(p.read_bytes()).hexdigest();return json.loads(p.read_text())
 row=next(r for r in load(ROOT/'data/Sanctuary/Balance/class-skills.json')if r['talentId']=='amazon.valkyrie');f=row['sourceFields'];expected=dict(srvdofunc=16,summon='valkyrie',pettype='valkyrie',petmax=1,InTown=1,anim='SC',seqtrans='SC',mana=25,lvlmana=1,manashift=8,minmana=1,leftskill=0,rightskill=1)
 assert {k:f[k]for k in expected}==expected and not f.get('srvstfunc')and not f.get('delay')
 old=load(ROOT/'tools/reference/valkyrie-equipment-engine-evidence.json');p=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src/SKILLS/SkillAma.cpp';raw=p.read_text();assert hashlib.sha256(p.read_bytes()).hexdigest()==old['sourceHashes']['source/D2Game/src/SKILLS/SkillAma.cpp'];inputs[str(p.relative_to(ROOT))]=hashlib.sha256(p.read_bytes()).hexdigest()
 body=function(raw,'SKILLS_SrvDo016_Valkyrie');assert hashlib.sha256(body.encode()).hexdigest()==old['blockHashes']['SKILLS_SrvDo016_Valkyrie']and'pGame->dwGameFrame + 20'in body
 p=ROOT/old['probe']['fixture'];assert hashlib.sha256(p.read_bytes()).hexdigest()==old['probe']['sha256'];inputs[str(p.relative_to(ROOT))]=hashlib.sha256(p.read_bytes()).hexdigest();dispatch=json.loads(p.read_text())['dispatch'];assert len(dispatch)==15
 clock=load(ROOT/'tools/reference/amazon-cast-clock-engine-evidence.json');assert clock['contexts']==2072 and len(clock['completeRoutineHashes'])==7
 for p,h in clock['artifacts'].items():
  q=ROOT/p;assert hashlib.sha256(q.read_bytes()).hexdigest()==h,p;inputs[p]=h
 rows=load(ROOT/'data/Sanctuary/Balance/amazon-cast-clock.json');assert rows==clock['animations']and len(rows)==8
 for rel in ['src/Sanctuary.Content/AmazonCastClock.cs','src/Sanctuary.Content/AmazonValkyrieCasting.cs','src/Sanctuary.Content/AmazonValkyrieBody.cs','src/Sanctuary.Content/AmazonCombat.cs','src/Sanctuary.Content/ClassCombat.cs','src/Sanctuary.Content/SkillBindings.cs','src/Sanctuary.Content/GroundSummonPlacement.cs',str(Path(__file__).resolve().relative_to(ROOT))]:inputs[rel]=hashlib.sha256((ROOT/rel).read_bytes()).hexdigest()
 result=dict(referenceVersion='3.3.93847',sourceId=row['sourceId'],row=expected,firstThinkFrames=20,sourceDispatchCases=15,sourceSchedulerCases=2072,weaponClasses=[r['WeaponClass']for r in rows],currentExecutableParity=False,inputs=inputs,scope=['Current row selects the same owned Amazon SC rows and shared seven-routine scheduling recipe already verified across2072contexts. Existing complete do16 evidence covers15dispatch cases and20frame first think;no new scheduler or spawn arithmetic is substituted.','Native placement uses the existing Druid target/fallback/two-tile adapter,not recovered D2room search. Candidate/payment/learned-roster commit follows existing persistent summon transactions;item-only/proc/foreign-class contexts are outside the trained-Amazon route.','Exact current executable outer mana/room/input chronology and graphical client acceptance remain unproven. Native tests separately verify actual routing,resource/roster/lifecycle and live socket combat.'])
 encoded=json.dumps(result,indent=2)+'\n';dest=ROOT/'tools/reference/valkyrie-cast-source-evidence.json'
 if check:assert dest.read_text()==encoded
 else:dest.write_text(encoded)
 print('Valkyrie cast:current row,8SCweapon classes/2072source clocks,15complete do16dispatch cases/20frame first think;native transaction and input checked separately.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');review(p.parse_args().check)
