/**
 * Pennywise Shared Navigation (Sidebar & Topbar)
 */
const Navigation = {
  navItems: [
    { id: 'dashboard', label: 'Dashboard', url: '/dashboard.html', icon: 'dashboard' },
    { id: 'transactions', label: 'Transactions', url: '/transactions.html', icon: 'transactions' },
    { id: 'budgets', label: 'Budgets', url: '/budgets.html', icon: 'budgets' },
    { id: 'categories', label: 'Categories', url: '/categories.html', icon: 'categories' },
    { id: 'reports', label: 'Reports', url: '/reports.html', icon: 'reports' },
    { id: 'savings-goals', label: 'Savings Goals', url: '/savings-goals.html', icon: 'savings' },
    { id: 'settings', label: 'Settings', url: '/settings.html', icon: 'settings' }
  ],

  init(activePageId) {
    if (!Auth.requireAuth()) return;

    const user = Auth.getUser() || { fullName: 'Ahmed Mohamed', email: 'ahmed@example.com' };
    const initials = user.fullName
      ? user.fullName.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase()
      : 'AM';

    // Render Sidebar if container exists
    const sidebarContainer = document.getElementById('sidebarContainer');
    if (sidebarContainer) {
      const navLinksHtml = this.navItems.map(item => `
        <a href="${item.url}" class="nav-link ${item.id === activePageId ? 'active' : ''}">
          ${Utils.getIconSvg(item.icon)}
          <span>${item.label}</span>
        </a>
      `).join('');

      sidebarContainer.innerHTML = `
        <div class="sidebar-brand">
          <div class="sidebar-brand-icon">
            <svg viewBox="0 0 32 32" fill="none"><path d="M 26 6 C 14 6 6 14 6 26 C 18 26 26 18 26 6 Z" fill="#10B981"/><path d="M 8 24 Q 16 16 24 8" stroke="#064E3B" stroke-width="2" stroke-linecap="round"/></svg>
          </div>
          <span>Pennywise</span>
        </div>
        <nav class="sidebar-nav">
          ${navLinksHtml}
        </nav>
        <div class="sidebar-footer">
          <button id="logoutBtn" class="nav-link" style="width:100%; border:none; background:none;">
            ${Utils.getIconSvg('logout')}
            <span>Logout</span>
          </button>
        </div>
      `;

      document.getElementById('logoutBtn')?.addEventListener('click', (e) => {
        e.preventDefault();
        if (confirm('Are you sure you want to log out of Pennywise?')) {
          Auth.logout();
        }
      });
    }

    // Render Top Header if container exists
    const headerContainer = document.getElementById('headerContainer');
    if (headerContainer) {
      headerContainer.innerHTML = `
        <button id="mobileNavToggle" class="mobile-nav-toggle" aria-label="Toggle navigation">
          <svg viewBox="0 0 24 24" width="24" height="24" fill="currentColor"><path d="M3 18h18v-2H3v2zm0-5h18v-2H3v2zm0-7v2h18V6H3z"/></svg>
        </button>
        <div class="header-search">
          ${Utils.getIconSvg('search')}
          <input type="text" id="globalSearchInput" placeholder="Search transactions, budgets..." />
        </div>
        <div class="header-actions">
          <button class="icon-btn" title="Notifications">
            ${Utils.getIconSvg('bell')}
          </button>
          <div class="user-profile-badge" id="userProfileBtn" onclick="window.location.href='/settings.html'">
            <div class="user-avatar">${initials}</div>
            <span class="user-name">${Utils.escapeHtml(user.fullName)}</span>
            ${Utils.getIconSvg('chevronDown')}
          </div>
        </div>
      `;

      // Global search redirection
      const searchInput = document.getElementById('globalSearchInput');
      searchInput?.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
          const val = searchInput.value.trim();
          if (val) {
            window.location.href = `/transactions.html?search=${encodeURIComponent(val)}`;
          }
        }
      });

      // Mobile Drawer Toggle
      const mobileToggle = document.getElementById('mobileNavToggle');
      const sidebar = document.getElementById('sidebarContainer');
      if (mobileToggle && sidebar) {
        let backdrop = document.getElementById('sidebarBackdrop');
        if (!backdrop) {
          backdrop = document.createElement('div');
          backdrop.id = 'sidebarBackdrop';
          backdrop.className = 'sidebar-backdrop';
          document.body.appendChild(backdrop);
        }

        mobileToggle.addEventListener('click', () => {
          sidebar.classList.toggle('open');
          backdrop.classList.toggle('active');
        });

        backdrop.addEventListener('click', () => {
          sidebar.classList.remove('open');
          backdrop.classList.remove('active');
        });
      }
    }
  }
};
