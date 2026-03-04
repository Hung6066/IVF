import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

interface TrustData {
  companyName: string;
  lastUpdated: string;
  frameworks: Framework[];
  securityControls: SecurityControl[];
  metrics: Metrics;
  certifications: Certification[];
  documents: Document[];
  contact: Contact;
}

interface Framework {
  id: string;
  name: string;
  score: number;
  status: string;
  icon: string;
  description: string;
}

interface SecurityControl {
  category: string;
  items: string[];
}

interface Metrics {
  trainingComplianceRate: number;
  aiBiasFairnessRate: number;
  incidentResolutionRate: number;
  encryptionAtRest: string;
  encryptionInTransit: string;
  mfaEnforced: boolean;
  zeroTrustEnabled: boolean;
  uptimeSla: string;
  backupFrequency: string;
  dataRetention: string;
}

interface Certification {
  name: string;
  status: string;
  date: string;
}

interface Document {
  name: string;
  available: boolean;
}

interface Contact {
  securityEmail: string;
  dpoEmail: string;
  reportVulnerability: string;
}

@Component({
  selector: 'app-trust-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './trust-page.component.html',
  styleUrls: ['./trust-page.component.scss'],
})
export class TrustPageComponent implements OnInit {
  data = signal<TrustData | null>(null);
  loading = signal(true);
  error = signal('');
  activeSection = signal('overview');

  private readonly apiUrl = environment.apiUrl.replace('/api', '');

  constructor(private http: HttpClient) {}

  ngOnInit() {
    this.http.get<TrustData>(`${this.apiUrl}/api/trust`).subscribe({
      next: (data) => {
        this.data.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Unable to load trust information');
        this.loading.set(false);
      },
    });
  }

  setSection(section: string) {
    this.activeSection.set(section);
  }

  getScoreClass(score: number): string {
    if (score >= 90) return 'score-excellent';
    if (score >= 80) return 'score-good';
    if (score >= 70) return 'score-fair';
    return 'score-low';
  }

  getStatusClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'compliant':
      case 'completed':
        return 'status-compliant';
      case 'audit ready':
      case 'ready':
        return 'status-ready';
      case 'maturing':
      case 'on track':
      case 'developing':
        return 'status-progress';
      default:
        return 'status-default';
    }
  }

  getFrameworkIcon(icon: string): string {
    const icons: Record<string, string> = {
      shield: '🛡️',
      lock: '🔒',
      heart: '❤️',
      globe: '🌍',
      award: '🏆',
      cpu: '🤖',
      brain: '🧠',
    };
    return icons[icon] || '📋';
  }

  getCertStatusIcon(status: string): string {
    return status === 'completed' ? '✅' : '🔵';
  }
}
