import { Component, input } from '@angular/core';
import { UI_MESSAGES } from '../../shared/ui.messages';
import { CASES_REVIEW_MESSAGES } from '../cases.messages';
import { CaseFormField } from '../cases.models';

/**
 * Presentational: application form field list (KYC-065).
 * No HTTP / CasesService — data via `input()`.
 */
@Component({
  selector: 'app-case-form-data-pane',
  templateUrl: './case-form-data-pane.html',
  styleUrl: './case-form-data-pane.css',
})
export class CaseFormDataPane {
  readonly formFields = input.required<readonly CaseFormField[]>();

  protected readonly copy: typeof CASES_REVIEW_MESSAGES = CASES_REVIEW_MESSAGES;
  protected readonly sharedCopy: typeof UI_MESSAGES = UI_MESSAGES;
}
