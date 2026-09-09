import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiService, RuleRequest } from './api.service';
import { RuleDto } from '../shared/models';

/**
 * Single shared source of truth for the rules list — same rationale as CategoriesService:
 * KeepAliveRouteReuseStrategy keeps page instances alive across navigation, so a rule edited
 * from Transactions/Categories (see #51) needs every other page's rules list to pick it up
 * without a full reload.
 */
@Injectable({ providedIn: 'root' })
export class RulesService {
  private api = inject(ApiService);
  private loaded = false;

  readonly rules = signal<RuleDto[]>([]);

  /** Fetches once per app session; later callers just read the shared signal. */
  ensureLoaded(): void {
    if (this.loaded) return;
    this.loaded = true;
    this.refresh();
  }

  refresh(): void {
    this.api.getRules().subscribe((r) => this.rules.set(r));
  }

  create(req: RuleRequest): Observable<RuleDto> {
    return this.api.createRule(req).pipe(tap(() => this.refresh()));
  }

  update(id: number, req: RuleRequest): Observable<RuleDto> {
    return this.api.updateRule(id, req).pipe(tap(() => this.refresh()));
  }

  delete(id: number): Observable<unknown> {
    return this.api.deleteRule(id).pipe(tap(() => this.refresh()));
  }
}
