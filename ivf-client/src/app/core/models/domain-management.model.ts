export interface TenantDomainInfo {
  tenantId: string;
  tenantName: string;
  slug: string;
  subdomain: string | null;
  customDomain: string | null;
  customDomainStatus: string;
  isActive: boolean;
}

export interface CaddySyncResult {
  success: boolean;
  message: string;
  domainsConfigured: number;
}
