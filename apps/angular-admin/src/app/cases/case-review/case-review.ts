import { DatePipe } from '@angular/common';
import {
  Component,
  DestroyRef,
  ResourceLoaderParams,
  ResourceRef,
  Signal,
  WritableSignal,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, rxResource, toSignal } from '@angular/core/rxjs-interop';
import type { RxResourceOptions } from '@angular/core/rxjs-interop';
import { FieldTree, form } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, ParamMap, RouterLink } from '@angular/router';
import { Observable, map } from 'rxjs';
import { toShellSession } from '../../auth/auth.mappers';
import { AppRole } from '../../auth/auth.models';
import { TokenStorage } from '../../auth/token-storage';
import { UI_MESSAGES } from '../../shared/ui.messages';
import { CaseDocumentsPane } from '../case-documents-pane/case-documents-pane';
import { CaseFormDataPane } from '../case-form-data-pane/case-form-data-pane';
import { CaseReviewActionsPane } from '../case-review-actions-pane/case-review-actions-pane';
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
} from '../cases.messages';
import {
  CaseDetail,
  CaseDocument,
  CaseReviewActions,
  RejectCommentModel,
} from '../cases.models';
import { rejectCommentSchema } from '../cases.reject-schema';
import { CasesService } from '../cases.service';

/**
 * Smart case review route (KYC-063 product + KYC-065 structure):
 * `rxResource` read path, Signal Form reject comment, presentational panes.
 */
@Component({
  selector: 'app-case-review',
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatProgressSpinnerModule,
    CaseFormDataPane,
    CaseDocumentsPane,
    CaseReviewActionsPane,
  ],
  templateUrl: './case-review.html',
  styleUrl: './case-review.css',
})
export class CaseReview {
  private readonly casesService: CasesService = inject(CasesService);
  private readonly route: ActivatedRoute = inject(ActivatedRoute);
  private readonly tokens: TokenStorage = inject(TokenStorage);
  private readonly destroyRef: DestroyRef = inject(DestroyRef);

  private readonly routeCaseIdParam: Signal<string | undefined> = toSignal(
    this.route.paramMap.pipe(
      map((params: ParamMap): string | undefined => {
        const raw: string | null = params.get('caseId');
        if (!raw || !isCaseId(raw)) {
          return undefined;
        }
        return raw;
      }),
    ),
    { initialValue: readRouteCaseId(this.route) },
  );

  protected readonly caseId: Signal<string> = computed(
    (): string => this.routeCaseIdParam() ?? '',
  );

  protected readonly copy: typeof CASES_REVIEW_MESSAGES = CASES_REVIEW_MESSAGES;
  protected readonly sharedCopy: typeof UI_MESSAGES = UI_MESSAGES;

  private readonly caseIdParam: Signal<string | undefined> = this.routeCaseIdParam;

  protected readonly caseResource: ResourceRef<CaseDetail | undefined> = rxResource({
    // `undefined` params idle the resource (Angular runtime); typings omit the sentinel.
    params: (): string | undefined => this.caseIdParam(),
    stream: (loader: ResourceLoaderParams<string>): Observable<CaseDetail> =>
      this.casesService.getById(loader.params),
  } as RxResourceOptions<CaseDetail, string>);

  protected readonly rejectModel: WritableSignal<RejectCommentModel> = signal({ comment: '' });
  protected readonly rejectForm: FieldTree<RejectCommentModel> = form(
    this.rejectModel,
    rejectCommentSchema,
  );

  protected readonly approveComment: WritableSignal<string> = signal('');
  protected readonly actionError: WritableSignal<string | null> = signal(null);
  protected readonly actionBusy: WritableSignal<boolean> = signal(false);
  protected readonly downloadError: WritableSignal<string | null> = signal(null);
  protected readonly downloadingId: WritableSignal<string | null> = signal(null);

  protected readonly detail: Signal<CaseDetail | null> = computed((): CaseDetail | null => {
    if (!this.caseResource.hasValue()) {
      return null;
    }
    return this.caseResource.value() ?? null;
  });

  protected readonly loading: Signal<boolean> = computed((): boolean => this.caseResource.isLoading());

  protected readonly loadError: Signal<string | null> = computed((): string | null => {
    if (!this.routeCaseIdParam()) {
      return CASES_REVIEW_MESSAGES.invalidCaseLink;
    }
    const err: Error | undefined = this.caseResource.error();
    return err ? toCasesLoadError(err).message : null;
  });

  protected readonly callerRole: Signal<AppRole | null> = computed((): AppRole | null => {
    const session = toShellSession(this.tokens.getAccessToken(), this.tokens.getTenantSlug());
    return session?.role ?? null;
  });

  protected readonly actions: Signal<CaseReviewActions> = computed((): CaseReviewActions => {
    const current: CaseDetail | null = this.detail();
    if (!current) {
      return { canStartReview: false, canApprove: false, canReject: false };
    }
    return resolveReviewActions(current.status, this.callerRole());
  });

  protected readonly statusLabel: Signal<string> = computed((): string => {
    const current: CaseDetail | null = this.detail();
    return current ? caseStatusLabel(current.status) : '';
  });

  protected readonly noActionsMessage: Signal<string> = computed((): string =>
    noReviewActionsMessage(this.statusLabel()),
  );

  protected reload(): void {
    this.actionError.set(null);
    this.downloadError.set(null);
    this.caseResource.reload();
  }

  protected startReview(): void {
    if (!this.actions().canStartReview || this.actionBusy()) {
      return;
    }
    this.runAction((): Observable<unknown> => this.casesService.startReview(this.caseId()));
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
      (): Observable<unknown> => this.casesService.approve(this.caseId(), normalized.comment),
    );
  }

  protected reject(): void {
    if (!this.actions().canReject || this.actionBusy()) {
      return;
    }
    this.rejectForm.comment().markAsTouched();
    const normalized = normalizeRejectComment(this.rejectModel().comment);
    if (!normalized.ok) {
      this.actionError.set(normalized.message);
      return;
    }
    this.runAction(
      (): Observable<unknown> => this.casesService.reject(this.caseId(), normalized.comment),
    );
  }

  protected download(doc: CaseDocument): void {
    if (this.downloadingId()) {
      return;
    }
    this.downloadError.set(null);
    this.downloadingId.set(doc.id);

    this.casesService
      .downloadDocument(this.caseId(), doc.id)
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
          this.rejectForm.comment().reset('');
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

function readRouteCaseId(route: ActivatedRoute): string | undefined {
  const idParam: string | null = route.snapshot.paramMap.get('caseId');
  if (!idParam || !isCaseId(idParam)) {
    return undefined;
  }
  return idParam;
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
  window.setTimeout((): void => {
    URL.revokeObjectURL(objectUrl);
  }, 1_000);
}
