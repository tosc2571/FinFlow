import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { CategoriesService } from '../../core/categories.service';
import { onEnterSubmit } from '../../shared/keyboard';
import { TransactionClassifyDialog } from '../../shared/transaction-classify-dialog';
import {
  ClassificationStatus,
  PagedTransactions,
  STATUS_LABELS,
  TransactionDto,
  TransactionFilter,
} from '../../shared/models';

@Component({
  selector: 'app-transactions-page',
  imports: [FormsModule, DatePipe, DecimalPipe, TransactionClassifyDialog],
  templateUrl: './transactions-page.html',
})
export class TransactionsPage {
  private api = inject(ApiService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  protected categoriesService = inject(CategoriesService);
  protected classifyDialog = viewChild.required(TransactionClassifyDialog);

  protected onToolbarEnter(event: Event): void {
    onEnterSubmit(event, () => this.load(1));
  }

  protected readonly statusLabels = STATUS_LABELS;
  protected readonly statuses: ClassificationStatus[] = [
    'Auto',
    'NeedsReview',
    'Ignored',
    'ManualOverride',
    'InternalTransfer',
  ];

  // Filter form state (plain fields — ngModel; zoneless CD runs after template events). Re-seeded
  // from the URL's query params on every change, not just once — see #35 for why the URL is the
  // source of truth, and #68: KeepAliveRouteReuseStrategy keeps this component instance alive
  // across navigation, so a link into this page with different query params (e.g. a category's
  // transaction count) needs an active subscription to be picked up, not just a one-time read at
  // construction — otherwise the page silently keeps showing the previous filter/data until a
  // full reload.
  protected year: number | null = null;
  protected contains = '';
  protected bank = '';
  protected status: ClassificationStatus | '' = '';
  protected categoryId: number | '' = '';
  protected sort = 'date';

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
    // queryParamMap emits the current value synchronously on subscribe, so this also does the
    // initial parse — then keeps firing on every later navigation into this same, kept-alive
    // component instance (e.g. the Categories page's transaction-count link).
    this.route.queryParamMap.subscribe((qp) => {
      this.year = qp.has('year') ? (qp.get('year') ? Number(qp.get('year')) : null) : new Date().getFullYear();
      this.contains = qp.get('contains') ?? '';
      this.bank = qp.get('bank') ?? '';
      this.status = (qp.get('status') ?? '') as ClassificationStatus | '';
      this.categoryId = qp.get('categoryId') ? Number(qp.get('categoryId')) : '';
      this.sort = qp.get('sort') ?? 'date';
      this.load(qp.get('page') ? Number(qp.get('page')) : 1);
    });
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

  /** Category and matching rule are now one combined dialog (#55) instead of an inline
   * <select> next to a separate rule chip/modal. */
  protected classify(t: TransactionDto): void {
    this.classifyDialog().open(t);
  }

  /** The dialog already applied the category change / re-ran classification — just reload. */
  protected onClassifyChanged(): void {
    this.load(this.data()?.page ?? 1);
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
