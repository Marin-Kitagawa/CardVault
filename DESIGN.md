# Design

<!-- impeccable:product-schema-authored 1 -->

## World

CardVault ships two complete visual worlds, switched at runtime from Settings. Both worlds honor the same product truth: the home screen shows only card labels, nothing sensitive without an explicit reveal, and focus/keyboard/OS-text-scaling stays intact. The worlds share structure and component anatomy; they differ in palette, type, material, and motion.

Switching is a runtime resource swap: `ThemeService.Apply(kind)` loads a palette `ResourceDictionary` (`Styles/Themes/AtelierTheme.axaml` / `ReadoutTheme.axaml`), writes its entries into `Application.Current.Resources`, and sets the Fluent base theme variant (Light / Dark). Every themed value in XAML is consumed via `{DynamicResource ...}`, so all open windows re-skin instantly. The chosen world is persisted in the vault DB meta key `theme`.

All themed keys (both worlds must define the same key set):

| Key | Meaning |
| --- | --- |
| `BrushAppBg` | window / chrome ground |
| `BrushSurface` / `BrushSurfaceAlt` / `BrushSurfaceHigh` | card-likes surfaces, insets, raisers |
| `BrushStroke` / `BrushStrokeSoft` | hairline rules, dividers |
| `BrushText` / `BrushTextMuted` / `BrushTextFaint` | type scale on ground |
| `BrushAccent` / `Hover` / `Pressed` / `Soft` | the one active color + its coat |
| `BrushOnAccent` | text on accent (must contrast in both worlds) |
| `BrushSuccess` / `BrushDanger` / `BrushDangerSoft` / `BrushDangerBorder` | validation states |
| `BrushGold` | reserved nuance |
| `BrushInputBg` / `BrushInputBorder` / `BrushSelectionBg` / `BrushSelectionText` | input chrome |
| `BrushGhostBg` / `Hover` / `Pressed`, `BrushPrimaryDisabled`, `BrushIconHover` | button chrome |
| `BrushAccentGlow`, `BrushTileGlow` | shadow/glow accents |
| `GateGlowA` / `GateGlowB` | setup screen ambient light colors |
| `BrandA` / `BrandB` | emblem + FAB gradient stops |
| `FontDisplay` / `FontBody` / `FontMono` / `FontDigits` | family per role |
| `ShadowFocus` / `ShadowFab*` / `ShadowTile*` | `BoxShadows`-typed values (DynamicResource cannot string-convert these — keep them typed) |

## Direction contract

- **THESIS** — CardVault is a private lockbox you already own: it reads like a physical object from the owner's own world, not like a generic dark dashboard. It refuses the default "near-black + violet glass" identity the category ships.
- **OWN-WORLD** — two committed worlds, one famous device each: a bedside clock / gas-station totem (Readout) and washi paper / print artifact (Atelier). Both are typography-led, hairline-honest, quiet energy, one accent at a time. Card faces themselves stay plastic; the chrome around them changes world.
- **STORY** — unlock, glance, reveal on demand, copy, lock. The gate shows a live clock; the wallet shows only labels; details hide until an explicit reveal. Switching worlds in Settings re-skins everything live, persisting across launches.
- **FIRST VIEWPORT** — the locked gate: clock at top in segment/print digits, then the lock emblem (accent gradient), the vault title, the master-password field, the single primary action, ambient light tinted by the world's accent. Signature interaction: the blinking colon; the reveal-to-see detail flow.
- **FORM** — "seven-segment display" family (gas-station totem / food-scoreboard) + "japanese print" family (shippori mincho / sumi on washi); seed key from `concept-seed --scope direction` roll (index 5, seven-segment world). Fused from challenger Atelier (washi/orizuru) and Machine Room (normalled jackfield) donated the amber-on-black discipline; Console donated instant color-swap.

## Atelier — washi paper, sumi ink, one vermillion mark

Material: warm washi paper, ink hairline rules, print captions. Light world, editorial / print-artifact register.

