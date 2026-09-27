// Shared BackOffice helpers and component behaviour, loaded by Shared/_Layout.cshtml and Shared/_JSAssets.cshtml
// before any page script. Feature scripts and view partials call the global helpers below.
// Tested by src/Web.Tests/js/common.test.mjs.

// Client-side validation for AJAX forms: shows Bootstrap's invalid state, ties each error text to its field
// for assistive tech, and moves focus to the first field that needs fixing.
function checkFormValidity(formId) {
    var form = document.getElementById(formId);
    form.classList.add("was-validated");
    syncFieldValidity(form);
    if (!form.dataset.scsValidityWatch) {
        form.dataset.scsValidityWatch = "true";
        form.addEventListener("input", function () { syncFieldValidity(form); });
        form.addEventListener("change", function () { syncFieldValidity(form); });
    }
    if (form.checkValidity()) return true;
    var firstInvalid = form.querySelector(":invalid:not(fieldset)");
    if (firstInvalid) firstInvalid.focus();
    return false;
}

function syncFieldValidity(form) {
    form.querySelectorAll("input, select, textarea").forEach(function (field) {
        if (field.type === "hidden" || !field.willValidate) return;
        if (field.validity.valid) {
            field.removeAttribute("aria-invalid");
            return;
        }
        field.setAttribute("aria-invalid", "true");
        var feedback = field.parentElement && field.parentElement.querySelector(".invalid-feedback");
        if (!feedback) return;
        if (!feedback.id) feedback.id = (field.id || field.name || "field") + "-error";
        var describedBy = (field.getAttribute("aria-describedby") || "").split(" ").filter(Boolean);
        if (describedBy.indexOf(feedback.id) === -1) {
            describedBy.push(feedback.id);
            field.setAttribute("aria-describedby", describedBy.join(" "));
        }
    });
}

// Loading placeholder for a region (modal body, list) that is about to be filled by a request.
function clearModalBody(regionId) {
    $("#" + regionId).html(
        '<div class="p-5 text-center" data-scs-pending>' +
        '<div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading</span></div>' +
        "</div>");
    var region = document.getElementById(regionId);
    markPending("regions", region && region.querySelector("[data-scs-pending]"));
}

// The record is saved before its file is uploaded, so a failed upload still reloads to show the record.
function uploadFile(file, url, entityId) {
    var upload = new FormData();
    upload.append("File", file);
    upload.append("EntityId", entityId);
    upload.append("Extension", file.type.toLowerCase());

    function uploadFailed() {
        messageBox("Saved without the file", "The details were saved, but the file couldn't be uploaded. Edit the item to try the upload again.", "warning")
            .then(function () { location.reload(); });
    }

    $.ajax({
        url: url,
        type: "POST",
        contentType: false,
        processData: false,
        data: upload
    }).done(function (data) {
        if (data === "Done") location.reload();
        else uploadFailed();
    }).fail(uploadFailed);
}

// Command buttons show progress while their request runs: disabled (so it can't be sent twice), aria-busy,
// and a spinner label. The original label is kept so it can be restored after a failed request.
function setBusy(btnId, text) {
    var button = document.getElementById(btnId);
    if (!button) return;
    if (!button.hasAttribute("data-scs-label")) button.setAttribute("data-scs-label", button.innerHTML);
    // A new token per busy state, so a request only ever restores the busy state it owned.
    button.setAttribute("data-scs-busy", String(++busyCount));
    markPending("buttons", button);
    button.disabled = true;
    button.setAttribute("aria-busy", "true");
    button.innerHTML = '<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>' + text;
}

function clearBusy(button, funcName, html) {
    if (!button) return;
    if (html === undefined) html = button.getAttribute("data-scs-label");
    button.disabled = false;
    button.removeAttribute("aria-busy");
    if (html !== null) button.innerHTML = html;
    button.removeAttribute("data-scs-label");
    button.removeAttribute("data-scs-busy");
    if (funcName) button.setAttribute("onclick", funcName);
}

function setLoadingForBtnFilter(btnId) { setBusy(btnId, "Filtering"); }
function removeLoadingForBtnFilter(btnId, funcName) { clearBusy(document.getElementById(btnId), funcName); }
function setLoadingForBtn(btnId) { setBusy(btnId, "Saving"); }
function removeLoadingForBtn(btnId, funcName, text) { clearBusy(document.getElementById(btnId), funcName, text); }

// Returns a promise in every case so callers can chain what happens after the message is dismissed.
function messageBox(title, text, icon) {
    if (window.Swal) return Swal.fire({ backdrop: false, title: title, text: text, icon: icon });
    window.alert(title + " " + text);
    return Promise.resolve({});
}

// Request-scoped recovery. A busy button or pending region created in the same synchronous turn as a
// request (setLoadingForBtn / clearModalBody, then $.ajax) belongs to that request, first come first served,
// so two requests started together each own their own region. A request started from another request's
// success callback inherits that request's still-pending UI (e.g. save, then reload the list).
// If a request fails and its caller handles errors itself (.fail, .catch, .then(_, onError) or an `error`
// option), nothing happens here. Otherwise only the UI that request owned is put back and explained.
var busyCount = 0;
var fresh = { buttons: [], regions: [] };
var freshResetQueued = false;
var resolvingRequest = null;

