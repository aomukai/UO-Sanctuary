#!/usr/bin/env python3
"""Full unfiltered Valkyrie resources plus existing generic profile controls."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_valkyrie_numeric import configuration as prior
def configuration(retained=False):
 c=prior(retained);c['stage']=ROOT/'.local/class-skills'/('valkyrie-resources-retained'if retained else'valkyrie-resources')
 c['extra_families']+=('generated-npc-live-resources','valkyrie-resources')
 c['extra_files'].update({'valkyrie-resources-cases.json':ROOT/'.local/class-skills/valkyrie-resources-source/cases.json',
  'generated-npc-resources-cases.json':ROOT/'.local/class-skills/generated-npc-resources-source/cases.json',
  'generated-npc-legacy-resource-fixtures.json':ROOT/'.local/class-skills/generated-npc-live-resources-compact/generated-npc-live-resources-fixtures.json'})
 c['extra_check_timeouts']={'valkyrie-resources-check':300,'valkyrie-resources-reload-check':180}
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
