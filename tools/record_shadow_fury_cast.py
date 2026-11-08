#!/usr/bin/env python3
"""Bind approved intrinsic Shadow Fury to native action/projectile evidence."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-fury-cast'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 reused={}
 for name in ['shadow-fury-packets','shadow-fury-missiles','shadow-fury-clock','shadow-fury-action','shadow-fury-callback']:
  p=ROOT/f'tools/reference/{name}-source-evidence.json';proof=json.loads(p.read_text());reused[name]=dict(sha256=sha(p),contexts=proof['contexts'])
  for group in ['inputs','artifacts']:
   for rel,h in proof.get(group,{}).items():assert sha(ROOT/rel)==h,rel
 for name,approval in [('shadow-fury-action-proposal','Approve the single-sequence Shadow Fury rule'),('shadow-missile-critical-proposal','Approve unsigned NPC missile critical')]:assert approval in(ROOT/f'docs/{name}.md').read_text()
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==27 and ': failed'not in log and ' ERR]'not in log
 for name in ['shadow-fury-cast','shadow-fury-clock','shadow-fury-packets','shadow-fury-missiles','shadow-master-world','blade-fury-missile']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-3.log').read_text()
 for kind,n in [('content',1),('integration',3)]:assert all(x in(STAGE/f'{kind}-build-{n}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-4.json').read_text());assert native['copiedData']==snap['inputs']['copiedData']
 for rel,h in native['sourceInputs'].items():assert sha(ROOT/rel)==h,rel
 for rel,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][rel]==h,rel
 saved=json.loads((STAGE/'shadow-fury-cast-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Bodies'])==2
 paths=['src/Sanctuary.Content/'+n+'.cs'for n in ['ShadowBladeFury','ShadowFuryPackets','ShadowFuryMissiles','ShadowSkillCasting','ShadowActions','NpcMissileCritical','AssassinShadowBody','ShadowMasterWorld']]+['tests/Sanctuary.Integration/ShadowFuryActionChecks.cs','tests/Sanctuary.Integration/ShadowMasterWorldChecks.cs','tests/check_shadow_fury_cast.py','tools/record_shadow_fury_cast.py']
 result=dict(referenceVersion='3.3.93847',handlers=208,completedClasses=[],learnedShadowsEnabled=False,approvedRules=['Approve the single-sequence Shadow Fury rule','Approve unsigned NPC missile critical'],nativeMarkers=27,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=3,sourceSnapshot=4,newCompilerMatrices=0,reusedSource=reused,files={p:sha(ROOT/p)for p in paths},artifacts=native['artifacts'],preservation=preservation(),
 scope='Actual original own mode14 sequence releases at10 and recovers at18,without rewind/cross-action lease. Shared startup26/do48/free resource state and action conductor reused. Unsigned own337/141/selected-primary null-skill mastery tests short circuit on actual NPC stream;world unit seed allocated separately,private projectile seed preserved. No additional player or pet mana. Common own packet/projectile/typed damage/return/credit/save host retained. Direct action-born blade travel/HP damage,ground,moving/deleted-target,live-rank,duplicate/end,pending suspend/no replay,eight interruptions and both natural Warrior and isolated-candidate Master AI pass. Existing 128NPCimpact,16packet,12clock,46SC/critical controls and retained player missile/Master world checks pass across two reloads. Master synthetic unavailable-repeat regression now uses owned passive252 rather than newly supported Fury266;no off-class skill inserted.',
 diagnostic='Native1 fixture expected a previously initialized world seed although the allocation service correctly initializes lazily. Added explicit world initialization before seed-order comparisons. C1/I3/native3 passes27markers. Earlier failed log/input manifest retained. No prior compiler matrix rerun.',
 remaining=['Learned Shadow admission and full class certification remain separate;handler count remains208','Exact current D2R rewind/re-entry/critical signedness is replaced only by the two explicit approvals,not established as current-executable behavior','Other recipient/property contexts retain explicit refusals,including NPC Fury unsupported burn/bypass/positive armor packets and player/PvP recipients','Full modern AI/geometry/mutation/removal parity and other inherited item consumers remain scoped','Final Fury/Shadow poses,blade travel/impact/expiry and sound remain existing identified placeholders;talent/hotkey icons separately tracked'])
 p=ROOT/'tools/reference/shadow-fury-cast-native-evidence.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Approved intrinsic Shadow Fury:27native markers/two reloads;own action/critical/world/private streams and both natural AIs;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
