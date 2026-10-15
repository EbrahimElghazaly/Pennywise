/**
 * Pennywise Authentication Module
 */
const Auth = {
  getUser() {
    try {
      const stored = localStorage.getItem('pennywise_user');
      return stored ? JSON.parse(stored) : null;
    } catch {
      return null;
    }
  },

  setUser(user) {
    if (user) {
      localStorage.setItem('pennywise_user', JSON.stringify(user));
      // Also apply user's saved currency and theme
      if (user.currency) localStorage.setItem('pennywise_currency', user.currency);
      if (user.theme) document.documentElement.setAttribute('data-theme', user.theme);
    } else {
      localStorage.removeItem('pennywise_user');
    }
  },

  isLoggedIn() {
    return !!API.getToken() && !!this.getUser();
  },

  async checkAuth() {
    const token = API.getToken();
    if (!token) return null;

    try {
      const res = await API.get('/auth/me');
      if (res && res.success && res.user) {
        this.setUser(res.user);
        return res.user;
      }
    } catch {
      this.setUser(null);
      API.setToken(null);
    }
    return null;
  },

  requireAuth() {
    if (!API.getToken()) {
      window.location.href = '/login.html';
      return false;
    }
    return true;
  },

  redirectIfAuth() {
    if (API.getToken() && this.getUser()) {
      window.location.href = '/dashboard.html';
      return true;
    }
    return false;
  },

  async login(email, password, rememberMe) {
    const res = await API.post('/auth/login', { email, password, rememberMe });
    if (res.success && res.token) {
      API.setToken(res.token);
      this.setUser(res.user);
    }
    return res;
  },

  async register(fullName, email, password, confirmPassword, agreeToTerms) {
    const res = await API.post('/auth/register', {
      fullName,
      email,
      password,
      confirmPassword,
      agreeToTerms
    });
    if (res.success && res.token) {
      API.setToken(res.token);
      this.setUser(res.user);
    }
    return res;
  },

  async logout() {
    try {
      await API.post('/auth/logout', {});
    } catch (e) {
      console.warn('Logout request error:', e);
    } finally {
      API.setToken(null);
      this.setUser(null);
      window.location.href = '/login.html';
    }
  },

  async changePassword(currentPassword, newPassword, confirmNewPassword) {
    return await API.post('/auth/change-password', {
      currentPassword,
      newPassword,
      confirmNewPassword
    });
  }
};
