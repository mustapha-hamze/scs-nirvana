// Legacy Farsi Translation Queue dashboard (Areas/BackOffice/Views/LegacyFarsiTranslationQueue/Index.cshtml).
// Reads GET Candidates, posts only selected content IDs to POST Queue, then polls GET Progress with the
// returned job IDs until every submitted item is terminal. On load it also reads GET RecoveredJobs - the
// application's active and recently completed jobs, durable across a refresh - shows them in their own
// section (never in the submitted batch's bar, whose denominator is unknown after a refresh) and polls the
// active ones through the same bounded Progress chain, rotating when there are more than one request may
// carry; a job is polled once however it is known. The two sections are tabs ("To Translate" and "Recovered
// Jobs") that only show or hide their panel. The Recovered Jobs tab heads its rows with live counts over the
// whole recovered scope (RecoveredJobs counts, adjusted in place by each Progress answer and re-read after a
// submission and once polling drains) - counts, never a percentage, since no batch survives a refresh.
// RecoveredJobs is paged with active jobs first, so they fill its first ceil(active / page size) pages. Besides
// the page the user is viewing, each poll round may read one of those pages in rotation into a pool of active
// jobs, so jobs beyond the visible page are polled too; that read never touches the visible table or pager.
// A Failed job shows the server's fixed failureReason text (Recovered Jobs column, submitted-items list); no
// other state keeps or shows one. Requests go through $.ajax so the layout's global $.ajaxSetup adds the X-CSRF-TOKEN antiforgery header. Every dynamic value is written with
// textContent. The server re-checks every submitted ID; the page only reflects the candidate list it
// was given, the outcomes Queue returned and the job states Progress reports.
var LegacyFarsiQueue = (function () {
  var CANDIDATES_URL = "/BackOffice/LegacyFarsiTranslationQueue/Candidates";
  var QUEUE_URL = "/BackOffice/LegacyFarsiTranslationQueue/Queue";
  var PROGRESS_URL = "/BackOffice/LegacyFarsiTranslationQueue/Progress";
  var RECOVERED_URL = "/BackOffice/LegacyFarsiTranslationQueue/RecoveredJobs";
  var POLL_INTERVAL_MS = 2500;
  var SORTS = ["Id", "Title", "TypeId", "UpdatedAt"];
  var PAGE_SIZE = 25;

  var OUTCOME_LABELS = {
    Queued: "Queued",
    AlreadyQueued: "Already queued",
    AlreadyReady: "Already translated",
    Skipped: "Skipped",
    CultureUnavailable: "Culture unavailable",
    NotFound: "Not found",
  };
  var OUTCOME_STATUS = {
    Queued: "scs-status--success",
    AlreadyQueued: "scs-status--info",
    AlreadyReady: "scs-status--success",
    Skipped: "scs-status--warning",
    CultureUnavailable: "scs-status--danger",
    NotFound: "scs-status--danger",
  };
  // Job states Progress reports, labelled for the batch panel and candidate rows.
  var STATE_LABELS = {
    Queued: "Queued",
    Processing: "Translating",
    Succeeded: "Translated",
    Failed: "Failed",
    Superseded: "Superseded (source changed)",
    NotFound: "Not found",
  };
  var STATE_STATUS = {
    Queued: "scs-status--info",
    Processing: "scs-status--info",
    Succeeded: "scs-status--success",
    Failed: "scs-status--danger",
    Superseded: "scs-status--warning",
    NotFound: "scs-status--danger",
  };
  var PENDING = { Queued: true, Processing: true };
  // An active job or a translation exists for these: not offered for selection again this session.
  // Failed and Superseded jobs may be queued again.
  var LOCKED = { Queued: true, Processing: true, Succeeded: true, AlreadyReady: true };

  // Job states the recovered counts are kept by.
  var COUNT_KEYS = { Queued: "queued", Processing: "processing", Succeeded: "succeeded", Failed: "failed", Superseded: "superseded" };

  // ---- Pure helpers (covered by Web.Tests/js/legacy-farsi-queue.test.mjs) ----

  // A positive whole number, or null.
  function parsePositiveInt(value) {
    var text = String(value == null ? "" : value).trim();
    if (!/^\d+$/.test(text)) return null;
    var number = Number(text);
    return number > 0 && Number.isSafeInteger(number) ? number : null;
  }

  // The typed Phase 1 query: only known parameters, only configured type IDs, defined sorts.
  function buildQuery(filters, page, typeIds) {
    var query = {
      sort: SORTS.indexOf(filters.sort) >= 0 ? filters.sort : "Id",
      descending: filters.descending === true || filters.descending === "true",
      page: Math.max(1, parsePositiveInt(page) || 1),
      pageSize: PAGE_SIZE,
    };
    var typeId = parsePositiveInt(filters.typeId);
    if (typeId !== null && typeIds.indexOf(typeId) >= 0) query.typeId = typeId;
    var title = String(filters.title || "").trim();
    if (title) query.title = title;
    var contentId = parsePositiveInt(filters.contentId);
    if (contentId !== null) query.contentId = contentId;
    return query;
  }

  function pageCount(totalCount, pageSize) {
    return Math.max(1, Math.ceil(totalCount / pageSize));
  }

  // New selection after (un)checking id; never grows past max.
  function toggle(selected, id, checked, max) {
    var without = selected.filter(function (x) { return x !== id; });
    if (!checked) return without;
    if (selected.indexOf(id) >= 0) return selected.slice();
    return selected.length >= max ? selected.slice() : selected.concat([id]);
  }

  // Why queueing is unavailable (null when available). cultureAvailable is null until Candidates answers.
  function unavailableReason(workerEnabled, cultureAvailable) {
    if (!workerEnabled) return "Background translation is unavailable in this environment.";
    if (cultureAvailable === false) return "The translation target culture is unavailable.";
    if (cultureAvailable !== true) return "Checking queue availability.";
    return null;
  }

  function canQueue(workerEnabled, cultureAvailable, selectedCount, busy) {
    return !busy && selectedCount > 0 && unavailableReason(workerEnabled, cultureAvailable) === null;
  }

  function confirmText(count) {
    return "Queue background translation for " + count + " content item" + (count === 1 ? "" : "s") +
      "? Each selected item will create or reuse a background translation job. The server re-checks every item before queueing.";
  }

  function queueRequest(contentIds) {
    return {
      url: QUEUE_URL,
      type: "POST",
      contentType: "application/json",
      dataType: "json",
      data: JSON.stringify({ contentIds: contentIds.slice() }),
    };
  }

  function candidatesRequest(query) {
    return { url: CANDIDATES_URL, type: "GET", dataType: "json", data: query };
  }

  function outcomeLabel(outcome) {
    return OUTCOME_LABELS[outcome] || "Skipped";
  }

  // Batch items from a Queue response. Queued/AlreadyQueued with a job start as Queued and are polled;
  // every other outcome has no job and is terminal as submitted (state = outcome).
  function batchFromQueue(items) {
    return items.map(function (item) {
      var polled = (item.outcome === "Queued" || item.outcome === "AlreadyQueued") && item.jobId != null;
      return { contentId: item.contentId, outcome: item.outcome, jobId: polled ? item.jobId : null, state: polled ? "Queued" : item.outcome };
    });
  }

  function isPending(item) {
    return item.jobId != null && PENDING[item.state] === true;
  }

  function pendingJobIds(batch) {
    return batch.filter(isPending).map(function (item) { return item.jobId; });
  }

  // Distinct pending job IDs, ascending: the batch's, and the recovered ones not already in the batch.
  function pendingSets(batch, recovered) {
    var ascending = function (a, b) { return a - b; };
    var batchIds = [];
    batch.forEach(function (item) { if (isPending(item) && batchIds.indexOf(item.jobId) < 0) batchIds.push(item.jobId); });
    var recoveredIds = [];
    recovered.forEach(function (item) {
      if (isPending(item) && batchIds.indexOf(item.jobId) < 0 && recoveredIds.indexOf(item.jobId) < 0) recoveredIds.push(item.jobId);
    });
    return { batch: batchIds.sort(ascending), recovered: recoveredIds.sort(ascending) };
  }

  // Up to limit of the ascending ids, starting at the first ID above cursor (a job ID, or null for the
  // start) and wrapping around. The cursor is a job ID rather than an index, so it stays meaningful when
  // jobs finish, are added or the list is reloaded. Returns the window and the cursor for the next one.
  function rotatingWindow(ids, cursor, limit) {
    var count = Math.min(Math.max(limit, 0), ids.length);
    var start = 0;
    while (cursor != null && start < ids.length && ids[start] <= cursor) start++;
    if (start === ids.length) start = 0;
    var picked = [];
    for (var i = 0; i < count; i++) picked.push(ids[(start + i) % ids.length]);
    return { ids: picked, next: picked.length > 0 ? picked[picked.length - 1] : cursor };
  }

  // One Progress request's job IDs (distinct, at most limit) and the cursors to use once it succeeds.
  // The batch submitted on this page comes first, but while recovered jobs are pending one slot is kept
  // for them; each group rotates through its own room when it does not fit, so no pending job starves.
  // With a limit of 1 there is no room to share, so one combined rotation covers both groups.
  function pollWindow(sets, cursors, limit) {
    if (limit <= 1) {
      var all = sets.batch.concat(sets.recovered).sort(function (a, b) { return a - b; });
      var single = rotatingWindow(all, cursors.batch, limit);
      return { ids: single.ids, cursors: { batch: single.next, recovered: cursors.recovered } };
    }
    var take = function (ids, cursor, room) {
      return room >= ids.length ? { ids: ids.slice(), next: cursor } : rotatingWindow(ids, cursor, room);
    };
    var batch = take(sets.batch, cursors.batch, sets.recovered.length > 0 ? limit - 1 : limit);
    var recovered = take(sets.recovered, cursors.recovered, limit - batch.ids.length);
    return { ids: batch.ids.concat(recovered.ids), cursors: { batch: batch.next, recovered: recovered.next } };
  }

  // Applies Progress items (state, the failure reason of a Failed job, and attempts for items that show
  // them) by job ID to batch or recovered items. Unknown states are ignored so a job keeps its last known state.
  function mergeProgress(items, progressItems) {
    var byJob = {};
    (progressItems || []).forEach(function (p) { if (STATE_LABELS[p.state]) byJob[p.jobId] = p; });
    return items.map(function (item) {
      var p = item.jobId != null ? byJob[item.jobId] : undefined;
      if (!p) return item;
      var next = Object.assign({}, item, { state: p.state });
      delete next.failureReason;
      var reason = failureReason(p);
      if (reason) next.failureReason = reason;
      if ("attemptCount" in item && typeof p.attemptCount === "number") next.attemptCount = p.attemptCount;
      return next;
    });
  }

  // Aggregate counts over submitted content items. "skipped" is every terminal item without a finished
  // job: Skipped, AlreadyReady, CultureUnavailable and NotFound (at submission or later). percent is
  // terminal content items / submitted content items, never an estimate of the provider call.
  function progressCounts(batch) {
    var counts = { total: batch.length, queued: 0, processing: 0, succeeded: 0, failed: 0, superseded: 0, skipped: 0 };
    batch.forEach(function (item) {
      if (item.jobId != null && item.state === "Queued") counts.queued++;
      else if (item.jobId != null && item.state === "Processing") counts.processing++;
      else if (item.jobId != null && item.state === "Succeeded") counts.succeeded++;
      else if (item.jobId != null && item.state === "Failed") counts.failed++;
      else if (item.jobId != null && item.state === "Superseded") counts.superseded++;
      else counts.skipped++;
    });
    counts.terminal = counts.succeeded + counts.failed + counts.superseded + counts.skipped;
    counts.percent = counts.total === 0 ? 100 : Math.floor((counts.terminal / counts.total) * 100);
    return counts;
  }

  // A Failed job's server-provided reason (plain text), else null.
  function failureReason(item) {
    return item.state === "Failed" && typeof item.failureReason === "string" && item.failureReason.trim() ? item.failureReason : null;
  }

  function batchLabel(item) {
    return item.jobId != null ? STATE_LABELS[item.state] : outcomeLabel(item.outcome);
  }

  // A submitted-items line: its label, plus the reason when its job failed.
  function batchLine(item) {
    var reason = item.jobId != null ? failureReason(item) : null;
    return "Content " + item.contentId + ": " + batchLabel(item) + (reason ? " — " + reason : "");
  }

  function batchStatus(item) {
    return item.jobId != null ? STATE_STATUS[item.state] : OUTCOME_STATUS[item.outcome];
  }

  // traditional: jobIds=1&jobIds=2, the form the endpoint binds (not jobIds[]=1).
  function progressRequest(jobIds) {
    return { url: PROGRESS_URL, type: "GET", dataType: "json", traditional: true, data: { jobIds: jobIds.slice() } };
  }

  function recoveredRequest(page) {
    return { url: RECOVERED_URL, type: "GET", dataType: "json", data: { page: Math.max(1, parsePositiveInt(page) || 1), pageSize: PAGE_SIZE } };
  }

  // Recovered jobs to show: those not already in this session's batch panel.
  function recoveredToShow(recovered, batch) {
    var inBatch = batch.map(function (item) { return item.jobId; }).filter(function (id) { return id != null; });
    return recovered.filter(function (item) { return inBatch.indexOf(item.jobId) < 0; });
  }

  // The active-job poll pool after a RecoveredJobs page: the page's active jobs join once (by job ID), pooled
  // jobs it reports finished leave, and pooled jobs not on the page are kept.
  function mergePool(pool, items) {
    var onPage = {};
    (items || []).forEach(function (item) {
      if (STATE_LABELS[item.state] && parsePositiveInt(item.jobId) === item.jobId) onPage[item.jobId] = item.state;
    });
    var next = pool.filter(function (job) { return !(job.jobId in onPage) || PENDING[onPage[job.jobId]] === true; })
      .map(function (job) { return job.jobId in onPage ? { jobId: job.jobId, state: onPage[job.jobId] } : job; });
    Object.keys(onPage).forEach(function (key) {
      var jobId = Number(key);
      if (PENDING[onPage[key]] && !next.some(function (job) { return job.jobId === jobId; })) next.push({ jobId: jobId, state: onPage[key] });
    });
    return next;
  }

  // The recovered page to read for discovery next, or null when none is needed: the page after cursor (the last
  // page read, or null), wrapping within the pages that can hold active jobs. When active jobs fit on the viewed
  // first page and some are already being polled, that page's load has found them all.
  function discoveryPage(activeCount, pageSize, viewedPage, pendingCount, cursor) {
    if (activeCount <= 0) return null;
    var pages = Math.ceil(activeCount / pageSize);
    if (pages === 1 && viewedPage === 1 && pendingCount > 0) return null;
    return cursor == null || cursor >= pages ? 1 : cursor + 1;
  }

  function withTotals(counts) {
    counts.active = counts.queued + counts.processing;
    counts.total = counts.active + counts.succeeded + counts.failed + counts.superseded;
    return counts;
  }

  // The RecoveredJobs per-state counts (every page of the recovered scope); anything but a whole number is 0.
  function recoveredCounts(raw) {
    var counts = {};
    Object.keys(COUNT_KEYS).forEach(function (state) {
      var value = raw ? raw[COUNT_KEYS[state]] : 0;
      counts[COUNT_KEYS[state]] = Number.isSafeInteger(value) && value > 0 ? value : 0;
    });
    return withTotals(counts);
  }

  // counts after Progress moved recovered jobs from their before states to their after states (the same
  // items, as mergeProgress returns them). A job that is NotFound now has left the scope.
  function applyStateChanges(counts, before, after) {
    var next = Object.assign({}, counts);
    before.forEach(function (item, i) {
      if (item.state === after[i].state) return;
      var from = COUNT_KEYS[item.state];
      var to = COUNT_KEYS[after[i].state];
      if (from && next[from] > 0) next[from]--;
      if (to) next[to]++;
    });
    return withTotals(next);
  }

  // The recovered-activity headline and counts line. live: an active job is being polled right now.
  function recoveredStatus(counts, live) {
    var title = counts.active > 0
      ? counts.active + " job" + (counts.active === 1 ? "" : "s") + " active" + (live ? " · updating every few seconds" : "")
      : counts.total > 0 ? "No jobs active." : "No current or recent translation jobs.";
    var detail = counts.total > 0
      ? "Queued " + counts.queued + " · Translating " + counts.processing + " · Translated recently " + counts.succeeded +
        " · Failed " + counts.failed + " · Superseded " + counts.superseded
      : "";
    return { title: title, detail: detail };
  }

  function shortTime(value) {
    return String(value || "").slice(0, 16).replace("T", " ");
  }

  // ---- Page ----

  function init(root) {
    var byId = function (id) { return document.getElementById(id); };
    var config = {
      workerEnabled: root.getAttribute("data-worker-enabled") === "true",
      maxItems: parsePositiveInt(root.getAttribute("data-max-items")) || 1,
      typeIds: (root.getAttribute("data-type-ids") || "").split(",").map(parsePositiveInt).filter(function (x) { return x !== null; }),
    };
    var state = { page: 1, filters: readFilters(), selected: [], batch: [], cultureAvailable: null, busy: false, last: null,
      recovered: [], recoveredLast: null, recoveredPage: 1, recoveredCounts: recoveredCounts(null), recoveredSeq: 0, tabChosen: false,
      pool: [] };
    // cursors: where the next window starts (see pollWindow); retried: the current window failed once.
    var poll = { timer: null, inFlight: false, stopped: false, cursors: { batch: null, recovered: null }, retried: false };
    // cursor: the last recovered page discovery read (it moves on after a success, or after a retried failure).
    var discovery = { cursor: null, retried: false };

    var TABS = { candidates: ["lfqTabCandidates", "lfqPanelCandidates"], recovered: ["lfqTabRecovered", "lfqPanelRecovered"] };

    // Shows one panel and hides the other; nothing is loaded, reset or re-polled.
    function showTab(name, focus) {
      Object.keys(TABS).forEach(function (key) {
        var tab = byId(TABS[key][0]);
        var active = key === name;
        tab.className = "nav-link" + (active ? " active" : "");
        tab.setAttribute("aria-selected", active ? "true" : "false");
        tab.tabIndex = active ? 0 : -1;
        byId(TABS[key][1]).hidden = !active;
        if (active && focus) tab.focus();
      });
    }

    function readFilters() {
      return {
        typeId: byId("lfqTypeId").value,
        title: byId("lfqTitle").value,
        contentId: byId("lfqContentId").value,
        sort: byId("lfqSort").value,
        descending: byId("lfqDescending").value,
      };
    }

    function el(tag, className, text) {
      var node = document.createElement(tag);
      if (className) node.className = className;
      if (text != null) node.textContent = String(text);
      return node;
    }

    function messageRow(text, tbodyId, colSpan) {
      var tbody = byId(tbodyId || "lfqRows");
      tbody.replaceChildren();
      var tr = el("tr");
      var td = el("td", "scs-empty-state", text);
      td.colSpan = colSpan || 7;
      tr.appendChild(td);
      tbody.appendChild(tr);
    }

    function batchItem(contentId) {
      for (var i = 0; i < state.batch.length; i++) if (state.batch[i].contentId === contentId) return state.batch[i];
      return null;
    }

    function isSelectable(item) {
      var known = batchItem(item.contentId);
      return unavailableReason(config.workerEnabled, state.cultureAvailable) === null && !(known && LOCKED[known.state]);
    }

    function renderRows(items) {
      var tbody = byId("lfqRows");
      tbody.replaceChildren();
      var full = state.selected.length >= config.maxItems;
      items.forEach(function (item) {
        var id = item.contentId;
        var checked = state.selected.indexOf(id) >= 0;
        var tr = el("tr");
        if (checked) tr.setAttribute("aria-selected", "true");

        var selectCell = el("td");
        if (isSelectable(item)) {
          var box = el("input", "form-check-input");
          box.type = "checkbox";
          box.checked = checked;
          box.disabled = state.busy || (full && !checked);
          box.setAttribute("aria-label", "Select content " + id);
          box.addEventListener("change", function () {
            state.selected = toggle(state.selected, id, box.checked, config.maxItems);
            renderRows(state.last.items);
            renderToolbar();
          });
          selectCell.appendChild(box);
        }
        tr.appendChild(selectCell);

        tr.appendChild(el("td", "scs-meta", id));
        var title = el("td", "scs-content-title", item.title);
        title.title = item.title || "";
        tr.appendChild(title);
        tr.appendChild(el("td", null, item.typeId));
        var active = el("td");
        active.appendChild(el("span", item.isActive ? "scs-status scs-status--success" : "scs-status", item.isActive ? "Active" : "Inactive"));
        tr.appendChild(active);
        tr.appendChild(el("td", null, shortTime(item.updatedAt)));
        var outcome = el("td");
        var known = batchItem(id);
        outcome.appendChild(known
          ? el("span", "scs-status " + (batchStatus(known) || ""), batchLabel(known))
          : el("span", "text-muted", "Eligible"));
        tr.appendChild(outcome);
        tbody.appendChild(tr);
      });
      if (items.length === 0)
        messageRow("No legacy Farsi content needs queueing for these filters. Content with a current translation job is not listed here; see the Recovered Jobs tab.");
    }

    function renderToolbar() {
      var count = state.selected.length;
      byId("lfqSelectedCount").textContent = count + " selected" + (count >= config.maxItems ? " (limit reached)" : "");
      byId("lfqClear").disabled = state.busy || count === 0;
      byId("lfqQueue").disabled = !canQueue(config.workerEnabled, state.cultureAvailable, count, state.busy);
      byId("lfqCultureUnavailable").hidden = state.cultureAvailable !== false;
    }

    function renderPager() {
      var last = state.last;
      var pages = last ? pageCount(last.totalCount, last.pageSize) : 1;
      var select = byId("lfqPage");
      select.replaceChildren();
      for (var p = 1; p <= pages; p++) {
        var option = el("option", null, "Page " + p + " of " + pages);
        option.value = String(p);
        option.selected = p === state.page;
        select.appendChild(option);
      }
      select.disabled = !last || pages <= 1 || state.busy;
      byId("lfqPrev").disabled = !last || state.page <= 1 || state.busy;
      byId("lfqNext").disabled = !last || state.page >= pages || state.busy;
      byId("lfqPageInfo").textContent = last ? last.totalCount + " candidate" + (last.totalCount === 1 ? "" : "s") : "";
    }

    function load(page) {
      state.page = page;
      messageRow("Loading candidates…");
      $.ajax(candidatesRequest(buildQuery(state.filters, page, config.typeIds)))
        .done(function (data) {
          state.cultureAvailable = data.cultureAvailable === true;
          state.last = data;
          var pages = pageCount(data.totalCount, data.pageSize);
          if (data.items.length === 0 && data.totalCount > 0 && page > pages) {
            load(pages); // the list shrank (e.g. after queueing): show the new last page
            return;
          }
          state.page = data.page;
          renderRows(data.items);
          renderPager();
          renderToolbar();
        })
        .fail(function () {
          state.last = null;
          messageRow("Candidates could not be loaded. Reload the page and try again.");
          renderPager();
          renderToolbar();
        });
    }

    function renderRecovered() {
      var last = state.recoveredLast;
      var tbody = byId("lfqRecoveredRows");
      var shown = recoveredToShow(state.recovered, state.batch);
      tbody.replaceChildren();
      shown.forEach(function (item) {
        var tr = el("tr");
        tr.appendChild(el("td", "scs-meta", item.contentId));
        var title = el("td", "scs-content-title", item.title);
        title.title = item.title || "";
        tr.appendChild(title);
        tr.appendChild(el("td", null, item.typeId));
        var active = el("td");
        active.appendChild(el("span", item.isActive ? "scs-status scs-status--success" : "scs-status", item.isActive ? "Active" : "Inactive"));
        tr.appendChild(active);
        var job = el("td");
        job.appendChild(el("span", "scs-status " + (STATE_STATUS[item.state] || ""), STATE_LABELS[item.state] || "Not found"));
        tr.appendChild(job);
        var reason = failureReason(item);
        var why = el("td", "text-wrap", reason);
        if (!reason) {
          why.appendChild(el("span", null, "—")).setAttribute("aria-hidden", "true");
          why.appendChild(el("span", "visually-hidden", "No failure reason"));
        }
        tr.appendChild(why);
        tr.appendChild(el("td", null, item.attemptCount));
        tr.appendChild(el("td", null, shortTime(item.relevantAt)));
        tbody.appendChild(tr);
      });
      if (!last) messageRow("Loading translation jobs…", "lfqRecoveredRows", 8);
      else if (last.failed) messageRow("Translation jobs could not be loaded. Reload the page and try again.", "lfqRecoveredRows", 8);
      else if (shown.length === 0)
        messageRow(state.recovered.length > 0 ? "These jobs are shown in the submitted batch on the To Translate tab." : "No current or recent translation jobs.",
          "lfqRecoveredRows", 8);
      var pages = last && !last.failed ? pageCount(last.totalCount, last.pageSize) : 1;
      byId("lfqRecoveredPrev").disabled = !last || state.recoveredPage <= 1;
      byId("lfqRecoveredNext").disabled = !last || state.recoveredPage >= pages;
      byId("lfqRecoveredInfo").textContent = last && !last.failed
        ? last.totalCount + " job" + (last.totalCount === 1 ? "" : "s") + " · page " + state.recoveredPage + " of " + pages
        : "";
      renderRecoveredStatus();
    }

    function renderRecoveredStatus() {
      var last = state.recoveredLast;
      var counts = state.recoveredCounts;
      var status = { title: "Loading recovered translation activity…", detail: "" };
      if (last && last.failed) status.title = "Recovered translation activity could not be loaded. Reload the page and try again.";
      else if (last && last.cultureAvailable !== true) status.title = "The translation target culture is unavailable, so there is no translation activity to show.";
      else if (last) status = recoveredStatus(counts, !poll.stopped && hasWork());
      byId("lfqRecoveredStatusTitle").textContent = status.title;
      byId("lfqRecoveredStatusCounts").textContent = status.detail;
      byId("lfqRecoveredTabCount").textContent = counts.active > 0 ? " (" + counts.active + " active)" : "";
    }

    // Only the latest read is applied, so an earlier answer arriving late never overwrites a newer one.
    function loadRecovered(page) {
      var seq = ++state.recoveredSeq;
      $.ajax(recoveredRequest(page))
        .done(function (data) {
          if (seq !== state.recoveredSeq) return;
          state.recoveredLast = data;
          state.recoveredCounts = recoveredCounts(data.counts);
          state.recoveredPage = data.page;
          state.pool = mergePool(state.pool, data.items);
          if (discovery.cursor === null) discovery.cursor = data.page;
          state.recovered = (data.items || []).filter(function (item) { return STATE_LABELS[item.state]; }).map(function (item) {
            return {
              jobId: item.jobId, contentId: item.contentId, title: item.title, typeId: item.typeId, isActive: item.isActive === true,
              state: item.state, attemptCount: item.attemptCount, relevantAt: item.relevantAt, failureReason: failureReason(item),
            };
          });
          // The first answer picks the initial tab once: running work first. A tab chosen by hand is never overridden.
          if (!state.tabChosen && state.recoveredCounts.active > 0) showTab("recovered", false);
          state.tabChosen = true;
          renderRecovered();
          schedulePoll();
        })
        .fail(function () {
          if (seq !== state.recoveredSeq) return;
          state.tabChosen = true;
          state.recoveredLast = { failed: true };
          state.recovered = [];
          state.recoveredCounts = recoveredCounts(null);
          renderRecovered();
        });
    }

    function renderProgress() {
      var counts = progressCounts(state.batch);
      byId("lfqSummaryTitle").textContent = counts.terminal + " of " + counts.total + " submitted item" + (counts.total === 1 ? "" : "s") +
        " complete" + (counts.terminal === counts.total ? "." : ", checking every few seconds.");
      byId("lfqProgressCounts").textContent = "Submitted " + counts.total + " · Queued " + counts.queued + " · Translating " + counts.processing +
        " · Translated " + counts.succeeded + " · Failed " + counts.failed + " · Superseded " + counts.superseded +
        " · Skipped or not queued " + counts.skipped;
      var bar = byId("lfqProgressBar");
      bar.style.width = counts.percent + "%";
      bar.setAttribute("aria-valuenow", counts.percent);
      bar.setAttribute("aria-valuetext", counts.terminal + " of " + counts.total + " content items complete");
      bar.textContent = counts.percent + "%";
      var list = byId("lfqSummaryList");
      list.replaceChildren();
      state.batch.forEach(function (item) {
        list.appendChild(el("li", null, batchLine(item)));
      });
      byId("lfqSummary").hidden = false;
    }

    function setPollMessage(text) {
      var node = byId("lfqPollStatus");
      node.textContent = text || "";
      node.hidden = !text;
    }

    function stopPolling() {
      poll.stopped = true;
      if (poll.timer !== null) clearTimeout(poll.timer);
      poll.timer = null;
    }

    // One request at a time: the next round is scheduled only after the previous one (Progress, then any
    // discovery read) has finished.
    function schedulePoll() {
      if (poll.stopped || poll.inFlight || poll.timer !== null || !hasWork()) return;
      poll.timer = setTimeout(pollOnce, POLL_INTERVAL_MS);
    }

    function tracked() {
      var sets = pendingSets(state.batch, state.pool);
      return sets.batch.concat(sets.recovered);
    }

    // A job is being polled, or the server still counts active recovered jobs not yet discovered.
    function hasWork() {
      return tracked().length > 0 || state.recoveredCounts.active > 0;
    }

    function pollOnce() {
      poll.timer = null;
      if (poll.stopped) return;
      var slot = pollWindow(pendingSets(state.batch, state.pool), poll.cursors, config.maxItems);
      poll.inFlight = true;
      if (slot.ids.length === 0) {
        discover();
        return;
      }
      $.ajax(progressRequest(slot.ids))
        .done(function (data) {
          // While discovery runs, its read right after this answer brings the server's counts; adjusting them
          // here as well could count a change it already includes twice.
          var discovering = nextDiscoveryPage() !== null;
          state.batch = mergeProgress(state.batch, data && data.items);
          state.recovered = mergeProgress(state.recovered, data && data.items);
          var before = state.pool;
          var after = mergeProgress(before, data && data.items);
          if (!discovering) state.recoveredCounts = applyStateChanges(state.recoveredCounts, before, after);
          state.pool = after.filter(isPending);
          poll.cursors = slot.cursors;
          poll.retried = false;
          setPollMessage(null);
        })
        .fail(function () {
          // Keep the last known states. The same window is retried once before the rotation moves on,
          // so a transient failure never skips a group, and a persistent one never blocks the others.
          if (poll.retried) poll.cursors = slot.cursors;
          poll.retried = !poll.retried;
          setPollMessage("Progress is temporarily unavailable. Retrying…");
        })
        .always(function () {
          if (poll.stopped) return;
          if (state.batch.length > 0) renderProgress();
          if (state.last) renderRows(state.last.items);
          renderToolbar();
          discover();
        });
    }

    // Reads the next active recovered page, if one is due, into the pool and the counts - never into the
    // visible table's page, pager or loading state; rows already shown only take the fresher states. A failed
    // read keeps the last known state and is retried once before the rotation moves on.
    function nextDiscoveryPage() {
      return discoveryPage(state.recoveredCounts.active, PAGE_SIZE, state.recoveredPage, tracked().length, discovery.cursor);
    }

    function discover() {
      var page = nextDiscoveryPage();
      if (page === null) {
        finishRound();
        return;
      }
      $.ajax(recoveredRequest(page))
        .done(function (data) {
          if (poll.stopped) return;
          if (data.cultureAvailable === true) {
            state.recoveredCounts = recoveredCounts(data.counts);
            state.pool = mergePool(state.pool, data.items);
            state.recovered = mergeProgress(state.recovered, data.items);
          } else {
            state.recoveredCounts = recoveredCounts(null);
            state.pool = [];
          }
          discovery.cursor = page;
          discovery.retried = false;
        })
        .fail(function () {
          if (discovery.retried) discovery.cursor = page;
          discovery.retried = !discovery.retried;
        })
        .always(function () {
          if (!poll.stopped) finishRound();
        });
    }

    function finishRound() {
      poll.inFlight = false;
      renderRecovered();
      if (hasWork()) schedulePoll();
      else {
        // Everything known is terminal and nothing active is left to discover: reconcile both lists; the
        // summary stays visible.
        loadRecovered(state.recoveredPage);
        load(state.page);
      }
    }

    function confirmQueue(count) {
      if (window.Swal)
        return Swal.fire({
          title: "Queue translation?",
          text: confirmText(count),
          icon: "question",
          showCancelButton: true,
          confirmButtonText: "Queue " + count,
        }).then(function (result) { return result.isConfirmed === true; });
      return Promise.resolve(window.confirm(confirmText(count)));
    }

    function queueSelected() {
      if (!canQueue(config.workerEnabled, state.cultureAvailable, state.selected.length, state.busy)) return;
      var ids = state.selected.slice();
      confirmQueue(ids.length).then(function (confirmed) {
        if (!confirmed) return;
        state.busy = true;
        renderRows(state.last ? state.last.items : []);
        renderToolbar();
        renderPager();
        $.ajax(queueRequest(ids))
          .done(function (data) {
            var submitted = batchFromQueue(data.items);
            var ids = submitted.map(function (item) { return item.contentId; });
            state.batch = state.batch.filter(function (item) { return ids.indexOf(item.contentId) < 0; }).concat(submitted);
            state.selected = [];
            renderProgress();
            renderRecovered();
            loadRecovered(state.recoveredPage); // the new jobs join the recovered counts
          })
          .fail(function (xhr) {
            // A 400 carries the server's fixed validation message only.
            var message = xhr.status === 400 && xhr.responseJSON && typeof xhr.responseJSON.error === "string"
              ? xhr.responseJSON.error
              : "The selection could not be queued. Reload the page and try again.";
            messageBox("Not queued", message, "error");
          })
          .always(function () {
            state.busy = false;
            if (tracked().length > 0) {
              // Keep the list and the batch visible; rows update in place while polling.
              if (state.last) renderRows(state.last.items);
              renderToolbar();
              renderPager();
              schedulePoll();
            } else {
              load(state.page); // reconcile: nothing to wait for
            }
          });
      });
    }

    byId("lfqFilters").addEventListener("submit", function (event) {
      event.preventDefault();
      var filters = readFilters();
      if (String(filters.contentId).trim() && parsePositiveInt(filters.contentId) === null) {
        messageRow("Enter a positive whole-number content ID.");
        return;
      }
      state.filters = filters;
      load(1);
    });
    byId("lfqPrev").addEventListener("click", function () { load(state.page - 1); });
    byId("lfqNext").addEventListener("click", function () { load(state.page + 1); });
    byId("lfqPage").addEventListener("change", function () { load(parsePositiveInt(byId("lfqPage").value) || 1); });
    byId("lfqClear").addEventListener("click", function () {
      state.selected = [];
      if (state.last) renderRows(state.last.items);
      renderToolbar();
    });
    byId("lfqQueue").addEventListener("click", queueSelected);
    byId("lfqRecoveredPrev").addEventListener("click", function () { loadRecovered(state.recoveredPage - 1); });
    byId("lfqRecoveredNext").addEventListener("click", function () { loadRecovered(state.recoveredPage + 1); });
    Object.keys(TABS).forEach(function (key) {
      var tab = byId(TABS[key][0]);
      tab.addEventListener("click", function () {
        state.tabChosen = true;
        showTab(key, false);
      });
      // Arrow keys move between the two tabs, Home/End to the first/last (the WAI-ARIA tabs pattern).
      tab.addEventListener("keydown", function (event) {
        var other = key === "candidates" ? "recovered" : "candidates";
        var target = { ArrowLeft: other, ArrowRight: other, Home: "candidates", End: "recovered" }[event.key];
        if (!target) return;
        event.preventDefault();
        state.tabChosen = true;
        showTab(target, true);
      });
    });
    window.addEventListener("pagehide", stopPolling);
    window.addEventListener("beforeunload", stopPolling);

    showTab("candidates", false);
    renderToolbar();
    load(1);
    renderRecovered();
    loadRecovered(1);
  }

  return {
    init: init,
    parsePositiveInt: parsePositiveInt,
    buildQuery: buildQuery,
    pageCount: pageCount,
    toggle: toggle,
    unavailableReason: unavailableReason,
    canQueue: canQueue,
    confirmText: confirmText,
    queueRequest: queueRequest,
    candidatesRequest: candidatesRequest,
    outcomeLabel: outcomeLabel,
    batchFromQueue: batchFromQueue,
    pendingJobIds: pendingJobIds,
    pendingSets: pendingSets,
    rotatingWindow: rotatingWindow,
    pollWindow: pollWindow,
    mergeProgress: mergeProgress,
    recoveredRequest: recoveredRequest,
    recoveredToShow: recoveredToShow,
    recoveredCounts: recoveredCounts,
    mergePool: mergePool,
    discoveryPage: discoveryPage,
    applyStateChanges: applyStateChanges,
    recoveredStatus: recoveredStatus,
    progressCounts: progressCounts,
    batchLine: batchLine,
    progressRequest: progressRequest,
    POLL_INTERVAL_MS: POLL_INTERVAL_MS,
  };
})();
