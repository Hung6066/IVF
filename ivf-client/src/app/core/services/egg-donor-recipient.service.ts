import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { EggDonorRecipientDto } from '../models/clinical-management.models';

export interface MatchEggDonorRequest {
  eggDonorId: string;
  recipientCoupleId: string;
  matchedByUserId: string;
  notes?: string;
}

@Injectable({ providedIn: 'root' })
export class EggDonorRecipientService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getById(id: string): Observable<EggDonorRecipientDto> {
    return this.http.get<EggDonorRecipientDto>(`${this.baseUrl}/egg-donor-recipients/${id}`);
  }

  getByDonor(donorId: string): Observable<EggDonorRecipientDto[]> {
    return this.http.get<EggDonorRecipientDto[]>(
      `${this.baseUrl}/egg-donor-recipients/donor/${donorId}`,
    );
  }

  getByRecipientCouple(coupleId: string): Observable<EggDonorRecipientDto[]> {
    return this.http.get<EggDonorRecipientDto[]>(
      `${this.baseUrl}/egg-donor-recipients/recipient/${coupleId}`,
    );
  }

  match(request: MatchEggDonorRequest): Observable<EggDonorRecipientDto> {
    return this.http.post<EggDonorRecipientDto>(`${this.baseUrl}/egg-donor-recipients`, request);
  }

  linkToCycle(id: string, cycleId: string): Observable<EggDonorRecipientDto> {
    return this.http.put<EggDonorRecipientDto>(
      `${this.baseUrl}/egg-donor-recipients/${id}/link-cycle`,
      { cycleId },
    );
  }

  complete(id: string, notes?: string): Observable<EggDonorRecipientDto> {
    return this.http.put<EggDonorRecipientDto>(
      `${this.baseUrl}/egg-donor-recipients/${id}/complete`,
      { notes },
    );
  }

  cancel(id: string, reason?: string): Observable<EggDonorRecipientDto> {
    return this.http.put<EggDonorRecipientDto>(
      `${this.baseUrl}/egg-donor-recipients/${id}/cancel`,
      { reason },
    );
  }
}
