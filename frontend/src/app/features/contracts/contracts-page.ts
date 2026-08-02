import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, ContractRequest } from '../../core/api.service';
import { onEnterSubmit } from '../../shared/keyboard';
import {
  CategoryDto,
  ContractDetail,
  ContractDto,
  ContractPeriod,
  OCCURRENCE_STATUS_LABELS,
  OccurrenceStatus,
  PERIOD_LABELS,
} from '../../shared/models';

@Component({
  selector: 'app-contracts-page',
  imports: [FormsModule, DatePipe, DecimalPipe],
  templateUrl: './contracts-page.html',
})
export class ContractsPage {
  private api = inject(ApiService);

  protected onToolbarEnter(event: Event): void {
    onEnterSubmit(event, () => this.save());
  }

  protected readonly periods: ContractPeriod[] = ['Monthly', 'Quarterly', 'SemiAnnually', 'Annually'];
  protected readonly periodLabels = PERIOD_LABELS;
  protected readonly occurrenceStatusLabels = OCCURRENCE_STATUS_LABELS;

  protected readonly contracts = signal<ContractDto[]>([]);
  protected readonly categories = signal<CategoryDto[]>([]);
  protected readonly detail = signal<ContractDetail | null>(null);

  protected editingId: number | null = null;
  protected name = '';
  protected nominalAmount = 0;
  protected period: ContractPeriod = 'Monthly';
  protected anchorDate = '';
  protected counterpartyPattern = '';
  protected amountTolerancePercent = 5;
  protected categoryId: number | '' = '';
  protected isActive = true;

  constructor() {
    this.load();
    this.api.getCategories().subscribe((c) => this.categories.set(c));
  }

  protected load(): void {
    this.api.getContracts().subscribe((c) => this.contracts.set(c));
  }

  /** Cost overview: monthly-equivalent totals across active contracts, split by sign. */
  protected readonly monthlyIncome = computed(() =>
    this.contracts()
      .filter((c) => c.isActive && c.monthlyEquivalent > 0)
      .reduce((sum, c) => sum + c.monthlyEquivalent, 0),
  );

  protected readonly monthlyExpenses = computed(() =>
    this.contracts()
      .filter((c) => c.isActive && c.monthlyEquivalent < 0)
      .reduce((sum, c) => sum + c.monthlyEquivalent, 0),
  );

  protected edit(contract: ContractDto): void {
    this.editingId = contract.id;
    this.name = contract.name;
    this.nominalAmount = contract.nominalAmount;
    this.period = contract.period;
    this.anchorDate = contract.anchorDate;
    this.counterpartyPattern = contract.counterpartyPattern;
    this.amountTolerancePercent = contract.amountTolerance * 100;
    this.categoryId = contract.categoryId ?? '';
    this.isActive = contract.isActive;
  }

  protected resetForm(): void {
    this.editingId = null;
    this.name = '';
    this.nominalAmount = 0;
    this.period = 'Monthly';
    this.anchorDate = '';
    this.counterpartyPattern = '';
    this.amountTolerancePercent = 5;
    this.categoryId = '';
    this.isActive = true;
  }

  protected save(): void {
    if (!this.name.trim() || !this.counterpartyPattern.trim() || !this.anchorDate) return;
    const req: ContractRequest = {
      name: this.name,
      nominalAmount: this.nominalAmount,
      period: this.period,
      anchorDate: this.anchorDate,
      counterpartyPattern: this.counterpartyPattern,
      amountTolerance: this.amountTolerancePercent / 100,
      categoryId: this.categoryId === '' ? null : this.categoryId,
      isActive: this.isActive,
    };
    const call =
      this.editingId === null ? this.api.createContract(req) : this.api.updateContract(this.editingId, req);
    call.subscribe(() => {
      this.resetForm();
      this.load();
    });
  }

  protected delete(contract: ContractDto): void {
    if (!confirm(`Delete contract "${contract.name}"? Linked transactions are kept, just unlinked.`)) return;
    if (this.detail()?.contract.id === contract.id) this.detail.set(null);
    this.api.deleteContract(contract.id).subscribe(() => this.load());
  }

  protected showDetail(contract: ContractDto): void {
    if (this.detail()?.contract.id === contract.id) {
      this.detail.set(null);
      return;
    }
    this.api.getContract(contract.id).subscribe((d) => this.detail.set(d));
  }

  protected occurrenceBadgeClass(status: OccurrenceStatus): string {
    switch (status) {
      case 'Matched':
        return 'badge auto';
      case 'AmountDeviation':
        return 'badge needsreview';
      case 'Missing':
        return 'badge failed';
      case 'Upcoming':
        return 'badge ignored';
    }
  }
}
