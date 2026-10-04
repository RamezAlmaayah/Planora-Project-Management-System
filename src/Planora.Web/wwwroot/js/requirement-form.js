(() => {
    "use strict";

    document.querySelectorAll("[data-requirement-form]").forEach(form => {
        const typeSelect = form.querySelector("[data-requirement-type]");
        const sections = Array.from(form.querySelectorAll("[data-type-section]"));
        const dynamicLabels = Array.from(form.querySelectorAll("[data-fr-label][data-nfr-label]"));

        const updateType = () => {
            if (!typeSelect) return;
            const value = typeSelect.value.toLowerCase();
            const isFunctional = value === "1" || value === "functional";

            sections.forEach(section => {
                const show = section.dataset.typeSection ===
                    (isFunctional ? "functional" : "nonfunctional");
                section.hidden = !show;
                section.querySelectorAll("input, select, textarea, button").forEach(control => {
                    control.disabled = !show;
                });
            });

            dynamicLabels.forEach(label => {
                label.textContent = isFunctional
                    ? label.dataset.frLabel
                    : label.dataset.nfrLabel;
            });

            const title = form.querySelector("#Title");
            const category = form.querySelector("#NfrCategory");
            if (title) title.required = isFunctional;
            if (category) category.required = !isFunctional;
        };

        if (typeSelect) {
            typeSelect.addEventListener("change", updateType);
            updateType();
        }

        form.querySelectorAll("[data-fr-search]").forEach(search => {
            search.addEventListener("input", () => {
                const query = search.value.trim().toLowerCase();
                const picker = search.closest(".requirements-v3-fr-picker");
                picker?.querySelectorAll("[data-fr-option]").forEach(option => {
                    option.hidden = query.length > 0 &&
                        !option.textContent.toLowerCase().includes(query);
                });
            });
        });
    });
})();
