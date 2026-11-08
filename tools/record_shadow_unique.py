#!/usr/bin/env python3
"""Bind original Shadow unique identity to tested source and native persistence."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/shadow-unique'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 review_path=ROOT/'tools/reference/shadow-death-modifier-source-review.json';review=json.loads(review_path.read_text())
 for rel,h in review['inputs'].items():assert sha(ROOT/rel)==h,rel
 assert review['reviewToolSha256']==sha(ROOT/'tools/review_shadow_death_modifier.py')
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log))
 assert sum(counts.values())==15 and ': failed'not in log and ' ERR]'not in log
 for family in ['shadow-unique','npc-item-slow']:assert counts[family+'-check']==1 and counts[family+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-2.log').read_text()
 for kind in ['content','integration']:assert all(x in(STAGE/f'{kind}-build-1.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-2.json').read_text());build=json.loads((STAGE/'source-snapshot-1.json').read_text())
 assert native['copiedData']==snap['inputs']['copiedData']==build['inputs']['copiedData']
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 for p,h in build['inputs']['sourceInputs'].items():
  if p.startswith(('src/','shared/')):assert native['sourceInputs'][p]==h,p
 fixture=json.loads((STAGE/'shadow-unique-fixtures.json').read_text());assert len(fixture['Bodies'])==len(fixture['States'])==2
 files=[ROOT/p for p in ['src/Sanctuary.Content/AssassinShadowBody.cs','src/Sanctuary.Content/PreparedMonsterStun.cs','src/Sanctuary.Content/CombatStuns.cs','src/Sanctuary.Content/NpcItemSlow.cs','src/Sanctuary.Content/PlayerWeaponContact.cs','src/Sanctuary.Content/IronGolemContact.cs','tests/Sanctuary.Integration/ShadowUniqueChecks.cs','tests/check_shadow_unique.py','tools/record_shadow_unique.py']]
 receipt=dict(parent='af2f7d1a7abfdfb40dd7ffddb9907bda7a2472f7',referenceVersion='3.3.93847',handlers=208,completedClasses=[],learnedShadowsEnabled=False,sourceReviewSha256=sha(review_path),newCompilerMatrices=0,nativeMarkers=15,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=2,artifacts=native['artifacts'],files={str(p.relative_to(ROOT)):sha(p)for p in files},preservation=preservation(),
 diagnostic='First run passed new Shadow identity checks;existing pet-slow fixture then required its connected-socket harness. Reused that fixture configuration without rebuilding unchanged assemblies. First log/input manifest retained. Second run passes all three boots/two reloads.',
 scope='Both real original Shadow species expose assigned modifier42 as unique,without EncounterGrade/boss/loot changes. Shared prepared player/pet melee and pet missile recipient constructors use the same unique predicate;Smite retains its explicit Champion rule. Approved pet slow reads unique identity and actual applications cap at50%. Generic Shadow stun rules read original monstats zero Velocity,not separate movement speed;unique draw occurs and stun is rejected. Explicit nonzero-velocity prepared controls verify unique90% rejection boundary. Original actor RNG/birth state unchanged;wounds/identity persist. Existing slow categories/contact/absorption/refresh/expiry/save checks retained.',
 remaining=['Modifier42 delayed death nova,actual retained dead caster/packet lifetime and cleanup remain unimplemented','Modern executable stun behavior is not independently observed;this qualifies pinned row/older-source routing','Fury rewind/critical,Master item/range/action gaps and learned Shadow admission remain separate','No class certified complete;installed build and saves preserved'])
 p=ROOT/'tools/reference/shadow-unique-native-evidence.json';s=json.dumps(receipt,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Original Shadow unique identity:15native markers/3boots/2reloads;existing pet-slow consumers pass;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
