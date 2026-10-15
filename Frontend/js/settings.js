/**
 * Pennywise Profile & Settings Module
 */
const Settings = {
  currentTab: 'profile',

  async init() {
    Navigation.init('settings');
    this.setupEventListeners();
    await this.loadProfile();
  },

  setupEventListeners() {
    // Tabs
    document.getElementById('tabProfileBtn')?.addEventListener('click', () => {
      this.switchTab('profile');
    });
    document.getElementById('tabPasswordBtn')?.addEventListener('click', () => {
      this.switchTab('password');
    });
    document.getElementById('tabPreferencesBtn')?.addEventListener('click', () => {
      this.switchTab('preferences');
    });

    // Profile Form
    document.getElementById('profileForm')?.addEventListener('submit', (e) => {
      e.preventDefault();
      this.saveProfile();
    });

    // Password Form
    document.getElementById('passwordForm')?.addEventListener('submit', (e) => {
      e.preventDefault();
      this.savePassword();
    });

    // Preferences Form
    document.getElementById('preferencesForm')?.addEventListener('submit', (e) => {
      e.preventDefault();
      this.savePreferences();
    });

    // Theme Buttons
    document.getElementById('themeLightBtn')?.addEventListener('click', () => {
      this.setTheme('light');
    });
    document.getElementById('themeDarkBtn')?.addEventListener('click', () => {
      this.setTheme('dark');
    });

    // Settings Logout Button
    document.getElementById('settingsLogoutBtn')?.addEventListener('click', () => {
      if (confirm('Are you sure you want to log out?')) {
        Auth.logout();
      }
    });
  },

  switchTab(tab) {
    this.currentTab = tab;
    document.querySelectorAll('.settings-tab-btn').forEach(btn => btn.classList.remove('active'));
    document.getElementById(`tab${tab.charAt(0).toUpperCase() + tab.slice(1)}Btn`)?.classList.add('active');

    document.getElementById('sectionProfile').style.display = tab === 'profile' ? 'block' : 'none';
    document.getElementById('sectionPassword').style.display = tab === 'password' ? 'block' : 'none';
    document.getElementById('sectionPreferences').style.display = tab === 'preferences' ? 'block' : 'none';
  },

  async loadProfile() {
    try {
      const res = await API.get('/profile');
      if (res.success && res.data) {
        const u = res.data;
        Auth.setUser(u);

        // Sidebar avatar update
        const initials = u.fullName.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
        document.getElementById('profileAvatarLarge').textContent = initials;
        document.getElementById('profileDisplayName').textContent = u.fullName;
        document.getElementById('profileDisplayEmail').textContent = u.email;
        document.getElementById('profileJoinedDate').textContent = `Account Created: ${Utils.formatDate(u.createdAt)}`;

        // Form Fields
        document.getElementById('profileFullNameInput').value = u.fullName;
        document.getElementById('profileEmailInput').value = u.email;
        document.getElementById('profilePhoneInput').value = u.phoneNumber || '';

        // Preferences
        document.getElementById('prefCurrencySelect').value = u.currency || 'EGP';
        this.setTheme(u.theme || 'light', false);
      }
    } catch (err) {
      console.error('Error loading profile:', err);
      Utils.showToast('Failed to load profile', 'error');
    }
  },

  setTheme(theme, save = true) {
    document.documentElement.setAttribute('data-theme', theme);
    document.getElementById('themeLightBtn')?.classList.toggle('active', theme === 'light');
    document.getElementById('themeDarkBtn')?.classList.toggle('active', theme === 'dark');

    if (save) {
      const currency = document.getElementById('prefCurrencySelect')?.value || 'EGP';
      API.put('/preferences', { currency, theme }).then(() => {
        Utils.showToast(`Theme changed to ${theme}`, 'info');
        const user = Auth.getUser();
        if (user) {
          user.theme = theme;
          Auth.setUser(user);
        }
      }).catch(err => {
        console.error('Error saving theme:', err);
      });
    }
  },

  async saveProfile() {
    const fullName = document.getElementById('profileFullNameInput').value.trim();
    const email = document.getElementById('profileEmailInput').value.trim();
    const phoneNumber = document.getElementById('profilePhoneInput').value.trim();

    if (!fullName) {
      Utils.showToast('Full name is required', 'error');
      return;
    }
    if (!email) {
      Utils.showToast('Email address is required', 'error');
      return;
    }

    try {
      const res = await API.put('/profile', { fullName, email, phoneNumber });
      if (res.success && res.data) {
        Auth.setUser(res.data);
        Utils.showToast('Profile updated successfully', 'success');
        await this.loadProfile();
      }
    } catch (err) {
      Utils.showToast(err.message || 'Error updating profile', 'error');
    }
  },

  async savePassword() {
    const currentPassword = document.getElementById('currentPasswordInput').value;
    const newPassword = document.getElementById('newPasswordInput').value;
    const confirmNewPassword = document.getElementById('confirmPasswordInput').value;

    if (!currentPassword) {
      Utils.showToast('Please enter your current password', 'error');
      return;
    }
    if (!newPassword || newPassword.length < 6) {
      Utils.showToast('New password must be at least 6 characters long', 'error');
      return;
    }
    if (newPassword !== confirmNewPassword) {
      Utils.showToast('New passwords do not match', 'error');
      return;
    }

    try {
      const res = await Auth.changePassword(currentPassword, newPassword, confirmNewPassword);
      if (res.success) {
        Utils.showToast('Password changed successfully!', 'success');
        document.getElementById('passwordForm').reset();
      }
    } catch (err) {
      Utils.showToast(err.message || 'Error updating password', 'error');
    }
  },

  async savePreferences() {
    const currency = document.getElementById('prefCurrencySelect').value;
    const theme = document.documentElement.getAttribute('data-theme') || 'light';

    try {
      const res = await API.put('/preferences', { currency, theme });
      if (res.success) {
        Utils.showToast('Preferences saved successfully!', 'success');
        const user = Auth.getUser();
        if (user) {
          user.currency = currency;
          user.theme = theme;
          Auth.setUser(user);
        }
      }
    } catch (err) {
      Utils.showToast(err.message || 'Error saving preferences', 'error');
    }
  }
};
