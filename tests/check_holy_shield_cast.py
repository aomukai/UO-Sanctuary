#!/usr/bin/env python3
"""Holy Shield paid start,do18 rank rules and original saved state/actions."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_heaven_bolt import configuration as parent

def configuration(full=False):
 c=parent(full);c['stage']=ROOT/'.local/class-skills/holy-shield-cast'
 if not full:
  # The coordinator must exercise its retained Teleport preparation even in
  # focused acceptance; the earlier reduced cohort could not detect a skip.
  f=parent(True)
  c['extra_families']+=('player-teleport',)
  c['extra_manifest_names']['player-teleport']=f['extra_manifest_names'].get('player-teleport','player-teleport-fixtures.json')
  c['extra_wait_results']['player-teleport-reload-check']=f['extra_wait_results']['player-teleport-reload-check']
  c['extra_connected_checks']+=('player-teleport-reload-check',)
  c['extra_target_input_commands']+=tuple(n for n in f['extra_target_input_commands'] if n.startswith('player-teleport-'))
  c['extra_ground_input_commands']=c.get('extra_ground_input_commands',())+tuple(n for n in f.get('extra_ground_input_commands',()) if n.startswith('player-teleport-'))
  c['extra_files'].update(f['extra_files']);c['extra_inputs']+=f['extra_inputs']
 c['extra_families']+=('holy-shield-cast',)
 c['extra_manifest_names']['holy-shield-cast']='holy-shield-cast-checks.json'
 c['extra_target_input_commands']+=tuple('holy-shield-cast-'+mode for mode in ('right','quick'))
 c['extra_files']['holy-shield-cast-source-cases.json']=ROOT/'.local/class-skills/holy-shield-cast-source/cases.json'
 c['save_command']=c['save_result']='holy-shield-cast-save'
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/probe_holy_shield_cast.py',ROOT/'tools/reference/holy-shield-cast-source-evidence.json',ROOT/'.local/class-skills/holy-shield-cast-source/cases.json')
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
