#!/usr/bin/env python3
"""Saved supplied Valkyrie body, existing creation and shared demon regressions."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_leap_attack import configuration as prior

def configuration(retained=False):
 c=prior(True)if retained else dict(extra_families=(),extra_checks=(),extra_files={},extra_inputs=(),extra_manifest_names={})
 c['stage']=ROOT/'.local/class-skills'/('valkyrie-body-retained'if retained else'valkyrie-body')
 c['extra_families']+=('valkyrie-body','valkyrie-creation','goatman-body','tainted-body','defiler-body','goatman-foundation')
 for n in ['valkyrie-creation','goatman-formula','goatman-creation']:
  c['extra_files'][n+'-source-probe.json']=ROOT/'.local/class-skills'/(n+'-source-probe.json')
 c['extra_files']['goatman-tooltip-observation.json']=ROOT/'.local/class-skills/goatman-tooltip-observation.json'
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
