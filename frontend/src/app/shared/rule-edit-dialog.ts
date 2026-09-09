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

/**
 * Quick "edit this rule" modal, opened from the Categories page (#51: which rule leads to this
 * category) and the Transactions page (which rule classified this row / attach this row to an
 * existing rule) — editing a rule without leaving whatever page you were reviewing.
 *
 * Always edits an existing rule (never creates one) — the Rules page's own inline form remains
 * the place to create new rules from scratch.
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
  private ruleId = 0;
  protected pattern = '';
  protected categoryId: number | '' = '';
  protected status: RuleStatus = 'NeedsReview';
  protected priority = 100;
  protected isActive = true;

  /** Set only when opened from a specific transaction — drives the live match indicator. */
  protected matchContext: RuleMatchContext | null = null;

  open(rule: RuleDto, matchContext: RuleMatchContext | null = null): void {
    this.ruleId = rule.id;
    this.pattern = rule.pattern;
    this.categoryId = rule.categoryId;
    this.status = rule.status;
    this.priority = rule.priority;
    this.isActive = rule.isActive;
    this.matchContext = matchContext;
    this.visible.set(true);
  }

  /** null when there's no transaction to check against (case A/B without one); otherwise
   * whether the pattern, as currently typed, matches that transaction's counterparty+purpose —
   * same "counterparty + purpose" text RuleSet.MatchFor matches against server-side. */
  protected get matchesTransaction(): boolean | null {
    if (!this.matchContext) return null;
    const text = `${this.matchContext.counterpartyName ?? ''} ${this.matchContext.purpose ?? ''}`.trim();
    try {
      return new RegExp(this.pattern, 'i').test(text);
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
    this.saving.set(true);
    this.rulesService.update(this.ruleId, req).subscribe((rule) => {
      // Make the edit visible right away wherever it was opened from, not just next time
      // someone clicks "Re-run classification" on the Rules page.
      this.api.reclassify().subscribe(() => {
        this.saving.set(false);
        this.visible.set(false);
        this.saved.emit(rule);
      });
    });
  }
}
