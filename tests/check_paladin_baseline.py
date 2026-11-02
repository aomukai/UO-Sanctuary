#!/usr/bin/env python3
"""Retain the existing Paladin aura/melee/projectile/save cohort on the current build."""
import json,os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_sorceress_bindings import configuration as sorceress

FAMILIES=('paladin-aura','paladin-resource','paladin-resistance','paladin-travel',
 'paladin-offense','paladin-element','paladin-conviction','paladin-thorns','holy-shield',
 'sacrifice','smite','vengeance','conversion','charge','zeal','holy-bolt','blessed-hammer',
 'heaven-fist','sanctuary-aura','paladin-binding')
ASYNC=('paladin-aura','paladin-resource','paladin-resistance','paladin-travel',
 'paladin-offense','paladin-element','paladin-conviction','paladin-thorns','holy-shield',
 'holy-bolt','blessed-hammer','heaven-fist','sanctuary-aura')
def configuration(full=False):
 if full:c=sorceress(True)
 else:c=dict(extra_families=(),extra_checks=(),extra_wait_results={},extra_files={},extra_manifest_names={},extra_inputs=())
 c['stage']=ROOT/'.local/class-skills/paladin-baseline'
 c['extra_families']+=FAMILIES
 c['extra_files']['paladin-binding-reference.json']=ROOT/'tools/reference/paladin-binding-reference.json'
 for n in ASYNC:
  c['extra_wait_results'][n+'-check']=n+'-result';c['extra_wait_results'][n+'-reload-check']=n+'-reload-result'
 for n in ('smite','conversion'):c['extra_wait_results'][n+'-check']=n+'-result'
 c.setdefault('extra_manifest_names',{}).update({n:n+'-checks.json' for n in FAMILIES})
 c['extra_manifest_names']['paladin-binding']='paladin-binding-fixtures.json'
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/paladin-binding-reference.json')
 return c
if __name__=='__main__':
 c=configuration('--full' in sys.argv)
 if '--plan' in sys.argv:print(json.dumps(dict(families=c['extra_families'],waits=c['extra_wait_results']),indent=2))
 else:
  os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
