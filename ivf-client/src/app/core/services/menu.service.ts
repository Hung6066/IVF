import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, catchError, of } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface MenuItemDto {
  id: string;
  icon: string;
  label: string;
  route: string;
  permission?: string;
  adminOnly: boolean;
  sortOrder: number;
}

export interface MenuSectionDto {
  section: string | null;
  header: string | null;
  adminOnly: boolean;
  items: MenuItemDto[];
}

/** Full menu item (admin view) */
export interface MenuItemAdmin {
  id: string;
  section: string | null;
  sectionHeader: string | null;
  icon: string;
  label: string;
  route: string;
  permission: string | null;
  adminOnly: boolean;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class MenuService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/menu`;

  /** Cached menu sections for sidebar */
  menuSections = signal<MenuSectionDto[]>([]);
  loading = signal(false);

  // ── Public API ──────────────────────────────────────

  /** Load active menu (for sidebar). Caches in signal. */
  loadMenu(): Observable<MenuSectionDto[]> {
    this.loading.set(true);
    return this.http.get<MenuSectionDto[]>(this.baseUrl).pipe(
      tap((sections) => {
        this.menuSections.set(sections);
        this.loading.set(false);
      }),
      catchError(() => {
        this.loading.set(false);
        return of([]);
      }),
    );
  }

  // ── Admin API ──────────────────────────────────────

  /** Get all menu items including inactive (admin) */
  getAllItems(): Observable<MenuItemAdmin[]> {
    return this.http.get<MenuItemAdmin[]>(`${this.baseUrl}/all`);
  }

  /** Create a new menu item */
  createItem(
    data: Omit<MenuItemAdmin, 'id' | 'createdAt' | 'updatedAt'>,
  ): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, data);
  }

  /** Update existing menu item */
  updateItem(
    id: string,
    data: Omit<MenuItemAdmin, 'id' | 'createdAt' | 'updatedAt'>,
  ): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, data);
  }

  /** Soft-delete a menu item */
  deleteItem(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Toggle active/inactive */
  toggleItem(id: string): Observable<{ isActive: boolean }> {
    return this.http.patch<{ isActive: boolean }>(`${this.baseUrl}/${id}/toggle`, {});
  }

  /** Reorder items */
  reorder(items: { id: string; sortOrder: number }[]): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/reorder`, { items });
  }
}
