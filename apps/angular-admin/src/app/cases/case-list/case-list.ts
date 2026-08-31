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
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { EMPTY, Observable, Subject, catchError, switchMap, tap } from 'rxjs';
import { UI_MESSAGES } from '../../shared/ui.messages';
import { parseStatusFilterValue, toCasesLoadError } from '../cases.mappers';
import {
  CASE_STATUS_LABELS,
  CASES_LIST_MESSAGES,
  casesCountLabel,
  casesEmptyForStatusLabel,
} from '../cases.messages';
import { CASE_STATUSES, CaseListItem, CaseListPage, CaseStatus } from '../cases.models';
import { CasesService } from '../cases.service';

/**
 * Reviewer / TenantAdmin case list with status filter (KYC-062).
 * Row opens case review (KYC-063). Chrome / logout live in AdminShell (KYC-064).
 * Overlapping reloads cancel via `switchMap` so only the latest response updates UI.
 */
@Component({
  selector: 'app-case-list',
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatTableModule,
  ],
  templateUrl: './case-list.html',
  styleUrl: './case-list.css',
})
export class CaseList implements OnInit {
  private readonly casesService: CasesService = inject(CasesService);
  private readonly router: Router = inject(Router);
  private readonly destroyRef: DestroyRef = inject(DestroyRef);

  private readonly reloadRequests: Subject<void> = new Subject<void>();

  protected readonly copy = CASES_LIST_MESSAGES;
  protected readonly sharedCopy = UI_MESSAGES;
  protected readonly statusOptions: readonly CaseStatus[] = CASE_STATUSES;
  protected readonly statusLabels: Readonly<Record<CaseStatus, string>> = CASE_STATUS_LABELS;
  protected readonly displayedColumns: readonly string[] = [
    'title',
    'customerEmail',
    'status',
    'updatedAt',
  ];

  protected readonly statusFilter: WritableSignal<CaseStatus | null> = signal(null);
  protected readonly items: WritableSignal<CaseListItem[]> = signal([]);
  protected readonly totalCount: WritableSignal<number> = signal(0);
  protected readonly loading: WritableSignal<boolean> = signal(false);
  protected readonly loadError: WritableSignal<string | null> = signal(null);

  protected readonly isEmpty = computed(
    (): boolean => !this.loading() && this.loadError() === null && this.items().length === 0,
  );

  protected readonly countLabel = computed((): string => casesCountLabel(this.totalCount()));

  protected readonly emptyMessage = computed((): string => {
    const status: CaseStatus | null = this.statusFilter();
    return status ? casesEmptyForStatusLabel(status) : CASES_LIST_MESSAGES.emptyAll;
  });

  ngOnInit(): void {
    this.reloadRequests
      .pipe(
        tap((): void => {
          this.loading.set(true);
          this.loadError.set(null);
        }),
        switchMap(
          (): Observable<CaseListPage> =>
            this.casesService.list({ status: this.statusFilter() }).pipe(
              catchError((err: unknown): Observable<never> => {
                this.loading.set(false);
                this.items.set([]);
                this.totalCount.set(0);
                this.loadError.set(toCasesLoadError(err).message);
                return EMPTY;
              }),
            ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((page: CaseListPage): void => {
        this.items.set(page.items);
        this.totalCount.set(page.totalCount);
        this.loading.set(false);
      });

    this.reload();
  }

  protected filterStatus(status: CaseStatus | null): void {
    this.statusFilter.set(status);
    this.reload();
  }

  protected onStatusFilterChange(value: unknown): void {
    const parsed: CaseStatus | null | undefined = parseStatusFilterValue(value);
    if (parsed === undefined) {
      return;
    }
    this.filterStatus(parsed);
  }

  protected reload(): void {
    this.reloadRequests.next();
  }

  protected openCase(row: CaseListItem): void {
    void this.router.navigate(['/cases', row.id]);
  }
}
