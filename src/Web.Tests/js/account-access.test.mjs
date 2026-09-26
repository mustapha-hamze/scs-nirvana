// Users and access script in Areas/BackOffice/Views/Account/__AccountJSFunctions.cshtml, run with a
// minimal jQuery/SweetAlert stub:
//   node --test src/Web.Tests/js/account-access.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";

const partial = readFileSync(
  new URL("../../Web/Areas/BackOffice/Views/Account/__AccountJSFunctions.cshtml", import.meta.url),
  "utf8",
);
const source = partial.replace(/^\s*<script>/, "").replace(/<\/script>\s*$/, "");

// `answer` is what the stubbed SweetAlert resolves with; `values` seeds .val() by selector;
// `response` is what every $.ajax call answers with.
function load({ answer = true, values = {}, response = "Done", tableRows = 0 } = {}) {
  const calls = { ajax: [], swal: [], html: {}, text: {}, search: [] };
  const state = { ...values };
  const node = (selector) => {
    const self = {
      length: selector === "#datatable-buttons" ? tableRows : 1,
      val: (v) => (v === undefined ? state[selector] ?? "" : ((state[selector] = v), self)),
      html: (v) => (v === undefined ? "" : ((calls.html[selector] = v), self)),
      text: (v) => (v === undefined ? state[selector + ":text"] ?? "" : ((calls.text[selector] = v), self)),
      data: (k, v) => (v === undefined ? state[selector + ":" + k] : ((state[selector + ":" + k] = v), self)),
      attr: () => undefined,
      serialize: () => "form:" + selector,
      prop: () => self, removeAttr: () => self, addClass: () => self, removeClass: () => self,
      toggleClass: () => self, trigger: () => self, on: () => self, find: () => self, first: () => self, is: () => false,
      DataTable: () => ({ column: () => ({ search: (...a) => ((calls.search.push(a)), { draw: () => {} }) }) }),
    };
    return self;
  };
  const $ = (selector) => node(typeof selector === "string" ? selector : "#el");
  $.trim = (s) => String(s).trim();
  $.extend = Object.assign;
  $.each = (list, fn) => list.forEach((item) => fn.call(item));
  $.ajax = (request) => {
    calls.ajax.push(request);
    const chain = {
      done: (fn) => (fn(response), chain),
      fail: () => chain,
      always: (fn) => (fn(), chain),
    };
    return chain;
  };
  $.fn = {};
  const noop = () => {};
  const context = {
    $,
    document: { querySelector: () => "open-modal", getElementById: () => ({}), addEventListener: noop },
    Swal: { fire: (opts) => (calls.swal.push(opts), Promise.resolve({ isConfirmed: answer })) },
    bootstrap: { Tab: { getOrCreateInstance: () => ({ show: noop }) } },
    clearModalBody: noop, setLoadingForBtnFilter: noop, removeLoadingForBtnFilter: noop,
    setLoadingForBtn: noop, removeLoadingForBtn: noop, checkFormValidity: () => true,
    location: { reload: noop },
  };
  vm.createContext(context);
  vm.runInContext(source, context);
  return { context, calls, state };
}

const settle = () => new Promise((resolve) => setImmediate(resolve));

test("granting a role confirms inside the open modal with Cancel focused, then posts the same endpoint", async () => {
  const { context, calls } = load();
  context.accessUserName = "Jane Doe";
  context.addUserToRole("u1", "Editor");
  await settle();
  const [dialog] = calls.swal;
  assert.equal(dialog.title, "Grant the Editor role to Jane Doe?");
  assert.equal(dialog.confirmButtonText, "Grant Editor");
  assert.equal(dialog.cancelButtonText, "Cancel");
  assert.equal(dialog.focusCancel, true);
  assert.equal(dialog.target, "open-modal");
  assert.equal(calls.ajax[0].url, "/BackOffice/Account/AddUserToRole/u1/Editor");
  assert.equal(calls.ajax[0].type, "POST");
});

test("removing a role is a danger confirmation and does nothing when cancelled", async () => {
  const { context, calls } = load({ answer: false });
  context.removeUserFromRole("u1", "Editor");
  await settle();
  assert.equal(calls.swal[0].confirmButtonText, "Remove Editor");
  assert.equal(calls.swal[0].confirmButtonColor, "var(--scs-danger-solid)");
  assert.equal(calls.ajax.length, 0);
});

test("an empty user list response becomes a search prompt instead of a blank area", () => {
  const { context, calls } = load({ response: "" });
  context.findUsers();
  assert.equal(calls.ajax[0].url, "/BackOffice/Account/UserList");
  assert.equal(calls.ajax[0].data, "form:#frmUserFilters");
  assert.match(calls.html["#findUsersResultBody"], /Enter all or part of an email address/);
});

test("the approval filter matches the whole status, so Approved never matches Pending approval", () => {
  const { context, calls } = load({ tableRows: 1, values: { "#userApprovalFilter": "Approved" } });
  context.filterUsersByApproval();
  assert.deepEqual(calls.search[0], ["^Approved$", true, false]);
});

test("ticking a permission updates the hidden value and flags unsaved changes", () => {
  const { context, calls, state } = load({ values: { "#Accesses": "A,", "#__ddlApplications__ option:selected:text": "Main" } });
  state["#Accesses:saved"] = "A,";
  context.setEntityAccessHideInput("B");
  assert.equal(state["#Accesses"], "A,B,");
  assert.equal(calls.text["#accessSummary"], "2 permissions selected in Main. Unsaved changes.");
  context.setEntityAccessHideInput("B");
  assert.equal(state["#Accesses"], "A,");
  assert.equal(calls.text["#accessSummary"], "1 permission selected in Main.");
});

test("saving permissions confirms the replacement, then posts the unchanged form payload", async () => {
  const { context, calls } = load({ values: { "#Accesses": "A,B,", "#__ddlApplications__ option:selected:text": "Main" } });
  context.saveEntityAccess();
  await settle();
  assert.equal(calls.swal[0].confirmButtonText, "Save permissions");
  assert.match(calls.swal[0].text, /replaced with the 2 selected/);
  assert.equal(calls.ajax[0].url, "/BackOffice/Account/SetAccessForUser");
  assert.equal(calls.ajax[0].data, "form:#frmSaveEntitiesToUser");
});
