---
name: Canopy
description: A local planner and Software Factory where one project's colour lights the work on a charcoal bench.
colors:
  canopy-green: "#2f7258"
  canopy-green-deep: "#29644d"
  canopy-green-lit: "#558c76"
  canopy-green-border: "#97b9ac"
  canopy-green-border-night: "#24463b"
  react-flow-pink: "#ff0073"
  project-violet: "#6c63a8"
  project-ochre: "#c38b46"
  project-steel: "#477fa1"
  project-rose: "#bd6c73"
  project-sage: "#6c8580"
  dependency-gold: "#c29141"
  dependency-gold-night: "#e4b16c"
  night-floor: "#111114"
  night-rail: "#151518"
  graphite: "#19191d"
  graphite-raised: "#25252b"
  graphite-line: "#303036"
  graphite-edge: "#51515e"
  ash: "#777784"
  pewter: "#aaaab7"
  mist: "#c6c6cf"
  moonlight: "#ededf0"
  chalk: "#fafafa"
  paper: "#ffffff"
  slate-50: "#f8fafc"
  slate-100: "#f1f5f9"
  slate-200: "#e2e8f0"
  slate-300: "#cbd5e1"
  slate-400: "#94a3b8"
  slate-500: "#64748b"
  slate-600: "#475569"
  slate-700: "#334155"
  slate-800: "#1e293b"
  slate-900: "#0f172a"
  clay-danger: "#b85851"
  clay-danger-deep: "#a24741"
  coral-night: "#ff9b9b"
  amber-pending: "#826435"
  amber-pending-night: "#e8c58c"
typography:
  wordmark:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif"
    fontSize: "32px"
    fontWeight: 700
    lineHeight: 1.2
    letterSpacing: "-1.3px"
  display:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif"
    fontSize: "36px"
    fontWeight: 600
    lineHeight: 1.25
    letterSpacing: "-1.05px"
  headline:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif"
    fontSize: "26px"
    fontWeight: 600
    lineHeight: 1.3
    letterSpacing: "-0.8px"
  title:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif"
    fontSize: "16px"
    fontWeight: 600
    lineHeight: 1.5
    letterSpacing: "-0.07px"
  body:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif"
    fontSize: "16px"
    fontWeight: 400
    lineHeight: 1.5
  body-spec:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif"
    fontSize: "16px"
    fontWeight: 400
    lineHeight: 1.75
  ui:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif"
    fontSize: "14px"
    fontWeight: 400
    lineHeight: 1.5
  label:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif"
    fontSize: "12px"
    fontWeight: 600
    lineHeight: 1.5
    letterSpacing: "1.35px"
  mono:
    fontFamily: "ui-monospace, monospace"
    fontSize: "14px"
    fontWeight: 400
    lineHeight: 1.6
rounded:
  hairline: "3px"
  control-sm: "4px"
  control: "6px"
  menu: "7px"
  card: "10px"
  panel: "12px"
  dialog: "13px"
  card-night: "14px"
  pill: "999px"
spacing:
  gutter: "34px"
  gutter-tablet: "25px"
  gutter-narrow: "20px"
  gutter-phone: "12px"
  column-gap: "18px"
  stack: "12px"
  card-inset: "16px"
  dialog-inset: "29px"
  touch-target: "44px"
