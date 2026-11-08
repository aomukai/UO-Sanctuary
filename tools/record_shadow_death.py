#!/usr/bin/env python3
"""Bind retained original Shadow death nova to source/native/save evidence."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-death'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 reused={}
 for name in ['npc-item-spell-damage-source-evidence','shadow-fury-missiles-source-evidence']:
  p=ROOT/'tools/reference'/f'{name}.json';d=json.loads(p.read_text())
  for rel,h in (d.get('inputs',{})|d.get('source',{})|d.get('artifacts',{})|d.get('retainedProof',{})).items():assert sha(ROOT/rel)==h,rel
  for rel,h in d.get('sourceHashes',{}).items():assert sha(BASE/'d2moo-pinned-source'/rel)==h,rel
  reused[name]=sha(p)
 source_path=ROOT/'tools/reference/shadow-death-source-evidence.json';source=json.loads(source_path.read_text());assert source['contexts']==336
 for rel,h in (source['inputs']|source['artifacts']).items():assert sha(ROOT/rel)==h,rel
 assert not(BASE/'shadow-death-source/sanitizer.log').read_text()
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==24 and ': failed'not in log and ' ERR]'not in log
 for name in ['shadow-death','npc-item-spell-flights','shadow-unique']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 assert counts['shadow-death-live']==counts['shadow-death-live-result']==3
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-5.log').read_text()
 for kind,n in [('content',4),('integration',6)]:assert all(x in(STAGE/f'{kind}-build-{n}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-9.json').read_text());content=json.loads((STAGE/'source-snapshot-8.json').read_text())
 assert native['copiedData']==snap['inputs']['copiedData']==content['inputs']['copiedData']
 for rel,h in native['sourceInputs'].items():assert sha(ROOT/rel)==h,rel
 for rel,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][rel]==h,rel
 for rel,h in content['inputs']['sourceInputs'].items():
  if rel.startswith(('src/','shared/')):assert native['sourceInputs'][rel]==h,rel
 fixture=json.loads((STAGE/'shadow-death-fixtures.json').read_text());assert fixture['Generation']==2 and len(fixture['Pending'])==len(fixture['Emitted'])==2 and len(fixture['Flights'])==128
 paths=['src/Sanctuary.Content/'+n+'.cs'for n in ['AssassinShadowBody','AssassinShadowDeath','ShadowLoadout','PersistentMinions','PreparedMonsterDamage','InstantSpellNpcDamage','NpcElementSpellHit','NpcItemSpellFlights','SorceressSpellMissiles']]+['tests/Sanctuary.Integration/ShadowDeathChecks.cs','tests/check_shadow_death.py','tools/probe_shadow_death.py','tools/record_shadow_death.py','tools/reference/shadow-death-source-evidence.json']
 result=dict(parent='4463dcbcccfe007916a150ad463b86429a20fcd6',referenceVersion='3.3.93847',handlers=208,completedClasses=[],learnedShadowsEnabled=False,newCompiledSourceContexts=336,reusedSource=reused,sourceProofSha256=sha(source_path),oldCompilerMatricesRerun=0,nativeMarkers=24,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=4,integrationBuild=6,sourceSnapshot=9,contentSourceSnapshot=8,artifacts=native['artifacts'],files={p:sha(ROOT/p)for p in paths},preservation=preservation(),
 diagnostic='Initial content compile used a nonexistent batch name;changed to existing species-neutral CommitValkyrie. First native cohort passed initial prepared deaths and actual connected lethal-hit/travel/damage/cleanup,but reload exposed dead-original RNG filtering. Read-only original-loadout access now permits only retained retiring Shadows. Native2 still exposed pending-source placement loss;native3 added diagnostics identifying shared minion suspension internalizing dead casters. Shared suspension now applies only to living companions. Native4 passed all24markers;native5 adds explicit default dead-source refusal and actual returned lightning hit-class controls,also passes. Failed logs/input manifests and native4 passed log retained. No old compiler matrices rerun.',
 scope='Both seed1 initialized Shadow species retain their actual dead body,original items and ownership for four-frame Nova emission and child expiry. Nova rank uses summon-birth effective Shadow Warrior snapshot even for Master,capped15;current packet is own NPC Nova with no player weapon/potion/synergy payload.64 rays use existing ring geometry/flags7/velocity24/private world allocation and shared typed NPC damage;actual hit class64 is retained. Only matching saved retirement context admits dead missile delivery;ordinary/default dead sources refuse. Real death differs from ordinary Delete;inactive callbacks expire without catch-up damage;duplicate calls cannot allocate again. Original items and attack stream survive pending/emitted reloads and are deleted once at expiry. Owner parking preserves dead-source facet. Both species direct Kill and NPC HP/root kill-credit paths pass;connected real native lethal damage and automatic timed travelling Nova pass for Warrior on each boot. Existing original-NPC spell flights and unique/slow consumers retained.',
 remaining=['Mapped NPC recipients only:player/PvP and nonzero ordinary shield-rate recipients remain explicitly rejected by the existing NPC-spell host pending their adapters','Current executable death timing/callback/item mutation parity is not independently observed;older source/current tables and approved summon-birth snapshot precedence are explicit','Legacy owner snapshots without skills and unqualified Master off-class/item graphs are not admitted by the capture service;resolve before learned admission','Remaining Fury rewind/critical,Master item/range/AI gaps and learned Shadow Warrior/Master admission remain unfinished','Final Shadow death collapse/Nova ring/impact/expiry art and sound remain placeholders;no separate public proc icon'])
 p=ROOT/'tools/reference/shadow-death-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==text
 else:p.write_text(text)
 print('Retained Shadow death nova:336new source contexts/24native markers/2reloads;old NPC spell/unique consumers retained;installed artifacts/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
