import { Component, inject, signal, viewChild } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CategoryRequest } from '../../core/api.service';
import { CategoriesService } from '../../core/categories.service';
import { RulesService } from '../../core/rules.service';
import { onEnterSubmit } from '../../shared/keyboard';
import { RuleEditDialog } from '../../shared/rule-edit-dialog';
import { CategoryDto, RuleDto } from '../../shared/models';

@Component({
  selector: 'app-categories-page',
  imports: [FormsModule, NgTemplateOutlet, RouterLink, RuleEditDialog],
  templateUrl: './categories-page.html',
})
export class CategoriesPage {
  protected categoriesService = inject(CategoriesService);
  protected rulesService = inject(RulesService);
  protected ruleDialog = viewChild.required(RuleEditDialog);

  protected onToolbarEnter(event: Event): void {
    onEnterSubmit(event, () => this.save());
  }

  protected editingId: number | null = null;
  protected name = '';
  protected parentCategoryId: number | '' = '';
  protected isIncome = false;

  /** Text filter (#53) — matches against the category name. */
  protected filterText = '';

  /** Collapsed parent category ids (#56) — everything is expanded by default; only ids
   * explicitly toggled closed end up here. */
  private collapsedIds = signal<ReadonlySet<number>>(new Set());

  constructor() {
    this.categoriesService.ensureLoaded();
    this.rulesService.ensureLoaded();
  }

  /** Categories matching the text filter, plus any ancestor of a match — so a matching
   * sub-category doesn't lose its parent row and become orphaned in the tree. */
  protected get filteredCategories(): CategoryDto[] {
    const term = this.filterText.trim().toLowerCase();
    const all = this.categoriesService.categories();
    if (!term) return all;
    const keepIds = new Set(all.filter((c) => c.name.toLowerCase().includes(term)).map((c) => c.id));
    for (const c of all) {
      if (keepIds.has(c.id) && c.parentCategoryId !== null) keepIds.add(c.parentCategoryId);
    }
    return all.filter((c) => keepIds.has(c.id));
  }

  protected get topLevelCategories(): CategoryDto[] {
    return this.filteredCategories.filter((c) => c.parentCategoryId === null).sort((a, b) => a.name.localeCompare(b.name));
  }

  protected childrenOf(parent: CategoryDto): CategoryDto[] {
    return this.filteredCategories.filter((c) => c.parentCategoryId === parent.id).sort((a, b) => a.name.localeCompare(b.name));
  }

  /** While filtering, force everything open so matches stay visible regardless of prior state. */
  protected isExpanded(category: CategoryDto): boolean {
    return this.filterText.trim() !== '' || !this.collapsedIds().has(category.id);
  }

  protected toggleExpand(category: CategoryDto): void {
    this.collapsedIds.update((ids) => {
      const next = new Set(ids);
      if (next.has(category.id)) {
        next.delete(category.id);
      } else {
        next.add(category.id);
      }
      return next;
    });
  }

  protected parentOptions(): CategoryDto[] {
    return this.categoriesService.categories().filter((c) => c.id !== this.editingId);
  }

  /** Rules assigned to this category (#51) — clicking one opens it for editing right here. */
  protected rulesFor(category: CategoryDto): RuleDto[] {
    return this.rulesService.rules().filter((r) => r.categoryId === category.id);
  }

  protected editRule(rule: RuleDto): void {
    this.ruleDialog().open(rule);
  }

  /** No rule fits — open the editor in create mode with this category preselected (#53). */
  protected newRuleFor(category: CategoryDto): void {
    this.ruleDialog().open(null, null, { categoryId: category.id });
  }

  protected edit(category: CategoryDto): void {
    this.editingId = category.id;
    this.name = category.name;
    this.parentCategoryId = category.parentCategoryId ?? '';
    this.isIncome = category.isIncome;
  }

  protected resetForm(): void {
    this.editingId = null;
    this.name = '';
    this.parentCategoryId = '';
    this.isIncome = false;
  }

  protected save(): void {
    if (!this.name.trim()) return;
    const req: CategoryRequest = {
      name: this.name,
      parentCategoryId: this.parentCategoryId === '' ? null : this.parentCategoryId,
      isIncome: this.isIncome,
      // No longer user-editable (#56) — ordering is alphabetical now, within each tree level.
      sortOrder: 0,
    };
    const call =
      this.editingId === null
        ? this.categoriesService.create(req)
        : this.categoriesService.update(this.editingId, req);
    call.subscribe(() => this.resetForm());
  }

  protected delete(category: CategoryDto): void {
    if (!confirm(`Delete category "${category.name}"? Its transactions fall back to "needs review".`)) return;
    this.categoriesService.delete(category.id).subscribe();
  }
}
