#!/usr/bin/env python3
"""Approved Grizzly unlocks with shared player/NPC CB and wound regression."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=('weapon-crushing','grizzly-sequence','grizzly-rate','grizzly-smite-sequence','bear-smite')
    check(stage=ROOT/'.local/class-skills/grizzly-upgrades',
        extra_files={'open-wounds-regen-source-cases.json':ROOT/'.local/class-skills/open-wounds-regen-source/cases.json',
            **{n+'-source-probe.json':ROOT/'.local/class-skills'/(n+'-source-probe.json') for n in names}},
        extra_families=('grizzly-upgrades','grizzly-world','npc-crushing','weapon-crushing','npc-open-wounds','spirit-wolf-upgrades'),
        extra_checks=('iron-golem-ordered-contact-check','grizzly-upgrades-live-check','grizzly-sequence-check','bear-smite-check'),
        extra_connected_checks=('grizzly-upgrades-live-check',),
        extra_wait_results={'grizzly-upgrades-live-check':'grizzly-upgrades-live-result'},
        extra_inputs=(Path(__file__).resolve(),))
