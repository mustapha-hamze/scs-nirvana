# BackOffice Design System

Nirvana CMS BackOffice styling sits on top of the Hyper (Modern) Bootstrap 5 theme. The theme supplies the components; `src/Web/wwwroot/BackOffice/css/scs-admin.css` supplies the CMS tokens and rules. Use this page as the reference for later UI tasks.

## Where it loads

- `Areas/BackOffice/Views/Shared/_Layout.cshtml` (app shell) and `Shared/_CSSAssets.cshtml` (Login, SelectApp, WaitingForApproval) both load `scs-admin.css` **after** `app-saas.min.css`, with `asp-append-version="true"` so every change gets a new `?v=` hash and browsers never keep a stale copy.
- Keep a single design-system stylesheet. Put page-specific CSS in a view's `Styles` section, and build it from the tokens.

## Scope rules

- **Tokens and colour/state theming** (buttons, form states, focus, alerts, links) apply to every BackOffice page. The stylesheet is BackOffice-only.
- **Density and layout** (card padding, table density, modal header, sidebar, topbar, page header) are scoped to `body.scs-backoffice`, the app shell. Auth pages (`body.authentication-bg`) keep their own composition.
- New components use `scs-` prefixed classes. Don't restyle bare Bootstrap classes per page.

## Tokens (`:root`)

| Group | Tokens |
| --- | --- |
| Surfaces | `--scs-canvas` (page), `--scs-surface` (cards, inputs), `--scs-surface-muted` (table heads, tab bars, hover), `--scs-surface-sunken` (auth background, disabled), `--scs-chrome` (sidebar) |
| Ink | `--scs-ink` (titles, primary text), `--scs-ink-soft`, `--scs-ink-body` (labels, table text), `--scs-muted` (help text, metadata; ≥4.5:1 on canvas), `--scs-ink-inverse` |
| Borders | `--scs-border` (decorative dividers), `--scs-border-subtle` (row lines), `--scs-border-strong` (form control boundary, ≥3:1) |
| Primary action | `--scs-primary`, `--scs-primary-strong` (hover, links, focus), `--scs-primary-subtle`, `--scs-primary-border`, `--scs-primary-rgb` |
| Secondary accent | `--scs-accent`, `--scs-accent-text`, `--scs-accent-subtle`. Use it for brand and workspace highlights only, never for status. |
| States | `--scs-{success,warning,danger,info}-{solid,text,subtle,border}`. The `-text` colour on `-subtle` passes AA, and `-solid` carries white text or a dot. |
| Type | `--scs-font` (template Nunito), `--scs-text-page-title` 1.55rem, `--scs-text-section-title` 1.05rem, `--scs-text-card-title` .95rem, `--scs-text-body` .9rem, `--scs-text-meta` .8125rem, `--scs-text-label` .8rem, `--scs-text-overline` .7rem (table headings) |
| Spacing | `--scs-space-1…7` = .25, .5, .75, 1, 1.5, 2, 3rem |
| Radius | `--scs-radius-xs` 4px (menu items), `--scs-radius-sm` 6px (buttons, inputs), `--scs-radius` 8px (cards, modals), `--scs-radius-pill` (status) |
| Elevation | `--scs-shadow-sm`, `--scs-shadow` (resting card), `--scs-shadow-raised` (hover, dropdown), `--scs-shadow-overlay` (modal) |
| Focus / motion | `--scs-focus-color`, `--scs-focus-ring`, `--scs-duration` (.16s) |

The Hyper variables `--ct-primary*`, `--ct-link-*`, `--ct-focus-ring-color`, `--ct-danger*`, `--ct-form-invalid-*` and `--ct-{state}-{text-emphasis,bg-subtle,border-subtle}` are remapped to these tokens, so `.text-primary`, links, `.alert-*` and validation all follow the system.

## Typography

- Page title: `.scs-page-title` inside `.scs-page-header`, with `.scs-breadcrumb` above it and `.scs-page-helper` below.
- Section title: `.scs-section-title`. Card title: `.card-title`.
- Metadata: `.scs-meta`. Overline and table headings: `.scs-overline` (uppercase, tracked). Use uppercase only there.

