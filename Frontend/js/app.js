/**
 * Pennywise Core Application Initialization
 */
document.addEventListener('DOMContentLoaded', () => {
  // Restore saved theme immediately to prevent flashing
  const savedUser = Auth.getUser();
  if (savedUser && savedUser.theme) {
    document.documentElement.setAttribute('data-theme', savedUser.theme);
  } else {
    const savedTheme = localStorage.getItem('pennywise_theme') || 'light';
    document.documentElement.setAttribute('data-theme', savedTheme);
  }
});
