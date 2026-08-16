let mainChart = null;
let categoryChart = null;
let reportTags = [];

document.addEventListener('DOMContentLoaded', function () {
    initYearSelector();
    initDatePickers();
    loadReportTags();

    document.getElementById('report-type').addEventListener('change', handleReportTypeChange);
    document.getElementById('load-report-btn').addEventListener('click', loadReport);
    document.getElementById('export-csv-btn').addEventListener('click', () => exportData('csv'));

    loadReport();
});

async function loadReportTags() {
    try {
        reportTags = await window.apiClient.get('/api/Tags');
        populateReportTagSelect();
    } catch (error) {
        console.error('[Reports] Failed to load tags:', error);
    }
}

function populateReportTagSelect() {
    const select = document.getElementById('report-tag');
    if (!select) return;

    select.innerHTML = '<option value="">Все категории</option>';
    reportTags.forEach(tag => {
        const opt = document.createElement('option');
        opt.value = tag.id;
        opt.textContent = (tag.icon || '') + ' ' + tag.name;
        select.appendChild(opt);
    });
}

function initYearSelector() {
    const yearSelect = document.getElementById('report-year');
    const currentYear = new Date().getFullYear();
    for (let i = currentYear; i >= currentYear - 5; i--) {
        const option = document.createElement('option');
        option.value = i;
        option.textContent = i;
        yearSelect.appendChild(option);
    }

    const monthSelect = document.getElementById('report-month');
    monthSelect.value = new Date().getMonth() + 1;
}

function initDatePickers() {
    const locale = (typeof flatpickr !== 'undefined' && flatpickr.l10ns && flatpickr.l10ns.ru) ? "ru" : "default";
    flatpickr(".flatpickr", {
        locale: locale,
        dateFormat: "d.m.Y",
        allowInput: true
    });
}

function handleReportTypeChange(e) {
    const type = e.target.value;
    const yearWrap = document.getElementById('year-selector-wrap');
    const monthWrap = document.getElementById('month-selector-wrap');
    const dateFromWrap = document.getElementById('date-from-wrap');
    const dateToWrap = document.getElementById('date-to-wrap');
    const groupByWrap = document.getElementById('group-by-wrap');

    // Сброс видимости
    [yearWrap, monthWrap, dateFromWrap, dateToWrap, groupByWrap].forEach(el => el.style.display = 'none');

    if (type === 'monthly') {
        yearWrap.style.display = 'block';
        monthWrap.style.display = 'block';
    } else if (type === 'yearly') {
        yearWrap.style.display = 'block';
    } else if (type === 'category') {
        dateFromWrap.style.display = 'block';
        dateToWrap.style.display = 'block';

        // Установка дат по умолчанию (текущий месяц)
        const now = new Date();
        const firstDay = new Date(now.getFullYear(), now.getMonth(), 1);
        const lastDay = new Date(now.getFullYear(), now.getMonth() + 1, 0);
        document.getElementById('report-date-from')._flatpickr.setDate(firstDay);
        document.getElementById('report-date-to')._flatpickr.setDate(lastDay);
    } else if (type === 'trend') {
        dateFromWrap.style.display = 'block';
        dateToWrap.style.display = 'block';
        groupByWrap.style.display = 'block';
    }
}