## Buttons

| Role | Markup | Rule |
| --- | --- | --- |
| Primary | `.btn.btn-primary` | One per view or section: the main forward action |
| Secondary | `.btn.btn-outline-primary` / `.btn.btn-outline-secondary` | Alternative actions |
| Quiet | `.btn.scs-btn-quiet` | Cancel, dismiss, low-emphasis |
| Destructive | `.btn.btn-danger` (in the confirm step) / `.btn.btn-outline-danger` (trigger) | Always confirm, and name the object in the label |
| Icon-only | `.scs-btn-icon` or `.action-icon` (+ `.is-danger`) | Requires `aria-label` and a `title` or `data-bs-toggle="tooltip"`. 32px target |

Buttons in the shell are at least 38px tall (32px for `.btn-sm`). Loading means disabling the button and swapping its label for a `spinner-border-sm` plus text (see SelectApp).

## Forms

- Put `.form-label` on every control and add `.scs-required` to the label when the input has `required`. Help text goes in `.form-text`, tied to the input with `aria-describedby`.
- Errors: `.is-invalid` / `.was-validated` with `.invalid-feedback`, which gets a ⚠ prefix so the state doesn't rely on colour. Legacy `.text-input-error` still works.
- Focus: primary border plus `--scs-focus-ring`. Disabled: sunken background, muted text, `not-allowed` cursor. Read-only: muted background, light border (flatpickr inputs are excluded).
- `.input-group-text`, `.form-check-input` and Select2 share the strong border and primary checked state.
- Layout: `.scs-field-grid` gives auto-fit columns of at least 16rem (one column on phones). `.scs-field-span` makes a field span the full row.

## Tables

- Wrap every data table in `.scs-table-wrap` (or `.table-responsive`) so it scrolls inside its card, never the page.
- Headings use overline type on `--scs-surface-muted` and don't wrap. Cells are `.82rem 1rem` with subtle row lines, and rows hover to `--scs-surface-muted`.
- Selected rows take `.is-selected` or `aria-selected="true"`, which adds a tint plus a 3px primary bar.
- Status: `<span class="scs-status scs-status--success">Published</span>` (also `--warning`, `--danger`, `--info`, or no modifier for neutral). The dot plus text label is mandatory.
- Actions column: `.scs-table-actions` holds icon buttons or a `.dropdown` menu, with destructive items as `.dropdown-item.text-danger`.
- Empty state: `.scs-empty-state` with an icon, one sentence and, where possible, a primary action.

## Cards, tabs, modals, alerts, loading

- Cards: 8px radius, `--scs-border`, `--scs-shadow`, 1.35rem padding (1rem under 768px). `.scs-dash-card` lifts on hover.
- Tabs: `.nav-tabs` / `.nav-pills` follow the primary colour. Editor tabs use `.scs-editor-tabs` (underline style, scrolls horizontally on mobile).
- Modals: 8px radius, overlay shadow, muted header, `.modal-title` at section-title size. Always set `aria-labelledby`.
- Alerts: Bootstrap `.alert-{state}` with the state tokens and a 4px leading border. Start the text with a word such as "Error:" or "Saved:".
- Loading: put `aria-busy="true"` and `.scs-loading` on a region to dim and lock it. Spinners carry `.visually-hidden` text.

## Accessibility and motion

- Every link, button, tab, check and `[tabindex]` gets a 2px `--scs-focus-color` outline via `:focus-visible` (lightened on the dark sidebar). This overrides the theme's `outline: 0 !important`. Don't remove it.
- Text tokens meet 4.5:1 and control borders meet 3:1. Status and errors always pair colour with text or an icon.
- `prefers-reduced-motion` removes transitions and hover lifts. Spinners keep animating because they carry meaning.

## Responsive

- Under 768px, page-header buttons go full width, card padding drops to 1rem, editor tabs scroll, and wide tables scroll inside `.scs-table-wrap`.
- The page must never scroll horizontally: bound media with `object-fit` (see the workspace picker logo stage) and truncate long titles with `text-truncate` plus a `title`.
