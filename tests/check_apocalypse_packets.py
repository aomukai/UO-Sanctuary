#!/usr/bin/env python3
"""Focused approved Apocalypse endpoints, current shared roll and actual HP."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(stage=ROOT/'.local/class-skills/apocalypse-packets',extra_families=('apocalypse-impact','apocalypse-packets'),
  extra_files={'skill-resistance-cases.json':ROOT/'.local/class-skills/skill-resistance-source/cases.json',
               'apocalypse-maximum-cases.json':ROOT/'.local/class-skills/apocalypse-maximum-source/cases.json',
               'spell-packet-roll-cases.json':ROOT/'.local/class-skills/spell-packet-roll-source/cases.json'},
  extra_inputs=(ROOT/'tests/check_apocalypse_packets.py',ROOT/'tools/reference/spell-packet-roll-current-evidence.json',ROOT/'tools/reference/apocalypse-endpoints-approval.json',ROOT/'tools/reference/apocalypse-maximum-current-evidence.json'))