async function loadReport() {
    const type = document.getElementById('report-type').value;
    const btn = document.getElementById('load-report-btn');
    btn.disabled = true;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Загрузка...';

    try {
        let endpoint = '';
        let params = new URLSearchParams();

        if (type === 'monthly') {
            const year = document.getElementById('report-year').value;
            const month = document.getElementById('report-month').value;
            endpoint = `/api/Reports/monthly/${year}/${month}`;
        } else if (type === 'yearly') {
            const year = document.getElementById('report-year').value;
            endpoint = `/api/Reports/yearly/${year}`;
        } else if (type === 'category') {
            const fromVal = document.getElementById('report-date-from').value;
            const toVal = document.getElementById('report-date-to').value;
            if (!fromVal || !toVal) {
                alert('Укажите даты начала и конца периода');
                return;
            }
            const from = parseDate(fromVal);
            const to = parseDate(toVal);
            endpoint = `/api/Reports/category`;
            params.append('startDate', from.toISOString());
            params.append('endDate', to.toISOString());
        } else if (type === 'trend') {
            const fromVal = document.getElementById('report-date-from').value;
            const toVal = document.getElementById('report-date-to').value;
            if (!fromVal || !toVal) {
                alert('Укажите даты начала и конца периода');
                return;
            }
            const from = parseDate(fromVal);
            const to = parseDate(toVal);
            const groupBy = document.getElementById('report-group-by').value;
            endpoint = `/api/Reports/trend`;
            params.append('startDate', from.toISOString());
            params.append('endDate', to.toISOString());
            params.append('groupBy', groupBy);
        }

        const reportTagSelect = document.getElementById('report-tag');
        const selectedTagId = reportTagSelect ? reportTagSelect.value : '';
        if (selectedTagId) {
            params.append('tagIds', selectedTagId);
        }

        const url = `${endpoint}${params.toString() ? '?' + params.toString() : ''}`;
        const data = await window.apiClient.get(url);

        renderReport(type, data);

    } catch (error) {
        console.error('[Reports] Failed to load report:', error);
        alert('Не удалось загрузить данные отчёта. Проверьте соединение с API.');
    } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="fas fa-sync-alt me-1"></i> Загрузить';
    }
}

function renderReport(type, data) {
    updateSummary(type, data);
    renderMainChart(type, data);
    renderCategoryChart(type, data);
    renderDetailTable(type, data);
}

function updateSummary(type, data) {
    let income = 0, expense = 0, balance = 0;
    let currency = '₽';

    if (type === 'monthly' || type === 'yearly') {
        const currencies = data.byCurrency || {};
        const keys = Object.keys(currencies);
        if (keys.length > 0) {
            for (const key of keys) {
                income += currencies[key].totalIncome || 0;
                expense += currencies[key].totalExpense || 0;
                balance += currencies[key].balance || 0;
            }
            if (keys.length === 1 && keys[0] !== 'RUB') {
                currency = keys[0];
            }
        }
    } else if (type === 'category') {
        expense = data.categories?.reduce((acc, c) => acc + c.amount, 0) || 0;
    } else if (type === 'trend') {
        income = data.data?.reduce((acc, d) => acc + d.income, 0) || 0;
        expense = data.data?.reduce((acc, d) => acc + d.expense, 0) || 0;
        balance = income - expense;
    }

    document.getElementById('summary-income').textContent = `${formatMoney(income)} ${currency}`;
    document.getElementById('summary-expense').textContent = `${formatMoney(expense)} ${currency}`;
    document.getElementById('summary-balance').textContent = `${formatMoney(balance)} ${currency}`;
}

function renderMainChart(type, data) {
    const chartTitle = document.getElementById('main-chart-title');
    let options = {
        chart: { type: 'area', height: 350, toolbar: { show: false } },
        colors: ['#00ab55', '#e7515a'],
        dataLabels: { enabled: false },
        stroke: { curve: 'smooth', width: 2 },
        xaxis: { categories: [] },
        series: []
    };

    if (type === 'monthly') {
        const days = data.byDay || [];
        chartTitle.textContent = 'Динамика по дням';
        options.xaxis.categories = days.map(d => new Date(d.date).getDate());
        options.series = [
            { name: 'Доход', data: days.map(d => d.income) },
            { name: 'Расход', data: days.map(d => d.expense) }
        ];
    } else if (type === 'yearly') {
        const months = data.byMonth || [];
        chartTitle.textContent = 'Динамика по месяцам';
        options.xaxis.categories = months.map(m => m.monthName);
        options.series = [
            { name: 'Доход', data: months.map(m => m.income) },
            { name: 'Расход', data: months.map(m => m.expense) }
        ];
    } else if (type === 'trend') {
        const trendData = data.data || [];
        chartTitle.textContent = 'Тренды операций';
        options.xaxis.categories = trendData.map(d => formatDateShort(d.date));
        options.series = [
            { name: 'Доход', data: trendData.map(d => d.income) },
            { name: 'Расход', data: trendData.map(d => d.expense) }
        ];
    }

    if (mainChart) mainChart.destroy();
    if (options.series.length > 0) {
        mainChart = new ApexCharts(document.querySelector("#main-chart"), options);
        mainChart.render();
    } else {
        document.querySelector("#main-chart").innerHTML = '<div class="text-center p-5 text-muted">Нет данных для графика</div>';
    }
}

