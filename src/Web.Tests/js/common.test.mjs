// Shared BackOffice behaviour in wwwroot/BackOffice/js/features/common.js, run against a minimal DOM/jQuery stub:
//   node --test src/Web.Tests/js/common.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";

const source = readFileSync(
  new URL("../../Web/wwwroot/BackOffice/js/features/common.js", import.meta.url),
  "utf8",
);

function element(attrs = {}, props = {}) {
  const listeners = {};
  return {
    attrs: { ...attrs },
    id: attrs.id || "",
    innerHTML: "",
    disabled: false,
    isConnected: true,
    dataset: {},
    classList: { added: [], add(c) { this.added.push(c); } },
    getAttribute(name) { return name in this.attrs ? this.attrs[name] : null; },
    setAttribute(name, value) { this.attrs[name] = String(value); },
    hasAttribute(name) { return name in this.attrs; },
    removeAttribute(name) { delete this.attrs[name]; },
    addEventListener(type, fn) { listeners[type] = fn; },
    fire(type, event = {}) { listeners[type]?.(event); },
    focus() { doc.activeElement = this; },
    click() { this.clicked = true; },
    querySelector: () => null,
    querySelectorAll: () => [],
    ...props,
  };
}

// `byId` backs getElementById; `all(selector)` backs querySelectorAll. Returns the document, the
// captured jQuery global handlers and every messageBox (Swal) call.
let doc;
function load({ byId = {}, all = () => [] } = {}) {
  const swal = [];
  const jq = {};
  doc = element({}, {
    body: element(),
    getElementById: (id) => byId[id] ?? null,
    querySelectorAll: all,
  });
  doc.activeElement = doc.body;
  const $ = () => ({
    ajaxError: (fn) => (jq.error = fn),
    ajaxStop: (fn) => (jq.stop = fn),
    on: (events, fn) => (jq.tables = fn),
  });
  const win = { Swal: { fire: (o) => (swal.push(o), Promise.resolve({})) } };
  win.jQuery = $;
  const context = { document: doc, window: win, Swal: win.Swal, $, location: {}, Promise };
  vm.runInNewContext(source, context);
  return { context, jq, swal };
}

test("a busy button is disabled, announced, and restored to its original label", () => {
  const button = element({ id: "btnSave" });
  button.innerHTML = '<i class="uil uil-save"></i> Save tag';
  const { context } = load({ byId: { btnSave: button } });

  context.setLoadingForBtn("btnSave");
  assert.equal(button.disabled, true);
  assert.equal(button.getAttribute("aria-busy"), "true");
  assert.match(button.innerHTML, /Saving/);

  context.removeLoadingForBtn("btnSave", "saveTag()");
  assert.equal(button.disabled, false);
  assert.equal(button.getAttribute("aria-busy"), null);
  assert.equal(button.innerHTML, '<i class="uil uil-save"></i> Save tag');
  assert.equal(button.getAttribute("onclick"), "saveTag()");
});

test("an unhandled failed request unlocks buttons, replaces stuck spinners and says so", () => {
  const button = element({ id: "btnSave" });
  button.innerHTML = "Save tag";
  const spinner = element();
  const { context, jq, swal } = load({
    byId: { btnSave: button },
    all: (s) => (s === "[data-scs-pending]" ? [spinner] : s === "[data-scs-label]" && button.hasAttribute("data-scs-label") ? [button] : []),
  });
  context.setLoadingForBtn("btnSave");

  jq.error({}, { statusText: "error" });
  jq.stop();

  assert.equal(button.disabled, false);
  assert.equal(button.innerHTML, "Save tag");
  assert.match(spinner.outerHTML, /role="alert"/);
  assert.equal(swal.length, 1);
});

test("aborted requests and failures their caller already handled change nothing", () => {
  const { jq, swal } = load();
  jq.error({}, { statusText: "abort" });
  jq.stop();
  jq.error({}, { statusText: "error" });
  jq.stop();
  assert.equal(swal.length, 0);
});

