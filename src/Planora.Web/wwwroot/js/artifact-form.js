(() => {
    "use strict";
    document.querySelectorAll("[data-artifact-picker]").forEach(picker => {
        const search = picker.querySelector("[data-artifact-search]");
        if (!search) return;
        search.addEventListener("input", () => {
            const query = search.value.trim().toLowerCase();
            picker.querySelectorAll("[data-artifact-option]").forEach(option => {
                option.hidden = query.length > 0 &&
                    !option.textContent.toLowerCase().includes(query);
            });
        });
    });
})();
