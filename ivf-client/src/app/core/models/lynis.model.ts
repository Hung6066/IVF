export interface LynisReportSummary {
  hostname: string;
  date: string;
  key: string;
  size: number;
  lastModified: string;
}

export interface LynisReportsResponse {
  total: number;
  reports: LynisReportSummary[];
  error?: string;
}

export interface LynisHostsResponse {
  hosts: string[];
}

export interface LynisReport {
  hostname: string;
  report_date: string;
  generated_at: string;
  lynis_version: string;
  os: string;
  kernel: string;
  hardening_index: number;
  tests_executed: number;
  firewall_active: string;
  malware_scanner: string;
  compiler_installed: string;
  warnings: string[];
  suggestions: string[];
  vulnerable_packages: string[];
  warning_count: number;
  suggestion_count: number;
  source_file: string;
}
