export interface Tenant {
  id: string;
  name: string;
  slug: string;
  logoUrl?: string;
  address?: string;
  phone?: string;
  email?: string;
  website?: string;
  taxId?: string;
  status: TenantStatus;
  maxUsers: number;
  maxPatientsPerMonth: number;
  storageLimitMb: number;
  aiEnabled: boolean;
  digitalSigningEnabled: boolean;
  biometricsEnabled: boolean;
  advancedReportingEnabled: boolean;
  isolationStrategy: DataIsolationStrategy;
  isRootTenant: boolean;
  databaseSchema?: string;
  connectionString?: string;
  primaryColor?: string;
  locale: string;
  timeZone: string;
  customDomain?: string;
  createdAt: string;
  subscription?: TenantSubscription;
  currentUsage?: TenantUsage;
}

export interface TenantListItem {
  id: string;
  name: string;
  slug: string;
  status: TenantStatus;
  isolationStrategy: DataIsolationStrategy;
  plan?: SubscriptionPlan;
  userCount: number;
  patientCount: number;
  createdAt: string;
}

export interface TenantSubscription {
  id: string;
  plan: SubscriptionPlan;
  status: SubscriptionStatus;
  billingCycle: BillingCycle;
  monthlyPrice: number;
  discountPercent?: number;
  currency: string;
  startDate: string;
  endDate?: string;
  trialEndDate?: string;
  nextBillingDate?: string;
  autoRenew: boolean;
  effectivePrice: number;
}

export interface TenantUsage {
  year: number;
  month: number;
  activeUsers: number;
  newPatients: number;
  treatmentCycles: number;
  formResponses: number;
  signedDocuments: number;
  storageUsedMb: number;
  apiCalls: number;
}

export interface TenantPlatformStats {
  totalTenants: number;
  activeTenants: number;
  trialTenants: number;
  suspendedTenants: number;
  monthlyRevenue: number;
  totalUsers: number;
  totalPatients: number;
  totalStorageMb: number;
}

export interface CreateTenantRequest {
  name: string;
  slug: string;
  email: string;
  phone: string;
  address: string;
  plan: SubscriptionPlan;
  billingCycle: BillingCycle;
  isolationStrategy: DataIsolationStrategy;
  adminUsername: string;
  adminPassword: string;
  adminFullName: string;
}

export interface UpdateTenantRequest {
  id: string;
  name: string;
  address?: string;
  phone?: string;
  email?: string;
  website?: string;
  taxId?: string;
}

export interface UpdateBrandingRequest {
  id: string;
  logoUrl?: string;
  primaryColor?: string;
  customDomain?: string;
}

export interface UpdateLimitsRequest {
  id: string;
  maxUsers: number;
  maxPatientsPerMonth: number;
  storageLimitMb: number;
  aiEnabled: boolean;
  digitalSigningEnabled: boolean;
  biometricsEnabled: boolean;
  advancedReportingEnabled: boolean;
}

export interface UpdateSubscriptionRequest {
  tenantId: string;
  plan: SubscriptionPlan;
  billingCycle: BillingCycle;
  monthlyPrice: number;
  discountPercent?: number;
}

export interface PricingPlan {
  plan: string;
  displayName: string;
  description?: string;
  price: number;
  currency: string;
  duration: string;
  maxUsers: number;
  maxPatients: number;
  storageGb: number;
  isFeatured: boolean;
  features: PlanFeatureItem[];
}

export interface PlanFeatureItem {
  code: string;
  displayName: string;
  description?: string;
  icon: string;
  category: string;
}

export type TenantStatus = 'PendingSetup' | 'Active' | 'Suspended' | 'Cancelled' | 'Trial';
export type SubscriptionPlan = 'Trial' | 'Starter' | 'Professional' | 'Enterprise' | 'Custom';
export type SubscriptionStatus = 'Active' | 'PastDue' | 'Cancelled' | 'Expired' | 'Suspended';
export type BillingCycle = 'Monthly' | 'Quarterly' | 'Annually';
export type DataIsolationStrategy = 'SharedDatabase' | 'SeparateSchema' | 'SeparateDatabase';

export interface TenantFeatures {
  isPlatformAdmin: boolean;
  enabledFeatures: string[];
  isolationStrategy: DataIsolationStrategy;
  maxUsers: number;
  maxPatients: number;
}

export interface UpdateIsolationRequest {
  isolationStrategy: DataIsolationStrategy;
  connectionString?: string;
  databaseSchema?: string;
}
