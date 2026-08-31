import { DatePipe } from '@angular/common';
import {
  Component,
  DestroyRef,
  OnInit,
  WritableSignal,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EMPTY, Observable, Subject, catchError, switchMap, tap } from 'rxjs';
import { UI_MESSAGES } from '../../shared/ui.messages';
import {
  isCaseId,
  normalizeOptionalReviewComment,
  normalizeRejectComment,
  resolveReviewActions,
  toCaseActionError,
  toCaseDownloadError,
  toCasesLoadError,
} from '../cases.mappers';
import {
  CASES_REVIEW_MESSAGES,
  caseStatusLabel,
  noReviewActionsMessage,
  rejectCommentHint,
  rejectCommentMaxLengthMessage,
} from '../cases.messages';
import {
  CaseDetail,
  CaseDocument,
  CaseReviewActions,
  RejectFormControls,
  REVIEW_COMMENT_MAX_LENGTH,
} from '../cases.models';
import { CasesService } from '../cases.service';

/**
 * Case review detail: form data, documents + download, start / approve / reject (KYC-063).
 * Status rules via pure `resolveReviewActions`; HTTP in CasesService only.
 */
@Component({
  selector: 'app-case-review',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './case-review.html',
  styleUrl: './case-review.css',
})
export class CaseReview implements OnInit {
  private readonly casesService: CasesService = inject(CasesService);
  private readonly route: ActivatedRoute = inject(ActivatedRoute);
  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly destroyRef: DestroyRef = inject(DestroyRef);

  private readonly loadRequests: Subject<string> = new Subject<string>();

  protected caseId: string = '';

  protected readonly copy = CASES_REVIEW_MESSAGES;
  protected readonly sharedCopy = UI_MESSAGES;
  protected readonly commentMaxLength: number = REVIEW_COMMENT_MAX_LENGTH;
  protected readonly rejectHint: string = rejectCommentHint();
  protected readonly rejectMaxLengthError: string = rejectCommentMaxLengthMessage();

  protected readonly detail: WritableSignal<CaseDetail | null> = signal(null);
  protected readonly loading: WritableSignal<boolean> = signal(false);
  protected readonly loadError: WritableSignal<string | null> = signal(null);
  protected readonly actionError: WritableSignal<string | null> = signal(null);
  protected readonly actionBusy: WritableSignal<boolean> = signal(false);
  protected readonly downloadError: WritableSignal<string | null> = signal(null);
  protected readonly downloadingId: WritableSignal<string | null> = signal(null);

  protected readonly rejectForm: FormGroup<RejectFormControls> = this.fb.nonNullable.group({
    comment: ['', [Validators.required, Validators.maxLength(REVIEW_COMMENT_MAX_LENGTH)]],
  });

  protected readonly approveComment: WritableSignal<string> = signal('');

  protected readonly actions = computed((): CaseReviewActions => {
    const current: CaseDetail | null = this.detail();
    if (!current) {
      return { canStartReview: false, canApprove: false, canReject: false };
    }
    return resolveReviewActions(current.status);
  });

  protected readonly statusLabel = computed((): string => {
    const current: CaseDetail | null = this.detail();
    return current ? caseStatusLabel(current.status) : '';
  });

  protected readonly noActionsMessage = computed((): string =>
    noReviewActionsMessage(this.statusLabel()),
  );

  ngOnInit(): void {
    this.loadRequests
      .pipe(
        tap((): void => {
          this.loading.set(true);
          this.loadError.set(null);
          this.actionError.set(null);
          this.downloadError.set(null);
        }),
        switchMap(
          (id: string): Observable<CaseDetail> =>
            this.casesService.getById(id).pipe(
              catchError((err: unknown): Observable<never> => {
                this.loading.set(false);
                this.detail.set(null);
                this.loadError.set(toCasesLoadError(err).message);
                return EMPTY;
              }),
            ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((page: CaseDetail): void => {
        this.detail.set(page);
        this.loading.set(false);
      });

    const idParam: string | null = this.route.snapshot.paramMap.get('caseId');
    if (!idParam || !isCaseId(idParam)) {
      this.loadError.set(CASES_REVIEW_MESSAGES.invalidCaseLink);
      return;
    }
    this.caseId = idParam;
    this.loadRequests.next(idParam);
  }

  protected reload(): void {
    if (!this.caseId) {
      return;
    }
    this.loadRequests.next(this.caseId);
  }

  protected startReview(): void {
    if (!this.actions().canStartReview || this.actionBusy()) {
      return;
    }
    this.runAction((): Observable<unknown> => this.casesService.startReview(this.caseId));
  }

  protected approve(): void {
    if (!this.actions().canApprove || this.actionBusy()) {
      return;
    }
    const normalized = normalizeOptionalReviewComment(this.approveComment());
    if (!normalized.ok) {
      this.actionError.set(normalized.message);
      return;
    }
    this.runAction(
      (): Observable<unknown> => this.casesService.approve(this.caseId, normalized.comment),
    );
  }

  protected reject(): void {
    if (!this.actions().canReject || this.actionBusy()) {
      return;
    }
    this.rejectForm.markAllAsTouched();
    const normalized = normalizeRejectComment(this.rejectForm.controls.comment.getRawValue());
    if (!normalized.ok) {
      this.actionError.set(normalized.message);
      return;
    }
    this.runAction(
      (): Observable<unknown> => this.casesService.reject(this.caseId, normalized.comment),
    );
  }

  protected onApproveCommentInput(event: Event): void {
    const target: EventTarget | null = event.target;
    if (target instanceof HTMLTextAreaElement) {
      this.approveComment.set(target.value);
    }
  }

  protected download(doc: CaseDocument): void {
    if (this.downloadingId()) {
      return;
    }
    this.downloadError.set(null);
    this.downloadingId.set(doc.id);

    this.casesService
      .downloadDocument(this.caseId, doc.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (blob: Blob): void => {
          this.downloadingId.set(null);
          triggerBlobDownload(blob, doc.fileName);
        },
        error: (err: unknown): void => {
          this.downloadingId.set(null);
          this.downloadError.set(toCaseDownloadError(err).message);
        },
      });
  }

  private runAction(factory: () => Observable<unknown>): void {
    this.actionBusy.set(true);
    this.actionError.set(null);
    factory()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (): void => {
          this.actionBusy.set(false);
          this.rejectForm.reset({ comment: '' });
          this.approveComment.set('');
          this.reload();
        },
        error: (err: unknown): void => {
          this.actionBusy.set(false);
          this.actionError.set(toCaseActionError(err).message);
        },
      });
  }
}

/** Browser-only side effect: save a blob as a file download. */
function triggerBlobDownload(blob: Blob, fileName: string): void {
  const objectUrl: string = URL.createObjectURL(blob);
  const anchor: HTMLAnchorElement = document.createElement('a');
  anchor.href = objectUrl;
  anchor.download = fileName;
  anchor.rel = 'noopener';
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(objectUrl);
}
