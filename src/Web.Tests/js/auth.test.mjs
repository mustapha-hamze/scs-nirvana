// Sign-in behaviour in wwwroot/BackOffice/js/features/auth.js, run against a minimal DOM stub:
//   node --test src/Web.Tests/js/auth.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";

const source = readFileSync(
  new URL("../../Web/wwwroot/BackOffice/js/features/auth.js", import.meta.url),
  "utf8",
);

function element(attrs = {}) {
  const listeners = {};
  return {
    attrs: { ...attrs },
    getAttribute(name) { return name in this.attrs ? this.attrs[name] : null; },
    setAttribute(name, value) { this.attrs[name] = String(value); },
    removeAttribute(name) { delete this.attrs[name]; },
    addEventListener(type, fn) { listeners[type] = fn; },
    fire(type, event = {}) { listeners[type]({ preventDefault() { event.prevented = true; }, ...event }); return event; },
  };
}

// Builds the login page shape: #password, its toggle, and the auth form with a submit button.
function load() {
  const input = Object.assign(element(), { type: "password" });
  const icon = { className: "uil uil-eye" };
  const toggle = Object.assign(element({ "aria-controls": "password", "aria-pressed": "false" }), {
    querySelector: () => icon,
  });
  const button = { disabled: false, innerHTML: "Sign in" };
  const form = Object.assign(element(), { querySelector: () => button });
  const win = element();
  const doc = {
    getElementById: (id) => (id === "password" ? input : null),
    querySelectorAll: (selector) => (selector === "[data-scs-password-toggle]" ? [toggle] : [form]),
  };
  vm.runInNewContext(source, { document: doc, window: win });
  return { input, icon, toggle, button, form, win };
}

test("password toggle reveals and hides the password with matching state and label", () => {
  const { input, icon, toggle } = load();

  toggle.fire("click");
  assert.equal(input.type, "text");
  assert.equal(toggle.getAttribute("aria-pressed"), "true");
  assert.equal(toggle.getAttribute("aria-label"), "Hide password");
  assert.equal(icon.className, "uil uil-eye-slash");

  toggle.fire("click");
  assert.equal(input.type, "password");
  assert.equal(toggle.getAttribute("aria-pressed"), "false");
  assert.equal(toggle.getAttribute("aria-label"), "Show password");
});

test("first submit locks the form, hides the password and shows progress", () => {
  const { input, toggle, button, form } = load();
  toggle.fire("click");

  const event = form.fire("submit");

  assert.equal(event.prevented, undefined);
  assert.equal(form.getAttribute("aria-busy"), "true");
  assert.equal(button.disabled, true);
  assert.match(button.innerHTML, /Signing in/);
  assert.equal(input.type, "password");
});

test("a second submit while busy is blocked", () => {
  const { form } = load();
  form.fire("submit");
  assert.equal(form.fire("submit").prevented, true);
});

test("restoring the page from back/forward cache unlocks the form", () => {
  const { button, form, win } = load();
  form.fire("submit");

  win.fire("pageshow", { persisted: true });

  assert.equal(form.getAttribute("aria-busy"), null);
  assert.equal(button.disabled, false);
  assert.equal(button.innerHTML, "Sign in");
});
