// Keyboard behaviour of the content editor tabs in wwwroot/BackOffice/js/features/content.js.
// No JS toolchain in this repo, so this uses Node's built-in runner and a minimal fake DOM:
//   node --test src/Web.Tests/js/editor-tabs.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";

const source = readFileSync(
  new URL("../../Web/wwwroot/BackOffice/js/features/content.js", import.meta.url),
  "utf8",
);

// Loads content.js with a Bootstrap stub whose show() does what Bootstrap's does for tabs:
// flips aria-selected, then fires a bubbling "shown.bs.tab" on the activated tab.
function load(direction = "ltr") {
  const shown = [];
  const context = {
    getComputedStyle: () => ({ direction }),
    bootstrap: {
      Tab: {
        getOrCreateInstance: (tab) => ({
          show: () => {
            shown.push(tab.id);
            for (const t of tab.bar.tabs) t.setAttribute("aria-selected", String(t === tab));
            tab.bar.dispatch("shown.bs.tab", { target: tab });
          },
        }),
      },
    },
  };
  vm.createContext(context);
  vm.runInContext(source, context);
  return { ...context, shown };
}

// Tab bar with the given tab ids (the first starts selected). Keydown handlers must use capture.
function tabBar(ctx, ids, initialTabindex = {}) {
  const listeners = {};
  let focused = null;
  const bar = {
    tabs: [],
    addEventListener: (type, fn, capture) => {
      if (type === "keydown")
        assert.equal(capture, true, "must run in capture phase, before Bootstrap's handler");
      (listeners[type] ??= []).push(fn);
    },
    dispatch: (type, event) => (listeners[type] ?? []).forEach((fn) => fn(event)),
    querySelectorAll: () => bar.tabs,
    querySelector: () => bar.tabs.find((t) => t.getAttribute("aria-selected") === "true") ?? null,
  };
  bar.tabs = ids.map((id, i) => {
    const attrs = { "aria-selected": String(i === 0) };
    if (id in initialTabindex) attrs.tabindex = initialTabindex[id];
    return {
      id,
      bar,
      getAttribute: (name) => attrs[name] ?? null,
      setAttribute: (name, value) => { attrs[name] = String(value); },
      focus() { focused = id; },
    };
  });
  ctx.initEditorTabs(bar);

  const press = (fromId, key) => {
    const event = {
      key,
      target: bar.tabs.find((t) => t.id === fromId),
      prevented: false,
      stopped: false,
      preventDefault() { this.prevented = true; },
      stopPropagation() { this.stopped = true; },
    };
    focused = null;
    bar.dispatch("keydown", event);
    return { focused, event };
  };
  // What a mouse click does: Bootstrap's data API shows the clicked tab; our key handler isn't involved.
  const click = (id) => ctx.bootstrap.Tab.getOrCreateInstance(bar.tabs.find((t) => t.id === id)).show();
  const tabStops = () => Object.fromEntries(bar.tabs.map((t) => [t.id, t.getAttribute("tabindex")]));
  return { press, click, tabStops };
}

test("next index: arrows wrap, Home/End jump, other keys ignored", () => {
  const { nextEditorTabIndex: next } = load();
  assert.equal(next(0, 3, "ArrowRight", false), 1);
  assert.equal(next(2, 3, "ArrowRight", false), 0);
  assert.equal(next(0, 3, "ArrowLeft", false), 2);
  assert.equal(next(1, 3, "Home", false), 0);
  assert.equal(next(0, 3, "End", false), 2);
  assert.equal(next(0, 3, "Tab", false), -1);
  assert.equal(next(0, 3, "ArrowDown", false), -1);
});

test("next index: RTL flips Left/Right to match visual order", () => {
  const { nextEditorTabIndex: next } = load();
  assert.equal(next(0, 3, "ArrowLeft", true), 1);
  assert.equal(next(0, 3, "ArrowRight", true), 2);
  assert.equal(next(2, 3, "ArrowLeft", true), 0);
});

test("arrow key focuses and activates the next rendered tab", () => {
  const ctx = load();
  // Permission-limited editor: only General, Body and Relations are rendered.
  const { press } = tabBar(ctx, ["tab-general", "tab-body", "tab-relations"]);
  const { focused, event } = press("tab-body", "ArrowRight");
  assert.equal(focused, "tab-relations");
  assert.deepEqual(ctx.shown, ["tab-relations"]);
  assert.ok(event.prevented && event.stopped);
});

test("End then Right wraps to the first tab", () => {
  const ctx = load();
  const { press } = tabBar(ctx, ["tab-general", "tab-body", "tab-metadata"]);
  assert.equal(press("tab-general", "End").focused, "tab-metadata");
  assert.equal(press("tab-metadata", "ArrowRight").focused, "tab-general");
});

test("RTL tab bar: Left moves to the next tab", () => {
  const ctx = load("rtl");
  const { press } = tabBar(ctx, ["tab-general", "tab-body", "tab-metadata"]);
  assert.equal(press("tab-general", "ArrowLeft").focused, "tab-body");
});

test("unhandled keys pass through to the browser and Bootstrap", () => {
  const ctx = load();
  const { press } = tabBar(ctx, ["tab-general", "tab-body"]);
  const { focused, event } = press("tab-general", "Enter");
  assert.equal(focused, null);
  assert.ok(!event.prevented && !event.stopped);
  assert.deepEqual(ctx.shown, []);
});

test("roving tabindex: init normalizes stops to the selected tab only", () => {
  const ctx = load();
  // Markup without explicit stops on the inactive tabs still ends with exactly one stop.
  const { tabStops } = tabBar(ctx, ["tab-general", "tab-body", "tab-metadata"], { "tab-body": "0" });
  assert.deepEqual(tabStops(), { "tab-general": "0", "tab-body": "-1", "tab-metadata": "-1" });
});

test("roving tabindex follows keyboard activation (Right, End, Home, Left)", () => {
  const ctx = load();
  const { press, tabStops } = tabBar(ctx, ["tab-general", "tab-body", "tab-relations"]);
  press("tab-general", "ArrowRight");
  assert.deepEqual(tabStops(), { "tab-general": "-1", "tab-body": "0", "tab-relations": "-1" });
  press("tab-body", "End");
  assert.deepEqual(tabStops(), { "tab-general": "-1", "tab-body": "-1", "tab-relations": "0" });
  press("tab-relations", "Home");
  assert.deepEqual(tabStops(), { "tab-general": "0", "tab-body": "-1", "tab-relations": "-1" });
  press("tab-general", "ArrowLeft");
  assert.deepEqual(tabStops(), { "tab-general": "-1", "tab-body": "-1", "tab-relations": "0" });
});

test("roving tabindex follows mouse/Bootstrap activation", () => {
  const ctx = load();
  const { click, tabStops } = tabBar(ctx, ["tab-general", "tab-body", "tab-metadata"]);
  click("tab-metadata");
  assert.deepEqual(tabStops(), { "tab-general": "-1", "tab-body": "-1", "tab-metadata": "0" });
  click("tab-body");
  assert.deepEqual(tabStops(), { "tab-general": "-1", "tab-body": "0", "tab-metadata": "-1" });
});
