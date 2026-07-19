import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { SILENT_ERRORS } from './api-error.interceptor';
import {
  AnalyzeResponse,
  CategoryBreakdown,
  CategoryDto,
  ClassificationStatus,
  DashboardSummary,
  ImportBatchDto,
  ImportFileResult,
  MonthlyTrend,
  PagedTransactions,
  PatternTestResult,
  ReclassifyResult,
  RuleDto,
  RuleStatus,
  TransactionFilter,
} from '../shared/models';

export interface CategoryRequest {
  name: string;
  parentCategoryId: number | null;
  isIncome: boolean;
  sortOrder: number;
}

export interface RuleRequest {
  pattern: string;
  categoryId: number;
  status: RuleStatus;
  priority: number;
  isActive: boolean;
}

/** Typed client for the FinFlow API. All URLs are relative — the dev server
 * proxies /api to the backend (proxy.conf.json); in production nginx does. */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);

  // --- transactions ---

  getTransactions(filter: TransactionFilter, page: number, pageSize: number): Observable<PagedTransactions> {
    return this.http.get<PagedTransactions>('/api/transactions/', {
      params: this.toParams(filter, { page, pageSize }),
    });
  }

  patchTransaction(id: number, body: { categoryId: number | null; status?: ClassificationStatus }): Observable<unknown> {
    return this.http.patch(`/api/transactions/${id}`, body);
  }

  deleteTransaction(id: number): Observable<unknown> {
    return this.http.delete(`/api/transactions/${id}`);
  }

  exportUrl(kind: 'xlsx' | 'csv', filter: TransactionFilter): string {
    const query = this.toParams(filter, {}).toString();
    return `/api/export/${kind}${query ? '?' + query : ''}`;
  }

  // --- categories ---

  getCategories(): Observable<CategoryDto[]> {
    return this.http.get<CategoryDto[]>('/api/categories/');
  }

  createCategory(req: CategoryRequest): Observable<CategoryDto> {
    return this.http.post<CategoryDto>('/api/categories/', req);
  }

  updateCategory(id: number, req: CategoryRequest): Observable<CategoryDto> {
    return this.http.put<CategoryDto>(`/api/categories/${id}`, req);
  }

  deleteCategory(id: number): Observable<unknown> {
    return this.http.delete(`/api/categories/${id}`);
  }

  // --- rules ---

  getRules(): Observable<RuleDto[]> {
    return this.http.get<RuleDto[]>('/api/rules/');
  }

  createRule(req: RuleRequest): Observable<unknown> {
    return this.http.post('/api/rules/', req);
  }

  updateRule(id: number, req: RuleRequest): Observable<unknown> {
    return this.http.put(`/api/rules/${id}`, req);
  }

  deleteRule(id: number): Observable<unknown> {
    return this.http.delete(`/api/rules/${id}`);
  }

  testPattern(pattern: string): Observable<PatternTestResult> {
    return this.http.post<PatternTestResult>(
      '/api/rules/test',
      { pattern },
      { context: new HttpContext().set(SILENT_ERRORS, true) },
    );
  }

  reclassify(): Observable<ReclassifyResult> {
    return this.http.post<ReclassifyResult>('/api/rules/reclassify', {});
  }

  // --- import ---

  analyzeFiles(files: File[]): Observable<AnalyzeResponse> {
    const form = new FormData();
    for (const file of files) form.append('files', file);
    return this.http.post<AnalyzeResponse>('/api/import/analyze', form);
  }

  importFiles(files: File[], banks: string[]): Observable<ImportFileResult[]> {
    const form = new FormData();
    for (const file of files) form.append('files', file);
    for (const bank of banks) form.append('banks', bank);
    return this.http.post<ImportFileResult[]>('/api/import/', form);
  }

  getBatches(): Observable<ImportBatchDto[]> {
    return this.http.get<ImportBatchDto[]>('/api/import/batches');
  }

  deleteBatch(id: number): Observable<unknown> {
    return this.http.delete(`/api/import/batches/${id}`);
  }

  // --- dashboard ---

  getSummary(filter: TransactionFilter): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>('/api/dashboard/summary', { params: this.toParams(filter, {}) });
  }

  getByCategory(filter: TransactionFilter): Observable<CategoryBreakdown[]> {
    return this.http.get<CategoryBreakdown[]>('/api/dashboard/by-category', { params: this.toParams(filter, {}) });
  }

  getTrend(filter: TransactionFilter): Observable<MonthlyTrend[]> {
    return this.http.get<MonthlyTrend[]>('/api/dashboard/trend', { params: this.toParams(filter, {}) });
  }

  private toParams(filter: TransactionFilter, extra: Record<string, unknown>): HttpParams {
    let params = new HttpParams();
    for (const [key, value] of Object.entries({ ...filter, ...extra })) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return params;
  }
}
