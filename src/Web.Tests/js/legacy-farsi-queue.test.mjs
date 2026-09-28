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

test("a Queue response becomes a batch: only Queued/AlreadyQueued items with a job are polled", () => {
  const batch = q.batchFromQueue([
    { contentId: 1, outcome: "Queued", jobId: 11 },
    { contentId: 2, outcome: "AlreadyQueued", jobId: 12 },
    { contentId: 3, outcome: "AlreadyReady", jobId: null },
    { contentId: 4, outcome: "Skipped", jobId: null },
    { contentId: 5, outcome: "CultureUnavailable", jobId: null },
    { contentId: 6, outcome: "NotFound", jobId: null },
  ]);
  assert.deepEqual(plain(batch.map((i) => [i.contentId, i.jobId, i.state])),
    [[1, 11, "Queued"], [2, 12, "Queued"], [3, null, "AlreadyReady"], [4, null, "Skipped"], [5, null, "CultureUnavailable"], [6, null, "NotFound"]]);
  assert.deepEqual(plain(q.pendingJobIds(batch)), [11, 12]);
});

test("the Progress request sends only job IDs, as repeated jobIds parameters", () => {
  const request = q.progressRequest([11, 12]);
  assert.deepEqual(plain(request), {
    url: "/BackOffice/LegacyFarsiTranslationQueue/Progress", type: "GET", dataType: "json", traditional: true, data: { jobIds: [11, 12] },
  });
  assert.equal(q.POLL_INTERVAL_MS, 2500);
});

test("merging progress updates job states, keeps unknown or missing ones, and ends polling at terminal", () => {
  let batch = q.batchFromQueue([1, 2, 3, 4, 5].map((id) => ({ contentId: id, outcome: "Queued", jobId: id * 10 })));
  batch = q.mergeProgress(batch, [
    { jobId: 10, state: "Processing" }, { jobId: 20, state: "Succeeded" }, { jobId: 30, state: "Failed", errorCode: "provider_error" },
    { jobId: 40, state: "Superseded" }, { jobId: 50, state: "<script>" }, { jobId: 99, state: "Succeeded" },
  ]);
  assert.deepEqual(plain(batch.map((i) => i.state)), ["Processing", "Succeeded", "Failed", "Superseded", "Queued"]);
  assert.deepEqual(plain(q.pendingJobIds(batch)), [10, 50]);
  batch = q.mergeProgress(batch, [{ jobId: 10, state: "Succeeded" }, { jobId: 50, state: "NotFound" }]);
  assert.deepEqual(plain(q.pendingJobIds(batch)), []);
  assert.equal(JSON.stringify(batch).includes("provider_error"), false); // error codes are never kept for display
});

