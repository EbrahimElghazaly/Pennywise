/**
 * Pennywise Transactions Module (Full CRUD, Search, Filter & Pagination)
 */
const Transactions = {
  currentPage: 1,
  pageSize: 10,
  totalPages: 1,
  categories: [],
  editingId: null,

  async init() {
    Navigation.init('transactions');

    // Parse URL query params if any (e.g. ?search=xyz)
    const urlParams = new URLSearchParams(window.location.search);
    const searchParam = urlParams.get('search');
    if (searchParam) {
      const searchInput = document.getElementById('txSearchInput');
      if (searchInput) searchInput.value = searchParam;
    }

    await this.loadCategories();
    this.setupEventListeners();
    await this.loadTransactions();
  },

  async loadCategories() {
    try {
      const res = await API.get('/categories');
      if (res.success && res.data) {
        this.categories = res.data;
        this.populateCategorySelects();
      }
    } catch (err) {
      console.error('Error loading categories:', err);
    }
  },

  populateCategorySelects() {
    const filterCatSelect = document.getElementById('txCategoryFilter');
    if (filterCatSelect) {
      filterCatSelect.innerHTML = '<option value="all">All Categories</option>' +
        this.categories.map(c => `<option value="${c.id}">${Utils.escapeHtml(c.name)} (${c.type})</option>`).join('');
    }

    this.updateModalCategories();
  },

  updateModalCategories() {
    const modalTypeSelect = document.getElementById('txModalType');
    const modalCatSelect = document.getElementById('txModalCategory');
    if (!modalCatSelect) return;

    const selectedType = modalTypeSelect ? modalTypeSelect.value : 'expense';
    const filtered = this.categories.filter(c => c.type === selectedType);

    modalCatSelect.innerHTML = filtered.map(c => `
      <option value="${c.id}">${Utils.escapeHtml(c.name)}</option>
    `).join('');
  },

  setupEventListeners() {
    // Search & Filters
    document.getElementById('txSearchInput')?.addEventListener('input', () => {
      this.currentPage = 1;
      this.loadTransactions();
    });

    document.getElementById('txTypeFilter')?.addEventListener('change', () => {
      this.currentPage = 1;
      this.loadTransactions();
    });

    document.getElementById('txCategoryFilter')?.addEventListener('change', () => {
      this.currentPage = 1;
      this.loadTransactions();
    });

    document.getElementById('txMonthFilter')?.addEventListener('change', () => {
      this.currentPage = 1;
      this.loadTransactions();
    });

    // Add Transaction Button
    document.getElementById('openAddTxBtn')?.addEventListener('click', () => {
      this.openModal();
    });

    // Modal Type Change (filters category select)
    document.getElementById('txModalType')?.addEventListener('change', () => {
      this.updateModalCategories();
    });

    // Form Submit
    document.getElementById('txForm')?.addEventListener('submit', (e) => {
      e.preventDefault();
      this.saveTransaction();
    });

    // Modal Close
    document.getElementById('closeTxModalBtn')?.addEventListener('click', () => {
      Utils.closeModal('txModal');
    });
    document.getElementById('cancelTxModalBtn')?.addEventListener('click', () => {
      Utils.closeModal('txModal');
    });
  },

  async loadTransactions() {
    const search = document.getElementById('txSearchInput')?.value.trim();
    const type = document.getElementById('txTypeFilter')?.value;
    const categoryId = document.getElementById('txCategoryFilter')?.value;
    const monthFilter = document.getElementById('txMonthFilter')?.value;

    let startDate = '';
    let endDate = '';

    const now = new Date();
    if (monthFilter === 'this_month') {
      startDate = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-01`;
      const days = new Date(now.getFullYear(), now.getMonth() + 1, 0).getDate();
      endDate = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${days}`;
    } else if (monthFilter === 'last_month') {
      const prev = new Date(now.getFullYear(), now.getMonth() - 1, 1);
      startDate = `${prev.getFullYear()}-${String(prev.getMonth() + 1).padStart(2, '0')}-01`;
      const days = new Date(prev.getFullYear(), prev.getMonth() + 1, 0).getDate();
      endDate = `${prev.getFullYear()}-${String(prev.getMonth() + 1).padStart(2, '0')}-${days}`;
    }

    let url = `/transactions?page=${this.currentPage}&pageSize=${this.pageSize}`;
    if (search) url += `&search=${encodeURIComponent(search)}`;
    if (type && type !== 'all') url += `&type=${encodeURIComponent(type)}`;
    if (categoryId && categoryId !== 'all') url += `&categoryId=${encodeURIComponent(categoryId)}`;
    if (startDate) url += `&startDate=${startDate}&endDate=${endDate}`;

    try {
      const res = await API.get(url);
      if (res.success) {
        this.renderTable(res.data);
        this.renderTotals(res.totals);
        this.renderPagination(res.pagination);
      }
    } catch (err) {
      console.error('Error loading transactions:', err);
      Utils.showToast('Failed to load transactions', 'error');
    }
  },

  renderTable(items) {
    const tbody = document.getElementById('txTableBody');
    if (!tbody) return;

    if (!items || items.length === 0) {
      tbody.innerHTML = `
        <tr>
          <td colspan="6" style="text-align:center; padding:3rem; color:var(--text-secondary);">
            <div class="empty-state">
              <div class="empty-state-icon">${Utils.getIconSvg('transactions')}</div>
              <h3 class="empty-state-title">No transactions found</h3>
              <p class="empty-state-desc">Try adjusting your filters or record a new transaction.</p>
              <button class="btn btn-primary btn-sm" onclick="Transactions.openModal()">+ Add Transaction</button>
            </div>
          </td>
        </tr>
      `;
      return;
    }

    tbody.innerHTML = items.map(tx => `
      <tr>
        <td>${Utils.formatDate(tx.date)}</td>
        <td style="font-weight:600;">
          ${Utils.escapeHtml(tx.title)}
          ${tx.notes ? `<div style="font-size:0.75rem; color:var(--text-secondary); font-weight:normal;">${Utils.escapeHtml(tx.notes)}</div>` : ''}
        </td>
        <td>
          <span class="badge ${tx.type === 'income' ? 'badge-income' : 'badge-expense'}">
            ${tx.type}
          </span>
        </td>
        <td>
          <div class="tx-category-cell">
            <span class="tx-cat-icon" style="background-color: ${tx.categoryColor || '#165E34'};">
              ${Utils.getIconSvg(tx.categoryIcon || 'tag')}
            </span>
            <span>${Utils.escapeHtml(tx.categoryName)}</span>
          </div>
        </td>
        <td class="tx-amount ${tx.type === 'income' ? 'income' : 'expense'}">
          ${tx.type === 'income' ? '+' : '-'}${Utils.formatCurrency(tx.amount)}
        </td>
        <td>
          <div class="action-buttons">
            <button class="action-btn" title="Edit" onclick="Transactions.openModal('${tx.id}')">
              ${Utils.getIconSvg('edit')}
            </button>
            <button class="action-btn delete" title="Delete" onclick="Transactions.deleteTransaction('${tx.id}')">
              ${Utils.getIconSvg('delete')}
            </button>
          </div>
        </td>
      </tr>
    `).join('');
  },

  renderTotals(totals) {
    if (!totals) return;
    const incEl = document.getElementById('txTotalIncome');
    const expEl = document.getElementById('txTotalExpenses');
    const netEl = document.getElementById('txNetCashFlow');

    if (incEl) incEl.textContent = Utils.formatCurrency(totals.totalIncome);
    if (expEl) expEl.textContent = Utils.formatCurrency(totals.totalExpenses);
    if (netEl) netEl.textContent = Utils.formatCurrency(totals.netCashFlow);
  },

  renderPagination(p) {
    const container = document.getElementById('txPagination');
    if (!container || !p) return;

    this.totalPages = p.totalPages;
    const countInfo = document.getElementById('txCountInfo');
    if (countInfo) {
      const start = (p.currentPage - 1) * p.pageSize + 1;
      const end = Math.min(p.totalCount, p.currentPage * p.pageSize);
      countInfo.textContent = p.totalCount > 0 ? `Showing ${start}-${end} of ${p.totalCount} transactions` : '0 transactions';
    }

    let pagesHtml = '';
    for (let i = 1; i <= p.totalPages; i++) {
      pagesHtml += `
        <button class="page-btn ${i === p.currentPage ? 'active' : ''}" onclick="Transactions.goToPage(${i})">
          ${i}
        </button>
      `;
    }

    container.innerHTML = `
      <button class="page-btn" ${p.currentPage <= 1 ? 'disabled' : ''} onclick="Transactions.goToPage(${p.currentPage - 1})">
        &laquo;
      </button>
      ${pagesHtml}
      <button class="page-btn" ${p.currentPage >= p.totalPages ? 'disabled' : ''} onclick="Transactions.goToPage(${p.currentPage + 1})">
        &raquo;
      </button>
    `;
  },

  goToPage(page) {
    if (page < 1 || page > this.totalPages) return;
    this.currentPage = page;
    this.loadTransactions();
  },

  async openModal(id = null) {
    this.editingId = id;
    const titleEl = document.getElementById('txModalTitle');
    const form = document.getElementById('txForm');
    form.reset();

    // Default date to today
    document.getElementById('txModalDate').value = new Date().toISOString().split('T')[0];

    if (id) {
      if (titleEl) titleEl.textContent = 'Edit Transaction';
      try {
        const res = await API.get(`/transactions/${id}`);
        if (res.success && res.data) {
          const tx = res.data;
          document.getElementById('txModalTitleInput').value = tx.title;
          document.getElementById('txModalAmount').value = tx.amount;
          document.getElementById('txModalType').value = tx.type;
          this.updateModalCategories();
          document.getElementById('txModalCategory').value = tx.categoryId;
          document.getElementById('txModalDate').value = tx.date;
          document.getElementById('txModalNotes').value = tx.notes || '';
        }
      } catch (err) {
        Utils.showToast('Failed to load transaction details', 'error');
        return;
      }
    } else {
      if (titleEl) titleEl.textContent = 'Add Transaction';
      document.getElementById('txModalType').value = 'expense';
      this.updateModalCategories();
    }

    Utils.openModal('txModal');
  },

  async saveTransaction() {
    const title = document.getElementById('txModalTitleInput').value.trim();
    const amount = parseFloat(document.getElementById('txModalAmount').value);
    const type = document.getElementById('txModalType').value;
    const categoryId = document.getElementById('txModalCategory').value;
    const date = document.getElementById('txModalDate').value;
    const notes = document.getElementById('txModalNotes').value.trim();

    if (!title) {
      Utils.showToast('Please enter a transaction title', 'error');
      return;
    }
    if (isNaN(amount) || amount <= 0) {
      Utils.showToast('Please enter a valid amount', 'error');
      return;
    }
    if (!categoryId) {
      Utils.showToast('Please select a category', 'error');
      return;
    }

    const payload = { title, amount, type, categoryId, date, notes };

    try {
      if (this.editingId) {
        await API.put(`/transactions/${this.editingId}`, payload);
        Utils.showToast('Transaction updated successfully', 'success');
      } else {
        await API.post('/transactions', payload);
        Utils.showToast('Transaction created successfully', 'success');
      }
      Utils.closeModal('txModal');
      await this.loadTransactions();
    } catch (err) {
      Utils.showToast(err.message || 'Error saving transaction', 'error');
    }
  },

  async deleteTransaction(id) {
    if (!confirm('Are you sure you want to delete this transaction?')) return;

    try {
      await API.delete(`/transactions/${id}`);
      Utils.showToast('Transaction deleted successfully', 'success');
      await this.loadTransactions();
    } catch (err) {
      Utils.showToast(err.message || 'Error deleting transaction', 'error');
    }
  }
};
