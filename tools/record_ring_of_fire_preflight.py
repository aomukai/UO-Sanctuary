#!/usr/bin/env python3
"""Pin Ring of Fire's table contract and unavailable old callback boundaries."""
import argparse,hashlib,json,re,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,Image
from probe_skill_do_candidates import static
from pe_unwind_groups import UnwindGroups
OUT=ROOT/'tools/reference/ring-of-fire-preflight.json'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 folder=ROOT/'tools/reference/d2r-named-equipment/json';skills=json.loads((folder/'skills.json').read_text());missiles=json.loads((folder/'missiles.json').read_text());skill=skills['394'];parent=missiles['ringoffire'];child=missiles['ringoffireexplode']
 assert skill['srvdofunc']==154 and skill['calc2']=='((lvl>=10)?32:16)' and skill['mana']==16 and skill['manashift']==7 and skill['lvlmana']==1
 assert parent['pSrvDmgFunc']==17 and parent['DmgCalc1']=='100+(fnum*par1)' and parent['Param1']==5 and parent['*ID']==689 and child['*ID']==730
 treePath=ROOT/'.local/class-skills/d2moo-source-tree.json';tree=json.loads(treePath.read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 for name in ('source/D2Game/src/SKILLS/Skills.cpp','source/D2Game/src/MISSILES/MissMode.cpp'):
  raw=(ROOT/'.local/class-skills/d2moo-pinned-source'/name).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==name)
 base=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src';do=(base/'SKILLS/Skills.cpp').read_text().split('static const SkillDoFunc gpSkillSrvDoFnTable_6FD40A20[] =',1)[1].split('};',1)[0];dmg=(base/'MISSILES/MissMode.cpp').read_text().split('constexpr MissileDmgFunc gpMissileSrvDmgFnTable_6FD2E838[] =',1)[1].split('};',1)[0]
 dos=re.findall(r'^\s*(nullptr|SKILLS_\w+)\s*,',do,re.M);dmgs=re.findall(r'^\s*(nullptr|MISSMODE_\w+)\s*,',dmg,re.M);assert dos[154]==dmgs[17]=='nullptr'
 image=Image();table,_,_=static(image);rva=struct.unpack('<191Q',image.bytes(int(table['tableCandidateRva'],16),191*8))[154]-image.base;fragments=[dict(rva=hex(a),endRva=hex(b))for a,b,_ in UnwindGroups(image).fragments(rva)]
 foundation=ROOT/'tools/reference/warlock-foundation-preflight.json';pin=json.loads(foundation.read_text());assert pin['referenceVersion']=='3.3.93847' and pin['pinnedInputHashes']['skills']==sha(folder/'skills.json') and pin['pinnedInputHashes']['missiles']==sha(folder/'missiles.json') and pin['licensedSkillSha256']==sha(ROOT/'.local/research/d2r-3.3.93847/skills.txt')
 paths=[Path(__file__).resolve(),folder/'skills.json',folder/'missiles.json',base/'SKILLS/Skills.cpp',base/'MISSILES/MissMode.cpp',foundation,image.reference,treePath]
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,currentExecutableParity=False,skill=skill,parentMissile=parent,visualExplosion=child,rankOneMana256=2048,missileCountBoundary=dict(before10=16,from10=32),oldSourceDo154Null=True,oldSourceDamage17Null=True,
  currentCastCandidate=dict(tableRva=table['tableCandidateRva'],slot=154,rva=hex(rva),fragments=fragments,dispatcherLinkVerified=False),
  sourceInputs={str(p.relative_to(ROOT)):sha(p)for p in paths},
  reuse=['Standard saved missile motion/collision and fire impact kernels;spell FP damage/hard synergy/gear suppliers;existing native knockback movement after qualified missile flag handling.',
         'Primary table cost uses manashift7:8mana at rank1,not the raw mana16. calc2 switches16to32at effective10;Param1=5is not the missile count.'],
  gates=['New do154launch directions,child/factory/seed/failure composition remain unverified;matching the generic Nova label does not establish this body.',
         'New damage17must bind fnum and its scaling/rounding/application order. Retaining the formula is not permission to multiply a guessed stage of damage.',
         'Current acceleration,motion/activation,collision/explosion/NextHit identity,knockback versus immunity and post-first-hit damage require composition and native acceptance. Visual explosion730has no server do/hit fields;do not allocate a second server damage child from its name.'],
  referenceLeads=[dict(url='https://diabloforfans.com/skill/ring-of-fire',checked='2026-11-06',role='User-supplied data site;summary is not stronger than primary owned tables.'),dict(url='https://upload3.inven.co.kr/upload/2026/03/02/bbs/WarlockSkillsNotesandFunctionality.pdf',checked='2026-11-06',role='Named author Warren/Kabryxis reports testing;version applicability must be checked. Older Cleave/Mirrored values conflict with the pinned current rows. Not current-code or full timing proof.')])
 encoded=json.dumps(result,indent=2)+'\n'
 if check:assert OUT.read_text()==encoded
 else:assert not OUT.exists(),'Preserve prior preflight';OUT.write_text(encoded)
 print('Ring of Fire:owned table contract pinned;new do154/damage17remain gates;no handler or process-memory access.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
