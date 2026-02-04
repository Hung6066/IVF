import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { SpermBankService, Donor, Sample, Match } from '../sperm-bank.service';

@Component({
  selector: 'app-sperm-bank-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './sperm-bank-dashboard.component.html',
  styleUrls: ['./sperm-bank-dashboard.component.scss']
})
export class SpermBankDashboardComponent implements OnInit {
  private service = inject(SpermBankService);

  activeTab = 'donors';
  donors = signal<Donor[]>([]);
  samples = signal<Sample[]>([]);
  matches = signal<Match[]>([]);

  totalDonors = signal(0);
  totalSamples = signal(0);
  availableSamples = signal(0);
  quarantineSamples = signal(0);

  showNewDonor = false;
  showNewSample = false;
  showNewMatch = false;

  newDonor: any = { bloodType: '', age: null, height: null, education: '', notes: '' };
  newSample: any = { donor: '', vials: 1, status: 'Quarantine', location: '' };
  newMatch: any = { recipient: '', donor: '', notes: '' };

  ngOnInit(): void {
    this.refreshData();
  }

  refreshData() {
    this.service.getDonors().subscribe(d => {
      this.donors.set(d);
      this.totalDonors.set(d.length);
    });

    this.service.getSamples().subscribe(s => {
      this.samples.set(s);
      this.totalSamples.set(s.length);
      this.availableSamples.set(s.filter(x => x.status === 'Available').length);
      this.quarantineSamples.set(s.filter(x => x.status === 'Quarantine').length);
    });

    this.service.getMatches().subscribe(m => this.matches.set(m));
  }

  viewDonor(d: any): void { alert('Xem thông tin người hiến: ' + d.code); }
  editDonor(d: any): void { alert('Sửa người hiến: ' + d.code); }
  editSample(s: any): void { alert('Sửa mẫu: ' + s.code); }
  deleteSample(s: any): void {
    if (confirm('Xóa mẫu ' + s.code + '?')) {
      this.samples.update(list => list.filter(x => x.id !== s.id));
    }
  }
  viewMatch(m: any): void { alert('Xem ghép đôi: ' + m.recipient + ' - ' + m.donor); }
  editMatch(m: any): void { alert('Sửa ghép đôi'); }

  submitDonor(): void {
    const newCode = 'NH-' + String(this.donors().length + 1).padStart(3, '0');
    this.donors.update(list => [...list, { id: newCode, code: newCode, bloodType: this.newDonor.bloodType, age: this.newDonor.age, samples: 0, status: 'Screening' }]);
    this.showNewDonor = false;
    this.newDonor = { bloodType: '', age: null, height: null, education: '', notes: '' };
  }

  submitSample(): void {
    const newCode = 'SP-' + String(this.samples().length + 1).padStart(3, '0');
    this.samples.update(list => [...list, { id: newCode, code: newCode, donor: this.newSample.donor, vials: this.newSample.vials, status: this.newSample.status }]);
    this.showNewSample = false;
    this.newSample = { donor: '', vials: 1, status: 'Quarantine', location: '' };
  }

  submitMatch(): void {
    this.matches.update(list => [...list, { id: String(list.length + 1), recipient: this.newMatch.recipient, donor: this.newMatch.donor, date: new Date().toLocaleDateString('vi-VN'), status: 'Pending' }]);
    this.showNewMatch = false;
    this.newMatch = { recipient: '', donor: '', notes: '' };
  }
}
