import { Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ChartConfiguration } from 'chart.js';
import { ApiService } from '../../core/api.service';
import { CategoriesService } from '../../core/categories.service';
import { ChartComponent } from '../../shared/chart';
import { onEnterSubmit } from '../../shared/keyboard';
import { CategoryBreakdown, CategoryDto, DashboardSummary, MonthForecast, MonthlyTrend } from '../../shared/models';

// Validated reference palette: diverging blue/red pair for polarity (income vs. expenses).
const POSITIVE = '#2a78d6';
const NEGATIVE = '#e34948';

// Qualitative palette for the two per-category pie charts. Each pie already reads as "income" or
// "expenses" from its heading, so distinct hues (rather than shades of one polarity color) are
// used here instead — shades of a single hue become indistinguishable once there are more than
// two or three slices.
const CATEGORY_PALETTE = [
  '#2a78d6', '#e34948', '#3fa34d', '#e0a72e', '#8456c9',
  '#2bb3b3', '#d6672a', '#c93f8d', '#6b8e23', '#4a5fc1',
];
const OTHER_COLOR = '#c3c2b7'; // neutral gray, deliberately outside the palette — marks the catch-all slice as "not a real category"
const OTHER_LABEL = 'Other';

// Categories under this share of the pie's total are folded into one "Other" slice — with many
// small categories the pie becomes unreadable otherwise.
const OTHER_THRESHOLD = 0.04;

/** Sorts by size, folds everything under OTHER_THRESHOLD into a single "Other" slice (only
 * worthwhile once there are 2+ of them — isolating just one small category gains nothing), and
 * assigns each remaining slice a distinct color. */
function pieSlices(
  rows: CategoryBreakdown[],
  amountOf: (r: CategoryBreakdown) => number,
): { labels: string[]; values: number[]; colors: string[] } {
  const sorted = [...rows].sort((a, b) => amountOf(b) - amountOf(a));
  const total = sorted.reduce((sum, r) => sum + amountOf(r), 0);
  const big = total > 0 ? sorted.filter((r) => amountOf(r) / total >= OTHER_THRESHOLD) : sorted;
  const small = total > 0 ? sorted.filter((r) => amountOf(r) / total < OTHER_THRESHOLD) : [];

  const labels = big.map((r) => r.categoryName);
  const values = big.map(amountOf);
  const colors = big.map((_, i) => CATEGORY_PALETTE[i % CATEGORY_PALETTE.length]);

  if (small.length === 1) {
    labels.push(small[0].categoryName);
    values.push(amountOf(small[0]));
    colors.push(CATEGORY_PALETTE[big.length % CATEGORY_PALETTE.length]);
  } else if (small.length > 1) {
    labels.push(OTHER_LABEL);
    values.push(small.reduce((sum, r) => sum + amountOf(r), 0));
    colors.push(OTHER_COLOR);
  }
  return { labels, values, colors };
}

// Sentinel id for the synthetic "uncategorized" row — CategoryBreakdown.categoryId is null for
// transactions with no category (backend labels it "Other"), distinct from any real category id.
const UNCATEGORIZED_ID = -1;

export interface CategoryBreakdownNode {
  id: number;
  name: string;
  /** Own transactions plus every descendant's, recursively. */
  count: number;
  total: number;
  children: CategoryBreakdownNode[];
}

/** Mirrors the category tree (arbitrary depth, #70/#72) with each node's count/total rolled up
 * from its own directly-assigned transactions plus all of its descendants'. Nodes with no
 * transactions anywhere in their subtree are dropped — same "only show what has data" behavior
 * the flat table already had. */
function buildCategoryBreakdownTree(categories: CategoryDto[], breakdown: CategoryBreakdown[]): CategoryBreakdownNode[] {
  const ownById = new Map<number, { count: number; total: number }>();
  for (const b of breakdown) {
    ownById.set(b.categoryId ?? UNCATEGORIZED_ID, { count: b.count, total: b.total });
  }

  const nodeById = new Map<number, CategoryBreakdownNode>();
  for (const c of categories) {
    nodeById.set(c.id, { id: c.id, name: c.name, count: 0, total: 0, children: [] });
  }
  const roots: CategoryBreakdownNode[] = [];
  for (const c of categories) {
    const node = nodeById.get(c.id)!;
    const parent = c.parentCategoryId !== null ? nodeById.get(c.parentCategoryId) : undefined;
    if (parent) parent.children.push(node);
    else roots.push(node);
  }

  function aggregate(node: CategoryBreakdownNode): void {
    const own = ownById.get(node.id);
    node.count = own?.count ?? 0;
    node.total = own?.total ?? 0;
    for (const child of node.children) {
      aggregate(child);
      node.count += child.count;
      node.total += child.total;
    }
  }
  for (const root of roots) aggregate(root);

  function prune(node: CategoryBreakdownNode): CategoryBreakdownNode {
    return { ...node, children: node.children.filter((c) => c.count > 0).map(prune) };
  }
  const byName = (a: CategoryBreakdownNode, b: CategoryBreakdownNode) => a.name.localeCompare(b.name);
  function sortTree(node: CategoryBreakdownNode): CategoryBreakdownNode {
    node.children.sort(byName);
    node.children.forEach(sortTree);
    return node;
  }

  const result = roots.filter((r) => r.count > 0).map(prune);
  result.forEach(sortTree);
  result.sort(byName);

  const uncategorized = ownById.get(UNCATEGORIZED_ID);
  if (uncategorized && uncategorized.count > 0) {
    result.push({ id: UNCATEGORIZED_ID, name: 'Other', count: uncategorized.count, total: uncategorized.total, children: [] });
  }
  return result;
}

@Component({
  selector: 'app-dashboard-page',
  imports: [FormsModule, DecimalPipe, NgTemplateOutlet, ChartComponent],
  templateUrl: './dashboard-page.html',
})
export class DashboardPage {
  private api = inject(ApiService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  protected categoriesService = inject(CategoriesService);

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
    this.categoriesService.ensureLoaded();
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

  /** By-category breakdown, in the same parent/child tree shape as the Categories page (#70/#72),
   * with every node showing its own transactions' total plus all of its descendants'. */
  protected readonly categoryBreakdownTree = computed<CategoryBreakdownNode[]>(() =>
    buildCategoryBreakdownTree(this.categoriesService.categories(), this.byCategory()),
  );

  /** Expense categories only, as a share of total spending. */
  protected readonly expensesPieChart = computed<ChartConfiguration | null>(() => {
    const rows = this.byCategory().filter((r) => r.total < 0);
    if (rows.length === 0) return null;
    const { labels, values, colors } = pieSlices(rows, (r) => -r.total);
    return {
      type: 'pie',
      data: { labels, datasets: [{ data: values, backgroundColor: colors }] },
      options: { plugins: { legend: { position: 'right' } } },
    };
  });

  /** Income categories only, as a share of total income. */
  protected readonly incomePieChart = computed<ChartConfiguration | null>(() => {
    const rows = this.byCategory().filter((r) => r.total > 0);
    if (rows.length === 0) return null;
    const { labels, values, colors } = pieSlices(rows, (r) => r.total);
    return {
      type: 'pie',
      data: { labels, datasets: [{ data: values, backgroundColor: colors }] },
      options: { plugins: { legend: { position: 'right' } } },
    };
  });
}
