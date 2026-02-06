import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { CoupleService } from '../../../core/services/couple.service';
import { Couple } from '../../../core/models/api.models';

@Component({
    selector: 'app-couple-list',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterModule],
    templateUrl: './couple-list.component.html',
    styleUrls: ['./couple-list.component.scss']
})
export class CoupleListComponent implements OnInit {
    couples = signal<Couple[]>([]);
    searchTerm = signal<string>('');

    filteredCouples = computed(() => {
        const term = this.searchTerm().toLowerCase().trim();
        const list = this.couples();
        if (!term) return list;

        return list.filter(c =>
            (c.wife?.fullName?.toLowerCase().includes(term) ?? false) ||
            (c.wife?.patientCode?.toLowerCase().includes(term) ?? false) ||
            (c.husband?.fullName?.toLowerCase().includes(term) ?? false) ||
            (c.husband?.patientCode?.toLowerCase().includes(term) ?? false)
        );
    });

    constructor(private coupleService: CoupleService) { }

    ngOnInit(): void {
        this.coupleService.getCouples().subscribe(c => this.couples.set(c));
    }

    formatDate(date?: string): string {
        if (!date) return '';
        return new Date(date).toLocaleDateString('vi-VN');
    }

    getWifeId(couple: Couple): string {
        return couple.wife?.id || '';
    }

    getHusbandId(couple: Couple): string {
        return couple.husband?.id || '';
    }
}
