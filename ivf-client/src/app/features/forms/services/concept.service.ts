import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface Concept {
    id: string;
    code: string;
    display: string;
    description?: string;
    system: string;
    conceptType: ConceptType;
    mappings: ConceptMapping[];
}

export interface ConceptMapping {
    id: string;
    targetSystem: string;
    targetCode: string;
    targetDisplay: string;
    relationship?: string;
}

export interface SearchConceptsResult {
    concepts: Concept[];
    totalCount: number;
}

export enum ConceptType {
    Clinical = 0,
    Laboratory = 1,
    Medication = 2,
    Diagnosis = 3,
    Procedure = 4,
    Anatomical = 5,
    Administrative = 6
}

export interface CreateConceptRequest {
    code: string;
    display: string;
    description?: string;
    system?: string;
    conceptType: ConceptType;
}

export interface AddConceptMappingRequest {
    targetSystem: string;
    targetCode: string;
    targetDisplay: string;
    relationship?: string;
}

@Injectable({
    providedIn: 'root'
})
export class ConceptService {
    private apiUrl = `${environment.apiUrl}/concepts`;

    constructor(private http: HttpClient) { }

    /**
     * Search concepts using full-text search
     */
    searchConcepts(
        searchTerm?: string,
        conceptType?: ConceptType,
        pageSize: number = 20,
        pageNumber: number = 1
    ): Observable<SearchConceptsResult> {
        let params = new HttpParams()
            .set('pageSize', pageSize.toString())
            .set('pageNumber', pageNumber.toString());

        if (searchTerm) {
            params = params.set('q', searchTerm);
        }

        if (conceptType !== undefined) {
            params = params.set('conceptType', conceptType.toString());
        }

        return this.http.get<SearchConceptsResult>(`${this.apiUrl}/search`, { params });
    }

    /**
     * Get concept by ID
     */
    getConceptById(id: string): Observable<Concept> {
        return this.http.get<Concept>(`${this.apiUrl}/${id}`);
    }

    /**
     * Get concepts by type
     */
    getConceptsByType(
        conceptType: ConceptType,
        pageSize: number = 50,
        pageNumber: number = 1
    ): Observable<Concept[]> {
        const params = new HttpParams()
            .set('pageSize', pageSize.toString())
            .set('pageNumber', pageNumber.toString());

        return this.http.get<Concept[]>(`${this.apiUrl}/by-type/${conceptType}`, { params });
    }

    /**
     * Create new concept
     */
    createConcept(request: CreateConceptRequest): Observable<Concept> {
        return this.http.post<Concept>(this.apiUrl, request);
    }

    /**
     * Update concept
     */
    updateConcept(id: string, display: string, description?: string): Observable<Concept> {
        return this.http.put<Concept>(`${this.apiUrl}/${id}`, {
            display,
            description
        });
    }

    /**
     * Add external terminology mapping (SNOMED CT, HL7, LOINC)
     */
    addConceptMapping(conceptId: string, request: AddConceptMappingRequest): Observable<ConceptMapping> {
        return this.http.post<ConceptMapping>(`${this.apiUrl}/${conceptId}/mappings`, request);
    }

    /**
     * Link form field to concept
     */
    linkFieldToConcept(fieldId: string, conceptId: string): Observable<any> {
        return this.http.post(`${this.apiUrl}/link/field`, {
            fieldId,
            conceptId
        });
    }

    /**
     * Link form field option to concept
     */
    linkOptionToConcept(optionId: string, conceptId: string): Observable<any> {
        return this.http.post(`${this.apiUrl}/link/option`, {
            optionId,
            conceptId
        });
    }
}