- **Palette** — ground `#F3EEE1`; surface `#FBF7EA`; inset `#EFE7D6`; strokes `#D3C8B0` / `#DFD6C2`; ink text `#221E18`, muted `#6B6355`, faint `#8F8675`; accent vermillion `#C93B2A` (hover `#D74A38`, pressed `#A02E1F`, soft `#C93B2A1F`); on-accent `#FFFFFF`; success ink-green `#2F7D5B`; danger `#A63A2A`; gold dot `#C9A227`; input paper `#FFFDF6`, border `#BFB499`.
- **Type** — display: Shippori Mincho (embedded Bold); body: Yu Gothic UI / Meiryo fallback chain; captions: Cascadia Mono. Card-face digits use Shippori Mincho.
- **Mood** — red seal-ink pulls the only saturated word on the page.

## Readout — bedside clock / gas-station totem

Material: matte black glass, phosphor amber, seven-segment digits. Dark world, instrument register.

- **Palette** — ground `#0A0B0E`; surface `#111317`; inset `#16191E`; strokes `#262B33` / `#1F232A`; text `#EDF1F5`, muted `#8A949E`, faint `#5D666F`; accent amber `#FFB300` (hover `#FFC433`, pressed `#C77E00`, soft `#FFB30022`); on-accent near-black `#14110C` (amber + white text is a contrast failure — never ship it); success LED green `#31D37B`; danger LED red `#FF5A5A`; input `#0E1013`, border `#343A44`.
- **Type** — display: Barlow Condensed; body/mono: JetBrains Mono (embedded); digits: DSEG7 Classic embedded (`avares://CardVault/Assets/Fonts/DSEG7Classic-Bold.ttf#DSEG7 Classic`) — the card-face number renders in true segments.
- **Mood** — one amber signal lit on black glass; every glow answers to it.

## Components & motion

Shared anatomy, themed by the key set above: `tcard`/`tarea` inputs (watermark toggled by `StringEmptyToBool`, rounded 12px, focus = accent border + accent `ShadowFocus` ring), `tprimary` (accent ground, `BrushOnAccent` text), `ghost`, `danger` (outline + danger), `icon`, `fab` (accent gradient + glow), wallet `tcardtile` (lift + subtle scale on hover, accent-tinted shadow), swatch chips. The gate clock blinks its colon once a second and refreshes the minute; focus states stay visible; nothing animates when the vault is locked beyond the clock.

The card face (`CardFaceView`) is a drawn multi-layer material, not a raster: a plastic base, a bottom legibility scrim, a hairline inner print frame, a metal-fork EMV chip, and a brand pill. Its text is luminance-bound — a per-face ink vs. paper decision derived from the actual swatch, so a bright custom colour never drops below readable contrast. The card face is deliberately plastic in both worlds; the chrome around it changes world. The `generic` palette is sumi graphite (`#38342D` → `#1A1813`), which reads as ink in Atelier and black glass in Readout; the accent swatch ring is themed via the accent brush rather than a hard-coded violet.

Secret-entry rows follow the same reveal rule as the card payload: each named value is masked until its own row is revealed, and copy exists only while that row is revealed. In the edit form they are plain inputs (consistent with how number/CVV/notes are edited); on the detail surface they are masked.

Since the storage layer is one encrypted blob per record, the vault also holds **non-card entries**. Each kind is a template: labelled fields (some reveal-gated/masked), plain notes, plus free-form key–value rows; cards keep their dedicated face, everything else renders as a "pocket" tile — a surface pocket with a stroke-outline kind glyph, kind eyebrow, name and dotted placeholders. The `+` FAB opens a kind picker (`key`, `landmark`, `bitcoin`, `contact`, `file-text`, `sticky-note`, `award`, `gift`, `shield` glyphs; Lucide provenance in `Assets/Icons/LICENSES.Lucide.txt`). Templates live in `EntryKinds`; detail/editor UIs are the same components in both worlds. Kinds: Card, Login, Account (financial), Crypto, Identity, Document, Membership, Gift card, Physical (combinations), Secure note.

## Motion & interaction rules

- The only continuous motion is the gate clock (blinking colon). Everything else is hover/press states.
- Reveal is a state, not a transition: friendly banner text explains sensitivity; copy exists only while revealed.
- Theme switching is instant by contract (single resource swap), never animated across worlds.

## FINISH

unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance. The Avalonia surfaces carry no raster assets (all type and ink are drawn or themed); the only generated visual asset is the decision-page mock at `design/ui-concepts.html`, which is a selection aid, not a shipped raster.