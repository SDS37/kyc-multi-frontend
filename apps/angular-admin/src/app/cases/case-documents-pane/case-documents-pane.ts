import { DatePipe } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { CASES_REVIEW_MESSAGES } from '../cases.messages';
import { CaseDocument } from '../cases.models';

/**
 * Presentational: case document list + download buttons (KYC-065).
 * Emits `download` — parent owns CasesService + blob save.
 */
@Component({
  selector: 'app-case-documents-pane',
  imports: [DatePipe, MatButtonModule],
  templateUrl: './case-documents-pane.html',
  styleUrl: './case-documents-pane.css',
})
export class CaseDocumentsPane {
  readonly documents = input.required<readonly CaseDocument[]>();
  readonly downloadError = input<string | null>(null);
  readonly downloadingId = input<string | null>(null);

  readonly download = output<CaseDocument>();

  protected readonly copy: typeof CASES_REVIEW_MESSAGES = CASES_REVIEW_MESSAGES;

  protected onDownload(doc: CaseDocument): void {
    this.download.emit(doc);
  }
}
