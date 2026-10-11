// Scoped to the auth forms: retain validation and avoid repeated valid submissions.
document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll(".auth-v3-form").forEach(form => {
        form.addEventListener("submit", event => {
            if (form.dataset.submitting === "true") {
                event.preventDefault();
                return;
            }
            queueMicrotask(() => {
                if (event.defaultPrevented || !form.checkValidity()) return;
                form.dataset.submitting = "true";
                form.setAttribute("aria-busy", "true");
                form.querySelectorAll('button[type="submit"]').forEach(button => {
                    button.disabled = true;
                });
            });
        });
    });
});

window.addEventListener("pageshow", () => {
    document.querySelectorAll(".auth-v3-form").forEach(form => {
        delete form.dataset.submitting;
        form.removeAttribute("aria-busy");
        form.querySelectorAll('button[type="submit"]').forEach(button => button.disabled = false);
    });
    // History restoration must not retain the outgoing page's hidden animation.
    document.querySelectorAll(".auth-v3-shell").forEach(shell => {
        [...shell.classList].filter(name => name.startsWith("auth-v3-leave-to-"))
            .forEach(name => shell.classList.remove(name));
    });
});
