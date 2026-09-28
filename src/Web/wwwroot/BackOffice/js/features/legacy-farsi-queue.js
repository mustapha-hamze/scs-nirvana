// Legacy Farsi Translation Queue dashboard (Areas/BackOffice/Views/LegacyFarsiTranslationQueue/Index.cshtml).
// Reads GET Candidates, posts only selected content IDs to POST Queue, then polls GET Progress with the
// returned job IDs until every submitted item is terminal. On load it also reads GET RecoveredJobs - the
// application's active and recently completed jobs, durable across a refresh - shows them in their own
// section (never in the submitted batch's bar, whose denominator is unknown after a refresh) and polls the
// active ones through the same bounded Progress chain; a job is polled once however it is known.
// Requests go through $.ajax so the layout's global $.ajaxSetup adds the X-CSRF-TOKEN antiforgery header. Every dynamic value is written with
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

  // Distinct pending job IDs of the batch and the recovered jobs, at most max (the Progress limit).
  // ponytail: the first max stay polled until terminal; rotate the window if long queues starve the rest.
  function trackedJobIds(batch, recovered, max) {
    var ids = [];
    batch.concat(recovered).forEach(function (item) {
      if (isPending(item) && ids.indexOf(item.jobId) < 0) ids.push(item.jobId);
    });
    return ids.slice(0, max);
  }

  // Applies Progress items (state, and attempts for items that show them) by job ID to batch or
  // recovered items. Unknown states are ignored so a job keeps its last known state.
  function mergeProgress(items, progressItems) {
    var byJob = {};
    (progressItems || []).forEach(function (p) { if (STATE_LABELS[p.state]) byJob[p.jobId] = p; });
    return items.map(function (item) {
      var p = item.jobId != null ? byJob[item.jobId] : undefined;
      if (!p) return item;
      var next = Object.assign({}, item, { state: p.state });
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

  function batchLabel(item) {
    return item.jobId != null ? STATE_LABELS[item.state] : outcomeLabel(item.outcome);
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
      recovered: [], recoveredLast: null, recoveredPage: 1 };
    var poll = { timer: null, inFlight: false, stopped: false };

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

    function messageRow(text, tbodyId) {
      var tbody = byId(tbodyId || "lfqRows");
      tbody.replaceChildren();
      var tr = el("tr");
      var td = el("td", "scs-empty-state", text);
      td.colSpan = 7;
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
        messageRow("No legacy Farsi content needs queueing for these filters. Content with a current translation job is not listed here; see Recovered translation jobs.");
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
        tr.appendChild(el("td", null, item.attemptCount));
        tr.appendChild(el("td", null, shortTime(item.relevantAt)));
        tbody.appendChild(tr);
      });
      if (!last) messageRow("Loading translation jobs…", "lfqRecoveredRows");
      else if (last.failed) messageRow("Translation jobs could not be loaded. Reload the page and try again.", "lfqRecoveredRows");
      else if (shown.length === 0)
        messageRow(state.recovered.length > 0 ? "These jobs are shown in the submitted batch above." : "No current or recent translation jobs.", "lfqRecoveredRows");
      var pages = last && !last.failed ? pageCount(last.totalCount, last.pageSize) : 1;
      byId("lfqRecoveredPrev").disabled = !last || state.recoveredPage <= 1;
      byId("lfqRecoveredNext").disabled = !last || state.recoveredPage >= pages;
      byId("lfqRecoveredInfo").textContent = last && !last.failed
        ? last.totalCount + " job" + (last.totalCount === 1 ? "" : "s") + " · page " + state.recoveredPage + " of " + pages
        : "";
    }

    function loadRecovered(page) {
      $.ajax(recoveredRequest(page))
        .done(function (data) {
          state.recoveredLast = data;
          state.recoveredPage = data.page;
          state.recovered = (data.items || []).filter(function (item) { return STATE_LABELS[item.state]; }).map(function (item) {
            return {
              jobId: item.jobId, contentId: item.contentId, title: item.title, typeId: item.typeId, isActive: item.isActive === true,
              state: item.state, attemptCount: item.attemptCount, relevantAt: item.relevantAt,
            };
          });
          renderRecovered();
          schedulePoll();
        })
        .fail(function () {
          state.recoveredLast = { failed: true };
          state.recovered = [];
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
        list.appendChild(el("li", null, "Content " + item.contentId + ": " + batchLabel(item)));
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

    // One request at a time: the next poll is scheduled only after the previous one has finished.
    function schedulePoll() {
      if (poll.stopped || poll.inFlight || poll.timer !== null || tracked().length === 0) return;
      poll.timer = setTimeout(pollOnce, POLL_INTERVAL_MS);
    }

    function tracked() {
      return trackedJobIds(state.batch, state.recovered, config.maxItems);
    }

    function pollOnce() {
      poll.timer = null;
      var ids = tracked();
      if (poll.stopped || ids.length === 0) return;
      poll.inFlight = true;
      $.ajax(progressRequest(ids))
        .done(function (data) {
          state.batch = mergeProgress(state.batch, data && data.items);
          state.recovered = mergeProgress(state.recovered, data && data.items);
          setPollMessage(null);
        })
        .fail(function () {
          // Keep the last known states; the next interval retries.
          setPollMessage("Progress is temporarily unavailable. Retrying…");
        })
        .always(function () {
          poll.inFlight = false;
          if (poll.stopped) return;
          if (state.batch.length > 0) renderProgress();
          renderRecovered();
          if (state.last) renderRows(state.last.items);
          renderToolbar();
          if (tracked().length > 0) schedulePoll();
          else load(state.page); // all terminal: reconcile the list; the summary stays visible
        });
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
    window.addEventListener("pagehide", stopPolling);
    window.addEventListener("beforeunload", stopPolling);

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
    trackedJobIds: trackedJobIds,
    mergeProgress: mergeProgress,
    recoveredRequest: recoveredRequest,
    recoveredToShow: recoveredToShow,
    progressCounts: progressCounts,
    progressRequest: progressRequest,
    POLL_INTERVAL_MS: POLL_INTERVAL_MS,
  };
})();
