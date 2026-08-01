import { Component, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { SettingsDto } from '../../shared/models';

@Component({
  selector: 'app-settings-page',
  templateUrl: './settings-page.html',
})
export class SettingsPage {
  private api = inject(ApiService);

  protected readonly settings = signal<SettingsDto | null>(null);

  constructor() {
    this.load();
  }

  private load(): void {
    this.api.getSettings().subscribe((s) => this.settings.set(s));
  }

  protected toggleAutoBackup(): void {
    const current = this.settings();
    if (!current) return;
    this.api
      .updateSettings({ autoBackupEnabled: !current.autoBackupEnabled })
      .subscribe((s) => this.settings.set(s));
  }
}
