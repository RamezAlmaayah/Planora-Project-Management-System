(() => {
    "use strict";

    document.querySelectorAll("form[data-single-submit]").forEach((form) => {
        form.addEventListener("submit", (event) => {
            const message = form.dataset.confirm;
            if (message && !window.confirm(message)) {
                event.preventDefault();
                return;
            }

            form.querySelectorAll("button[type='submit']").forEach((button) => {
                button.disabled = true;
                button.setAttribute("aria-disabled", "true");
            });
        });
    });
})();
