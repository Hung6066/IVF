export interface Invoice {
  id: string;
  invoiceNumber: string;
  patientId: string;
  status: InvoiceStatus;
  invoiceDate: string;
  subTotal: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  paidAmount: number;
  items?: InvoiceItem[];
}

export interface InvoiceItem {
  id: string;
  serviceCode: string;
  description: string;
  quantity: number;
  unitPrice: number;
  amount: number;
  feeType?: 'IVFMD' | 'Hospital'; // P10.06: distinguish IVFMD vs Hospital (BV Mỹ Đức) fees
}

export type InvoiceStatus =
  | 'Draft'
  | 'Issued'
  | 'PartiallyPaid'
  | 'Paid'
  | 'Refunded'
  | 'Cancelled';
