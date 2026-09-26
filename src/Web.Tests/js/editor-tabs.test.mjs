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

function load(direction = "ltr") {
  const shown = [];
  const context = {
    getComputedStyle: () => ({ direction }),
    bootstrap: { Tab: { getOrCreateInstance: (tab) => ({ show: () => shown.push(tab.id) }) } },
  };
  vm.createContext(context);
  vm.runInContext(source, context);
  return { ...context, shown };
}

// Tab bar with the given tab ids; returns a function that presses a key on one tab.
function tabBar(ctx, ids) {
  let handler;
  let focused = null;
  const tabs = ids.map((id) => ({ id, focus() { focused = id; } }));
  ctx.initEditorTabs({
    addEventListener: (type, fn, capture) => {
      assert.equal(type, "keydown");
      assert.equal(capture, true, "must run in capture phase, before Bootstrap's handler");
      handler = fn;
    },
    querySelectorAll: () => tabs,
  });
  return (fromId, key) => {
    const event = {
      key,
      target: tabs.find((t) => t.id === fromId),
      prevented: false,
      stopped: false,
      preventDefault() { this.prevented = true; },
      stopPropagation() { this.stopped = true; },
    };
    focused = null;
    handler(event);
    return { focused, event };
  };
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
  const press = tabBar(ctx, ["tab-general", "tab-body", "tab-relations"]);
  const { focused, event } = press("tab-body", "ArrowRight");
  assert.equal(focused, "tab-relations");
  assert.deepEqual(ctx.shown, ["tab-relations"]);
  assert.ok(event.prevented && event.stopped);
});

test("End then Right wraps to the first tab", () => {
  const ctx = load();
  const press = tabBar(ctx, ["tab-general", "tab-body", "tab-metadata"]);
  assert.equal(press("tab-general", "End").focused, "tab-metadata");
  assert.equal(press("tab-metadata", "ArrowRight").focused, "tab-general");
});

test("RTL tab bar: Left moves to the next tab", () => {
  const ctx = load("rtl");
  const press = tabBar(ctx, ["tab-general", "tab-body", "tab-metadata"]);
  assert.equal(press("tab-general", "ArrowLeft").focused, "tab-body");
});

test("unhandled keys pass through to the browser and Bootstrap", () => {
  const ctx = load();
  const press = tabBar(ctx, ["tab-general", "tab-body"]);
  const { focused, event } = press("tab-general", "Enter");
  assert.equal(focused, null);
  assert.ok(!event.prevented && !event.stopped);
  assert.deepEqual(ctx.shown, []);
});
