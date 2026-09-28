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
const REASON = "Translation provider could not complete the request. Try again later."; // a fixed server message for provider_error

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
    { jobId: 10, state: "Processing", failureReason: "stale" }, { jobId: 20, state: "Succeeded" },
    { jobId: 30, state: "Failed", errorCode: "provider_error", failureReason: REASON },
    { jobId: 40, state: "Superseded" }, { jobId: 50, state: "<script>" }, { jobId: 99, state: "Succeeded" },
  ]);
  assert.deepEqual(plain(batch.map((i) => i.state)), ["Processing", "Succeeded", "Failed", "Superseded", "Queued"]);
  assert.deepEqual(plain(q.pendingJobIds(batch)), [10, 50]);
  assert.deepEqual(plain(batch.map((i) => i.failureReason ?? null)), [null, null, REASON, null, null]); // Failed only
  assert.deepEqual(batch.map(q.batchLine), ["Content 1: Translating", "Content 2: Translated", `Content 3: Failed — ${REASON}`,
    "Content 4: Superseded (source changed)", "Content 5: Queued"]);
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
  focus() { Node.focused = this; }
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

// Builds the dashboard, answers Candidates with `pages` in order (then the last one again), RecoveredJobs
// with `recovered` likewise (an empty page by default) and
// Queue with `queueResult` (or queueResult(contentIds)). `confirm` is what Swal resolves to. Progress requests wait until the test
// calls answer(data), or answer(null) for a failure. Timers are fake: tick() runs the pending one.
function page({ workerEnabled = true, maxItems = 2, candidates, recovered = [recoveredPage([])], recoveredFails = false, queueResult, queueStatus = 200, confirm = true,
  getFails = false }) {
  const ids = ["lfqRows", "lfqSelectedCount", "lfqClear", "lfqQueue", "lfqCultureUnavailable", "lfqPage", "lfqPrev",
    "lfqNext", "lfqPageInfo", "lfqSummary", "lfqSummaryTitle", "lfqSummaryList", "lfqFilters", "lfqTypeId", "lfqTitle",
    "lfqContentId", "lfqSort", "lfqDescending", "lfqProgressBar", "lfqProgressCounts", "lfqPollStatus", "lfqRecoveredRows",
    "lfqRecoveredPrev", "lfqRecoveredNext", "lfqRecoveredInfo", "lfqTabCandidates", "lfqTabRecovered", "lfqPanelCandidates",
    "lfqPanelRecovered", "lfqRecoveredTabCount", "lfqRecoveredStatusTitle", "lfqRecoveredStatusCounts"];
  const elements = Object.fromEntries(ids.map((id) => [id, new Node("div")]));
  elements.lfqSort.value = "Id";
  elements.lfqDescending.value = "false";
  const root = new Node("div");
  root.setAttribute("data-worker-enabled", String(workerEnabled));
  root.setAttribute("data-max-items", String(maxItems));
  root.setAttribute("data-type-ids", "1001,1003");

  const calls = { ajax: [], swal: [], messages: [], timers: [], progress: [] };
  const responses = [...candidates];
  // recovered may also be a function of the request's data (a fake server); it returns null for a failure.
  const recoveredResponses = typeof recovered === "function" ? [] : [...recovered];
  const answerRecovered = (data, ok, fail) => {
    const response = typeof recovered === "function" ? recovered(data) : next(recoveredResponses);
    if (recoveredFails || response == null) fail({ status: 500, responseText: "<b>raw failure</b>" });
    else ok(plain(response));
  };
  const next = (list) => plain(list.length > 1 ? list.shift() : list[0]);
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
        else if (request.url.endsWith("/RecoveredJobs")) answerRecovered(request.data, ok, fail);
        else if (request.type === "GET") ok(next(responses));
        else if (queueStatus === 200) ok(plain(typeof queueResult === "function" ? queueResult(JSON.parse(request.data).contentIds) : queueResult));
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

function recoveredPage(jobs, { totalCount = jobs.length, page = 1, counts } = {}) {
  const count = (state) => jobs.filter((j) => j[2] === state).length;
  counts ??= { queued: count("Queued"), processing: count("Processing"), succeeded: count("Succeeded"), failed: count("Failed"), superseded: count("Superseded") };
  counts = { ...counts, active: counts.queued + counts.processing };
  counts.total = counts.active + counts.succeeded + counts.failed + counts.superseded;
  return {
    cultureAvailable: true,
    items: jobs.map(([jobId, contentId, state, attemptCount = 1]) => ({
      jobId, contentId, title: `<i>Recovered ${contentId}</i>`, typeId: 1003, isActive: true, state, attemptCount,
      // errorCode is no longer sent; it is kept here to prove the script ignores it.
      errorCode: state === "Failed" ? "provider_error" : null, failureReason: state === "Failed" ? REASON : null, relevantAt: "2026-09-28T08:09:10",
    })),
    totalCount,
    page,
    pageSize: 25,
    counts,
  };
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

  const gets = calls.ajax.filter((r) => r.url.endsWith("/Candidates"));
  assert.deepEqual(plain(gets[1].data), { sort: "UpdatedAt", descending: true, page: 1, pageSize: 25, typeId: 1003, title: "abc" });
  assert.deepEqual(plain(gets[2].data), { sort: "UpdatedAt", descending: true, page: 2, pageSize: 25, typeId: 1003, title: "abc" });
});

