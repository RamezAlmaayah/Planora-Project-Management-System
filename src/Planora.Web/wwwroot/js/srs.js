(() => {
    "use strict";
    const form = document.querySelector("[data-srs-save-form]");
    if (!form) return;
    form.addEventListener("submit", () => {
        const button = form.querySelector("[data-srs-save-button]");
        if (!button || button.disabled) return;
        button.disabled = true;
        button.setAttribute("aria-disabled", "true");
        button.textContent = "Saving SRS...";
    });
})();
