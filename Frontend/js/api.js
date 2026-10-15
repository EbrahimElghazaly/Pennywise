/**
 * Pennywise Centralized API Client
 */
const API = {
  baseUrl: '/api',

  getToken() {
    return localStorage.getItem('pennywise_token');
  },

  setToken(token) {
    if (token) {
      localStorage.setItem('pennywise_token', token);
    } else {
      localStorage.removeItem('pennywise_token');
    }
  },

  async request(endpoint, options = {}) {
    const url = `${this.baseUrl}${endpoint}`;
    const headers = {
      'Content-Type': 'application/json',
      ...options.headers
    };

    const token = this.getToken();
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    try {
      const response = await fetch(url, {
        ...options,
        headers
      });

      // Handle 401 Unauthorized
      if (response.status === 401) {
        // If not already on login or register or landing page, redirect to login
        const path = window.location.pathname;
        if (!path.includes('login') && !path.includes('register') && path !== '/' && !path.endsWith('index.html')) {
          this.setToken(null);
          localStorage.removeItem('pennywise_user');
          window.location.href = '/login.html';
        }
      }

      // Check if response is CSV or blob
      const contentType = response.headers.get('content-type');
      if (contentType && contentType.includes('text/csv')) {
        return await response.blob();
      }

      const data = await response.json().catch(() => ({}));

      if (!response.ok) {
        throw new Error(data.message || `Request failed with status ${response.status}`);
      }

      return data;
    } catch (err) {
      console.error(`API Error on ${endpoint}:`, err);
      throw err;
    }
  },

  get(endpoint) {
    return this.request(endpoint, { method: 'GET' });
  },

  post(endpoint, body) {
    return this.request(endpoint, {
      method: 'POST',
      body: JSON.stringify(body)
    });
  },

  put(endpoint, body) {
    return this.request(endpoint, {
      method: 'PUT',
      body: JSON.stringify(body)
    });
  },

  delete(endpoint) {
    return this.request(endpoint, { method: 'DELETE' });
  }
};
