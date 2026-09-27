// Legacy Farsi Translation Queue dashboard script (wwwroot/BackOffice/js/features/legacy-farsi-queue.js),
// run against a minimal DOM and $.ajax stub:
//   node --test src/Web.Tests/js/legacy-farsi-queue.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";

const source = readFileSync(
  new URL("../../Web/wwwroot/BackOffice/js/features/legacy-farsi-queue.js", import.meta.url),
  "utf8",
);

function load(extra = {}) {
  const context = { ...extra };
  vm.createContext(context);
  vm.runInContext(source, context);
  return context;
}

const q = load().LegacyFarsiQueue;
const plain = (value) => JSON.parse(JSON.stringify(value));

// ---- Pure helpers ----

test("buildQuery sends only the typed Phase 1 parameters", () => {
  assert.deepEqual(
    plain(q.buildQuery({ typeId: "1003", title: "  news ", contentId: "42", sort: "Title", descending: "true" }, 3, [1001, 1003])),
    { sort: "Title", descending: true, page: 3, pageSize: 25, typeId: 1003, title: "news", contentId: 42 },
  );
});

test("buildQuery drops unconfigured types, undefined sorts, blank and invalid values", () => {
  assert.deepEqual(
    plain(q.buildQuery({ typeId: "999", title: "   ", contentId: "-4", sort: "FarsiContent", descending: "false" }, 0, [1001])),
    { sort: "Id", descending: false, page: 1, pageSize: 25 },
  );
  for (const bad of ["0", "1.5", "1e3", "abc", "", " "]) assert.equal(q.parsePositiveInt(bad), null, bad);
  assert.equal(q.parsePositiveInt(" 7 "), 7);
});

test("pageCount is at least one page", () => {
  assert.equal(q.pageCount(0, 25), 1);
  assert.equal(q.pageCount(25, 25), 1);
  assert.equal(q.pageCount(26, 25), 2);
});

test("toggle never selects more than the limit and never duplicates", () => {
  let selected = [];
  for (const id of [1, 2, 3]) selected = q.toggle(selected, id, true, 2);
  assert.deepEqual(plain(selected), [1, 2]);
  assert.deepEqual(plain(q.toggle(selected, 1, true, 2)), [1, 2]);
  assert.deepEqual(plain(q.toggle(selected, 1, false, 2)), [2]);
});

test("queueing needs the worker, an available culture, a selection and no request in flight", () => {
  assert.equal(q.canQueue(true, true, 1, false), true);
  assert.equal(q.canQueue(false, true, 1, false), false);
  assert.equal(q.canQueue(true, false, 1, false), false);
  assert.equal(q.canQueue(true, null, 1, false), false);
  assert.equal(q.canQueue(true, true, 0, false), false);
  assert.equal(q.canQueue(true, true, 1, true), false);
  assert.match(q.unavailableReason(false, true), /Background translation is unavailable/);
  assert.match(q.unavailableReason(true, false), /culture is unavailable/);
});

test("confirmation states the count and that each item creates or reuses a job", () => {
  assert.match(q.confirmText(3), /3 content items/);
  assert.match(q.confirmText(1), /1 content item\?/);
  assert.match(q.confirmText(3), /create or reuse a background translation job/);
});

test("the Queue request posts JSON with only content IDs", () => {
  const request = q.queueRequest([5, 9]);
  assert.equal(request.url, "/BackOffice/LegacyFarsiTranslationQueue/Queue");
  assert.equal(request.type, "POST");
  assert.equal(request.contentType, "application/json");
  assert.equal(request.data, '{"contentIds":[5,9]}');
  assert.equal(request.headers, undefined); // antiforgery comes from the layout's $.ajaxSetup
});

test("summarize counts every returned outcome", () => {
  assert.deepEqual(plain(q.summarize([{ outcome: "Queued" }, { outcome: "Skipped" }, { outcome: "Queued" }])), {
    total: 3,
    counts: { Queued: 2, Skipped: 1 },
  });
});

// ---- Page, over a minimal DOM ----

class Node {
  constructor(tag) {
    this.tagName = tag;
    this.children = [];
    this.attributes = {};
    this.listeners = {};
    this.textContent = "";
    this.hidden = false;
    this.disabled = false;
    this.value = "";
  }
  appendChild(child) { this.children.push(child); return child; }
  replaceChildren() { this.children = []; }
  setAttribute(name, value) { this.attributes[name] = String(value); }
  getAttribute(name) { return name in this.attributes ? this.attributes[name] : null; }
  addEventListener(type, fn) { (this.listeners[type] ||= []).push(fn); }
  fire(type, event = {}) { for (const fn of this.listeners[type] || []) fn({ preventDefault() {}, ...event }); }
  all(predicate) {
    const found = [];
    const walk = (n) => { for (const c of n.children) { if (predicate(c)) found.push(c); walk(c); } };
    walk(this);
    return found;
  }
  get text() { return this.textContent + this.children.map((c) => c.text).join(" "); }
}