test("Enter in an AJAX form runs its primary command instead of posting the page", () => {
  load();
  const command = element({ onclick: "saveTagForm()" });
  const form = element({}, {
    querySelector: (s) => (s === ".btn-primary[onclick]" ? command : null),
  });
  let prevented = false;
  doc.fire("submit", { target: form, defaultPrevented: false, preventDefault: () => (prevented = true) });
  assert.equal(prevented, true);
  assert.equal(command.clicked, true);
});

test("forms with a real submit button, or their own submit handler, still submit natively", () => {
  load();
  const submitButton = element();
  const nativeForm = element({}, { querySelector: (s) => (s.includes("[type=submit]") ? submitButton : element()) });
  let prevented = false;
  doc.fire("submit", { target: nativeForm, defaultPrevented: false, preventDefault: () => (prevented = true) });
  assert.equal(prevented, false);

  const handledForm = element({}, { querySelector: () => null });
  doc.fire("submit", { target: handledForm, defaultPrevented: true, preventDefault: () => (prevented = true) });
  assert.equal(prevented, false);
});

test("closing a modal whose opener was re-rendered focuses the replacement opener", () => {
  const replacement = element({ onclick: "schemaForm(4)", "data-bs-target": "#schema-modal" });
  load({ all: () => [element({ onclick: "schemaForm(3)", "data-bs-target": "#schema-modal" }), replacement] });
  const opener = element({ onclick: "schemaForm(4)", "data-bs-target": "#schema-modal" });
  opener.isConnected = false;

  doc.fire("show.bs.modal", { relatedTarget: opener });
  doc.fire("hidden.bs.modal", {});
  assert.equal(doc.activeElement, replacement);
});

test("without a replacement, focus lands on the main region; Bootstrap's own restore is left alone", () => {
  const main = element({ id: "main-content" });
  load({ byId: { "main-content": main } });
  const opener = element({ onclick: "gone()" });
  opener.isConnected = false;
  doc.fire("show.bs.modal", { relatedTarget: opener });
  doc.fire("hidden.bs.modal", {});
  assert.equal(doc.activeElement, main);

  const restored = element();
  doc.fire("show.bs.modal", { relatedTarget: element() });
  doc.activeElement = restored; // Bootstrap already returned focus
  doc.fire("hidden.bs.modal", {});
  assert.equal(doc.activeElement, restored);
});

test("invalid fields are marked, tied to their error text, and the first one is focused", () => {
  const feedback = element();
  const title = element({ id: "Title" }, {
    type: "text", willValidate: true, validity: { valid: false },
    parentElement: { querySelector: () => feedback },
  });
  const typeId = element({ id: "TypeId" }, { type: "select-one", willValidate: true, validity: { valid: true } });
  typeId.setAttribute("aria-invalid", "true");
  const form = element({ id: "frmTag" }, {
    checkValidity: () => false,
    querySelector: (s) => (s.startsWith(":invalid") ? title : null),
    querySelectorAll: () => [title, typeId],
  });
  const { context } = load({ byId: { frmTag: form } });

  assert.equal(context.checkFormValidity("frmTag"), false);
  assert.deepEqual(form.classList.added, ["was-validated"]);
  assert.equal(title.getAttribute("aria-invalid"), "true");
  assert.equal(feedback.id, "Title-error");
  assert.equal(title.getAttribute("aria-describedby"), "Title-error");
  assert.equal(typeId.getAttribute("aria-invalid"), null);
  assert.equal(doc.activeElement, title);

  // Fixing the field clears the state as the user types.
  title.validity = { valid: true };
  form.fire("input");
  assert.equal(title.getAttribute("aria-invalid"), null);
});

test("a collapsed table's details toggle is named by its text plus what Enter does", () => {
  const row = { classList: { contains: (c) => c === "parent" && row.open } };
  const cell = element({}, { textContent: " 4380 ", parentElement: row, closest: () => ({}) });
  const { jq } = load({ all: (s) => (s === "td.dtr-control" ? [cell] : []) });

  jq.tables();
  assert.equal(cell.getAttribute("aria-label"), "4380, show details");
  row.open = true;
  doc.fire("click", { target: { closest: () => cell } });
  assert.equal(cell.getAttribute("aria-label"), "4380, hide details");

  cell.closest = () => null; // table wide enough again: no toggle to describe
  jq.tables();
  assert.equal(cell.getAttribute("aria-label"), null);
});
