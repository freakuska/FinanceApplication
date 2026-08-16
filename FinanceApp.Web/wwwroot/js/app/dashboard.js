let currentEditingOperationId = null;
let allTags = [];
const TEMPLATES_KEY = 'finance_quick_templates';

document.addEventListener('DOMContentLoaded', function () {
    loadAllTags();

    const today = new Date().toISOString().split('T')[0];
    document.getElementById('operation-date').value = today;

    loadDashboardData();
    loadOperations();
    renderQuickTemplates();

    document.getElementById('operation-form').addEventListener('submit', handleOperationSubmit);
    document.getElementById('cancel-edit-btn').addEventListener('click', cancelEdit);
    document.getElementById('save-operation-modal-btn').addEventListener('click', saveOperationFromModal);
    document.getElementById('delete-operation-modal-btn').addEventListener('click', deleteOperationFromModal);
    document.getElementById('save-template-btn').addEventListener('click', saveTemplate);
});

async function loadAllTags() {
    try {
        allTags = await window.apiClient.get('/api/Tags');
        populateTagSelect('operation-tag', '');
    } catch (error) {
        if (error.isNetworkError) return;
        console.error('[Dashboard] Failed to load tags:', error);
    }
}

function populateTagSelect(selectId, selectedTagId) {
    const select = document.getElementById(selectId);
    if (!select) return;

    select.innerHTML = '<option value="">Без категории</option>';
    allTags.forEach(tag => {
        const opt = document.createElement('option');
        opt.value = tag.id;
        opt.textContent = (tag.icon || '') + ' ' + tag.name;
        if (tag.id === selectedTagId) opt.selected = true;
        select.appendChild(opt);
    });
}

async function loadDashboardData() {
    try {
        const now = new Date();
        const startDate = new Date(now.getFullYear(), now.getMonth(), 1).toISOString();
        const endDate = new Date(now.getFullYear(), now.getMonth() + 1, 0).toISOString();

        const stats = await window.apiClient.get(
            `/api/Operations/stats?startDate=${startDate}&endDate=${endDate}`
        );

        if (stats && stats.RUB) {
            document.getElementById('total-income').textContent = `${formatMoney(stats.RUB.totalIncome)} ₽`;
            document.getElementById('total-expense').textContent = `${formatMoney(stats.RUB.totalExpense)} ₽`;
            document.getElementById('total-balance').textContent = `${formatMoney(stats.RUB.balance)} ₽`;
        } else {
            document.getElementById('total-income').textContent = '0.00 ₽';
            document.getElementById('total-expense').textContent = '0.00 ₽';
            document.getElementById('total-balance').textContent = '0.00 ₽';
        }
    } catch (error) {
        if (error.isNetworkError) return;
        console.error('[Dashboard] Failed to load stats:', error);
    }
}

async function loadOperations() {
    const tbody = document.getElementById('operations-list');

    try {
        const result = await window.apiClient.get('/api/Operations?page=1&pageSize=20');
        const operations = result.items || [];

        if (operations.length === 0) {
            tbody.innerHTML = '<tr><td colspan="6" class="text-center text-muted">Нет операций. Добавьте первую операцию выше!</td></tr>';
            return;
        }

        tbody.innerHTML = operations.map(op => createOperationRow(op)).join('');

        tbody.querySelectorAll('.edit-operation-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                openEditOperationModal(btn.getAttribute('data-operation-id'));
            });
        });

        tbody.querySelectorAll('.delete-operation-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                deleteOperation(btn.getAttribute('data-operation-id'));
            });
        });
    } catch (error) {
        if (error.isNetworkError) return;
        console.error('[Dashboard] Failed to load operations:', error);
        tbody.innerHTML = '<tr><td colspan="6" class="text-center text-danger">Ошибка загрузки</td></tr>';
    }
}

