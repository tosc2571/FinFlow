import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService, CategoryRequest } from '../../core/api.service';
import { CategoryDto } from '../../shared/models';

@Component({
  selector: 'app-categories-page',
  imports: [FormsModule],
  templateUrl: './categories-page.html',
})
export class CategoriesPage {
  private api = inject(ApiService);

  protected readonly categories = signal<CategoryDto[]>([]);

  protected editingId: number | null = null;
  protected name = '';
  protected parentCategoryId: number | '' = '';
  protected isIncome = false;
  protected sortOrder = 0;

  constructor() {
    this.load();
  }

  protected load(): void {
    this.api.getCategories().subscribe((c) => this.categories.set(c));
  }

  protected parentName(category: CategoryDto): string {
    if (category.parentCategoryId === null) return '';
    return this.categories().find((c) => c.id === category.parentCategoryId)?.name ?? '';
  }

  protected parentOptions(): CategoryDto[] {
    return this.categories().filter((c) => c.id !== this.editingId);
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
      this.editingId === null ? this.api.createCategory(req) : this.api.updateCategory(this.editingId, req);
    call.subscribe(() => {
      this.resetForm();
      this.load();
    });
  }

  protected delete(category: CategoryDto): void {
    if (!confirm(`Delete category "${category.name}"? Its transactions fall back to "needs review".`)) return;
    this.api.deleteCategory(category.id).subscribe(() => this.load());
  }
}
