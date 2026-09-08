import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { CategoriesService } from '../../core/categories.service';
import { onEnterSubmit } from '../../shared/keyboard';
import { QuickCreateCategoryDialog } from '../../shared/quick-create-category-dialog';
import {
  CategoryDto,
  ClassificationStatus,
  PagedTransactions,
  STATUS_LABELS,
  TransactionDto,
  TransactionFilter,
} from '../../shared/models';

@Component({
  selector: 'app-transactions-page',
  imports: [FormsModule, DatePipe, DecimalPipe, QuickCreateCategoryDialog],
  templateUrl: './transactions-page.html',
})
export class TransactionsPage {
  private api = inject(ApiService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  protected categoriesService = inject(CategoriesService);
  protected categoryDialog = viewChild.required(QuickCreateCategoryDialog);

  /** Transaction the "→ New category…" option was picked for; set right before the dialog
   * opens, consumed by onCategoryCreated once the user finishes the form. */
  private pendingCategoryTx: TransactionDto | null = null;

  protected onToolbarEnter(event: Event): void {
    onEnterSubmit(event, () => this.load(1));
  }

  /** Sentinel option value for the per-row category <select>'s "new category" entry — distinct
   * from any real category id (number) or null (clear category). */
  protected readonly manageCategoriesOption = '__manage__';

  protected readonly statusLabels = STATUS_LABELS;
  protected readonly statuses: ClassificationStatus[] = [
    'Auto',
    'NeedsReview',
    'Ignored',
    'ManualOverride',
    'InternalTransfer',
  ];

  // Filter form state (plain fields — ngModel; zoneless CD runs after template events). Seeded
  // from the URL's query params so a reload (or a pasted link) restores the same filtered view
  // instead of always resetting to today's year — see #35. `qp` is read once here; KeepAliveRouteReuseStrategy
  // means this constructor never reruns for SPA navigation, only for a fresh app load.
  private qp = this.route.snapshot.queryParamMap;
  protected year: number | null = this.qp.has('year')
    ? this.qp.get('year')
      ? Number(this.qp.get('year'))
      : null
    : new Date().getFullYear();
  protected contains = this.qp.get('contains') ?? '';
  protected bank = this.qp.get('bank') ?? '';
  protected status = (this.qp.get('status') ?? '') as ClassificationStatus | '';
  protected categoryId: number | '' = this.qp.get('categoryId') ? Number(this.qp.get('categoryId')) : '';
  protected sort = this.qp.get('sort') ?? 'date';

  protected readonly pageSize = 50;
  protected readonly data = signal<PagedTransactions | null>(null);
  protected readonly loading = signal(false);

  protected readonly totalPages = computed(() => {
    const d = this.data();
    return d ? Math.max(1, Math.ceil(d.total / d.pageSize)) : 1;
  });

  // Guards against calling router.navigate() synchronously while this component is still being
  // constructed as part of an in-flight navigation (the initial filter state already came FROM
  // the URL, so there's nothing to write back yet anyway).
  private initialSyncDone = false;

  constructor() {
    this.categoriesService.ensureLoaded();
    this.load(this.qp.get('page') ? Number(this.qp.get('page')) : 1);
    this.initialSyncDone = true;
  }

  /** Keeps the URL in sync with the current filters (query-param values, not history entries —
   * `replaceUrl` avoids spamming browser history on every page-through/filter tweak). */
  private syncUrl(page: number): void {
    if (!this.initialSyncDone) return;
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        year: this.year !== null ? this.year : '',
        contains: this.contains || null,
        bank: this.bank || null,
        status: this.status || null,
        categoryId: this.categoryId !== '' ? this.categoryId : null,
        sort: this.sort !== 'date' ? this.sort : null,
        page: page !== 1 ? page : null,
      },
      replaceUrl: true,
    });
  }

  protected filter(): TransactionFilter {
    return {
      year: this.year,
      contains: this.contains,
      bank: this.bank,
      status: this.status,
      categoryId: this.categoryId === '' ? null : this.categoryId,
      sort: this.sort,
    };
  }

  protected load(page: number): void {
    this.syncUrl(page);
    this.loading.set(true);
    this.api.getTransactions(this.filter(), page, this.pageSize).subscribe({
      next: (d) => {
        this.data.set(d);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected showNeedsReview(): void {
    this.status = 'NeedsReview';
    this.load(1);
  }

  protected setCategory(t: TransactionDto, value: string): void {
    if (value === this.manageCategoriesOption) {
      this.pendingCategoryTx = t;
      this.categoryDialog().open();
      return;
    }
    const categoryId = value === '' ? null : Number(value);
    this.api.patchTransaction(t.id, { categoryId }).subscribe(() => this.load(this.data()?.page ?? 1));
  }

  /** New category created via the dialog — assign it straight to the transaction that
   * triggered it, so the user never has to click back into the row. */
  protected onCategoryCreated(category: CategoryDto): void {
    const t = this.pendingCategoryTx;
    this.pendingCategoryTx = null;
    if (!t) return;
    this.api.patchTransaction(t.id, { categoryId: category.id }).subscribe(() => this.load(this.data()?.page ?? 1));
  }

  protected setStatus(t: TransactionDto, status: ClassificationStatus): void {
    this.api
      .patchTransaction(t.id, { categoryId: t.categoryId, status })
      .subscribe(() => this.load(this.data()?.page ?? 1));
  }

  protected delete(t: TransactionDto): void {
    if (!confirm(`Delete transaction "${t.counterpartyName ?? t.purpose ?? t.id}"?`)) return;
    this.api.deleteTransaction(t.id).subscribe(() => this.load(this.data()?.page ?? 1));
  }

  protected createContract(t: TransactionDto): void {
    this.api.createContractFromTransaction(t.id).subscribe(() => this.load(this.data()?.page ?? 1));
  }

  protected exportUrl(kind: 'xlsx' | 'csv'): string {
    return this.api.exportUrl(kind, this.filter());
  }
}
