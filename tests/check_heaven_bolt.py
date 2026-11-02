#!/usr/bin/env python3
"""FoH child typed damage and shared source flight, retaining parent/Holy Bolt."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_heaven_fist_damage import configuration as parent

def configuration(full=False):
 c=parent(full);c['stage']=ROOT/'.local/class-skills/heaven-bolt'
 c['extra_families']+=('heaven-bolt',)
 c['extra_manifest_names']['heaven-bolt']='heaven-bolt-checks.json'
 c['extra_wait_results']['heaven-bolt-reload-check']='heaven-bolt-reload-result'
 c['extra_connected_checks']=c.get('extra_connected_checks',())+('heaven-bolt-reload-check',)
 c['extra_target_input_commands']+=tuple('heaven-bolt-'+mode for mode in ('left','right','quick'))
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
