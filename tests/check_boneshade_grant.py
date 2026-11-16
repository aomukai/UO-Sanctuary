#!/usr/bin/env python3
"""Real complete Boneshade acquisition,equipment and item-granted barrier inputs."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/boneshade-grant'
def configuration():
    names=('boneshade-grant-check','boneshade-grant-reload-check')
    inputs=('boneshade-grant-right','boneshade-grant-quick')
    return dict(stage=STAGE,extra_families=('boneshade-grant',),extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_target_input_commands=inputs,extra_ground_input_commands=inputs,extra_connection_settle=.15,extra_inputs=(Path(__file__).resolve(),ROOT/'tests/check_assassin_planned_acceptance.py',ROOT/'tools/reference/bone-barrier-world-rule-approval.json'))
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
