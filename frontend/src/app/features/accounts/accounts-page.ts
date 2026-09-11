import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BankAccountRequest } from '../../core/api.service';
import { AccountsService } from '../../core/accounts.service';
import { onEnterSubmit } from '../../shared/keyboard';
import { BankAccountDto } from '../../shared/models';

@Component({
  selector: 'app-accounts-page',
  imports: [FormsModule],
  templateUrl: './accounts-page.html',
})
export class AccountsPage {
  private accountsService = inject(AccountsService);

  protected onToolbarEnter(event: Event): void {
    onEnterSubmit(event, () => this.save());
  }

  protected readonly accounts = this.accountsService.accounts;

  protected editingId: number | null = null;
  protected bankName = '';
  protected displayName = '';
  protected iban = '';

  constructor() {
    this.accountsService.ensureLoaded();
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
    const call =
      this.editingId === null ? this.accountsService.create(req) : this.accountsService.update(this.editingId, req);
    call.subscribe(() => this.resetForm());
  }

  protected delete(account: BankAccountDto): void {
    if (
      !confirm(
        `Delete account "${account.displayName ?? account.bankName}"? Transactions already recognized as transfers to it fall back to "needs review".`,
      )
    )
      return;
    this.accountsService.delete(account.id).subscribe();
  }
}
