import { Component, EventEmitter, Input, Output, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConceptService, Concept, ConceptType, SearchConceptsResult } from '../services/concept.service';
import { debounceTime, distinctUntilChanged, Subject } from 'rxjs';

@Component({
    selector: 'app-concept-picker',
    standalone: true,
    imports: [CommonModule, FormsModule],
    templateUrl: './concept-picker.component.html',
    styleUrls: ['./concept-picker.component.scss']
})
export class ConceptPickerComponent implements OnInit {
    @Input() isOpen = false;
    @Input() fieldId?: string;
    @Input() optionId?: string;
    @Output() conceptLinked = new EventEmitter<Concept>();
    @Output() closed = new EventEmitter<void>();

    searchTerm = '';
    selectedType?: ConceptType;
    loading = false;
    searchResults?: SearchConceptsResult;
    selectedConcept?: Concept;
    createMode = false;

    newConcept = {
        code: '',
        display: '',
        description: '',
        conceptType: ConceptType.Clinical
    };

    private searchSubject = new Subject<string>();

    conceptTypes = [
        { label: 'All', value: undefined },
        { label: 'Clinical', value: ConceptType.Clinical },
        { label: 'Laboratory', value: ConceptType.Laboratory },
        { label: 'Medication', value: ConceptType.Medication },
        { label: 'Diagnosis', value: ConceptType.Diagnosis },
        { label: 'Procedure', value: ConceptType.Procedure }
    ];

    constructor(private conceptService: ConceptService) { }

    ngOnInit() {
        // Debounce search input
        this.searchSubject
            .pipe(
                debounceTime(300),
                distinctUntilChanged()
            )
            .subscribe(term => {
                this.performSearch(term);
            });

        // Initial search
        this.performSearch('');
    }

    onSearchChange(term: string) {
        this.searchSubject.next(term);
    }

    filterByType(type?: ConceptType) {
        this.selectedType = type;
        this.performSearch(this.searchTerm);
    }

    performSearch(term: string) {
        this.loading = true;
        this.conceptService.searchConcepts(term, this.selectedType)
            .subscribe({
                next: (result) => {
                    this.searchResults = result;
                    this.loading = false;
                },
                error: (error) => {
                    console.error('Search failed:', error);
                    this.loading = false;
                }
            });
    }

    selectConcept(concept: Concept) {
        this.selectedConcept = concept;
    }

    linkConcept() {
        if (!this.selectedConcept) return;

        if (this.fieldId) {
            this.conceptService.linkFieldToConcept(this.fieldId, this.selectedConcept.id)
                .subscribe({
                    next: () => {
                        this.conceptLinked.emit(this.selectedConcept);
                        this.close();
                    },
                    error: (error) => console.error('Link failed:', error)
                });
        } else if (this.optionId) {
            this.conceptService.linkOptionToConcept(this.optionId, this.selectedConcept.id)
                .subscribe({
                    next: () => {
                        this.conceptLinked.emit(this.selectedConcept);
                        this.close();
                    },
                    error: (error) => console.error('Link failed:', error)
                });
        }
    }

    openCreateMode() {
        this.createMode = true;
        this.newConcept.display = this.searchTerm;
    }

    cancelCreate() {
        this.createMode = false;
        this.newConcept = {
            code: '',
            display: '',
            description: '',
            conceptType: ConceptType.Clinical
        };
    }

    canCreate(): boolean {
        return !!this.newConcept.code && !!this.newConcept.display;
    }

    createConcept() {
        if (!this.canCreate()) return;

        this.conceptService.createConcept(this.newConcept)
            .subscribe({
                next: (concept) => {
                    this.selectedConcept = concept;
                    this.createMode = false;
                    this.linkConcept();
                },
                error: (error) => console.error('Create failed:', error)
            });
    }

    close() {
        this.isOpen = false;
        this.closed.emit();
    }
}
