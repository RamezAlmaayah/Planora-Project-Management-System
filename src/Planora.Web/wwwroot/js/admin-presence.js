(() => {
    const root = document.querySelector("[data-admin-presence]");
    if (!root) return;
    const rows = root.querySelector("[data-presence-rows]");
    const status = root.querySelector("[data-presence-connection]");
    const snapshotUrl = root.dataset.snapshotUrl;
    let fallbackTimer = 0;

    function render(people) {
        const frag = document.createDocumentFragment();
        for (const person of people) {
            const row = document.createElement("tr");
            for (const value of [person.name, person.role, person.online ? "Online" : "Offline",
                person.online ? "—" : person.lastSeenAt ? new Date(person.lastSeenAt).toLocaleString() : "Never"]) {
                const cell = document.createElement("td");
                cell.textContent = value ?? "";
                row.append(cell);
            }
            frag.append(row);
        }
        rows.replaceChildren(frag);
    }

    async function refresh() {
        try {
            const response = await fetch(snapshotUrl, { credentials: "same-origin", cache: "no-store" });
            if (response.ok) render(await response.json());
        } catch { /* The previous snapshot remains visible during transient failures. */ }
    }

    window.setInterval(refresh, 30000);
    if (!window.signalR || !window.planoraPresenceConnection) {
        status.textContent = "Live updates unavailable; refreshing this snapshot every 30 seconds.";
        void refresh();
        return;
    }
    const connection = window.planoraPresenceConnection;
    connection.on("PresenceChanged", person => {
        const match = Array.from(rows.rows).filter(row => row.cells[0].textContent === person.name && row.cells[1].textContent === person.role);
        for (const row of match) {
            row.cells[2].textContent = person.online ? "Online" : "Offline";
            row.cells[3].textContent = person.online ? "—" : person.lastSeenAt ? new Date(person.lastSeenAt).toLocaleString() : "Never";
        }
    });
    connection.onreconnecting(() => { status.textContent = "Live updates interrupted; refreshing the snapshot."; });
    connection.onreconnected(async () => {
        status.textContent = "Live updates connected.";
        try { render(await connection.invoke("GetSnapshot")); } catch { await refresh(); }
    });
    connection.start().then(async () => {
        status.textContent = "Live updates connected.";
        render(await connection.invoke("GetSnapshot"));
    }).catch(() => {
        status.textContent = "Live updates unavailable; refreshing this snapshot every 30 seconds.";
        void refresh();
    });
})();
