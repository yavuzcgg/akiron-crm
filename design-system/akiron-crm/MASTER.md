# Akiron CRM — Design System (Master)

> Generated with the UI/UX Pro Max skill (`.claude/skills/ui-ux-pro-max`, query "CRM client management
> sales pipeline agency", dials variance 4 · motion 3 · density 7), then curated. Where this file and
> the raw generator output disagree, this file wins; the reasons are in **Decisions** at the end.
>
> **Hierarchy:** when building a page, read `pages/<page>.md` first if it exists; its rules override
> this file. Otherwise follow this file. Tokens live in `frontend/src/app/globals.css`; components
> never use raw hex values.

**Product:** agency-first business management (CRM + jobs + pre-accounting), Turkish market.
**Personality:** calm, precise, trustworthy; the data is the hero, the chrome stays quiet.
**Style:** Minimalism / Swiss, with Soft UI touches (hairline borders, one soft shadow level).

---

## Color

Semantic tokens only. Every text pair below is measured (WCAG 2.2 AA needs 4.5:1 for text, 3:1 for
UI boundaries).

### Light

| Token | Hex | Use | Contrast |
| --- | --- | --- | --- |
| `--background` | `#F7F8FB` | App canvas | — |
| `--foreground` | `#0F172A` | Primary text | 16.8 on canvas |
| `--card` | `#FFFFFF` | Surfaces: cards, tables, menus | — |
| `--muted` | `#EFF1F6` | Quiet fills: table header, hover | — |
| `--muted-foreground` | `#525B6E` | Secondary text, labels | 6.8 on card, 6.0 on muted |
| `--border` | `#E3E6EE` | Hairlines between surfaces | decorative |
| `--input` | `#858DA1` | Form control outline | 3.3 on card |
| `--primary` | `#4F46E5` | Brand indigo: primary action, active nav, links | 6.3 with white |
| `--primary-soft` | `#EEF0FF` | Active nav fill, selected rows | indigo-700 text 7.0 |
| `--success` | `#047857` | Money in, won, paid, done | 5.5 on card |
| `--success-soft` | `#E7F8F0` | Success badge fill | 5.0 |
| `--warning` | `#B45309` | Due soon, needs attention | 5.0 on card |
| `--destructive` | `#DC2626` | Delete, overdue, errors | 4.8 |
| `--ring` | `#4F46E5` | Focus ring (2px + 2px offset) | — |

### Dark

Designed with light, not inverted: desaturated surfaces, lighter brand tint.

| Token | Hex | Contrast |
| --- | --- | --- |
| `--background` | `#0B0E17` | — |
| `--foreground` | `#E6E8F0` | 15.8 |
| `--card` | `#121726` | — |
| `--muted-foreground` | `#9AA3B5` | 7.0 on card |
| `--border` | `#232A3B` | decorative |
| `--input` | `#6B7489` | 3.8 on card |
| `--primary` | `#818CF8` | 6.5 with `#0B0E17` text |
| `--success` | `#34D399` | 9.3 |
| `--destructive` | `#F87171` | 6.5 |

### Status colours

Pipeline and job states always pair colour with a label or icon (never colour alone):
neutral (slate) · info (indigo) · progress (sky) · warning (amber) · success (emerald) · danger (red).

---

## Typography

| Role | Font | Size / line-height | Weight |
| --- | --- | --- | --- |
| Display (auth, empty states) | Plus Jakarta Sans | 30/36 | 700 |
| Page title | Plus Jakarta Sans | 22/28 | 650 |
| Section title | Plus Jakarta Sans | 15/22 | 600 |
| Body | Plus Jakarta Sans | 14/20 (app), 16/24 (forms on mobile) | 400 |
| Label, table header | Plus Jakarta Sans | 12/16, tracking +0.02em | 500 |
| Numbers, amounts, codes | Plus Jakarta Sans `tabular-nums`; Geist Mono for ids | — | 500 |

Plus Jakarta Sans: SaaS/B2B pairing from the generator's first pass, full Turkish coverage
(`latin-ext`), friendly but precise. Body never below 12px; inputs 16px on mobile to stop iOS zoom.

---

## Space, radius, elevation

- Spacing scale (density 7): 4 · 8 · 12 · 16 · 24 · 32 · 48. Page padding 24 desktop / 16 mobile.
- Radius: 10px controls and cards (`--radius: 0.625rem`), 6px badges, full for avatars.
- Elevation: surfaces are separated by **hairline borders**, not shadows. One shadow level
  (`0 1px 2px rgb(15 23 42 / 0.04), 0 1px 1px rgb(15 23 42 / 0.03)`) for cards; menus and dialogs
  get `0 12px 32px -8px rgb(15 23 42 / 0.18)`.
- Container: app content max 1280px; reading width 720px.
- Z-index: sticky header 20 · sidebar 30 · dropdown 40 · dialog 50 · toast 60.

---

## Layout

- **App shell:** left sidebar 248px (collapsible to icons), white surface, grouped navigation with
  section labels; active item = primary-soft fill + primary text + 2px indicator. Header 56px,
  sticky, translucent card over canvas; holds page context on the left, notifications and user on
  the right.
- **Page:** `PageHeader` (title, one-line description, primary action on the right) then content.
  One primary action per page.
- **Auth:** split screen ≥1024px: left brand panel (indigo, product promise and a real product
  glimpse), right form column 400px. Below 1024px the form stands alone with the logo.
- Breakpoints: 375 · 768 · 1024 · 1440. No horizontal scroll at 375.

---

## Components

- **Button:** primary (indigo fill), secondary (card + input border), ghost, destructive. Height 36
  (sm 32, lg 40). Loading state disables and shows a spinner; label stays.
- **Input:** 36px, `--input` outline, focus ring; label always visible above; helper or error below,
  linked by `aria-describedby`; password fields have a show/hide toggle.
- **Card:** card surface, hairline border, 10px radius, header 16/20 padding.
- **Table:** muted header row, 44px rows, hover muted, numbers right-aligned tabular, sticky header
  on long lists.
- **Badge:** soft fill + strong text of the same hue, 6px radius, 12px text.
- **Empty state:** icon in a soft circle, one sentence of what goes here, one action.
- **Timeline:** day groups, 28px icon badges on a hairline rail, time right-aligned tabular.
- **Toast:** bottom-right, 4s, `aria-live=polite`, never steals focus.

---

## Motion

Subtle. 150ms colour/opacity for hover and press, 200ms for menus and dialogs (enter), 140ms exit.
Easing `cubic-bezier(0.2, 0, 0, 1)`. Only `transform` and `opacity` animate. Everything respects
`prefers-reduced-motion`. No scroll-reveal choreography inside the app.

---

## Anti-patterns

- Glassmorphism or heavy blur on data surfaces (hurts legibility of dense content).
- Emoji as icons (Lucide only, 16/18px, stroke 1.75).
- Colour-only status; gray-on-gray text; placeholder-only labels.
- Hover-only affordances; layout-shifting hover transforms.
- Invented numbers or placeholder charts: show an empty state until real data exists.

---

## Pre-delivery checklist

- [ ] Text contrast ≥ 4.5:1, control boundaries ≥ 3:1, in light and dark
- [ ] Visible focus on every interactive element; logical tab order
- [ ] `cursor-pointer` on clickable elements; 36px+ targets (44px on touch)
- [ ] Loading, empty and error states for every async view
- [ ] Works at 375px without horizontal scroll
- [ ] `prefers-reduced-motion` respected
- [ ] All text through the i18n dictionary; money and dates through `format.ts`

---

## Decisions (curation of the generator output)

| Generated | Chosen | Why |
| --- | --- | --- |
| Primary `#2563EB` (generic SaaS blue) | Indigo `#4F46E5` | Distinct brand for Akiron while keeping the "professional" family; the generator's own Micro-SaaS indigo `#6366F1` fails 4.5:1 with white, one step darker passes at 6.3. |
| Accent `#059669` for primary buttons | Emerald kept as **success** only | Green buttons collide with "paid/won" meaning in a finance product; one action colour (indigo). |
| Cormorant Garamond / Crimson Pro | Plus Jakarta Sans | The serif pairing matched the word "agency" (academic mood), not a data-dense app; the generator's SaaS pass picked Plus Jakarta Sans. |
| Glassmorphism (first pass) | Minimalism + Soft UI | The CRM product profile recommends Flat/Minimalism; blur behind tables hurts legibility. |
| Border `#E4ECFC` for inputs | `--input #858DA1` | Form control boundaries need 3:1 (WCAG 1.4.11); hairline borders stay light for surfaces only. |
| Card shadow md + hover lift | Hairline + one soft shadow, no lift | "No shadows / no layout-shifting hovers" from the same output; cards are not all clickable. |
| Landing-page pattern (hero + demo) | Applies to the future marketing site only | The app itself has no hero. |
