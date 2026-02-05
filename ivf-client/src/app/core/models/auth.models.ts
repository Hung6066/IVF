export interface LoginRequest {
    username: string;
    password: string;
}

export interface AuthResponse {
    accessToken: string;
    refreshToken: string;
    expiresIn: number;
    user: User;
}

export interface User {
    id: string;
    username: string;
    fullName: string;
    role: string;
    department?: string;
}
