#!/usr/bin/env python3
"""Learned Clay world controller with retained skeleton/Iron worlds and historical contacts."""
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    check(stage=ROOT/'.local/class-skills/clay-golem-world',extra_files={
        'clay-golem-birth-source-cases.json':ROOT/'.local/class-skills/clay-golem-birth-source/cases.json',
        'clay-golem-slow-source-cases.json':ROOT/'.local/class-skills/clay-golem-slow-source/cases.json',
        'iron-golem-birth-graph-source-cases.json':ROOT/'.local/class-skills/iron-golem-birth-graph-source/cases.json',
        'iron-golem-body-source-cases.json':ROOT/'.local/class-skills/iron-golem-body-source/cases.json',
        'necromancer-pet-ai-source-probe.json':ROOT/'.local/class-skills/necromancer-pet-ai-source-probe.json',
        'skeleton-birth-source-cases.json':ROOT/'.local/class-skills/skeleton-birth-source/cases.json',
        'skeleton-roster-source-cases.json':ROOT/'.local/class-skills/skeleton-roster-source/cases.json'},
        extra_families=('clay-golem-world','clay-golem-contact','clay-golem-combat','clay-golem-slow','clay-golem-birth','golem-roster','iron-golem-birth-graph','iron-golem-body','iron-golem-world','skeleton-contact','skeleton-world','skeleton-birth','skeleton-roster','skeleton-combat','summon-difficulty'),
        extra_checks=('necromancer-pet-ai-check','necromancer-pet-targets-check'),
        extra_live_commands=('clay-golem-world-live','clay-golem-combat-live','iron-golem-world-live','skeleton-world-live','skeleton-combat-live'),
        extra_ground_input_commands=('clay-golem-world-right','clay-golem-world-quick'),
        extra_target_input_commands=('clay-golem-world-right','clay-golem-world-quick','skeleton-birth-right','skeleton-birth-quick'),extra_inputs=(Path(__file__).resolve(),))
