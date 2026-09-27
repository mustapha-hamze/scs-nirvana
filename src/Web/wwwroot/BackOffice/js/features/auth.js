// Sign-in page behaviour (Areas/BackOffice/Views/Account/Login.cshtml): an accessible password
// visibility toggle and a one-shot submit state. Tested by src/Web.Tests/js/auth.test.mjs.
function initAuthForms(doc, win) {
    doc.querySelectorAll("[data-scs-password-toggle]").forEach(function (toggle) {
        var input = doc.getElementById(toggle.getAttribute("aria-controls"));
        toggle.addEventListener("click", function () {
            setPasswordVisible(toggle, input, input.type === "password");
        });
    });

    doc.querySelectorAll("form[data-scs-auth-form]").forEach(function (form) {
        var button = form.querySelector("[type=submit]");
        var label = button.innerHTML;

        // The submit event only fires once native validation passes, so this never locks an invalid form.
        form.addEventListener("submit", function (event) {
            if (form.getAttribute("aria-busy") === "true") {
                event.preventDefault();
                return;
            }
            form.setAttribute("aria-busy", "true");
            button.disabled = true;
            button.innerHTML = '<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span> Signing in...';
            doc.querySelectorAll("[data-scs-password-toggle]").forEach(function (toggle) {
                setPasswordVisible(toggle, doc.getElementById(toggle.getAttribute("aria-controls")), false);
            });
        });

        // Back/forward cache can restore the locked form; give the user a usable button again.
        win.addEventListener("pageshow", function (event) {
            if (!event.persisted) return;
            form.removeAttribute("aria-busy");
            button.disabled = false;
            button.innerHTML = label;
        });
    });
}

function setPasswordVisible(toggle, input, visible) {
    var text = visible ? "Hide password" : "Show password";
    input.type = visible ? "text" : "password";
    toggle.setAttribute("aria-pressed", String(visible));
    toggle.setAttribute("aria-label", text);
    toggle.setAttribute("title", text);
    toggle.querySelector("i").className = visible ? "uil uil-eye-slash" : "uil uil-eye";
}

initAuthForms(document, window);
