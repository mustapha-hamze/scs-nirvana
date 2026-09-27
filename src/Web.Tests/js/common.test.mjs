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
  const $ = (target) => ({
    ajaxSend: (fn) => (jq.send = fn),
    ajaxError: (fn) => (jq.error = fn),
    on: (events, fn) => (jq.tables = fn),
    // clearModalBody: $("#id").html(spinner) puts a fresh pending placeholder in that region.
    html: () => {
      const region = byId[String(target).slice(1)];
      region.placeholder = element({ "data-scs-pending": "" });
      region.querySelector = () => region.placeholder;
    },
  });
  const win = { Swal: { fire: (o) => (swal.push(o), Promise.resolve({})) } };
  win.jQuery = $;
  const context = { document: doc, window: win, Swal: win.Swal, $, location: {}, Promise };
  vm.runInNewContext(source, context);
  return { context, jq, swal };
}

// A jqXHR-shaped request, sent the way jQuery does: ajaxSend fires inside $.ajax, before the caller
// chains .done/.fail. fail() rejects like jQuery: local fail callbacks first, then the global ajaxError.
function request(jq, settings = {}) {
  const callbacks = { done: [], fail: [] };
  const xhr = {
    statusText: "",
    done(fn) { callbacks.done.push(fn); return this; },
    fail(fn) { callbacks.fail.push(fn); return this; },
    then(onDone, onFail) { if (onDone) callbacks.done.push(onDone); if (onFail) callbacks.fail.push(onFail); return this; },
    catch(fn) { callbacks.fail.push(fn); return this; },
    succeed() { callbacks.done.forEach((fn) => fn()); },
    failWith(statusText = "error") {
      xhr.statusText = statusText;
      callbacks.fail.forEach((fn) => fn(xhr));
      jq.error({}, xhr, settings);
    },
  };
  jq.send({}, xhr, settings);
  return xhr;
}

const errorAlert = /role="alert"/;

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

test("an unhandled failure restores only the busy button and pending region it owned", () => {
  const button = element({ id: "btnSave" });
  button.innerHTML = "Save tag";
  const region = element({ id: "generalModalBody" });
  const { context, jq, swal } = load({ byId: { btnSave: button, generalModalBody: region } });

  context.setLoadingForBtn("btnSave");
  context.clearModalBody("generalModalBody");
  const xhr = request(jq).done(() => {});
  const placeholder = region.placeholder;
  xhr.failWith();

  assert.equal(button.disabled, false);
  assert.equal(button.getAttribute("aria-busy"), null);
  assert.equal(button.innerHTML, "Save tag");
  assert.match(placeholder.outerHTML, errorAlert);
  assert.equal(swal.length, 1);
});

test("a failure the caller handles itself has no generic side effect", () => {
  for (const handle of [
    (xhr) => xhr.fail(() => {}),
    (xhr) => xhr.catch(() => {}),
    (xhr) => xhr.then(() => {}, () => {}),
  ]) {
    const button = element({ id: "btnSave" });
    const region = element({ id: "body" });
    const { context, jq, swal } = load({ byId: { btnSave: button, body: region } });
    context.setLoadingForBtn("btnSave");
    context.clearModalBody("body");
    const xhr = request(jq);
    handle(xhr);
    xhr.failWith();

    assert.equal(button.disabled, true, "the caller decides what the button does");
    assert.equal(region.placeholder.outerHTML, undefined);
    assert.equal(swal.length, 0);
  }

  // An `error` option counts too.
  const button = element({ id: "btnSave" });
  const { context, jq, swal } = load({ byId: { btnSave: button } });
  context.setLoadingForBtn("btnSave");
  request(jq, { error() {} }).failWith();
  assert.equal(button.disabled, true);
  assert.equal(swal.length, 0);
});

test("one failed request cannot unlock, replace or report failure for another active request", () => {
  const formRegion = element({ id: "schema_details_form" });
  const listRegion = element({ id: "schema_details_list" });
  const other = element({ id: "btnUserFilter" });
  const { context, jq, swal } = load({ byId: { schema_details_form: formRegion, schema_details_list: listRegion, btnUserFilter: other } });

  // Two regions loaded together (newSchemaDetailsForm) each belong to their own request, in order.
  context.clearModalBody("schema_details_form");
  context.clearModalBody("schema_details_list");
  const formRequest = request(jq);
  const listRequest = request(jq);
  const formPlaceholder = formRegion.placeholder;
  const listPlaceholder = listRegion.placeholder;

  // An unrelated busy button from a separate, later request.
  context.setLoadingForBtnFilter("btnUserFilter");
  request(jq);

  listRequest.failWith();
  assert.match(listPlaceholder.outerHTML, errorAlert);
  assert.equal(formPlaceholder.outerHTML, undefined);
  assert.equal(other.disabled, true);
  assert.equal(swal.length, 0);

  formRequest.succeed();
  assert.equal(formPlaceholder.outerHTML, undefined);
});

test("a request started from a successful one inherits its busy button; a newer busy state is never undone", () => {
  const button = element({ id: "__btnCreateSliderItem__" });
  button.innerHTML = "Add slide";
  const { context, jq, swal } = load({ byId: { __btnCreateSliderItem__: button } });

  // createSliderItem: save, then upload the image from the save's success callback.
  context.setLoadingForBtn("__btnCreateSliderItem__");
  let upload;
  request(jq).done(() => { upload = request(jq); }).succeed();
  upload.failWith();
  assert.equal(button.disabled, false);
  assert.equal(button.innerHTML, "Add slide");
  assert.equal(swal.length, 1);

  // An old request failing after the button was busied again by a new one leaves the new state alone.
  context.setLoadingForBtn("__btnCreateSliderItem__");
  const first = request(jq);
  context.removeLoadingForBtn("__btnCreateSliderItem__");
  context.setLoadingForBtn("__btnCreateSliderItem__");
  request(jq);
  first.failWith();
  assert.equal(button.disabled, true);
  assert.equal(swal.length, 1);
});

test("aborted requests change nothing", () => {
  const button = element({ id: "btnSave" });
  const { context, jq, swal } = load({ byId: { btnSave: button } });
  context.setLoadingForBtn("btnSave");
  request(jq).failWith("abort");
  assert.equal(button.disabled, true);
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
