#!/usr/bin/env python3
"""Isolate the retained Confuse learned-input/AI/expiry regression."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
def configuration(baseline=False):
 return dict(stage=ROOT/'.local/class-skills'/('valkyrie-damage-confuse-baseline'if baseline else'valkyrie-damage-confuse'),extra_families=('confuse-cast',),
  extra_target_input_commands=('confuse-cast-right','confuse-cast-quick'),extra_input_timeouts={'confuse-cast-right':18,'confuse-cast-quick':18},extra_inputs=(Path(__file__).resolve(),))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--baseline',action='store_true');a=p.parse_args();os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.baseline))
