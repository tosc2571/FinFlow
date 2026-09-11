import { Component, computed, effect, inject, signal, viewChild } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AccountsService } from '../../core/accounts.service';
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
  private accountsService = inject(AccountsService);
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

  // Bulk selection (persists across pages within the same filter — see `load`'s keepSelection
  // param — so "select all matching filter" can span more rows than fit on one page).
  protected readonly selectedIds = signal<ReadonlySet<number>>(new Set());
  protected readonly selectingAllMatching = signal(false);
  protected bulkCategoryId: number | null | '' = '';

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

    // Creating/editing an account retroactively re-flags matching transactions as
    // InternalTransfer on the backend (TransferDetectionService) — without this, a Transactions
    // tab kept alive by KeepAliveRouteReuseStrategy would go on showing the stale NeedsReview
    // status until some unrelated reload happened to fire.
    this.accountsService.ensureLoaded();
    let skipFirstAccountsChange = true;
    effect(() => {
      this.accountsService.accounts();
      if (skipFirstAccountsChange) {
        skipFirstAccountsChange = false;
        return;
      }
      this.load(this.data()?.page ?? 1, true);
    });
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

  /** `keepSelection`: pagination and single-row actions (classify/status/delete/contract) keep
   * the current bulk selection; an actual filter change (Apply, Enter, "Needs review", a link
   * into this page with different query params) starts a fresh one — the old selection wouldn't
   * mean much once what's being filtered has changed. */
  protected load(page: number, keepSelection = false): void {
    if (!keepSelection) {
      this.selectedIds.set(new Set());
      this.selectingAllMatching.set(false);
    }
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
    this.load(this.data()?.page ?? 1, true);
  }

  protected setStatus(t: TransactionDto, status: ClassificationStatus): void {
    this.api
      .patchTransaction(t.id, { categoryId: t.categoryId, status })
      .subscribe(() => this.load(this.data()?.page ?? 1, true));
  }

  protected delete(t: TransactionDto): void {
    if (!confirm(`Delete transaction "${t.counterpartyName ?? t.purpose ?? t.id}"?`)) return;
    this.api.deleteTransaction(t.id).subscribe(() => {
      if (this.selectedIds().has(t.id)) {
        const next = new Set(this.selectedIds());
        next.delete(t.id);
        this.selectedIds.set(next);
      }
      this.load(this.data()?.page ?? 1, true);
    });
  }

  protected createContract(t: TransactionDto): void {
    this.api.createContractFromTransaction(t.id).subscribe(() => this.load(this.data()?.page ?? 1, true));
  }

  protected exportUrl(kind: 'xlsx' | 'csv'): string {
    return this.api.exportUrl(kind, this.filter());
  }

  // --- bulk selection & categorize ---

  protected isSelected(id: number): boolean {
    return this.selectedIds().has(id);
  }

  protected toggleSelect(id: number): void {
    const next = new Set(this.selectedIds());
    if (next.has(id)) next.delete(id);
    else next.add(id);
    this.selectedIds.set(next);
    this.selectingAllMatching.set(false);
  }

  /** Whether every row on the current page is selected — drives the header checkbox's state. */
  protected get allOnPageSelected(): boolean {
    const items = this.data()?.items ?? [];
    return items.length > 0 && items.every((t) => this.selectedIds().has(t.id));
  }

  protected toggleSelectAllOnPage(): void {
    const items = this.data()?.items ?? [];
    const next = new Set(this.selectedIds());
    if (this.allOnPageSelected) {
      for (const t of items) next.delete(t.id);
    } else {
      for (const t of items) next.add(t.id);
    }
    this.selectedIds.set(next);
    this.selectingAllMatching.set(false);
  }

  /** "Select all N matching filter" — beyond just this page, everything the current filter
   * matches, mirroring the classic "select all conversations" pattern for a filtered list. */
  protected selectAllMatching(): void {
    this.selectingAllMatching.set(true);
    this.api.getTransactionIds(this.filter()).subscribe((ids) => {
      this.selectedIds.set(new Set(ids));
    });
  }

  protected clearSelection(): void {
    this.selectedIds.set(new Set());
    this.selectingAllMatching.set(false);
  }

  protected applyBulkCategory(): void {
    if (this.bulkCategoryId === '') return;
    const ids = [...this.selectedIds()];
    if (ids.length === 0) return;
    this.api.bulkCategorizeTransactions(ids, this.bulkCategoryId).subscribe(() => {
      this.bulkCategoryId = '';
      this.selectedIds.set(new Set());
      this.selectingAllMatching.set(false);
      this.load(this.data()?.page ?? 1, true);
    });
  }
}
