import {
  Component,
  ElementRef,
  ViewChild,
  AfterViewInit,
  Output,
  EventEmitter,
  Input,
  OnDestroy,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-signature-pad',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './signature-pad.component.html',
  styleUrls: ['./signature-pad.component.scss'],
})
export class SignaturePadComponent implements AfterViewInit, OnDestroy {
  @ViewChild('signatureCanvas') canvasRef!: ElementRef<HTMLCanvasElement>;

  @Input() width = 500;
  @Input() height = 200;
  @Input() penColor = '#1e293b';
  @Input() penWidth = 2.5;
  @Input() existingImage: string | null = null;

  @Output() signatureChanged = new EventEmitter<string | null>();

  isEmpty = signal(true);
  private ctx!: CanvasRenderingContext2D;
  private isDrawing = false;
  private lastX = 0;
  private lastY = 0;
  private resizeObserver?: ResizeObserver;

  ngAfterViewInit() {
    this.initCanvas();

    // If there's an existing image, draw it
    if (this.existingImage) {
      this.loadImage(this.existingImage);
    }
  }

  ngOnDestroy() {
    this.resizeObserver?.disconnect();
  }

  private initCanvas() {
    const canvas = this.canvasRef.nativeElement;
    this.ctx = canvas.getContext('2d')!;

    // Scale for high-DPI displays
    const dpr = window.devicePixelRatio || 1;
    canvas.width = this.width * dpr;
    canvas.height = this.height * dpr;
    canvas.style.width = `${this.width}px`;
    canvas.style.height = `${this.height}px`;
    this.ctx.scale(dpr, dpr);

    this.ctx.strokeStyle = this.penColor;
    this.ctx.lineWidth = this.penWidth;
    this.ctx.lineCap = 'round';
    this.ctx.lineJoin = 'round';

    // Mouse events
    canvas.addEventListener('mousedown', this.startDrawing.bind(this));
    canvas.addEventListener('mousemove', this.draw.bind(this));
    canvas.addEventListener('mouseup', this.stopDrawing.bind(this));
    canvas.addEventListener('mouseout', this.stopDrawing.bind(this));

    // Touch events
    canvas.addEventListener('touchstart', this.handleTouchStart.bind(this), { passive: false });
    canvas.addEventListener('touchmove', this.handleTouchMove.bind(this), { passive: false });
    canvas.addEventListener('touchend', this.stopDrawing.bind(this));
  }

  private startDrawing(e: MouseEvent) {
    this.isDrawing = true;
    const rect = this.canvasRef.nativeElement.getBoundingClientRect();
    this.lastX = e.clientX - rect.left;
    this.lastY = e.clientY - rect.top;
  }

  private draw(e: MouseEvent) {
    if (!this.isDrawing) return;

    const rect = this.canvasRef.nativeElement.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;

    this.ctx.beginPath();
    this.ctx.moveTo(this.lastX, this.lastY);
    this.ctx.lineTo(x, y);
    this.ctx.stroke();

    this.lastX = x;
    this.lastY = y;
    this.isEmpty.set(false);
  }

  private stopDrawing() {
    if (this.isDrawing) {
      this.isDrawing = false;
      this.emitSignature();
    }
  }

  private handleTouchStart(e: TouchEvent) {
    e.preventDefault();
    const touch = e.touches[0];
    const rect = this.canvasRef.nativeElement.getBoundingClientRect();
    this.isDrawing = true;
    this.lastX = touch.clientX - rect.left;
    this.lastY = touch.clientY - rect.top;
  }

  private handleTouchMove(e: TouchEvent) {
    e.preventDefault();
    if (!this.isDrawing) return;

    const touch = e.touches[0];
    const rect = this.canvasRef.nativeElement.getBoundingClientRect();
    const x = touch.clientX - rect.left;
    const y = touch.clientY - rect.top;

    this.ctx.beginPath();
    this.ctx.moveTo(this.lastX, this.lastY);
    this.ctx.lineTo(x, y);
    this.ctx.stroke();

    this.lastX = x;
    this.lastY = y;
    this.isEmpty.set(false);
  }

  clear() {
    const canvas = this.canvasRef.nativeElement;
    const dpr = window.devicePixelRatio || 1;
    this.ctx.clearRect(0, 0, canvas.width / dpr, canvas.height / dpr);
    this.isEmpty.set(true);
    this.signatureChanged.emit(null);
  }

  getSignatureBase64(): string | null {
    if (this.isEmpty()) return null;
    const canvas = this.canvasRef.nativeElement;

    // Create a temp canvas with white bg for the exported image
    const tempCanvas = document.createElement('canvas');
    tempCanvas.width = canvas.width;
    tempCanvas.height = canvas.height;
    const tempCtx = tempCanvas.getContext('2d')!;

    // Transparent background for PNG
    tempCtx.drawImage(canvas, 0, 0);

    // Get data URL and strip prefix to return just base64
    const dataUrl = tempCanvas.toDataURL('image/png');
    return dataUrl.replace('data:image/png;base64,', '');
  }

  getSignatureDataUrl(): string | null {
    if (this.isEmpty()) return null;
    return this.canvasRef.nativeElement.toDataURL('image/png');
  }

  private loadImage(base64: string) {
    const img = new Image();
    img.onload = () => {
      const dpr = window.devicePixelRatio || 1;
      this.ctx.drawImage(img, 0, 0, this.width, this.height);
      this.isEmpty.set(false);
    };
    // Handle both raw base64 and data URL
    if (base64.startsWith('data:')) {
      img.src = base64;
    } else {
      img.src = `data:image/png;base64,${base64}`;
    }
  }

  private emitSignature() {
    this.signatureChanged.emit(this.getSignatureBase64());
  }
}
