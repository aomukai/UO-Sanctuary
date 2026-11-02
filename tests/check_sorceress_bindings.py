#!/usr/bin/env python3
"""Sorceress binding acceptance with retained Teleport/Hydra and spell scopes."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_player_teleport import configuration as teleport
def configuration(full=False):
 c=teleport(full);c['stage']=ROOT/'.local/class-skills/sorceress-bindings'
 c['extra_families']+=('sorceress-binding',)
 c['extra_files']['sorceress-binding-reference.json']=ROOT/'tools/reference/sorceress-binding-reference.json'
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/sorceress-binding-reference.json')
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
