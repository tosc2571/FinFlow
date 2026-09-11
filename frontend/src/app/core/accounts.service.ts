import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiService, BankAccountRequest } from './api.service';
import { BankAccountDto } from '../shared/models';

/**
 * Single shared source of truth for the accounts list — same rationale as CategoriesService, plus
 * one more: registering/editing an account has a server-side side effect (TransferDetectionService
 * retroactively flags matching transactions as InternalTransfer), so a Transactions tab kept alive
 * by KeepAliveRouteReuseStrategy needs to know an account changed, not just Accounts itself.
 */
@Injectable({ providedIn: 'root' })
export class AccountsService {
  private api = inject(ApiService);
  private loaded = false;

  readonly accounts = signal<BankAccountDto[]>([]);

  /** Fetches once per app session; later callers just read the shared signal. */
  ensureLoaded(): void {
    if (this.loaded) return;
    this.loaded = true;
    this.refresh();
  }

  refresh(): void {
    this.api.getAccounts().subscribe((a) => this.accounts.set(a));
  }

  create(req: BankAccountRequest): Observable<BankAccountDto> {
    return this.api.createAccount(req).pipe(tap(() => this.refresh()));
  }

  update(id: number, req: BankAccountRequest): Observable<BankAccountDto> {
    return this.api.updateAccount(id, req).pipe(tap(() => this.refresh()));
  }

  delete(id: number): Observable<unknown> {
    return this.api.deleteAccount(id).pipe(tap(() => this.refresh()));
  }
}
