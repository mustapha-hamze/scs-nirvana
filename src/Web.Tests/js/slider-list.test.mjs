// Slider list DataTables setup in wwwroot/BackOffice/js/features/slider.js, run with a minimal jQuery stub:
//   node --test src/Web.Tests/js/slider-list.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";

const source = readFileSync(
  new URL("../../Web/wwwroot/BackOffice/js/features/slider.js", import.meta.url),
  "utf8",
);

// Runs getSliderList() against a stubbed $.ajax that answers with `html`; `hasTable` says whether
// that html contains #datatable-buttons. Returns the requests made and the DataTable options used.
function run(hasTable) {
  const calls = { ajax: [], inserted: [], dataTable: [] };
  const $ = (selector) => ({
    length: selector === "#datatable-buttons" && hasTable ? 1 : 0,
    html: (value) => calls.inserted.push([selector, value]),
    DataTable: (options) => calls.dataTable.push([selector, options]),
    addClass: () => {},
  });
  $.ajax = (request) => {
    calls.ajax.push(request);
    return { done: (fn) => fn("<list/>") };
  };
  const context = { $ };
  vm.createContext(context);
  vm.runInContext(source, context);
  context.getSliderList();
  return calls;
}

test("keeps the list request and insertion target", () => {
  const calls = run(true);
  assert.equal(calls.ajax[0].url, "/BackOffice/Slider/List");
  assert.equal(calls.ajax[0].type, "GET");
  assert.deepEqual(calls.inserted[0], ["#findSlidersResultBody", "<list/>"]);
});

test("initialises the table with the shared supporting-list configuration", () => {
  const [[selector, options]] = run(true).dataTable;
  assert.equal(selector, "#datatable-buttons");
  assert.equal(options.pageLength, 25);
  assert.equal(options.lengthChange, false);
  assert.deepEqual(JSON.parse(JSON.stringify(options.order)), [[1, "asc"]]); // Title
  assert.ok(options.columnDefs.some((d) => d.orderable === false && d.targets === -1), "Actions not sortable");
  assert.match(options.language.zeroRecords, /No sliders match your search/);
});

test("an empty list (empty state, no table) is not initialised", () => {
  assert.equal(run(false).dataTable.length, 0);
});
