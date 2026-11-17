#!/usr/bin/env python3
"""Fill actual Terror right/quick and Confuse quick input coverage without rebuilding."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_necromancer_planned_acceptance import planned_configuration
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/necromancer-terror-input'
def configuration():
    files=dict(planned_configuration()['extra_files']);files['hellslayer-before-readiness.json']=ROOT/'tools/reference/named-item-pre-proc-baseline.json'
    inputs=(Path(__file__).resolve(),ROOT/'tools/reference/ordinary-cold-corpse-adoption-native-evidence.json',ROOT/'tools/review_necromancer_input_coverage.py')
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests')
    return dict(stage=STAGE,extra_families=('terror-cast','hellslayer-readiness'),extra_files=files,extra_target_input_commands=('terror-cast-right','terror-cast-quick','confuse-cast-quick'),extra_input_timeouts={'terror-cast-right':12,'terror-cast-quick':12,'confuse-cast-quick':15},extra_inputs=tuple(dict.fromkeys((*inputs,*helpers))))
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
