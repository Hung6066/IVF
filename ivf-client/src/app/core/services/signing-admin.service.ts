import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  SigningDashboard,
  SigningConfig,
  ServiceHealthDetail,
  TestSignResult,
  EjbcaCertSearchRequest,
  EjbcaCertSearchResponse,
  EjbcaRevokeResult,
} from '../models/signing.models';

@Injectable({ providedIn: 'root' })
export class SigningAdminService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/admin/signing`;

  /** Get the complete signing dashboard overview */
  getDashboard(): Observable<SigningDashboard> {
    return this.http.get<SigningDashboard>(this.baseUrl + '/dashboard');
  }

  /** Get signing configuration */
  getConfig(): Observable<SigningConfig> {
    return this.http.get<SigningConfig>(this.baseUrl + '/config');
  }

  /** Check SignServer health */
  getSignServerHealth(): Observable<ServiceHealthDetail> {
    return this.http.get<ServiceHealthDetail>(this.baseUrl + '/signserver/health');
  }

  /** Get SignServer workers */
  getSignServerWorkers(): Observable<any> {
    return this.http.get(this.baseUrl + '/signserver/workers');
  }

  /** Get specific worker detail */
  getSignServerWorker(workerId: number): Observable<any> {
    return this.http.get(this.baseUrl + `/signserver/workers/${workerId}`);
  }

  /** Check EJBCA health */
  getEjbcaHealth(): Observable<ServiceHealthDetail> {
    return this.http.get<ServiceHealthDetail>(this.baseUrl + '/ejbca/health');
  }

  /** Get EJBCA CAs */
  getEjbcaCAs(): Observable<any> {
    return this.http.get(this.baseUrl + '/ejbca/cas');
  }

  /** Search EJBCA certificates */
  searchEjbcaCertificates(request: EjbcaCertSearchRequest): Observable<EjbcaCertSearchResponse> {
    return this.http.post<EjbcaCertSearchResponse>(
      this.baseUrl + '/ejbca/certificates/search',
      request,
    );
  }

  /** Get EJBCA certificate detail */
  getEjbcaCertificateDetail(serialNumber: string, issuerDn?: string): Observable<any> {
    const params: any = {};
    if (issuerDn) params.issuerDn = issuerDn;
    return this.http.get(this.baseUrl + `/ejbca/certificates/${serialNumber}`, { params });
  }

  /** Revoke EJBCA certificate */
  revokeEjbcaCertificate(
    serialNumber: string,
    issuerDn: string,
    reason?: string,
  ): Observable<EjbcaRevokeResult> {
    return this.http.put<EjbcaRevokeResult>(
      this.baseUrl + `/ejbca/certificates/${serialNumber}/revoke`,
      { issuerDn, reason: reason || 'UNSPECIFIED' },
    );
  }

  /** Get EJBCA certificate profiles */
  getEjbcaCertificateProfiles(): Observable<any> {
    return this.http.get(this.baseUrl + '/ejbca/certificate-profiles');
  }

  /** Get EJBCA end entity profiles */
  getEjbcaEndEntityProfiles(): Observable<any> {
    return this.http.get(this.baseUrl + '/ejbca/endentity-profiles');
  }

  /** Download CA certificate */
  downloadCaCertificate(caName: string): Observable<Blob> {
    return this.http.get(this.baseUrl + `/ejbca/ca/${encodeURIComponent(caName)}/certificate`, {
      responseType: 'blob',
    });
  }

  /** Run a test signing */
  testSign(): Observable<TestSignResult> {
    return this.http.post<TestSignResult>(this.baseUrl + '/test-sign', {});
  }
}
