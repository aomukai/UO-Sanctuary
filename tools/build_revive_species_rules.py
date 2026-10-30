#!/usr/bin/env python3
"""Verify owned Revive species flags and pin the complete older cast references."""
import argparse,csv,hashlib,json,re,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,digest
LOCAL=ROOT/'.local/class-skills/generic-pet-search-nested'
def build(check=False):
 prior=ROOT/'tools/reference/generic-pet-secondary-data-evidence.json';p=json.loads(prior.read_text());assert p['referenceVersion']=='3.3.93847'
 for n,h in p['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
 raw=(LOCAL/'monstats2.bin').read_bytes();assert struct.unpack_from('<I',raw)[0]==623 and len(raw)==4+623*296
 rows=[r for r in csv.DictReader((LOCAL/'monstats2.txt').read_text().splitlines(),delimiter='\t') if r['Id']!='Expansion'];assert len(rows)==623
 secondary=json.loads((ROOT/'data/Sanctuary/Balance/generic-pet-search-monster2.json').read_text());rules=[]
 for i,r in enumerate(rows):
  assert secondary[i]['id']==i and secondary[i]['name']==r['Id'] and struct.unpack_from('<I',raw,4+i*296)[0]==i
  bit=bool(raw[4+i*296+5]&1);assert bit==bool(int(r['revive'] or 0));rules.append(dict(id=i,name=r['Id'],allowed=bit))
 assert sum(r['allowed'] for r in rules)==391
 policyPath=ROOT/'data/Sanctuary/Balance/revive-policy.json';policy=json.loads(policyPath.read_text());decisionPath=ROOT/'tools/reference/revive-lifetime-deviation.json';decision=json.loads(decisionPath.read_text())
 assert policy==[dict(maximumAgeFrames=None,removeOnReviveDeath=True,removeOnNecromancerDeath=True,removeOnReplacement=True)]
 assert decision['status']=='user-approved' and decision['sanctuaryMaximumAgeFrames'] is None and not decision['scheduleExpirationEvent']
 out=ROOT/'data/Sanctuary/Balance/revive-species-rules.json';encoded=json.dumps(rules,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 treePath=ROOT/'.local/class-skills/d2moo-source-tree.json';tree=json.loads(treePath.read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 source=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src/SKILLS/SkillNec.cpp';b=source.read_bytes();rel='source/D2Game/src/SKILLS/SkillNec.cpp'
 assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
 text=b.decode();blocks={}
 for name in ['SKILLS_SrvSt21_Revive','SKILLS_SrvDo058_Revive','sub_6FD0DF40']:
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m;end=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
  blocks[name]=text[m.start():end]
 release=blocks['SKILLS_SrvDo058_Revive'];owner=blocks['sub_6FD0DF40']
 assert 'gdwBitMasks[8]' in blocks['SKILLS_SrvSt21_Revive'] and 'MONSTATS2FLAGINDEX_REVIVE' in release
 assert 'pTarget, AISPECIALSTATE_REVIVED' in release and 'pTarget, nSkillId, nSkillLevel' in release
 assert 'MONMODE_NEUTRAL' in release and 'nMaxHP - monStatsInit.nMinHP + 1' in release and 'if (nHitpoints < 1)' in release
 assert release.index('if (pRoom)')<release.index('D2MonStatsInitStrc monStatsInit')<release.index('sub_6FD0DF40')
 assert 'pGame->dwGameFrame + 15' in owner and 'STATE_REVIVE' in owner and 'nDuration + pGame->dwGameFrame' in owner
 skills=ROOT/'.local/class-skills/generic-pet-metadata/skills.txt';skill=next(r for r in csv.DictReader(skills.read_text().splitlines(),delimiter='\t') if r['skill']=='Revive')
 assert [skill[k] for k in ['*Id','srvstfunc','srvdofunc','mana','Param1','Param3','Param4','Param5','petmax','calc2']]==['95','21','58','45','200','4500','0','50','lvl','ln34']
 primary=ROOT/'.local/class-skills/generic-pet-search-services/monstats.bin';mb=primary.read_bytes();assert struct.unpack_from('<I',mb)[0]==751 and len(mb)==4+751*508
 classes=[]
 for i in range(751):
  link=struct.unpack_from('<h',mb,4+i*508+76)[0];classes.append([i,link,rules[link]['allowed'] if 0<=link<len(rules) else None])
 fixture=ROOT/'.local/class-skills/revive-species-rules-data-probe.json';encoded=json.dumps(dict(secondary=rules,classes=classes),separators=(',',':'))+'\n'
 if check:assert fixture.read_text()==encoded
 else:fixture.write_text(encoded)
 artifacts=[Path(__file__).resolve(),prior,LOCAL/'monstats2.bin',LOCAL/'monstats2.txt',out,ROOT/'data/Sanctuary/Balance/generic-pet-search-monster2.json',ROOT/'data/Sanctuary/Balance/generic-pet-search-monster-links.json',source,treePath,skills,policyPath,decisionPath,primary,fixture]
 result=dict(referenceVersion='3.3.93847',component='Current species Revive flags and complete older eligibility/release/owner references',processMemoryRead=False,handlerEnabled=False,legacyBodiesExecuted=False,modernCodeEquivalenceClaimed=False,
  verified=dict(secondaryRows=623,secondaryStride=296,compiledTextReviveMatches=623,allowedSecondaryRows=391,reviveFlagByte=5,reviveFlagMask=1,completeLegacyRoutines=3),legacyCommit=tree['sha'],routineHashes={name:digest(body.encode()) for name,body in blocks.items()},
  pinnedSkill=dict(skillId=95,start=21,release=58,mana=45,baseLifePercent=200,durationFrames=4500,velocityPercent=50,cap='effective rank'),sanctuaryLifetimePolicy=policy[0],artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))},
  established=['All623current compiled byte5/bit0 values match the owned text revive column.391secondary rows permit the species gate. Current primary-to-secondary links are reused;the documented text hcIdx/compiled ordinal differences are not treated as identities.',
   'The complete older start21requires a monster,revivable secondary row,dead/consumable state and revived-AI permission. The complete older release58modifies the same target rather than allocating a generic replacement. It rolls inclusive source base HP using the target seed,downscales HP/level for a lower-level caster,and then applies summon passives,owner conversion,cap registration and AI updates. No-free-room does not return failure before those later steps.',
   'The complete older owner helper records ownership,requests AI think at current frame+15,sets revive state and schedules unique-mod21expiry for positive duration. Current skill95pins4500frames/180seconds,45mana,200base life percent and50velocity percent. Passive/stat/proc and native world behavior are not established by reading these bodies.',
   'The user-approved Sanctuary lifetime overrides the source expiry:maximum age is null and no expiration event is scheduled. Revives end on their own death,Necromancer death or replacement. Functional AI/combat/status timers remain independent. This policy does not enable an incomplete actor.'],
  remaining=['This reference analysis does not execute the older routines or establish modern executable equivalence. Native species/identity integration,full start/release policy,revived AI permission,in-place native reconstruction,full stat/passive/special-ability transfer,GUID reservation and complete lifecycle remain required. Species permission is necessary,not sufficient,for a successful cast.'])
 p=ROOT/'tools/reference/revive-species-data-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Revive species:623compiled/text flags,391allowed secondary rows;3complete older routine references;learned skill remains gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
