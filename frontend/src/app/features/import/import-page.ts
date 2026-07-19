import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { ImportBatchDto, ImportFileResult } from '../../shared/models';

interface ImportRow {
  file: File;
  detectedBank: string | null;
  /** '' = user still has to pick (detection failed); otherwise bank name or 'auto'. */
  selectedBank: string;
}

@Component({
  selector: 'app-import-page',
  imports: [FormsModule, DatePipe],
  templateUrl: './import-page.html',
})
export class ImportPage {
  private api = inject(ApiService);

  // Review rows are a plain array: the per-row <select> mutates entries via ngModel,
  // and zoneless CD re-renders after the template event.
  protected rows: ImportRow[] = [];
  protected knownBanks = signal<string[]>([]);
  protected analyzing = signal(false);
  protected importing = signal(false);
  protected dragOver = signal(false);
  protected results = signal<ImportFileResult[] | null>(null);
  protected batches = signal<ImportBatchDto[]>([]);

  constructor() {
    this.loadBatches();
  }

  protected get canImport(): boolean {
    return this.rows.length > 0 && this.rows.every((r) => r.selectedBank !== '');
  }

  protected onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.analyze(Array.from(input.files ?? []));
    input.value = '';
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(false);
    this.analyze(Array.from(event.dataTransfer?.files ?? []));
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(true);
  }

  private analyze(files: File[]): void {
    const csvFiles = files.filter((f) => f.name.toLowerCase().endsWith('.csv'));
    if (csvFiles.length === 0) return;
    this.analyzing.set(true);
    this.results.set(null);
    this.api.analyzeFiles(csvFiles).subscribe({
      next: (res) => {
        this.knownBanks.set(res.knownBanks);
        this.rows = csvFiles.map((file, i) => ({
          file,
          detectedBank: res.files[i]?.detectedBank ?? null,
          selectedBank: res.files[i]?.detectedBank ?? '',
        }));
        this.analyzing.set(false);
      },
      error: () => this.analyzing.set(false),
    });
  }

  protected removeRow(row: ImportRow): void {
    this.rows = this.rows.filter((r) => r !== row);
  }

  protected import(): void {
    if (!this.canImport) return;
    this.importing.set(true);
    this.api.importFiles(this.rows.map((r) => r.file), this.rows.map((r) => r.selectedBank)).subscribe({
      next: (results) => {
        this.results.set(results);
        this.rows = [];
        this.importing.set(false);
        this.loadBatches();
      },
      error: () => this.importing.set(false),
    });
  }

  protected loadBatches(): void {
    this.api.getBatches().subscribe((b) => this.batches.set(b));
  }

  protected deleteBatch(batch: ImportBatchDto): void {
    if (!confirm(`Roll back batch "${batch.sourceFileName}" (${batch.transactionCount} transaction(s))?`)) return;
    this.api.deleteBatch(batch.id).subscribe(() => this.loadBatches());
  }
}
