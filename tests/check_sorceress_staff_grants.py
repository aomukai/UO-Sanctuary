#!/usr/bin/env python3
"""Three complete Sorceress staffs,nine direct grants and retained spell damage."""
import os
import sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/sorceress-staff-grants'
def configuration():
    modes=('nova-right','frost-quick','blaze-quick','wall-right','ball-right','chain-quick','lightning-right','static-quick')
    names=('sorceress-staff-grants-check','sorceress-staff-grants-reload-check')
    return dict(stage=STAGE,extra_families=('sorceress-staff-grants',),extra_checks=('sorceress-staff-readiness-check',),extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_target_input_commands=tuple('sorc-staff-'+m for m in modes),extra_ground_input_commands=tuple('sorc-staff-'+m for m in ('nova-right','frost-quick','blaze-quick','wall-right','static-quick')),extra_files={'sorceress-staff-before-readiness.json':ROOT/'tools/reference/named-item-runtime-audit.json'},extra_connection_settle=.15,extra_inputs=(Path(__file__).resolve(),ROOT/'tests/check_assassin_planned_acceptance.py',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/'tools/reference/warpspear-grants-native-evidence.json'))
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    options=configuration()
    if '--chain-only' in sys.argv:
        options.update(stage=ROOT/'.local/class-skills/sorceress-staff-grants-chain-focus',extra_families=(),extra_checks=(),extra_connected_checks=(),extra_wait_results={},extra_target_input_commands=('sorc-staff-chain-quick',),extra_ground_input_commands=())
    with paced_clients():check(**options)
