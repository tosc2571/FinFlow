import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiService, CategoryRequest } from './api.service';
import { CategoryDto } from '../shared/models';

/**
 * Single shared source of truth for the category list. Transactions/Rules/Contracts/Categories
 * each used to hold their own private copy, fetched once in their constructor — but
 * KeepAliveRouteReuseStrategy keeps page instances alive across navigation, so a constructor
 * never reruns when navigating back to a tab, and a category created on one page stayed
 * invisible on the others until a full reload. Every page now reads this signal instead, and
 * every mutation refreshes it so all of them update immediately.
 */
@Injectable({ providedIn: 'root' })
export class CategoriesService {
  private api = inject(ApiService);
  private loaded = false;

  readonly categories = signal<CategoryDto[]>([]);

  /** Fetches once per app session; later callers just read the shared signal. */
  ensureLoaded(): void {
    if (this.loaded) return;
    this.loaded = true;
    this.refresh();
  }

  refresh(): void {
    this.api.getCategories().subscribe((c) => this.categories.set(c));
  }

  create(req: CategoryRequest): Observable<CategoryDto> {
    return this.api.createCategory(req).pipe(tap(() => this.refresh()));
  }

  update(id: number, req: CategoryRequest): Observable<CategoryDto> {
    return this.api.updateCategory(id, req).pipe(tap(() => this.refresh()));
  }

  delete(id: number): Observable<unknown> {
    return this.api.deleteCategory(id).pipe(tap(() => this.refresh()));
  }
}
