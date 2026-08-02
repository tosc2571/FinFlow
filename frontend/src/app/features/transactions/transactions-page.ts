import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
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
  imports: [FormsModule, DatePipe, DecimalPipe],
  templateUrl: './transactions-page.html',
})
export class TransactionsPage {
  private api = inject(ApiService);
  private router = inject(Router);

  /** Sentinel option value for the per-row category <select>'s "manage categories" entry —
   * distinct from any real category id (number) or null (clear category). */
  protected readonly manageCategoriesOption = '__manage__';

  protected readonly statusLabels = STATUS_LABELS;
  protected readonly statuses: ClassificationStatus[] = [
    'Auto',
    'NeedsReview',
    'Ignored',
    'ManualOverride',
    'InternalTransfer',
  ];

  // Filter form state (plain fields — ngModel; zoneless CD runs after template events).
  protected year: number | null = new Date().getFullYear();
  protected contains = '';
  protected bank = '';
  protected status: ClassificationStatus | '' = '';
  protected categoryId: number | '' = '';
  protected sort = 'date';

  protected readonly pageSize = 50;
  protected readonly categories = signal<CategoryDto[]>([]);
  protected readonly data = signal<PagedTransactions | null>(null);
  protected readonly loading = signal(false);

  protected readonly totalPages = computed(() => {
    const d = this.data();
    return d ? Math.max(1, Math.ceil(d.total / d.pageSize)) : 1;
  });

  constructor() {
    this.api.getCategories().subscribe((c) => this.categories.set(c));
    this.load(1);
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
      this.router.navigate(['/categories']);
      return;
    }
    const categoryId = value === '' ? null : Number(value);
    this.api.patchTransaction(t.id, { categoryId }).subscribe(() => this.load(this.data()?.page ?? 1));
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
