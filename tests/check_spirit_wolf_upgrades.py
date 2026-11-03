#!/usr/bin/env python3
"""Approved cold-only wolf upgrades with the existing Blood wound regression."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/spirit-wolf-upgrades',
        extra_files={'open-wounds-regen-source-cases.json':ROOT/'.local/class-skills/open-wounds-regen-source/cases.json',
            **{name+'-source-probe.json':ROOT/'.local/class-skills'/(name+'-source-probe.json') for name in ('spirit-wolf-hit','spirit-wolf-rate')}},
        extra_families=('spirit-wolf-upgrades','npc-open-wounds'),
        extra_manifest_names={'spirit-wolf-upgrades':'spirit-wolf-upgrades-fixtures.json','npc-open-wounds':'npc-open-wounds-fixtures.json'},
        extra_checks=('spirit-wolf-hit-check','spirit-wolf-rate-check','spirit-wolf-upgrades-live-check'),
        extra_connected_checks=('spirit-wolf-upgrades-live-check',),
        extra_wait_results={'spirit-wolf-upgrades-live-check':'spirit-wolf-upgrades-live-result'},
        extra_inputs=(Path(__file__).resolve(),))
