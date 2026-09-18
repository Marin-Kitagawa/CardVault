# Product

<!-- impeccable:product-schema 1 -->

## Platform

adaptive
Native Windows desktop app built on .NET 8 + Avalonia 11.3.22 (Win32 window). The product carries one custom design language; it does not re-skin per OS, so none of the web/mobile references apply.

## Users

One primary user: the owner, on their personal Windows PC. They use the vault occasionally, hold a handful of cards, and value how the tool *feels* — the app is a craft/portfolio object as much as a utility.

## Product Purpose

Private, local-first storage of payment-card details. The user sets a master password once, stores cards, reveals details only when asked, and can back up or move the vault through an encrypted export file.

## Positioning

Everything at rest is encrypted (AES-256-GCM, master password derived via PBKDF2). Nothing leaves the device except a passphrase-encrypted `.cvault` export. The interface behaves accordingly: the home screen shows only card labels — never a number, never a hint of one — until the user explicitly reveals details in the detail view.

## Operating Context

Runs on a personal Windows PC as a single window. Occasional, personal use: unlock, glance, reveal a number, copy, lock. A handful of cards is the realistic scale. Used at a desk; ambient light is desk lighting.

## Capabilities and Constraints

Confirmed functionality (from implementation):

- Master-password gate: create vault, unlock, change password; password must be ≥8 chars; auto-lock after configurable inactivity (1–60 min); in-memory key zeroed on lock.
- Card CRUD: add, edit, delete (delete is confirmed). Card fields: name, holder, number, expiry MM/YY, CVV, notes, accent swatch.
- Non-card encrypted entries, same blob anatomy as cards, selected via a kind picker from the `+` FAB: Login (url/username/password), Account (financial: institution/type/name/account number/routing-SWIFT), Crypto (network/address/seed phrase/private key), Identity (document type/name/number/country/DOB/expiry), Document (free-form key–value rows + notes), Membership, Gift card, Physical (combinations: safe, deposit box, locker, alarm, gate), Secure note. Every kind carries notes, accent swatch, and free-form secret rows; masked template fields and secret values are individually revealed and only then copyable. Old `cards`-table vault files migrate automatically to the `entries` table on open.
- Secret entries: each entry can hold a list of named secrets (PIN, security question answers, 2FA backup codes, online logins, up to 12), stored inside the same AES-256-GCM payload as the entry data, per-entry masked until individually revealed, copyable only while revealed, round-tripped through `.cvault` export/import.
- Live validation: brand detection (Visa/Mastercard/Amex/Discover/Generic), Luhn check, brand-correct CVV length, expiry-not-past, holder-name charset. Inputs auto-format (number grouping, MM/YY, max lengths).
- Reveal/copy: details masked by default; copy only while revealed; clipboard text not auto-cleared.
- Export/import `.cvault` (JSON envelope, AES-GCM, passphrase-derived key, "CVLT" magic), passphrase-entry dialogs.
- Confirmed dialogs: message, confirm (delete is danger-red), passphrase entry.
- Theme switching: two worlds (Atelier / Readout) selectable in Settings, applied instantly app-wide via themed `DynamicResource` keys, persisted in the vault DB (`theme` meta key). Fonts embedded as Avalonia resources for offline rendering.

Technical constraints:

- .NET 8, Avalonia 11.3.22. `AvaloniaUseCompiledBindingsByDefault=true` (binding errors fail the build).
- Avalonia 11.3.22 has NO `PasswordBox` control — password inputs are `TextBox` + `PasswordChar="●"`.
- CommunityToolkit.Mvvm (source-generated commands), MVVM pattern, `ApiCompat`-free.
- SQLite via Microsoft.Data.Sqlite; vault file at `%AppData%\CardVault\vault.db`.

## Brand Commitments

- Name: CardVault.
- Privacy-first behaviors are binding product truth: home screen shows only card labels; nothing sensitive without an explicit reveal; local-only by default.
- Visual identity: two themed worlds, switchable live from Settings and persisted — Atelier (washi paper, sumi ink, vermillion; default) and Readout (seven-segment gas-station totem; amber on black glass). Both are typography-led, quiet, one accent. A new generic look replaces these only through an explicit redesign decision.

## Evidence on Hand

- Builds clean in Debug and Release (0 warnings / 0 errors); app launches without startup crashes.
- No real user data or brand imagery exists; all demo content in concepts must be labeled synthetic where it could be mistaken for real card data.

## Product Principles

- The machine never shows what you did not ask for: numbers stay hidden until an explicit reveal.
- Local and encrypted is the point: the feel should make security feel owned, not warned-about.
- Craft over decoration: typography-led, precise, quiet — the chosen taste lane is editorial / print artifacts.
- Validation is conversation: guidance in the field, not error walls.
- Design respects the single scene: a personal desk, occasional use, a few cards kept well.

## Accessibility & Inclusion

Personal desktop tool, solo user. Keep OS text scaling, keyboard-only operation, and visible focus working; the redesign must not trade these for theatrics.