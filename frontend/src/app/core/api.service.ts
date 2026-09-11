import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { SILENT_ERRORS } from './api-error.interceptor';
import {
  AnalyzeResponse,
  BankAccountDto,
  CategoryBreakdown,
  CategoryDto,
  ClassificationStatus,
  ContractDetail,
  ContractDto,
  ContractPeriod,
  DashboardSummary,
  ImportBatchDto,
  ImportFileResult,
  MonthForecast,
  MonthlyTrend,
  NoteDetailDto,
  NoteSummaryDto,
  PagedTransactions,
  PatternTestResult,
  ReclassifyResult,
  RuleDto,
  RuleStatus,
  SettingsDto,
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
  /** Null only when status is InternalTransfer — that status needs no category. */
  categoryId: number | null;
  status: RuleStatus;
  priority: number;
  isActive: boolean;
}

export interface ContractRequest {
  name: string;
  nominalAmount: number;
  period: ContractPeriod;
  anchorDate: string;
  counterpartyPattern: string;
  amountTolerance: number;
  categoryId: number | null;
  isActive: boolean;
}

export interface BankAccountRequest {
  bankName: string;
  displayName: string | null;
  iban: string;
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

  /** All transaction ids matching a filter, unpaginated — backs "select all N matching filter". */
  getTransactionIds(filter: TransactionFilter): Observable<number[]> {
    return this.http.get<number[]>('/api/transactions/ids', { params: this.toParams(filter, {}) });
  }

  patchTransaction(id: number, body: { categoryId: number | null; status?: ClassificationStatus }): Observable<unknown> {
    return this.http.patch(`/api/transactions/${id}`, body);
  }

  bulkCategorizeTransactions(transactionIds: number[], categoryId: number | null): Observable<{ updated: number }> {
    return this.http.post<{ updated: number }>('/api/transactions/bulk-categorize', { transactionIds, categoryId });
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

  getRule(id: number): Observable<RuleDto> {
    return this.http.get<RuleDto>(`/api/rules/${id}`);
  }

  createRule(req: RuleRequest): Observable<RuleDto> {
    return this.http.post<RuleDto>('/api/rules/', req);
  }

  updateRule(id: number, req: RuleRequest): Observable<RuleDto> {
    return this.http.put<RuleDto>(`/api/rules/${id}`, req);
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

  // --- contracts ---

  getContracts(): Observable<ContractDto[]> {
    return this.http.get<ContractDto[]>('/api/contracts/');
  }

  getContract(id: number): Observable<ContractDetail> {
    return this.http.get<ContractDetail>(`/api/contracts/${id}`);
  }

  createContract(req: ContractRequest): Observable<unknown> {
    return this.http.post('/api/contracts/', req);
  }

  createContractFromTransaction(transactionId: number): Observable<unknown> {
    return this.http.post(`/api/contracts/from-transaction/${transactionId}`, {});
  }

  updateContract(id: number, req: ContractRequest): Observable<unknown> {
    return this.http.put(`/api/contracts/${id}`, req);
  }

  deleteContract(id: number): Observable<unknown> {
    return this.http.delete(`/api/contracts/${id}`);
  }

  getForecast(months: number): Observable<MonthForecast[]> {
    return this.http.get<MonthForecast[]>('/api/contracts/forecast', { params: { months } });
  }

  // --- settings ---

  getSettings(): Observable<SettingsDto> {
    return this.http.get<SettingsDto>('/api/settings/');
  }

  updateSettings(req: { autoBackupEnabled: boolean }): Observable<SettingsDto> {
    return this.http.put<SettingsDto>('/api/settings/', req);
  }

  // --- accounts ---

  getAccounts(): Observable<BankAccountDto[]> {
    return this.http.get<BankAccountDto[]>('/api/accounts/');
  }

  createAccount(req: BankAccountRequest): Observable<BankAccountDto> {
    return this.http.post<BankAccountDto>('/api/accounts/', req);
  }

  updateAccount(id: number, req: BankAccountRequest): Observable<BankAccountDto> {
    return this.http.put<BankAccountDto>(`/api/accounts/${id}`, req);
  }

  deleteAccount(id: number): Observable<unknown> {
    return this.http.delete(`/api/accounts/${id}`);
  }

  // --- notes ---

  getNotes(): Observable<NoteSummaryDto[]> {
    return this.http.get<NoteSummaryDto[]>('/api/notes/');
  }

  getNote(name: string): Observable<NoteDetailDto> {
    return this.http.get<NoteDetailDto>(`/api/notes/${encodeURIComponent(name)}`);
  }

  createNote(name: string, content: string): Observable<NoteDetailDto> {
    return this.http.post<NoteDetailDto>('/api/notes/', { name, content });
  }

  updateNote(name: string, content: string): Observable<NoteDetailDto> {
    return this.http.put<NoteDetailDto>(`/api/notes/${encodeURIComponent(name)}`, { content });
  }

  deleteNote(name: string): Observable<unknown> {
    return this.http.delete(`/api/notes/${encodeURIComponent(name)}`);
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
