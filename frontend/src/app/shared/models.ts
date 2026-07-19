// DTO shapes mirroring backend/FinFlow.Api (enums arrive as strings via JsonStringEnumConverter).

export type ClassificationStatus = 'Auto' | 'NeedsReview' | 'Ignored' | 'ManualOverride';
export type RuleStatus = 'Auto' | 'NeedsReview' | 'Ignore';
export type ImportStatus = 'Completed' | 'Failed';

export interface TransactionDto {
  id: number;
  sourceBank: string;
  bookingDate: string | null;
  valueDate: string | null;
  amount: number;
  currency: string;
  counterpartyName: string | null;
  counterpartyIban: string | null;
  counterpartyBic: string | null;
  purpose: string | null;
  bookingType: string | null;
  balance: number | null;
  categoryId: number | null;
  categoryName: string | null;
  classificationStatus: ClassificationStatus;
  importBatchId: number;
}

export interface PagedTransactions {
  total: number;
  page: number;
  pageSize: number;
  items: TransactionDto[];
}

export interface CategoryDto {
  id: number;
  name: string;
  parentCategoryId: number | null;
  isIncome: boolean;
  sortOrder: number;
}

export interface RuleDto {
  id: number;
  pattern: string;
  categoryId: number;
  categoryName: string;
  status: RuleStatus;
  priority: number;
  isActive: boolean;
}

export interface AnalyzeFileResult {
  fileName: string;
  detectedBank: string | null;
}

export interface AnalyzeResponse {
  files: AnalyzeFileResult[];
  knownBanks: string[];
}

export interface ImportFileResult {
  fileName: string;
  bank: string | null;
  batchId: number;
  imported: number;
  duplicates: number;
  error: string | null;
}

export interface ImportBatchDto {
  id: number;
  sourceFileName: string;
  detectedBank: string;
  importedAt: string;
  transactionCount: number;
  duplicateCount: number;
  status: ImportStatus;
  errorMessage: string | null;
}

export interface DashboardSummary {
  income: number;
  expenses: number;
  net: number;
  transactionCount: number;
  needsReviewCount: number;
  ignoredCount: number;
}

export interface CategoryBreakdown {
  categoryId: number | null;
  categoryName: string;
  count: number;
  total: number;
}

export interface MonthlyTrend {
  year: number;
  month: number;
  income: number;
  expenses: number;
  net: number;
}

export interface PatternTestMatch {
  transactionId: number;
  bookingDate: string | null;
  counterpartyName: string | null;
  purpose: string | null;
  amount: number;
}

export interface PatternTestResult {
  matchCount: number;
  sample: PatternTestMatch[];
}

export interface ReclassifyResult {
  reclassified: number;
  skippedManualOverride: number;
}

export interface TransactionFilter {
  year?: number | null;
  from?: string | null;
  to?: string | null;
  contains?: string | null;
  bank?: string | null;
  categoryId?: number | null;
  status?: ClassificationStatus | '' | null;
  sort?: string;
}

export const STATUS_LABELS: Record<ClassificationStatus, string> = {
  Auto: 'Auto',
  NeedsReview: 'Needs review',
  Ignored: 'Ignored',
  ManualOverride: 'Manual',
};
