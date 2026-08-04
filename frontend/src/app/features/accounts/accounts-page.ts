import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService, BankAccountRequest } from '../../core/api.service';
import { onEnterSubmit } from '../../shared/keyboard';
import { BankAccountDto } from '../../shared/models';

@Component({
  selector: 'app-accounts-page',
  imports: [FormsModule],
  templateUrl: './accounts-page.html',
})
export class AccountsPage {
  private api = inject(ApiService);

  protected onToolbarEnter(event: Event): void {
    onEnterSubmit(event, () => this.save());
  }

  protected readonly accounts = signal<BankAccountDto[]>([]);

  protected editingId: number | null = null;
  protected bankName = '';
  protected displayName = '';
  protected iban = '';

  constructor() {
    this.load();
  }

  protected load(): void {
    this.api.getAccounts().subscribe((a) => this.accounts.set(a));
  }

  protected edit(account: BankAccountDto): void {
    this.editingId = account.id;
    this.bankName = account.bankName;
    this.displayName = account.displayName ?? '';
    this.iban = account.iban;
  }

  protected resetForm(): void {
    this.editingId = null;
    this.bankName = '';
    this.displayName = '';
    this.iban = '';
  }

  protected save(): void {
    if (!this.bankName.trim() || !this.iban.trim()) return;
    const req: BankAccountRequest = {
      bankName: this.bankName,
      displayName: this.displayName.trim() || null,
      iban: this.iban,
    };
    const call = this.editingId === null ? this.api.createAccount(req) : this.api.updateAccount(this.editingId, req);
    call.subscribe(() => {
      this.resetForm();
      this.load();
    });
  }

  protected delete(account: BankAccountDto): void {
    if (
      !confirm(
        `Delete account "${account.displayName ?? account.bankName}"? Transactions already recognized as transfers to it fall back to "needs review".`,
      )
    )
      return;
    this.api.deleteAccount(account.id).subscribe(() => this.load());
  }
}
