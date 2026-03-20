import { Patient } from './patient.models';

export interface Appointment {
  id: string;
  patientId: string;
  patientName?: string;
  patientCode?: string;
  cycleId?: string;
  doctorId?: string;
  doctorName?: string;
  scheduledAt: string;
  durationMinutes: number;
  type: AppointmentType;
  status: AppointmentStatus;
  notes?: string;
  roomNumber?: string;
  patient?: Patient;
  doctor?: any;
  createdAt: string;
  updatedAt?: string;
}

export type AppointmentType =
  | 'Consultation'
  | 'Ultrasound'
  | 'Injection'
  | 'EggRetrieval'
  | 'EmbryoTransfer'
  | 'LabTest'
  | 'SemenCollection'
  | 'FollowUp'
  | 'Other';
export type AppointmentStatus =
  | 'Scheduled'
  | 'Confirmed'
  | 'CheckedIn'
  | 'InProgress'
  | 'Completed'
  | 'Cancelled'
  | 'NoShow'
  | 'Rescheduled';

export interface CreateAppointmentRequest {
  patientId: string;
  scheduledAt: string;
  type: AppointmentType;
  cycleId?: string;
  doctorId?: string;
  durationMinutes?: number;
  notes?: string;
  roomNumber?: string;
}
