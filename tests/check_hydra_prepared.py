#!/usr/bin/env python3
"""Hydra captured birth/frame components with retained class regression."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_player_frozen_orb import configuration as frozen
def configuration(full=False):
 c=frozen(full);c['stage']=ROOT/'.local/class-skills/hydra-prepared'
 c['extra_families']+=('hydra-prepared',)
 for n in ('creation','ai','clock','damage','bolt-creation'):
  c['extra_files']['hydra-'+n+'-source-cases.json']=ROOT/('.local/class-skills/hydra-'+n+'-source/cases.json')
 c['extra_inputs']+=(Path(__file__).resolve(),*(ROOT/('tools/reference/hydra-'+n+'-source-evidence.json') for n in ('creation','ai','clock','damage','bolt-creation')))
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
