document.addEventListener(
    "DOMContentLoaded",
    () => {
        initializeProjectSelector();
        initializeMiniChart();
        initializeSegmentedGauge();
    });

function initializeProjectSelector() {
    const selector =
        document.getElementById(
            "projectSelector");

    const form =
        document.getElementById(
            "projectSelectorForm");

    if (!selector || !form) {
        return;
    }

    selector.addEventListener(
        "change",
        () => {
            form.submit();
        });
}

function initializeMiniChart() {
    const chart =
        document.getElementById(
            "miniTaskChart");

    if (!chart) {
        return;
    }

    const values = [
        Number(chart.dataset.todo ?? 0),
        Number(chart.dataset.progress ?? 0),
        Number(chart.dataset.review ?? 0),
        Number(chart.dataset.done ?? 0),
        Number(chart.dataset.issues ?? 0)
    ];

    const maximum =
        Math.max(
            ...values,
            1);

    const bars =
        chart.querySelectorAll(
            ".mini-chart-bar span");

    bars.forEach(
        (bar, index) => {
            const value =
                values[index] ?? 0;

            const height =
                15
                + (
                    value
                    / maximum
                    * 85
                );

            bar.style.height =
                `${height}%`;
        });
}

function initializeSegmentedGauge() {
    const gauge =
        document.querySelector(
            ".half-gauge");

    if (!gauge) {
        return;
    }

    const lines =
        gauge.querySelector(
            ".half-gauge-lines");

    if (!lines) {
        return;
    }

    let progress =
        Number(
            gauge.dataset.progress
            ?? 0);

    if (!Number.isFinite(progress)) {
        progress = 0;
    }

    progress =
        Math.max(
            0,
            Math.min(
                100,
                progress));

    lines.innerHTML = "";

    const totalTicks = 39;

    for (
        let index = 0;
        index < totalTicks;
        index++
    ) {
        const tick =
            document.createElement(
                "span");

        tick.classList.add(
            "gauge-tick");

        const angle =
            -90
            + (
                180
                * index
                / (totalTicks - 1)
            );

        tick.style.setProperty(
            "--angle",
            `${angle}deg`);

        const tickPercentage =
            index
            / (totalTicks - 1)
            * 100;

        if (tickPercentage <= progress) {
            tick.classList.add(
                "is-active");
        }

        lines.appendChild(
            tick);
    }
}