test("an invalid content ID is rejected before any request", async () => {
  const { elements, calls } = page({ candidates: [candidatePage([1])] });
  await flush();
  elements.lfqContentId.value = "-3";
  elements.lfqFilters.fire("submit");
  await flush();

  assert.equal(calls.ajax.filter((r) => r.url.endsWith("/Candidates")).length, 1);
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
  assert.equal(calls.ajax.filter((r) => r.url.endsWith("/Candidates")).length, 1); // no candidate reload while a job is pending
  assert.equal(calls.ajax.at(-1).url, "/BackOffice/LegacyFarsiTranslationQueue/RecoveredJobs"); // only the recovered counts are re-read
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
  await answer({ items: [{ jobId: 10, state: "Failed", errorCode: "provider_error", failureReason: REASON }, { jobId: 30, state: "Superseded" }] });

  assert.deepEqual(elements.lfqSummaryList.children.map((c) => c.textContent),
    [`Content 1: Failed — ${REASON}`, "Content 2: Translated", "Content 3: Superseded (source changed)"]);
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

test("a second submission keeps the first batch's running items, and both batches count toward one bar", async () => {
  const jobsFor = (ids) => ({ items: ids.map((id) => ({ contentId: id, outcome: id === 4 ? "NotFound" : "Queued", jobId: id === 4 ? null : id * 10 })) });
  const view = page({ maxItems: 5, candidates: [candidatePage([1, 2, 3, 4])], queueResult: jobsFor });
  const { elements, calls, boxes, tick, answer, pendingTimers } = view;
  await flush();
  const select = async (ids) => {
    for (const box of boxes().filter((b) => ids.some((id) => b.getAttribute("aria-label") === `Select content ${id}`))) {
      box.checked = true;
      box.fire("change");
    }
    elements.lfqQueue.fire("click");
    for (let i = 0; i < 3; i++) await flush();
  };

  await select([1, 2]);
  tick();
  await answer({ items: [{ jobId: 10, state: "Succeeded" }, { jobId: 20, state: "Processing" }] });
  assert.equal(elements.lfqProgressBar.style.width, "50%");

  await select([3, 4]); // 1 and 2 are locked while known; 4 comes back NotFound with no job
  assert.deepEqual(plain(JSON.parse(calls.ajax.filter((r) => r.type === "POST").at(-1).data)), { contentIds: [3, 4] });
  assert.equal(pendingTimers().length, 1); // still one poll chain
  assert.deepEqual(elements.lfqSummaryList.children.map((c) => c.textContent),
    ["Content 1: Translated", "Content 2: Translating", "Content 3: Queued", "Content 4: Not found"]);
  assert.equal(elements.lfqProgressBar.style.width, "50%"); // 2 of 4 complete

  tick();
  assert.deepEqual(plain(progressGets(calls).at(-1).data), { jobIds: [20, 30] });
  await answer({ items: [{ jobId: 20, state: "Succeeded" }, { jobId: 30, state: "Processing" }] });
  assert.equal(elements.lfqProgressBar.style.width, "75%");
  assert.equal(calls.ajax.filter((r) => r.url.endsWith("/Candidates")).length, 1); // no reconciliation while one is running

  tick();
  await answer({ items: [{ jobId: 30, state: "Failed", errorCode: "provider_error" }] });
  await flush();
  assert.equal(pendingTimers().length, 0);
  assert.equal(elements.lfqProgressBar.style.width, "100%");
  assert.equal(elements.lfqSummaryTitle.textContent, "4 of 4 submitted items complete.");
  assert.equal(calls.ajax.at(-1).url, "/BackOffice/LegacyFarsiTranslationQueue/Candidates"); // reconciled once all are terminal
  assert.equal(elements.lfqSummary.hidden, false);
  assert.equal(elements.lfqSummaryList.children.length, 4); // the reload leaves the summary as it was
  assert.doesNotMatch(elements.lfqSummary.text, /provider_error/);
});

// ---- Recovery after a refresh ----

const recoveredGets = (calls) => calls.ajax.filter((r) => r.url.endsWith("/RecoveredJobs"));
const recoveredTexts = (elements) => elements.lfqRecoveredRows.children.map((tr) => tr.children.map((td) => td.text.trim()));

test("recovery helpers: a bounded paged GET, one poll per job across batch and recovered, batch jobs not shown twice", () => {
  assert.deepEqual(plain(q.recoveredRequest(0)), {
    url: "/BackOffice/LegacyFarsiTranslationQueue/RecoveredJobs", type: "GET", dataType: "json", data: { page: 1, pageSize: 25 },
  });
  const batch = [{ contentId: 1, jobId: 10, state: "Queued" }, { contentId: 2, jobId: null, state: "Skipped" }];
  const recovered = [{ jobId: 10, state: "Queued" }, { jobId: 20, state: "Processing" }, { jobId: 30, state: "Failed" }, { jobId: 40, state: "Queued" }];
  assert.deepEqual(plain(q.pendingSets(batch, recovered)), { batch: [10], recovered: [20, 40] }); // 10 once, in the batch
  assert.deepEqual(plain(q.recoveredToShow(recovered, batch).map((j) => j.jobId)), [20, 30, 40]);
});

test("a fresh page loads recovered jobs separately from candidates and says why the candidate list is empty", async () => {
  const { elements, calls } = page({
    candidates: [candidatePage([])],
    recovered: [recoveredPage([[10, 1, "Queued"], [20, 2, "Failed", 3], [30, 3, "Succeeded"]])],
  });
  await flush();

  assert.deepEqual(plain(recoveredGets(calls)[0].data), { page: 1, pageSize: 25 });
  assert.deepEqual(recoveredTexts(elements), [
    ["1", "<i>Recovered 1</i>", "1003", "Active", "Queued", "— No failure reason", "1", "2026-09-28 08:09"],
    ["2", "<i>Recovered 2</i>", "1003", "Active", "Failed", REASON, "3", "2026-09-28 08:09"],
    ["3", "<i>Recovered 3</i>", "1003", "Active", "Translated", "— No failure reason", "1", "2026-09-28 08:09"],
  ]);
  assert.equal(progressGets(calls).length, 0); // the reason comes with the recovered read, no Progress needed
  assert.equal(elements.lfqRecoveredInfo.textContent, "3 jobs · page 1 of 1");
  assert.match(elements.lfqRows.text, /Content with a current translation job is not listed here; see the Recovered Jobs tab/);
  assert.doesNotMatch(elements.lfqRows.text, /No legacy Farsi content matches/);
  // No batch panel or percentage: nothing was submitted this session.
  assert.equal(elements.lfqSummaryTitle.textContent, "");
  assert.equal(elements.lfqProgressBar.style.width, undefined);
  // Job IDs and error codes are never displayed; every cell is text only.
  assert.doesNotMatch(elements.lfqRecoveredRows.text, /provider_error|\b10\b|\b20\b|\b30\b/);
  assert.ok(elements.lfqRecoveredRows.all((n) => n.tagName === "td").every((td) => td.children.every((c) => c.tagName === "span" && c.children.length === 0)));
});

test("with no jobs the recovered section has its own empty state", async () => {
  const { elements } = page({ candidates: [candidatePage([1])] });
  await flush();

  assert.match(elements.lfqRecoveredRows.text, /No current or recent translation jobs\./);
  assert.equal(elements.lfqRecoveredNext.disabled, true);
});

test("an active recovered job is polled without any submission, updates in place, and polling stops at terminal", async () => {
  const { elements, calls, tick, answer, pendingTimers } = page({
    candidates: [candidatePage([]), candidatePage([2])],
    recovered: [recoveredPage([[10, 1, "Queued", 0], [20, 2, "Processing"], [30, 3, "Succeeded"]]),
      recoveredPage([[10, 1, "Succeeded"], [20, 2, "Failed", 2], [30, 3, "Succeeded"]])],
  });
  await flush();

  assert.deepEqual(calls.timers.map((t) => t.ms), [2500]);
  tick();
  assert.deepEqual(plain(progressGets(calls)[0].data), { jobIds: [10, 20] }); // only active jobs
  assert.equal(pendingTimers().length, 0); // one request at a time
  await answer({ items: [{ jobId: 10, state: "Processing", attemptCount: 1 }, { jobId: 20, state: "Processing", attemptCount: 1 }] });
  assert.deepEqual(recoveredTexts(elements).map((r) => [r[4], r[6]]), [["Translating", "1"], ["Translating", "1"], ["Translated", "1"]]);

  tick();
  await answer({ items: [{ jobId: 10, state: "Succeeded", attemptCount: 1 },
    { jobId: 20, state: "Failed", attemptCount: 2, errorCode: "provider_error", failureReason: REASON }] });
  assert.deepEqual(recoveredTexts(elements).map((r) => [r[4], r[5]]),
    [["Translated", "— No failure reason"], ["Failed", REASON], ["Translated", "— No failure reason"]]);
  await flush();
  assert.deepEqual(recoveredTexts(elements).map((r) => r[4]), ["Translated", "Failed", "Translated"]);
  assert.equal(pendingTimers().length, 0);
  assert.equal(progressGets(calls).length, 2);
  assert.equal(elements.lfqSummaryTitle.textContent, ""); // still no batch panel or percentage
  assert.equal(elements.lfqProgressBar.style.width, undefined);
  assert.doesNotMatch(elements.lfqRecoveredRows.text, /provider_error/);
  assert.equal(calls.ajax.at(-1).url, "/BackOffice/LegacyFarsiTranslationQueue/Candidates"); // reconciled once all are terminal
  assert.equal(recoveredGets(calls).length, 2); // rows updated in place, then re-read once to reconcile
  assert.deepEqual(recoveredTexts(elements).map((r) => r[4]), ["Translated", "Failed", "Translated"]);
  assert.equal(pendingTimers().length, 0); // the reconciled page has nothing pending
});

test("a submission whose job is also recovered is polled once and shown once, and keeps its own batch bar", async () => {
  const view = page({
    maxItems: 5,
    candidates: [candidatePage([1, 2])],
    recovered: [recoveredPage([[10, 1, "Failed", 5], [70, 7, "Queued"]])],
    queueResult: { items: [{ contentId: 1, outcome: "Queued", jobId: 10 }, { contentId: 2, outcome: "Queued", jobId: 20 }] },
  });
  const { elements, calls, boxes, tick, answer, pendingTimers } = view;
  await flush();
  for (const box of boxes()) {
    box.checked = true;
    box.fire("change");
  }
  elements.lfqQueue.fire("click");
  for (let i = 0; i < 3; i++) await flush();

  assert.equal(pendingTimers().length, 1); // one chain for recovered and submitted jobs
  assert.deepEqual(recoveredTexts(elements).map((r) => r[0]), ["7"]); // job 10 is shown in the batch panel only
  assert.deepEqual(elements.lfqSummaryList.children.map((c) => c.textContent), ["Content 1: Queued", "Content 2: Queued"]);
  assert.equal(elements.lfqSummaryTitle.textContent, "0 of 2 submitted items complete, checking every few seconds."); // recovered jobs are not in the denominator

  tick();
  assert.deepEqual(plain(progressGets(calls)[0].data), { jobIds: [10, 20, 70] }); // each job once
  await answer({ items: [{ jobId: 10, state: "Succeeded" }, { jobId: 20, state: "Succeeded" }, { jobId: 70, state: "Processing" }] });
  assert.equal(elements.lfqProgressBar.style.width, "100%");
  assert.deepEqual(recoveredTexts(elements).map((r) => r[4]), ["Translating"]);
  tick();
  assert.deepEqual(plain(progressGets(calls)[1].data), { jobIds: [70] });
});

test("recovered values with markup stay text and unknown states are dropped", async () => {
  const hostile = recoveredPage([[10, 1, "Queued"]]);
  hostile.items.push({ jobId: 11, contentId: 9, title: "<img src=x onerror=alert(1)>", typeId: 1003, isActive: false, state: "<script>", attemptCount: 0 });
  hostile.items[0].title = "<img src=x onerror=alert(1)>";
  hostile.items[0].failureReason = "stale"; // a reason on a non-failed job is never shown
  hostile.items.push({ jobId: 12, contentId: 8, title: "t", typeId: 1003, isActive: true, state: "Failed", attemptCount: 1,
    failureReason: "<b onclick=x>Bad</b>" });
  const { elements } = page({ candidates: [candidatePage([])], recovered: [hostile] });
  await flush();

  assert.deepEqual(recoveredTexts(elements).map((r) => [r[1], r[5]]),
    [["<img src=x onerror=alert(1)>", "— No failure reason"], ["t", "<b onclick=x>Bad</b>"]]);
  const reasonCell = elements.lfqRecoveredRows.children[1].children[5];
  assert.equal(reasonCell.children.length, 0); // the reason is the cell's own text, not markup
  assert.ok(elements.lfqRecoveredRows.all(() => true).every((n) => Object.keys(n.attributes).every((a) => a === "aria-hidden")));
});

test("a failed recovery read shows an error in its own section and leaves candidates working", async () => {
  const { elements, boxes, pendingTimers } = page({ candidates: [candidatePage([1])], recoveredFails: true });
  await flush();

  assert.equal(boxes().length, 1);
  assert.match(elements.lfqRecoveredRows.text, /Translation jobs could not be loaded/);
  assert.equal(pendingTimers().length, 0);
});

// ---- Rotation when more jobs are pending than one Progress request may carry ----

const queuedJobs = (jobIds) => recoveredPage(jobIds.map((jobId) => [jobId, jobId - 10, "Queued"]));

// Runs one poll: fires the timer, checks the request is bounded, distinct and the only one in flight,
// answers it (null = failure) and returns the job IDs it carried.
async function pollRound(view, max, items = []) {
  view.tick();
  assert.equal(view.calls.progress.length, 1, "exactly one Progress request in flight");
  assert.equal(view.pendingTimers().length, 0, "nothing scheduled while a request is in flight");
  const ids = plain(progressGets(view.calls).at(-1).data.jobIds);
  assert.ok(ids.length > 0 && ids.length <= max, `bounded: ${ids}`);
  assert.equal(new Set(ids).size, ids.length, `distinct: ${ids}`);
  await view.answer(items === null ? null : { items });
  return ids;
}

test("pending recovered jobs beyond the cap are polled in rotation while earlier ones stay pending", async () => {
  const view = page({ maxItems: 3, candidates: [candidatePage([])], recovered: [queuedJobs([11, 12, 13, 14, 15, 16, 17])] });
  await flush();

  const still = (ids) => ids.map((jobId) => ({ jobId, state: "Queued" }));
  const rounds = [];
  for (let i = 0; i < 4; i++) rounds.push(await pollRound(view, 3, still(rounds.at(-1) ?? [])));

  assert.deepEqual(rounds, [[11, 12, 13], [14, 15, 16], [17, 11, 12], [13, 14, 15]]);
  assert.equal(view.pendingTimers().length, 1); // all still pending: the chain goes on
});

test("a transient failure retries the same window once before the rotation moves on", async () => {
  const view = page({ maxItems: 3, candidates: [candidatePage([])], recovered: [queuedJobs([11, 12, 13, 14, 15, 16, 17])] });
  await flush();

  assert.deepEqual(await pollRound(view, 3), [11, 12, 13]);
  assert.deepEqual(await pollRound(view, 3, null), [14, 15, 16]); // fails
  assert.match(view.elements.lfqPollStatus.textContent, /temporarily unavailable/);
  assert.deepEqual(await pollRound(view, 3), [14, 15, 16]); // retried, succeeds
  assert.deepEqual(await pollRound(view, 3, null), [17, 11, 12]); // fails
  assert.deepEqual(await pollRound(view, 3, null), [17, 11, 12]); // retried once, fails again
  assert.deepEqual(await pollRound(view, 3), [13, 14, 15]); // moves on instead of blocking the rest
  assert.equal(view.elements.lfqPollStatus.hidden, true);
  assert.deepEqual(recoveredTexts(view.elements).map((r) => r[4]), Array(7).fill("Queued")); // last known states kept
});

test("a terminal job leaves the rotation without stalling later pending jobs, and polling stops when all are done", async () => {
  const final = { 11: "Succeeded", 12: "Succeeded", 13: "Superseded", 14: "Superseded", 15: "Failed", 16: "Superseded", 17: "Succeeded" };
  const reconciled = recoveredPage(Object.entries(final).map(([jobId, state]) => [Number(jobId), jobId - 10, state]));
  const view = page({ maxItems: 3, candidates: [candidatePage([])], recovered: [queuedJobs([11, 12, 13, 14, 15, 16, 17]), reconciled] });
  await flush();
  const done = (ids, state = "Succeeded") => ids.map((jobId) => ({ jobId, state }));

  assert.deepEqual(await pollRound(view, 3, done([12])), [11, 12, 13]);
  assert.deepEqual(await pollRound(view, 3, done([15], "Failed")), [14, 15, 16]);
  assert.deepEqual(await pollRound(view, 3, done([17, 11])), [17, 11, 13]);
  assert.deepEqual(await pollRound(view, 3, done([13, 14, 16], "Superseded")), [13, 14, 16]); // the rest fit: ascending

  assert.equal(view.pendingTimers().length, 0); // nothing pending: stopped
  await flush();
  assert.equal(view.calls.ajax.at(-1).url, "/BackOffice/LegacyFarsiTranslationQueue/Candidates"); // reconciled
  assert.deepEqual(recoveredTexts(view.elements).map((r) => r[4]),
    ["Translated", "Translated", "Superseded (source changed)", "Superseded (source changed)", "Failed", "Superseded (source changed)", "Translated"]);
  assert.equal(view.elements.lfqSummaryTitle.textContent, ""); // no batch bar for recovered history
});

test("a new batch comes first, is sent once when also recovered, and the recovered rotation continues", async () => {
  const view = page({
    maxItems: 3,
    candidates: [candidatePage([8, 9])],
    recovered: [queuedJobs([11, 12, 13, 14, 15])],
    queueResult: { items: [{ contentId: 8, outcome: "AlreadyQueued", jobId: 11 }, { contentId: 9, outcome: "Queued", jobId: 90 }] },
  });
  await flush();
  assert.deepEqual(await pollRound(view, 3), [11, 12, 13]);

  for (const box of view.boxes()) {
    box.checked = true;
    box.fire("change");
  }
  view.elements.lfqQueue.fire("click");
  for (let i = 0; i < 3; i++) await flush();
  assert.equal(view.pendingTimers().length, 1); // still one chain

  const rounds = [];
  for (let i = 0; i < 3; i++) rounds.push(await pollRound(view, 3));
  // Batch jobs 11 (also recovered) and 90 every time; the recovered slot keeps rotating from 13.
  assert.deepEqual(rounds, [[11, 90, 14], [11, 90, 15], [11, 90, 12]]);
  assert.equal(view.elements.lfqSummaryTitle.textContent, "0 of 2 submitted items complete, checking every few seconds."); // recovered not in the bar
});

test("a batch larger than the cap rotates too, and still leaves recovered jobs a slot", async () => {
  const view = page({
    maxItems: 2,
    candidates: [candidatePage([3, 4, 5])],
    recovered: [queuedJobs([11, 12])],
    queueResult: { items: [3, 4, 5].map((id) => ({ contentId: id, outcome: "Queued", jobId: id * 10 })) },
  });
  await flush();
  // The stubbed Queue answer carries three jobs, more than the cap of two (as several submissions would).
  view.boxes()[0].checked = true;
  view.boxes()[0].fire("change");
  view.elements.lfqQueue.fire("click");
  for (let i = 0; i < 3; i++) await flush();

  const rounds = [];
  for (let i = 0; i < 4; i++) rounds.push(await pollRound(view, 2));
  assert.deepEqual(rounds, [[30, 11], [40, 12], [50, 11], [30, 12]]);
});

test("reloading the recovered page keeps rotating instead of restarting at the first window", async () => {
  const view = page({
    maxItems: 2,
    candidates: [candidatePage([])],
    recovered: [queuedJobs([11, 12, 13, 14, 15]), queuedJobs([11, 12, 13, 14, 15, 16])],
  });
  await flush();
  assert.deepEqual(await pollRound(view, 2), [11, 12]);
  assert.deepEqual(await pollRound(view, 2), [13, 14]);

  view.elements.lfqRecoveredNext.disabled = false;
  view.elements.lfqRecoveredPrev.fire("click"); // reload (page 1 again) with one more job
  await flush();
  assert.equal(recoveredGets(view.calls).length, 2);

  assert.deepEqual(await pollRound(view, 2), [15, 16]);
  assert.deepEqual(await pollRound(view, 2), [11, 12]);
});

test("with a cap of one, batch and recovered jobs share one rotation", async () => {
  const view = page({
    maxItems: 1,
    candidates: [candidatePage([2])],
    recovered: [queuedJobs([11, 12])],
    queueResult: { items: [{ contentId: 2, outcome: "Queued", jobId: 20 }] },
  });
  await flush();
  view.boxes()[0].checked = true;
  view.boxes()[0].fire("change");
  view.elements.lfqQueue.fire("click");
  for (let i = 0; i < 3; i++) await flush();

  const rounds = [];
  for (let i = 0; i < 4; i++) rounds.push(await pollRound(view, 1));
  assert.deepEqual(rounds, [[11], [12], [20], [11]]);
});

test("unloading during rotation cancels the chain and ignores the in-flight answer", async () => {
  const view = page({ maxItems: 2, candidates: [candidatePage([])], recovered: [queuedJobs([11, 12, 13])] });
  await flush();
  await pollRound(view, 2);
  view.tick();
  view.unload();
  await view.answer({ items: [] });

  assert.equal(view.pendingTimers().length, 0);
  assert.equal(progressGets(view.calls).length, 2);
});

test("rotatingWindow wraps, never repeats an ID and survives a cursor that has left the list", () => {
  assert.deepEqual(plain(q.rotatingWindow([1, 2, 3, 4, 5], null, 2)), { ids: [1, 2], next: 2 });
  assert.deepEqual(plain(q.rotatingWindow([1, 2, 3, 4, 5], 4, 3)), { ids: [5, 1, 2], next: 2 });
  assert.deepEqual(plain(q.rotatingWindow([1, 2], 1, 5)), { ids: [2, 1], next: 1 });
  assert.deepEqual(plain(q.rotatingWindow([2, 7, 9], 5, 2)), { ids: [7, 9], next: 9 }); // 5 finished meanwhile
  assert.deepEqual(plain(q.rotatingWindow([], 5, 2)), { ids: [], next: 5 });
});

// ---- Tabs and the recovered-activity panel ----

const tabState = (elements) => ({
  selected: [elements.lfqTabCandidates, elements.lfqTabRecovered].map((t) => t.getAttribute("aria-selected")),
  hidden: [elements.lfqPanelCandidates.hidden, elements.lfqPanelRecovered.hidden],
  tabIndex: [elements.lfqTabCandidates.tabIndex, elements.lfqTabRecovered.tabIndex],
});
const CANDIDATES_SHOWN = { selected: ["true", "false"], hidden: [false, true], tabIndex: [0, -1] };
const RECOVERED_SHOWN = { selected: ["false", "true"], hidden: [true, false], tabIndex: [-1, 0] };
const statusText = (elements) => [elements.lfqRecoveredStatusTitle.textContent, elements.lfqRecoveredStatusCounts.textContent];

test("recovered count helpers: whole numbers only, moves between states, and no percentage", () => {
  assert.deepEqual(plain(q.recoveredCounts({ queued: 10, processing: 2, succeeded: 7, failed: 1, superseded: "<b>", active: 99, total: 99 })),
    { queued: 10, processing: 2, succeeded: 7, failed: 1, superseded: 0, active: 12, total: 20 });
  assert.deepEqual(plain(q.recoveredCounts(null)), { queued: 0, processing: 0, succeeded: 0, failed: 0, superseded: 0, active: 0, total: 0 });

  const counts = q.recoveredCounts({ queued: 2, processing: 1, succeeded: 0, failed: 0, superseded: 0 });
  const before = [{ jobId: 1, state: "Queued" }, { jobId: 2, state: "Queued" }, { jobId: 3, state: "Processing" }];
  const after = [{ jobId: 1, state: "Processing" }, { jobId: 2, state: "Queued" }, { jobId: 3, state: "NotFound" }];
  assert.deepEqual(plain(q.applyStateChanges(counts, before, after)),
    { queued: 1, processing: 1, succeeded: 0, failed: 0, superseded: 0, active: 2, total: 2 }); // a NotFound job leaves the scope
  assert.equal(counts.queued, 2); // not mutated

  const status = q.recoveredStatus(q.recoveredCounts({ queued: 10, processing: 2, succeeded: 7, failed: 1, superseded: 0 }), true);
  assert.deepEqual(plain(status), {
    title: "12 jobs active · updating every few seconds",
    detail: "Queued 10 · Translating 2 · Translated recently 7 · Failed 1 · Superseded 0",
  });
  assert.equal(q.recoveredStatus(q.recoveredCounts({ queued: 1 }), false).title, "1 job active");
  assert.equal(q.recoveredStatus(q.recoveredCounts({ failed: 1 }), false).title, "No jobs active.");
  assert.deepEqual(plain(q.recoveredStatus(q.recoveredCounts(null), false)), { title: "No current or recent translation jobs.", detail: "" });
});

test("with no active recovered job the page opens on To Translate and the tab shows no count", async () => {
  const { elements } = page({ candidates: [candidatePage([1])], recovered: [recoveredPage([[30, 3, "Succeeded"]])] });
  assert.deepEqual(tabState(elements), CANDIDATES_SHOWN);
  assert.deepEqual(statusText(elements), ["Loading recovered translation activity…", ""]);
  await flush();

  assert.deepEqual(tabState(elements), CANDIDATES_SHOWN);
  assert.equal(elements.lfqRecoveredTabCount.textContent, "");
  assert.deepEqual(statusText(elements), ["No jobs active.", "Queued 0 · Translating 0 · Translated recently 1 · Failed 0 · Superseded 0"]);
});

test("after a refresh, active recovered jobs open the Recovered Jobs tab with whole-scope counts and no batch percentage", async () => {
  const { elements } = page({
    candidates: [candidatePage([])],
    recovered: [recoveredPage([[10, 1, "Queued"], [20, 2, "Processing"]],
      { totalCount: 40, counts: { queued: 30, processing: 2, succeeded: 7, failed: 1, superseded: 0 } })],
  });
  await flush();

  assert.deepEqual(tabState(elements), RECOVERED_SHOWN);
  assert.equal(elements.lfqTabRecovered.className, "nav-link active");
  assert.equal(elements.lfqRecoveredTabCount.textContent, " (32 active)");
  assert.deepEqual(statusText(elements),
    ["32 jobs active · updating every few seconds", "Queued 30 · Translating 2 · Translated recently 7 · Failed 1 · Superseded 0"]);
  assert.doesNotMatch(statusText(elements).join(" "), /%| of \d+|submitted/);
  assert.equal(elements.lfqSummaryTitle.textContent, ""); // no batch panel is invented
  assert.equal(elements.lfqProgressBar.style.width, undefined);
});

test("switching tabs issues no request, keeps the selection, batch and poll chain, and is keyboard operable", async () => {
  const view = page({
    maxItems: 5,
    candidates: [candidatePage([1, 2])],
    queueResult: { items: [{ contentId: 1, outcome: "Queued", jobId: 10 }] },
  });
  const { elements, calls, boxes, pendingTimers } = view;
  await flush();
  boxes()[0].checked = true;
  boxes()[0].fire("change");
  elements.lfqQueue.fire("click");
  for (let i = 0; i < 3; i++) await flush();
  boxes()[0].checked = true; // content 2 (content 1 is locked while queued)
  boxes()[0].fire("change");
  const requests = calls.ajax.length;
  const timers = calls.timers.length;

  elements.lfqTabRecovered.fire("click");
  assert.deepEqual(tabState(elements), RECOVERED_SHOWN);
  elements.lfqTabRecovered.fire("keydown", { key: "ArrowLeft" });
  assert.deepEqual(tabState(elements), CANDIDATES_SHOWN);
  assert.equal(Node.focused, elements.lfqTabCandidates);
  elements.lfqTabCandidates.fire("keydown", { key: "End" });
  assert.deepEqual(tabState(elements), RECOVERED_SHOWN);
  assert.equal(Node.focused, elements.lfqTabRecovered);
  elements.lfqTabRecovered.fire("keydown", { key: "Home" });
  elements.lfqTabCandidates.fire("keydown", { key: "a" }); // other keys do nothing
  assert.deepEqual(tabState(elements), CANDIDATES_SHOWN);

  assert.equal(calls.ajax.length, requests); // no Candidates, RecoveredJobs or Progress request
  assert.equal(calls.timers.length, timers); // the poll chain is neither restarted nor duplicated
  assert.equal(pendingTimers().length, 1);
  assert.equal(elements.lfqSelectedCount.textContent, "1 selected");
  assert.equal(boxes()[0].checked, true);
  assert.equal(elements.lfqSummary.hidden, false);
  assert.deepEqual(elements.lfqSummaryList.children.map((c) => c.textContent), ["Content 1: Queued"]);
});

test("a manual tab choice is never overridden by a later recovered answer", async () => {
  const active = recoveredPage([[10, 1, "Queued"]]);
  const view = page({
    candidates: [candidatePage([2])],
    recovered: [recoveredPage([[10, 1, "Queued"]]), active],
    queueResult: { items: [{ contentId: 2, outcome: "Queued", jobId: 20 }] },
  });
  view.elements.lfqTabCandidates.fire("click"); // chosen before the first answer
  await flush();
  assert.deepEqual(tabState(view.elements), CANDIDATES_SHOWN);

  const auto = page({ candidates: [candidatePage([2])], recovered: [recoveredPage([[10, 1, "Queued"]])],
    queueResult: { items: [{ contentId: 2, outcome: "Queued", jobId: 20 }] } });
  await flush();
  assert.deepEqual(tabState(auto.elements), RECOVERED_SHOWN); // the initial decision, once
  auto.elements.lfqTabCandidates.fire("click");
  auto.boxes()[0].checked = true;
  auto.boxes()[0].fire("change");
  auto.elements.lfqQueue.fire("click");
  for (let i = 0; i < 4; i++) await flush();
  assert.equal(recoveredGets(auto.calls).length, 2); // re-read after the submission, still active
  assert.deepEqual(tabState(auto.elements), CANDIDATES_SHOWN);
});

test("a Progress answer updates recovered rows, summary counts and the tab count in place", async () => {
  const { elements, calls, tick, answer } = page({
    candidates: [candidatePage([])],
    recovered: [recoveredPage([[10, 1, "Queued"], [20, 2, "Processing"], [30, 3, "Succeeded"]],
      { counts: { queued: 5, processing: 1, succeeded: 3, failed: 0, superseded: 0 } })],
  });
  await flush();
  assert.equal(elements.lfqRecoveredTabCount.textContent, " (6 active)");

  tick();
  await answer({ items: [{ jobId: 10, state: "Processing" }, { jobId: 20, state: "Failed", errorCode: "provider_error" }] });

  assert.deepEqual(recoveredTexts(elements).map((r) => r[4]), ["Translating", "Failed", "Translated"]);
  assert.deepEqual(statusText(elements),
    ["5 jobs active · updating every few seconds", "Queued 4 · Translating 1 · Translated recently 3 · Failed 1 · Superseded 0"]);
  assert.equal(elements.lfqRecoveredTabCount.textContent, " (5 active)");
  assert.equal(recoveredGets(calls).length, 1); // counted in memory, not re-read while jobs are pending
  assert.doesNotMatch([...statusText(elements), elements.lfqRecoveredTabCount.textContent, elements.lfqRecoveredRows.text].join(" "),
    /provider_error|\b10\b|\b20\b/);
});

test("a job both submitted and recovered is counted, polled and rendered once", async () => {
  const view = page({
    maxItems: 5,
    candidates: [candidatePage([1])],
    recovered: [recoveredPage([]), recoveredPage([[10, 1, "Queued"]]), recoveredPage([[10, 1, "Succeeded"]])],
    queueResult: { items: [{ contentId: 1, outcome: "Queued", jobId: 10 }] },
  });
  const { elements, calls, boxes, tick, answer } = view;
  await flush();
  boxes()[0].checked = true;
  boxes()[0].fire("change");
  elements.lfqQueue.fire("click");
  for (let i = 0; i < 4; i++) await flush();

  assert.equal(elements.lfqRecoveredTabCount.textContent, " (1 active)");
  assert.match(elements.lfqRecoveredRows.text, /shown in the submitted batch on the To Translate tab/); // not a second row
  tick();
  assert.deepEqual(plain(progressGets(calls)[0].data), { jobIds: [10] });
  await answer({ items: [{ jobId: 10, state: "Succeeded" }] });
  assert.deepEqual(statusText(elements), ["No jobs active.", "Queued 0 · Translating 0 · Translated recently 1 · Failed 0 · Superseded 0"]);
  assert.equal(elements.lfqProgressBar.style.width, "100%"); // the batch bar is the session's own
});

test("the recovered panel has clear unavailable and error states", async () => {
  const unavailable = page({ candidates: [candidatePage([], { cultureAvailable: false })],
    recovered: [{ ...recoveredPage([]), cultureAvailable: false }] });
  await flush();
  assert.deepEqual(statusText(unavailable.elements), ["The translation target culture is unavailable, so there is no translation activity to show.", ""]);
  assert.deepEqual(tabState(unavailable.elements), CANDIDATES_SHOWN);

  const failed = page({ candidates: [candidatePage([1])], recoveredFails: true });
  await flush();
  assert.match(statusText(failed.elements)[0], /could not be loaded/);
  assert.equal(failed.elements.lfqRecoveredTabCount.textContent, "");
  assert.deepEqual(tabState(failed.elements), CANDIDATES_SHOWN);
});

// ---- Active recovered jobs beyond the visible page ----

// A fake RecoveredJobs server over job states it owns: active first (then by job ID, as a stand-in for the
// real order), paged by 25, with whole-scope counts. failOnce lists pages whose next read fails.
function recoveredServer(jobIds, { failOnce = [] } = {}) {
  const states = new Map(jobIds.map((id) => [id, "Queued"]));
  const reads = [];
  const fails = [...failOnce];
  const respond = ({ page = 1, pageSize = 25 }) => {
    reads.push(page);
    const failAt = fails.indexOf(page);
    if (failAt >= 0) {
      fails.splice(failAt, 1);
      return null;
    }
    const all = [...states.entries()];
    const pending = ([, state]) => state === "Queued" || state === "Processing";
    const ordered = all.filter(pending).concat(all.filter((j) => !pending(j)));
    const count = (state) => all.filter(([, s]) => s === state).length;
    return recoveredPage(ordered.slice((page - 1) * pageSize, page * pageSize).map(([id, state]) => [id, id - 100, state]), {
      totalCount: all.length, page,
      counts: { queued: count("Queued"), processing: count("Processing"), succeeded: count("Succeeded"), failed: count("Failed"), superseded: count("Superseded") },
    });
  };
  const progress = (ids) => ids.map((jobId) => ({ jobId, state: states.get(jobId) ?? "NotFound" }));
  return { states, reads, respond, progress };
}

const sixtyJobs = () => [...Array(60)].map((_, i) => 101 + i); // pages 1-3: 101-125, 126-150, 151-160

// Runs one round against the fake server: a bounded, distinct, single Progress request answered with the
// server's states, then the follow-up discovery read. Returns the job IDs it carried.
async function serverRound(view, server, max) {
  view.tick();
  assert.equal(view.calls.progress.length, 1, "exactly one Progress request in flight");
  assert.equal(view.pendingTimers().length, 0, "nothing scheduled while a request is in flight");
  const ids = plain(progressGets(view.calls).at(-1).data.jobIds);
  assert.ok(ids.length > 0 && ids.length <= max, `bounded: ${ids}`);
  assert.equal(new Set(ids).size, ids.length, `distinct: ${ids}`);
  await view.answer({ items: server.progress(ids) });
  await flush();
  return ids;
}

test("active jobs on later recovered pages are discovered in rotation and polled while page one stays pending", async () => {
  const server = recoveredServer(sixtyJobs());
  const view = page({ maxItems: 10, candidates: [candidatePage([])], recovered: server.respond });
  await flush();
  assert.deepEqual(server.reads, [1]);

  const polled = new Set();
  for (let i = 0; i < 9; i++) for (const id of await serverRound(view, server, 10)) polled.add(id);

  assert.deepEqual(server.reads, [1, 2, 3, 1, 2, 3, 1, 2, 3, 1]); // one discovery read per round, every active page in turn
  assert.deepEqual([...polled].sort((a, b) => a - b), sixtyJobs()); // every active job, not only page one's
  assert.ok([...polled].some((id) => id > 125 && id <= 150) && [...polled].some((id) => id > 150));
  // The visible table never moved: still page one, its rows, and the user's pager.
  assert.equal(view.elements.lfqRecoveredInfo.textContent, "60 jobs · page 1 of 3");
  assert.deepEqual(recoveredTexts(view.elements).map((r) => r[0]), [...Array(25)].map((_, i) => String(i + 1)));
  assert.equal(view.elements.lfqRecoveredPrev.disabled, true);
  assert.equal(view.elements.lfqRecoveredTabCount.textContent, " (60 active)");
  assert.equal(view.pendingTimers().length, 1);
});

test("jobs finishing beyond the visible page update the counts and tab badge, not the visible rows", async () => {
  const server = recoveredServer(sixtyJobs());
  const view = page({ maxItems: 10, candidates: [candidatePage([])], recovered: server.respond });
  await flush();
  server.states.set(140, "Failed");
  server.states.set(160, "Succeeded");
  server.states.set(155, "Superseded");

  for (let i = 0; i < 3; i++) await serverRound(view, server, 10);

  assert.equal(view.elements.lfqRecoveredTabCount.textContent, " (57 active)");
  assert.deepEqual(statusText(view.elements),
    ["57 jobs active · updating every few seconds", "Queued 57 · Translating 0 · Translated recently 1 · Failed 1 · Superseded 1"]);
  assert.deepEqual(recoveredTexts(view.elements).map((r) => r[4]), Array(25).fill("Queued")); // page one is unchanged
  const seen = progressGets(view.calls).length;
  for (let i = 0; i < 6; i++) await serverRound(view, server, 10);
  const later = progressGets(view.calls).slice(seen).flatMap((r) => r.data.jobIds);
  assert.equal(new Set(later).size, 57); // every still-active job keeps being polled...
  assert.ok(later.every((id) => ![140, 155, 160].includes(id))); // ...and finished ones no longer are
  assert.equal(view.elements.lfqRecoveredTabCount.textContent, " (57 active)"); // never counted twice
  assert.doesNotMatch([...statusText(view.elements), view.elements.lfqRecoveredRows.text].join(" "), /\b1[0-6]\d\b|provider_error/);
});

test("a failed discovery read keeps the last known state, is retried once, then the rotation moves on", async () => {
  const server = recoveredServer(sixtyJobs(), { failOnce: [2, 3, 3] });
  const view = page({ maxItems: 10, candidates: [candidatePage([])], recovered: server.respond });
  await flush();

  for (let i = 0; i < 6; i++) await serverRound(view, server, 10);

  // 2 fails then succeeds on its retry; 3 fails twice, so the rotation moves on to 1 instead of stalling.
  assert.deepEqual(server.reads, [1, 2, 2, 3, 3, 1, 2]);
  assert.equal(view.elements.lfqRecoveredInfo.textContent, "60 jobs · page 1 of 3");
  assert.equal(view.elements.lfqRecoveredTabCount.textContent, " (60 active)");
  assert.doesNotMatch(view.elements.lfqRecoveredRows.text + statusText(view.elements).join(" "), /raw failure|could not be loaded/);
  for (let i = 0; i < 8; i++) await serverRound(view, server, 10); // the next read of page 3, then the pool's rotation reaches it
  assert.ok(progressGets(view.calls).some((r) => r.data.jobIds.some((id) => id > 150))); // page three is still covered
});

test("pager navigation, a submission and a changed active count neither restart discovery at page one nor duplicate jobs", async () => {
  const server = recoveredServer(sixtyJobs());
  const view = page({
    maxItems: 10,
    candidates: [candidatePage([900])],
    recovered: server.respond,
    queueResult: { items: [{ contentId: 900, outcome: "Queued", jobId: 101 }] }, // also a recovered job
  });
  await flush();
  await serverRound(view, server, 10); // discovers page 2

  view.elements.lfqRecoveredNext.fire("click"); // the user views page 2
  await flush();
  assert.equal(view.elements.lfqRecoveredInfo.textContent, "60 jobs · page 2 of 3");
  view.boxes()[0].checked = true;
  view.boxes()[0].fire("change");
  view.elements.lfqQueue.fire("click");
  for (let i = 0; i < 4; i++) await flush();
  for (const id of [161, 162]) server.states.set(id, "Queued"); // two more active jobs: page count unchanged
  for (let i = 0; i < 3; i++) await serverRound(view, server, 10);

  // Reads: initial 1, discovery 2, the user's 2, the submission's reload of 2, then discovery 3, 1, 2.
  assert.deepEqual(server.reads, [1, 2, 2, 2, 3, 1, 2]);
  assert.equal(view.elements.lfqRecoveredInfo.textContent, "60 jobs · page 2 of 3"); // the user's page and its load stay as they were
  assert.equal(view.elements.lfqRecoveredTabCount.textContent, " (62 active)"); // discovery brought the new count
  assert.equal(view.pendingTimers().length, 1); // one chain
  for (const request of progressGets(view.calls)) assert.equal(new Set(request.data.jobIds).size, request.data.jobIds.length);
  assert.ok(progressGets(view.calls).slice(1).every((r) => r.data.jobIds[0] === 101)); // the batch job first, and only once
  assert.equal(view.elements.lfqSummaryTitle.textContent, "0 of 1 submitted item complete, checking every few seconds."); // session only
});

test("polling stops only once every active job on every page is terminal, then both lists reconcile", async () => {
  const server = recoveredServer(sixtyJobs());
  const view = page({ maxItems: 10, candidates: [candidatePage([]), candidatePage([])], recovered: server.respond });
  await flush();
  for (const id of sixtyJobs().slice(0, 25)) server.states.set(id, "Succeeded"); // the visible page finishes first

  for (let i = 0; i < 3; i++) await serverRound(view, server, 10);
  assert.deepEqual(recoveredTexts(view.elements).map((r) => r[4]).slice(0, 3), ["Translated", "Translated", "Translated"]);
  assert.equal(view.pendingTimers().length, 1); // the visible page is done, but 35 jobs elsewhere are not

  for (const id of sixtyJobs().slice(25)) server.states.set(id, "Failed");
  let rounds = 0;
  while (view.pendingTimers().length > 0 && rounds++ < 20) await serverRound(view, server, 10);
  await flush();

  assert.equal(view.pendingTimers().length, 0);
  assert.equal(view.calls.progress.length, 0);
  assert.equal(view.calls.ajax.at(-1).url, "/BackOffice/LegacyFarsiTranslationQueue/Candidates"); // reconciled
  assert.equal(server.reads.at(-1), 1); // the viewed page, re-read
  assert.equal(view.elements.lfqRecoveredTabCount.textContent, "");
  assert.deepEqual(statusText(view.elements), ["No jobs active.", "Queued 0 · Translating 0 · Translated recently 25 · Failed 35 · Superseded 0"]);
  assert.equal(view.elements.lfqSummaryTitle.textContent, ""); // no batch invented
});

test("unloading mid-round cancels discovery as well as polling", async () => {
  const server = recoveredServer(sixtyJobs());
  const view = page({ maxItems: 10, candidates: [candidatePage([])], recovered: server.respond });
  await flush();
  view.tick();
  view.unload();
  await view.answer({ items: [] });
  await flush();

  assert.deepEqual(server.reads, [1]); // no discovery read after unload
  assert.equal(view.pendingTimers().length, 0);
});

test("pool and discovery helpers", () => {
  const pool = q.mergePool([{ jobId: 1, state: "Queued" }, { jobId: 2, state: "Queued" }], [
    { jobId: 1, state: "Succeeded" }, { jobId: 2, state: "Processing" }, { jobId: 3, state: "Queued" }, { jobId: 3, state: "Queued" },
    { jobId: 4, state: "Failed" }, { jobId: "5", state: "Queued" }, { jobId: 6, state: "<script>" },
  ]);
  assert.deepEqual(plain(pool), [{ jobId: 2, state: "Processing" }, { jobId: 3, state: "Queued" }]);
  assert.deepEqual(plain(q.mergePool(pool, [])), plain(pool)); // jobs on other pages are kept

  assert.equal(q.discoveryPage(0, 25, 1, 0, null), null);
  assert.equal(q.discoveryPage(20, 25, 1, 5, 1), null); // all on the viewed first page, already polled
  assert.equal(q.discoveryPage(20, 25, 1, 0, 1), 1); // counted active but none known: look again
  assert.equal(q.discoveryPage(20, 25, 2, 0, null), 1); // viewing another page
  assert.deepEqual([null, 1, 2, 3, 7].map((cursor) => q.discoveryPage(60, 25, 1, 5, cursor)), [1, 2, 3, 1, 1]); // wraps, survives shrinkage
});
