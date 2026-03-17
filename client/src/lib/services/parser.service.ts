import { Injectable } from '@angular/core';
import { UploadService } from './upload.service';

@Injectable({ providedIn: 'root' })
export class ParserService {
  constructor(private readonly uploadService: UploadService) {}

  async uploadPdf(file: File): Promise<{ ok: boolean }> {
    await this.uploadService.uploadPdf(file);
    return { ok: true };
  }
}
