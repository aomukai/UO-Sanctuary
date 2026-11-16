#!/usr/bin/env python3
"""Complete Warpspear direct-grant acquisition,real consumers and saved staff."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/warpspear-grants'
def configuration():
    names=('warpspear-grants-check','warpspear-grants-reload-check')
    return dict(stage=STAGE,extra_families=('warpspear-grants',),extra_checks=('warpspear-readiness-check',),extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_target_input_commands=tuple('warpspear-'+m for m in ('tk-right','tk-quick','es-right','es-quick')),extra_ground_input_commands=('warpspear-es-right','warpspear-es-quick'),extra_files={'warpspear-before-readiness.json':ROOT/'tools/reference/named-item-runtime-audit.json'},extra_connection_settle=.15,extra_inputs=(Path(__file__).resolve(),ROOT/'tests/check_assassin_planned_acceptance.py',ROOT/'tools/reference/named-item-runtime-audit.json'))
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
