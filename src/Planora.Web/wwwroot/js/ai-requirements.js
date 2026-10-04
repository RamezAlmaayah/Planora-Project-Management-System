(() => {
    "use strict";

    const generationForm = document.querySelector("[data-ai-generation-form]");
    if (generationForm) {
        const generateButton = generationForm.querySelector("[data-ai-generate-button]");
        const qualityResult = generationForm.querySelector("[data-ai-quality-result]");
        const contextInput = generationForm.querySelector("#AdditionalContext");

        generationForm.addEventListener("submit", (event) => {
            const button = event.submitter;
            const loading = generationForm.querySelector("[data-ai-loading]");
            if (!button || button.disabled) return;
            generationForm.querySelectorAll("button[type='submit']").forEach((item) => {
                item.disabled = true;
                item.setAttribute("aria-disabled", "true");
            });
            const loadingText = generationForm.querySelector("[data-ai-loading-text]");
            if (loadingText) {
                loadingText.textContent = button.matches("[data-ai-analyze-button]")
                    ? "Analyzing input quality..."
                    : "Generating requirement suggestions...";
            }
            if (loading) loading.hidden = false;
        });

        if (qualityResult) {
            generationForm.querySelectorAll("#Mode, #SuggestionCount, #AdditionalContext").forEach((input) => {
                input.addEventListener("input", () => {
                    if (generateButton) {
                        generateButton.disabled = true;
                        generateButton.setAttribute("aria-disabled", "true");
                    }
                    qualityResult.classList.add("is-stale");
                    qualityResult.setAttribute("aria-label", "Input changed. Analyze again before generating.");
                });
            });
        }

        generationForm.querySelectorAll("[data-ai-apply-improvement]").forEach((button) => {
            button.addEventListener("click", () => {
                const item = button.closest("[data-ai-improvement]");
                if (!item || !contextInput) return;
                const selected = item.querySelector("input[type='radio']:checked");
                const freeText = item.querySelector("[data-ai-improvement-answer]");
                const answer = (selected?.value || freeText?.value || "").trim();
                const status = item.querySelector("[data-ai-improvement-status]");
                if (!answer) {
                    if (status) status.textContent = "Choose or enter an answer first.";
                    return;
                }
                const topic = (item.dataset.topic || "Additional detail").trim();
                const suggested = (item.dataset.suggestedText || "").trim();
                let sentence = suggested
                    ? `${suggested} Answer: ${answer}`
                    : `${topic}: ${answer}`;
                if (!/[.!?]$/.test(sentence)) sentence += ".";
                const current = contextInput.value.trim();
                if (!current.toLocaleLowerCase().includes(sentence.toLocaleLowerCase())) {
                    contextInput.value = current ? `${current}\n${sentence}` : sentence;
                    contextInput.dispatchEvent(new Event("input", { bubbles: true }));
                }
                if (status) status.textContent = "Applied. Analyze again to assess the updated input.";
            });
        });
    }

    document.querySelectorAll("[data-remove-ai-draft]").forEach((button) => {
        button.addEventListener("click", () => {
            const draft = button.closest("[data-ai-draft]");
            const include = draft?.querySelector("input[type='checkbox'][name$='.Include']");
            if (include) include.checked = false;
            if (draft) draft.hidden = true;
        });
    });
})();