function createOperationRow(operation) {
    const isIncome = operation.type === 'Income';
    const typeClass = isIncome ? 'income' : 'expense';
    const typeIcon = isIncome ? '<i class="fas fa-arrow-up"></i>' : '<i class="fas fa-arrow-down"></i>';
    const date = new Date(operation.operationDateTime).toLocaleDateString('ru-RU');
    const paymentMethodClass = getPaymentMethodClass(operation.paymentMethod);

    const tagsHtml = (operation.tags && operation.tags.length > 0)
        ? operation.tags.map(t => `<span class="badge bg-light text-dark me-1" style="font-size:11px;">${t.icon || ''} ${t.name}</span>`).join('')
        : '';

    return `
        <tr data-operation-id="${operation.id}" data-operation='${JSON.stringify(operation).replace(/'/g, "&apos;")}'>
            <td><div class="td-content operation-date">${date}</div></td>
            <td>
                <div class="td-content">
                    <span class="operation-type-badge ${typeClass}">
                        ${typeIcon}
                        <span>${getTypeLabel(operation.type)}</span>
                    </span>
                </div>
            </td>
            <td>
                <div class="td-content operation-description ${operation.description ? '' : 'empty'}">
                    ${operation.description || 'Без описания'}
                    ${tagsHtml ? '<div class="mt-1">' + tagsHtml + '</div>' : ''}
                </div>
            </td>
            <td>
                <div class="td-content">
                    <span class="payment-method-badge ${paymentMethodClass}">
                        ${getPaymentMethodLabel(operation.paymentMethod)}
                    </span>
                </div>
            </td>
            <td>
                <div class="td-content">
                    <span class="operation-amount ${typeClass}">
                        ${formatMoney(operation.money.amount)} ${operation.money.currency}
                    </span>
                </div>
            </td>
            <td>
                <div class="td-content">
                    <div class="operation-actions">
                        <button class="operation-action-btn edit-btn edit-operation-btn"
                                data-operation-id="${operation.id}"
                                title="Редактировать">
                            <i class="fas fa-edit"></i>
                        </button>
                        <button class="operation-action-btn delete-btn delete-operation-btn"
                                data-operation-id="${operation.id}"
                                title="Удалить">
                            <i class="fas fa-trash"></i>
                        </button>
                    </div>
                </div>
            </td>
        </tr>
    `;
}

async function handleOperationSubmit(e) {
    e.preventDefault();

    const type = parseInt(document.getElementById('operation-type').value);
    const amount = parseFloat(document.getElementById('operation-amount').value);
    const paymentMethod = parseInt(document.getElementById('operation-payment').value);
    const date = document.getElementById('operation-date').value;
    const description = document.getElementById('operation-description').value.trim();
    const selectedTagId = document.getElementById('operation-tag').value;

    if (!amount || amount <= 0) {
        showNotification('Введите корректную сумму', 'warning');
        return;
    }

    const operationData = {
        type, amount, currency: 'RUB', paymentMethod,
        operationDateTime: new Date(date).toISOString(),
        description: description || null,
        tagIds: selectedTagId ? [selectedTagId] : []
    };

    try {
        if (currentEditingOperationId) {
            await window.apiClient.put(`/api/Operations/${currentEditingOperationId}`, operationData);
        } else {
            await window.apiClient.post('/api/Operations', operationData);
        }

        showNotification(
            currentEditingOperationId ? 'Операция обновлена' : 'Операция добавлена',
            'success'
        );

        document.getElementById('operation-form').reset();
        document.getElementById('operation-date').value = new Date().toISOString().split('T')[0];
        document.getElementById('operation-tag').value = '';
        currentEditingOperationId = null;

        const submitBtn = document.querySelector('#operation-form button[type="submit"]');
        submitBtn.innerHTML = '<i class="fas fa-plus me-2"></i> Добавить';
        submitBtn.className = 'btn btn-success';
        document.getElementById('cancel-edit-btn').style.display = 'none';

        await loadDashboardData();
        await loadOperations();
    } catch (error) {
        console.error('[Dashboard] Failed to save operation:', error);
        showNotification(error.message, 'error');
    }
}

async function openEditOperationModal(operationId) {
    try {
        await loadAllTags();
        const operation = await window.apiClient.get(`/api/Operations/${operationId}`);

        document.getElementById('edit-operation-type').value = getTypeValue(operation.type);
        document.getElementById('edit-operation-amount').value = operation.money.amount;
        document.getElementById('edit-operation-payment').value = getPaymentMethodValue(operation.paymentMethod);

        const date = new Date(operation.operationDateTime);
        document.getElementById('edit-operation-date').value = date.toISOString().split('T')[0];
        document.getElementById('edit-operation-description').value = operation.description || '';
        document.getElementById('edit-operation-notes').value = operation.notes || '';

        const currentTagId = (operation.tags && operation.tags.length > 0) ? operation.tags[0].id : '';
        populateTagSelect('edit-operation-tag', currentTagId);

        currentEditingOperationId = operationId;

        const modal = new bootstrap.Modal(document.getElementById('editOperationModal'));
        modal.show();
    } catch (error) {
        console.error('[Dashboard] Failed to load operation for editing:', error);
        showNotification('Ошибка загрузки операции', 'error');
    }
}

async function deleteOperation(operationId) {
    if (!confirm('Вы уверены, что хотите удалить эту операцию?')) return;

    try {
        await window.apiClient.delete(`/api/Operations/${operationId}`);
        showNotification('Операция удалена', 'success');
        await loadDashboardData();
        await loadOperations();
    } catch (error) {
        console.error('[Dashboard] Failed to delete operation:', error);
        showNotification(error.message, 'error');
    }
}

