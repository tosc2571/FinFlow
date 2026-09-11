import { Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ChartConfiguration } from 'chart.js';
import { ApiService } from '../../core/api.service';
import { ChartComponent } from '../../shared/chart';
import { onEnterSubmit } from '../../shared/keyboard';
import { CategoryBreakdown, DashboardSummary, MonthForecast, MonthlyTrend } from '../../shared/models';

// Validated reference palette: diverging blue/red pair for polarity (income vs. expenses).
const POSITIVE = '#2a78d6';
const NEGATIVE = '#e34948';

// Category hues for the two per-category pie charts, matching the POSITIVE/NEGATIVE hue family
// so each pie still reads as "income" or "expenses" at a glance, with lightness distinguishing
// individual categories within it.
const POSITIVE_HUE = 213;
const NEGATIVE_HUE = 2;

/** `count` evenly-spaced shades of one hue — one per pie slice. */
function shades(hue: number, count: number): string[] {
  if (count <= 1) return [`hsl(${hue}, 55%, 50%)`];
  return Array.from({ length: count }, (_, i) => `hsl(${hue}, 55%, ${35 + (i * 35) / (count - 1)}%)`);
}

@Component({
  selector: 'app-dashboard-page',
  imports: [FormsModule, DecimalPipe, ChartComponent],
  templateUrl: './dashboard-page.html',
})
export class DashboardPage {
  private api = inject(ApiService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  protected onToolbarEnter(event: Event): void {
    onEnterSubmit(event, () => this.load());
  }

  // Seeded from the URL so a reload restores the same year instead of resetting to today — #35.
  private qp = this.route.snapshot.queryParamMap;
  protected year: number | null = this.qp.has('year')
    ? this.qp.get('year')
      ? Number(this.qp.get('year'))
      : null
    : new Date().getFullYear();

  protected readonly summary = signal<DashboardSummary | null>(null);
  protected readonly byCategory = signal<CategoryBreakdown[]>([]);
  protected readonly trend = signal<MonthlyTrend[]>([]);
  protected readonly forecast = signal<MonthForecast[]>([]);

  protected readonly monthNames = [
    'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
  ];

  // See TransactionsPage.initialSyncDone for why this guards the very first syncUrl call.
  private initialSyncDone = false;

  constructor() {
    this.load();
    this.initialSyncDone = true;
    this.api.getForecast(6).subscribe((f) => this.forecast.set(f));
  }

  private syncUrl(): void {
    if (!this.initialSyncDone) return;
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { year: this.year !== null ? this.year : '' },
      replaceUrl: true,
    });
  }

  protected load(): void {
    this.syncUrl();
    const filter = { year: this.year };
    this.api.getSummary(filter).subscribe((s) => this.summary.set(s));
    this.api.getByCategory(filter).subscribe((c) => this.byCategory.set(c));
    this.api.getTrend(filter).subscribe((t) => this.trend.set(t));
  }

  /** Income up, expenses down around the zero baseline — polarity, not two magnitudes. */
  protected readonly trendChart = computed<ChartConfiguration | null>(() => {
    const rows = this.trend();
    if (rows.length === 0) return null;
    return {
      type: 'bar',
      data: {
        labels: rows.map((r) => `${String(r.month).padStart(2, '0')}/${r.year}`),
        datasets: [
          {
            label: 'Income',
            data: rows.map((r) => r.income),
            backgroundColor: POSITIVE,
            borderRadius: 4,
            maxBarThickness: 28,
          },
          {
            label: 'Expenses',
            data: rows.map((r) => r.expenses),
            backgroundColor: NEGATIVE,
            borderRadius: 4,
            maxBarThickness: 28,
          },
        ],
      },
      options: {
        plugins: { legend: { position: 'bottom' } },
        scales: { x: { grid: { display: false } } },
      },
    };
  });

  /** Forecast: same income/expense polarity pair as the historical trend chart, so the two
   * read as one continuous story even though the data sources differ (actuals vs. contracts). */
  protected readonly forecastChart = computed<ChartConfiguration | null>(() => {
    const rows = this.forecast();
    if (rows.length === 0) return null;
    return {
      type: 'bar',
      data: {
        labels: rows.map((r) => `${this.monthNames[r.month - 1]} ${r.year}`),
        datasets: [
          {
            label: 'Expected income',
            data: rows.map((r) => r.expectedIncome),
            backgroundColor: POSITIVE,
            borderRadius: 4,
            maxBarThickness: 28,
          },
          {
            label: 'Expected expenses',
            data: rows.map((r) => r.expectedExpenses),
            backgroundColor: NEGATIVE,
            borderRadius: 4,
            maxBarThickness: 28,
          },
        ],
      },
      options: {
        plugins: { legend: { position: 'bottom' } },
        scales: { x: { grid: { display: false } } },
      },
    };
  });

  /** One measure across categories — horizontal bars, colored by sign (same diverging pair). */
  protected readonly categoryChart = computed<ChartConfiguration | null>(() => {
    const rows = [...this.byCategory()].sort((a, b) => a.total - b.total);
    if (rows.length === 0) return null;
    return {
      type: 'bar',
      data: {
        labels: rows.map((r) => r.categoryName),
        datasets: [
          {
            label: 'Total',
            data: rows.map((r) => r.total),
            backgroundColor: rows.map((r) => (r.total < 0 ? NEGATIVE : POSITIVE)),
            borderRadius: 4,
            maxBarThickness: 22,
          },
        ],
      },
      options: {
        indexAxis: 'y',
        plugins: { legend: { display: false } },
        scales: { y: { grid: { display: false } } },
      },
    };
  });

  /** Expense categories only, as a share of total spending. */
  protected readonly expensesPieChart = computed<ChartConfiguration | null>(() => {
    const rows = this.byCategory()
      .filter((r) => r.total < 0)
      .sort((a, b) => a.total - b.total);
    if (rows.length === 0) return null;
    return {
      type: 'pie',
      data: {
        labels: rows.map((r) => r.categoryName),
        datasets: [{ data: rows.map((r) => -r.total), backgroundColor: shades(NEGATIVE_HUE, rows.length) }],
      },
      options: { plugins: { legend: { position: 'right' } } },
    };
  });

  /** Income categories only, as a share of total income. */
  protected readonly incomePieChart = computed<ChartConfiguration | null>(() => {
    const rows = this.byCategory()
      .filter((r) => r.total > 0)
      .sort((a, b) => b.total - a.total);
    if (rows.length === 0) return null;
    return {
      type: 'pie',
      data: {
        labels: rows.map((r) => r.categoryName),
        datasets: [{ data: rows.map((r) => r.total), backgroundColor: shades(POSITIVE_HUE, rows.length) }],
      },
      options: { plugins: { legend: { position: 'right' } } },
    };
  });
}
