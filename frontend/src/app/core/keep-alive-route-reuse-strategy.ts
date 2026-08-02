import { ActivatedRouteSnapshot, DetachedRouteHandle, RouteReuseStrategy } from '@angular/router';

/**
 * Keeps every route's component instance alive across navigation instead of the Angular
 * default (destroy on leave, rebuild from scratch on return) — filters, pagination, scroll
 * position, and already-loaded data all survive switching tabs and back. Keyed by the route's
 * static path; none of FinFlow's routes have params, so that's unambiguous.
 *
 * Trade-off: a page's constructor/ngOnInit only ever runs once, so data isn't automatically
 * refetched on return to a tab. Acceptable here — see issue #28.
 */
export class KeepAliveRouteReuseStrategy implements RouteReuseStrategy {
  private readonly handles = new Map<string, DetachedRouteHandle>();

  private key(route: ActivatedRouteSnapshot): string | null {
    return route.routeConfig?.path ?? null;
  }

  shouldDetach(route: ActivatedRouteSnapshot): boolean {
    return this.key(route) !== null;
  }

  store(route: ActivatedRouteSnapshot, handle: DetachedRouteHandle | null): void {
    const key = this.key(route);
    if (key === null) return;
    if (handle === null) {
      this.handles.delete(key);
    } else {
      this.handles.set(key, handle);
    }
  }

  shouldAttach(route: ActivatedRouteSnapshot): boolean {
    const key = this.key(route);
    return key !== null && this.handles.has(key);
  }

  retrieve(route: ActivatedRouteSnapshot): DetachedRouteHandle | null {
    const key = this.key(route);
    return key === null ? null : (this.handles.get(key) ?? null);
  }

  shouldReuseRoute(future: ActivatedRouteSnapshot, current: ActivatedRouteSnapshot): boolean {
    return future.routeConfig === current.routeConfig;
  }
}
