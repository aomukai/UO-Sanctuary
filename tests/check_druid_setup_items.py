#!/usr/bin/env python3
"""Real PMH/defense-loss phases across all six form skills and retained procs."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_druid_action_proc import configuration as prior,OPEN
STAGE=ROOT/'.local/class-skills/druid-setup-items'
def configuration():
    c=prior();c.update(stage=STAGE,extra_families=(*c['extra_families'],'druid-setup-items'),extra_target_input_commands=(*c['extra_target_input_commands'],*(f'druid-setup-items-{skill}-{mode}' for skill in ('maul','hunger','rabies','fury') for mode in ('right','quick'))))
    modules=[]
    for m in tuple(sys.modules.values()):
        name=getattr(m,'__file__',None)
        if name:
            p=Path(name).resolve()
            if p.is_relative_to(ROOT/'tests') and p.suffix=='.py':modules.append(p)
    c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/druid-action-proc-native-evidence.json',ROOT/'tools/reference/weapon-start-source-review.json',*modules)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    try:
        with paced_clients():check(**configuration())
    finally:
        for client in OPEN:client.close()
