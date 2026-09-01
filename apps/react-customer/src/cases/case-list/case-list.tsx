import {
  type ReactElement,
  type SubmitEvent,
  type RefObject,
  useCallback,
  useEffect,
  useRef,
  useState,
} from 'react';
import { useNavigate, type NavigateFunction } from 'react-router';
import { createDraftCase, listCases } from '../cases-api';
import {
  toCasesLoadError,
  toCreateDraftError,
  validateCreateDraftTitle,
} from '../cases.mappers';
import {
  CASES_LIST_MESSAGES,
  type CasesListMessages,
  casesCountLabel,
  casesEmptyForStatusLabel,
} from '../cases.messages';
import {
  type CaseListItem,
  type CaseListPage,
  type CaseStatus,
  type CreatedDraftCase,
} from '../cases.models';
import { CaseListTable } from './case-list-table';
import styles from './case-list.module.css';
import { CasesEmpty } from './cases-empty';
import { CasesLoadError } from './cases-load-error';
import { CasesLoading } from './cases-loading';
import { CasesToolbar } from './cases-toolbar';
import { CreateDraftDialog } from './create-draft-dialog';

/**
 * Smart screen: customer my-cases list + create draft (KYC-072).
 * Own-only filtering is enforced by the API JWT — client never sends user ids.
 */
export function CaseList(): ReactElement {
  const copy: CasesListMessages = CASES_LIST_MESSAGES;
  const navigate: NavigateFunction = useNavigate();
  const loadSeq: RefObject<number> = useRef(0);
  const createLock: RefObject<boolean> = useRef(false);
  const creatingRef: RefObject<boolean> = useRef(false);

  const [statusFilter, setStatusFilter] = useState<CaseStatus | null>(null);
  const [items, setItems] = useState<readonly CaseListItem[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [loading, setLoading] = useState<boolean>(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [createOpen, setCreateOpen] = useState<boolean>(false);
  const [createTitle, setCreateTitle] = useState<string>('');
  const [createTouched, setCreateTouched] = useState<boolean>(false);
  const [creating, setCreating] = useState<boolean>(false);
  const [createError, setCreateError] = useState<string | null>(null);

  const loadCases = useCallback(async (status: CaseStatus | null): Promise<void> => {
    const seq: number = loadSeq.current + 1;
    loadSeq.current = seq;
    setLoading(true);
    setLoadError(null);
    try {
      const page: CaseListPage = await listCases({ status });
      if (loadSeq.current !== seq) {
        return;
      }
      setItems(page.items);
      setTotalCount(page.totalCount);
      setLoading(false);
    } catch (err: unknown) {
      if (loadSeq.current !== seq) {
        return;
      }
      setItems([]);
      setTotalCount(0);
      setLoading(false);
      setLoadError(toCasesLoadError(err).message);
    }
  }, []);

  useEffect((): void => {
    void loadCases(statusFilter);
  }, [loadCases, statusFilter]);

  const isEmpty: boolean = !loading && loadError === null && items.length === 0;
  const countText: string | null = loading
    ? copy.loading
    : loadError !== null
      ? null
      : casesCountLabel(items.length, totalCount);
  const emptyMessage: string = statusFilter
    ? casesEmptyForStatusLabel(statusFilter)
    : copy.emptyAll;
  const createTitleError: string | null = validateCreateDraftTitle(createTitle);

  function openCreateDialog(): void {
    setCreateTitle('');
    setCreateTouched(false);
    setCreateError(null);
    setCreateOpen(true);
  }

  const closeCreateDialog = useCallback((): void => {
    if (creatingRef.current) {
      return;
    }
    setCreateOpen(false);
  }, []);

  async function onCreateSubmit(event: SubmitEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setCreateTouched(true);
    setCreateError(null);
    if (createTitleError !== null || createLock.current) {
      return;
    }

    createLock.current = true;
    creatingRef.current = true;
    setCreating(true);
    try {
      const created: CreatedDraftCase = await createDraftCase({ title: createTitle });
      creatingRef.current = false;
      setCreating(false);
      createLock.current = false;
      setCreateOpen(false);
      void navigate(`/cases/${created.id}`);
    } catch (err: unknown) {
      creatingRef.current = false;
      createLock.current = false;
      setCreating(false);
      setCreateError(toCreateDraftError(err).message);
    }
  }

  return (
    <main className={styles['cases']}>
      <header className={styles['header']}>
        <div>
          <h1 id="cases-heading" className={styles['title']}>
            {copy.pageTitle}
          </h1>
          <p className={styles['lede']}>{copy.lede}</p>
        </div>
        <button type="button" className={styles['create']} onClick={openCreateDialog}>
          {copy.createAction}
        </button>
      </header>

      <CasesToolbar
        statusFilter={statusFilter}
        countText={countText}
        onStatusChange={setStatusFilter}
      />

      {loadError !== null ? (
        <CasesLoadError
          message={loadError}
          onRetry={(): void => {
            void loadCases(statusFilter);
          }}
        />
      ) : loading ? (
        <CasesLoading />
      ) : isEmpty ? (
        <CasesEmpty message={emptyMessage} />
      ) : (
        <CaseListTable items={items} labelledBy="cases-heading" />
      )}

      {createOpen ? (
        <CreateDraftDialog
          title={createTitle}
          titleError={createTitleError}
          touched={createTouched}
          creating={creating}
          formError={createError}
          onTitleChange={setCreateTitle}
          onClose={closeCreateDialog}
          onSubmit={onCreateSubmit}
        />
      ) : null}
    </main>
  );
}
