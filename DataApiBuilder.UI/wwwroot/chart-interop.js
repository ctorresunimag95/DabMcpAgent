const charts = new Map();
const palette = ['#6366f1', '#0ea5e9', '#14b8a6', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899'];

window.renderAnalysisChart = (canvasId, chartType, labels, datasets, metricLabel) => {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    charts.get(canvasId)?.destroy();

    // A doughnut chart communicates a single distribution. Use bars for multi-series comparisons.
    const type = chartType === 'doughnut' && datasets.length === 1 ? 'doughnut' : chartType;
    const perLabel = type === 'doughnut' || type === 'pie';
    const chartDatasets = datasets.map((dataset, index) => ({
        label: dataset.label,
        data: dataset.data,
        backgroundColor: perLabel
            ? labels.map((_, labelIndex) => palette[labelIndex % palette.length])
            : palette[index % palette.length],
        borderColor: palette[index % palette.length],
        borderWidth: type === 'line' ? 2 : 0,
        borderRadius: type === 'bar' ? 4 : 0,
        tension: type === 'line' ? .25 : 0,
        fill: false,
    }));

    const chart = new Chart(canvas, {
        type,
        data: {
            labels,
            datasets: chartDatasets,
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: perLabel || datasets.length > 1, position: 'bottom' },
                tooltip: { callbacks: { label: context => `${context.dataset.label}: ${context.formattedValue}` } },
            },
            scales: perLabel ? {} : {
                y: { beginAtZero: true, title: { display: true, text: metricLabel } },
            },
        },
    });
    charts.set(canvasId, chart);
};