components:
  button-primary:
    backgroundColor: "{colors.canopy-green}"
    textColor: "{colors.paper}"
    rounded: "{rounded.pill}"
    padding: "9px 15px"
    height: "40px"
    typography: "{typography.ui}"
  button-primary-hover:
    backgroundColor: "{colors.canopy-green-deep}"
    textColor: "{colors.paper}"
  button-primary-light:
    backgroundColor: "{colors.canopy-green}"
    textColor: "{colors.paper}"
    rounded: "{rounded.control}"
    padding: "9px 15px"
    height: "40px"
  button-secondary:
    backgroundColor: "{colors.graphite}"
    textColor: "{colors.mist}"
    rounded: "{rounded.pill}"
    padding: "9px 15px"
    height: "40px"
  button-secondary-hover:
    backgroundColor: "{colors.graphite-raised}"
  button-ghost:
    textColor: "{colors.mist}"
    rounded: "{rounded.pill}"
    padding: "9px 15px"
    height: "40px"
  button-danger:
    backgroundColor: "{colors.clay-danger}"
    textColor: "{colors.paper}"
    rounded: "{rounded.pill}"
    padding: "9px 15px"
    height: "40px"
  button-danger-hover:
    backgroundColor: "{colors.clay-danger-deep}"
  icon-button:
    textColor: "{colors.mist}"
    rounded: "{rounded.control-sm}"
    size: "34px"
  view-tabs:
    backgroundColor: "{colors.graphite}"
    rounded: "{rounded.pill}"
    padding: "3px"
  view-tab:
    textColor: "{colors.mist}"
    rounded: "{rounded.pill}"
    padding: "6px 12px"
    height: "38px"
  task-card:
    backgroundColor: "{colors.graphite}"
    textColor: "{colors.moonlight}"
    rounded: "{rounded.card-night}"
    padding: "{spacing.card-inset}"
  kanban-column:
    backgroundColor: "{colors.night-rail}"
    rounded: "{rounded.card-night}"
    padding: "0 12px 15px"
    width: "310px"
  input-field:
    backgroundColor: "{colors.graphite}"
    textColor: "{colors.mist}"
    rounded: "{rounded.control}"
    padding: "10px 11px"
  dialog:
    backgroundColor: "{colors.graphite}"
    textColor: "{colors.mist}"
    rounded: "{rounded.dialog}"
    padding: "{spacing.dialog-inset}"
    width: "440px"
  dropdown-menu:
    backgroundColor: "{colors.graphite}"
    textColor: "{colors.mist}"
    rounded: "{rounded.menu}"
    padding: "5px"
  inspector:
    backgroundColor: "{colors.graphite}"
    width: "480px"
  eyebrow:
    textColor: "{colors.mist}"
    typography: "{typography.label}"
---

# Design System: Canopy

## Overview

**Creative North Star: "The Night Workshop"**

Canopy is a charcoal bench after hours. The room is dark and quiet, and the one lit thing is the project in front of you: its colour runs through the active view tab, the selected nav item, avatars, the progress ring, the mind-map root and every focus ring. Everything else sits in layered greys so the work, not the chrome, holds the eye. Dark ("React Flow" theme) is the default; a light theme ("Navy", cool slate neutrals) offers the same structure in daylight.

The workshop is warm, not clinical. Warmth lives in the microcopy ("A little room for what's next", "A little progress, one card at a time"), in the sprout and tree marks, in earthy semantic tones (ochre for high priority, clay for urgent and overdue, amber for pending) instead of alarm red, and in small kindnesses such as a gentle radial glow behind the welcome panel. Density is moderate: a planner you work in all day, with 14px UI text, generous card padding and clear 1px boundaries.

Controls are soft-edged and steady. Buttons and tabs become pills in the dark theme, cards have gently rounded corners, and touch targets grow to 44px on phones. Nothing bounces or flourishes; motion is short and only confirms what you did.

**Key Characteristics:**
- Charcoal tonal layers with 1px lines; depth from tone first, soft shadow second.
- One accent at a time: the current project's colour, derived for contrast, with Canopy Green when no project applies.
- Inter throughout, tight negative tracking on headings, uppercase tracked eyebrows.
- Earthy, low-alarm semantic colour; warm copy in empty states and footers.
- Every pointer interaction has a keyboard path and a reduced-motion fallback.

## Colors

A near-neutral charcoal (or cool slate) base where a single, user-chosen project colour does all the accent work.

### Primary
- **Canopy Green** (`canopy-green`): the theme's accent source when no project colour applies, and the default for the brand mark, primary buttons, active tabs and checkbox accents. Its hover is **Canopy Green Deep** (`canopy-green-deep`); on dark surfaces accent text uses the lighter **Canopy Green Lit** (`canopy-green-lit`) so it stays readable. Borders around accent fills use **Canopy Green Border** in light and **Canopy Green Border Night** in dark.

