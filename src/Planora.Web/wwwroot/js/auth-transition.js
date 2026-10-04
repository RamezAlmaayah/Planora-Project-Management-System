document.addEventListener("DOMContentLoaded", () => {

    const shell =
        document.querySelector(".auth-v3-shell");

    if (!shell) {
        return;
    }

    const currentPage =
        shell.dataset.authPage;

    const previousPage =
        sessionStorage.getItem("planora-auth-from");


    /* =====================================================
       PAGE ENTER ANIMATION
       ===================================================== */

    if (previousPage &&
        previousPage !== currentPage) {

        shell.classList.add(
            `auth-v3-enter-from-${previousPage}`
        );

        requestAnimationFrame(() => {

            requestAnimationFrame(() => {

                shell.classList.add(
                    "auth-v3-enter-active"
                );

            });

        });

        window.setTimeout(() => {

            shell.classList.remove(
                `auth-v3-enter-from-${previousPage}`,
                "auth-v3-enter-active"
            );

            sessionStorage.removeItem(
                "planora-auth-from"
            );

        }, 650);
    }


    /* =====================================================
       LINK TRANSITION
       ===================================================== */

    const links =
        document.querySelectorAll(
            ".auth-v3-transition-link[data-auth-target]"
        );

    links.forEach(link => {

        link.addEventListener(
            "click",
            event => {

                if (
                    event.ctrlKey ||
                    event.metaKey ||
                    event.shiftKey ||
                    event.altKey
                ) {
                    return;
                }

                event.preventDefault();

                const target =
                    link.dataset.authTarget;

                const href =
                    link.href;

                shell.classList.add(
                    `auth-v3-leave-to-${target}`
                );

                sessionStorage.setItem(
                    "planora-auth-from",
                    currentPage
                );

                window.setTimeout(() => {

                    window.location.href =
                        href;

               }, 560);

            }
        );

    });

});