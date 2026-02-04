import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LabDashboardComponent } from './lab-dashboard.component';
import { LabService } from './lab.service';
import { of } from 'rxjs';
import { ActivatedRoute } from '@angular/router';

describe('LabDashboardComponent', () => {
    let component: LabDashboardComponent;
    let fixture: ComponentFixture<LabDashboardComponent>;
    let mockLabService: jasmine.SpyObj<LabService>;

    beforeEach(async () => {
        mockLabService = jasmine.createSpyObj('LabService', [
            'getQueue', 'getEmbryos', 'getSchedule', 'getCryoLocations', 'getStats',
            'callPatient', 'toggleScheduleStatus', 'addCryoLocation'
        ]);

        // Setup default mock returns
        mockLabService.getQueue.and.returnValue(of([]));
        mockLabService.getEmbryos.and.returnValue(of([]));
        mockLabService.getSchedule.and.returnValue(of([]));
        mockLabService.getCryoLocations.and.returnValue(of([]));
        mockLabService.getStats.and.returnValue(of({
            eggRetrievalCount: 0, cultureCount: 0, transferCount: 0, freezeCount: 0,
            totalFrozenEmbryos: 0, totalFrozenEggs: 0, totalFrozenSperm: 0
        }));

        await TestBed.configureTestingModule({
            imports: [LabDashboardComponent],
            providers: [
                { provide: LabService, useValue: mockLabService },
                { provide: ActivatedRoute, useValue: {} }
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(LabDashboardComponent);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });

    it('should load data on init', () => {
        expect(mockLabService.getQueue).toHaveBeenCalled();
        expect(mockLabService.getEmbryos).toHaveBeenCalled();
        expect(mockLabService.getStats).toHaveBeenCalled();
    });

    it('should change tab', () => {
        component.setActiveTab('embryos');
        expect(component.activeTab).toBe('embryos');
    });

    it('should call patient', () => {
        mockLabService.callPatient.and.returnValue(of({}));
        const mockQueueItem = { id: '1', number: '001', patientName: 'Test', patientCode: 'P1', issueTime: '', status: 'Waiting' };

        spyOn(window, 'alert'); // Spy on alert to prevent popup during test
        component.onCallPatient(mockQueueItem);

        expect(mockLabService.callPatient).toHaveBeenCalledWith('1');
    });

    it('should change day', () => {
        const today = new Date();
        component.changeDay(-1); // Go back one day

        const yesterday = new Date();
        yesterday.setDate(today.getDate() - 1);

        // Compare dates (ignoring time for simplicity in this basic test)
        expect(component.currentDate.getDate()).toBe(yesterday.getDate());
        expect(mockLabService.getSchedule).toHaveBeenCalled();
    });
});
