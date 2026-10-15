/**
 * Pennywise Budgets Module
 */
const Budgets = {
  budgets: [],
  categories: [],
  editingId: null,

  async init() {
    Navigation.init('budgets');
    await this.loadCategories();
    this.setupEventListeners();
    await this.loadBudgets();
  },

  async loadCategories() {
    try {
      const res = await API.get('/categories?type=expense');
      if (res.success && res.data) {
        this.categories = res.data;
        const catSelect = document.getElementById('budgetModalCategory');
        if (catSelect) {
          catSelect.innerHTML = this.categories.map(c => `
            <option value="${c.id}">${Utils.escapeHtml(c.name)}</option>
          `).join('');
        }
      }
    } catch (err) {
      console.error('Error loading categories:', err);
    }
  },

  setupEventListeners() {
    document.getElementById('openCreateBudgetBtn')?.addEventListener('click', () => {
      this.openModal();
    });

    document.getElementById('budgetForm')?.addEventListener('submit', (e) => {
      e.preventDefault();
      this.saveBudget();
    });

    document.getElementById('closeBudgetModalBtn')?.addEventListener('click', () => {
      Utils.closeModal('budgetModal');
    });
    document.getElementById('cancelBudgetModalBtn')?.addEventListener('click', () => {
      Utils.closeModal('budgetModal');
    });
  },

  async loadBudgets() {
    try {
      const [budgetsRes, summaryRes] = await Promise.all([
        API.get('/budgets'),
        API.get('/budgets/summary')
      ]);

      if (budgetsRes.success) {
        this.budgets = budgetsRes.data;
        this.renderBudgetsGrid(this.budgets);
      }

      if (summaryRes.success && summaryRes.data) {
        this.renderSummary(summaryRes.data);
      }
    } catch (err) {
      console.error('Error loading budgets:', err);
      Utils.showToast('Failed to load budgets', 'error');
    }
  },

  renderSummary(s) {
    const totalEl = document.getElementById('bannerTotalBudget');
    const spentEl = document.getElementById('bannerTotalSpent');
    const remEl = document.getElementById('bannerRemaining');
    const fillEl = document.getElementById('bannerProgressFill');
    const pctEl = document.getElementById('bannerProgressPct');

    if (totalEl) totalEl.textContent = Utils.formatCurrency(s.totalBudget);
    if (spentEl) spentEl.textContent = Utils.formatCurrency(s.totalSpent);
    if (remEl) {
      remEl.textContent = Utils.formatCurrency(s.remaining);
      remEl.className = `budget-metric-value remaining ${s.remaining < 0 ? 'exceeded' : ''}`;
    }

    if (fillEl) {
      fillEl.style.width = `${Math.min(100, s.percentage)}%`;
      fillEl.className = `banner-progress-fill ${s.percentage > 100 ? 'exceeded' : ''}`;
    }
    if (pctEl) pctEl.textContent = `${s.percentage}%`;
  },

  renderBudgetsGrid(list) {
    const container = document.getElementById('budgetsGrid');
    if (!container) return;

    if (!list || list.length === 0) {
      container.innerHTML = `
        <div class="empty-state" style="grid-column: 1 / -1;">
          <div class="empty-state-icon">${Utils.getIconSvg('budgets')}</div>
          <h3 class="empty-state-title">No budgets established</h3>
          <p class="empty-state-desc">Set spending limits on your categories to take control of your expenses.</p>
          <button class="btn btn-primary btn-sm" onclick="Budgets.openModal()">+ Create Budget</button>
        </div>
      `;
      return;
    }

    container.innerHTML = list.map(b => {
      const isExceeded = b.spentAmount > b.limitAmount;
      const progressWidth = Math.min(100, b.percentage);
      const remainingLabel = isExceeded
        ? `-${Utils.formatCurrency(Math.abs(b.remainingAmount))}`
        : Utils.formatCurrency(b.remainingAmount);

      return `
        <div class="budget-card ${isExceeded ? 'exceeded' : ''}">
          <div class="budget-card-header">
            <div class="budget-card-category">
              <div class="budget-card-icon" style="background-color: ${b.categoryColor || '#FF9800'};">
                ${Utils.getIconSvg(b.categoryIcon || 'tag')}
              </div>
              <span class="budget-card-name">${Utils.escapeHtml(b.categoryName)}</span>
            </div>
            <div class="action-buttons">
              <button class="action-btn" title="Edit Budget" onclick="Budgets.openModal('${b.id}')">
                ${Utils.getIconSvg('edit')}
              </button>
              <button class="action-btn delete" title="Delete Budget" onclick="Budgets.deleteBudget('${b.id}')">
                ${Utils.getIconSvg('delete')}
              </button>
            </div>
          </div>

          <div class="budget-card-amounts">
            <div>
              <div style="font-size:0.75rem; color:var(--text-secondary); text-transform:uppercase;">Limit</div>
              <div class="budget-card-limit">${Utils.formatCurrency(b.limitAmount)}</div>
            </div>
            <div class="budget-card-spent-stats">
              <span class="budget-card-spent-label">Spent: </span>
              <span class="budget-card-spent-val">${Utils.formatCurrency(b.spentAmount)}</span>
            </div>
          </div>

          <div class="budget-card-remaining ${isExceeded ? 'exceeded' : 'safe'}">
            Remaining: <span>${remainingLabel}</span>
          </div>

          <div class="progress-bar">
            <div class="progress-fill ${isExceeded ? 'danger' : ''}" style="width: ${progressWidth}%;"></div>
          </div>

          <div class="budget-card-footer ${isExceeded ? 'exceeded' : ''}">
            <span>${b.period.toUpperCase()}</span>
            <span>${b.percentage}%</span>
          </div>
        </div>
      `;
    }).join('');
  },

  async openModal(id = null) {
    this.editingId = id;
    const titleEl = document.getElementById('budgetModalTitle');
    const form = document.getElementById('budgetForm');
    form.reset();

    if (id) {
      if (titleEl) titleEl.textContent = 'Edit Budget';
      try {
        const res = await API.get(`/budgets/${id}`);
        if (res.success && res.data) {
          const b = res.data;
          document.getElementById('budgetModalCategory').value = b.categoryId;
          document.getElementById('budgetModalLimit').value = b.limitAmount;
          document.getElementById('budgetModalPeriod').value = b.period || 'monthly';
        }
      } catch (err) {
        Utils.showToast('Failed to load budget', 'error');
        return;
      }
    } else {
      if (titleEl) titleEl.textContent = 'Create Budget';
      document.getElementById('budgetModalPeriod').value = 'monthly';
    }

    Utils.openModal('budgetModal');
  },

  async saveBudget() {
    const categoryId = document.getElementById('budgetModalCategory').value;
    const limitAmount = parseFloat(document.getElementById('budgetModalLimit').value);
    const period = document.getElementById('budgetModalPeriod').value;

    if (!categoryId) {
      Utils.showToast('Please select a category', 'error');
      return;
    }
    if (isNaN(limitAmount) || limitAmount <= 0) {
      Utils.showToast('Please enter a valid budget limit', 'error');
      return;
    }

    const payload = { categoryId, limitAmount, period };

    try {
      if (this.editingId) {
        await API.put(`/budgets/${this.editingId}`, payload);
        Utils.showToast('Budget updated successfully', 'success');
      } else {
        await API.post('/budgets', payload);
        Utils.showToast('Budget created successfully', 'success');
      }
      Utils.closeModal('budgetModal');
      await this.loadBudgets();
    } catch (err) {
      Utils.showToast(err.message || 'Error saving budget', 'error');
    }
  },

  async deleteBudget(id) {
    if (!confirm('Are you sure you want to delete this budget?')) return;

    try {
      await API.delete(`/budgets/${id}`);
      Utils.showToast('Budget deleted successfully', 'success');
      await this.loadBudgets();
    } catch (err) {
      Utils.showToast(err.message || 'Error deleting budget', 'error');
    }
  }
};
