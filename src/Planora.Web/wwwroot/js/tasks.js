for (const form of document.querySelectorAll("form[data-task-form]")) {
    let submitting = false;
    const buttons = Array.from(form.querySelectorAll('button[type="submit"]'), button => ({
        element: button,
        text: button.textContent,
        disabled: button.disabled
    }));

    form.addEventListener("submit", event => {
        if (submitting) {
            event.preventDefault();
            return;
        }

        const jquery = window.jQuery;
        if (jquery && typeof jquery.fn.valid === "function" && !jquery(form).valid()) {
            event.preventDefault();
            return;
        }

        if (event.defaultPrevented) {
            return;
        }

        submitting = true;
        form.setAttribute("aria-busy", "true");
        for (const button of buttons) {
            button.element.disabled = true;
            button.element.textContent = "Saving...";
        }
    });

    window.addEventListener("pageshow", () => {
        submitting = false;
        form.removeAttribute("aria-busy");
        for (const button of buttons) {
            button.element.disabled = button.disabled;
            button.element.textContent = button.text;
        }
    });
}
