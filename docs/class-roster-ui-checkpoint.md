# Eight-class creation UI checkpoint

Verified 2026-11-18 JST against the installed class package. The [retained receipt](../tools/reference/class-roster-ui-evidence.json) qualifies Warlock/Witch creation through the actual ClassicUO client. The pinned mechanical reference remains D2R Reign of the Warlock 3.3.93847; this check introduces no mechanical adaptation.

The existing class-name driver now visits all eight choices, selects both appearance sexes, and checks Back/reselection. Sixteen appearance summaries were visually inspected, including Warlock and Witch. Two disposable GUI runs submitted wire class 108 through ordinary character creation, one male and one female. Both entered the world with the correct native title and rendered talent heading. The screenshot named `paperdoll` captures world entry; it does not show an open paperdoll, so rendered paperdoll title placement is not qualified here. Native checks confirm the submitted sex, class, level, title, open talent gump and three distinct ten-skill trees. Both characters survived a native save/shutdown/reload with the same identity and heading. This is six native checks across four boots and four saves; it is not eight online class submissions or a combat playtest.

The integration-only assembly built with zero warnings/errors. Installed server, content, client and numerical data were reused. All 109 protected installation files, including the original saves, configuration, client profiles/data/settings and production assembly manifest, retain their hashes. The integration assembly is loaded only in disposable worlds; the installed build is unchanged. Existing class input/lifecycle evidence remains separate, and final effects/icons remain identified placeholders.

The first attempt passed Warlock creation but failed during shutdown:disabling ModernUO's ping service exposed its existing null shutdown callback. Exact driver sources, integration binary, artifact hashes, logs and screenshots are retained under the numbered failure. The second driver uses the established isolated UDP ping service and also preserves disposable saves/configuration on failure. Both variants then passed. No upstream/gameplay fix was required. The first failed temporary world itself was removed by the original driver's cleanup; its save is not claimed preserved.

Evidence is retained at `.local/class-skills/class-roster-ui` (an external archive alias). `source-inputs.json` binds the compiled fixture inputs; `driver-run-2.py` binds the revised orchestration; the installed package hashes, protected manifest, rendered screenshots, native logs and disposable saved worlds are retained separately.

```bash
python3 tools/record_class_roster_ui.py --check
```

Remaining:the final per-skill targeting/damage/recipient and approved pet-consumer audit, graphical combat qualification, and separately tracked effects/audio/icons. No class or goal completion certificate is issued by this roster check.
