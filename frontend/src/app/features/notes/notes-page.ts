import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { marked } from 'marked';
import { ApiService } from '../../core/api.service';
import { onEnterSubmit } from '../../shared/keyboard';
import { NoteSummaryDto } from '../../shared/models';

@Component({
  selector: 'app-notes-page',
  imports: [FormsModule],
  templateUrl: './notes-page.html',
})
export class NotesPage {
  private api = inject(ApiService);

  protected readonly notes = signal<NoteSummaryDto[]>([]);
  protected readonly selectedName = signal<string | null>(null);
  protected readonly mode = signal<'edit' | 'preview'>('edit');
  protected readonly renderedHtml = signal('');

  protected content = '';
  protected newNoteName = '';

  constructor() {
    this.load();
  }

  protected load(): void {
    this.api.getNotes().subscribe((notes) => this.notes.set(notes));
  }

  protected onNewNoteEnter(event: Event): void {
    onEnterSubmit(event, () => this.createNote());
  }

  protected createNote(): void {
    const name = this.newNoteName.trim();
    if (!name) return;
    this.api.createNote(name, '').subscribe((note) => {
      this.newNoteName = '';
      this.load();
      // Freshly created and empty — nothing to preview yet, so start in Edit.
      this.open(note.name, note.content, 'edit');
    });
  }

  protected select(summary: NoteSummaryDto): void {
    // Notes are read far more often than edited, so opening an existing one defaults to Preview.
    this.api.getNote(summary.name).subscribe((note) => this.open(note.name, note.content, 'preview'));
  }

  private open(name: string, content: string, mode: 'edit' | 'preview'): void {
    this.selectedName.set(name);
    this.content = content;
    if (mode === 'preview') this.renderedHtml.set(marked.parse(content, { async: false }));
    this.mode.set(mode);
  }

  protected showEdit(): void {
    this.mode.set('edit');
  }

  protected showPreview(): void {
    this.renderedHtml.set(marked.parse(this.content, { async: false }));
    this.mode.set('preview');
  }

  protected save(): void {
    const name = this.selectedName();
    if (name === null) return;
    this.api.updateNote(name, this.content).subscribe(() => this.load());
  }

  protected delete(): void {
    const name = this.selectedName();
    if (name === null) return;
    if (!confirm(`Delete note "${name}"? This cannot be undone.`)) return;
    this.api.deleteNote(name).subscribe(() => {
      this.selectedName.set(null);
      this.content = '';
      this.load();
    });
  }
}
