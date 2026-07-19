import { Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChartConfiguration } from 'chart.js';
import { ApiService } from '../../core/api.service';
import { ChartComponent } from '../../shared/chart';
import { CategoryBreakdown, DashboardSummary, MonthlyTrend } from '../../shared/models';

// Validated reference palette: diverging blue/red pair for polarity (income vs. expenses).
const POSITIVE = '#2a78d6';
const NEGATIVE = '#e34948';

@Component({
  selector: 'app-dashboard-page',
  imports: [FormsModule, DecimalPipe, ChartComponent],
  templateUrl: './dashboard-page.html',
})
export class DashboardPage {
  private api = inject(ApiService);

  protected year: number | null = new Date().getFullYear();

  protected readonly summary = signal<DashboardSummary | null>(null);
  protected readonly byCategory = signal<CategoryBreakdown[]>([]);
  protected readonly trend = signal<MonthlyTrend[]>([]);

  constructor() {
    this.load();
  }

  protected load(): void {
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
}
