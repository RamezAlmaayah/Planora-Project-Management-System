(() => {
    if (!window.signalR) return;
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/admin-presence")
        .withAutomaticReconnect()
        .build();
    window.planoraPresenceConnection = connection;
    let touchTimer = 0;
    const touch = () => {
        if (connection.state === signalR.HubConnectionState.Connected)
            connection.invoke("Touch").catch(() => {});
    };
    connection.start().then(() => {
        touchTimer = window.setInterval(touch, 60000);
    }).catch(() => window.clearInterval(touchTimer));
    connection.onreconnected(touch);
    window.addEventListener("pagehide", () => {
        window.clearInterval(touchTimer);
        void connection.stop();
    }, { once: true });
})();
