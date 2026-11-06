#!/usr/bin/env python3
"""Pin whole-item Necromancer grant metadata and the exact activation delta."""
import argparse,hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];PARENT='91f3ede72ddb8b0902369e86483663cb6d6dec92';KEYS=['Maelstromwrath','Umes Lament',"Blackbog's Sharp",'Boneshade']
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def review(check=False):
 inputs={}
 def read(rel):
  p=ROOT/rel;inputs[rel]=sha(p);return json.loads(p.read_text())
 prefix='tools/reference/d2r-named-equipment/';pins=read(prefix+'sources.json');tables={}
 for name in ['uniqueitems','skills']:
  rel='json/'+name+'.json';tables[name]=read(prefix+rel);assert inputs[prefix+rel]==pins['files'][rel]['sha256']
 definitions=read('data/Sanctuary/Balance/unique-items.json');bindings=read('data/Sanctuary/Balance/named-skill-bindings.json');by={key:b for b in bindings for key in [str(b['sourceId']),b['sourceKey']]}
 active=read('data/Sanctuary/Balance/named-active-skills.json');prior=json.loads(subprocess.check_output(['git','show',PARENT+':data/Sanctuary/Balance/named-active-skills.json'],cwd=ROOT))
 selected=[next(d for d in definitions if d['sourceKey']==key)for key in KEYS];rows=[];talents=set();count=0
 for item in selected:
  assert item['sourceFields']==tables['uniqueitems'][str(item['sourceId'])] and item['sourceSpawnable']
  grants=[]
  for p in item['properties']:
   if p['code']!='skill':continue
   raw=item['sourceFields'];slot=p['slot'];assert raw['prop'+slot]=='skill' and raw['par'+slot]==p['parameter'] and raw['min'+slot]==p['sourceMin'] and raw['max'+slot]==p['sourceMax']
   b=by[str(p['parameter'])];s=tables['skills'][str(b['sourceId'])];assert b['characterClass']=='Necromancer' and b['classCode']==s['charclass']=='nec' and b['sourceKey']==s['skill']
   talents.add(b['talentId']);count+=1;grants.append(dict(sourceId=b['sourceId'],sourceKey=b['sourceKey'],talentId=b['talentId'],minimum=p['sourceMin'],maximum=p['sourceMax']))
  rows.append(dict(id=item['id'],sourceId=item['sourceId'],sourceKey=item['sourceKey'],base=item['baseCode'],requiredLevel=item['requiredLevel'],grants=grants))
 assert count==14 and len(talents)==13 and len(set(active)-set(prior))==12 and set(active)==set(prior)|talents and len(active)==len(set(active))==16
 for rel in ['src/Sanctuary.Content/NamedSkills.cs','src/Sanctuary.Content/NamedGear.cs','src/Sanctuary.Content/SkillMechanics.cs','src/Sanctuary.Content/SkillBindings.cs','src/Sanctuary.Content/SkillBindingNetwork.cs','src/Sanctuary.Content/NecromancerCombat.cs','src/Sanctuary.Content/NecromancerSpellMissiles.cs','src/Sanctuary.Content/NecromancerEffects.cs','tests/Sanctuary.Integration/NecromancerItemSkillChecks.cs',str(Path(__file__).resolve().relative_to(ROOT))]:inputs[rel]=sha(ROOT/rel)
 result=dict(referenceVersion='3.3.93847',parent=PARENT,inputs=inputs,items=rows,wholeItems=4,propertyGrants=14,distinctSkills=13,activatedBindings=sorted(set(active)-set(prior)),expectedAvailabilityDelta=[x['id']for x in rows],
  scope=['Exact current pinned unique source rows and all14numeric/textual specific-skill parameters bind to Necromancer skill IDs. Only12additional named-active bindings change;prior3Necromancer/Sorceress Teleport entries remain.',
         'Existing rank consumer adds direct grants and general bonuses only to learned/granted class skills. Existing hard-rank providers remain separate. This is current metadata and consumer binding review;native cast/ownership/snapshot acceptance is separate.'],
  limits=['No new source-engine routine or modern executable certificate. Arm of King Leoric retains gethit-skill and unapproved Mage grant gates;Marrowwalk retains charges. Other class item activations and Necromancer barrier/Sunder/world/client gaps remain open.'])
 target=ROOT/'tools/reference/necromancer-item-skills-reference.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==text
 else:target.write_text(text)
 print('Necromancer item grants:4complete pinned uniques/14properties/13skills;12activation additions;current source metadata and existing consumers bound;no executable parity claim.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');review(p.parse_args().check)
