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

## App shell navigation

- **Sidebar** (`Shared/_SideBar.cshtml`): brand, then the current-workspace card (`.scs-sidebar-workspace`, a group labelled "Current workspace", with a "Switch workspace" link only for users who can switch), then task sections: Overview, Content, Administration. Keep the existing menu partials and their access gates. Nested content types wrap rather than clip.
- **Active and open states**: Hyper marks the link for the current URL. The layout script then sets `aria-current="page"` on it and syncs `aria-expanded` on every collapse toggle. A page that isn't in the menu (for example the content editor) highlights the list it belongs to, taken from its last linked breadcrumb, with `aria-current="true"`. Sidebar icons use the Unicons set (`uil uil-*`) from `icons.min.css`. Dripicons aren't bundled. The current item gets a fill, a 3px leading bar and bold text, so it doesn't rely on colour. Open groups rotate their arrow.
- **Topbar** (`Shared/_Navbar.cshtml`): the menu toggle (`aria-controls="leftside-menu"`, with `aria-expanded` kept in sync), the current workspace name, and the profile menu (name, email, Switch workspace, Log out). Only add controls that have real behaviour. No demo search, notifications or fullscreen.
- **Responsive**: at 1140px and below the sidebar becomes Hyper's icon rail, where the workspace card is hidden and the topbar still names the workspace. Under 768px it's an off-canvas panel. Opening it moves focus to the Close button, and Escape or the backdrop closes it and returns focus to the toggle. While closed it is `visibility: hidden`, so it can't be tabbed into.

## Page header

`_Layout.cshtml` renders one standard header for every page that sets `ViewData["Title"]`. Everything else is an optional Razor section, so views need no extra view data or C# types:

```cshtml
@{ ViewData["Title"] = "Categories"; }                      // h1 and current crumb
@section PageHelper { One short sentence. }
@section PageActions { <button type="button" class="btn btn-primary">…</button> }  // right-aligned; full width under 768px
@* PageHeading overrides the h1 text only (the dashboard greeting).
   Breadcrumb adds linked crumbs before the page, e.g. the editor's parent list. *@
```

- The breadcrumb reads `Dashboard › <menu groups> › <page>`. The server renders Dashboard, any `Breadcrumb` section and the page. The shell script then inserts the sidebar groups above the highlighted menu link (for example Content Management › Pages), so the trail always uses the menu's own labels. The dashboard has no breadcrumb.
- A page that isn't a menu link sets its parent list as the last linked crumb. The script highlights that menu link (`aria-current="true"`) and relabels the crumb with the menu's text.
- Put one primary action in `PageActions`, plus a secondary outline button if the page needs one. Don't build page headers by hand in views.

## Typography

- Page title: `.scs-page-title` in the shared page header (see above), with `.scs-breadcrumb` above it and `.scs-page-helper` below.
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

- Cards: 8px radius, `--scs-border`, `--scs-shadow`, 1.35rem padding (1rem under 768px). `.scs-dash-card` lifts on hover; its icon tile takes a module accent via `.scs-dash-tone-{primary|accent|success|warning|info|neutral}`.
- Tabs: `.nav-tabs` / `.nav-pills` follow the primary colour. Editor tabs use `.scs-editor-tabs`: underline style, numbered steps from a CSS counter (so permission-gated tabs renumber themselves), and they scroll inside their own bar at every width. Give the list `role="tablist"` and plain-text labels. Bootstrap 5.3 adds tab roles and arrow-key navigation.
- Modals: 8px radius, overlay shadow, muted header, `.modal-title` at section-title size. Always set `aria-labelledby`.
- Alerts: Bootstrap `.alert-{state}` with the state tokens and a 4px leading border. Start the text with a word such as "Error:" or "Saved:".
- Loading: put `aria-busy="true"` and `.scs-loading` on a region to dim and lock it. Spinners carry `.visually-hidden` text.

## Authoring screens

Use these for editors built from tabs, such as the content editor and the Farsi translation form.

