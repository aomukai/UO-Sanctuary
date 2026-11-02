#!/usr/bin/env python3
"""Holy Bolt source do1 movement,ally-aware piercing and saved RNG."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_holy_bolt_damage import configuration as damage

def configuration(full=False):
 c=damage(full);c['stage']=ROOT/'.local/class-skills/holy-bolt-flight'
 c['extra_families']+=('holy-bolt-flight',)
 c['extra_manifest_names']['holy-bolt-flight']='holy-bolt-flight-checks.json'
 c['extra_wait_results'].update({'holy-bolt-flight-check':'holy-bolt-flight-result','holy-bolt-flight-reload-check':'holy-bolt-flight-reload-result'})
 c['extra_connected_checks']=c.get('extra_connected_checks',())+('holy-bolt-flight-reload-check',)
 c['extra_target_input_commands']+=tuple('holy-bolt-flight-'+mode for mode in ('left','right','quick'))
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
