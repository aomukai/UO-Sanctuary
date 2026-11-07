#!/usr/bin/env python3
"""Original Valkyrie external elemental buffs,source packets and saved contact."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_valkyrie_town import configuration as prior
def configuration(retained=False,isolate=False,shared=False):
 c=prior(retained,isolate,shared)
 c['stage']=ROOT/'.local/class-skills'/('valkyrie-buffs-shared'if shared else'valkyrie-buffs-isolate'if isolate else'valkyrie-buffs-retained'if retained else'valkyrie-buffs')
 if not shared:
  c['extra_families']+=('valkyrie-buffs',)
  c['extra_files']['valkyrie-buffs-source-cases.json']=ROOT/'.local/class-skills/valkyrie-buffs-source/cases.json'
 c.setdefault('extra_check_timeouts',{}).update({'valkyrie-buffs-check':120,'valkyrie-buffs-reload-check':120})
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/probe_valkyrie_buffs.py')
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');p.add_argument('--isolate',action='store_true');p.add_argument('--shared',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained,a.isolate,a.shared))