function markPending(kind, el) {
    if (!el) return;
    fresh[kind].push(el);
    if (freshResetQueued) return;
    freshResetQueued = true;
    Promise.resolve().then(function () {
        fresh = { buttons: [], regions: [] };
        freshResetQueued = false;
    });
}

function claimPendingUi(xhr, settings) {
    var parent = resolvingRequest;
    var button = fresh.buttons.shift();
    xhr.scsButton = button ? { el: button, token: button.getAttribute("data-scs-busy") } : parent && parent.scsButton;
    xhr.scsRegion = fresh.regions.shift() || (parent && parent.scsRegion);
    xhr.scsHandled = typeof settings.error === "function" || (Array.isArray(settings.error) && settings.error.length > 0);

    function handledBy(method, failArgIndex) {
        var original = xhr[method];
        if (!original) return;
        xhr[method] = function () {
            if (typeof arguments[failArgIndex] === "function") xhr.scsHandled = true;
            return original.apply(this, arguments);
        };
    }
    handledBy("fail", 0);
    handledBy("catch", 0);
    handledBy("then", 1);

    // Registered before the caller's own callbacks, so requests they start can see which request they came from.
    xhr.done(function () {
        resolvingRequest = xhr;
        Promise.resolve().then(function () { if (resolvingRequest === xhr) resolvingRequest = null; });
    });
}

function recoverFailedRequest(xhr) {
    if (xhr.statusText === "abort" || xhr.scsHandled) return;
    var region = xhr.scsRegion;
    if (region && region.isConnected) {
        region.outerHTML = '<div class="alert alert-danger mb-0" role="alert">Error: this couldn\'t be loaded or saved. ' +
            "Close it and try again, or reload the page.</div>";
    }
    var owned = xhr.scsButton;
    if (owned && owned.el.getAttribute("data-scs-busy") === owned.token) {
        clearBusy(owned.el);
        messageBox("Not saved", "The server couldn't complete the request. Check your connection and try again.", "error");
    }
}

function initBackOfficeComponents(doc, $) {
    if ($) {
        $(doc).ajaxSend(function (event, xhr, settings) { claimPendingUi(xhr, settings); });
        $(doc).ajaxError(function (event, xhr) { recoverFailedRequest(xhr); });
        $(doc).on("draw.dt responsive-resize.dt", function () { labelDetailToggles(doc); });
    }
    // Responsive's own Enter handler clicks the cell, so this covers keyboard and pointer toggles.
    doc.addEventListener("click", function (event) {
        if (event.target.closest && event.target.closest("td.dtr-control")) labelDetailToggles(doc);
    });

    // Enter in a single-field AJAX form would natively POST the whole page. Run the form's own primary
    // command instead; forms with a real submit button, or that handle submit themselves, are left alone.
    doc.addEventListener("submit", function (event) {
        var form = event.target;
        if (event.defaultPrevented || form.querySelector("[type=submit], button:not([type])")) return;
        var command = form.querySelector(".btn-primary[onclick]");
        if (!command) return;
        event.preventDefault();
        if (!command.disabled) command.click();
    });

    // Bootstrap returns focus to a modal's opener only while it is still on the page. Saving from a modal
    // usually re-renders the list the opener lived in, so fall back to its replacement, then to the main region.
    var opener = null;
    doc.addEventListener("show.bs.modal", function (event) {
        opener = event.relatedTarget || doc.activeElement;
    });
    doc.addEventListener("hidden.bs.modal", function () {
        var from = opener;
        opener = null;
        if (!from || (doc.activeElement && doc.activeElement !== doc.body)) return;
        var target = from.isConnected ? from : findReplacement(doc, from);
        if (target) target.focus();
    });
}

// DataTables Responsive makes the first cell of a collapsed row a keyboard toggle (Enter) for its hidden
// columns, but gives it no name or state. Keep the cell's own text and say what Enter does.
function labelDetailToggles(doc) {
    doc.querySelectorAll("td.dtr-control").forEach(function (cell) {
        if (!cell.hasAttribute("data-scs-text")) cell.setAttribute("data-scs-text", cell.textContent.trim());
        if (!cell.closest("table.collapsed")) {
            cell.removeAttribute("aria-label");
            return;
        }
        var open = cell.parentElement.classList.contains("parent");
        cell.setAttribute("aria-label", cell.getAttribute("data-scs-text") + (open ? ", hide details" : ", show details"));
    });
}

function findReplacement(doc, from) {
    if (from.id && doc.getElementById(from.id)) return doc.getElementById(from.id);
    var command = from.getAttribute("onclick");
    var modal = from.getAttribute("data-bs-target");
    var match = null;
    if (command || modal) {
        doc.querySelectorAll("[data-bs-target], [onclick]").forEach(function (candidate) {
            if (!match && candidate.getAttribute("onclick") === command && candidate.getAttribute("data-bs-target") === modal) match = candidate;
        });
    }
    return match || doc.getElementById("main-content");
}

initBackOfficeComponents(document, window.jQuery);
