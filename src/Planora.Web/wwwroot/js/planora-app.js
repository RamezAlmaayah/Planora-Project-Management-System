document.addEventListener(
    "DOMContentLoaded",
    () => {
        const sidebar =
            document.getElementById(
                "workspaceSidebar");

        const backdrop =
            document.getElementById(
                "sidebarBackdrop");

        const toggle =
            document.getElementById(
                "sidebarToggle");

        if (!sidebar || !backdrop || !toggle) {
            return;
        }

        function closeSidebar() {
            sidebar.classList.remove(
                "is-open");

            backdrop.classList.remove(
                "visible");

            toggle.setAttribute(
                "aria-expanded",
                "false");
            toggle.setAttribute(
                "aria-label",
                "Open navigation");
        }

        toggle.addEventListener(
            "click",
            () => {
                const isOpen =
                    sidebar.classList.toggle(
                        "is-open");

                backdrop.classList.toggle(
                    "visible",
                    isOpen);

                toggle.setAttribute(
                    "aria-expanded",
                    isOpen ? "true" : "false");
                toggle.setAttribute(
                    "aria-label",
                    isOpen ? "Close navigation" : "Open navigation");
            });

        backdrop.addEventListener(
            "click",
            closeSidebar);

        sidebar.addEventListener(
            "click",
            event => {
                if (window.innerWidth <= 920 &&
                    event.target.closest("a")) {
                    closeSidebar();
                }
            });

        document.addEventListener(
            "keydown",
            event => {
                if (event.key === "Escape") {
                    closeSidebar();
                }
            });

        window.addEventListener(
            "resize",
            () => {
                if (window.innerWidth > 920) {
                    closeSidebar();
                }
            });
    });
