import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
    TreatmentCycle, TreatmentCycleDetail,
    TreatmentIndication, StimulationData, CultureData, TransferData,
    LutealPhaseData, PregnancyData, BirthData, AdverseEventData
} from '../models/cycle.models';

@Injectable({ providedIn: 'root' })
export class CycleService {
    private http = inject(HttpClient);
    private readonly baseUrl = environment.apiUrl;

    getCycle(id: string): Observable<TreatmentCycleDetail> {
        return this.http.get<TreatmentCycleDetail>(`${this.baseUrl}/cycles/${id}`);
    }

    getCyclesByCouple(coupleId: string): Observable<TreatmentCycle[]> {
        return this.http.get<TreatmentCycle[]>(`${this.baseUrl}/cycles/couple/${coupleId}`);
    }

    createCycle(cycle: Partial<TreatmentCycle>): Observable<string> {
        return this.http.post<string>(`${this.baseUrl}/cycles`, cycle);
    }

    advanceCyclePhase(id: string, phase: string): Observable<TreatmentCycle> {
        return this.http.post<TreatmentCycle>(`${this.baseUrl}/cycles/${id}/advance`, { phase });
    }

    completeCycle(id: string, outcome: string): Observable<TreatmentCycle> {
        return this.http.post<TreatmentCycle>(`${this.baseUrl}/cycles/${id}/complete`, { outcome });
    }

    // ==================== CYCLE PHASE DATA ====================
    getCycleIndication(cycleId: string): Observable<TreatmentIndication> {
        return this.http.get<TreatmentIndication>(`${this.baseUrl}/cycles/${cycleId}/indication`);
    }

    updateCycleIndication(cycleId: string, data: Partial<TreatmentIndication>): Observable<TreatmentIndication> {
        return this.http.put<TreatmentIndication>(`${this.baseUrl}/cycles/${cycleId}/indication`, data);
    }

    getCycleStimulation(cycleId: string): Observable<StimulationData> {
        return this.http.get<StimulationData>(`${this.baseUrl}/cycles/${cycleId}/stimulation`);
    }

    updateCycleStimulation(cycleId: string, data: Partial<StimulationData>): Observable<StimulationData> {
        return this.http.put<StimulationData>(`${this.baseUrl}/cycles/${cycleId}/stimulation`, data);
    }

    getCycleCulture(cycleId: string): Observable<CultureData> {
        return this.http.get<CultureData>(`${this.baseUrl}/cycles/${cycleId}/culture`);
    }

    updateCycleCulture(cycleId: string, data: Partial<CultureData>): Observable<CultureData> {
        return this.http.put<CultureData>(`${this.baseUrl}/cycles/${cycleId}/culture`, data);
    }

    getCycleTransfer(cycleId: string): Observable<TransferData> {
        return this.http.get<TransferData>(`${this.baseUrl}/cycles/${cycleId}/transfer`);
    }

    updateCycleTransfer(cycleId: string, data: Partial<TransferData>): Observable<TransferData> {
        return this.http.put<TransferData>(`${this.baseUrl}/cycles/${cycleId}/transfer`, data);
    }

    getCycleLutealPhase(cycleId: string): Observable<LutealPhaseData> {
        return this.http.get<LutealPhaseData>(`${this.baseUrl}/cycles/${cycleId}/luteal-phase`);
    }

    updateCycleLutealPhase(cycleId: string, data: Partial<LutealPhaseData>): Observable<LutealPhaseData> {
        return this.http.put<LutealPhaseData>(`${this.baseUrl}/cycles/${cycleId}/luteal-phase`, data);
    }

    getCyclePregnancy(cycleId: string): Observable<PregnancyData> {
        return this.http.get<PregnancyData>(`${this.baseUrl}/cycles/${cycleId}/pregnancy`);
    }

    updateCyclePregnancy(cycleId: string, data: Partial<PregnancyData>): Observable<PregnancyData> {
        return this.http.put<PregnancyData>(`${this.baseUrl}/cycles/${cycleId}/pregnancy`, data);
    }

    getCycleBirth(cycleId: string): Observable<BirthData> {
        return this.http.get<BirthData>(`${this.baseUrl}/cycles/${cycleId}/birth`);
    }

    updateCycleBirth(cycleId: string, data: Partial<BirthData>): Observable<BirthData> {
        return this.http.put<BirthData>(`${this.baseUrl}/cycles/${cycleId}/birth`, data);
    }

    getCycleAdverseEvents(cycleId: string): Observable<AdverseEventData[]> {
        return this.http.get<AdverseEventData[]>(`${this.baseUrl}/cycles/${cycleId}/adverse-events`);
    }

    createCycleAdverseEvent(cycleId: string, data: Partial<AdverseEventData>): Observable<AdverseEventData> {
        return this.http.post<AdverseEventData>(`${this.baseUrl}/cycles/${cycleId}/adverse-events`, data);
    }
}
