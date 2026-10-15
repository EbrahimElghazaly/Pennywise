/**
 * Pennywise Savings Goals Module
 */
const SavingsGoals = {
  goals: [],
  editingId: null,
  activeGoalId: null,

  async init() {
    Navigation.init('savings-goals');
    this.setupEventListeners();
    await this.loadGoals();
  },

  setupEventListeners() {
    // Add Goal
    document.getElementById('openAddGoalBtn')?.addEventListener('click', () => {
      this.openGoalModal();
    });

    document.getElementById('goalForm')?.addEventListener('submit', (e) => {
      e.preventDefault();
      this.saveGoal();
    });

    document.getElementById('closeGoalModalBtn')?.addEventListener('click', () => {
      Utils.closeModal('goalModal');
    });
    document.getElementById('cancelGoalModalBtn')?.addEventListener('click', () => {
      Utils.closeModal('goalModal');
    });

    // Savings Transaction Form (Deposit / Withdraw)
    document.getElementById('savingsActionForm')?.addEventListener('submit', (e) => {
      e.preventDefault();
      this.saveSavingsTransaction();
    });

    document.getElementById('closeSavingsActionModalBtn')?.addEventListener('click', () => {
      Utils.closeModal('savingsActionModal');
    });
    document.getElementById('cancelSavingsActionModalBtn')?.addEventListener('click', () => {
      Utils.closeModal('savingsActionModal');
    });
  },

  async loadGoals() {
    try {
      const res = await API.get('/savings-goals');
      if (res.success) {
        this.goals = res.data;
        this.renderSummary(res.summary);
        this.renderGoalsGrid(this.goals);
      }
    } catch (err) {
      console.error('Error loading savings goals:', err);
      Utils.showToast('Failed to load savings goals', 'error');
    }
  },

  renderSummary(s) {
    if (!s) return;
    document.getElementById('goalsCountVal').textContent = s.totalGoals;
    document.getElementById('goalsTargetVal').textContent = Utils.formatCurrency(s.totalTargetAmount);
    document.getElementById('goalsSavedVal').textContent = Utils.formatCurrency(s.totalSaved);
    document.getElementById('goalsRemainingVal').textContent = Utils.formatCurrency(s.remainingAmount);
  },

  renderGoalsGrid(list) {
    const container = document.getElementById('goalsGrid');
    if (!container) return;

    if (!list || list.length === 0) {
      container.innerHTML = `
        <div class="empty-state" style="grid-column: 1 / -1;">
          <div class="empty-state-icon">${Utils.getIconSvg('savings')}</div>
          <h3 class="empty-state-title">No savings goals yet</h3>
          <p class="empty-state-desc">Create your first goal to save for vacations, gadgets, or rainy day funds.</p>
          <button class="btn btn-primary btn-sm" onclick="SavingsGoals.openGoalModal()">+ Add Goal</button>
        </div>
      `;
      return;
    }

    container.innerHTML = list.map(g => {
      const iconKey = g.name.toLowerCase().includes('laptop') ? 'laptop'
        : (g.name.toLowerCase().includes('vacation') || g.name.toLowerCase().includes('trip') ? 'vacation' : 'savings');

      return `
        <div class="goal-card">
          <div class="goal-card-header">
            <div class="goal-title-area">
              <div class="goal-icon">
                ${Utils.getIconSvg(iconKey)}
              </div>
              <div>
                <h4 class="goal-name">${Utils.escapeHtml(g.name)}</h4>
                <div class="goal-dates">
                  ${g.targetDate ? `Target Date: ${Utils.formatDate(g.targetDate)}` : 'No deadline set'}
                </div>
              </div>
            </div>
            <div class="action-buttons">
              <button class="action-btn" title="Edit Goal" onclick="SavingsGoals.openGoalModal('${g.id}')">
                ${Utils.getIconSvg('edit')}
              </button>
              <button class="action-btn delete" title="Delete Goal" onclick="SavingsGoals.deleteGoal('${g.id}')">
                ${Utils.getIconSvg('delete')}
              </button>
            </div>
          </div>

          <div class="goal-stats-row">
            <div class="goal-stat-box">
              <span class="goal-stat-label">Target</span>
              <span class="goal-stat-val">${Utils.formatCurrency(g.targetAmount)}</span>
            </div>
            <div class="goal-stat-box">
              <span class="goal-stat-label">Saved</span>
              <span class="goal-stat-val" style="color:var(--primary-green);">${Utils.formatCurrency(g.currentAmount)}</span>
            </div>
            <div class="goal-stat-box">
              <span class="goal-stat-label">Remaining</span>
              <span class="goal-stat-val" style="color:var(--secondary-text);">${Utils.formatCurrency(g.remainingAmount)}</span>
            </div>
          </div>

          <div class="goal-progress-section">
            <div class="goal-progress-header">
              <span>Progress</span>
              <span class="goal-progress-pct">${g.percentage}%</span>
            </div>
            <div class="progress-bar">
              <div class="progress-fill" style="width: ${Math.min(100, g.percentage)}%;"></div>
            </div>
          </div>

          <div class="goal-actions-row">
            <button class="goal-btn-add" onclick="SavingsGoals.openActionModal('${g.id}', 'deposit')">
              ${Utils.getIconSvg('plus')} Add Savings
            </button>
            <button class="goal-btn-withdraw" title="Withdraw savings" onclick="SavingsGoals.openActionModal('${g.id}', 'withdrawal')">
              Withdraw
            </button>
          </div>
        </div>
      `;
    }).join('');
  },

  async openGoalModal(id = null) {
    this.editingId = id;
    const titleEl = document.getElementById('goalModalTitle');
    const form = document.getElementById('goalForm');
    form.reset();

    if (id) {
      if (titleEl) titleEl.textContent = 'Edit Savings Goal';
      try {
        const res = await API.get(`/savings-goals/${id}`);
        if (res.success && res.data) {
          const g = res.data;
          document.getElementById('goalModalName').value = g.name;
          document.getElementById('goalModalTarget').value = g.targetAmount;
          document.getElementById('goalModalCurrent').value = g.currentAmount;
          document.getElementById('goalModalDate').value = g.targetDate || '';
          document.getElementById('goalModalDesc').value = g.description || '';
        }
      } catch (err) {
        Utils.showToast('Failed to load goal', 'error');
        return;
      }
    } else {
      if (titleEl) titleEl.textContent = 'Add Savings Goal';
      document.getElementById('goalModalCurrent').value = '0';
    }

    Utils.openModal('goalModal');
  },

  async saveGoal() {
    const name = document.getElementById('goalModalName').value.trim();
    const targetAmount = parseFloat(document.getElementById('goalModalTarget').value);
    const currentAmount = parseFloat(document.getElementById('goalModalCurrent').value) || 0;
    const targetDate = document.getElementById('goalModalDate').value;
    const description = document.getElementById('goalModalDesc').value.trim();

    if (!name) {
      Utils.showToast('Goal name is required', 'error');
      return;
    }
    if (isNaN(targetAmount) || targetAmount <= 0) {
      Utils.showToast('Target amount must be greater than zero', 'error');
      return;
    }

    const payload = { name, targetAmount, currentAmount, targetDate, description };

    try {
      if (this.editingId) {
        await API.put(`/savings-goals/${this.editingId}`, payload);
        Utils.showToast('Savings goal updated successfully', 'success');
      } else {
        await API.post('/savings-goals', payload);
        Utils.showToast('Savings goal created successfully', 'success');
      }
      Utils.closeModal('goalModal');
      await this.loadGoals();
    } catch (err) {
      Utils.showToast(err.message || 'Error saving goal', 'error');
    }
  },

  async deleteGoal(id) {
    if (!confirm('Are you sure you want to delete this savings goal?')) return;

    try {
      await API.delete(`/savings-goals/${id}`);
      Utils.showToast('Goal deleted successfully', 'success');
      await this.loadGoals();
    } catch (err) {
      Utils.showToast(err.message || 'Error deleting goal', 'error');
    }
  },

  openActionModal(goalId, type) {
    this.activeGoalId = goalId;
    const goal = this.goals.find(g => g.id === goalId);
    if (!goal) return;

    const modalTitle = document.getElementById('savingsActionModalTitle');
    const form = document.getElementById('savingsActionForm');
    form.reset();

    document.getElementById('savingsActionType').value = type;
    document.getElementById('savingsActionDate').value = new Date().toISOString().split('T')[0];

    if (type === 'deposit') {
      if (modalTitle) modalTitle.textContent = `Add Savings to "${goal.name}"`;
      document.getElementById('savingsActionSubmitBtn').textContent = 'Confirm Deposit';
    } else {
      if (modalTitle) modalTitle.textContent = `Withdraw from "${goal.name}" (Max: ${Utils.formatCurrency(goal.currentAmount)})`;
      document.getElementById('savingsActionSubmitBtn').textContent = 'Confirm Withdrawal';
    }

    Utils.openModal('savingsActionModal');
  },

  async saveSavingsTransaction() {
    const type = document.getElementById('savingsActionType').value;
    const amount = parseFloat(document.getElementById('savingsActionAmount').value);
    const date = document.getElementById('savingsActionDate').value;
    const notes = document.getElementById('savingsActionNotes').value.trim();

    if (isNaN(amount) || amount <= 0) {
      Utils.showToast('Please enter a valid amount', 'error');
      return;
    }

    const endpoint = type === 'deposit'
      ? `/savings-goals/${this.activeGoalId}/contributions`
      : `/savings-goals/${this.activeGoalId}/withdrawals`;

    try {
      await API.post(endpoint, { amount, date, notes });
      Utils.showToast(type === 'deposit' ? 'Savings added successfully!' : 'Withdrawal recorded successfully!', 'success');
      Utils.closeModal('savingsActionModal');
      await this.loadGoals();
    } catch (err) {
      Utils.showToast(err.message || 'Transaction failed', 'error');
    }
  }
};