### Secondary
The project palette. Each project picks one, and `projectAccentTokens` derives the whole accent family (`--accent`, `--accent-fill`, `--accent-hover`, `--accent-border`, `--accent-soft`, `--accent-soft-strong`, `--accent-soft-text`) from it, nudging lightness until text reaches 4.5:1 against the surface.
- **React Flow Hot Pink** (`react-flow-pink`): the default colour for a new project.
- **Project Violet**, **Project Ochre**, **Project Steel**, **Project Rose**, **Project Sage**: the remaining presets. A custom picker allows any colour.

### Tertiary
- **Dependency Gold** (`dependency-gold`, `dependency-gold-night` in dark): dependency handles and dashed dependency arrows on the mind map. It is deliberately not the project colour, so hierarchy links and dependencies never read as the same thing.

### Neutral
Dark theme (default):
- **Night Floor** (`night-floor`): the app floor and canvas behind views.
- **Night Rail** (`night-rail`): sidebar and Kanban column wells.
- **Graphite** (`graphite`): cards, dialogs, menus, inputs, the inspector.
- **Graphite Raised** (`graphite-raised`): hover fills.
- **Graphite Line** (`graphite-line`) and **Graphite Edge** (`graphite-edge`): resting borders and hover borders.
- **Ash** (`ash`): decoration such as hint dots and tree lines.
- **Pewter**, **Mist**, **Moonlight**, **Chalk**: secondary text, body text, foreground and headings, in rising brightness.

Light theme ("Navy"): **Paper** (`paper`) surfaces on **Slate 50/100** floors and hovers, **Slate 200/300** lines, **Slate 400** decoration, and **Slate 500 to 900** for muted text through headings.

Semantic: **Clay Danger** (`clay-danger`, hover `clay-danger-deep`) for destructive buttons, **Coral Night** for danger text and overdue in dark, **Amber Pending** (`amber-pending`, `amber-pending-night`) for unsaved and syncing states. Workflow status dot colours are not design tokens: they come from `WorkflowDefaults` in configuration and from each project's columns.

### Named Rules
**The One Lamp Rule.** Only the current project's colour is used as an accent on a screen. Do not introduce a second brand hue; derived soft tints of the same colour are the only variation.

**The Derived Accent Rule.** Never hard-code a project's accent. Read the `--accent*` custom properties, which are recalculated for contrast whenever the project colour or theme changes.

**The No Alarm Rule.** Urgency, overdue and blocked states use earthy, muted tones (clay, ochre, amber), not saturated red. Saturated red is reserved for genuine error toasts.

## Typography

**Display Font:** Inter (with -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif)
**Body Font:** Inter
**Label/Mono Font:** Inter for labels; `ui-monospace` for code in specifications

**Character:** One workhorse sans used at many weights. Headings tighten their tracking as they grow, which gives the large titles a confident, compact feel; uppercase eyebrows open their tracking right up to label sections quietly.

### Hierarchy
- **Wordmark** (700, 32px, 1.2, -1.3px): the "canopy." brand in the sidebar, with its full stop in the accent colour.
- **Display** (600, 36px, 1.25, -1.05px): the Overview heading; 40px at 1700px and wider, 30 to 32px on smaller screens.
- **Headline** (600, 26px, 1.3, -0.8px): project titles and the task title in the inspector; dialog titles step up to 28px.
- **Title** (600, 16px, 1.5): card, column and map-node titles; project cards use 18px.
- **Body** (400, 16px, 1.5): form fields and general reading. Specifications use 1.75 line height.
- **UI** (400 to 600, 14px): the most common size; nav, toolbars, buttons, metadata.
- **Label** (600, 12px, 1.35px tracking, uppercase in the markup): eyebrows such as "WORKSPACE OVERVIEW" and section labels.

