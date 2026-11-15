#!/usr/bin/env python3
"""Current combined Necromancer checks under the no-charms/no-runewords scope.

Reuse cached reference fixtures; do not rerun old source compilers or read a
newer D2R installation. Dedicated edge-case receipts remain distinct evidence.
"""
import os
from pathlib import Path
from check_necromancer_current_acceptance import configuration
from check_iron_golem_birth import ROOT,check

def planned_configuration():
    c=configuration();c['stage']=ROOT/'.local/class-skills/necromancer-planned-acceptance'
    def add(key,values):c[key]=tuple(dict.fromkeys((*c.get(key,()),*values)))
    add('extra_families',('bone-armor-rule','raised-minions','native-corpse','native-corpse-difficulties','native-lich-revive'))
    add('extra_checks',('raised-minions-live-check','native-lich-revive-live-check'))
    add('extra_connected_checks',('raised-minions-live-check','native-lich-revive-live-check'))
    for name in('raised-minions-live-check','native-lich-revive-live-check'):c['extra_wait_results'][name]=name.replace('-check','-result')
    inputs=tuple('native-corpse-'+mode for mode in('ce-right','poison-quick','nightmare-ce-right','hell-poison-quick'))+tuple('native-lich-revive-'+mode for mode in('normal-right','normal-quick','nightmare-right','hell-quick'))
    add('extra_target_input_commands',inputs)
    for name in inputs:c['extra_input_timeouts'][name]=12
    add('extra_inputs',(Path(__file__).resolve(),ROOT/'tools/reference/item-content-scope-decision.json',ROOT/'tools/reference/native-lich-revive-world-rule-approval.json'))
    return c

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**planned_configuration())
