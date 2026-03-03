import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  UserAnalytics,
  UserDetail,
  UserSession,
  UserGroup,
  UserGroupDetail,
  UserGroupListResponse,
  UserLoginHistory,
  LoginHistoryListResponse,
  UserConsent,
  CreateUserGroupRequest,
  UpdateUserGroupRequest,
  GrantConsentRequest,
} from '../models/enterprise-user.model';

@Injectable({ providedIn: 'root' })
export class EnterpriseUserService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  // ═══════════════════════════════════════════════════
  // ANALYTICS & USER DETAIL
  // ═══════════════════════════════════════════════════

  getAnalytics(): Observable<UserAnalytics> {
    return this.http.get<UserAnalytics>(`${this.baseUrl}/user-analytics`);
  }

  getUserDetail(userId: string): Observable<UserDetail> {
    return this.http.get<UserDetail>(`${this.baseUrl}/user-analytics/users/${userId}`);
  }

  // ═══════════════════════════════════════════════════
  // SESSION MANAGEMENT
  // ═══════════════════════════════════════════════════

  getUserSessions(userId: string, activeOnly = true): Observable<UserSession[]> {
    const params = new HttpParams().set('activeOnly', activeOnly);
    return this.http.get<UserSession[]>(`${this.baseUrl}/user-sessions/${userId}`, { params });
  }

  revokeSession(sessionId: string, reason?: string): Observable<void> {
    let params = new HttpParams();
    if (reason) params = params.set('reason', reason);
    return this.http.delete<void>(`${this.baseUrl}/user-sessions/${sessionId}`, { params });
  }

  revokeAllSessions(userId: string, reason?: string): Observable<{ revokedCount: number }> {
    let params = new HttpParams();
    if (reason) params = params.set('reason', reason);
    return this.http.delete<{ revokedCount: number }>(
      `${this.baseUrl}/user-sessions/user/${userId}/all`,
      { params },
    );
  }

  // ═══════════════════════════════════════════════════
  // GROUP MANAGEMENT
  // ═══════════════════════════════════════════════════

  getGroups(
    search?: string,
    groupType?: string,
    page = 1,
    pageSize = 20,
  ): Observable<UserGroupListResponse> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    if (groupType) params = params.set('groupType', groupType);
    return this.http.get<UserGroupListResponse>(`${this.baseUrl}/user-groups`, { params });
  }

  getGroupDetail(groupId: string): Observable<UserGroupDetail> {
    return this.http.get<UserGroupDetail>(`${this.baseUrl}/user-groups/${groupId}`);
  }

  createGroup(data: CreateUserGroupRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/user-groups`, data);
  }

  updateGroup(groupId: string, data: UpdateUserGroupRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/user-groups/${groupId}`, data);
  }

  deleteGroup(groupId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/user-groups/${groupId}`);
  }

  addGroupMember(
    groupId: string,
    userId: string,
    memberRole?: string,
    addedBy?: string,
  ): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/user-groups/${groupId}/members`, {
      userId,
      memberRole,
      addedBy,
    });
  }

  removeGroupMember(groupId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/user-groups/${groupId}/members/${userId}`);
  }

  updateMemberRole(groupId: string, userId: string, memberRole: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/user-groups/${groupId}/members/${userId}/role`, {
      memberRole,
    });
  }

  assignGroupPermissions(
    groupId: string,
    permissions: string[],
    grantedBy?: string,
  ): Observable<{ message: string; count: number }> {
    return this.http.post<{ message: string; count: number }>(
      `${this.baseUrl}/user-groups/${groupId}/permissions`,
      { permissions, grantedBy },
    );
  }

  // ═══════════════════════════════════════════════════
  // LOGIN HISTORY
  // ═══════════════════════════════════════════════════

  getLoginHistory(
    userId?: string,
    page = 1,
    pageSize = 50,
    isSuccess?: boolean,
    isSuspicious?: boolean,
  ): Observable<LoginHistoryListResponse> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (userId) params = params.set('userId', userId);
    if (isSuccess !== undefined) params = params.set('isSuccess', isSuccess);
    if (isSuspicious !== undefined) params = params.set('isSuspicious', isSuspicious);
    return this.http.get<LoginHistoryListResponse>(`${this.baseUrl}/login-history`, { params });
  }

  // ═══════════════════════════════════════════════════
  // CONSENT MANAGEMENT
  // ═══════════════════════════════════════════════════

  getUserConsents(userId: string): Observable<UserConsent[]> {
    return this.http.get<UserConsent[]>(`${this.baseUrl}/user-consents/${userId}`);
  }

  grantConsent(data: GrantConsentRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/user-consents`, data);
  }

  revokeConsent(consentId: string, reason?: string): Observable<void> {
    let params = new HttpParams();
    if (reason) params = params.set('reason', reason);
    return this.http.delete<void>(`${this.baseUrl}/user-consents/${consentId}`, { params });
  }
}
