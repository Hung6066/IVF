import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PermissionDto {
  id: string;
  code: string;
  displayName: string;
  sortOrder: number;
}

export interface PermissionGroupDto {
  groupCode: string;
  groupName: string;
  groupIcon: string;
  groupSortOrder: number;
  permissions: PermissionDto[];
}

export interface PermissionDefinitionAdmin {
  id: string;
  code: string;
  displayName: string;
  groupCode: string;
  groupDisplayName: string;
  groupIcon: string;
  sortOrder: number;
  groupSortOrder: number;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class PermissionDefinitionService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/permission-definitions`;

  /** Cached permission groups for UI consumption */
  permissionGroups = signal<PermissionGroupDto[]>([]);

  /** Load active permissions grouped — caches in signal */
  loadPermissionGroups(): Observable<PermissionGroupDto[]> {
    return this.http
      .get<PermissionGroupDto[]>(this.baseUrl)
      .pipe(tap((groups) => this.permissionGroups.set(groups)));
  }

  /** Admin: get all definitions including inactive */
  getAll(): Observable<PermissionDefinitionAdmin[]> {
    return this.http.get<PermissionDefinitionAdmin[]>(`${this.baseUrl}/all`);
  }

  /** Admin: create new permission definition */
  create(data: Omit<PermissionDefinitionAdmin, 'id' | 'isActive'>): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, data);
  }

  /** Admin: update permission definition */
  update(
    id: string,
    data: Omit<PermissionDefinitionAdmin, 'id' | 'code' | 'isActive'>,
  ): Observable<any> {
    return this.http.put(`${this.baseUrl}/${id}`, data);
  }

  /** Admin: toggle active/inactive */
  toggle(id: string): Observable<{ isActive: boolean }> {
    return this.http.patch<{ isActive: boolean }>(`${this.baseUrl}/${id}/toggle`, {});
  }

  /** Admin: soft delete */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
