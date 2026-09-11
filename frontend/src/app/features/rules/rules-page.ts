import { Component, inject, signal, viewChild } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, RuleRequest } from '../../core/api.service';
import { CategoriesService } from '../../core/categories.service';
import { RulesService } from '../../core/rules.service';
import { onEnterSubmit } from '../../shared/keyboard';
import { QuickCreateCategoryDialog } from '../../shared/quick-create-category-dialog';
import { CategoryDto, PatternTestResult, ReclassifyResult, RuleDto, RuleStatus } from '../../shared/models';

@Component({
  selector: 'app-rules-page',
  imports: [FormsModule, DatePipe, DecimalPipe, QuickCreateCategoryDialog],
  templateUrl: './rules-page.html',
})
export class RulesPage {
  private api = inject(ApiService);
  protected categoriesService = inject(CategoriesService);
  protected rulesService = inject(RulesService);
  protected categoryDialog = viewChild.required(QuickCreateCategoryDialog);
  private testTimer: ReturnType<typeof setTimeout> | null = null;

  protected onToolbarEnter(event: Event): void {
    onEnterSubmit(event, () => this.save());
  }

  /** Sentinel option value for the category <select>'s "new category" entry — distinct
   * from any real category id (number) or the empty "— choose —" placeholder. */
  protected readonly manageCategoriesOption = '__manage__';

  protected readonly testResult = signal<PatternTestResult | null>(null);
  protected readonly testError = signal<string | null>(null);
  protected readonly reclassifyResult = signal<ReclassifyResult | null>(null);

  /** Text filter (#53) — matches against the pattern and the target category's name. */
  protected filterText = '';

  protected get filteredRules(): RuleDto[] {
    const term = this.filterText.trim().toLowerCase();
    const rules = this.rulesService.rules();
    if (!term) return rules;
    return rules.filter(
      (r) => r.pattern.toLowerCase().includes(term) || (r.categoryName ?? '').toLowerCase().includes(term),
    );
  }

  // Form state (plain fields for ngModel).
  protected editingId: number | null = null;
  protected pattern = '';
  protected categoryId: number | '' = '';
  protected status: RuleStatus = 'NeedsReview';
  // No UI for this — every rule is created at the same priority in practice, so the field just
  // cluttered the form/table. Still sent to the backend (which still orders matches by it) and
  // still round-tripped by edit() below, in case a rule somehow ends up with a different value.
  protected priority = 100;
  protected isActive = true;

  constructor() {
    this.rulesService.ensureLoaded();
    this.categoriesService.ensureLoaded();
  }

  /** Live preview: debounce, then dry-run the pattern against existing transactions. */
  protected onPatternInput(): void {
    if (this.testTimer) clearTimeout(this.testTimer);
    const pattern = this.pattern;
    if (!pattern.trim()) {
      this.testResult.set(null);
      this.testError.set(null);
      return;
    }
    this.testTimer = setTimeout(() => this.runTest(pattern), 400);
  }

  private runTest(pattern: string): void {
    this.api.testPattern(pattern).subscribe({
      next: (result) => {
        this.testResult.set(result);
        this.testError.set(null);
      },
      error: (err) => {
        this.testResult.set(null);
        this.testError.set(err?.error?.error ?? 'Pattern test failed.');
      },
    });
  }

  protected onCategoryChange(value: number | string): void {
    if (value === this.manageCategoriesOption) {
      this.categoryDialog().open();
      return;
    }
    this.categoryId = value as number | '';
  }

  /** New category created via the dialog — select it in the form directly. */
  protected onCategoryCreated(category: CategoryDto): void {
    this.categoryId = category.id;
  }

  protected edit(rule: RuleDto): void {
    this.editingId = rule.id;
    this.pattern = rule.pattern;
    this.categoryId = rule.categoryId ?? '';
    this.status = rule.status;
    this.priority = rule.priority;
    this.isActive = rule.isActive;
    this.onPatternInput();
  }

  protected resetForm(): void {
    this.editingId = null;
    this.pattern = '';
    this.categoryId = '';
    this.status = 'NeedsReview';
    this.priority = 100;
    this.isActive = true;
    this.testResult.set(null);
    this.testError.set(null);
  }

  /** Internal-transfer rules need no category — every other status still requires one. */
  protected get needsCategory(): boolean {
    return this.status !== 'InternalTransfer';
  }

  protected get canSave(): boolean {
    return this.pattern.trim() !== '' && (!this.needsCategory || this.categoryId !== '');
  }

  protected save(): void {
    if (!this.canSave) return;
    const req: RuleRequest = {
      pattern: this.pattern,
      categoryId: this.needsCategory ? (this.categoryId as number) : null,
      status: this.status,
      priority: this.priority,
      isActive: this.isActive,
    };
    const call = this.editingId === null ? this.rulesService.create(req) : this.rulesService.update(this.editingId, req);
    call.subscribe(() => this.resetForm());
  }

  protected delete(rule: RuleDto): void {
    if (!confirm(`Delete rule "${rule.pattern}"?`)) return;
    this.rulesService.delete(rule.id).subscribe();
  }

  protected reclassify(): void {
    if (!confirm('Re-run classification over all transactions? Manual overrides are preserved.')) return;
    this.api.reclassify().subscribe((r) => this.reclassifyResult.set(r));
  }
}
