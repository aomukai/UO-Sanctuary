#!/usr/bin/env python3
"""Verify prepared Poison Dagger contact with retained composition, Iron and Venom checks."""
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    check(stage=ROOT/'.local/class-skills/poison-dagger-attack',extra_files={
        'poison-dagger-source-cases.json':ROOT/'.local/class-skills/poison-dagger-source/cases.json',
        'npc-high-level-resource-source-cases.json':ROOT/'.local/class-skills/npc-high-level-resources-source/cases.json',
        'item-property-writers-source-probe.json':ROOT/'.local/class-skills/item-property-writers-source-probe.json',
        'npc-impact-allocation-source-probe.json':ROOT/'.local/class-skills/npc-impact-allocation-source-probe.json',
        'npc-equipment-damage-source-cases.json':ROOT/'.local/class-skills/npc-equipment-damage-source/cases.json',
        'npc-equipment-elements-source-cases.json':ROOT/'.local/class-skills/npc-equipment-elements-source/cases.json',
        'venom-damage-source-probe.json':ROOT/'.local/class-skills/venom-damage-source-probe.json',
        'npc-equipment-critical-source-cases.json':ROOT/'.local/class-skills/npc-equipment-critical-source/critical-cases.json',
        'npc-equipment-seed-source-cases.json':ROOT/'.local/class-skills/npc-equipment-critical-source/seed-cases.json',
        'monster-weapon-phases-source-probe.json':ROOT/'.local/class-skills/monster-weapon-phases-source-probe.json',
        'goatman-npc-release-source-probe.json':ROOT/'.local/class-skills/goatman-npc-release-source-probe.json',
        'melee-result-source-probe.json':ROOT/'.local/class-skills/melee-result-source-probe.json',
        'iron-golem-birth-graph-source-cases.json':ROOT/'.local/class-skills/iron-golem-birth-graph-source/cases.json',
        'iron-golem-body-source-cases.json':ROOT/'.local/class-skills/iron-golem-body-source/cases.json',
        'necromancer-pet-ai-source-probe.json':ROOT/'.local/class-skills/necromancer-pet-ai-source-probe.json',
        'iron-golem-retaliation-source-probe.json':ROOT/'.local/class-skills/iron-golem-retaliation-source-probe.json'},
        extra_families=('poison-dagger-attack','poison-dagger','venom','iron-golem-cast','item-property-writers','iron-golem-birth-graph','iron-golem-body','iron-golem-world','iron-golem-retaliation','golem-roster','monster-ordered-release','monster-return','prepared-weapon-event','npc-weapon-preparation','npc-equipment-damage','npc-equipment-elements','npc-equipment-poison','npc-equipment-critical','npc-impact-allocation'),
        extra_checks=('npc-high-level-resource-check','iron-golem-ordered-contact-check','prepared-native-damage-check','monster-weapon-phases-check','monster-contact-check','melee-result-check','necromancer-pet-ai-check','iron-golem-retaliation-expiry'),extra_live_commands=('iron-golem-world-live',),extra_target_input_commands=('poison-dagger-right','poison-dagger-quick','iron-golem-right','iron-golem-quick'),extra_wait_results={'monster-return-check':'monster-return-result','venom-check':'venom-result','venom-reload-check':'venom-reload-result'},extra_inputs=(Path(__file__).resolve(),ROOT/'.local/class-skills/monster-return/classic.json',ROOT/'.local/class-skills/monster-return/ubers.json'))
