export type UserRole = 'Admin' | 'Staff' | 'Content Moderator' | 'Farmer' | 'RetailBuyer' | 'WholesaleBuyer';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accountId: number;
  email: string;
  fullName: string;
  role: string;
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
}

export interface UserSession {
  accountId: number;
  email: string;
  fullName: string;
  role: UserRole;
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
}