const flush = () => new Promise((resolve) => setImmediate(resolve));

// Builds the dashboard, answers Candidates with `pages` in order (then the last one again) and
// Queue with `queueResult`. `confirm` is what Swal resolves to.
function page({ workerEnabled = true, maxItems = 2, candidates, queueResult, queueStatus = 200, confirm = true, getFails = false }) {
  const ids = ["lfqRows", "lfqSelectedCount", "lfqClear", "lfqQueue", "lfqCultureUnavailable", "lfqPage", "lfqPrev",
    "lfqNext", "lfqPageInfo", "lfqSummary", "lfqSummaryTitle", "lfqSummaryList", "lfqFilters", "lfqTypeId", "lfqTitle",
    "lfqContentId", "lfqSort", "lfqDescending"];
  const elements = Object.fromEntries(ids.map((id) => [id, new Node("div")]));
  elements.lfqSort.value = "Id";
  elements.lfqDescending.value = "false";
  const root = new Node("div");
  root.setAttribute("data-worker-enabled", String(workerEnabled));
  root.setAttribute("data-max-items", String(maxItems));
  root.setAttribute("data-type-ids", "1001,1003");

  const calls = { ajax: [], swal: [], messages: [] };
  const responses = [...candidates];
  const $ = {
    ajax(request) {
      calls.ajax.push(request);
      let ok, fail, always;
      const promise = {
        done(fn) { ok = fn; return promise; },
        fail(fn) { fail = fn; return promise; },
        always(fn) { always = fn; return promise; },
      };
      setImmediate(() => {
        if (request.type === "GET" && getFails) fail({ status: 500 });
        else if (request.type === "GET") ok(plain(responses.length > 1 ? responses.shift() : responses[0]));
        else if (queueStatus === 200) ok(plain(queueResult));
        else fail({ status: queueStatus, responseJSON: queueResult });
        always && always();
      });
      return promise;
    },
  };
  const document = {
    getElementById: (id) => elements[id],
    createElement: (tag) => new Node(tag),
  };
  const context = load({
    $,
    document,
    window: { Swal: true },
    Swal: { fire: (options) => { calls.swal.push(options); return Promise.resolve({ isConfirmed: confirm }); } },
    messageBox: (title, text) => calls.messages.push([title, text]),
  });
  context.LegacyFarsiQueue.init(root);
  const boxes = () => elements.lfqRows.all((n) => n.type === "checkbox");
  return { elements, calls, boxes };
}

const candidatePage = (ids, { cultureAvailable = true, totalCount = ids.length, page = 1 } = {}) => ({
  cultureAvailable,
  items: ids.map((id) => ({ contentId: id, title: `<b>Title ${id}</b>`, typeId: 1001, isActive: id % 2 === 0, updatedAt: "2026-09-27T10:11:12" })),
  totalCount,
  page,
  pageSize: 25,
});

test("loads the first page with a typed GET and renders rows as text", async () => {
  const { elements, calls, boxes } = page({ candidates: [candidatePage([1, 2, 3], { totalCount: 60 })] });
  await flush();

  assert.deepEqual(plain(calls.ajax[0]), {
    url: "/BackOffice/LegacyFarsiTranslationQueue/Candidates",
    type: "GET",
    dataType: "json",
    data: { sort: "Id", descending: false, page: 1, pageSize: 25 },
  });
  assert.equal(boxes().length, 3);
  assert.match(elements.lfqRows.text, /<b>Title 1<\/b>/); // stored as text, never parsed as HTML
  assert.match(elements.lfqRows.text, /2026-09-27 10:11/);
  assert.equal(elements.lfqPage.children.length, 3);
  assert.equal(elements.lfqPrev.disabled, true);
  assert.equal(elements.lfqNext.disabled, false);
  assert.equal(elements.lfqQueue.disabled, true); // nothing selected yet
});

test("filters and paging send the typed query", async () => {
  const { elements, calls } = page({ candidates: [candidatePage([1], { totalCount: 60 })] });
  await flush();
  elements.lfqTypeId.value = "1003";
  elements.lfqTitle.value = " abc ";
  elements.lfqContentId.value = "";
  elements.lfqSort.value = "UpdatedAt";
  elements.lfqDescending.value = "true";
  elements.lfqFilters.fire("submit");
  await flush();
  elements.lfqNext.fire("click");
  await flush();

  assert.deepEqual(plain(calls.ajax[1].data), { sort: "UpdatedAt", descending: true, page: 1, pageSize: 25, typeId: 1003, title: "abc" });
  assert.deepEqual(plain(calls.ajax[2].data), { sort: "UpdatedAt", descending: true, page: 2, pageSize: 25, typeId: 1003, title: "abc" });
});

