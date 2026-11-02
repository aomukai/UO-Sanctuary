#!/usr/bin/env python3
"""Might and shared aura persistence,retaining sibling aura and save coordinators."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_holy_shield_cast import configuration as parent

def configuration(full=False):
 c=parent(full);c['stage']=ROOT/'.local/class-skills/might-aura'
 if not full:
  f=parent(True)
  for name in ('paladin-aura','paladin-resistance','paladin-resource','paladin-travel','paladin-element','paladin-offense','paladin-thorns','paladin-conviction','sanctuary-aura'):
   assert name in f['extra_families'],name
   if name not in c['extra_families']:c['extra_families']+=(name,)
   if name in f['extra_manifest_names']:c['extra_manifest_names'][name]=f['extra_manifest_names'][name]
   for command in (name+'-check',name+'-reload-check'):
    if command in f['extra_wait_results']:c['extra_wait_results'][command]=f['extra_wait_results'][command]
    if command in f['extra_connected_checks'] and command not in c['extra_connected_checks']:c['extra_connected_checks']+=(command,)
  c['extra_files'].update(f['extra_files']);c['extra_inputs']+=f['extra_inputs']
 c['extra_families']+=('might-aura',)
 c['extra_manifest_names']['might-aura']='might-aura-checks.json'
 c['extra_wait_results']['might-aura-reload-check']='might-aura-reload-result'
 c['extra_connected_checks']+=('might-aura-reload-check',)
 names=tuple('might-aura-'+mode for mode in ('right','quick'))
 c['extra_target_input_commands']+=names;c['extra_instant_input_commands']=names
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/probe_might_callback.py',ROOT/'tools/reference/might-callback-source-evidence.json',ROOT/'.local/class-skills/might-callback-source/cases.json')
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
