#!/usr/bin/env python3
"""Original saved skeleton reconnect with retained automatic obstacle scopes."""
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    check(stage=ROOT/'.local/class-skills/pet-reconnect',extra_files={
        'iron-golem-birth-graph-source-cases.json':ROOT/'.local/class-skills/iron-golem-birth-graph-source/cases.json',
        'iron-golem-body-source-cases.json':ROOT/'.local/class-skills/iron-golem-body-source/cases.json',
        'necromancer-pet-ai-source-probe.json':ROOT/'.local/class-skills/necromancer-pet-ai-source-probe.json',
        'skeleton-birth-source-cases.json':ROOT/'.local/class-skills/skeleton-birth-source/cases.json',
        'skeleton-roster-source-cases.json':ROOT/'.local/class-skills/skeleton-roster-source/cases.json',
        'astar-path-source-cases.json':ROOT/'.local/class-skills/astar-path-source/cases.json',
        'toward-path-source-probe.json':ROOT/'.local/class-skills/toward-path-source-probe.json'},
        extra_families=('iron-golem-birth-graph','iron-golem-body','iron-golem-world','skeleton-contact','skeleton-world','skeleton-birth','skeleton-roster','skeleton-combat','summon-difficulty','pet-obstacle','pet-reconnect'),
        extra_checks=('necromancer-pet-ai-check','necromancer-pet-targets-check','astar-path-check','toward-path-check'),
        extra_live_commands=('iron-golem-world-live','skeleton-world-live','skeleton-combat-live','pet-obstacle-live'),
        extra_manifest_names={'pet-obstacle':'pet-obstacle-fixture.json','pet-reconnect':'pet-reconnect-fixture.json'},
        extra_connected_checks=('pet-reconnect-reload-check',),extra_wait_results={'pet-reconnect-reload-check':'pet-reconnect-reload-result'},
        extra_target_input_commands=('skeleton-birth-right','skeleton-birth-quick'),extra_inputs=(Path(__file__).resolve(),))
