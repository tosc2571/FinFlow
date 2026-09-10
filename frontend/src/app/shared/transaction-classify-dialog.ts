import { Component, EventEmitter, Output, inject, signal, viewChild } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { CategoriesService } from '../core/categories.service';
import { RulesService } from '../core/rules.service';
import { onEnterSubmit } from './keyboard';
import { QuickCreateCategoryDialog } from './quick-create-category-dialog';
import { RuleEditDialog } from './rule-edit-dialog';
import { RulePickerDialog } from './rule-picker-dialog';
import { CategoryDto, RuleDto, TransactionDto } from './models';

/**
 * Single entry point for "classify this transaction" (#55) — category and matching rule used to
 * live in the same table cell as two different interaction styles (an instant-apply `<select>`
 * next to a chip that opened a modal). This dialog folds both into one place; category changes
 * apply on Save, rule actions (create/edit/pick) still apply and reclassify immediately, same as
 * #51/#53, and refresh this dialog's own view of the transaction afterward.
 */
@Component({
  selector: 'app-transaction-classify-dialog',
  imports: [FormsModule, DatePipe, DecimalPipe, QuickCreateCategoryDialog, RuleEditDialog, RulePickerDialog],
  templateUrl: './transaction-classify-dialog.html',
})
export class TransactionClassifyDialog {
  private api = inject(ApiService);
  protected categoriesService = inject(CategoriesService);
  private rulesService = inject(RulesService);
  protected categoryDialog = viewChild.required(QuickCreateCategoryDialog);
  protected ruleDialog = viewChild.required(RuleEditDialog);
  protected rulePicker = viewChild.required(RulePickerDialog);

  /** Fires after Save (category change) or after a rule create/edit — either way the caller
   * should reload its transaction list. */
  @Output() changed = new EventEmitter<void>();

  protected readonly visible = signal(false);
  protected readonly saving = signal(false);
  protected transaction: TransactionDto | null = null;
  protected categoryId: number | '' = '';

  /** Sentinel option value for the category <select>'s "new category" entry — distinct
   * from any real category id (number) or '' (clear category). */
  protected readonly manageCategoriesOption = '__manage__';

  constructor() {
    // The rule picker (nested below) reads this signal directly — ensure it's loaded even if
    // this is the first dialog opened this session, rather than relying on the host page.
    this.rulesService.ensureLoaded();
  }

  open(t: TransactionDto): void {
    this.transaction = t;
    this.categoryId = t.categoryId ?? '';
    this.visible.set(true);
  }

  protected onDialogEnter(event: Event): void {
    onEnterSubmit(event, () => this.save());
  }

  protected onCategorySelect(value: number | string): void {
    if (value === this.manageCategoriesOption) {
      this.categoryDialog().open();
      return;
    }
    this.categoryId = value as number | '';
  }

  protected onCategoryCreated(category: CategoryDto): void {
    this.categoryId = category.id;
  }

  protected editRule(): void {
    const t = this.transaction;
    if (!t || t.matchedRuleId === null) return;
    this.api.getRule(t.matchedRuleId).subscribe((rule) => {
      this.ruleDialog().open(rule, { counterpartyName: t.counterpartyName, purpose: t.purpose });
    });
  }

  protected openRulePicker(): void {
    this.rulePicker().open();
  }

  protected onRulePicked(rule: RuleDto): void {
    const t = this.transaction;
    if (!t) return;
    this.ruleDialog().open(rule, { counterpartyName: t.counterpartyName, purpose: t.purpose });
  }

  protected onRuleCreateNew(): void {
    const t = this.transaction;
    if (!t) return;
    this.ruleDialog().open(null, { counterpartyName: t.counterpartyName, purpose: t.purpose }, { pattern: t.counterpartyName ?? '' });
  }

  /** The rule editor already applied the category and reclassified server-side — close outright
   * rather than leaving this dialog open. Leaving it open invited clicking this dialog's own
   * Save afterward, which called patchTransaction without a status and forced ManualOverride,
   * clobbering the Auto status the rule save had just set (and, since ReclassifyAll skips
   * ManualOverride transactions, freezing that transaction out of future reclassification too). */
  protected onRuleSaved(): void {
    this.visible.set(false);
    this.changed.emit();
  }

  protected cancel(): void {
    this.visible.set(false);
  }

  protected save(): void {
    const t = this.transaction;
    if (!t) return;
    const categoryId = this.categoryId === '' ? null : this.categoryId;
    this.saving.set(true);
    this.api.patchTransaction(t.id, { categoryId }).subscribe(() => {
      this.saving.set(false);
      this.visible.set(false);
      this.changed.emit();
    });
  }
}
