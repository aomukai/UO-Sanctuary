#!/usr/bin/env python3
"""Read original source-graph controls and current property-to-stat identities."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-contact-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);ref=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((ref/'sources.json').read_text());p=ref/'json/properties.json';assert sha(p)==manifest['files']['json/properties.json']['sha256'];properties=json.loads(p.read_text())
 costs=json.loads((ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json').read_text());ids={r['Name']:r['Id']for r in costs}
 codes=['thorns','knock','heal-kill','rip','howl','stupidity','dmg-to-mana','dmg-ac','light-thorns','freeze','openwounds','crush','mana-kill','demon-heal','slow','magic-reduction','physical-reduction','ac-miss','ac-hth','res-pois-len','nofreeze','half-freeze','abs-fire%','abs-fire','abs-ltng%','abs-ltng','abs-cold%','abs-cold','pierce-fire','pierce-ltng','pierce-cold','pierce-pois','pierce-mag']
 aliases={'magic-reduction':'red-mag','physical-reduction':'red-dmg'}
 keys={code:ids[properties[aliases.get(code,code)]['stat1']]for code in codes};inputs=BASE/'valkyrie-resources-source/cases.json';rows=json.loads(inputs.read_text());cases=[]
 for c in rows:
  if c['Variant']!=0:continue
  stats=dict(c['Stages'][0][2]);blocked=[k for k,v in stats.items()if v and k>>16 in [195,196,197,198,199,201]]
  cases.append(dict(Case=c['Case'],Supported=not blocked,Unsupported=blocked,Bonuses={code:stats.get(stat<<16,0)for code,stat in keys.items()}))
 assert len(cases)==57 and sum(c['Supported']for c in cases)==30
 out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',cases=57,supported=30,unsupported=27,propertyStats=keys,inputs={str(q.relative_to(ROOT)):sha(q)for q in [p,ref/'sources.json',ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json',inputs,ROOT/'tools/reference/valkyrie-resources-source-evidence.json',Path(__file__).resolve()]},fixture=dict(path=str(out.relative_to(ROOT)),sha256=sha(out)),scope='Current property stat1 identities and previously independently source-read original graph values. No item roll filtering or new source callback/executable proof;existing complete allocation/result/event kernels remain the reference.')
 (ROOT/'tools/reference/valkyrie-contact-cases-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n');print('Original Valkyrie contact controls:57unfiltered loadouts,30numeric-event compatible/27explicit proc refusals;33current property stat identities.')
if __name__=='__main__':main()
