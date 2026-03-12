export interface DnsRecord {
  id: string;
  recordType: DnsRecordTypeEnum;
  name: string;
  content: string;
  ttlSeconds: number;
  isActive: boolean;
  createdAt: string;
}

export enum DnsRecordTypeEnum {
  A = 'A',
  AAAA = 'AAAA',
  CNAME = 'CNAME',
  MX = 'MX',
  TXT = 'TXT',
  NS = 'NS',
}

export interface CreateDnsRecordRequest {
  recordType: DnsRecordTypeEnum;
  name: string;
  content: string;
  ttlSeconds: number;
}

export interface DnsRecordResponse {
  id: string;
  recordType: string;
  name: string;
  content: string;
  ttlSeconds: number;
  isActive: boolean;
  createdAt: string;
}

export interface DnsListResponse extends DnsRecordResponse {}