test("an invalid content ID is rejected before any request", async () => {
  const { elements, calls } = page({ candidates: [candidatePage([1])] });
  await flush();
  elements.lfqContentId.value = "-3";
  elements.lfqFilters.fire("submit");
  await flush();

  assert.equal(calls.ajax.length, 1);
  assert.match(elements.lfqRows.text, /positive whole-number content ID/);
});

test("selection is capped at the configured limit", async () => {
  const { elements, boxes } = page({ maxItems: 2, candidates: [candidatePage([1, 2, 3])] });
  await flush();
  for (const index of [0, 1]) {
    const box = boxes()[index];
    box.checked = true;
    box.fire("change");
  }

  assert.equal(elements.lfqSelectedCount.textContent, "2 selected (limit reached)");
  assert.deepEqual(boxes().map((b) => b.disabled), [false, false, true]);
  assert.equal(elements.lfqQueue.disabled, false);
});

test("confirming posts only the selected IDs, shows every outcome, and reloads the list", async () => {
  const { elements, calls, boxes } = page({
    candidates: [candidatePage([1, 2, 3]), candidatePage([3])],
    queueResult: { items: [{ contentId: 1, outcome: "Queued", jobId: 11 }, { contentId: 2, outcome: "Skipped", jobId: null }] },
  });
  await flush();
  for (const index of [0, 1]) {
    boxes()[index].checked = true;
    boxes()[index].fire("change");
  }
  elements.lfqQueue.fire("click");
  await flush();
  await flush();
  await flush();

  assert.match(calls.swal[0].text, /2 content items\? Each selected item will create or reuse a background translation job/);
  const post = calls.ajax.find((r) => r.type === "POST");
  assert.equal(post.data, '{"contentIds":[1,2]}');
  assert.equal(post.contentType, "application/json");
  assert.equal(elements.lfqSummary.hidden, false);
  assert.deepEqual(elements.lfqSummaryList.children.map((c) => c.textContent), ["Content 1: Queued", "Content 2: Skipped"]);
  assert.doesNotMatch(elements.lfqSummary.text, /11/); // job IDs are not shown
  assert.equal(calls.ajax.at(-1).type, "GET"); // reconciled
  assert.equal(boxes().length, 1);
  assert.equal(elements.lfqSelectedCount.textContent, "0 selected");
});

test("cancelling the confirmation posts nothing", async () => {
  const { elements, calls, boxes } = page({ confirm: false, candidates: [candidatePage([1])] });
  await flush();
  boxes()[0].checked = true;
  boxes()[0].fire("change");
  elements.lfqQueue.fire("click");
  await flush();
  await flush();

  assert.equal(calls.swal.length, 1);
  assert.equal(calls.ajax.filter((r) => r.type === "POST").length, 0);
});

test("a rejected request shows only the server's fixed message", async () => {
  const { elements, calls, boxes } = page({
    candidates: [candidatePage([1])],
    queueStatus: 400,
    queueResult: { error: "Select 1 to 2 content items with positive IDs." },
  });
  await flush();
  boxes()[0].checked = true;
  boxes()[0].fire("change");
  elements.lfqQueue.fire("click");
  await flush();
  await flush();
  await flush();

  assert.deepEqual(calls.messages[0], ["Not queued", "Select 1 to 2 content items with positive IDs."]);
});

test("an unavailable culture shows the state and offers no selection", async () => {
  const { elements, boxes } = page({ candidates: [candidatePage([], { cultureAvailable: false })] });
  await flush();

  assert.equal(elements.lfqCultureUnavailable.hidden, false);
  assert.equal(boxes().length, 0);
  assert.equal(elements.lfqQueue.disabled, true);
});

test("a disabled worker allows read-only viewing but no selection or queueing", async () => {
  const { elements, boxes } = page({ workerEnabled: false, candidates: [candidatePage([1, 2])] });
  await flush();

  assert.match(elements.lfqRows.text, /Title 1/);
  assert.equal(boxes().length, 0);
  assert.equal(elements.lfqQueue.disabled, true);
});

test("a failed candidate load shows an error state and disables queueing", async () => {
  const { elements, boxes } = page({ getFails: true, candidates: [candidatePage([1])] });
  await flush();

  assert.match(elements.lfqRows.text, /could not be loaded/);
  assert.equal(boxes().length, 0);
  assert.equal(elements.lfqQueue.disabled, true);
});

test("the script never polls or calls a progress endpoint and never injects HTML", () => {
  for (const banned of [/setInterval/, /setTimeout/, /EventSource/, /WebSocket/, /signalr/i, /Progress/, /innerHTML/, /outerHTML/, /insertAdjacentHTML/, /\.html\(/, /fetch\(/]) {
    assert.doesNotMatch(source, banned, String(banned));
  }
});
