/**
 * Pennywise Reports & Analytics Module
 */
const Reports = {
  startDate: '',
  endDate: '',

  async init() {
    Navigation.init('reports');

    // Default to current month
    const now = new Date();
    const curYear = now.getFullYear();
    const curMonth = String(now.getMonth() + 1).padStart(2, '0');
    const daysInCurMonth = new Date(curYear, now.getMonth() + 1, 0).getDate();

    this.startDate = `${curYear}-${curMonth}-01`;
    this.endDate = `${curYear}-${curMonth}-${daysInCurMonth}`;

    const startInput = document.getElementById('reportStartDate');
    const endInput = document.getElementById('reportEndDate');
    if (startInput) startInput.value = this.startDate;
    if (endInput) endInput.value = this.endDate;

    this.setupEventListeners();
    await this.loadAllReports();
  },

  setupEventListeners() {
    document.getElementById('reportStartDate')?.addEventListener('change', (e) => {
      this.startDate = e.target.value;
      this.loadAllReports();
    });

    document.getElementById('reportEndDate')?.addEventListener('change', (e) => {
      this.endDate = e.target.value;
      this.loadAllReports();
    });

    document.getElementById('exportCsvBtn')?.addEventListener('click', () => {
      this.exportCsv();
    });
  },

  async loadAllReports() {
    await Promise.all([
      this.loadSummary(),
      this.loadExpensesByCategory(),
      this.loadMonthlyComparison(),
      this.loadTopCategory()
    ]);
  },

  async loadSummary() {
    try {
      const res = await API.get(`/reports/summary?startDate=${this.startDate}&endDate=${this.endDate}`);
      if (res.success && res.data) {
        const d = res.data;
        document.getElementById('repTotalIncome').textContent = Utils.formatCurrency(d.totalIncome);
        document.getElementById('repTotalExpenses').textContent = Utils.formatCurrency(d.totalExpenses);
        document.getElementById('repNetCashFlow').textContent = Utils.formatCurrency(d.netCashFlow);
        document.getElementById('repAvgDaily').textContent = Utils.formatCurrency(d.averageDailySpending);
      }
    } catch (err) {
      console.error('Error loading reports summary:', err);
    }
  },

  async loadExpensesByCategory() {
    try {
      const res = await API.get(`/reports/expenses-by-category?startDate=${this.startDate}&endDate=${this.endDate}`);
      if (res.success) {
        this.renderCategoryDonut(res.data);
      }
    } catch (err) {
      console.error('Error loading expenses by category:', err);
    }
  },

  renderCategoryDonut(categories) {
    const svgContainer = document.getElementById('reportsDonutSvg');
    const legendContainer = document.getElementById('reportsDonutLegend');

    if (!categories || categories.length === 0) {
      if (svgContainer) svgContainer.innerHTML = `<div class="empty-state"><p class="empty-state-desc">No expense records</p></div>`;
      if (legendContainer) legendContainer.innerHTML = '';
      return;
    }

    const size = 150;
    const strokeWidth = 20;
    const radius = (size - strokeWidth) / 2;
    const circumference = 2 * Math.PI * radius;

    let cumulativeOffset = 0;
    let circlesHtml = '';
    let legendHtml = '';

    categories.forEach(cat => {
      const strokeDash = (cat.percentage / 100) * circumference;
      const strokeOffset = -cumulativeOffset;

      circlesHtml += `
        <circle cx="${size / 2}" cy="${size / 2}" r="${radius}"
          fill="none"
          stroke="${cat.color || '#165E34'}"
          stroke-width="${strokeWidth}"
          stroke-dasharray="${strokeDash} ${circumference}"
          stroke-dashoffset="${strokeOffset}"
          transform="rotate(-90 ${size / 2} ${size / 2})">
          <title>${cat.category}: ${Utils.formatCurrency(cat.amount)} (${cat.percentage}%)</title>
        </circle>
      `;

      legendHtml += `
        <div class="legend-item" style="margin-bottom:0.4rem;">
          <div class="legend-left">
            <span class="legend-dot" style="background-color: ${cat.color || '#165E34'}"></span>
            <span>${Utils.escapeHtml(cat.category)}</span>
          </div>
          <span class="legend-pct">${cat.percentage}%</span>
        </div>
      `;

      cumulativeOffset += strokeDash;
    });

    if (svgContainer) {
      svgContainer.innerHTML = `
        <svg viewBox="0 0 ${size} ${size}" width="100%" height="100%">
          <circle cx="${size / 2}" cy="${size / 2}" r="${radius}" fill="none" stroke="#E2E8F0" stroke-width="${strokeWidth}"/>
          ${circlesHtml}
        </svg>
      `;
    }

    if (legendContainer) {
      legendContainer.innerHTML = legendHtml;
    }
  },

  async loadMonthlyComparison() {
    try {
      const res = await API.get('/reports/monthly-comparison');
      if (res.success && res.data) {
        this.renderMonthlyBars(res.data);
      }
    } catch (err) {
      console.error('Error loading monthly comparison:', err);
    }
  },

  renderMonthlyBars(data) {
    const container = document.getElementById('reportsMonthlyBars');
    if (!container || !data || data.length === 0) return;

    const width = 450;
    const height = 220;
    const padding = { top: 20, right: 20, bottom: 35, left: 45 };

    const maxVal = Math.max(...data.map(d => Math.max(d.income, d.expenses)), 1000) * 1.15;
    const chartWidth = width - padding.left - padding.right;
    const chartHeight = height - padding.top - padding.bottom;

    const groupWidth = chartWidth / data.length;
    const barWidth = Math.min(22, (groupWidth - 14) / 2);

    let barsHtml = '';
    let xLabelsHtml = '';

    data.forEach((d, i) => {
      const groupX = padding.left + i * groupWidth + (groupWidth / 2) - barWidth;

      const incHeight = Math.max(2, (d.income / maxVal) * chartHeight);
      const incY = padding.top + chartHeight - incHeight;

      const expHeight = Math.max(2, (d.expenses / maxVal) * chartHeight);
      const expY = padding.top + chartHeight - expHeight;

      barsHtml += `
        <!-- Income Bar -->
        <rect x="${groupX - barWidth/2}" y="${incY}" width="${barWidth}" height="${incHeight}" rx="4" fill="#3B82F6">
          <title>${d.month} Income: ${Utils.formatCurrency(d.income)}</title>
        </rect>
        <!-- Expense Bar -->
        <rect x="${groupX + barWidth/2 + 3}" y="${expY}" width="${barWidth}" height="${expHeight}" rx="4" fill="#10B981">
          <title>${d.month} Expenses: ${Utils.formatCurrency(d.expenses)}</title>
        </rect>
      `;

      xLabelsHtml += `
        <text x="${groupX + 3}" y="${height - 10}" font-size="11" fill="#64748B" text-anchor="middle">${d.month}</text>
      `;
    });

    let gridLinesHtml = '';
    const steps = 4;
    for (let s = 0; s <= steps; s++) {
      const yVal = (maxVal / steps) * s;
      const yPos = padding.top + chartHeight - (chartHeight / steps) * s;
      gridLinesHtml += `
        <line x1="${padding.left}" y1="${yPos}" x2="${width - padding.right}" y2="${yPos}" stroke="#E2E8F0" stroke-width="1" stroke-dasharray="3 3"/>
        <text x="${padding.left - 6}" y="${yPos + 4}" font-size="9" fill="#94A3B8" text-anchor="end">${Math.round(yVal / 1000)}k</text>
      `;
    }

    container.innerHTML = `
      <svg viewBox="0 0 ${width} ${height}" width="100%" height="100%">
        ${gridLinesHtml}
        ${barsHtml}
        ${xLabelsHtml}
      </svg>
      <div style="display:flex; justify-content:center; gap:1.5rem; font-size:0.8rem; color:#64748B; margin-top:0.5rem;">
        <span style="display:flex; align-items:center; gap:0.4rem;"><span style="width:10px; height:10px; border-radius:2px; background:#3B82F6;"></span> Income</span>
        <span style="display:flex; align-items:center; gap:0.4rem;"><span style="width:10px; height:10px; border-radius:2px; background:#10B981;"></span> Expenses</span>
      </div>
    `;
  },

  async loadTopCategory() {
    try {
      const res = await API.get(`/reports/top-category?startDate=${this.startDate}&endDate=${this.endDate}`);
      const card = document.getElementById('topCategoryCard');
      if (!card) return;

      if (res.success && res.data) {
        const top = res.data;
        card.style.display = 'flex';
        document.getElementById('topCategoryName').textContent = top.name;
        document.getElementById('topCategoryValue').textContent = Utils.formatCurrency(top.amount);
        document.getElementById('topCategoryPct').textContent = `${top.percentage}% of total expenses`;
        const iconEl = document.getElementById('topCategoryIcon');
        if (iconEl) {
          iconEl.style.backgroundColor = top.color || '#FF9800';
          iconEl.innerHTML = Utils.getIconSvg(top.icon || 'food');
        }
      } else {
        card.style.display = 'none';
      }
    } catch (err) {
      console.error('Error loading top category:', err);
    }
  },

  async exportCsv() {
    try {
      Utils.showToast('Generating CSV report...', 'info');
      const blob = await API.get(`/reports/export-csv?startDate=${this.startDate}&endDate=${this.endDate}`);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `pennywise-report-${this.startDate}-to-${this.endDate}.csv`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
      Utils.showToast('CSV report downloaded successfully', 'success');
    } catch (err) {
      Utils.showToast('Failed to export CSV', 'error');
    }
  }
};
