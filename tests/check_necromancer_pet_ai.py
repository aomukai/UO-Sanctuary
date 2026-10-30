#!/usr/bin/env python3
"""Compare complete prepared NecroPet policy in an isolated native boot."""
import export_runtime_audits as native

if __name__ == '__main__':
    native.export(native.ROOT / '.local/class-skills/necromancer-pet-ai',
        extra_commands=['necromancer-pet-ai-check'], extra_probes=['necromancer-pet-ai'])
    print('NecroPet AI67:1598 native ordered-service/state/draw comparisons pass. Moving actor and learned Iron Golem remain separate.')
