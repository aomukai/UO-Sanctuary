#!/usr/bin/env python3
"""Saved Grimoire captures with retained Defiler, Goatman and Hydra regression."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/grimoire-capture-focus',
        extra_files={'grimoire-armor-source.txt':ROOT/'.local/class-skills/warlock-hands-reference/armor.txt','grimoire-automatic-source.txt':ROOT/'.local/class-skills/warlock-hands-reference/automagic.txt','tainted-aura-callback-cases.json':ROOT/'.local/class-skills/tainted-aura-source/callback.json','tainted-aura-pulse-cases.json':ROOT/'.local/class-skills/tainted-aura-source/pulse.json','innate-npc-rank-source-cases.json':ROOT/'.local/class-skills/oak-skill-rank-source-probe.json','tainted-cast-source-cases.json':ROOT/'.local/class-skills/tainted-fireball-cast-source/cases.json','tainted-formula-source-cases.json':ROOT/'.local/class-skills/tainted-foundation-source/formula.json',
                     'tainted-fireball-source-cases.json':ROOT/'.local/class-skills/tainted-foundation-source/fireball.json',
                     'goatman-creation-source-probe.json':ROOT/'.local/class-skills/goatman-creation-source-probe.json'},
        extra_families=('grimoire-capture','defiler-body','demon-incoming','tainted-aura','tainted-ranks','tainted-casting','tainted-fireball','tainted-body','tainted-foundation','goatman-body','hydra-world'),
        extra_connected_checks=('tainted-aura-reload-check','tainted-ranks-reload-check','tainted-casting-reload-check','tainted-fireball-reload-check','hydra-world-reload-check',),
        extra_wait_results={'tainted-aura-reload-check':'tainted-aura-reload-result','tainted-ranks-reload-check':'tainted-ranks-reload-result','tainted-casting-reload-check':'tainted-casting-reload-result','tainted-fireball-reload-check':'tainted-fireball-reload-result','hydra-world-reload-check':'hydra-world-reload-result'},
        extra_target_input_commands=('hydra-world-right','hydra-world-quick'),extra_inputs=(Path(__file__).resolve(),))
