const board = document.querySelector("[data-scrum-board]");

if (board) {
    const projectId = Number(board.dataset.projectId);
    const sprintId = Number(board.dataset.sprintId);
    const updateUrl = board.dataset.updateUrl;
    const qaUrl = board.dataset.qaUrl;
    const readOnly = board.dataset.readOnly === "true";
    const token = document.querySelector('.scrum-board-v3-token input[name="__RequestVerificationToken"]')?.value;
    const toast = document.querySelector("[data-board-toast]");
    const qaDialog = document.querySelector("[data-qa-dialog]");
    const qaFailForm = document.querySelector("[data-qa-fail-form]");
    const qaNotes = document.querySelector("[data-qa-failure-notes]");
    const qaEvidence = document.querySelector("[data-qa-evidence]");
    const qaTitle = document.querySelector("[data-qa-title]");
    const qaNoteError = document.querySelector("[data-qa-note-error]");
    const qaFailSubmit = document.querySelector("[data-qa-fail-submit]");
    const pending = new Set();
    let draggedCard = null;
    let qaCard = null;
    let qaResult = 1;
    let toastTimer = 0;

    const nextStatus = status => status === 1 ? 2 : status === 2 ? 3 : null;

    function showToast(message, isError = false) {
        if (!toast) return;
        window.clearTimeout(toastTimer);
        toast.textContent = message;
        toast.classList.toggle("is-error", isError);
        toast.hidden = false;
        toastTimer = window.setTimeout(() => { toast.hidden = true; }, 4200);
    }

    function refreshColumns() {
        for (const column of board.querySelectorAll("[data-board-column]")) {
            const cards = column.querySelectorAll("[data-task-card]");
            const count = column.querySelector("[data-column-count]");
            const empty = column.querySelector("[data-column-empty]");
            if (count) count.textContent = String(cards.length);
            if (empty) empty.hidden = cards.length > 0;
        }
    }

    function setPending(card, value) {
        const id = card.dataset.taskId;
        card.classList.toggle("is-pending", value);
        card.setAttribute("aria-busy", String(value));
        if (value) pending.add(id); else pending.delete(id);
        for (const button of card.querySelectorAll("[data-transition-action], [data-qa-action]")) {
            button.disabled = value;
        }
        card.draggable = !value && card.dataset.transitionEnabled === "true";
    }

    function applyQaStatus(card, targetStatus) {
        card.dataset.status = String(targetStatus);
        card.dataset.transitionEnabled = "false";
        card.draggable = false;
        card.querySelector("[data-transition-action]")?.remove();
        card.querySelector(".scrum-board-v3-qa-actions")?.remove();
        board.querySelector(`[data-board-list="${targetStatus}"]`)?.append(card);
    }

    async function reviewTask(card, result, notes = null, files = []) {
        const taskId = Number(card.dataset.taskId);
        if (readOnly || pending.has(String(taskId))) return;
        setPending(card, true);
        if (qaFailSubmit) qaFailSubmit.disabled = true;

        try {
            const form = new FormData();
            form.append("ProjectId", String(projectId));
            form.append("SprintId", String(sprintId));
            form.append("TaskId", String(taskId));
            form.append("Result", String(result));
            if (notes) form.append("Notes", notes);
            for (const file of files) form.append("EvidenceFiles", file, file.name);
            const response = await fetch(qaUrl, {
                method: "POST",
                credentials: "same-origin",
                headers: { "RequestVerificationToken": token ?? "" },
                body: form
            });
            let payload = null;
            try { payload = await response.json(); } catch { /* controlled fallback below */ }
            if (!response.ok || !payload?.succeeded) {
                throw new Error(payload?.message || "The QA review could not be saved. Refresh and try again.");
            }

            applyQaStatus(card, Number(payload.status));
            qaDialog?.close();
            qaCard = null;
            if (qaNotes) qaNotes.value = "";
            if (qaEvidence) qaEvidence.value = "";
            showToast(payload.message || "QA review saved.");
        } catch (error) {
            showToast(error instanceof Error ? error.message : "Network error. The QA review was not saved.", true);
        } finally {
            setPending(card, false);
            if (qaFailSubmit) qaFailSubmit.disabled = false;
            refreshColumns();
        }
    }

    function applySuccessfulStatus(card, targetStatus) {
        card.dataset.status = String(targetStatus);
        const button = card.querySelector("[data-transition-action]");
        if (targetStatus === 2 && button) {
            button.dataset.sourceStatus = "2";
            button.dataset.targetStatus = "3";
            button.textContent = "Submit for review";
            card.dataset.transitionEnabled = "true";
        } else {
            button?.remove();
            card.dataset.transitionEnabled = "false";
        }
    }

    async function moveTask(card, targetStatus) {
        const taskId = Number(card.dataset.taskId);
        const sourceStatus = Number(card.dataset.status);
        if (readOnly || pending.has(String(taskId))) return;
        if (nextStatus(sourceStatus) !== targetStatus) {
            showToast("Only the next workflow step is allowed.", true);
            return;
        }

        const originalList = card.parentElement;
        const originalNext = card.nextElementSibling;
        const targetList = board.querySelector(`[data-board-list="${targetStatus}"]`);
        if (!targetList || !originalList) return;

        targetList.append(card);
        refreshColumns();
        setPending(card, true);

        try {
            const response = await fetch(updateUrl, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": token ?? ""
                },
                body: JSON.stringify({ projectId, sprintId, taskId, currentStatus: sourceStatus, targetStatus })
            });
            let result = null;
            try { result = await response.json(); } catch { /* controlled fallback below */ }
            if (!response.ok || !result?.succeeded) {
                throw new Error(result?.message || "The task could not be moved. Refresh the board and try again.");
            }

            applySuccessfulStatus(card, targetStatus);
            showToast(result.message || "Task status updated.");
        } catch (error) {
            if (originalNext?.parentElement === originalList) {
                originalList.insertBefore(card, originalNext);
            } else {
                originalList.append(card);
            }
            showToast(error instanceof Error ? error.message : "Network error. The task was returned to its original column.", true);
        } finally {
            setPending(card, false);
            refreshColumns();
        }
    }

    board.addEventListener("click", event => {
        const button = event.target.closest("[data-transition-action]");
        if (!button) return;
        const card = button.closest("[data-task-card]");
        if (card) void moveTask(card, Number(button.dataset.targetStatus));
    });

    board.addEventListener("click", event => {
        const button = event.target.closest("[data-qa-action]");
        if (!button) return;
        const card = button.closest("[data-task-card]");
        if (!card || pending.has(card.dataset.taskId)) return;
        if (button.dataset.qaAction === "pass") {
            qaResult = 1;
            if (qaTitle) qaTitle.textContent = "Pass QA review";
        } else {
            qaResult = 2;
            if (qaTitle) qaTitle.textContent = "Fail QA review";
        }

        qaCard = card;
        if (qaNotes) qaNotes.value = "";
        if (qaEvidence) qaEvidence.value = "";
        if (qaNoteError) qaNoteError.textContent = "";
        qaDialog?.showModal();
        qaNotes?.focus();
    });

    qaFailForm?.addEventListener("submit", event => {
        event.preventDefault();
        const notes = qaNotes?.value.trim() ?? "";
        if (qaResult === 2 && !notes) {
            if (qaNoteError) qaNoteError.textContent = "A failure note is required.";
            qaNotes?.focus();
            return;
        }
        if (qaNoteError) qaNoteError.textContent = "";
        if (qaCard) void reviewTask(qaCard, qaResult, notes || null,
            Array.from(qaEvidence?.files ?? []));
    });

    document.querySelector("[data-qa-cancel]")?.addEventListener("click", () => {
        qaDialog?.close();
        qaCard = null;
        if (qaNoteError) qaNoteError.textContent = "";
    });

    board.addEventListener("dragstart", event => {
        const card = event.target.closest("[data-task-card]");
        if (!card || card.dataset.transitionEnabled !== "true" || pending.has(card.dataset.taskId)) {
            event.preventDefault();
            return;
        }
        draggedCard = card;
        card.classList.add("is-dragging");
        event.dataTransfer.effectAllowed = "move";
        event.dataTransfer.setData("text/plain", card.dataset.taskId);
    });

    board.addEventListener("dragover", event => {
        const list = event.target.closest("[data-board-list]");
        if (!list || !draggedCard) return;
        if (nextStatus(Number(draggedCard.dataset.status)) !== Number(list.dataset.boardList)) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = "move";
    });

    board.addEventListener("drop", event => {
        const list = event.target.closest("[data-board-list]");
        if (!list || !draggedCard) return;
        event.preventDefault();
        const card = draggedCard;
        draggedCard = null;
        card.classList.remove("is-dragging");
        void moveTask(card, Number(list.dataset.boardList));
    });

    board.addEventListener("dragend", () => {
        draggedCard?.classList.remove("is-dragging");
        draggedCard = null;
    });

    refreshColumns();
}
