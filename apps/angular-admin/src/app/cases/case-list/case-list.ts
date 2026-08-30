import { DatePipe } from '@angular/common';
import {
  Component,
  OnInit,
  WritableSignal,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { TokenStorage } from '../../auth/token-storage';
import { parseStatusFilterValue, toCasesLoadError } from '../cases.mappers';
import {
  CASE_STATUSES,
  CASE_STATUS_LABELS,
  CaseListItem,
  CaseStatus,
  caseStatusLabel,
} from '../cases.models';
import { CasesService } from '../cases.service';

/**
 * Reviewer / TenantAdmin case list with status filter (KYC-062).
 * UI state via signals — no SignalStore (see frontend-code-standards).
 */
@Component({
  selector: 'app-case-list',
  imports: [
    DatePipe,
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
  private readonly tokens: TokenStorage = inject(TokenStorage);
  private readonly router: Router = inject(Router);

  protected readonly pageTitle: string = 'Cases';
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

  ngOnInit(): void {
    this.reload();
  }

  protected statusLabel(status: CaseStatus): string {
    return caseStatusLabel(status);
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
    this.loading.set(true);
    this.loadError.set(null);

    this.casesService.list({ status: this.statusFilter() }).subscribe({
      next: (page): void => {
        this.items.set(page.items);
        this.totalCount.set(page.totalCount);
        this.loading.set(false);
      },
      error: (err: unknown): void => {
        this.loading.set(false);
        this.items.set([]);
        this.totalCount.set(0);
        this.loadError.set(toCasesLoadError(err).message);
      },
    });
  }

  protected signOut(): void {
    this.tokens.clearAccessToken();
    void this.router.navigateByUrl('/login');
  }
}
