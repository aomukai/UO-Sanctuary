# Summon animations

The original sprite sheets each have five facings across and five poses down. The poses are standing, two walking strides, attack or spell release, and collapse. The columns face down, down-left, left, up-left, and up on screen. ClassicUO mirrors the other facings.

| Body | Appearance |
| --- | --- |
| 2008 | Skeleton with sword and shield |
| 2009 | Skeleton with sword, shield, and damaged chain mail |
| 2010 | Upright skeletal mage with a narrow-brimmed wizard hat |
| 2011 | Upright skeletal mage with a narrow-brimmed wizard hat and staff |
| 2012 | Upright skeletal mage with a narrow-brimmed wizard hat, staff, and robe |
| 2013 | Blood golem |
| 2014 | Iron golem |
| 2015 | Fire golem |
| 2016 | Valkyrie in engraved silver plate with a long spear |

The five skeleton sheets share a cool ivory bone palette. Run `python3 tools/build_summon_animations.py` to rebuild the MUL and IDX archives with Pillow. The builder crops the sheets, registers the figures to a ground anchor, and assembles ten walking frames and four frames for each attack, cast, impact, or fall. The client loads the archive from `Data/Sanctuary/Animations` and selects a body from the appearance packet. The body order is shared with [SkeletonAppearance.cs](../../shared/SkeletonAppearance.cs).

[Skeleton and golem ImageGen prompts](prompts.md) · [Valkyrie ImageGen prompts](valkyrie-prompts.md)
