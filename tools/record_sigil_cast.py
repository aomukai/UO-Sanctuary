#!/usr/bin/env python3
"""Authenticate learned Lethargy,its independent status and actual socket use."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/sigil-cast'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 proofs={}
 for name,n in [('sigil-lethargy-source-evidence',128),('sigil-tracking-current-evidence',111),('sigil-launch-current-evidence',126)]:
  p=ROOT/f'tools/reference/{name}.json';r=json.loads(p.read_text());assert r['cases']==n
  for rel,h in r['artifacts'].items():assert sha(ROOT/rel)==h,rel
  proofs[name]=sha(p)
 approval=ROOT/'tools/reference/sigil-lifecycle-approval.json';a=json.loads(approval.read_text());assert a['approvedByUser']and a['answer']=='Approve this Sigil lifecycle rule'
 inventory=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[r for r in inventory['skills']if r['implementation']=='handler-present'];assert len(handlers)==211 and sum(r['characterClass']=='Warlock'for r in handlers)==2 and inventory['completedClasses']==[]
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==51 and ': failed'not in log and ' ERR]'not in log
 for name in ['sigil-cast','sigil-lethargy','sigil-actor','sigil-tracking','sigil-launch']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 for name in ['sigil-cast','sigil-lethargy','sigil-actor']:assert counts[name+'-check-result']==1 and counts[name+'-reload-check-result']==2
 for name in ['sigil-cast-left','sigil-cast-right','sigil-cast-quick']:assert counts[name+'-check']==3 and counts[name+'-result']==3 and log.count(name+': windup')==3
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-4.log').read_text()
 for kind,n in [('content',1),('integration',4)]:assert all(x in(STAGE/f'{kind}-build-{n}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-5.json').read_text());assert native['copiedData']==snap['inputs']['copiedData']
 for rel,h in native['sourceInputs'].items():assert sha(ROOT/rel)==h,rel
 for rel,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][rel]==h,rel
 for name in ['sigil-cast','sigil-lethargy','sigil-actor']:assert json.loads((STAGE/(name+'-fixtures.json')).read_text())['Generation']==2
 paths=['src/Sanctuary.Content/'+n+'.cs'for n in ['SigilLethargy','SigilActors','WarlockCombat','ClassCombat','ClassStats','GearEffects','SourceAttackRate']]+['tests/Sanctuary.Integration/'+n+'.cs'for n in ['SigilCastChecks','SigilLethargyChecks','SigilActorChecks','SigilTrackingChecks','SigilLaunchChecks']]+['tools/probe_sigil_lethargy.py','tools/record_sigil_cast.py','tests/check_sigil_cast.py','tests/check_sigil_lethargy.py','data/Sanctuary/Balance/class-skill-status.json']
 result=dict(referenceVersion='3.3.93847',handlers=211,warlockHandlers=2,completedClasses=[],learnedLethargyEnabled=True,currentExecutableParity=False,sourceCases=dict(nonCurseReceiver=128,currentTracking=111,currentLaunch=126),sourceEvidence=proofs,lifecycleApprovalSha256=sha(approval),newFocusedSourceProbes=1,focusedSourceProbeBuilds=2,priorCompilerMatricesRerun=0,nativeMarkers=51,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=4,sourceSnapshot=5,nativeRun=4,artifacts=native['artifacts'],files={p:sha(ROOT/p)for p in paths},preservation=preservation(),
 scope='Learned Warlock/Witch ordinary SC ground cast,left/right mouse and quick slot compose current Sigil launch/tracking,original saved marker,shared do14periodic driver and independent state192. Mana4is paid once at successful candidate commit;windup/Stop,underfunded/late mana/late terrain failure preserve old roster/resources. Current target coordinates resolve at release. Source rank/creator/primary retention and secondary writes,ordinary curse/Attract coexistence,no curse-duration reduction,actual armor/attack/movement consumers,additive Ward slow and normal12frame expiry pass. Approved owner-death cleanup is exercised. Bindings survive reload;normal5second learned marker expires without replay;original longer-lived prepared marker/status identity survives actual reloads.',
 persistenceControl='The normal480msrecipient expiry is tested first. Only one inspected recipient payload then receives an explicit FixtureSpace retention hold to cross native boot times;its original target/creator/rank/expiry is verified through two reloads. This test-only hold is not product behavior. Longer-lived world marker uses normal rank200204second lifetime without an artificial hold.',
 diagnostic='Status-only C2/I2/native1passed27markers and is archived in recipient-pass-1. Initial status enum typoMobileDelta.Statswas correctedtoStat before that run;dependent Integration1missing-reference errors were setup fallout. Learned native1gameplay control passed but fixture logged result before acknowledgement;Integration2fixes the handshake. Native2right socket use passed;quick fixture lacked Strength for Magefist. Integration3uses the existing Warlock cast-clock fixtureStrength100setup;native3passes right/quick on all boots;Integration4/native4adds the independently permitted left mouse route and passes all three paths. Gameplay content stayedContent1through those two fixture fixes. Failed logs/inputs/source are archived.',
 remaining=['Exact current whole-factory RNG/notification/unknown12ccontext,room/half-cell ordering and mid-animation stat changes remain unverified port/acceptance scopes;no class certificate','Native recipient admission uses the shared hostile ordinary-NPC predicate;player/PvP,enemy-owned pets and unclassified species remain outside admitted world scope','Owned table comment labels Param5DamageDealt,but actual aurastat2is item_armor_percent16with armorclassop13. Implementation follows the actual pinned stat,not the comment;current in-game display/acceptance remains a separate check','Rancor Confuse61and Death reactive/execution/explosion remain separate unavailable handlers','Final cast/ground/pulse/recipient/expiry/audio and talent/hotkey art remain identified placeholders;installed build/configuration/saves unchanged'])
 p=ROOT/'tools/reference/sigil-cast-native-evidence.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Learned Lethargy:211/240handlers;Warlock2/30;51native markers/two reloads,all three real socket casts and independent stat delivery;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
