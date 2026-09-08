import { Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CategoryRequest } from '../core/api.service';
import { CategoriesService } from '../core/categories.service';
import { onEnterSubmit } from './keyboard';
import { CategoryDto } from './models';

/**
 * Quick "new category" modal, opened from the category <select> on Transactions/Rules instead of
 * navigating away to /categories — the newly created category is emitted so the caller can assign
 * it directly (see #47: category creation used to interrupt whatever the user was doing).
 */
@Component({
  selector: 'app-quick-create-category-dialog',
  imports: [FormsModule],
  templateUrl: './quick-create-category-dialog.html',
})
export class QuickCreateCategoryDialog {
  private categoriesService = inject(CategoriesService);

  @Output() created = new EventEmitter<CategoryDto>();

  protected readonly visible = signal(false);
  protected name = '';
  protected parentCategoryId: number | '' = '';
  protected isIncome = false;

  protected get categories(): CategoryDto[] {
    return this.categoriesService.categories();
  }

  open(): void {
    this.name = '';
    this.parentCategoryId = '';
    this.isIncome = false;
    this.visible.set(true);
  }

  protected onDialogEnter(event: Event): void {
    onEnterSubmit(event, () => this.save());
  }

  protected cancel(): void {
    this.visible.set(false);
  }

  protected save(): void {
    if (!this.name.trim()) return;
    const req: CategoryRequest = {
      name: this.name,
      parentCategoryId: this.parentCategoryId === '' ? null : this.parentCategoryId,
      isIncome: this.isIncome,
      sortOrder: 0,
    };
    this.categoriesService.create(req).subscribe((category) => {
      this.visible.set(false);
      this.created.emit(category);
    });
  }
}
