/**
 * Pennywise Dashboard Controller & Pure SVG Visualizations
 */
const Dashboard = {
  async init() {
    Navigation.init('dashboard');

    const periodElement = document.getElementById('dashboardPeriod');
    if (periodElement) {
      periodElement.textContent = new Intl.DateTimeFormat(undefined, {
        month: 'long',
        year: 'numeric',
        timeZone: 'UTC'
      }).format(new Date());
    }

    const user = Auth.getUser();
    if (user) {
      const welcomeEl = document.getElementById('welcomeUserName');
      if (welcomeEl) welcomeEl.textContent = user.fullName.split(' ')[0];
    }

    await Promise.all([
      this.loadSummary(),
      this.loadCharts(),
      this.loadRecentTransactions(),
      this.loadBudgetOverview()
    ]);
  },

  async loadSummary() {
    try {
      const res = await API.get('/dashboard/summary');
      if (res.success && res.data) {
        const d = res.data;
        document.getElementById('totalIncomeVal').textContent = Utils.formatCurrency(d.totalIncome);
        document.getElementById('totalExpensesVal').textContent = Utils.formatCurrency(d.totalExpenses);
        document.getElementById('availableBalanceVal').textContent = Utils.formatCurrency(d.availableBalance);
        document.getElementById('savingsGoalVal').textContent = `${d.savingsPercentage}% saved`;
        document.getElementById('savingsGoalSub').textContent = `${d.completedGoals} of ${d.totalGoals} goals`;

        // Deltas
        const incDelta = document.getElementById('incomeDelta');
        if (incDelta) {
          incDelta.textContent = `${d.incomeChangePercent >= 0 ? '+' : ''}${d.incomeChangePercent}% vs last month`;
          incDelta.className = `stat-delta ${d.incomeChangePercent >= 0 ? 'positive' : 'negative'}`;
        }

        const expDelta = document.getElementById('expenseDelta');
        if (expDelta) {
          expDelta.textContent = `${d.expensesChangePercent}% vs last month`;
        }

        const balDelta = document.getElementById('balanceDelta');
        if (balDelta) {
          balDelta.textContent = `${d.balanceChangePercent >= 0 ? '+' : ''}${d.balanceChangePercent}% vs last month`;
          balDelta.className = `stat-delta ${d.balanceChangePercent >= 0 ? 'positive' : 'negative'}`;
        }
      }
    } catch (err) {
      console.error('Error loading dashboard summary:', err);
    }
  },

  async loadCharts() {
    try {
      const res = await API.get('/dashboard/charts');
      if (res.success && res.data) {
        this.renderIncomeVsExpensesChart(res.data.monthlyComparison);
        this.renderExpensesByCategoryChart(res.data.expensesByCategory);
        this.renderSpendingTrendChart(res.data.spendingTrend);
      }
    } catch (err) {
      console.error('Error loading charts:', err);
    }
  },

  renderIncomeVsExpensesChart(data) {
    const container = document.getElementById('incomeExpensesChart');
    if (!container || !data || data.length === 0) {
      if (container) container.innerHTML = `<div class="empty-state"><p class="empty-state-desc">No transaction data available</p></div>`;
      return;
    }

    const width = 320;
    const height = 180;
    const padding = { top: 20, right: 15, bottom: 30, left: 35 };

    const maxVal = Math.max(...data.map(d => Math.max(d.income, d.expenses)), 1000) * 1.15;
    const chartWidth = width - padding.left - padding.right;
    const chartHeight = height - padding.top - padding.bottom;

    const groupWidth = chartWidth / data.length;
    const barWidth = Math.min(18, (groupWidth - 14) / 2);

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
        <text x="${groupX + 3}" y="${height - 8}" font-size="11" fill="#64748B" text-anchor="middle">${d.month}</text>
      `;
    });

    // Y Axis grid lines
    const ySteps = 3;
    let gridLinesHtml = '';
    for (let s = 0; s <= ySteps; s++) {
      const yVal = (maxVal / ySteps) * s;
      const yPos = padding.top + chartHeight - (chartHeight / ySteps) * s;
      gridLinesHtml += `
        <line x1="${padding.left}" y1="${yPos}" x2="${width - padding.right}" y2="${yPos}" stroke="#E2E8F0" stroke-width="1" stroke-dasharray="3 3"/>
        <text x="${padding.left - 6}" y="${yPos + 3}" font-size="9" fill="#94A3B8" text-anchor="end">${Math.round(yVal / 1000)}k</text>
      `;
    }

    container.innerHTML = `
      <svg viewBox="0 0 ${width} ${height}" width="100%" height="100%">
        ${gridLinesHtml}
        ${barsHtml}
        ${xLabelsHtml}
      </svg>
      <div style="display:flex; justify-content:center; gap:1.25rem; font-size:0.75rem; color:#64748B; margin-top:0.25rem;">
        <span style="display:flex; align-items:center; gap:0.35rem;"><span style="width:8px; height:8px; border-radius:2px; background:#3B82F6;"></span> Income</span>
        <span style="display:flex; align-items:center; gap:0.35rem;"><span style="width:8px; height:8px; border-radius:2px; background:#10B981;"></span> Expenses</span>
      </div>
    `;
  },

  renderExpensesByCategoryChart(categories) {
    const svgContainer = document.getElementById('expensesDonutChart');
    const legendContainer = document.getElementById('donutLegend');

    if (!categories || categories.length === 0) {
      if (svgContainer) svgContainer.innerHTML = `<div class="empty-state"><p class="empty-state-desc">No expenses</p></div>`;
      if (legendContainer) legendContainer.innerHTML = '';
      return;
    }

    const size = 120;
    const strokeWidth = 16;
    const radius = (size - strokeWidth) / 2;
    const circumference = 2 * Math.PI * radius;

    let cumulativeOffset = 0;
    let circlesHtml = '';
    let legendHtml = '';

    categories.forEach((cat) => {
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
        <div class="legend-item">
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

  renderSpendingTrendChart(trend) {
    const container = document.getElementById('spendingTrendChart');
    if (!container || !trend || trend.length === 0) {
      if (container) container.innerHTML = `<div class="empty-state"><p class="empty-state-desc">No trend data</p></div>`;
      return;
    }

    const width = 240;
    const height = 180;
    const padding = { top: 25, right: 20, bottom: 30, left: 35 };

    const chartWidth = width - padding.left - padding.right;
    const chartHeight = height - padding.top - padding.bottom;

    const maxVal = Math.max(...trend.map(t => t.expenses), 1000) * 1.15;
    const minVal = 0;

    let points = [];
    let xLabelsHtml = '';

    trend.forEach((t, i) => {
      const x = padding.left + (i / Math.max(1, trend.length - 1)) * chartWidth;
      const y = padding.top + chartHeight - ((t.expenses - minVal) / (maxVal - minVal)) * chartHeight;
      points.push({ x, y, expenses: t.expenses, month: t.month });

      xLabelsHtml += `
        <text x="${x}" y="${height - 8}" font-size="10" fill="#64748B" text-anchor="middle">${t.month}</text>
      `;
    });

    const pathD = points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x} ${p.y}`).join(' ');
    const areaD = `${pathD} L ${points[points.length - 1].x} ${padding.top + chartHeight} L ${points[0].x} ${padding.top + chartHeight} Z`;

    let dotsHtml = points.map(p => `
      <circle cx="${p.x}" cy="${p.y}" r="4" fill="#FFFFFF" stroke="#7E88EF" stroke-width="2.5">
        <title>${p.month}: ${Utils.formatCurrency(p.expenses)}</title>
      </circle>
    `).join('');

    container.innerHTML = `
      <svg viewBox="0 0 ${width} ${height}" width="100%" height="100%">
        <defs>
          <linearGradient id="trendGradient" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stop-color="#7E88EF" stop-opacity="0.3"/>
            <stop offset="100%" stop-color="#7E88EF" stop-opacity="0.0"/>
          </linearGradient>
        </defs>
        <!-- Grid lines -->
        <line x1="${padding.left}" y1="${padding.top}" x2="${width - padding.right}" y2="${padding.top}" stroke="#E2E8F0" stroke-dasharray="3 3"/>
        <line x1="${padding.left}" y1="${padding.top + chartHeight}" x2="${width - padding.right}" y2="${padding.top + chartHeight}" stroke="#CBD5E1"/>
        
        <!-- Fill Area -->
        <path d="${areaD}" fill="url(#trendGradient)"/>
        <!-- Trend Line -->
        <path d="${pathD}" fill="none" stroke="#7E88EF" stroke-width="2.5" stroke-linecap="round"/>
        <!-- Data Dots -->
        ${dotsHtml}
        ${xLabelsHtml}
      </svg>
    `;
  },

  async loadRecentTransactions() {
    const tableBody = document.getElementById('recentTransactionsBody');
    if (!tableBody) return;

    try {
      const res = await API.get('/dashboard/recent-transactions');
      if (res.success && res.data && res.data.length > 0) {
        tableBody.innerHTML = res.data.map(tx => `
          <tr>
            <td>${Utils.formatDate(tx.date)}</td>
            <td style="font-weight:600;">${Utils.escapeHtml(tx.title)}</td>
            <td>
              <div class="tx-category-cell">
                <span class="tx-cat-icon" style="background-color: ${tx.categoryColor || '#165E34'};">
                  ${Utils.getIconSvg(tx.categoryIcon || 'tag')}
                </span>
                <span>${Utils.escapeHtml(tx.categoryName)}</span>
              </div>
            </td>
            <td>
              <span class="badge ${tx.type === 'income' ? 'badge-income' : 'badge-expense'}">
                ${tx.type}
              </span>
            </td>
            <td class="tx-amount ${tx.type === 'income' ? 'income' : 'expense'}" style="text-align:right;">
              ${tx.type === 'income' ? '+' : '-'}${Utils.formatCurrency(tx.amount)}
            </td>
          </tr>
        `).join('');
      } else {
        tableBody.innerHTML = `
          <tr>
            <td colspan="5" style="text-align:center; padding:2rem; color:var(--text-secondary);">
              No recent transactions recorded.
            </td>
          </tr>
        `;
      }
    } catch (err) {
      console.error('Error loading recent transactions:', err);
    }
  },

  async loadBudgetOverview() {
    try {
      const res = await API.get('/budgets/summary');
      if (res.success && res.data) {
        const d = res.data;
        const totalBudgetEl = document.getElementById('budgetOverviewLimit');
        const spentEl = document.getElementById('budgetOverviewSpent');
        const remainingEl = document.getElementById('budgetOverviewRemaining');
        const fillEl = document.getElementById('budgetOverviewFill');
        const pctEl = document.getElementById('budgetOverviewPct');

        if (totalBudgetEl) totalBudgetEl.textContent = Utils.formatCurrency(d.totalBudget);
        if (spentEl) spentEl.textContent = Utils.formatCurrency(d.totalSpent);
        if (remainingEl) remainingEl.textContent = Utils.formatCurrency(d.remaining);

        if (fillEl) {
          fillEl.style.width = `${Math.min(100, d.percentage)}%`;
          fillEl.className = `banner-progress-fill ${d.percentage > 100 ? 'exceeded' : ''}`;
        }
        if (pctEl) pctEl.textContent = `${d.percentage}%`;
      }
    } catch (err) {
      console.error('Error loading budget overview:', err);
    }
  }
};
