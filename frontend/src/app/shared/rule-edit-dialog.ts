import { Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService, RuleRequest } from '../core/api.service';
import { CategoriesService } from '../core/categories.service';
import { RulesService } from '../core/rules.service';
import { onEnterSubmit } from './keyboard';
import { RuleDto, RuleStatus } from './models';

/** Just enough of a transaction to check "does the pattern match this one" — see matchesTransaction. */
export interface RuleMatchContext {
  counterpartyName: string | null;
  purpose: string | null;
}

/** Defaults to prefill when opening in create mode (#53) — e.g. the counterparty name of the
 * transaction that had no matching rule, as a starting point for a brand new pattern. */
export interface RuleCreateDefaults {
  pattern?: string;
  categoryId?: number;
}

/**
 * Quick "create or edit a rule" modal, opened from the Categories page and the Transactions page
 * (#51: which rule leads to this category; #53: also let a new rule be created from there) —
 * without leaving whatever page you were reviewing.
 */
@Component({
  selector: 'app-rule-edit-dialog',
  imports: [FormsModule],
  templateUrl: './rule-edit-dialog.html',
})
export class RuleEditDialog {
  private api = inject(ApiService);
  private rulesService = inject(RulesService);
  protected categoriesService = inject(CategoriesService);

  @Output() saved = new EventEmitter<RuleDto>();

  protected readonly visible = signal(false);
  protected readonly saving = signal(false);
  /** null while creating a new rule; the existing rule's id while editing one. */
  private ruleId: number | null = null;
  protected pattern = '';
  protected categoryId: number | '' = '';
  protected status: RuleStatus = 'NeedsReview';
  protected priority = 100;
  protected isActive = true;

  /** Set only when opened from a specific transaction — drives the live match indicator. */
  protected matchContext: RuleMatchContext | null = null;

  /** Opens in edit mode for an existing rule, or create mode when `rule` is null. */
  open(rule: RuleDto | null, matchContext: RuleMatchContext | null = null, createDefaults: RuleCreateDefaults = {}): void {
    this.ruleId = rule?.id ?? null;
    this.pattern = rule?.pattern ?? createDefaults.pattern ?? '';
    this.categoryId = rule?.categoryId ?? createDefaults.categoryId ?? '';
    // Creating from a specific transaction (case C) means "auto-classify this going forward" —
    // defaulting to NeedsReview would leave that very transaction looking unresolved. Creating
    // from Categories (no transaction in view) keeps the cautious NeedsReview default, same as
    // the standalone Rules page.
    this.status = rule?.status ?? (matchContext ? 'Auto' : 'NeedsReview');
    this.priority = rule?.priority ?? 100;
    this.isActive = rule?.isActive ?? true;
    this.matchContext = matchContext;
    this.visible.set(true);
  }

  protected get isCreating(): boolean {
    return this.ruleId === null;
  }

  /** null when there's no transaction to check against (case A/B without one); otherwise
   * whether the pattern, as currently typed, matches that transaction's counterparty+purpose —
   * mirrors RuleSet.MatchFor server-side, including stripping all whitespace from both sides
   * first (some bank exports insert a stray space mid-word — see #66 — so matching ignores
   * whitespace entirely rather than trying to guess where a "real" space belongs). */
  protected get matchesTransaction(): boolean | null {
    if (!this.matchContext) return null;
    const text = `${this.matchContext.counterpartyName ?? ''} ${this.matchContext.purpose ?? ''}`.replace(/\s+/g, '');
    const pattern = this.pattern.replace(/\s+/g, '');
    try {
      return new RegExp(pattern, 'i').test(text);
    } catch {
      return false;
    }
  }

  protected onDialogEnter(event: Event): void {
    onEnterSubmit(event, () => this.save());
  }

  protected cancel(): void {
    this.visible.set(false);
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
    this.saving.set(true);
    const call = this.ruleId === null ? this.rulesService.create(req) : this.rulesService.update(this.ruleId, req);
    call.subscribe((rule) => {
      // Make the change visible right away wherever it was opened from, not just next time
      // someone clicks "Re-run classification" on the Rules page.
      this.api.reclassify().subscribe(() => {
        this.saving.set(false);
        this.visible.set(false);
        this.saved.emit(rule);
      });
    });
  }
}
