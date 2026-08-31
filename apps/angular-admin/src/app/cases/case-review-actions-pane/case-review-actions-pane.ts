import { Component, input, model, output } from '@angular/core';
import { FormField, FieldTree } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import {
  CASES_REVIEW_MESSAGES,
  rejectCommentHint,
  rejectCommentMaxLengthMessage,
} from '../cases.messages';
import { CaseReviewActions, REVIEW_COMMENT_MAX_LENGTH } from '../cases.models';

/**
 * Presentational: start / approve / reject chrome (KYC-065).
 * Reject field tree comes from the smart parent (Signal Form).
 */
@Component({
  selector: 'app-case-review-actions-pane',
  imports: [FormField, MatButtonModule, MatFormFieldModule, MatInputModule],
  templateUrl: './case-review-actions-pane.html',
  styleUrl: './case-review-actions-pane.css',
})
export class CaseReviewActionsPane {
  readonly actions = input.required<CaseReviewActions>();
  readonly actionError = input<string | null>(null);
  readonly actionBusy = input<boolean>(false);
  readonly noActionsMessage = input.required<string>();
  readonly rejectCommentField = input.required<FieldTree<string>>();
  readonly approveComment = model<string>('');

  readonly startReview = output<void>();
  readonly approve = output<void>();
  readonly reject = output<void>();

  protected readonly copy = CASES_REVIEW_MESSAGES;
  protected readonly commentMaxLength: number = REVIEW_COMMENT_MAX_LENGTH;
  protected readonly rejectHint: string = rejectCommentHint();
  protected readonly rejectMaxLengthError: string = rejectCommentMaxLengthMessage();

  protected onApproveCommentInput(event: Event): void {
    const target: EventTarget | null = event.target;
    if (target instanceof HTMLTextAreaElement) {
      this.approveComment.set(target.value);
    }
  }
}
