import { Injectable } from '@angular/core';
import { ApiService } from '../../../core/services/api.service';
import { Observable } from 'rxjs';
import { TreatmentCycle, Embryo, Ultrasound } from '../../../core/models/api.models';

@Injectable({
    providedIn: 'root'
})
export class CycleService {

    constructor(private api: ApiService) { }

    getCycle(id: string): Observable<TreatmentCycle> {
        return this.api.getCycle(id);
    }

    getUltrasounds(cycleId: string): Observable<Ultrasound[]> {
        return this.api.getUltrasoundsByCycle(cycleId);
    }

    getEmbryos(cycleId: string): Observable<Embryo[]> {
        return this.api.getEmbryosByCycle(cycleId);
    }

    advancePhase(cycleId: string, nextPhase: string): Observable<TreatmentCycle> {
        return this.api.advanceCyclePhase(cycleId, nextPhase);
    }
}
