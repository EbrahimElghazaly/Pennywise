/**
 * Pennywise Categories Module
 */
const Categories = {
  currentTab: 'expense',
  categories: [],
  editingId: null,

  async init() {
    Navigation.init('categories');
    this.setupEventListeners();
    await this.loadCategories();
  },

  setupEventListeners() {
    // Tabs
    document.getElementById('tabExpenseBtn')?.addEventListener('click', () => {
      this.switchTab('expense');
    });
    document.getElementById('tabIncomeBtn')?.addEventListener('click', () => {
      this.switchTab('income');
    });

    // Add Category Button
    document.getElementById('openAddCategoryBtn')?.addEventListener('click', () => {
      this.openModal();
    });

    // Modal Form
    document.getElementById('categoryForm')?.addEventListener('submit', (e) => {
      e.preventDefault();
      this.saveCategory();
    });

    document.getElementById('closeCategoryModalBtn')?.addEventListener('click', () => {
      Utils.closeModal('categoryModal');
    });
    document.getElementById('cancelCategoryModalBtn')?.addEventListener('click', () => {
      Utils.closeModal('categoryModal');
    });
  },

  switchTab(tab) {
    this.currentTab = tab;
    document.getElementById('tabExpenseBtn')?.classList.toggle('active', tab === 'expense');
    document.getElementById('tabIncomeBtn')?.classList.toggle('active', tab === 'income');
    this.renderGrid();
  },

  async loadCategories() {
    try {
      const res = await API.get('/categories');
      if (res.success && res.data) {
        this.categories = res.data;
        this.renderGrid();
      }
    } catch (err) {
      console.error('Error loading categories:', err);
      Utils.showToast('Failed to load categories', 'error');
    }
  },

  renderGrid() {
    const container = document.getElementById('categoriesGrid');
    if (!container) return;

    const filtered = this.categories.filter(c => c.type === this.currentTab);

    if (filtered.length === 0) {
      container.innerHTML = `
        <div class="empty-state" style="grid-column: 1 / -1;">
          <div class="empty-state-icon">${Utils.getIconSvg('categories')}</div>
          <h3 class="empty-state-title">No ${this.currentTab} categories found</h3>
          <p class="empty-state-desc">Create your first custom category to organize your financial records.</p>
          <button class="btn btn-primary btn-sm" onclick="Categories.openModal()">+ Add Category</button>
        </div>
      `;
      return;
    }

    container.innerHTML = filtered.map(c => `
      <div class="category-card">
        <div class="category-info">
          <div class="category-icon-circle" style="background-color: ${c.color || '#165E34'};">
            ${Utils.getIconSvg(c.icon || 'tag')}
          </div>
          <div class="category-details">
            <h4>${Utils.escapeHtml(c.name)}</h4>
            <div class="category-tx-count">
              ${c.type === 'income' ? 'Income' : 'Expenses'} • ${c.transactionCount} transactions
            </div>
          </div>
        </div>
        <div class="category-actions">
          <button class="action-btn" title="Edit Category" onclick="Categories.openModal('${c.id}')">
            ${Utils.getIconSvg('edit')}
          </button>
          <button class="action-btn delete" title="Delete Category" onclick="Categories.deleteCategory('${c.id}')">
            ${Utils.getIconSvg('delete')}
          </button>
        </div>
      </div>
    `).join('');
  },

  async openModal(id = null) {
    this.editingId = id;
    const titleEl = document.getElementById('categoryModalTitle');
    const form = document.getElementById('categoryForm');
    form.reset();

    if (id) {
      if (titleEl) titleEl.textContent = 'Edit Category';
      try {
        const res = await API.get(`/categories/${id}`);
        if (res.success && res.data) {
          const c = res.data;
          document.getElementById('catModalName').value = c.name;
          document.getElementById('catModalType').value = c.type;
          document.getElementById('catModalIcon').value = c.icon;
          document.getElementById('catModalColor').value = c.color;
        }
      } catch (err) {
        Utils.showToast('Failed to load category', 'error');
        return;
      }
    } else {
      if (titleEl) titleEl.textContent = 'Add Category';
      document.getElementById('catModalType').value = this.currentTab;
      document.getElementById('catModalIcon').value = 'tag';
      document.getElementById('catModalColor').value = '#165E34';
    }

    Utils.openModal('categoryModal');
  },

  async saveCategory() {
    const name = document.getElementById('catModalName').value.trim();
    const type = document.getElementById('catModalType').value;
    const icon = document.getElementById('catModalIcon').value;
    const color = document.getElementById('catModalColor').value;

    if (!name) {
      Utils.showToast('Category name is required', 'error');
      return;
    }

    const payload = { name, type, icon, color };

    try {
      if (this.editingId) {
        await API.put(`/categories/${this.editingId}`, payload);
        Utils.showToast('Category updated successfully', 'success');
      } else {
        await API.post('/categories', payload);
        Utils.showToast('Category created successfully', 'success');
      }
      Utils.closeModal('categoryModal');
      await this.loadCategories();
    } catch (err) {
      Utils.showToast(err.message || 'Error saving category', 'error');
    }
  },

  async deleteCategory(id) {
    if (!confirm('Are you sure you want to delete this category?')) return;

    try {
      await API.delete(`/categories/${id}`);
      Utils.showToast('Category deleted successfully', 'success');
      await this.loadCategories();
    } catch (err) {
      Utils.showToast(err.message || 'Cannot delete category', 'error');
    }
  }
};
