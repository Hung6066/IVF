import {
  Component,
  OnInit,
  OnDestroy,
  inject,
  signal,
  computed,
  HostListener,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CdkDragDrop, CdkDragMove, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import {
  FormsService,
  FormField,
  ReportTemplate,
  ReportData,
  ReportType,
  FieldType,
  FieldTypeLabels,
  ReportDesign,
  ReportBand,
  ReportControl,
  ReportControlStyle,
  ReportParameter,
  ReportDataSource,
  ReportStyleDef,
  BandType,
  ControlType,
  SubReportConfig,
  ReportTab,
  CrossTabConfig,
  ReportTemplateLibraryItem,
  ReportPageConfig,
} from '../forms.service';

// ===== Control palette items =====
interface ToolboxItem {
  type: ControlType;
  icon: string;
  label: string;
  defaultWidth: number;
  defaultHeight: number;
}

const TOOLBOX_ITEMS: ToolboxItem[] = [
  { type: 'label', icon: 'Aa', label: 'Nhãn', defaultWidth: 150, defaultHeight: 24 },
  { type: 'field', icon: '📊', label: 'Trường dữ liệu', defaultWidth: 150, defaultHeight: 24 },
  { type: 'expression', icon: 'fx', label: 'Biểu thức', defaultWidth: 150, defaultHeight: 24 },
  { type: 'richText', icon: '📝', label: 'Rich Text', defaultWidth: 250, defaultHeight: 40 },
  { type: 'image', icon: '🖼️', label: 'Hình ảnh', defaultWidth: 100, defaultHeight: 80 },
  { type: 'shape', icon: '⬛', label: 'Hình khối', defaultWidth: 80, defaultHeight: 80 },
  { type: 'line', icon: '—', label: 'Đường kẻ', defaultWidth: 200, defaultHeight: 2 },
  { type: 'barcode', icon: '|||', label: 'Barcode/QR', defaultWidth: 100, defaultHeight: 60 },
  { type: 'chart', icon: '📈', label: 'Biểu đồ', defaultWidth: 300, defaultHeight: 200 },
  { type: 'table', icon: '▦', label: 'Bảng', defaultWidth: 400, defaultHeight: 100 },
  { type: 'checkbox', icon: '☑', label: 'Checkbox', defaultWidth: 120, defaultHeight: 20 },
  { type: 'pageNumber', icon: '#', label: 'Số trang', defaultWidth: 80, defaultHeight: 20 },
  { type: 'totalPages', icon: '##', label: 'Tổng trang', defaultWidth: 80, defaultHeight: 20 },
  { type: 'currentDate', icon: '📅', label: 'Ngày hiện tại', defaultWidth: 120, defaultHeight: 20 },
  { type: 'signatureZone', icon: '✍️', label: 'Vùng chữ ký', defaultWidth: 200, defaultHeight: 70 },
];

const BAND_LABELS: Record<BandType, string> = {
  reportHeader: 'Report Header',
  pageHeader: 'Page Header',
  groupHeader: 'Group Header',
  detail: 'Detail',
  groupFooter: 'Group Footer',
  pageFooter: 'Page Footer',
  reportFooter: 'Report Footer',
};

// ===== Built-in template library =====
const TEMPLATE_LIBRARY: ReportTemplateLibraryItem[] = [
  {
    id: 'tpl_patient_list',
    name: 'Danh sách bệnh nhân',
    description: 'Báo cáo bảng đơn giản với header & footer',
    category: 'Bảng cơ bản',
    designJson: JSON.stringify({
      bands: [
        {
          id: 'b1',
          type: 'pageHeader',
          height: 60,
          visible: true,
          controls: [
            {
              id: 'c1',
              type: 'label',
              x: 0,
              y: 10,
              width: 400,
              height: 30,
              text: 'DANH SÁCH BỆNH NHÂN',
              style: { fontSize: 18, fontWeight: 'bold', textAlign: 'center' },
            },
            {
              id: 'c2',
              type: 'currentDate',
              x: 400,
              y: 15,
              width: 150,
              height: 20,
              format: 'dd/MM/yyyy',
              style: { textAlign: 'right', fontSize: 10 },
            },
          ],
        },
        {
          id: 'b2',
          type: 'detail',
          height: 28,
          visible: true,
          controls: [
            {
              id: 'c3',
              type: 'field',
              x: 0,
              y: 4,
              width: 200,
              height: 20,
              dataField: 'patientName',
              style: { fontSize: 10 },
            },
            {
              id: 'c4',
              type: 'field',
              x: 210,
              y: 4,
              width: 120,
              height: 20,
              dataField: 'submittedAt',
              format: 'dd/MM/yyyy',
              style: { fontSize: 10 },
            },
            {
              id: 'c5',
              type: 'field',
              x: 340,
              y: 4,
              width: 100,
              height: 20,
              dataField: 'status',
              style: { fontSize: 10 },
            },
          ],
        },
        {
          id: 'b3',
          type: 'pageFooter',
          height: 30,
          visible: true,
          controls: [
            {
              id: 'c6',
              type: 'pageNumber',
              x: 200,
              y: 5,
              width: 80,
              height: 20,
              style: { textAlign: 'center', fontSize: 9 },
            },
          ],
        },
      ],
      parameters: [],
      dataSources: [],
      pageSettings: {
        size: 'A4',
        orientation: 'portrait',
        margins: { top: 30, right: 30, bottom: 30, left: 30 },
      },
      styles: [],
    }),
  },
  {
    id: 'tpl_grouped',
    name: 'Báo cáo nhóm',
    description: 'Báo cáo với GroupHeader/Footer và tổng hợp',
    category: 'Nhóm',
    designJson: JSON.stringify({
      bands: [
        {
          id: 'b1',
          type: 'reportHeader',
          height: 80,
          visible: true,
          controls: [
            {
              id: 'c1',
              type: 'label',
              x: 0,
              y: 10,
              width: 500,
              height: 30,
              text: 'BÁO CÁO TỔNG HỢP',
              style: { fontSize: 20, fontWeight: 'bold', textAlign: 'center', color: '#667eea' },
            },
            {
              id: 'c2',
              type: 'label',
              x: 0,
              y: 45,
              width: 500,
              height: 20,
              text: 'Phân nhóm theo trạng thái',
              style: { fontSize: 11, textAlign: 'center', fontStyle: 'italic', color: '#64748b' },
            },
          ],
        },
        {
          id: 'b2',
          type: 'groupHeader',
          height: 32,
          visible: true,
          groupField: 'status',
          controls: [
            {
              id: 'c3',
              type: 'field',
              x: 0,
              y: 6,
              width: 300,
              height: 20,
              dataField: 'status',
              style: {
                fontSize: 12,
                fontWeight: 'bold',
                backgroundColor: '#eff6ff',
                color: '#1e40af',
              },
            },
          ],
        },
        {
          id: 'b3',
          type: 'detail',
          height: 24,
          visible: true,
          controls: [
            {
              id: 'c4',
              type: 'field',
              x: 20,
              y: 2,
              width: 200,
              height: 20,
              dataField: 'patientName',
              style: { fontSize: 10 },
            },
            {
              id: 'c5',
              type: 'field',
              x: 230,
              y: 2,
              width: 120,
              height: 20,
              dataField: 'submittedAt',
              format: 'dd/MM/yyyy',
              style: { fontSize: 10 },
            },
          ],
        },
        {
          id: 'b4',
          type: 'groupFooter',
          height: 28,
          visible: true,
          groupField: 'status',
          controls: [
            {
              id: 'c6',
              type: 'expression',
              x: 0,
              y: 4,
              width: 300,
              height: 20,
              expression: '"Tổng: " + Count()',
              style: { fontSize: 10, fontWeight: 'bold', backgroundColor: '#f8fafc' },
            },
          ],
        },
        {
          id: 'b5',
          type: 'pageFooter',
          height: 30,
          visible: true,
          controls: [
            {
              id: 'c7',
              type: 'label',
              x: 0,
              y: 5,
              width: 200,
              height: 20,
              text: 'IVF Report System',
              style: { fontSize: 8, color: '#94a3b8' },
            },
            {
              id: 'c8',
              type: 'pageNumber',
              x: 250,
              y: 5,
              width: 80,
              height: 20,
              style: { textAlign: 'center', fontSize: 8 },
            },
          ],
        },
      ],
      parameters: [],
      dataSources: [],
      pageSettings: {
        size: 'A4',
        orientation: 'landscape',
        margins: { top: 25, right: 25, bottom: 25, left: 25 },
      },
      styles: [],
    }),
  },
  {
    id: 'tpl_cover',
    name: 'Bìa hồ sơ',
    description: 'Trang bìa cho hồ sơ bệnh nhân IVF',
    category: 'Chuyên dụng',
    designJson: JSON.stringify({
      bands: [
        {
          id: 'b1',
          type: 'reportHeader',
          height: 400,
          visible: true,
          controls: [
            {
              id: 'c1',
              type: 'label',
              x: 50,
              y: 60,
              width: 400,
              height: 40,
              text: 'HỒ SƠ BỆNH ÁN',
              style: { fontSize: 28, fontWeight: 'bold', textAlign: 'center', color: '#1e293b' },
            },
            {
              id: 'c2',
              type: 'label',
              x: 50,
              y: 110,
              width: 400,
              height: 30,
              text: 'TRUNG TÂM HỖ TRỢ SINH SẢN',
              style: { fontSize: 16, textAlign: 'center', color: '#667eea' },
            },
            {
              id: 'c3',
              type: 'line',
              x: 100,
              y: 155,
              width: 300,
              height: 2,
              style: { borderColor: '#667eea', borderWidth: 2 },
            },
            {
              id: 'c4',
              type: 'label',
              x: 80,
              y: 180,
              width: 120,
              height: 22,
              text: 'Bệnh nhân:',
              style: { fontSize: 12, fontWeight: 'bold' },
            },
            {
              id: 'c5',
              type: 'field',
              x: 210,
              y: 180,
              width: 250,
              height: 22,
              dataField: 'patientName',
              style: { fontSize: 12 },
            },
            {
              id: 'c6',
              type: 'label',
              x: 80,
              y: 210,
              width: 120,
              height: 22,
              text: 'Ngày khám:',
              style: { fontSize: 12, fontWeight: 'bold' },
            },
            {
              id: 'c7',
              type: 'field',
              x: 210,
              y: 210,
              width: 250,
              height: 22,
              dataField: 'submittedAt',
              format: 'dd/MM/yyyy',
              style: { fontSize: 12 },
            },
            {
              id: 'c8',
              type: 'label',
              x: 80,
              y: 240,
              width: 120,
              height: 22,
              text: 'Trạng thái:',
              style: { fontSize: 12, fontWeight: 'bold' },
            },
            {
              id: 'c9',
              type: 'field',
              x: 210,
              y: 240,
              width: 250,
              height: 22,
              dataField: 'status',
              style: { fontSize: 12 },
            },
            {
              id: 'c10',
              type: 'currentDate',
              x: 130,
              y: 320,
              width: 250,
              height: 22,
              format: 'dd/MM/yyyy',
              style: { fontSize: 11, textAlign: 'center', fontStyle: 'italic' },
            },
          ],
        },
        {
          id: 'b2',
          type: 'pageFooter',
          height: 30,
          visible: true,
          controls: [
            {
              id: 'c11',
              type: 'label',
              x: 0,
              y: 5,
              width: 500,
              height: 20,
              text: '© IVF Information System',
              style: { fontSize: 8, color: '#94a3b8', textAlign: 'center' },
            },
          ],
        },
      ],
      parameters: [],
      dataSources: [],
      pageSettings: {
        size: 'A4',
        orientation: 'portrait',
        margins: { top: 40, right: 40, bottom: 40, left: 40 },
      },
      styles: [],
    }),
  },
  {
    id: 'tpl_semen_analysis',
    name: 'Phiếu xét nghiệm tinh dịch đồ',
    description: 'Phiếu kết quả XN tinh dịch đồ theo WHO 2021 – dạng y khoa chuyên nghiệp',
    category: 'Chuyên dụng',
    designJson: JSON.stringify({
      bands: [
        // ========== PAGE HEADER ==========
        {
          id: 'sa_h',
          type: 'pageHeader',
          height: 140,
          visible: true,
          controls: [
            // Hospital name
            {
              id: 'sa_h1',
              type: 'label',
              x: 0,
              y: 4,
              width: 340,
              height: 22,
              text: 'TRUNG TÂM HỖ TRỢ SINH SẢN',
              style: { fontSize: 13, fontWeight: 'bold', color: '#0D47A1' },
            },
            {
              id: 'sa_h2',
              type: 'label',
              x: 0,
              y: 24,
              width: 340,
              height: 18,
              text: 'PHÒNG XÉT NGHIỆM NAM KHOA',
              style: { fontSize: 10, color: '#1565C0' },
            },
            // Right side
            {
              id: 'sa_h3',
              type: 'label',
              x: 520,
              y: 4,
              width: 220,
              height: 18,
              text: 'BỘ Y TẾ',
              style: { fontSize: 10, fontWeight: 'bold', textAlign: 'right', color: '#616161' },
            },
            {
              id: 'sa_h4',
              type: 'currentDate',
              x: 520,
              y: 22,
              width: 220,
              height: 18,
              format: 'dd/MM/yyyy',
              style: { fontSize: 9, textAlign: 'right', color: '#757575' },
            },
            // Divider
            {
              id: 'sa_h5',
              type: 'line',
              x: 0,
              y: 48,
              width: 740,
              height: 3,
              style: { borderColor: '#1565C0', borderWidth: 3 },
            },
            // Title
            {
              id: 'sa_h6',
              type: 'label',
              x: 0,
              y: 56,
              width: 740,
              height: 28,
              text: 'PHIẾU KẾT QUẢ XÉT NGHIỆM TINH DỊCH ĐỒ',
              style: { fontSize: 17, fontWeight: 'bold', textAlign: 'center', color: '#0D47A1' },
            },
            {
              id: 'sa_h7',
              type: 'label',
              x: 0,
              y: 82,
              width: 740,
              height: 18,
              text: '(Theo tiêu chuẩn WHO 2021)',
              style: { fontSize: 10, fontStyle: 'italic', textAlign: 'center', color: '#757575' },
            },
            // Patient info row
            {
              id: 'sa_h8',
              type: 'label',
              x: 0,
              y: 108,
              width: 70,
              height: 20,
              text: 'Họ tên:',
              style: { fontSize: 10, fontWeight: 'bold' },
            },
            {
              id: 'sa_h9',
              type: 'field',
              x: 70,
              y: 108,
              width: 220,
              height: 20,
              dataField: 'patientName',
              style: { fontSize: 11, fontWeight: 'bold' },
            },
            {
              id: 'sa_h10',
              type: 'label',
              x: 310,
              y: 108,
              width: 80,
              height: 20,
              text: 'Ngày XN:',
              style: { fontSize: 10, fontWeight: 'bold' },
            },
            {
              id: 'sa_h11',
              type: 'field',
              x: 390,
              y: 108,
              width: 120,
              height: 20,
              dataField: 'submittedAt',
              format: 'dd/MM/yyyy',
              style: { fontSize: 10 },
            },
            {
              id: 'sa_h12',
              type: 'label',
              x: 530,
              y: 108,
              width: 80,
              height: 20,
              text: 'Ngày kiêng:',
              style: { fontSize: 10, fontWeight: 'bold' },
            },
            {
              id: 'sa_h13',
              type: 'field',
              x: 610,
              y: 108,
              width: 50,
              height: 20,
              dataField: 'abstinence_days',
              style: { fontSize: 10, textAlign: 'center' },
            },
            {
              id: 'sa_h14',
              type: 'label',
              x: 660,
              y: 108,
              width: 40,
              height: 20,
              text: 'ngày',
              style: { fontSize: 10, color: '#757575' },
            },
          ],
        },
        // ========== DETAIL BAND ==========
        {
          id: 'sa_d',
          type: 'detail',
          height: 530,
          visible: true,
          controls: [
            // ── Collection info ──
            {
              id: 'sa_d1',
              type: 'label',
              x: 0,
              y: 0,
              width: 120,
              height: 20,
              text: 'PP lấy mẫu:',
              style: { fontSize: 10, fontWeight: 'bold' },
            },
            {
              id: 'sa_d2',
              type: 'field',
              x: 120,
              y: 0,
              width: 200,
              height: 20,
              dataField: 'collection_method',
              style: { fontSize: 10 },
            },
            {
              id: 'sa_d3',
              type: 'label',
              x: 340,
              y: 0,
              width: 100,
              height: 20,
              text: 'Ngày lấy mẫu:',
              style: { fontSize: 10, fontWeight: 'bold' },
            },
            {
              id: 'sa_d4',
              type: 'field',
              x: 440,
              y: 0,
              width: 160,
              height: 20,
              dataField: 'collection_date',
              format: 'dd/MM/yyyy HH:mm',
              style: { fontSize: 10 },
            },

            // ── Section I: ĐẠI THỂ ──
            {
              id: 'sa_s1',
              type: 'label',
              x: 0,
              y: 32,
              width: 740,
              height: 24,
              text: 'I. ĐẠI THỂ (MACROSCOPIC)',
              style: {
                fontSize: 11,
                fontWeight: 'bold',
                color: '#0D47A1',
                backgroundColor: '#E8EAF6',
                padding: 4,
              },
            },
            // Table header
            {
              id: 'sa_th1',
              type: 'label',
              x: 0,
              y: 58,
              width: 240,
              height: 22,
              text: 'Chỉ số',
              style: {
                fontSize: 9,
                fontWeight: 'bold',
                color: '#FFFFFF',
                backgroundColor: '#0D47A1',
                padding: 4,
              },
            },
            {
              id: 'sa_th2',
              type: 'label',
              x: 240,
              y: 58,
              width: 130,
              height: 22,
              text: 'Kết quả',
              style: {
                fontSize: 9,
                fontWeight: 'bold',
                color: '#FFFFFF',
                backgroundColor: '#0D47A1',
                padding: 4,
                textAlign: 'center',
              },
            },
            {
              id: 'sa_th3',
              type: 'label',
              x: 370,
              y: 58,
              width: 100,
              height: 22,
              text: 'Đơn vị',
              style: {
                fontSize: 9,
                fontWeight: 'bold',
                color: '#FFFFFF',
                backgroundColor: '#0D47A1',
                padding: 4,
                textAlign: 'center',
              },
            },
            {
              id: 'sa_th4',
              type: 'label',
              x: 470,
              y: 58,
              width: 140,
              height: 22,
              text: 'Tham chiếu WHO',
              style: {
                fontSize: 9,
                fontWeight: 'bold',
                color: '#FFFFFF',
                backgroundColor: '#0D47A1',
                padding: 4,
                textAlign: 'center',
              },
            },
            {
              id: 'sa_th5',
              type: 'label',
              x: 610,
              y: 58,
              width: 130,
              height: 22,
              text: 'Đánh giá',
              style: {
                fontSize: 9,
                fontWeight: 'bold',
                color: '#FFFFFF',
                backgroundColor: '#0D47A1',
                padding: 4,
                textAlign: 'center',
              },
            },

            // Row 1: Thể tích
            {
              id: 'sa_r1a',
              type: 'label',
              x: 0,
              y: 82,
              width: 240,
              height: 22,
              text: 'Thể tích',
              style: { fontSize: 10, padding: 4, borderColor: '#E0E0E0', borderWidth: 1 },
            },
            {
              id: 'sa_r1b',
              type: 'field',
              x: 240,
              y: 82,
              width: 130,
              height: 22,
              dataField: 'volume',
              style: {
                fontSize: 11,
                fontWeight: 'bold',
                textAlign: 'center',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r1c',
              type: 'label',
              x: 370,
              y: 82,
              width: 100,
              height: 22,
              text: 'ml',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r1d',
              type: 'label',
              x: 470,
              y: 82,
              width: 140,
              height: 22,
              text: '≥ 1.4',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r1e',
              type: 'expression',
              x: 610,
              y: 82,
              width: 130,
              height: 22,
              expression: 'Iif(volume >= 1.4, "Bình thường", "Thấp ↓")',
              style: {
                fontSize: 9,
                fontWeight: 'bold',
                textAlign: 'center',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },

            // Row 2: pH
            {
              id: 'sa_r2a',
              type: 'label',
              x: 0,
              y: 104,
              width: 240,
              height: 22,
              text: 'pH',
              style: {
                fontSize: 10,
                padding: 4,
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r2b',
              type: 'field',
              x: 240,
              y: 104,
              width: 130,
              height: 22,
              dataField: 'ph',
              style: {
                fontSize: 11,
                fontWeight: 'bold',
                textAlign: 'center',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r2c',
              type: 'label',
              x: 370,
              y: 104,
              width: 100,
              height: 22,
              text: '',
              style: {
                fontSize: 9,
                textAlign: 'center',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r2d',
              type: 'label',
              x: 470,
              y: 104,
              width: 140,
              height: 22,
              text: '7.2 – 8.0',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r2e',
              type: 'expression',
              x: 610,
              y: 104,
              width: 130,
              height: 22,
              expression: 'Iif(ph >= 7.2, Iif(ph <= 8.0, "Bình thường", "Cao ↑"), "Thấp ↓")',
              style: {
                fontSize: 9,
                fontWeight: 'bold',
                textAlign: 'center',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },

            // Row 3: Màu sắc
            {
              id: 'sa_r3a',
              type: 'label',
              x: 0,
              y: 126,
              width: 240,
              height: 22,
              text: 'Màu sắc',
              style: { fontSize: 10, padding: 4, borderColor: '#E0E0E0', borderWidth: 1 },
            },
            {
              id: 'sa_r3b',
              type: 'field',
              x: 240,
              y: 126,
              width: 130,
              height: 22,
              dataField: 'appearance',
              style: {
                fontSize: 11,
                fontWeight: 'bold',
                textAlign: 'center',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r3c',
              type: 'label',
              x: 370,
              y: 126,
              width: 100,
              height: 22,
              text: '',
              style: { fontSize: 9, borderColor: '#E0E0E0', borderWidth: 1 },
            },
            {
              id: 'sa_r3d',
              type: 'label',
              x: 470,
              y: 126,
              width: 140,
              height: 22,
              text: 'Trắng đục',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r3e',
              type: 'label',
              x: 610,
              y: 126,
              width: 130,
              height: 22,
              text: '',
              style: { fontSize: 9, textAlign: 'center', borderColor: '#E0E0E0', borderWidth: 1 },
            },

            // Row 4: Hóa lỏng
            {
              id: 'sa_r4a',
              type: 'label',
              x: 0,
              y: 148,
              width: 240,
              height: 22,
              text: 'Thời gian hóa lỏng',
              style: {
                fontSize: 10,
                padding: 4,
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r4b',
              type: 'field',
              x: 240,
              y: 148,
              width: 130,
              height: 22,
              dataField: 'liquefaction',
              style: {
                fontSize: 11,
                fontWeight: 'bold',
                textAlign: 'center',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r4c',
              type: 'label',
              x: 370,
              y: 148,
              width: 100,
              height: 22,
              text: 'phút',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r4d',
              type: 'label',
              x: 470,
              y: 148,
              width: 140,
              height: 22,
              text: '< 60',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r4e',
              type: 'expression',
              x: 610,
              y: 148,
              width: 130,
              height: 22,
              expression: 'Iif(liquefaction < 60, "Bình thường", "Bất thường ↑")',
              style: {
                fontSize: 9,
                fontWeight: 'bold',
                textAlign: 'center',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },

            // ── Section II: VI THỂ ──
            {
              id: 'sa_s2',
              type: 'label',
              x: 0,
              y: 180,
              width: 740,
              height: 24,
              text: 'II. VI THỂ (MICROSCOPIC)',
              style: {
                fontSize: 11,
                fontWeight: 'bold',
                color: '#0D47A1',
                backgroundColor: '#E8EAF6',
                padding: 4,
              },
            },

            // Row 5: Mật độ
            {
              id: 'sa_r5a',
              type: 'label',
              x: 0,
              y: 206,
              width: 240,
              height: 22,
              text: 'Mật độ tinh trùng',
              style: { fontSize: 10, padding: 4, borderColor: '#E0E0E0', borderWidth: 1 },
            },
            {
              id: 'sa_r5b',
              type: 'field',
              x: 240,
              y: 206,
              width: 130,
              height: 22,
              dataField: 'concentration',
              style: {
                fontSize: 11,
                fontWeight: 'bold',
                textAlign: 'center',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r5c',
              type: 'label',
              x: 370,
              y: 206,
              width: 100,
              height: 22,
              text: 'triệu/ml',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r5d',
              type: 'label',
              x: 470,
              y: 206,
              width: 140,
              height: 22,
              text: '≥ 16',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r5e',
              type: 'expression',
              x: 610,
              y: 206,
              width: 130,
              height: 22,
              expression: 'Iif(concentration >= 16, "Bình thường", "Thấp ↓")',
              style: {
                fontSize: 9,
                fontWeight: 'bold',
                textAlign: 'center',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },

            // Row 6: Tổng số
            {
              id: 'sa_r6a',
              type: 'label',
              x: 0,
              y: 228,
              width: 240,
              height: 22,
              text: 'Tổng số tinh trùng',
              style: {
                fontSize: 10,
                padding: 4,
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r6b',
              type: 'field',
              x: 240,
              y: 228,
              width: 130,
              height: 22,
              dataField: 'total_count',
              style: {
                fontSize: 11,
                fontWeight: 'bold',
                textAlign: 'center',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r6c',
              type: 'label',
              x: 370,
              y: 228,
              width: 100,
              height: 22,
              text: 'triệu',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r6d',
              type: 'label',
              x: 470,
              y: 228,
              width: 140,
              height: 22,
              text: '≥ 39',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r6e',
              type: 'expression',
              x: 610,
              y: 228,
              width: 130,
              height: 22,
              expression: 'Iif(total_count >= 39, "Bình thường", "Thấp ↓")',
              style: {
                fontSize: 9,
                fontWeight: 'bold',
                textAlign: 'center',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },

            // Row 7: Di động PR
            {
              id: 'sa_r7a',
              type: 'label',
              x: 0,
              y: 250,
              width: 240,
              height: 22,
              text: 'Di động tiến tới (PR)',
              style: { fontSize: 10, padding: 4, borderColor: '#E0E0E0', borderWidth: 1 },
            },
            {
              id: 'sa_r7b',
              type: 'field',
              x: 240,
              y: 250,
              width: 130,
              height: 22,
              dataField: 'motility_pr',
              style: {
                fontSize: 11,
                fontWeight: 'bold',
                textAlign: 'center',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r7c',
              type: 'label',
              x: 370,
              y: 250,
              width: 100,
              height: 22,
              text: '%',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r7d',
              type: 'label',
              x: 470,
              y: 250,
              width: 140,
              height: 22,
              text: '≥ 30',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r7e',
              type: 'expression',
              x: 610,
              y: 250,
              width: 130,
              height: 22,
              expression: 'Iif(motility_pr >= 30, "Bình thường", "Thấp ↓")',
              style: {
                fontSize: 9,
                fontWeight: 'bold',
                textAlign: 'center',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },

            // Row 8: Di động NP
            {
              id: 'sa_r8a',
              type: 'label',
              x: 0,
              y: 272,
              width: 240,
              height: 22,
              text: 'Di động tại chỗ (NP)',
              style: {
                fontSize: 10,
                padding: 4,
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r8b',
              type: 'field',
              x: 240,
              y: 272,
              width: 130,
              height: 22,
              dataField: 'motility_np',
              style: {
                fontSize: 11,
                fontWeight: 'bold',
                textAlign: 'center',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r8c',
              type: 'label',
              x: 370,
              y: 272,
              width: 100,
              height: 22,
              text: '%',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r8d',
              type: 'label',
              x: 470,
              y: 272,
              width: 140,
              height: 22,
              text: '–',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r8e',
              type: 'label',
              x: 610,
              y: 272,
              width: 130,
              height: 22,
              text: '',
              style: {
                fontSize: 9,
                textAlign: 'center',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },

            // Row 9: Bất động
            {
              id: 'sa_r9a',
              type: 'label',
              x: 0,
              y: 294,
              width: 240,
              height: 22,
              text: 'Bất động (IM)',
              style: { fontSize: 10, padding: 4, borderColor: '#E0E0E0', borderWidth: 1 },
            },
            {
              id: 'sa_r9b',
              type: 'field',
              x: 240,
              y: 294,
              width: 130,
              height: 22,
              dataField: 'immotile',
              style: {
                fontSize: 11,
                fontWeight: 'bold',
                textAlign: 'center',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r9c',
              type: 'label',
              x: 370,
              y: 294,
              width: 100,
              height: 22,
              text: '%',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r9d',
              type: 'label',
              x: 470,
              y: 294,
              width: 140,
              height: 22,
              text: '–',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r9e',
              type: 'label',
              x: 610,
              y: 294,
              width: 130,
              height: 22,
              text: '',
              style: { fontSize: 9, textAlign: 'center', borderColor: '#E0E0E0', borderWidth: 1 },
            },

            // Row 10: Hình dạng
            {
              id: 'sa_r10a',
              type: 'label',
              x: 0,
              y: 316,
              width: 240,
              height: 22,
              text: 'Hình dạng bình thường',
              style: {
                fontSize: 10,
                padding: 4,
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r10b',
              type: 'field',
              x: 240,
              y: 316,
              width: 130,
              height: 22,
              dataField: 'morphology_normal',
              style: {
                fontSize: 11,
                fontWeight: 'bold',
                textAlign: 'center',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r10c',
              type: 'label',
              x: 370,
              y: 316,
              width: 100,
              height: 22,
              text: '%',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r10d',
              type: 'label',
              x: 470,
              y: 316,
              width: 140,
              height: 22,
              text: '≥ 4',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r10e',
              type: 'expression',
              x: 610,
              y: 316,
              width: 130,
              height: 22,
              expression: 'Iif(morphology_normal >= 4, "Bình thường", "Thấp ↓")',
              style: {
                fontSize: 9,
                fontWeight: 'bold',
                textAlign: 'center',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },

            // ── Section III: CÁC CHỈ SỐ KHÁC ──
            {
              id: 'sa_s3',
              type: 'label',
              x: 0,
              y: 348,
              width: 740,
              height: 24,
              text: 'III. CÁC CHỈ SỐ KHÁC',
              style: {
                fontSize: 11,
                fontWeight: 'bold',
                color: '#0D47A1',
                backgroundColor: '#E8EAF6',
                padding: 4,
              },
            },

            // Row 11: WBC
            {
              id: 'sa_r11a',
              type: 'label',
              x: 0,
              y: 374,
              width: 240,
              height: 22,
              text: 'Bạch cầu (WBC)',
              style: { fontSize: 10, padding: 4, borderColor: '#E0E0E0', borderWidth: 1 },
            },
            {
              id: 'sa_r11b',
              type: 'field',
              x: 240,
              y: 374,
              width: 130,
              height: 22,
              dataField: 'wbc',
              style: {
                fontSize: 11,
                fontWeight: 'bold',
                textAlign: 'center',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r11c',
              type: 'label',
              x: 370,
              y: 374,
              width: 100,
              height: 22,
              text: 'triệu/ml',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r11d',
              type: 'label',
              x: 470,
              y: 374,
              width: 140,
              height: 22,
              text: '< 1.0',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r11e',
              type: 'expression',
              x: 610,
              y: 374,
              width: 130,
              height: 22,
              expression: 'Iif(wbc < 1.0, "Bình thường", "Cao ↑")',
              style: {
                fontSize: 9,
                fontWeight: 'bold',
                textAlign: 'center',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },

            // Row 12: Vitality
            {
              id: 'sa_r12a',
              type: 'label',
              x: 0,
              y: 396,
              width: 240,
              height: 22,
              text: 'Tỷ lệ sống (Vitality)',
              style: {
                fontSize: 10,
                padding: 4,
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r12b',
              type: 'field',
              x: 240,
              y: 396,
              width: 130,
              height: 22,
              dataField: 'vitality',
              style: {
                fontSize: 11,
                fontWeight: 'bold',
                textAlign: 'center',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r12c',
              type: 'label',
              x: 370,
              y: 396,
              width: 100,
              height: 22,
              text: '%',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r12d',
              type: 'label',
              x: 470,
              y: 396,
              width: 140,
              height: 22,
              text: '≥ 54',
              style: {
                fontSize: 9,
                textAlign: 'center',
                color: '#757575',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_r12e',
              type: 'expression',
              x: 610,
              y: 396,
              width: 130,
              height: 22,
              expression: 'Iif(vitality >= 54, "Bình thường", "Thấp ↓")',
              style: {
                fontSize: 9,
                fontWeight: 'bold',
                textAlign: 'center',
                backgroundColor: '#FAFAFA',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },

            // ── Diagnosis box ──
            {
              id: 'sa_dx1',
              type: 'label',
              x: 0,
              y: 430,
              width: 740,
              height: 24,
              text: 'CHẨN ĐOÁN',
              style: {
                fontSize: 11,
                fontWeight: 'bold',
                color: '#0D47A1',
                backgroundColor: '#E8EAF6',
                padding: 4,
              },
            },
            {
              id: 'sa_dx2',
              type: 'field',
              x: 0,
              y: 456,
              width: 740,
              height: 26,
              dataField: 'diagnosis',
              style: {
                fontSize: 13,
                fontWeight: 'bold',
                textAlign: 'center',
                borderColor: '#1565C0',
                borderWidth: 1,
                padding: 4,
              },
            },

            // ── Notes ──
            {
              id: 'sa_n1',
              type: 'label',
              x: 0,
              y: 492,
              width: 70,
              height: 18,
              text: 'Ghi chú:',
              style: { fontSize: 9, fontWeight: 'bold', color: '#616161' },
            },
            {
              id: 'sa_n2',
              type: 'field',
              x: 70,
              y: 492,
              width: 670,
              height: 30,
              dataField: 'notes',
              style: { fontSize: 9, fontStyle: 'italic', color: '#757575' },
            },
          ],
        },
        // ========== PAGE FOOTER ==========
        {
          id: 'sa_f',
          type: 'pageFooter',
          height: 90,
          visible: true,
          controls: [
            // Signature zones
            {
              id: 'sa_f1',
              type: 'signatureZone',
              x: 0,
              y: 0,
              width: 200,
              height: 55,
              text: 'KỸ THUẬT VIÊN',
              signatureRole: 'technician',
              style: {
                fontSize: 9,
                fontWeight: 'bold',
                textAlign: 'center',
                color: '#616161',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_f3',
              type: 'signatureZone',
              x: 270,
              y: 0,
              width: 200,
              height: 55,
              text: 'TRƯỞNG KHOA XN',
              signatureRole: 'department_head',
              style: {
                fontSize: 9,
                fontWeight: 'bold',
                textAlign: 'center',
                color: '#616161',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },
            {
              id: 'sa_f5',
              type: 'signatureZone',
              x: 540,
              y: 0,
              width: 200,
              height: 55,
              text: 'BÁC SĨ CHỈ ĐỊNH',
              signatureRole: 'doctor',
              style: {
                fontSize: 9,
                fontWeight: 'bold',
                textAlign: 'center',
                color: '#616161',
                borderColor: '#E0E0E0',
                borderWidth: 1,
              },
            },

            // Footer line
            {
              id: 'sa_f7',
              type: 'line',
              x: 0,
              y: 62,
              width: 740,
              height: 1,
              style: { borderColor: '#E0E0E0', borderWidth: 1 },
            },
            {
              id: 'sa_f8',
              type: 'label',
              x: 0,
              y: 68,
              width: 500,
              height: 16,
              text: 'Tiêu chuẩn tham chiếu: WHO Laboratory Manual, 6th Edition (2021)',
              style: { fontSize: 7, fontStyle: 'italic', color: '#9E9E9E' },
            },
            {
              id: 'sa_f9',
              type: 'pageNumber',
              x: 600,
              y: 68,
              width: 140,
              height: 16,
              text: 'Trang {page}',
              style: { fontSize: 7, textAlign: 'right', color: '#9E9E9E' },
            },
          ],
        },
      ],
      parameters: [],
      dataSources: [],
      pageSettings: {
        size: 'A4',
        orientation: 'portrait',
        margins: { top: 25, right: 30, bottom: 20, left: 30 },
      },
      styles: [],
    }),
  },
];

// ===== Helpers =====
function formatDateStr(isoStr: string, fmt: string): string {
  try {
    const d = new Date(isoStr);
    if (isNaN(d.getTime())) return isoStr;
    const pad = (n: number) => n.toString().padStart(2, '0');
    return fmt
      .replace('dd', pad(d.getDate()))
      .replace('MM', pad(d.getMonth() + 1))
      .replace('yyyy', d.getFullYear().toString())
      .replace('HH', pad(d.getHours()))
      .replace('mm', pad(d.getMinutes()))
      .replace('ss', pad(d.getSeconds()));
  } catch {
    return isoStr;
  }
}

// ===== Expression functions =====
function evaluateExpression(
  expr: string,
  row: Record<string, any>,
  allRows: Record<string, any>[],
): string {
  if (!expr) return '';
  let result = expr;

  // Replace [Data.fieldKey] references
  result = result.replace(/\[Data\.(\w+)\]/g, (_, key) => {
    return (row[key] ?? '').toString();
  });

  // Replace [fieldKey] short form
  result = result.replace(/\[(\w+)\]/g, (_, key) => {
    return (row[key] ?? '').toString();
  });

  // Replace bare field names with values (longest first to avoid partial matches)
  const fieldKeys = Object.keys(row).sort((a, b) => b.length - a.length);
  for (const key of fieldKeys) {
    const val = row[key];
    if (val !== undefined && val !== null) {
      result = result.replace(new RegExp(`\\b${key}\\b`, 'g'), val.toString());
    }
  }

  // Count() function
  result = result.replace(/Count\(\)/gi, allRows.length.toString());

  // Sum(fieldKey)
  result = result.replace(/Sum\((\w+)\)/gi, (_, key) => {
    return allRows.reduce((s, r) => s + (parseFloat(r[key]?.toString() ?? '0') || 0), 0).toFixed(2);
  });

  // Avg(fieldKey)
  result = result.replace(/Avg\((\w+)\)/gi, (_, key) => {
    const nums = allRows.map((r) => parseFloat(r[key]?.toString() ?? '0')).filter((n) => !isNaN(n));
    return nums.length ? (nums.reduce((a, n) => a + n, 0) / nums.length).toFixed(2) : '0';
  });

  // Min(fieldKey)
  result = result.replace(/Min\((\w+)\)/gi, (_, key) => {
    const nums = allRows.map((r) => parseFloat(r[key]?.toString() ?? '0')).filter((n) => !isNaN(n));
    return nums.length ? Math.min(...nums).toString() : '0';
  });

  // Max(fieldKey)
  result = result.replace(/Max\((\w+)\)/gi, (_, key) => {
    const nums = allRows.map((r) => parseFloat(r[key]?.toString() ?? '0')).filter((n) => !isNaN(n));
    return nums.length ? Math.max(...nums).toString() : '0';
  });

  // Upper()
  result = result.replace(/Upper\("?([^"]*?)"?\)/gi, (_, inner) => inner.toUpperCase());

  // Lower()
  result = result.replace(/Lower\("?([^"]*?)"?\)/gi, (_, inner) => inner.toLowerCase());

  // Iif(condition, trueVal, falseVal) — evaluate from innermost outward
  let iifLimit = 10;
  while (/Iif\s*\(/i.test(result) && iifLimit-- > 0) {
    result = evaluateInnermostIif(result);
  }

  // Format() for dates and numbers — simple passthrough
  result = result.replace(/Format\(([^,]+),\s*['"]([^'"]+)['"]\)/gi, (_, val, fmt) => {
    return val.trim();
  });

  // String concatenation with +
  if (result.includes('"') && result.includes('+')) {
    try {
      const parts = result.split('+').map((p) => p.trim().replace(/^"|"$/g, ''));
      result = parts.join('');
    } catch {
      /* keep as is */
    }
  }

  return result;
}

/** Parse and evaluate the innermost Iif() call (no nested Iif in its args) */
function evaluateInnermostIif(expr: string): string {
  // Find innermost Iif — one whose parenthesized content has no nested Iif
  const iifRe = /Iif\s*\(([^()]*)\)/i;
  const m = expr.match(iifRe);
  if (!m) return expr;

  const inner = m[1]; // content inside Iif(...)
  // Split by comma, respecting quoted strings
  const args = splitRespectingQuotes(inner);
  if (args.length < 3) return expr.replace(m[0], inner);

  const condition = args[0].trim();
  const trueVal = args[1].trim();
  const falseVal = args[2].trim();

  // Evaluate condition: supports >=, <=, >, <, ==, !=
  const condMatch = condition.match(/^(.+?)\s*(>=|<=|!=|<>|==|=|>|<)\s*(.+)$/);
  let condResult = false;
  if (condMatch) {
    const left = parseFloat(condMatch[1].replace(/['"]/g, ''));
    const right = parseFloat(condMatch[3].replace(/['"]/g, ''));
    switch (condMatch[2]) {
      case '>':
        condResult = left > right;
        break;
      case '<':
        condResult = left < right;
        break;
      case '>=':
        condResult = left >= right;
        break;
      case '<=':
        condResult = left <= right;
        break;
      case '==':
      case '=':
        condResult = left === right;
        break;
      case '!=':
      case '<>':
        condResult = left !== right;
        break;
    }
  }

  const chosen = condResult ? trueVal : falseVal;
  // Unquote string literals
  const unquoted = chosen.replace(/^["']|["']$/g, '');
  return expr.replace(m[0], unquoted);
}

/** Split a string by commas, but not inside quoted strings */
function splitRespectingQuotes(s: string): string[] {
  const parts: string[] = [];
  let current = '';
  let inQuote: string | null = null;
  for (let i = 0; i < s.length; i++) {
    const ch = s[i];
    if (inQuote) {
      current += ch;
      if (ch === inQuote) inQuote = null;
    } else if (ch === '"' || ch === "'") {
      current += ch;
      inQuote = ch;
    } else if (ch === ',') {
      parts.push(current);
      current = '';
    } else {
      current += ch;
    }
  }
  if (current) parts.push(current);
  return parts;
}

@Component({
  selector: 'app-report-designer',
  standalone: true,
  imports: [CommonModule, FormsModule, DragDropModule],
  templateUrl: './report-designer.component.html',
  styleUrls: ['./report-designer.component.scss'],
})
export class ReportDesignerComponent implements OnInit, OnDestroy {
  private readonly formsService = inject(FormsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  // ===== Undo / Redo =====
  private undoStack: string[] = [];
  private redoStack: string[] = [];
  private readonly MAX_UNDO = 50;
  private isUndoRedoAction = false;
  private snapshotTimer: any = null;

  // State
  reportId = '';
  report: ReportTemplate | null = null;
  formFields: FormField[] = [];
  isSaving = false;
  isDirty = false;
  mode: 'design' | 'preview' = 'design';
  sidePanel:
    | 'toolbox'
    | 'properties'
    | 'data'
    | 'parameters'
    | 'templates'
    | 'tabs'
    | 'crosstab'
    | 'explorer' = 'toolbox';
  sidePanelCollapsed = false;

  // The report design
  design: ReportDesign = this.getDefaultDesign();

  // Selection state
  selectedBandId: string | null = null;
  selectedControlId: string | null = null;
  /** Multi-select: IDs of all selected controls (within the same band) */
  selectedControlIds: Set<string> = new Set();

  // Preview
  previewData: ReportData | null = null;
  isLoadingPreview = false;

  // Dragging
  isDraggingControl = false;
  draggedToolboxItem: ToolboxItem | null = null;

  // Rubber-band (lasso) selection
  isRubberBanding = false;
  rubberBandStart: { x: number; y: number } | null = null;
  rubberBandRect = { x: 0, y: 0, w: 0, h: 0 };
  rubberBandBandId: string | null = null;

  // Resize via drag handle
  isResizing = false;
  resizeHandle: string | null = null; // 'n','s','e','w','ne','nw','se','sw'
  resizeStart: {
    x: number;
    y: number;
    ctrlX: number;
    ctrlY: number;
    ctrlW: number;
    ctrlH: number;
  } | null = null;
  resizeControlId: string | null = null;

  // Context menu
  showContextMenu = false;
  contextMenuPos = { x: 0, y: 0 };

  // Snap-to-grid
  snapToGrid = true;
  gridSize = 5;
  showGrid = true;

  // Smart guides (alignment snap lines)
  guideLines: { orientation: 'h' | 'v'; pos: number; bandId: string }[] = [];
  draggedControlRef: ReportControl | null = null;
  readonly GUIDE_SNAP_THRESHOLD = 3; // px threshold to show guide

  // Parameters at runtime
  parameterValues: Record<string, string> = {};
  showParametersDialog = false;

  // Template library
  templateLibrary = TEMPLATE_LIBRARY;
  templateCategories: string[] = [];

  // Expression editor
  showExpressionEditor = false;
  expressionTarget: 'control' | 'parameter' = 'control';
  expressionValue = '';

  // Cross-tab preview
  crossTabData: {
    headers: string[];
    rows: { label: string; values: (string | number)[] }[];
    totals: (string | number)[];
  } | null = null;

  // Property grid collapsible sections
  propSections: Record<string, boolean> = {
    general: false,
    layout: false,
    style: false,
    layoutOps: false,
    arrangement: false,
  };

  // Report Explorer state
  explorerCollapsedBands: Set<string> = new Set();
  explorerEditingId: string | null = null;
  explorerEditingName: string = '';
  controlNames: Record<string, string> = {};

  // Constants
  readonly TOOLBOX_ITEMS = TOOLBOX_ITEMS;
  readonly BAND_LABELS = BAND_LABELS;
  readonly BAND_TYPES: BandType[] = [
    'reportHeader',
    'pageHeader',
    'groupHeader',
    'detail',
    'groupFooter',
    'pageFooter',
    'reportFooter',
  ];

  get selectedBand(): ReportBand | null {
    return this.design.bands.find((b) => b.id === this.selectedBandId) ?? null;
  }

  get selectedControl(): ReportControl | null {
    for (const band of this.design.bands) {
      const ctrl = band.controls.find((c) => c.id === this.selectedControlId);
      if (ctrl) return ctrl;
    }
    return null;
  }

  get dataFieldOptions(): { key: string; label: string }[] {
    const options: { key: string; label: string }[] = [
      { key: 'patientName', label: 'Bệnh nhân' },
      { key: 'submittedAt', label: 'Ngày nộp' },
      { key: 'status', label: 'Trạng thái' },
    ];
    for (const f of this.formFields) {
      if (
        f.fieldType !== FieldType.Section &&
        f.fieldType !== FieldType.Label &&
        f.fieldType !== FieldType.PageBreak
      ) {
        options.push({ key: f.fieldKey, label: f.label });
      }
    }
    return options;
  }

  get activeTabDesign(): ReportDesign {
    if (this.design.tabs?.length && this.activeTabId) {
      const tab = this.design.tabs.find((t) => t.id === this.activeTabId);
      if (tab) {
        try {
          return JSON.parse(tab.designJson);
        } catch {
          /* fallback */
        }
      }
    }
    return this.design;
  }

  activeTabId: string | null = null;

  @HostListener('window:keydown', ['$event'])
  onKeyDown(e: KeyboardEvent) {
    // Don't intercept if user is typing in input/textarea/select
    const tag = (e.target as HTMLElement).tagName?.toLowerCase();
    if (tag === 'input' || tag === 'textarea' || tag === 'select') return;

    if ((e.ctrlKey || e.metaKey) && e.key === 'z' && !e.shiftKey) {
      e.preventDefault();
      this.undo();
    } else if (
      ((e.ctrlKey || e.metaKey) && e.key === 'y') ||
      ((e.ctrlKey || e.metaKey) && e.shiftKey && e.key === 'z')
    ) {
      e.preventDefault();
      this.redo();
    } else if ((e.ctrlKey || e.metaKey) && e.key === 'a') {
      // Ctrl+A: Select all controls in current band
      e.preventDefault();
      this.selectAllInBand();
    } else if ((e.ctrlKey || e.metaKey) && e.key === 'c') {
      e.preventDefault();
      this.copySelected();
    } else if ((e.ctrlKey || e.metaKey) && e.key === 'x') {
      e.preventDefault();
      this.cutSelected();
    } else if ((e.ctrlKey || e.metaKey) && e.key === 'v') {
      e.preventDefault();
      this.pasteControls();
    } else if ((e.ctrlKey || e.metaKey) && e.key === 'd') {
      // Ctrl+D: Duplicate selected
      e.preventDefault();
      if (this.selectedControlIds.size > 0) {
        const ctrls = this.selectedControls;
        const band = this.design.bands.find((b) => b.id === this.selectedBandId);
        if (band) {
          this.selectedControlIds.clear();
          for (const orig of ctrls) {
            const copy: ReportControl = {
              ...JSON.parse(JSON.stringify(orig)),
              id: crypto.randomUUID(),
              x: orig.x + 10,
              y: orig.y + 10,
            };
            band.controls.push(copy);
            this.selectedControlIds.add(copy.id);
          }
          this.selectedControlId = [...this.selectedControlIds][0];
          this.markDirty();
        }
      }
    } else if (e.key === 'Delete' || e.key === 'Backspace') {
      e.preventDefault();
      this.deleteSelected();
    } else if (e.key === 'Escape') {
      this.clearSelection();
    } else if (
      e.key === 'ArrowLeft' ||
      e.key === 'ArrowRight' ||
      e.key === 'ArrowUp' ||
      e.key === 'ArrowDown'
    ) {
      // Arrow keys: nudge selected controls
      // Shift = large step (10px), Ctrl = fine step (1px), default = gridSize when snap enabled
      let ctrls = this.selectedControls;
      // Fallback: single selected control
      if (ctrls.length === 0 && this.selectedControlId && this.selectedBandId) {
        const band = this.design.bands.find((b) => b.id === this.selectedBandId);
        const ctrl = band?.controls.find((c) => c.id === this.selectedControlId);
        if (ctrl) ctrls = [ctrl];
      }
      if (ctrls.length > 0) {
        e.preventDefault();
        let delta: number;
        if (e.shiftKey) {
          delta = 10;
        } else if (e.ctrlKey) {
          delta = 1;
        } else {
          delta = this.snapToGrid ? this.gridSize : 1;
        }
        for (const ctrl of ctrls) {
          switch (e.key) {
            case 'ArrowLeft':
              ctrl.x = Math.max(
                0,
                this.snapToGrid && !e.ctrlKey ? this.snapValue(ctrl.x - delta) : ctrl.x - delta,
              );
              break;
            case 'ArrowRight':
              ctrl.x =
                this.snapToGrid && !e.ctrlKey ? this.snapValue(ctrl.x + delta) : ctrl.x + delta;
              break;
            case 'ArrowUp':
              ctrl.y = Math.max(
                0,
                this.snapToGrid && !e.ctrlKey ? this.snapValue(ctrl.y - delta) : ctrl.y - delta,
              );
              break;
            case 'ArrowDown':
              ctrl.y =
                this.snapToGrid && !e.ctrlKey ? this.snapValue(ctrl.y + delta) : ctrl.y + delta;
              break;
          }
        }
        this.markDirty();
      }
    }
  }

  ngOnInit() {
    this.templateCategories = [...new Set(TEMPLATE_LIBRARY.map((t) => t.category))];
    this.route.params.subscribe((params) => {
      if (params['id']) {
        this.reportId = params['id'];
        this.loadReport();
      }
    });
  }

  ngOnDestroy() {
    if (this.snapshotTimer) clearTimeout(this.snapshotTimer);
  }

  private loadReport() {
    this.formsService.getReportTemplateById(this.reportId).subscribe((report) => {
      this.report = report;
      this.parseDesign(report.configurationJson);
      this.formsService.getFieldsByTemplate(report.formTemplateId).subscribe((fields) => {
        this.formFields = fields.sort((a, b) => a.displayOrder - b.displayOrder);
      });
    });
  }

  private parseDesign(json: string) {
    try {
      const parsed = JSON.parse(json);
      if (parsed?.bands) {
        this.design = { ...this.getDefaultDesign(), ...parsed };
      } else if (parsed?.columns) {
        // Legacy: convert config-editor format to band design
        this.design = this.convertLegacyConfig(parsed);
      }
    } catch {
      /* keep default */
    }
    // Capture initial state as base for undo
    this.undoStack = [];
    this.redoStack = [];
    this.takeSnapshot();
  }

  private convertLegacyConfig(config: any): ReportDesign {
    const design = this.getDefaultDesign();
    if (config.page) {
      design.pageSettings = config.page;
    }
    // Build pageHeader from config.header
    if (config.header) {
      const headerBand = design.bands.find((b) => b.type === 'pageHeader')!;
      if (config.header.title) {
        headerBand.controls.push({
          id: crypto.randomUUID(),
          type: 'label',
          x: 0,
          y: 10,
          width: 400,
          height: 30,
          text: config.header.title,
          style: { fontSize: 18, fontWeight: 'bold', textAlign: 'center' },
        });
      }
      if (config.header.showDate) {
        headerBand.controls.push({
          id: crypto.randomUUID(),
          type: 'currentDate',
          x: 400,
          y: 15,
          width: 150,
          height: 20,
          format: 'dd/MM/yyyy',
          style: { textAlign: 'right', fontSize: 10 },
        });
      }
    }
    // Build detail band from columns
    if (config.columns?.length) {
      const detailBand = design.bands.find((b) => b.type === 'detail')!;
      let x = 0;
      for (const col of config.columns.filter((c: any) => c.visible)) {
        detailBand.controls.push({
          id: crypto.randomUUID(),
          type: 'field',
          x,
          y: 2,
          width: col.width || 120,
          height: 20,
          dataField: col.fieldKey,
          style: { fontSize: 10 },
        });
        x += (col.width || 120) + 10;
      }
    }
    return design;
  }

  getDefaultDesign(): ReportDesign {
    return {
      bands: [
        { id: crypto.randomUUID(), type: 'pageHeader', height: 60, visible: true, controls: [] },
        { id: crypto.randomUUID(), type: 'detail', height: 30, visible: true, controls: [] },
        { id: crypto.randomUUID(), type: 'pageFooter', height: 30, visible: true, controls: [] },
      ],
      parameters: [],
      dataSources: [],
      pageSettings: {
        size: 'A4',
        orientation: 'landscape',
        margins: { top: 30, right: 30, bottom: 30, left: 30 },
      },
      styles: [],
      subReports: [],
      tabs: [],
    };
  }

  // ===== Band Management =====

  addBand(type: BandType) {
    const existing = this.design.bands.filter((b) => b.type === type);
    // Allow multiple group headers/footers
    if (type !== 'groupHeader' && type !== 'groupFooter' && existing.length > 0) return;
    const band: ReportBand = {
      id: crypto.randomUUID(),
      type,
      height: type === 'detail' ? 30 : 50,
      visible: true,
      controls: [],
    };
    // Insert in correct order
    const order = this.BAND_TYPES;
    const idx = order.indexOf(type);
    let insertIdx = this.design.bands.length;
    for (let i = 0; i < this.design.bands.length; i++) {
      if (order.indexOf(this.design.bands[i].type) > idx) {
        insertIdx = i;
        break;
      }
    }
    this.design.bands.splice(insertIdx, 0, band);
    this.selectedBandId = band.id;
    this.selectedControlId = null;
    this.markDirty();
  }

  removeBand(bandId: string) {
    const idx = this.design.bands.findIndex((b) => b.id === bandId);
    if (idx >= 0) {
      this.design.bands.splice(idx, 1);
      if (this.selectedBandId === bandId) {
        this.selectedBandId = null;
        this.selectedControlId = null;
      }
      this.markDirty();
    }
  }

  toggleBandVisibility(band: ReportBand) {
    band.visible = !band.visible;
    this.markDirty();
  }

  resizeBand(band: ReportBand, delta: number) {
    band.height = Math.max(20, band.height + delta);
    this.markDirty();
  }

  selectBand(bandId: string) {
    this.selectedBandId = bandId;
    this.selectedControlId = null;
    this.selectedControlIds.clear();
    this.sidePanel = 'properties';
    this.showContextMenu = false;
  }

  // ===== Multi-select helpers =====

  get selectedControls(): ReportControl[] {
    if (!this.selectedBandId || this.selectedControlIds.size === 0) return [];
    const band = this.design.bands.find((b) => b.id === this.selectedBandId);
    if (!band) return [];
    return band.controls.filter((c) => this.selectedControlIds.has(c.id));
  }

  get hasMultipleSelected(): boolean {
    return this.selectedControlIds.size > 1;
  }

  isControlSelected(controlId: string): boolean {
    return this.selectedControlIds.has(controlId);
  }

  selectAllInBand() {
    if (!this.selectedBandId) return;
    const band = this.design.bands.find((b) => b.id === this.selectedBandId);
    if (!band) return;
    this.selectedControlIds.clear();
    for (const ctrl of band.controls) {
      this.selectedControlIds.add(ctrl.id);
    }
    if (band.controls.length > 0) {
      this.selectedControlId = band.controls[0].id;
    }
  }

  clearSelection() {
    this.selectedControlId = null;
    this.selectedControlIds.clear();
    this.showContextMenu = false;
  }

  // ===== Rubber-band (lasso) selection =====

  onBandCanvasMouseDown(event: MouseEvent, bandId: string) {
    // Only start rubber-band if clicking on the band canvas itself (not on a control)
    if ((event.target as HTMLElement).closest('.control-wrapper')) return;
    if (event.button !== 0) return; // left click only

    const bandCanvas = (event.target as HTMLElement).closest('.band-canvas') as HTMLElement;
    if (!bandCanvas) return;

    const rect = bandCanvas.getBoundingClientRect();
    this.rubberBandStart = {
      x: event.clientX - rect.left,
      y: event.clientY - rect.top,
    };
    this.rubberBandBandId = bandId;
    this.isRubberBanding = false;

    // Clear selection unless Ctrl/Shift held
    if (!event.ctrlKey && !event.shiftKey && !event.metaKey) {
      this.selectedControlIds.clear();
      this.selectedControlId = null;
      this.selectedBandId = bandId;
    }
  }

  onBandCanvasMouseMove(event: MouseEvent, bandId: string) {
    if (!this.rubberBandStart || this.rubberBandBandId !== bandId) return;

    const bandCanvas = (event.target as HTMLElement).closest('.band-canvas') as HTMLElement;
    if (!bandCanvas) return;

    const rect = bandCanvas.getBoundingClientRect();
    const curX = event.clientX - rect.left;
    const curY = event.clientY - rect.top;

    const dx = Math.abs(curX - this.rubberBandStart.x);
    const dy = Math.abs(curY - this.rubberBandStart.y);

    if (dx > 3 || dy > 3) {
      this.isRubberBanding = true;
    }

    if (this.isRubberBanding) {
      this.rubberBandRect = {
        x: Math.min(this.rubberBandStart.x, curX),
        y: Math.min(this.rubberBandStart.y, curY),
        w: Math.abs(curX - this.rubberBandStart.x),
        h: Math.abs(curY - this.rubberBandStart.y),
      };

      // Select controls within the rubber-band rectangle
      const band = this.design.bands.find((b) => b.id === bandId);
      if (band) {
        if (!event.ctrlKey && !event.shiftKey) {
          this.selectedControlIds.clear();
        }
        for (const ctrl of band.controls) {
          if (
            this.rectsIntersect(
              this.rubberBandRect.x,
              this.rubberBandRect.y,
              this.rubberBandRect.w,
              this.rubberBandRect.h,
              ctrl.x,
              ctrl.y,
              ctrl.width,
              ctrl.height,
            )
          ) {
            this.selectedControlIds.add(ctrl.id);
          }
        }
        this.selectedBandId = bandId;
        if (this.selectedControlIds.size > 0) {
          this.selectedControlId = [...this.selectedControlIds][0];
        }
      }
    }
  }

  onBandCanvasMouseUp(event: MouseEvent) {
    this.isRubberBanding = false;
    this.rubberBandStart = null;
    this.rubberBandRect = { x: 0, y: 0, w: 0, h: 0 };
    this.rubberBandBandId = null;
  }

  private rectsIntersect(
    ax: number,
    ay: number,
    aw: number,
    ah: number,
    bx: number,
    by: number,
    bw: number,
    bh: number,
  ): boolean {
    return ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;
  }

  // ===== Alignment operations (DevExpress-style) =====

  alignLeft() {
    const ctrls = this.selectedControls;
    if (ctrls.length < 2) return;
    const minX = Math.min(...ctrls.map((c) => c.x));
    ctrls.forEach((c) => (c.x = minX));
    this.markDirty();
  }

  alignCenter() {
    const ctrls = this.selectedControls;
    if (ctrls.length < 2) return;
    const centers = ctrls.map((c) => c.x + c.width / 2);
    const avgCenter = centers.reduce((a, b) => a + b, 0) / centers.length;
    ctrls.forEach((c) => (c.x = Math.round(avgCenter - c.width / 2)));
    this.markDirty();
  }

  alignRight() {
    const ctrls = this.selectedControls;
    if (ctrls.length < 2) return;
    const maxRight = Math.max(...ctrls.map((c) => c.x + c.width));
    ctrls.forEach((c) => (c.x = maxRight - c.width));
    this.markDirty();
  }

  alignTop() {
    const ctrls = this.selectedControls;
    if (ctrls.length < 2) return;
    const minY = Math.min(...ctrls.map((c) => c.y));
    ctrls.forEach((c) => (c.y = minY));
    this.markDirty();
  }

  alignMiddle() {
    const ctrls = this.selectedControls;
    if (ctrls.length < 2) return;
    const middles = ctrls.map((c) => c.y + c.height / 2);
    const avgMiddle = middles.reduce((a, b) => a + b, 0) / middles.length;
    ctrls.forEach((c) => (c.y = Math.round(avgMiddle - c.height / 2)));
    this.markDirty();
  }

  alignBottom() {
    const ctrls = this.selectedControls;
    if (ctrls.length < 2) return;
    const maxBottom = Math.max(...ctrls.map((c) => c.y + c.height));
    ctrls.forEach((c) => (c.y = maxBottom - c.height));
    this.markDirty();
  }

  // Distribute evenly
  distributeHorizontal() {
    const ctrls = this.selectedControls;
    if (ctrls.length < 3) return;
    const sorted = [...ctrls].sort((a, b) => a.x - b.x);
    const first = sorted[0];
    const last = sorted[sorted.length - 1];
    const totalSpace = last.x + last.width - first.x;
    const totalControlWidth = sorted.reduce((s, c) => s + c.width, 0);
    const gap = (totalSpace - totalControlWidth) / (sorted.length - 1);
    let x = first.x;
    for (const ctrl of sorted) {
      ctrl.x = Math.round(x);
      x += ctrl.width + gap;
    }
    this.markDirty();
  }

  distributeVertical() {
    const ctrls = this.selectedControls;
    if (ctrls.length < 3) return;
    const sorted = [...ctrls].sort((a, b) => a.y - b.y);
    const first = sorted[0];
    const last = sorted[sorted.length - 1];
    const totalSpace = last.y + last.height - first.y;
    const totalControlHeight = sorted.reduce((s, c) => s + c.height, 0);
    const gap = (totalSpace - totalControlHeight) / (sorted.length - 1);
    let y = first.y;
    for (const ctrl of sorted) {
      ctrl.y = Math.round(y);
      y += ctrl.height + gap;
    }
    this.markDirty();
  }

  // Make same size
  makeSameWidth() {
    const ctrls = this.selectedControls;
    if (ctrls.length < 2) return;
    // Use the first selected control as reference
    const refWidth = ctrls[0].width;
    ctrls.forEach((c) => (c.width = refWidth));
    this.markDirty();
  }

  makeSameHeight() {
    const ctrls = this.selectedControls;
    if (ctrls.length < 2) return;
    const refHeight = ctrls[0].height;
    ctrls.forEach((c) => (c.height = refHeight));
    this.markDirty();
  }

  makeSameSize() {
    const ctrls = this.selectedControls;
    if (ctrls.length < 2) return;
    const refW = ctrls[0].width;
    const refH = ctrls[0].height;
    ctrls.forEach((c) => {
      c.width = refW;
      c.height = refH;
    });
    this.markDirty();
  }

  // Center in band (works for single or multi)
  centerInBandHorizontal() {
    const ctrls = this.selectedControls;
    if (ctrls.length === 0) return;
    const pageWidth = this.design.pageSettings.orientation === 'landscape' ? 1100 : 800;
    if (ctrls.length === 1) {
      ctrls[0].x = Math.round((pageWidth - ctrls[0].width) / 2);
    } else {
      const totalWidth = ctrls.reduce((s, c) => s + c.width, 0);
      const gap = 4;
      const totalWithGap = totalWidth + gap * (ctrls.length - 1);
      let x = Math.round((pageWidth - totalWithGap) / 2);
      const sorted = [...ctrls].sort((a, b) => a.x - b.x);
      for (const ctrl of sorted) {
        ctrl.x = x;
        x += ctrl.width + gap;
      }
    }
    this.markDirty();
  }

  centerInBandVertical() {
    const ctrls = this.selectedControls;
    if (ctrls.length === 0 || !this.selectedBandId) return;
    const band = this.design.bands.find((b) => b.id === this.selectedBandId);
    if (!band) return;
    if (ctrls.length === 1) {
      ctrls[0].y = Math.round((band.height - ctrls[0].height) / 2);
    } else {
      const totalHeight = ctrls.reduce((s, c) => s + c.height, 0);
      const gap = 2;
      const totalWithGap = totalHeight + gap * (ctrls.length - 1);
      let y = Math.round((band.height - totalWithGap) / 2);
      const sorted = [...ctrls].sort((a, b) => a.y - b.y);
      for (const ctrl of sorted) {
        ctrl.y = Math.max(0, y);
        y += ctrl.height + gap;
      }
    }
    this.markDirty();
  }

  // ===== Arrangement (z-order) =====

  bringToFront() {
    if (!this.selectedBandId || this.selectedControlIds.size === 0) return;
    const band = this.design.bands.find((b) => b.id === this.selectedBandId);
    if (!band) return;
    const selected = band.controls.filter((c) => this.selectedControlIds.has(c.id));
    const rest = band.controls.filter((c) => !this.selectedControlIds.has(c.id));
    band.controls = [...rest, ...selected];
    this.markDirty();
  }

  sendToBack() {
    if (!this.selectedBandId || this.selectedControlIds.size === 0) return;
    const band = this.design.bands.find((b) => b.id === this.selectedBandId);
    if (!band) return;
    const selected = band.controls.filter((c) => this.selectedControlIds.has(c.id));
    const rest = band.controls.filter((c) => !this.selectedControlIds.has(c.id));
    band.controls = [...selected, ...rest];
    this.markDirty();
  }

  bringForward() {
    if (!this.selectedBandId || this.selectedControlIds.size !== 1) return;
    const band = this.design.bands.find((b) => b.id === this.selectedBandId);
    if (!band) return;
    const idx = band.controls.findIndex((c) => this.selectedControlIds.has(c.id));
    if (idx >= 0 && idx < band.controls.length - 1) {
      [band.controls[idx], band.controls[idx + 1]] = [band.controls[idx + 1], band.controls[idx]];
      this.markDirty();
    }
  }

  sendBackward() {
    if (!this.selectedBandId || this.selectedControlIds.size !== 1) return;
    const band = this.design.bands.find((b) => b.id === this.selectedBandId);
    if (!band) return;
    const idx = band.controls.findIndex((c) => this.selectedControlIds.has(c.id));
    if (idx > 0) {
      [band.controls[idx], band.controls[idx - 1]] = [band.controls[idx - 1], band.controls[idx]];
      this.markDirty();
    }
  }

  // ===== Resize via drag handles =====

  onResizeStart(event: MouseEvent, handle: string, ctrl: ReportControl) {
    event.preventDefault();
    event.stopPropagation();
    this.isResizing = true;
    this.resizeHandle = handle;
    this.resizeControlId = ctrl.id;
    this.resizeStart = {
      x: event.clientX,
      y: event.clientY,
      ctrlX: ctrl.x,
      ctrlY: ctrl.y,
      ctrlW: ctrl.width,
      ctrlH: ctrl.height,
    };
  }

  @HostListener('window:mousemove', ['$event'])
  onWindowMouseMove(event: MouseEvent) {
    if (this.isResizing && this.resizeStart && this.resizeControlId) {
      const ctrl = this.findControlById(this.resizeControlId);
      if (!ctrl) return;

      const dx = event.clientX - this.resizeStart.x;
      const dy = event.clientY - this.resizeStart.y;
      const h = this.resizeHandle;

      if (h?.includes('e')) {
        ctrl.width = Math.max(10, this.snapValue(this.resizeStart.ctrlW + dx));
      }
      if (h?.includes('w')) {
        const newW = Math.max(10, this.resizeStart.ctrlW - dx);
        ctrl.x = this.snapValue(this.resizeStart.ctrlX + (this.resizeStart.ctrlW - newW));
        ctrl.width = newW;
      }
      if (h?.includes('s')) {
        ctrl.height = Math.max(6, this.snapValue(this.resizeStart.ctrlH + dy));
      }
      if (h?.includes('n')) {
        const newH = Math.max(6, this.resizeStart.ctrlH - dy);
        ctrl.y = this.snapValue(this.resizeStart.ctrlY + (this.resizeStart.ctrlH - newH));
        ctrl.height = newH;
      }
    }
  }

  @HostListener('window:mouseup', ['$event'])
  onWindowMouseUp(event: MouseEvent) {
    if (this.isResizing) {
      this.isResizing = false;
      this.resizeHandle = null;
      this.resizeControlId = null;
      this.resizeStart = null;
      this.markDirty();
    }
    // Also end rubber-band
    if (this.rubberBandStart) {
      this.onBandCanvasMouseUp(event);
    }
  }

  private findControlById(id: string): ReportControl | null {
    for (const band of this.design.bands) {
      const ctrl = band.controls.find((c) => c.id === id);
      if (ctrl) return ctrl;
    }
    return null;
  }

  // ===== Snap-to-grid =====

  snapValue(val: number): number {
    if (!this.snapToGrid) return val;
    return Math.round(val / this.gridSize) * this.gridSize;
  }

  toggleSnapToGrid() {
    this.snapToGrid = !this.snapToGrid;
  }

  toggleShowGrid() {
    this.showGrid = !this.showGrid;
  }

  // ===== Context menu =====

  onContextMenu(event: MouseEvent, controlId: string, bandId: string) {
    event.preventDefault();
    event.stopPropagation();
    if (!this.selectedControlIds.has(controlId)) {
      this.selectedControlIds.clear();
      this.selectedControlIds.add(controlId);
      this.selectedControlId = controlId;
      this.selectedBandId = bandId;
    }
    this.contextMenuPos = { x: event.clientX, y: event.clientY };
    this.showContextMenu = true;
  }

  onBandContextMenu(event: MouseEvent, bandId: string) {
    event.preventDefault();
    this.selectBand(bandId);
    this.contextMenuPos = { x: event.clientX, y: event.clientY };
    this.showContextMenu = true;
  }

  closeContextMenu() {
    this.showContextMenu = false;
  }

  // ===== Delete selected controls =====

  deleteSelected() {
    if (this.selectedControlIds.size === 0) return;
    for (const band of this.design.bands) {
      band.controls = band.controls.filter((c) => !this.selectedControlIds.has(c.id));
    }
    this.selectedControlIds.clear();
    this.selectedControlId = null;
    this.markDirty();
  }

  // ===== Copy/Paste =====
  clipboard: ReportControl[] = [];

  copySelected() {
    this.clipboard = this.selectedControls.map((c) => JSON.parse(JSON.stringify(c)));
  }

  cutSelected() {
    this.copySelected();
    this.deleteSelected();
  }

  pasteControls() {
    if (!this.clipboard.length || !this.selectedBandId) return;
    const band = this.design.bands.find((b) => b.id === this.selectedBandId);
    if (!band) return;
    this.selectedControlIds.clear();
    for (const orig of this.clipboard) {
      const copy: ReportControl = {
        ...JSON.parse(JSON.stringify(orig)),
        id: crypto.randomUUID(),
        x: orig.x + 15,
        y: orig.y + 15,
      };
      band.controls.push(copy);
      this.selectedControlIds.add(copy.id);
    }
    if (this.selectedControlIds.size > 0) {
      this.selectedControlId = [...this.selectedControlIds][0];
    }
    this.markDirty();
  }

  addControlToBand(bandId: string, toolboxItem: ToolboxItem) {
    const band = this.design.bands.find((b) => b.id === bandId);
    if (!band) return;
    const ctrl: ReportControl = {
      id: crypto.randomUUID(),
      type: toolboxItem.type,
      x: 10,
      y: 4,
      width: toolboxItem.defaultWidth,
      height: toolboxItem.defaultHeight,
      text: toolboxItem.type === 'label' ? 'Nhãn mới' : undefined,
      style: { fontSize: 10, fontWeight: 'normal', fontStyle: 'normal', textAlign: 'left' },
    };
    if (toolboxItem.type === 'field' && this.dataFieldOptions.length > 0) {
      ctrl.dataField = this.dataFieldOptions[0].key;
    }
    if (toolboxItem.type === 'shape') {
      ctrl.shapeType = 'rectangle';
      ctrl.style = {
        ...ctrl.style,
        borderColor: '#334155',
        borderWidth: 1,
        backgroundColor: '#f1f5f9',
      };
    }
    if (toolboxItem.type === 'barcode') {
      ctrl.barcodeType = 'qr';
      ctrl.barcodeValue = 'https://example.com';
    }
    if (toolboxItem.type === 'pageNumber') {
      ctrl.text = 'Trang {page}';
    }
    if (toolboxItem.type === 'currentDate') {
      ctrl.format = 'dd/MM/yyyy HH:mm';
    }
    if (toolboxItem.type === 'line') {
      ctrl.style = { ...ctrl.style, borderColor: '#94a3b8', borderWidth: 1 };
    }
    band.controls.push(ctrl);
    this.selectedControlId = ctrl.id;
    this.selectedBandId = bandId;
    this.sidePanel = 'properties';
    this.markDirty();
  }

  removeControl(controlId: string) {
    for (const band of this.design.bands) {
      const idx = band.controls.findIndex((c) => c.id === controlId);
      if (idx >= 0) {
        band.controls.splice(idx, 1);
        if (this.selectedControlId === controlId) this.selectedControlId = null;
        this.selectedControlIds.delete(controlId);
        this.markDirty();
        return;
      }
    }
  }

  duplicateControl(controlId: string) {
    for (const band of this.design.bands) {
      const ctrl = band.controls.find((c) => c.id === controlId);
      if (ctrl) {
        const copy: ReportControl = {
          ...JSON.parse(JSON.stringify(ctrl)),
          id: crypto.randomUUID(),
          x: ctrl.x + 10,
          y: ctrl.y + 10,
        };
        band.controls.push(copy);
        this.selectedControlId = copy.id;
        this.selectedControlIds.clear();
        this.selectedControlIds.add(copy.id);
        this.markDirty();
        return;
      }
    }
  }

  selectControl(controlId: string, bandId: string, event?: MouseEvent) {
    if (event && (event.ctrlKey || event.metaKey)) {
      // Toggle selection (Ctrl+Click)
      if (this.selectedBandId !== bandId) {
        // Switching bands → reset
        this.selectedControlIds.clear();
      }
      this.selectedBandId = bandId;
      if (this.selectedControlIds.has(controlId)) {
        this.selectedControlIds.delete(controlId);
        if (this.selectedControlIds.size > 0) {
          this.selectedControlId = [...this.selectedControlIds][this.selectedControlIds.size - 1];
        } else {
          this.selectedControlId = null;
        }
      } else {
        this.selectedControlIds.add(controlId);
        this.selectedControlId = controlId;
      }
    } else if (event && event.shiftKey) {
      // Add to selection (Shift+Click)
      if (this.selectedBandId !== bandId) {
        this.selectedControlIds.clear();
      }
      this.selectedBandId = bandId;
      this.selectedControlIds.add(controlId);
      this.selectedControlId = controlId;
    } else {
      // Normal click → single select
      this.selectedControlIds.clear();
      this.selectedControlIds.add(controlId);
      this.selectedControlId = controlId;
      this.selectedBandId = bandId;
    }
    this.sidePanel = 'properties';
    this.showContextMenu = false;
  }

  onControlDragStart(ctrl: ReportControl) {
    this.draggedControlRef = ctrl;
  }

  onControlDragMove(ctrl: ReportControl, event: CdkDragMove, bandId: string) {
    // Calculate current dragged position
    const dx = event.distance.x;
    const dy = event.distance.y;
    const dragX = ctrl.x + dx;
    const dragY = ctrl.y + dy;
    const dragRight = dragX + ctrl.width;
    const dragBottom = dragY + ctrl.height;
    const dragCenterX = dragX + ctrl.width / 2;
    const dragCenterY = dragY + ctrl.height / 2;

    const band = this.design.bands.find(b => b.id === bandId);
    if (!band) return;

    const guides: { orientation: 'h' | 'v'; pos: number; bandId: string }[] = [];
    const threshold = this.GUIDE_SNAP_THRESHOLD;

    for (const other of band.controls) {
      if (other.id === ctrl.id) continue;
      if (this.selectedControlIds.has(other.id)) continue; // skip co-selected

      const oLeft = other.x;
      const oRight = other.x + other.width;
      const oTop = other.y;
      const oBottom = other.y + other.height;
      const oCenterX = other.x + other.width / 2;
      const oCenterY = other.y + other.height / 2;

      // Vertical guides (x-axis alignment)
      // Left ↔ Left
      if (Math.abs(dragX - oLeft) <= threshold) guides.push({ orientation: 'v', pos: oLeft, bandId });
      // Right ↔ Right
      if (Math.abs(dragRight - oRight) <= threshold) guides.push({ orientation: 'v', pos: oRight, bandId });
      // Left ↔ Right
      if (Math.abs(dragX - oRight) <= threshold) guides.push({ orientation: 'v', pos: oRight, bandId });
      // Right ↔ Left
      if (Math.abs(dragRight - oLeft) <= threshold) guides.push({ orientation: 'v', pos: oLeft, bandId });
      // Center ↔ Center
      if (Math.abs(dragCenterX - oCenterX) <= threshold) guides.push({ orientation: 'v', pos: oCenterX, bandId });

      // Horizontal guides (y-axis alignment)
      // Top ↔ Top
      if (Math.abs(dragY - oTop) <= threshold) guides.push({ orientation: 'h', pos: oTop, bandId });
      // Bottom ↔ Bottom
      if (Math.abs(dragBottom - oBottom) <= threshold) guides.push({ orientation: 'h', pos: oBottom, bandId });
      // Top ↔ Bottom
      if (Math.abs(dragY - oBottom) <= threshold) guides.push({ orientation: 'h', pos: oBottom, bandId });
      // Bottom ↔ Top
      if (Math.abs(dragBottom - oTop) <= threshold) guides.push({ orientation: 'h', pos: oTop, bandId });
      // Center ↔ Center
      if (Math.abs(dragCenterY - oCenterY) <= threshold) guides.push({ orientation: 'h', pos: oCenterY, bandId });
    }

    // Deduplicate
    const seen = new Set<string>();
    this.guideLines = guides.filter(g => {
      const key = `${g.orientation}-${Math.round(g.pos)}`;
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    });
  }

  onControlDragEnd(ctrl: ReportControl, event: any) {
    // Update position from CDK drag, with snap-to-grid
    if (event?.distance) {
      const dx = event.distance.x;
      const dy = event.distance.y;

      // If this control is part of a multi-selection, move all selected
      if (this.selectedControlIds.has(ctrl.id) && this.selectedControlIds.size > 1) {
        for (const c of this.selectedControls) {
          c.x = Math.max(0, this.snapValue(c.x + dx));
          c.y = Math.max(0, this.snapValue(c.y + dy));
        }
      } else {
        ctrl.x = Math.max(0, this.snapValue(ctrl.x + dx));
        ctrl.y = Math.max(0, this.snapValue(ctrl.y + dy));
      }
      this.markDirty();
    }
    // Clear alignment guides
    this.guideLines = [];
    this.draggedControlRef = null;
    // Reset CDK drag transform so position matches the updated left/top
    if (event?.source) {
      event.source.reset();
    }
  }

  // ===== Toolbox drag to band =====

  onToolboxDragStart(item: ToolboxItem) {
    this.isDraggingControl = true;
    this.draggedToolboxItem = item;
  }

  onToolboxDragEnd() {
    this.isDraggingControl = false;
    this.draggedToolboxItem = null;
  }

  onBandDrop(event: DragEvent, bandId: string) {
    event.preventDefault();
    const toolboxType = event.dataTransfer?.getData('toolboxType');
    if (toolboxType) {
      const item = TOOLBOX_ITEMS.find((t) => t.type === toolboxType);
      if (item) {
        const band = this.design.bands.find((b) => b.id === bandId);
        if (band) {
          const rect = (event.target as HTMLElement)
            .closest('.band-canvas')
            ?.getBoundingClientRect();
          const x = rect ? event.clientX - rect.left : 10;
          const y = rect ? event.clientY - rect.top : 4;
          const ctrl: ReportControl = {
            id: crypto.randomUUID(),
            type: item.type,
            x: Math.max(0, x),
            y: Math.max(0, y),
            width: item.defaultWidth,
            height: item.defaultHeight,
            text: item.type === 'label' ? 'Nhãn mới' : undefined,
            style: { fontSize: 10, fontWeight: 'normal', fontStyle: 'normal', textAlign: 'left' },
          };
          if (item.type === 'field' && this.dataFieldOptions.length > 0)
            ctrl.dataField = this.dataFieldOptions[0].key;
          if (item.type === 'pageNumber') ctrl.text = 'Trang {page}';
          if (item.type === 'currentDate') ctrl.format = 'dd/MM/yyyy HH:mm';
          band.controls.push(ctrl);
          this.selectedControlId = ctrl.id;
          this.selectedBandId = bandId;
          this.sidePanel = 'properties';
          this.markDirty();
        }
      }
    }
  }

  onBandDragOver(event: DragEvent) {
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'copy';
  }

  // ===== Parameters (Phase 3) =====

  addParameter() {
    this.design.parameters.push({
      name: 'param_' + (this.design.parameters.length + 1),
      label: 'Tham số ' + (this.design.parameters.length + 1),
      type: 'text',
      defaultValue: '',
      required: false,
    });
    this.markDirty();
  }

  removeParameter(index: number) {
    this.design.parameters.splice(index, 1);
    this.markDirty();
  }

  initParameterValues() {
    this.parameterValues = {};
    for (const p of this.design.parameters) {
      this.parameterValues[p.name] = p.defaultValue ?? '';
    }
    if (this.design.parameters.length > 0) {
      this.showParametersDialog = true;
    }
  }

  // ===== Tabs (Phase 3: multi-page) =====

  addTab() {
    if (!this.design.tabs) this.design.tabs = [];
    const tabDesign = this.getDefaultDesign();
    this.design.tabs.push({
      id: crypto.randomUUID(),
      name: 'Tab ' + (this.design.tabs.length + 1),
      designJson: JSON.stringify(tabDesign),
    });
    this.markDirty();
  }

  removeTab(index: number) {
    this.design.tabs?.splice(index, 1);
    this.activeTabId = null;
    this.markDirty();
  }

  selectTab(tabId: string | null) {
    this.activeTabId = tabId;
    if (tabId) {
      this.sidePanel = 'tabs';
    }
  }

  // ===== Cross-tab (Phase 3) =====

  initCrossTab() {
    if (!this.design.crossTab) {
      this.design.crossTab = {
        rowFields: [],
        columnFields: [],
        dataField: '',
        aggregation: 'count',
        showRowTotals: true,
        showColumnTotals: true,
        showGrandTotal: true,
      };
    }
    this.markDirty();
  }

  removeCrossTab() {
    this.design.crossTab = undefined;
    this.crossTabData = null;
    this.markDirty();
  }

  computeCrossTab(data: Record<string, any>[]) {
    const ct = this.design.crossTab;
    if (!ct || !ct.rowFields.length || !ct.columnFields.length) {
      this.crossTabData = null;
      return;
    }
    const rowField = ct.rowFields[0];
    const colField = ct.columnFields[0];
    const uniqueCols = [...new Set(data.map((r) => (r[colField] ?? '-').toString()))].sort();
    const uniqueRows = [...new Set(data.map((r) => (r[rowField] ?? '-').toString()))].sort();

    const rows: { label: string; values: (string | number)[] }[] = [];
    const totals: (string | number)[] = new Array(uniqueCols.length).fill(0);

    for (const rowVal of uniqueRows) {
      const values: (string | number)[] = [];
      for (let ci = 0; ci < uniqueCols.length; ci++) {
        const colVal = uniqueCols[ci];
        const matching = data.filter(
          (r) =>
            (r[rowField] ?? '-').toString() === rowVal &&
            (r[colField] ?? '-').toString() === colVal,
        );
        let agg: number;
        switch (ct.aggregation) {
          case 'count':
            agg = matching.length;
            break;
          case 'sum':
            agg = matching.reduce(
              (s, r) => s + (parseFloat(r[ct.dataField]?.toString() ?? '0') || 0),
              0,
            );
            break;
          case 'avg': {
            const nums = matching
              .map((r) => parseFloat(r[ct.dataField]?.toString() ?? '0'))
              .filter((n) => !isNaN(n));
            agg = nums.length ? nums.reduce((a, n) => a + n, 0) / nums.length : 0;
            break;
          }
          case 'min': {
            const nums = matching
              .map((r) => parseFloat(r[ct.dataField]?.toString() ?? '0'))
              .filter((n) => !isNaN(n));
            agg = nums.length ? Math.min(...nums) : 0;
            break;
          }
          case 'max': {
            const nums = matching
              .map((r) => parseFloat(r[ct.dataField]?.toString() ?? '0'))
              .filter((n) => !isNaN(n));
            agg = nums.length ? Math.max(...nums) : 0;
            break;
          }
          default:
            agg = matching.length;
        }
        values.push(agg);
        totals[ci] = (totals[ci] as number) + agg;
      }
      rows.push({ label: rowVal, values });
    }

    this.crossTabData = { headers: uniqueCols, rows, totals };
  }

  // ===== Sub-reports (Phase 3) =====

  addSubReport(bandId: string) {
    if (!this.design.subReports) this.design.subReports = [];
    this.design.subReports.push({
      id: crypto.randomUUID(),
      name: 'Sub-report ' + (this.design.subReports.length + 1),
      reportDesignJson: JSON.stringify(this.getDefaultDesign()),
      bandId,
      parameterBindings: [],
    });
    this.markDirty();
  }

  removeSubReport(index: number) {
    this.design.subReports?.splice(index, 1);
    this.markDirty();
  }

  // ===== Template Library (Phase 3) =====

  applyTemplate(template: ReportTemplateLibraryItem) {
    if (this.isDirty && !confirm('Áp dụng mẫu sẽ ghi đè thiết kế hiện tại. Tiếp tục?')) return;
    try {
      this.design = { ...this.getDefaultDesign(), ...JSON.parse(template.designJson) };
      this.selectedBandId = null;
      this.selectedControlId = null;
      this.markDirty();
    } catch {
      alert('Mẫu không hợp lệ.');
    }
  }

  // ===== Expression Editor =====

  openExpressionEditor(currentExpr: string) {
    this.expressionValue = currentExpr;
    this.showExpressionEditor = true;
  }

  applyExpression() {
    if (this.selectedControl) {
      this.selectedControl.expression = this.expressionValue;
      this.markDirty();
    }
    this.showExpressionEditor = false;
  }

  insertExpressionSnippet(snippet: string) {
    this.expressionValue += snippet;
  }

  // ===== Preview Rendering =====

  toggleMode() {
    if (this.mode === 'design') {
      this.mode = 'preview';
      this.loadPreviewData();
    } else {
      this.mode = 'design';
    }
  }

  loadPreviewData() {
    if (!this.reportId || this.isLoadingPreview) return;
    this.isLoadingPreview = true;
    // Save first then generate
    const designJson = JSON.stringify(this.design);
    if (this.report) {
      this.formsService
        .updateReportTemplate(this.reportId, {
          name: this.report.name,
          description: this.report.description,
          reportType: this.report.reportType,
          configurationJson: designJson,
        })
        .subscribe({
          next: () => {
            this.isDirty = false;
            this.formsService.generateReport(this.reportId).subscribe({
              next: (data) => {
                this.previewData = data;
                this.isLoadingPreview = false;
                if (this.design.crossTab) {
                  this.computeCrossTab(data.data);
                }
              },
              error: () => {
                this.isLoadingPreview = false;
              },
            });
          },
          error: () => {
            this.isLoadingPreview = false;
          },
        });
    }
  }

  renderControlPreview(
    ctrl: ReportControl,
    row: Record<string, any>,
    allRows: Record<string, any>[],
  ): string {
    switch (ctrl.type) {
      case 'label':
        return ctrl.text ?? '';
      case 'field': {
        const raw = row[ctrl.dataField ?? ''] ?? '-';
        return this.formatFieldValue(raw, ctrl.format);
      }
      case 'expression':
        return evaluateExpression(ctrl.expression ?? '', row, allRows);
      case 'pageNumber':
        return ctrl.text?.replace('{page}', '1') ?? 'Trang 1';
      case 'totalPages':
        return '1';
      case 'currentDate': {
        const fmt = ctrl.format || 'dd/MM/yyyy';
        return formatDateStr(new Date().toISOString(), fmt);
      }
      case 'checkbox':
        return ctrl.dataField ? (row[ctrl.dataField] ? '☑' : '☐') : '☐';
      case 'barcode':
        return `[${ctrl.barcodeType?.toUpperCase() ?? 'QR'}]`;
      case 'image':
        return ctrl.imageUrl ? '🖼️' : '[Image]';
      case 'richText':
        return ctrl.text ?? '';
      default:
        return '';
    }
  }

  private formatFieldValue(raw: any, format?: string): string {
    if (raw === null || raw === undefined || raw === '-') return '-';
    const str = raw.toString();
    if (!format) return str;
    // If it looks like a date string
    if (typeof raw === 'string' && /\d{4}-\d{2}-\d{2}/.test(raw)) {
      return formatDateStr(raw, format);
    }
    return str;
  }

  toggleSidePanel() {
    this.sidePanelCollapsed = !this.sidePanelCollapsed;
  }

  getControlStyle(ctrl: ReportControl): Record<string, string> {
    const s: Record<string, string> = {
      position: 'absolute',
      left: ctrl.x + 'px',
      top: ctrl.y + 'px',
      width: ctrl.width + 'px',
      height: ctrl.height + 'px',
    };
    const st = ctrl.style;
    if (st) {
      if (st.fontSize) s['font-size'] = st.fontSize + 'px';
      if (st.fontWeight) s['font-weight'] = st.fontWeight;
      if (st.fontStyle) s['font-style'] = st.fontStyle;
      if (st.textAlign) s['text-align'] = st.textAlign;
      if (st.color) s['color'] = st.color;
      if (st.backgroundColor) s['background-color'] = st.backgroundColor;
      if (st.borderColor) s['border-color'] = st.borderColor;
      if (st.borderWidth) s['border-width'] = st.borderWidth + 'px';
      if (st.borderStyle) s['border-style'] = st.borderStyle;
      if (st.padding) s['padding'] = st.padding + 'px';
      if (st.fontFamily) s['font-family'] = st.fontFamily;
    }
    return s;
  }

  getBandLabel(type: BandType): string {
    return BAND_LABELS[type] || type;
  }

  getAvailableBandTypes(): BandType[] {
    const existing = new Set(this.design.bands.map((b) => b.type));
    return this.BAND_TYPES.filter(
      (t) => t === 'groupHeader' || t === 'groupFooter' || !existing.has(t),
    );
  }

  getDataFieldLabel(key: string): string {
    return this.dataFieldOptions.find((f) => f.key === key)?.label ?? key;
  }

  getControlIcon(type: string): string {
    const item = TOOLBOX_ITEMS.find((t) => t.type === type);
    return item?.icon ?? '📦';
  }

  // ===== Report Explorer helpers =====

  toggleExplorerBand(bandId: string) {
    if (this.explorerCollapsedBands.has(bandId)) {
      this.explorerCollapsedBands.delete(bandId);
    } else {
      this.explorerCollapsedBands.add(bandId);
    }
  }

  isExplorerBandExpanded(bandId: string): boolean {
    return !this.explorerCollapsedBands.has(bandId);
  }

  getControlDisplayName(ctrl: ReportControl): string {
    if (this.controlNames[ctrl.id]) return this.controlNames[ctrl.id];
    // Auto-generate a display name
    switch (ctrl.type) {
      case 'label':
        return ctrl.text?.substring(0, 30) || 'Label';
      case 'field':
        return `[${this.getDataFieldLabel(ctrl.dataField ?? '')}]`;
      case 'expression':
        return `fx: ${ctrl.expression?.substring(0, 25) || 'Expression'}`;
      case 'image':
        return 'Image';
      case 'shape':
        return `Shape (${ctrl.shapeType || 'rect'})`;
      case 'line':
        return 'Line';
      case 'barcode':
        return `Barcode (${ctrl.barcodeType || 'qr'})`;
      case 'chart':
        return 'Chart';
      case 'table':
        return 'Table';
      case 'checkbox':
        return 'Checkbox';
      case 'pageNumber':
        return 'Page Number';
      case 'totalPages':
        return 'Total Pages';
      case 'currentDate':
        return 'Current Date';
      case 'richText':
        return ctrl.text?.substring(0, 30) || 'Rich Text';
      case 'signatureZone':
        return ctrl.text || 'Signature Zone';
      default:
        return ctrl.type;
    }
  }

  startRenameControl(ctrl: ReportControl, event: MouseEvent) {
    event.stopPropagation();
    this.explorerEditingId = ctrl.id;
    this.explorerEditingName = this.controlNames[ctrl.id] || this.getControlDisplayName(ctrl);
  }

  finishRenameControl(ctrl: ReportControl) {
    if (this.explorerEditingName.trim()) {
      this.controlNames[ctrl.id] = this.explorerEditingName.trim();
    }
    this.explorerEditingId = null;
    this.explorerEditingName = '';
  }

  cancelRenameControl() {
    this.explorerEditingId = null;
    this.explorerEditingName = '';
  }

  moveControlUp(bandId: string, controlId: string) {
    const band = this.design.bands.find((b) => b.id === bandId);
    if (!band) return;
    const idx = band.controls.findIndex((c) => c.id === controlId);
    if (idx > 0) {
      [band.controls[idx - 1], band.controls[idx]] = [band.controls[idx], band.controls[idx - 1]];
      this.markDirty();
    }
  }

  moveControlDown(bandId: string, controlId: string) {
    const band = this.design.bands.find((b) => b.id === bandId);
    if (!band) return;
    const idx = band.controls.findIndex((c) => c.id === controlId);
    if (idx >= 0 && idx < band.controls.length - 1) {
      [band.controls[idx], band.controls[idx + 1]] = [band.controls[idx + 1], band.controls[idx]];
      this.markDirty();
    }
  }

  moveBandUp(bandId: string) {
    const idx = this.design.bands.findIndex((b) => b.id === bandId);
    if (idx > 0) {
      [this.design.bands[idx - 1], this.design.bands[idx]] = [
        this.design.bands[idx],
        this.design.bands[idx - 1],
      ];
      this.markDirty();
    }
  }

  moveBandDown(bandId: string) {
    const idx = this.design.bands.findIndex((b) => b.id === bandId);
    if (idx >= 0 && idx < this.design.bands.length - 1) {
      [this.design.bands[idx], this.design.bands[idx + 1]] = [
        this.design.bands[idx + 1],
        this.design.bands[idx],
      ];
      this.markDirty();
    }
  }

  getControlSizeInfo(ctrl: ReportControl): string {
    return `${ctrl.x},${ctrl.y} ${ctrl.width}×${ctrl.height}`;
  }

  getBandIconClass(type: BandType): string {
    switch (type) {
      case 'reportHeader':
      case 'pageHeader':
        return 'explorer-band-header';
      case 'detail':
        return 'explorer-band-detail';
      case 'reportFooter':
      case 'pageFooter':
        return 'explorer-band-footer';
      case 'groupHeader':
        return 'explorer-band-group-h';
      case 'groupFooter':
        return 'explorer-band-group-f';
      default:
        return '';
    }
  }

  // ===== Undo / Redo =====

  private takeSnapshot() {
    const snap = JSON.stringify(this.design);
    // Avoid duplicate consecutive snapshots
    if (this.undoStack.length && this.undoStack[this.undoStack.length - 1] === snap) return;
    this.undoStack.push(snap);
    if (this.undoStack.length > this.MAX_UNDO) this.undoStack.shift();
    this.redoStack.length = 0; // clear redo on new action
  }

  private scheduleSnapshot() {
    // Debounce rapid changes (e.g. typing, dragging) into one snapshot
    if (this.snapshotTimer) clearTimeout(this.snapshotTimer);
    this.snapshotTimer = setTimeout(() => this.takeSnapshot(), 300);
  }

  undo() {
    if (!this.undoStack.length) return;
    // Save current state to redo
    this.redoStack.push(JSON.stringify(this.design));
    const prev = this.undoStack.pop()!;
    this.isUndoRedoAction = true;
    this.design = JSON.parse(prev);
    this.selectedControlId = null;
    this.selectedBandId = null;
    this.isUndoRedoAction = false;
  }

  redo() {
    if (!this.redoStack.length) return;
    // Save current state to undo
    this.undoStack.push(JSON.stringify(this.design));
    const next = this.redoStack.pop()!;
    this.isUndoRedoAction = true;
    this.design = JSON.parse(next);
    this.selectedControlId = null;
    this.selectedBandId = null;
    this.isUndoRedoAction = false;
  }

  get canUndo(): boolean {
    return this.undoStack.length > 0;
  }

  get canRedo(): boolean {
    return this.redoStack.length > 0;
  }

  // ===== Persistence =====

  markDirty() {
    this.isDirty = true;
    if (!this.isUndoRedoAction) {
      this.scheduleSnapshot();
    }
  }

  save() {
    if (!this.report || this.isSaving) return;
    this.isSaving = true;
    this.formsService
      .updateReportTemplate(this.reportId, {
        name: this.report.name,
        description: this.report.description,
        reportType: this.report.reportType,
        configurationJson: JSON.stringify(this.design),
      })
      .subscribe({
        next: () => {
          this.isSaving = false;
          this.isDirty = false;
        },
        error: () => {
          this.isSaving = false;
          alert('Lỗi khi lưu thiết kế.');
        },
      });
  }

  saveAndPreview() {
    if (!this.report) return;
    this.isSaving = true;
    this.formsService
      .updateReportTemplate(this.reportId, {
        name: this.report.name,
        description: this.report.description,
        reportType: this.report.reportType,
        configurationJson: JSON.stringify(this.design),
      })
      .subscribe({
        next: () => {
          this.isSaving = false;
          this.isDirty = false;
          this.router.navigate(['/forms/reports', this.reportId]);
        },
        error: () => {
          this.isSaving = false;
          alert('Lỗi khi lưu.');
        },
      });
  }

  exportPdf() {
    if (!this.reportId) return;
    this.formsService.exportReportPdf(this.reportId).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `${this.report?.name ?? 'Report'}.pdf`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => alert('Lỗi khi xuất PDF.'),
    });
  }

  goBack() {
    if (this.isDirty && !confirm('Bạn có thay đổi chưa lưu. Thoát không?')) return;
    this.router.navigate(['/forms/reports', this.reportId]);
  }

  /** Sum a number array — used in cross-tab row totals template */
  sumArray(values: (string | number)[]): number {
    return values.reduce((a: number, b) => a + (+b || 0), 0);
  }
}