### Named Rules
**The Static Weights Rule.** Only Inter 400, 500, 600 and 700 are loaded and `font-synthesis` is off. Weights written as 550 render as 600, and 650 as 700; prefer the loaded weights in new work.

**The Small Print Floor Rule.** Keep readable text at 12px or larger. The 8 to 10px sizes in dense factory diagrams are an existing exception, not a pattern to extend.

## Layout

The shell is a two-column grid: a 260px sidebar (220px at 1100px and below, a 72px icon rail when collapsed) beside the main area, under a 48px top bar. Project pages stack a heading, a view toolbar (view tabs, search, filters, primary action), an optional scope breadcrumb, the view itself and a 38px footer carrying warm status copy and the item count.

Page gutters are 34px on desktop, 25px at 1100px, 20px at 550px and 12px on phones. Kanban columns are at least 310px wide with 18px gaps; Components columns are 370px. The task inspector is a 480px right-hand panel (440px at 1100px) that pushes the workspace aside on wide screens and overlays it below 850px; it can expand to a centred modal up to 1440px wide.

Breakpoints step at 1700, 1300, 1100, 850 and 550px. At 550px and below the sidebar becomes an overlay rail, the view tabs become a four-column grid, every control grows to a 44px touch target, inputs use 16px text to avoid zoom, and safe-area insets are respected. Rhythm is hand-tuned rather than on a strict scale; reuse the gutter and inset values above before inventing new ones.

## Elevation & Depth

Depth is tonal first, with a soft lift for things that float or can be picked up. Surfaces stack as floor, rail, graphite and raised graphite, separated by 1px lines. Shadows are soft and low-opacity, and they mark something that floats (menus, dialogs, the inspector) or responds to the hand (a card on hover, a card being dragged).

### Shadow Vocabulary
- **Card rest** (`box-shadow: 0 2px 4px #0f172a06`; dark `0 3px 8px #0002`): Kanban and project cards at rest.
- **Card hover** (`box-shadow: 0 3px 9px #0000000b`; dark `0 4px 16px #0003`): cards under the pointer.
- **Drag lift** (`box-shadow: 0 12px 28px #00000020` with a 2° tilt): the card being dragged.
- **Menu** (`box-shadow: 0 8px 30px #00000013`): dropdowns and pickers.
- **Dialog** (`box-shadow: 0 22px 75px #1e293b30`; dark `0 24px 80px #0008`): dialogs and the expanded inspector, over a 3px backdrop blur.
- **Inspector edge** (`box-shadow: -7px 0 25px #00000006`): the side panel's left edge.
- **Accent focus** (`box-shadow: 0 0 0 3px` accent at 12%): focused inputs in dark; selected map nodes use a 2px accent ring.

### Named Rules
**The Flat at Rest Rule.** Panels, columns and toolbars carry no shadow; only cards, floating layers and selected nodes may lift.

## Shapes

Corners are gently rounded and scale with the size of the object: 3px for keyboard hints and chips, 4 to 6px for controls and inputs, 7px for menus, 10px for cards (14px in dark), 12px for map nodes and component columns, 13px for dialogs. In the dark theme buttons, view tabs and their container become full pills (999px). Avatars and status dots are circles; project dots are 2px-rounded squares. In light, the brand mark has an asymmetric radius (11px top, 13px bottom) like a rounded shield; in dark it becomes an even 10px tile with an accent border. Dashed borders mean "empty or add here" (new-project card, empty columns, custom colour swatch).

## Components

### Buttons
Soft-edged and steady, with a one-pixel press.
- **Shape:** 6px corners in light, full pills in dark; 40px tall (36px small, 44px on phones).
- **Primary:** accent fill with white text and a hairline accent border, 9px × 15px padding, 14px text.
- **Hover / Focus:** primary darkens to the accent hover with a slightly deeper shadow; secondary and ghost pick up the raised surface. Every button presses down 1px on `:active`. Focus shows a 2px outline offset 3px (accent in dark, secondary text in light).
- **Secondary:** surface fill, 1px line border, body text. **Ghost:** text only. **Danger:** clay fill with white text.
- **Keyboard hints:** a `kbd` inside a button is a faint translucent box, hidden on narrow screens.

