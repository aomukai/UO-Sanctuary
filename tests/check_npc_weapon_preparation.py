#!/usr/bin/env python3
"""Verify offensive NPC snapshots and native ordered damage credit with retained original/body/world/retaliation consumers."""
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    check(stage=ROOT/'.local/class-skills/npc-weapon-preparation',extra_files={
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
        extra_families=('iron-golem-birth-graph','iron-golem-body','iron-golem-world','iron-golem-retaliation','golem-roster','monster-ordered-release','monster-return','prepared-weapon-event','npc-weapon-preparation','npc-equipment-damage','npc-equipment-elements','npc-equipment-poison','npc-equipment-critical'),
        extra_checks=('monster-weapon-phases-check','monster-contact-check','melee-result-check','necromancer-pet-ai-check','iron-golem-retaliation-expiry'),extra_live_commands=('iron-golem-world-live',),extra_wait_results={'monster-return-check':'monster-return-result'},extra_inputs=(Path(__file__).resolve(),ROOT/'.local/class-skills/monster-return/classic.json',ROOT/'.local/class-skills/monster-return/ubers.json'))
