import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  readonly message = signal<string | null>(null);

  error(message: string): void {
    this.message.set(message);
  }

  clear(): void {
    this.message.set(null);
  }
}
