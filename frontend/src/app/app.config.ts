import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { RouteReuseStrategy, provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { apiErrorInterceptor } from './core/api-error.interceptor';
import { KeepAliveRouteReuseStrategy } from './core/keep-alive-route-reuse-strategy';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([apiErrorInterceptor])),
    { provide: RouteReuseStrategy, useClass: KeepAliveRouteReuseStrategy },
  ],
};