function getTypeValue(typeString) {
    return { 'Income': 0, 'Expense': 1 }[typeString] || 0;
}

function getTypeLabel(typeString) {
    return { 'Income': 'Доход', 'Expense': 'Расход', 'Transfer': 'Перевод' }[typeString] || typeString;
}

function getPaymentMethodValue(methodString) {
    return { 'Cash': 0, 'Card': 1, 'BankTransfer': 2 }[methodString] || 0;
}

function getPaymentMethodLabel(methodString) {
    return { 'Cash': 'Наличные', 'Card': 'Карта', 'BankTransfer': 'Перевод' }[methodString] || methodString;
}

function getPaymentMethodClass(methodString) {
    return { 'Cash': 'cash', 'Card': 'card', 'BankTransfer': 'transfer' }[methodString] || 'card';
}

function formatMoney(amount) {
    return new Intl.NumberFormat('ru-RU', {
        minimumFractionDigits: 2, maximumFractionDigits: 2
    }).format(amount);
}

function cancelEdit() {
    document.getElementById('operation-form').reset();
    document.getElementById('operation-date').value = new Date().toISOString().split('T')[0];
    document.getElementById('operation-tag').value = '';
    currentEditingOperationId = null;

    const submitBtn = document.querySelector('#operation-form button[type="submit"]');
    submitBtn.innerHTML = '<i class="fas fa-plus me-2"></i> Добавить';
    submitBtn.className = 'btn btn-success';
    document.getElementById('cancel-edit-btn').style.display = 'none';

    showNotification('Редактирование отменено', 'info');
}

async function saveOperationFromModal() {
    if (!currentEditingOperationId) {
        showNotification('Ошибка: ID операции не найден', 'error');
        return;
    }

    const type = parseInt(document.getElementById('edit-operation-type').value);
    const amount = parseFloat(document.getElementById('edit-operation-amount').value);
    const paymentMethod = parseInt(document.getElementById('edit-operation-payment').value);
    const date = document.getElementById('edit-operation-date').value;
    const description = document.getElementById('edit-operation-description').value.trim();
    const notes = document.getElementById('edit-operation-notes').value.trim();
    const selectedTagId = document.getElementById('edit-operation-tag').value;

    if (!amount || amount <= 0) {
        showNotification('Введите корректную сумму', 'warning');
        return;
    }

    const operationData = {
        type, amount, currency: 'RUB', paymentMethod,
        operationDateTime: new Date(date).toISOString(),
        description: description || null,
        notes: notes || null,
        tagIds: selectedTagId ? [selectedTagId] : []
    };

    try {
        await window.apiClient.put(`/api/Operations/${currentEditingOperationId}`, operationData);
        showNotification('Операция успешно обновлена', 'success');

        const modal = bootstrap.Modal.getInstance(document.getElementById('editOperationModal'));
        modal.hide();

        currentEditingOperationId = null;

        await loadAllTags();
        await loadDashboardData();
        await loadOperations();
    } catch (error) {
        console.error('[Dashboard] Failed to update operation:', error);
        showNotification(error.message, 'error');
    }
}

async function deleteOperationFromModal() {
    if (!currentEditingOperationId) {
        showNotification('Ошибка: ID операции не найден', 'error');
        return;
    }

    if (!confirm('Вы действительно хотите удалить эту операцию?')) return;

    try {
        await window.apiClient.delete(`/api/Operations/${currentEditingOperationId}`);
        showNotification('Операция удалена', 'success');

        const modal = bootstrap.Modal.getInstance(document.getElementById('editOperationModal'));
        modal.hide();

        currentEditingOperationId = null;

        await loadDashboardData();
        await loadOperations();
    } catch (error) {
        console.error('[Dashboard] Failed to delete operation:', error);
        showNotification(error.message, 'error');
    }
}

function getTemplates() {
    try {
        return JSON.parse(localStorage.getItem(TEMPLATES_KEY)) || [];
    } catch { return []; }
}

function saveTemplates(templates) {
    localStorage.setItem(TEMPLATES_KEY, JSON.stringify(templates));
}

