import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, LoginRequest, User } from '../models/api.models';

const TOKEN_KEY = 'ivf_access_token';
const REFRESH_KEY = 'ivf_refresh_token';
const USER_KEY = 'ivf_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
    private readonly apiUrl = `${environment.apiUrl}/auth`;

    private _user = signal<User | null>(this.loadUser());
    private _isAuthenticated = computed(() => !!this._user() && !!this.getToken());

    readonly user = this._user.asReadonly();
    readonly isAuthenticated = this._isAuthenticated;

    constructor(private http: HttpClient, private router: Router) { }

    login(credentials: LoginRequest): Observable<AuthResponse> {
        return this.http.post<AuthResponse>(`${this.apiUrl}/login`, credentials).pipe(
            tap(response => this.handleAuthSuccess(response)),
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
            tap(response => this.handleAuthSuccess(response)),
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
        this._user.set(null);
        this.router.navigate(['/login']);
    }

    getToken(): string | null {
        return localStorage.getItem(TOKEN_KEY);
    }

    hasRole(role: string): boolean {
        return this._user()?.role === role;
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
}
