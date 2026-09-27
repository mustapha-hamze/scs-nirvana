// Legacy Farsi Translation Queue dashboard (Areas/BackOffice/Views/LegacyFarsiTranslationQueue/Index.cshtml).
// Reads GET Candidates and posts only selected content IDs to POST Queue. Requests go through $.ajax so
// the layout's global $.ajaxSetup adds the X-CSRF-TOKEN antiforgery header. Every dynamic value is
// written with textContent. The server re-checks every submitted ID; the page only reflects the
// candidate list it was given and the outcomes Queue returned. No polling or progress requests.
var LegacyFarsiQueue = (function () {
  var CANDIDATES_URL = "/BackOffice/LegacyFarsiTranslationQueue/Candidates";
  var QUEUE_URL = "/BackOffice/LegacyFarsiTranslationQueue/Queue";
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
  // A job exists (or the item is done) for these: never offer them for selection again this session.
  var LOCKED = { Queued: true, AlreadyQueued: true, AlreadyReady: true };

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

  // Counts per outcome, in the order the server returned them.
  function summarize(items) {
    var counts = {};
    items.forEach(function (item) { counts[item.outcome] = (counts[item.outcome] || 0) + 1; });
    return { total: items.length, counts: counts };
  }

  function outcomeLabel(outcome) {
    return OUTCOME_LABELS[outcome] || "Skipped";
  }

  // ---- Page ----

  function init(root) {
    var byId = function (id) { return document.getElementById(id); };
    var config = {
      workerEnabled: root.getAttribute("data-worker-enabled") === "true",
      maxItems: parsePositiveInt(root.getAttribute("data-max-items")) || 1,
      typeIds: (root.getAttribute("data-type-ids") || "").split(",").map(parsePositiveInt).filter(function (x) { return x !== null; }),
    };
    var state = { page: 1, filters: readFilters(), selected: [], outcomes: {}, cultureAvailable: null, busy: false, last: null };

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

    function messageRow(text) {
      var tbody = byId("lfqRows");
      tbody.replaceChildren();
      var tr = el("tr");
      var td = el("td", "scs-empty-state", text);
      td.colSpan = 7;
      tr.appendChild(td);
      tbody.appendChild(tr);
    }

    function isSelectable(item) {
      return unavailableReason(config.workerEnabled, state.cultureAvailable) === null && !LOCKED[state.outcomes[item.contentId]];
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
        tr.appendChild(el("td", null, String(item.updatedAt || "").slice(0, 16).replace("T", " ")));
        var outcome = el("td");
        var known = state.outcomes[id];
        outcome.appendChild(known
          ? el("span", "scs-status " + (OUTCOME_STATUS[known] || ""), outcomeLabel(known))
          : el("span", "text-muted", "Eligible"));
        tr.appendChild(outcome);
        tbody.appendChild(tr);
      });
      if (items.length === 0) messageRow("No legacy Farsi content matches these filters.");
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

    function renderSummary(items) {
      var summary = summarize(items);
      byId("lfqSummaryTitle").textContent = "Submitted " + summary.total + " item" + (summary.total === 1 ? "" : "s") + ": " +
        Object.keys(summary.counts).map(function (k) { return outcomeLabel(k) + " " + summary.counts[k]; }).join(", ") + ".";
      var list = byId("lfqSummaryList");
      list.replaceChildren();
      items.forEach(function (item) {
        list.appendChild(el("li", null, "Content " + item.contentId + ": " + outcomeLabel(item.outcome)));
      });
      byId("lfqSummary").hidden = false;
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
            data.items.forEach(function (item) { state.outcomes[item.contentId] = item.outcome; });
            state.selected = [];
            renderSummary(data.items);
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
            load(state.page); // reconcile: queued or no-longer-eligible items drop out of the list
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

    renderToolbar();
    load(1);
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
    summarize: summarize,
    outcomeLabel: outcomeLabel,
  };
})();
