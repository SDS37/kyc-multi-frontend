import { schema, required, maxLength } from '@angular/forms/signals';
import { CASES_REVIEW_MESSAGES, rejectCommentMaxLengthMessage } from './cases.messages';
import { REVIEW_COMMENT_MAX_LENGTH, RejectCommentModel } from './cases.models';

/** Signal Forms schema for the reject comment (KYC-065). */
export const rejectCommentSchema = schema<RejectCommentModel>((path) => {
  required(path.comment, { message: CASES_REVIEW_MESSAGES.rejectCommentRequired });
  maxLength(path.comment, REVIEW_COMMENT_MAX_LENGTH, {
    message: rejectCommentMaxLengthMessage(),
  });
});
