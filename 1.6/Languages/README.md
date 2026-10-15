# Languages

Nine languages ship with this mod: English, ChineseSimplified, French, German, Japanese, Polish,
PortugueseBrazilian, Russian and Spanish.

This file is a note for contributors. It is not shipped: `build.sh` stages only the XML under this
folder.

## What each language folder holds

```
<Language>/
└── Keyed/
    └── CaravansCriticallyEnhanced_Keys.xml
```

`DefInjected/` folders arrive with the mod's first defs. There is no `LanguageInfo.xml`: the game
never reads one for a language it already ships.

## The rule that matters

**A key added to one language is a shipped bug, not a warning.** The player sees a raw key name and
nothing in the game fails loudly enough to catch it. Every key lands in all nine folders in the same
commit. `Tests/LanguageParityTests.cs` enforces that both ways: every language declares the same
keys, every key the code asks for exists, and every key declared is still loaded by the compiled
mod, read from the IL so that a comment quoting a removed key cannot keep it alive.

## Translating

- Keep the key names and the XML structure exactly. Translate only the text between the tags.
- Keep `{0}` as it is. A count or a duration belongs inside the sentence as a placeholder, never
  appended in code, and should be phrased so that no noun has to agree with it: a label, a colon,
  then the value.
- Use the game's own words for game things: caravan, pack animal, hitching spot, Force depart, the
  Work tab, allowed area. Take them from the vanilla translation the player sees, not from a
  dictionary. The Japanese Work tab, for example, is 優先順位. The workspace keeps a glossary of the
  terms already looked up in `docs/caravans/glossary.md`.
- No em or en dashes. `Tests/SourceTests.cs` fails on either.
- The mod's own name, `CaravansCriticallyEnhanced_SettingsCategory`, stays in English in every
  language.
