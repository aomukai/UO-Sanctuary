#!/usr/bin/env python3
"""Check distinct Entropy cloud rank channels without enabling unknown do41."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_double_swing_sequence import configuration as prior
def configuration(retained=False):
 c=prior(True)if retained else dict(extra_families=(),extra_inputs=(),extra_files={},extra_manifest_names={})
 c['stage']=ROOT/'.local/class-skills'/('entropy-clouds-retained'if retained else'entropy-clouds')
 c['extra_families']+=('entropy-cloud','miasma-packets')
 c['extra_manifest_names']['miasma-packets']='miasma-packet-fixtures.json'
 c['extra_files']['miasma-packets-source-probe.json']=ROOT/'.local/class-skills/miasma-packets-source-probe.json'
 if not retained:
  c['extra_families']+=('attract-cast',)
  c['extra_connected_checks']=('attract-cast-reload-check',)
  c['extra_target_input_commands']=('attract-cast-right','attract-cast-quick')
  c['extra_wait_results']={'attract-cast-reload-check':'attract-cast-reload-result'}
  c['extra_input_timeouts']={'attract-cast-right':20,'attract-cast-quick':20}
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
