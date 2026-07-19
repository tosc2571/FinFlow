import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'dashboard',
    loadComponent: () => import('./features/dashboard/dashboard-page').then((m) => m.DashboardPage),
  },
  {
    path: 'transactions',
    loadComponent: () => import('./features/transactions/transactions-page').then((m) => m.TransactionsPage),
  },
  {
    path: 'import',
    loadComponent: () => import('./features/import/import-page').then((m) => m.ImportPage),
  },
  {
    path: 'rules',
    loadComponent: () => import('./features/rules/rules-page').then((m) => m.RulesPage),
  },
  {
    path: 'categories',
    loadComponent: () => import('./features/categories/categories-page').then((m) => m.CategoriesPage),
  },
];
