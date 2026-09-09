import { Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RulesService } from '../core/rules.service';
import { RuleDto } from './models';

/**
 * Filterable "pick an existing rule" modal — the entry point for #51's case C: attaching an
 * uncategorized transaction to an existing rule instead of creating a new one. Picking a rule
 * closes this dialog; the caller opens RuleEditDialog with the pick to extend its pattern.
 */
@Component({
  selector: 'app-rule-picker-dialog',
  imports: [FormsModule],
  templateUrl: './rule-picker-dialog.html',
})
export class RulePickerDialog {
  protected rulesService = inject(RulesService);

  @Output() picked = new EventEmitter<RuleDto>();
  /** No existing rule fits — the caller should open the editor in create mode instead (#53). */
  @Output() createNew = new EventEmitter<void>();

  protected readonly visible = signal(false);
  protected filterText = '';

  open(): void {
    this.filterText = '';
    this.visible.set(true);
  }

  protected cancel(): void {
    this.visible.set(false);
  }

  protected pick(rule: RuleDto): void {
    this.visible.set(false);
    this.picked.emit(rule);
  }

  protected pickCreateNew(): void {
    this.visible.set(false);
    this.createNew.emit();
  }

  protected get filteredRules(): RuleDto[] {
    const term = this.filterText.trim().toLowerCase();
    const rules = this.rulesService.rules();
    if (!term) return rules;
    return rules.filter((r) => r.pattern.toLowerCase().includes(term) || r.categoryName.toLowerCase().includes(term));
  }
}
