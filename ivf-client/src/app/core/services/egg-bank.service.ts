import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  EggDonorDto,
  OocyteSampleDto,
  CreateEggDonorRequest,
  UpdateEggDonorProfileRequest,
  CreateOocyteSampleRequest,
  RecordOocyteQualityRequest,
  VitrifyOocytesRequest,
  EggDonorSearchResult,
} from '../models/egg-donor.models';

@Injectable({ providedIn: 'root' })
export class EggBankService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  // Donors
  searchDonors(query?: string, page = 1, pageSize = 20): Observable<EggDonorSearchResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (query) params = params.set('q', query);
    return this.http.get<EggDonorSearchResult>(`${this.baseUrl}/eggbank/donors`, { params });
  }

  getDonorById(id: string): Observable<EggDonorDto> {
    return this.http.get<EggDonorDto>(`${this.baseUrl}/eggbank/donors/${id}`);
  }

  createDonor(request: CreateEggDonorRequest): Observable<EggDonorDto> {
    return this.http.post<EggDonorDto>(`${this.baseUrl}/eggbank/donors`, request);
  }

  updateDonorProfile(id: string, request: UpdateEggDonorProfileRequest): Observable<EggDonorDto> {
    return this.http.put<EggDonorDto>(`${this.baseUrl}/eggbank/donors/${id}/profile`, request);
  }

  // Oocyte Samples
  getSamplesByDonor(donorId: string): Observable<OocyteSampleDto[]> {
    return this.http.get<OocyteSampleDto[]>(`${this.baseUrl}/eggbank/samples/donor/${donorId}`);
  }

  getAvailableSamples(): Observable<OocyteSampleDto[]> {
    return this.http.get<OocyteSampleDto[]>(`${this.baseUrl}/eggbank/samples/available`);
  }

  createSample(request: CreateOocyteSampleRequest): Observable<OocyteSampleDto> {
    return this.http.post<OocyteSampleDto>(`${this.baseUrl}/eggbank/samples`, request);
  }

  recordQuality(
    sampleId: string,
    request: RecordOocyteQualityRequest,
  ): Observable<OocyteSampleDto> {
    return this.http.put<OocyteSampleDto>(
      `${this.baseUrl}/eggbank/samples/${sampleId}/quality`,
      request,
    );
  }

  vitrifySample(sampleId: string, request: VitrifyOocytesRequest): Observable<OocyteSampleDto> {
    return this.http.put<OocyteSampleDto>(
      `${this.baseUrl}/eggbank/samples/${sampleId}/vitrify`,
      request,
    );
  }
}
