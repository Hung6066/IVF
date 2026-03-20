import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  PagedResult,
  PrescriptionCycleType,
  PrescriptionTemplateDto,
} from '../models/clinical-management.models';

export interface TemplateItemInput {
  drugName: string;
  drugCode?: string;
  dosage: string;
  frequency: string;
  duration: string;
  sortOrder: number;
}

export interface CreatePrescriptionTemplateRequest {
  name: string;
  cycleType: PrescriptionCycleType;
  createdByDoctorId: string;
  description?: string;
  items: TemplateItemInput[];
}

export interface UpdatePrescriptionTemplateRequest {
  name: string;
  cycleType: PrescriptionCycleType;
  description?: string;
  items: TemplateItemInput[];
}

@Injectable({ providedIn: 'root' })
export class PrescriptionTemplateService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  search(
    query?: string,
    cycleType?: PrescriptionCycleType,
    isActive?: boolean,
    page = 1,
    pageSize = 20,
  ): Observable<PagedResult<PrescriptionTemplateDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (query) params = params.set('q', query);
    if (cycleType) params = params.set('cycleType', cycleType);
    if (isActive !== undefined) params = params.set('isActive', isActive);
    return this.http.get<PagedResult<PrescriptionTemplateDto>>(
      `${this.baseUrl}/prescription-templates`,
      { params },
    );
  }

  getById(id: string): Observable<PrescriptionTemplateDto> {
    return this.http.get<PrescriptionTemplateDto>(`${this.baseUrl}/prescription-templates/${id}`);
  }

  getByDoctor(doctorId: string): Observable<PrescriptionTemplateDto[]> {
    return this.http.get<PrescriptionTemplateDto[]>(
      `${this.baseUrl}/prescription-templates/doctor/${doctorId}`,
    );
  }

  getByCycleType(cycleType: PrescriptionCycleType): Observable<PrescriptionTemplateDto[]> {
    return this.http.get<PrescriptionTemplateDto[]>(
      `${this.baseUrl}/prescription-templates/cycle-type/${cycleType}`,
    );
  }

  create(request: CreatePrescriptionTemplateRequest): Observable<PrescriptionTemplateDto> {
    return this.http.post<PrescriptionTemplateDto>(
      `${this.baseUrl}/prescription-templates`,
      request,
    );
  }

  update(
    id: string,
    request: UpdatePrescriptionTemplateRequest,
  ): Observable<PrescriptionTemplateDto> {
    return this.http.put<PrescriptionTemplateDto>(
      `${this.baseUrl}/prescription-templates/${id}`,
      request,
    );
  }

  toggleActive(id: string): Observable<PrescriptionTemplateDto> {
    return this.http.put<PrescriptionTemplateDto>(
      `${this.baseUrl}/prescription-templates/${id}/toggle-active`,
      {},
    );
  }
}