const CHART_COLORS = [
    '#4361ee', '#e2a03f', '#e7515a', '#2196f3', '#8b5cf6',
    '#00ab55', '#f472b6', '#06b6d4', '#f59e0b', '#6366f1',
    '#ec4899', '#14b8a6', '#f97316', '#a855f7'
];

function renderCategoryChart(type, data) {
    let categories = [];
    if (type === 'category') {
        categories = data.categories || [];
    } else if (type === 'monthly' || type === 'yearly') {
        categories = data.byCategory || [];
    }

    const totalAmount = categories.reduce((acc, c) => acc + c.amount, 0);

    let options = {
        chart: { type: 'donut', height: 420 },
        series: categories.map(c => c.amount),
        labels: categories.map(c => `${c.tagIcon} ${c.tagName}`),
        colors: CHART_COLORS.slice(0, categories.length),
        legend: { show: false },
        dataLabels: {
            enabled: true,
            formatter: function (val) {
                return val.toFixed(0) + '%';
            },
            dropShadow: { enabled: false }
        },
        plotOptions: {
            pie: {
                donut: {
                    size: '75%',
                    labels: {
                        show: true,
                        name: {
                            show: true,
                            fontSize: '14px',
                            color: '#888ea8'
                        },
                        value: {
                            show: true,
                            fontSize: '26px',
                            fontWeight: 700,
                            formatter: function (val) {
                                return formatMoney(parseFloat(val)) + ' ₽';
                            }
                        },
                        total: {
                            show: true,
                            label: 'Всего',
                            fontSize: '14px',
                            formatter: function () {
                                return formatMoney(totalAmount) + ' ₽';
                            }
                        }
                    }
                }
            }
        },
        tooltip: {
            y: {
                formatter: function (val) {
                    return formatMoney(val) + ' ₽';
                }
            }
        },
        responsive: [{
            breakpoint: 576,
            options: { chart: { height: 320 } }
        }]
    };

    if (categoryChart) categoryChart.destroy();
    if (categories.length > 0) {
        categoryChart = new ApexCharts(document.querySelector("#category-donut-chart"), options);
        categoryChart.render();
    } else {
        document.querySelector("#category-donut-chart").innerHTML = '<div class="text-center p-5 text-muted">Нет данных по категориям</div>';
    }

    renderCategoryList(categories, totalAmount);
}

function renderCategoryList(categories, totalAmount) {
    const container = document.getElementById('category-list');
    if (!categories || categories.length === 0) {
        container.innerHTML = '<div class="text-center p-4 text-muted">Нет данных по категориям</div>';
        return;
    }

    let html = '<div class="row g-2">';
    categories.forEach((c, i) => {
        const color = CHART_COLORS[i % CHART_COLORS.length];
        const pct = totalAmount > 0 ? ((c.amount / totalAmount) * 100).toFixed(1) : '0.0';
        html += `
            <div class="col-sm-6">
                <div class="category-item">
                    <div class="category-item__indicator" style="background-color: ${color};"></div>
                    <span class="category-item__icon">${c.tagIcon || ''}</span>
                    <div class="category-item__info">
                        <span class="category-item__name">${c.tagName}</span>
                        <span class="category-item__pct">${pct}%</span>
                    </div>
                    <span class="category-item__amount">${formatMoney(c.amount)} ₽</span>
                </div>
            </div>`;
    });
    html += '</div>';
    container.innerHTML = html;
}