function renderQuickTemplates() {
    const container = document.getElementById('quick-templates-container');
    if (!container) return;

    const templates = getTemplates();
    let html = `
        <div class="col-xl-3 col-lg-4 col-md-6 col-sm-6 col-12">
            <div class="quick-action-card qa-add-new" id="qa-add-template">
                <div class="quick-action-card__icon">
                    <i class="fas fa-plus"></i>
                </div>
                <div class="quick-action-card__body">
                    <h6 class="quick-action-card__title">Добавить шаблон</h6>
                    <p class="quick-action-card__desc">Создать быструю операцию</p>
                </div>
            </div>
        </div>`;

    templates.forEach(tpl => {
        const isIncome = tpl.type === 0;
        const modifier = isIncome ? 'income' : 'expense';
        const typeLabel = isIncome ? 'Доход' : 'Расход';
        const tagName = getTagNameById(tpl.tagId);

        html += `
            <div class="col-xl-3 col-lg-4 col-md-6 col-sm-6 col-12">
                <div class="quick-action-card quick-action--${modifier} qa-template" data-tpl-id="${tpl.id}">
                    <button class="qa-template__delete" data-tpl-id="${tpl.id}" title="Удалить шаблон">
                        <i class="fas fa-times"></i>
                    </button>
                    <div class="quick-action-card__icon">
                        <i class="fas fa-arrow-${isIncome ? 'up' : 'down'}"></i>
                    </div>
                    <div class="quick-action-card__body">
                        <h6 class="quick-action-card__title">${escapeHtml(tpl.name)}</h6>
                        <p class="quick-action-card__desc">
                            ${formatMoney(tpl.amount)} ₽ &middot; ${typeLabel}${tagName ? ' &middot; ' + escapeHtml(tagName) : ''}
                        </p>
                    </div>
                </div>
            </div>`;
    });

    container.innerHTML = html;

    container.querySelectorAll('.qa-template').forEach(card => {
        card.addEventListener('click', (e) => {
            if (e.target.closest('.qa-template__delete')) return;
            executeTemplate(card.dataset.tplId);
        });
    });

    container.querySelectorAll('.qa-template__delete').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            deleteTemplate(btn.dataset.tplId);
        });
    });

    document.getElementById('qa-add-template').addEventListener('click', openTemplateModal);
}

function getTagNameById(tagId) {
    if (!tagId) return '';
    const tag = allTags.find(t => t.id === tagId);
    return tag ? tag.name : '';
}

function escapeHtml(str) {
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
}

function openTemplateModal() {
    populateTagSelect('tpl-tag', '');
    document.getElementById('template-form').reset();
    const modal = new bootstrap.Modal(document.getElementById('templateModal'));
    modal.show();
}

function saveTemplate() {
    const name = document.getElementById('tpl-name').value.trim();
    const amount = parseFloat(document.getElementById('tpl-amount').value);

    if (!name) { showNotification('Введите название шаблона', 'warning'); return; }
    if (!amount || amount <= 0) { showNotification('Введите корректную сумму', 'warning'); return; }

    const tpl = {
        id: Date.now().toString(36) + Math.random().toString(36).slice(2, 6),
        name,
        type: parseInt(document.getElementById('tpl-type').value),
        amount,
        paymentMethod: parseInt(document.getElementById('tpl-payment').value),
        tagId: document.getElementById('tpl-tag').value || null,
        description: document.getElementById('tpl-description').value.trim() || null
    };

    const templates = getTemplates();
    templates.push(tpl);
    saveTemplates(templates);

    const modal = bootstrap.Modal.getInstance(document.getElementById('templateModal'));
    modal.hide();

    renderQuickTemplates();
    showNotification('Шаблон сохранён', 'success');
}

function deleteTemplate(id) {
    if (!confirm('Удалить этот шаблон?')) return;
    const templates = getTemplates().filter(t => t.id !== id);
    saveTemplates(templates);
    renderQuickTemplates();
    showNotification('Шаблон удалён', 'success');
}

async function executeTemplate(id) {
    const tpl = getTemplates().find(t => t.id === id);
    if (!tpl) return;

    const operationData = {
        type: tpl.type,
        amount: tpl.amount,
        currency: 'RUB',
        paymentMethod: tpl.paymentMethod,
        operationDateTime: new Date().toISOString(),
        description: tpl.description || tpl.name,
        tagIds: tpl.tagId ? [tpl.tagId] : []
    };

    try {
        await window.apiClient.post('/api/Operations', operationData);
        showNotification(`${tpl.name}: ${formatMoney(tpl.amount)} ₽ — добавлено`, 'success');
        await loadDashboardData();
        await loadOperations();
    } catch (error) {
        console.error('[Dashboard] Failed to execute template:', error);
        showNotification('Не удалось создать операцию', 'error');
    }
}

function showNotification(message, type = 'info') {
    const alertClass = type === 'success' ? 'alert-success' :
        type === 'error' ? 'alert-danger' :
            type === 'warning' ? 'alert-warning' : 'alert-info';

    const notification = document.createElement('div');
    notification.className = `alert ${alertClass} alert-dismissible fade show position-fixed`;
    notification.style.cssText = 'top: 20px; right: 20px; z-index: 9999; min-width: 300px;';
    notification.innerHTML = `
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;

    document.body.appendChild(notification);
    setTimeout(() => notification.remove(), 3000);
}
