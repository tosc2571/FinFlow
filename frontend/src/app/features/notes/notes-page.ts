import { Component, ElementRef, Injector, afterNextRender, inject, runInInjectionContext, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import mermaid from 'mermaid';
import { ApiService } from '../../core/api.service';
import { onEnterSubmit } from '../../shared/keyboard';
import { renderMarkdown } from '../../shared/markdown';
import { NoteSummaryDto } from '../../shared/models';

mermaid.initialize({ startOnLoad: false });

@Component({
  selector: 'app-notes-page',
  imports: [FormsModule],
  templateUrl: './notes-page.html',
})
export class NotesPage {
  private api = inject(ApiService);
  private el: ElementRef<HTMLElement> = inject(ElementRef);
  private injector = inject(Injector);

  protected readonly notes = signal<NoteSummaryDto[]>([]);
  protected readonly selectedName = signal<string | null>(null);
  protected readonly mode = signal<'edit' | 'preview'>('edit');
  protected readonly renderedHtml = signal('');

  protected content = '';
  protected newNoteName = '';

  constructor() {
    this.load();
  }

  /** Mermaid needs its ```mermaid blocks (rendered as <pre class="mermaid"> by renderMarkdown)
   * actually in the DOM before it can find and replace them with SVG — afterNextRender defers
   * until Angular has committed the [innerHTML] update, unlike a plain effect() on the content
   * signal, which wouldn't refire when toggling back into an unchanged Preview recreates the
   * element without changing the signal's value. */
  private scheduleMermaidRender(): void {
    runInInjectionContext(this.injector, () =>
      afterNextRender(() => {
        const nodes = this.el.nativeElement.querySelectorAll<HTMLElement>('pre.mermaid');
        if (nodes.length === 0) return;
        mermaid.run({ nodes: Array.from(nodes) }).catch((err: unknown) => console.error('Mermaid render failed', err));
      }),
    );
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
    if (mode === 'preview') {
      this.renderedHtml.set(renderMarkdown(content));
      this.scheduleMermaidRender();
    }
    this.mode.set(mode);
  }

  protected showEdit(): void {
    this.mode.set('edit');
  }

  protected showPreview(): void {
    this.renderedHtml.set(renderMarkdown(this.content));
    this.mode.set('preview');
    this.scheduleMermaidRender();
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
