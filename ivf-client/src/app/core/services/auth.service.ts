import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, LoginRequest, User } from '../models/api.models';

const TOKEN_KEY = 'ivf_access_token';
const REFRESH_KEY = 'ivf_refresh_token';
const USER_KEY = 'ivf_user';
const PERMISSIONS_KEY = 'ivf_permissions';

@Injectable({ providedIn: 'root' })
export class AuthService {
    private readonly apiUrl = `${environment.apiUrl}/auth`;
    private readonly usersApiUrl = `${environment.apiUrl}/users`;

    private _user = signal<User | null>(this.loadUser());
    private _permissions = signal<string[]>(this.loadPermissions());
    private _isAuthenticated = computed(() => !!this._user() && !!this.getToken());

    readonly user = this._user.asReadonly();
    readonly permissions = this._permissions.asReadonly();
    readonly isAuthenticated = this._isAuthenticated;

    constructor(private http: HttpClient, private router: Router) { }

    login(credentials: LoginRequest): Observable<AuthResponse> {
        return this.http.post<AuthResponse>(`${this.apiUrl}/login`, credentials).pipe(
            tap(response => {
                this.handleAuthSuccess(response);
                // Load user permissions after successful login
                this.loadUserPermissions(response.user.id);
            }),
            catchError(err => {
                console.error('Login failed:', err);
                return throwError(() => err);
            })
        );
    }

    refreshToken(): Observable<AuthResponse> {
        const refreshToken = localStorage.getItem(REFRESH_KEY);
        if (!refreshToken) {
            return throwError(() => new Error('No refresh token'));
        }
        return this.http.post<AuthResponse>(`${this.apiUrl}/refresh`, { refreshToken }).pipe(
            tap(response => {
                this.handleAuthSuccess(response);
                this.loadUserPermissions(response.user.id);
            }),
            catchError(err => {
                this.logout();
                return throwError(() => err);
            })
        );
    }

    logout(): void {
        localStorage.removeItem(TOKEN_KEY);
        localStorage.removeItem(REFRESH_KEY);
        localStorage.removeItem(USER_KEY);
        localStorage.removeItem(PERMISSIONS_KEY);
        this._user.set(null);
        this._permissions.set([]);
        this.router.navigate(['/login']);
    }

    getToken(): string | null {
        return localStorage.getItem(TOKEN_KEY);
    }

    hasRole(role: string): boolean {
        return this._user()?.role === role;
    }

    hasPermission(permission: string): boolean {
        // Admin has all permissions
        if (this._user()?.role === 'Admin') return true;
        return this._permissions().includes(permission);
    }

    hasAnyPermission(permissions: string[]): boolean {
        if (this._user()?.role === 'Admin') return true;
        return permissions.some(p => this._permissions().includes(p));
    }

    setPermissions(permissions: string[]): void {
        localStorage.setItem(PERMISSIONS_KEY, JSON.stringify(permissions));
        this._permissions.set(permissions);
    }

    // Load permissions from API for the current logged-in user
    loadUserPermissions(userId?: string): void {
        // Use /me/permissions endpoint - works for any authenticated user
        this.http.get<string[]>(`${this.apiUrl}/me/permissions`).subscribe({
            next: (permissions) => {
                this.setPermissions(permissions || []);
            },
            error: () => {
                // If cannot load permissions, set empty (fallback)
                this.setPermissions([]);
            }
        });
    }

    private handleAuthSuccess(response: AuthResponse): void {
        localStorage.setItem(TOKEN_KEY, response.accessToken);
        localStorage.setItem(REFRESH_KEY, response.refreshToken);
        localStorage.setItem(USER_KEY, JSON.stringify(response.user));
        this._user.set(response.user);
    }

    private loadUser(): User | null {
        const userJson = localStorage.getItem(USER_KEY);
        return userJson ? JSON.parse(userJson) : null;
    }

    private loadPermissions(): string[] {
        const permsJson = localStorage.getItem(PERMISSIONS_KEY);
        return permsJson ? JSON.parse(permsJson) : [];
    }
}


