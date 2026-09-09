import { Component, inject, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CategoryRequest } from '../../core/api.service';
import { CategoriesService } from '../../core/categories.service';
import { RulesService } from '../../core/rules.service';
import { onEnterSubmit } from '../../shared/keyboard';
import { RuleEditDialog } from '../../shared/rule-edit-dialog';
import { CategoryDto, RuleDto } from '../../shared/models';

@Component({
  selector: 'app-categories-page',
  imports: [FormsModule, RuleEditDialog],
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
  protected sortOrder = 0;

  constructor() {
    this.categoriesService.ensureLoaded();
    this.rulesService.ensureLoaded();
  }

  protected parentName(category: CategoryDto): string {
    if (category.parentCategoryId === null) return '';
    return this.categoriesService.categories().find((c) => c.id === category.parentCategoryId)?.name ?? '';
  }

  /** Rules assigned to this category (#51) — clicking one opens it for editing right here. */
  protected rulesFor(category: CategoryDto): RuleDto[] {
    return this.rulesService.rules().filter((r) => r.categoryId === category.id);
  }

  protected editRule(rule: RuleDto): void {
    this.ruleDialog().open(rule);
  }

  protected parentOptions(): CategoryDto[] {
    return this.categoriesService.categories().filter((c) => c.id !== this.editingId);
  }

  protected edit(category: CategoryDto): void {
    this.editingId = category.id;
    this.name = category.name;
    this.parentCategoryId = category.parentCategoryId ?? '';
    this.isIncome = category.isIncome;
    this.sortOrder = category.sortOrder;
  }

  protected resetForm(): void {
    this.editingId = null;
    this.name = '';
    this.parentCategoryId = '';
    this.isIncome = false;
    this.sortOrder = 0;
  }

  protected save(): void {
    if (!this.name.trim()) return;
    const req: CategoryRequest = {
      name: this.name,
      parentCategoryId: this.parentCategoryId === '' ? null : this.parentCategoryId,
      isIncome: this.isIncome,
      sortOrder: this.sortOrder,
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
