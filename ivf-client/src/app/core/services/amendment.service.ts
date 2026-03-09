import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AmendmentDto,
  AmendmentApproveResult,
  CreateAmendmentRequest,
  ApproveRejectRequest,
  PendingAmendmentsResponse,
} from '../models/amendment.models';

@Injectable({ providedIn: 'root' })
export class AmendmentService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  /** Tạo yêu cầu chỉnh sửa phiếu đã ký */
  createAmendment(
    formResponseId: string,
    request: CreateAmendmentRequest,
  ): Observable<AmendmentDto> {
    return this.http.post<AmendmentDto>(
      `${this.baseUrl}/forms/responses/${formResponseId}/amendments`,
      request,
    );
  }

  /** Lấy lịch sử chỉnh sửa của một phiếu */
  getAmendmentHistory(formResponseId: string): Observable<AmendmentDto[]> {
    return this.http.get<AmendmentDto[]>(
      `${this.baseUrl}/forms/responses/${formResponseId}/amendments`,
    );
  }

  /** Lấy chi tiết một yêu cầu chỉnh sửa */
  getAmendmentDetail(amendmentId: string): Observable<AmendmentDto> {
    return this.http.get<AmendmentDto>(`${this.baseUrl}/amendments/${amendmentId}`);
  }

  /** Lấy danh sách yêu cầu đang chờ duyệt */
  getPendingAmendments(page = 1, pageSize = 20): Observable<PendingAmendmentsResponse> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PendingAmendmentsResponse>(`${this.baseUrl}/amendments/pending`, {
      params,
    });
  }

  /** Phê duyệt yêu cầu chỉnh sửa */
  approveAmendment(
    amendmentId: string,
    request: ApproveRejectRequest,
  ): Observable<AmendmentApproveResult> {
    return this.http.post<AmendmentApproveResult>(
      `${this.baseUrl}/amendments/${amendmentId}/approve`,
      request,
    );
  }

  /** Từ chối yêu cầu chỉnh sửa */
  rejectAmendment(
    amendmentId: string,
    request: ApproveRejectRequest,
  ): Observable<AmendmentApproveResult> {
    return this.http.post<AmendmentApproveResult>(
      `${this.baseUrl}/amendments/${amendmentId}/reject`,
      request,
    );
  }
}