### Chips
- **Style:** small subtask, count and filter chips sit on the raised surface with 3 to 5px corners and 12px text. Blocked badges use a warm amber tint with a matching border.
- **State:** active filters switch their text to the accent colour.

### Cards / Containers
- **Corner Style:** 10px (14px dark) for task and project cards; 12px for map cards and component columns.
- **Background:** graphite on the night floor in dark; paper on slate in light.
- **Shadow Strategy:** card rest and card hover from Elevation; drag lift while dragging.
- **Border:** 1px line, brightening to the hover edge (and to the accent border in dark) under the pointer.
- **Internal Padding:** 16px on task cards, 24px on project cards, 18px × 20px on map cards.

### Inputs / Fields
- **Style:** 1px line border, 6px corners, 10px × 11px padding, 16px text on the surface (or slate floor inside the inspector).
- **Focus:** border shifts to the accent with a 3px soft accent glow (dark) or a neutral glow (light).
- **Error / Disabled:** disabled controls drop to 50% opacity; save errors appear as a warm, bordered note with retry actions rather than red text.

### Navigation
- **Sidebar:** wordmark, workspace switcher, quick search with `⌘ K`, primary nav, then "YOUR PROJECTS" with coloured project dots and counts. Items are 6px-rounded rows; hover lifts to the raised surface; the active item takes the accent soft tint in dark (accent fill with white text in light).
- **View tabs:** a segmented control of Components, Board, Gantt, Mind map and Software Factory; the active tab takes the accent (soft tint with an inset accent border in dark).
- **Top bar:** breadcrumb path, sync state, full screen, theme toggle, search and avatar.
- **Mobile:** the sidebar becomes a 72px overlay rail with a dimmed backdrop; controls grow to 44px.

### Dialogs and the Inspector
Dialogs are 440px (560px wide variant) with 29px padding, a 28px headline, warm one-line descriptions and a right-aligned footer above a hairline. They enter with a short rise and fade. The inspector slides in from the right; its title is an editable 26px headline, followed by a property grid (120px labels), tabs with an accent underline, and a Tiptap specification editor.

### Mind Map Nodes (signature)
Task nodes are 12px-rounded graphite cards with a status dot, two-line title and footer metadata. The project root is filled with the accent (a soft accent gradient in dark) and carries a left border in the branch colour. Gold dependency handles appear on hover; dropping onto a node to reparent it shows a ring and a small "Drop to move here" tag in the accent.

### Progress Ring (signature)
A 44px conic ring in the project colour shows completion beside the project title, with tabular figures in its centre. Thin 3px progress tracks repeat the same idea on cards and components.

### Build Reactor (signature)
An opt-in, interactive build view that follows a real implementation run. It defaults off, never shows an estimated percentage, and respects reduced motion.

## Do's and Don'ts

### Do:
- **Do** take every accent from the derived `--accent*` properties so any project colour stays readable in both themes.
- **Do** build depth from the night floor, rail, graphite and raised graphite layers with 1px lines before reaching for a shadow.
- **Do** keep controls at 40px (44px on phones) with 6px or pill corners, and give every drag a keyboard path.
- **Do** write empty and status states in the warm, short voice already used ("A little room for what's next").
- **Do** check new work in both the dark and light themes and at 550px.
- **Do** keep motion between 0.12s and 0.25s with ease-out, and let `prefers-reduced-motion` switch it off.

### Don't:
- **Don't** introduce a second accent hue or hard-code `#2f7258` where the project colour should apply.
- **Don't** colour urgency or overdue with saturated red; use the earthy clay, ochre and amber tones.
- **Don't** reuse dependency gold for anything other than dependencies.
- **Don't** hard-code workflow status colours; they come from configuration.
- **Don't** add shadows to panels, columns or toolbars at rest.
- **Don't** set new text below 12px.