- **Structure**: `.card.scs-editor-card` holds `.scs-editor-tabs`, then an optional `.scs-editor-note` (for example, which steps unlock after the first save), then `.tab-content.scs-editor-panes`. Put a footer save bar in `.scs-editor-footer`.
- **General tab**: main fields in `.col-xl-8` and a `.scs-publish-panel` aside in `.col-xl-4`. The panel shows status (`.scs-publish-status` with a `.scs-status`), then type and schedule fields, then `.scs-publish-actions`. The primary save button spans the full row, and the other actions wrap below it. Name each action by its effect ("Activate", "Deactivate", "Preview"), not by the current state.
- **Field columns**: inside tab panes use Bootstrap `.row.g-3` with explicit columns (`col-md-6` for paired fields, `col-12` for long text). `.scs-field-grid` packs too many columns on wide screens.
- **Rich text**: put `.scs-rich-editor` on the Quill target. It is fixed at 320px and scrolls inside, and Quill's toolbar joins it as one bordered control. Keep `dir="rtl"`/`.fa---elements` on Farsi fields and editors.
- **Pane actions**: each AJAX-loaded pane ends with `.scs-author-actions`: one primary save, plus secondary outline actions. The shared `setLoadingForBtn` supplies the loading state.
- **Media**: `.scs-media-upload` (labelled size select + file input), `.scs-media-specs` (a `<dl>` of required ratio and sizes), then a grid of `.scs-media-card` figures whose `.scs-media-thumb` is a fixed 4:3 `object-fit: contain` box with a truncated file-name caption. Untrusted image sizes can't change the layout. Show `.scs-empty-state.scs-media-empty` when there are no files. Section files use `.scs-file-tile`, and "add images" tiles are `<button class="scs-media-add">`.
- **Repeatable sections**: `.scs-body-section` blocks with a `.scs-body-section-toolbar` of labelled `.scs-btn-icon` controls (move up/down, remove as `.is-danger`). Removal always goes through the SweetAlert confirm. Schema pickers are `<button class="scs-schema-tile">` in a `.scs-schema-picker` grid, so they work from the keyboard.
- **Tables with DataTables**: render the table inside `.scs-table-wrap` and let DataTables supply search, sort, paging and export. Don't add separate filter controls. Set `language.emptyTable` / `zeroRecords` to plain sentences, make the actions column `orderable: false`, and give formatted dates a sortable `data-order`.

## Supporting CMS modules

Categories, schemas, sliders, tags, cultures, content types and application settings share one pattern.

- **List page**: one `.card` > `.card-body` > `.scs-table-wrap#…ResultBody` under the standard page header. The list partial renders an `.scs-empty-state` (icon plus one sentence naming the page action) instead of an empty table. DataTables sorts by the name column, pages at 25, and sets `zeroRecords` for searches. Long names use `.scs-cell-truncate` with a `title`; dates carry a sortable `data-order`.
- **Actions**: only render a control that has an endpoint behind it. Row actions are `<button class="action-icon">` with `aria-label`s that name the row; delete is last and `.is-danger`, behind a SweetAlert confirm whose button names the object ("Delete schema").
- **Modal forms**: `.row.g-3` fields with `.scs-required` labels, `.form-text` help tied by `aria-describedby`, `.invalid-feedback` under the field, then `.scs-author-actions.justify-content-end` with a quiet Cancel (`data-bs-dismiss`) and one primary Save named by object. Close buttons use `aria-label="Close"`. The empty MVC validation summary (`.validation-summary-valid`) is hidden.
- **Tool modals** (schema fields, slides): `modal-xl modal-dialog-scrollable` plus a `modal-fullscreen-*-down` breakpoint. Sections are `.scs-tool-section` with a `.scs-section-title`. Slides are `.scs-media-card.scs-media-card--wide` (16:9, contained) with an `.scs-status` and labelled Activate/Deactivate, Edit and Delete. A chosen image previews in the form before upload. The schema logo shows as `.scs-logo-thumb` in the list and `.scs-logo-preview` in the form. Keep row gutters at `gx-3` or less inside modals, because Hyper's `g-4` is wider than the modal padding.

## Accessibility and motion

- Every link, button, tab, check and `[tabindex]` gets a 2px `--scs-focus-color` outline via `:focus-visible` (lightened on the dark sidebar). This overrides the theme's `outline: 0 !important`. Don't remove it.
- Text tokens meet 4.5:1 and control borders meet 3:1. Status and errors always pair colour with text or an icon.
- `prefers-reduced-motion` removes transitions and hover lifts. Spinners keep animating because they carry meaning.

## Responsive

- Under 768px, the page header stacks with its actions full width below the title, card padding drops to 1rem, editor tabs scroll, and wide tables scroll inside `.scs-table-wrap`.
- The page must never scroll horizontally: bound media with `object-fit` (see the workspace picker logo stage) and truncate long titles with `text-truncate` plus a `title`.