test("the bar measures completed content items out of those submitted, not a provider estimate", () => {
  const batch = [
    { contentId: 1, jobId: 10, state: "Queued" },
    { contentId: 2, jobId: 20, state: "Processing" },
    { contentId: 3, jobId: 30, state: "Succeeded" },
    { contentId: 4, jobId: 40, state: "Failed" },
    { contentId: 5, jobId: 50, state: "Superseded" },
    { contentId: 6, jobId: null, state: "Skipped" },
    { contentId: 7, jobId: null, state: "AlreadyReady" },
    { contentId: 8, jobId: null, state: "CultureUnavailable" },
    { contentId: 9, jobId: null, state: "NotFound" },
    { contentId: 10, jobId: 100, state: "NotFound" },
  ];
  assert.deepEqual(plain(q.progressCounts(batch)), {
    total: 10, queued: 1, processing: 1, succeeded: 1, failed: 1, superseded: 1, skipped: 5, terminal: 8, percent: 80,
  });
  // A batch with no job at all is complete at once, never stuck below 100%.
  assert.equal(q.progressCounts(q.batchFromQueue([{ contentId: 1, outcome: "AlreadyReady", jobId: null }, { contentId: 2, outcome: "NotFound", jobId: null }])).percent, 100);
  // Floor: never 100% while an item is still pending.
  assert.equal(q.progressCounts([...Array(199)].map((_, i) => ({ contentId: i, jobId: i + 1, state: "Succeeded" }))
    .concat([{ contentId: 999, jobId: 999, state: "Processing" }])).percent, 99);
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
// Queue with `queueResult`. `confirm` is what Swal resolves to. Progress requests wait until the test
// calls answer(data), or answer(null) for a failure. Timers are fake: tick() runs the pending one.
function page({ workerEnabled = true, maxItems = 2, candidates, queueResult, queueStatus = 200, confirm = true, getFails = false }) {
  const ids = ["lfqRows", "lfqSelectedCount", "lfqClear", "lfqQueue", "lfqCultureUnavailable", "lfqPage", "lfqPrev",
    "lfqNext", "lfqPageInfo", "lfqSummary", "lfqSummaryTitle", "lfqSummaryList", "lfqFilters", "lfqTypeId", "lfqTitle",
    "lfqContentId", "lfqSort", "lfqDescending", "lfqProgressBar", "lfqProgressCounts", "lfqPollStatus"];
  const elements = Object.fromEntries(ids.map((id) => [id, new Node("div")]));
  elements.lfqSort.value = "Id";
  elements.lfqDescending.value = "false";
  const root = new Node("div");
  root.setAttribute("data-worker-enabled", String(workerEnabled));
  root.setAttribute("data-max-items", String(maxItems));
  root.setAttribute("data-type-ids", "1001,1003");

  const calls = { ajax: [], swal: [], messages: [], timers: [], progress: [] };
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
      if (request.url.endsWith("/Progress")) {
        calls.progress.push((data) => {
          if (data) ok(plain(data));
          else fail({ status: 503, responseText: "<b>raw failure</b>" });
          always && always();
        });
        return promise;
      }
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
  const windowListeners = {};
  const context = load({
    $,
    document,
    setTimeout: (fn, ms) => calls.timers.push({ fn, ms, cleared: false }),
    clearTimeout: (id) => { calls.timers[id - 1].cleared = true; },
    window: { Swal: true, addEventListener: (type, fn) => (windowListeners[type] ||= []).push(fn) },
    Swal: { fire: (options) => { calls.swal.push(options); return Promise.resolve({ isConfirmed: confirm }); } },
    messageBox: (title, text) => calls.messages.push([title, text]),
  });
  context.LegacyFarsiQueue.init(root);
  elements.lfqProgressBar.style = {};
  const boxes = () => elements.lfqRows.all((n) => n.type === "checkbox");
  const pendingTimers = () => calls.timers.filter((t) => !t.cleared && !t.ran);
  const tick = () => {
    const [timer] = pendingTimers();
    assert.ok(timer, "a poll is scheduled");
    timer.ran = true;
    timer.fn();
  };
  const answer = async (data) => {
    calls.progress.shift()(data);
    await flush();
  };
  const unload = (type = "pagehide") => (windowListeners[type] || []).forEach((fn) => fn());
  return { elements, calls, boxes, tick, answer, pendingTimers, unload };
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

test("confirming posts only the selected IDs, shows every outcome, and keeps the list while jobs are pending", async () => {
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
  assert.equal(calls.ajax.at(-1).type, "POST"); // no candidate reload while a job is pending
  assert.equal(boxes().length, 2); // row 1 is locked while queued; 2 and 3 stay selectable
  assert.match(elements.lfqRows.text, /Queued/);
  assert.equal(elements.lfqSelectedCount.textContent, "0 selected");
  assert.equal(elements.lfqSummaryTitle.textContent, "1 of 2 submitted items complete, checking every few seconds.");
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

test("the script polls only with $.ajax and a timeout chain and never injects HTML", () => {
  for (const banned of [/setInterval/, /EventSource/, /WebSocket/, /signalr/i, /innerHTML/, /outerHTML/, /insertAdjacentHTML/, /\.html\(/, /fetch\(/, /errorCode/]) {
    assert.doesNotMatch(source, banned, String(banned));
  }
});

// ---- Live progress ----

async function queued(queueResult, candidates = [candidatePage([1, 2, 3])]) {
  const view = page({ maxItems: 5, candidates, queueResult });
  await flush();
  for (const box of view.boxes()) {
    box.checked = true;
    box.fire("change");
  }
  view.elements.lfqQueue.fire("click");
  await flush();
  await flush();
  await flush();
  return view;
}

const threeJobs = { items: [1, 2, 3].map((id) => ({ contentId: id, outcome: "Queued", jobId: id * 10 })) };
const progressGets = (calls) => calls.ajax.filter((r) => r.url.endsWith("/Progress"));

test("polls every 2,500 ms with one request in flight, updates in place, and stops at terminal", async () => {
  const { elements, calls, tick, answer, pendingTimers } = await queued(threeJobs);

  assert.deepEqual(calls.timers.map((t) => t.ms), [2500]);
  tick();
  assert.deepEqual(plain(progressGets(calls)[0].data), { jobIds: [10, 20, 30] });
  assert.equal(pendingTimers().length, 0); // nothing scheduled while the request is in flight
  assert.equal(progressGets(calls).length, 1);

  await answer({ items: [{ jobId: 10, state: "Processing" }, { jobId: 20, state: "Succeeded" }, { jobId: 30, state: "Queued" }] });
  assert.deepEqual(elements.lfqSummaryList.children.map((c) => c.textContent),
    ["Content 1: Translating", "Content 2: Translated", "Content 3: Queued"]);
  assert.match(elements.lfqRows.text, /Translating/);
  assert.equal(elements.lfqProgressBar.style.width, "33%");
  assert.equal(elements.lfqProgressBar.getAttribute("aria-valuenow"), "33");
  assert.equal(elements.lfqProgressBar.getAttribute("aria-valuetext"), "1 of 3 content items complete");
  assert.equal(calls.ajax.filter((r) => r.url.endsWith("/Candidates")).length, 1); // no reload yet

  tick();
  assert.deepEqual(plain(progressGets(calls)[1].data), { jobIds: [10, 30] }); // only pending jobs are asked for
  await answer({ items: [{ jobId: 10, state: "Failed", errorCode: "provider_error" }, { jobId: 30, state: "Superseded" }] });

  assert.equal(pendingTimers().length, 0); // stopped
  assert.equal(progressGets(calls).length, 2);
  assert.equal(elements.lfqProgressBar.style.width, "100%");
  assert.equal(elements.lfqProgressCounts.textContent,
    "Submitted 3 · Queued 0 · Translating 0 · Translated 1 · Failed 1 · Superseded 1 · Skipped or not queued 0");
  assert.equal(elements.lfqSummaryTitle.textContent, "3 of 3 submitted items complete.");
  assert.doesNotMatch(elements.lfqSummary.text, /provider_error|\b10\b|\b30\b/); // no error codes or job IDs
  await flush();
  assert.equal(calls.ajax.at(-1).url, "/BackOffice/LegacyFarsiTranslationQueue/Candidates"); // reconciled after completion
  assert.equal(elements.lfqSummary.hidden, false); // summary stays
});

test("terminal non-job outcomes complete the bar and need no polling", async () => {
  const { elements, calls } = await queued({
    items: [{ contentId: 1, outcome: "AlreadyReady", jobId: null }, { contentId: 2, outcome: "Skipped", jobId: null }, { contentId: 3, outcome: "NotFound", jobId: null }],
  });

  assert.equal(calls.timers.length, 0);
  assert.equal(progressGets(calls).length, 0);
  assert.equal(elements.lfqProgressBar.style.width, "100%");
  assert.match(elements.lfqProgressCounts.textContent, /Skipped or not queued 3$/);
  assert.equal(calls.ajax.at(-1).type, "GET"); // reconciled at once
});

test("a failed poll keeps the last known states and retries on the next interval", async () => {
  const { elements, calls, tick, answer } = await queued(threeJobs);
  tick();
  await answer({ items: [{ jobId: 10, state: "Succeeded" }, { jobId: 20, state: "Processing" }, { jobId: 30, state: "Queued" }] });
  tick();
  await answer(null);

  assert.deepEqual(elements.lfqSummaryList.children.map((c) => c.textContent),
    ["Content 1: Translated", "Content 2: Translating", "Content 3: Queued"]);
  assert.equal(elements.lfqPollStatus.hidden, false);
  assert.match(elements.lfqPollStatus.textContent, /temporarily unavailable\. Retrying/);
  assert.doesNotMatch(elements.lfqSummary.text, /raw failure/);

  tick();
  assert.deepEqual(plain(progressGets(calls)[2].data), { jobIds: [20, 30] });
  await answer({ items: [{ jobId: 20, state: "Succeeded" }, { jobId: 30, state: "Processing" }] });
  assert.equal(elements.lfqPollStatus.hidden, true);
});

test("unloading the page cancels the scheduled poll and ignores a late response", async () => {
  const { calls, pendingTimers, unload } = await queued(threeJobs);
  unload();
  assert.equal(pendingTimers().length, 0);
  assert.equal(calls.timers[0].cleared, true);

  const second = await queued(threeJobs);
  second.tick();
  second.unload("beforeunload");
  await second.answer({ items: [{ jobId: 10, state: "Processing" }] });
  assert.equal(second.pendingTimers().length, 0); // nothing rescheduled after unload
  assert.equal(progressGets(second.calls).length, 1);
});

test("server text in progress responses is written as text, never HTML", async () => {
  const { elements, tick, answer } = await queued(threeJobs, [candidatePage([1, 2, 3])]);
  tick();
  await answer({ items: [{ jobId: 10, state: "<img src=x onerror=alert(1)>" }, { jobId: 20, state: "Succeeded", contentId: "<b>x</b>" }] });

  assert.deepEqual(elements.lfqSummaryList.children.map((c) => c.textContent),
    ["Content 1: Queued", "Content 2: Translated", "Content 3: Queued"]); // unknown state ignored
  assert.match(elements.lfqRows.text, /<b>Title 1<\/b>/); // candidate titles remain plain text
  assert.ok(elements.lfqSummaryList.children.every((c) => c.tagName === "li" && c.children.length === 0)); // text-only items
});