function renderDetailTable(type, data) {
    const head = document.getElementById('detail-table-head');
    const body = document.getElementById('detail-table-body');
    const title = document.getElementById('detail-table-title');

    title.textContent = 'Детализация данных';
    body.innerHTML = '';

    if (type === 'monthly') {
        head.innerHTML = '<tr><th>Дата</th><th>Доходы</th><th>Расходы</th><th>Баланс</th></tr>';
        (data.byDay || []).forEach(d => {
            body.innerHTML += `<tr>
                <td>${formatDateShort(d.date)}</td>
                <td class="text-success">+ ${formatMoney(d.income)}</td>
                <td class="text-danger">- ${formatMoney(d.expense)}</td>
                <td>${formatMoney(d.balance)}</td>
            </tr>`;
        });
    } else if (type === 'yearly') {
        head.innerHTML = '<tr><th>Месяц</th><th>Доходы</th><th>Расходы</th><th>Баланс</th></tr>';
        (data.byMonth || []).forEach(m => {
            body.innerHTML += `<tr>
                <td>${m.monthName}</td>
                <td class="text-success">+ ${formatMoney(m.income)}</td>
                <td class="text-danger">- ${formatMoney(m.expense)}</td>
                <td>${formatMoney(m.balance)}</td>
            </tr>`;
        });
    } else if (type === 'category') {
        head.innerHTML = '<tr><th>Категория</th><th>Сумма</th><th>Доля</th><th>Операций</th></tr>';
        (data.categories || []).forEach(c => {
            body.innerHTML += `<tr>
                <td>${c.tagIcon} ${c.tagName}</td>
                <td class="fw-bold">${formatMoney(c.amount)} ${c.currency}</td>
                <td>
                    <div class="progress br-30" style="height: 8px; width: 100px;">
                        <div class="progress-bar bg-primary" style="width: ${c.percentage}%"></div>
                    </div>
                    <small>${c.percentage.toFixed(1)}%</small>
                </td>
                <td>${c.count}</td>
            </tr>`;
        });
    } else if (type === 'trend') {
        head.innerHTML = '<tr><th>Период</th><th>Доходы</th><th>Расходы</th><th>Баланс</th></tr>';
        (data.data || []).forEach(d => {
            body.innerHTML += `<tr>
                <td>${formatDateShort(d.date)}</td>
                <td class="text-success">+ ${formatMoney(d.income)}</td>
                <td class="text-danger">- ${formatMoney(d.expense)}</td>
                <td>${formatMoney(d.balance)}</td>
            </tr>`;
        });
    }
}

async function exportData(format) {
    const type = document.getElementById('report-type').value;
    let startDate, endDate;

    if (type === 'monthly') {
        const year = document.getElementById('report-year').value;
        const month = document.getElementById('report-month').value;
        startDate = new Date(year, month - 1, 1).toISOString();
        endDate = new Date(year, month, 0).toISOString();
    } else if (type === 'yearly') {
        const year = document.getElementById('report-year').value;
        startDate = new Date(year, 0, 1).toISOString();
        endDate = new Date(year, 11, 31).toISOString();
    } else {
        const fromVal = document.getElementById('report-date-from').value;
        const toVal = document.getElementById('report-date-to').value;
        if (!fromVal || !toVal) {
            alert('Укажите даты для экспорта');
            return;
        }
        startDate = parseDate(fromVal).toISOString();
        endDate = parseDate(toVal).toISOString();
    }

    const endpoint = `/api/Reports/export/${format}?startDate=${startDate}&endDate=${endDate}`;

    try {
        const response = await fetch(`${window.apiClient.baseUrl}${endpoint}`, {
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' }
        });
        if (!response.ok) throw new Error('Export failed');

        const blob = await response.blob();
        const downloadUrl = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = downloadUrl;
        a.download = `report_${format === 'csv' ? 'data.csv' : 'data.xlsx'}`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        window.URL.revokeObjectURL(downloadUrl);
    } catch (error) {
        alert('Ошибка при экспорте: ' + error.message);
    }
}

// Вспомогательные функции
function formatMoney(amount) {
    return new Intl.NumberFormat('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(amount);
}

function formatDateShort(dateStr) {
    return new Date(dateStr).toLocaleDateString('ru-RU', { day: '2-digit', month: '2-digit' });
}

function parseDate(str) {
    const parts = str.split('.');
    return new Date(parts[2], parts[1] - 1, parts[0]);
}