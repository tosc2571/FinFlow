import { Component, inject, signal, viewChild } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, RuleRequest } from '../../core/api.service';
import { CategoriesService } from '../../core/categories.service';
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
  protected categoryDialog = viewChild.required(QuickCreateCategoryDialog);
  private testTimer: ReturnType<typeof setTimeout> | null = null;

  protected onToolbarEnter(event: Event): void {
    onEnterSubmit(event, () => this.save());
  }

  /** Sentinel option value for the category <select>'s "new category" entry — distinct
   * from any real category id (number) or the empty "— choose —" placeholder. */
  protected readonly manageCategoriesOption = '__manage__';

  protected readonly rules = signal<RuleDto[]>([]);
  protected readonly testResult = signal<PatternTestResult | null>(null);
  protected readonly testError = signal<string | null>(null);
  protected readonly reclassifyResult = signal<ReclassifyResult | null>(null);

  // Form state (plain fields for ngModel).
  protected editingId: number | null = null;
  protected pattern = '';
  protected categoryId: number | '' = '';
  protected status: RuleStatus = 'NeedsReview';
  protected priority = 100;
  protected isActive = true;

  constructor() {
    this.loadRules();
    this.categoriesService.ensureLoaded();
  }

  protected loadRules(): void {
    this.api.getRules().subscribe((r) => this.rules.set(r));
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
    this.categoryId = rule.categoryId;
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

  protected get canSave(): boolean {
    return this.pattern.trim() !== '' && this.categoryId !== '';
  }

  protected save(): void {
    if (!this.canSave) return;
    const req: RuleRequest = {
      pattern: this.pattern,
      categoryId: this.categoryId as number,
      status: this.status,
      priority: this.priority,
      isActive: this.isActive,
    };
    const call = this.editingId === null ? this.api.createRule(req) : this.api.updateRule(this.editingId, req);
    call.subscribe(() => {
      this.resetForm();
      this.loadRules();
    });
  }

  protected delete(rule: RuleDto): void {
    if (!confirm(`Delete rule "${rule.pattern}"?`)) return;
    this.api.deleteRule(rule.id).subscribe(() => this.loadRules());
  }

  protected reclassify(): void {
    if (!confirm('Re-run classification over all transactions? Manual overrides are preserved.')) return;
    this.api.reclassify().subscribe((r) => this.reclassifyResult.set(r));
  }
}
